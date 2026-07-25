using SkillMeter.Model;
using SkillMeter.Tokenizing;

namespace SkillMeter.Scanning;

/// <summary>
/// Discovers skills across every agent location we know about, and measures them.
///
/// This tool is strictly READ-ONLY. It never writes to, installs into, or otherwise
/// mutates agent configuration. (A prior tool in this space shipped a command that
/// rewrote ~/.claude/settings.json having, by its author's own admission, never been
/// run against a real one. We do not touch your config at all.)
/// </summary>
public sealed class SkillScanner(ITokenCounter tokens, int maxDescChars = Constants.DefaultMaxDescChars)
{
    /// <summary>Directories never worth walking into.</summary>
    private static readonly string[] ExcludedSegments =
    [
        "node_modules", ".git", "bin", "obj", "dist", "build", ".venv", "venv",
        // Some packs vendor a copy of their own upstream. Counting those inflates
        // totals by ~50%: managedcode/dotnet-skills reads as 279 skills instead of 179.
        "external-sources",
    ];

    private readonly List<SkippedPath> _skipped = [];

    /// <summary>
    /// Paths this scan could not read. Accumulates across every root scanned by this
    /// instance, so a caller measures exactly what was missed.
    /// </summary>
    public IReadOnlyList<SkippedPath> Skipped => _skipped;

    private void Skip(string path, string reason) => _skipped.Add(new SkippedPath(path, reason));

    private void Skip(string path, Exception ex) => Skip(path, Describe(ex, path));

    /// <summary>
    /// Short reason text for the two exception types every filesystem call here can
    /// throw. .NET embeds the full path in IO messages, but the path is already
    /// reported in its own field, so repeating it doubles the length of every line
    /// and pushes the text report out of its layout.
    /// </summary>
    private static string Describe(Exception ex, string path)
    {
        if (ex is UnauthorizedAccessException) return "permission denied";

        var message = ex.Message.Replace($"'{path}'", "").Replace(path, "");
        var collapsed = string.Join(' ', message.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        return collapsed.Length == 0 ? "unreadable" : collapsed;
    }

    /// <summary>
    /// Skill roots, in the order agents read them. Relative to <paramref name="projectDir"/>
    /// and the user's home directory.
    /// </summary>
    public static IEnumerable<SkillRoot> CandidateRoots(string projectDir)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        // Project scope
        yield return new(Path.Combine(projectDir, ".agents", "skills"), AgentKind.Portable, "project");
        yield return new(Path.Combine(projectDir, ".claude", "skills"), AgentKind.ClaudeCode, "project");
        yield return new(Path.Combine(projectDir, ".cursor", "skills"), AgentKind.Cursor, "project");
        yield return new(Path.Combine(projectDir, ".github", "skills"), AgentKind.Copilot, "project");
        yield return new(Path.Combine(projectDir, ".gemini", "skills"), AgentKind.Gemini, "project");

        if (string.IsNullOrEmpty(home)) yield break;

        // User scope
        yield return new(Path.Combine(home, ".agents", "skills"), AgentKind.Portable, "user");
        yield return new(Path.Combine(home, ".claude", "skills"), AgentKind.ClaudeCode, "user");
        yield return new(Path.Combine(home, ".cursor", "skills"), AgentKind.Cursor, "user");
        yield return new(Path.Combine(home, ".codex", "skills"), AgentKind.Codex, "user");
        yield return new(Path.Combine(home, ".config", "agents", "skills"), AgentKind.Portable, "user");
    }

    /// <summary>Scans the standard agent locations.</summary>
    public IReadOnlyList<Skill> ScanDefaultRoots(string projectDir)
    {
        var results = new List<Skill>();
        foreach (var root in CandidateRoots(projectDir))
        {
            if (Directory.Exists(root.Path))
                results.AddRange(ScanRoot(root));
        }
        return Deduplicate(results);
    }

    /// <summary>Scans an explicit directory, e.g. a cloned skills pack.</summary>
    public IReadOnlyList<Skill> ScanPath(string path)
    {
        var full = Path.GetFullPath(path);
        if (!Directory.Exists(full))
            throw new DirectoryNotFoundException($"No such directory: {full}");
        return Deduplicate(ScanRoot(new SkillRoot(full, AgentKind.Unknown, "explicit")).ToList());
    }

    private IEnumerable<Skill> ScanRoot(SkillRoot root)
    {
        foreach (var file in EnumerateSkillFiles(root.Path))
        {
            Skill? skill = null;
            try
            {
                skill = Measure(file, root);
            }
            // Still skipped rather than aborting the whole scan, but no longer in
            // silence: the caller can see what was missed and --strict can fail on it.
            catch (IOException ex) { Skip(file, ex); }
            catch (UnauthorizedAccessException ex) { Skip(file, ex); }

            if (skill is not null) yield return skill;
        }
    }

    private IEnumerable<string> EnumerateSkillFiles(string root)
    {
        var stack = new Stack<string>();
        stack.Push(root);

        // Packs commonly mirror one skills/ directory into several per-agent paths
        // using symlinks — addyosmani/agent-skills symlinks .opencode/skills ->
        // ../skills/, which naively reads as 48 skills instead of 24. Tracking the
        // resolved physical directory both deduplicates that and makes symlink
        // cycles impossible.
        var visited = new HashSet<string>(OrdinalPathComparer);

        while (stack.Count > 0)
        {
            var dir = stack.Pop();

            if (!visited.Add(RealPath(dir))) continue;

            string[] entries;
            // A permission-denied subtree silently removed every skill beneath it.
            try { entries = Directory.GetDirectories(dir); }
            catch (UnauthorizedAccessException ex) { Skip(dir, ex); continue; }
            catch (IOException ex) { Skip(dir, ex); continue; }

            foreach (var sub in entries)
            {
                var name = Path.GetFileName(sub);

                // Exclude the *contents* of these directories, but still allow a
                // skill legitimately named e.g. "build" — checking for its SKILL.md
                // before skipping means we no longer silently drop it.
                if (ExcludedSegments.Contains(name, StringComparer.OrdinalIgnoreCase)
                    && !File.Exists(Path.Combine(sub, "SKILL.md")))
                {
                    continue;
                }

                stack.Push(sub);
            }

            var candidate = Path.Combine(dir, "SKILL.md");
            if (!File.Exists(candidate)) continue;

            // A FIFO or device named SKILL.md is refused deliberately, but that is a
            // measurement gap the user should hear about rather than a non-event.
            if (IsRegularFile(candidate)) yield return candidate;
            else Skip(candidate, "not a regular file (device or FIFO)");
        }
    }

    /// <summary>
    /// Fully resolved physical path, following symlinks in EVERY path component —
    /// not just the last one.
    ///
    /// This is deliberately hand-rolled. FileSystemInfo.ResolveLinkTarget only
    /// inspects the final component, and for a relative link target it reports a
    /// FullName resolved against the process working directory rather than against
    /// the link's own directory. Both behaviours break deduplication here: the
    /// common real-world case is a symlinked *parent* (.opencode/skills -> ../skills/),
    /// where the leaf directories are not links at all.
    /// </summary>
    internal static string RealPath(string path)
    {
        const int maxLinkHops = 32;

        try
        {
            var full = Path.GetFullPath(path);
            var root = Path.GetPathRoot(full);
            if (string.IsNullOrEmpty(root)) return full;

            // Following a link can introduce a whole new prefix that may itself
            // contain links (/tmp -> /private/tmp on macOS is the everyday case),
            // so resolution restarts from the top whenever a link is followed
            // rather than continuing through the original segment list.
            var pending = new Queue<string>(
                full[root.Length..].Split(
                    [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                    StringSplitOptions.RemoveEmptyEntries));

            var current = root;
            var hops = 0;

            while (pending.Count > 0)
            {
                current = Path.Combine(current, pending.Dequeue());

                var target = LinkTargetOf(current);
                if (target is null) continue;
                if (++hops > maxLinkHops) break;

                var resolved = Path.IsPathRooted(target)
                    ? Path.GetFullPath(target)
                    // Relative targets are relative to the link's own directory.
                    : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(current) ?? root, target));

                // Re-queue the resolved prefix ahead of whatever is left to walk.
                var newRoot = Path.GetPathRoot(resolved);
                if (string.IsNullOrEmpty(newRoot)) break;

                var remaining = pending.ToArray();
                pending.Clear();

                foreach (var seg in resolved[newRoot.Length..].Split(
                             [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                             StringSplitOptions.RemoveEmptyEntries))
                {
                    pending.Enqueue(seg);
                }

                foreach (var seg in remaining) pending.Enqueue(seg);

                current = newRoot;
            }

            return current;
        }
        catch (IOException) { return path; }
        catch (UnauthorizedAccessException) { return path; }
        catch (ArgumentException) { return path; }
        catch (NotSupportedException) { return path; }
    }

    /// <summary>Raw link target for a path, or null when it is not a link.</summary>
    private static string? LinkTargetOf(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                var d = new DirectoryInfo(path);
                return d.LinkTarget;
            }

            var f = new FileInfo(path);
            return f.Exists ? f.LinkTarget : null;
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    /// <summary>Paths are case-insensitive on Windows and macOS, case-sensitive elsewhere.</summary>
    private static StringComparer OrdinalPathComparer =>
        OperatingSystem.IsLinux() ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;

    private Skill Measure(string file, SkillRoot root)
    {
        var text = File.ReadAllText(file);
        var (fields, body) = FrontmatterParser.Parse(text);

        var dir = Path.GetDirectoryName(file)!;
        var dirName = Path.GetFileName(dir);

        fields.TryGetValue("name", out var name);
        fields.TryGetValue("description", out var description);
        fields.TryGetValue("when_to_use", out var whenToUse);

        name = string.IsNullOrWhiteSpace(name) ? dirName : name.Trim();
        description ??= "";
        whenToUse ??= "";

        // Claude Code caps the COMBINED description + when_to_use text per skill.
        var listingText = string.IsNullOrEmpty(whenToUse)
            ? description
            : description + " " + whenToUse;

        var truncated = listingText.Length > maxDescChars;
        if (truncated) listingText = listingText[..maxDescChars];

        var (resourceTokens, resourceFiles) = MeasureResources(dir);

        // A skill the model cannot auto-invoke never enters the listing, so it
        // costs nothing per session. Counting them overstated the corpus by 515
        // tokens, which is enough to flip a --fail-on verdict on a small install.
        var listedInBudget = !IsTruthy(fields.GetValueOrDefault("disable-model-invocation"));

        return new Skill
        {
            Name = name,
            CountsTowardListing = listedInBudget,
            Path = file,
            Root = root,
            Description = description,
            WhenToUse = whenToUse,
            HasFrontmatter = fields.Count > 0,
            NameMismatch = fields.Count > 0
                           && !string.Equals(name, dirName, StringComparison.Ordinal),
            ListingTruncated = truncated,
            MetadataTokens = listedInBudget ? tokens.Count(name) + tokens.Count(listingText) : 0,
            BodyTokens = tokens.Count(body),
            BodyLines = CountLines(body),
            ResourceTokens = resourceTokens,
            ResourceFiles = resourceFiles,
        };
    }

    /// <summary>
    /// Measures bundled markdown the agent may load on demand.
    ///
    /// Walks explicitly rather than using EnumerateFiles(AllDirectories) so that the
    /// same exclusions, symlink deduplication and nested-skill rules that govern
    /// discovery also govern resource accounting. The recursive enumerator applied
    /// none of them: a `references/up -> ..` link recounted the same file 40 times,
    /// and a vendored node_modules inside a skill was billed as skill weight.
    /// </summary>
    private (int Tokens, int Files) MeasureResources(string skillDir)
    {
        var total = 0;
        var count = 0;

        var stack = new Stack<string>();
        stack.Push(skillDir);
        var visited = new HashSet<string>(OrdinalPathComparer);

        while (stack.Count > 0)
        {
            var dir = stack.Pop();
            if (!visited.Add(RealPath(dir))) continue;

            try
            {
                foreach (var sub in Directory.GetDirectories(dir))
                {
                    var name = Path.GetFileName(sub);
                    if (ExcludedSegments.Contains(name, StringComparer.OrdinalIgnoreCase)) continue;

                    // Anything at or below another SKILL.md belongs to that skill,
                    // at any depth — not just as an immediate child.
                    if (File.Exists(Path.Combine(sub, "SKILL.md"))) continue;

                    stack.Push(sub);
                }
            }
            catch (IOException ex) { Skip(dir, ex); }
            catch (UnauthorizedAccessException ex) { Skip(dir, ex); }

            string[] files;
            try { files = Directory.GetFiles(dir, "*.md"); }
            catch (IOException ex) { Skip(dir, ex); continue; }
            catch (UnauthorizedAccessException ex) { Skip(dir, ex); continue; }

            foreach (var file in files)
            {
                if (string.Equals(Path.GetFileName(file), "SKILL.md", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!IsRegularFile(file)) continue;
                if (!visited.Add(RealPath(file))) continue;

                // Caught per file rather than per directory: one unreadable resource
                // used to abandon every remaining file in the same directory too.
                try
                {
                    total += tokens.Count(File.ReadAllText(file));
                    count++;
                }
                catch (IOException ex) { Skip(file, ex); }
                catch (UnauthorizedAccessException ex) { Skip(file, ex); }
            }
        }

        return (total, count);
    }

    /// <summary>
    /// True for an ordinary file. Guards against FIFOs and character devices, where
    /// File.Exists is true but ReadAllText blocks forever or exhausts memory.
    /// </summary>
    private static bool IsRegularFile(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists) return false;

            // UnixFileMode is meaningless on Windows, where these device types
            // are not reachable through an ordinary path anyway.
            if (OperatingSystem.IsWindows()) return true;

            var attrs = File.GetAttributes(path);
            return (attrs & (FileAttributes.Device | FileAttributes.Directory)) == 0;
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    /// <summary>YAML truthiness for the boolean-ish frontmatter flags.</summary>
    private static bool IsTruthy(string? value) =>
        value is not null
        && value.Trim().ToLowerInvariant() is "true" or "yes" or "on" or "1";

    private static int CountLines(string s)
    {
        if (s.Length == 0) return 0;

        var n = 1;
        foreach (var c in s) if (c == '\n') n++;

        // A trailing newline terminates the last line rather than starting a new
        // one, so don't report a 500-line file as 501.
        if (s[^1] == '\n') n--;
        return n;
    }

    /// <summary>
    /// The same skill can be reachable through more than one root (a symlinked
    /// .agents/skills, for example). Count each real file once.
    /// </summary>
    private static List<Skill> Deduplicate(List<Skill> skills)
    {
        var seen = new HashSet<string>(OrdinalPathComparer);
        var output = new List<Skill>(skills.Count);

        foreach (var s in skills)
        {
            if (seen.Add(RealPath(s.Path))) output.Add(s);
        }

        return output;
    }
}
