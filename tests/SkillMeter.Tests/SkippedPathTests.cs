using System.Text.Json;
using SkillMeter.Cli;
using SkillMeter.Model;
using SkillMeter.Output;
using SkillMeter.Scanning;
using SkillMeter.Tokenizing;
using Xunit;

namespace SkillMeter.Tests;

/// <summary>
/// T3: an unreadable file used to drop the count, print nothing, and exit 0 — so a
/// CI gate passed *because* the scan failed. These pin the reporting of that.
/// </summary>
public sealed class SkippedPathTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "skillmeter-skipped-" + Guid.NewGuid().ToString("N"));

    public SkippedPathTests() => Directory.CreateDirectory(_root);

    public void Dispose() => LinkSupport.DeleteTree(_root);

    private string Write(string relativePath, string content)
    {
        // GetFullPath so the returned path uses the platform separator throughout;
        // the scanner reports normalised paths and these are compared by string.
        var full = Path.GetFullPath(Path.Combine(_root, relativePath));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
        return full;
    }

    private static BudgetReport WithSkips(params SkippedPath[] skipped)
        => BudgetReport.Create(
            [Fixtures.Skill("present", 100)], 200_000, 0.01, 1_536, "o200k_base", skipped);

    // ---- reporting ---------------------------------------------------------

    [Fact]
    public void TextOutputReportsSkippedPathsAsALowerBound()
    {
        var text = TextReporter.RenderBudget(
            WithSkips(new SkippedPath("/corpus/broken/SKILL.md", "permission denied")));

        Assert.Contains("Findings", text);
        Assert.Contains("1 path(s) could not be read", text);
        Assert.Contains("LOWER BOUND", text);
        Assert.Contains("permission denied", text);
        Assert.Contains("--strict", text);
    }

    [Fact]
    public void TextOutputTruncatesLongSkipListsButStatesTheTotal()
    {
        var many = Enumerable.Range(0, 9)
            .Select(i => new SkippedPath($"/corpus/s{i}/SKILL.md", "permission denied"))
            .ToArray();

        var text = TextReporter.RenderBudget(WithSkips(many));

        Assert.Contains("9 path(s) could not be read", text);
        Assert.Contains("4 more (use --json to see all)", text);
    }

    [Fact]
    public void JsonOutputCarriesSkippedArrayAndCount()
    {
        var json = JsonReporter.Render(
            WithSkips(
                new SkippedPath("/corpus/a/SKILL.md", "permission denied"),
                new SkippedPath("/corpus/b/SKILL.md", "unreadable")),
            "1.2.3");

        var root = JsonDocument.Parse(json).RootElement;

        Assert.Equal(2, root.GetProperty("totals").GetProperty("skippedCount").GetInt32());

        var skipped = root.GetProperty("skipped").EnumerateArray().ToList();
        Assert.Equal(2, skipped.Count);
        Assert.Equal("/corpus/a/SKILL.md", skipped[0].GetProperty("path").GetString());
        Assert.Equal("permission denied", skipped[0].GetProperty("reason").GetString());
    }

    [Fact]
    public void NothingSkippedMeansNoFindingsNoise()
    {
        var text = TextReporter.RenderBudget(WithSkips());

        Assert.DoesNotContain("could not be read", text);
        Assert.DoesNotContain("Findings", text);
    }

    // ---- the gate ----------------------------------------------------------

    [Fact]
    public void StrictExitsNonZeroWhenAnythingWasSkipped()
    {
        var r = Gate.Evaluate(
            WithSkips(new SkippedPath("/corpus/a/SKILL.md", "permission denied")),
            new Options { Strict = true });

        Assert.Equal(ExitCode.GateFailed, r.Code);
        Assert.Contains("lower bound", r.Message);
    }

    [Fact]
    public void WithoutStrictASkippedPathStillExitsZero()
    {
        // Reporting the gap is the default; failing on it is opt-in.
        var r = Gate.Evaluate(
            WithSkips(new SkippedPath("/corpus/a/SKILL.md", "permission denied")),
            new Options());

        Assert.Equal(ExitCode.Ok, r.Code);
    }

    [Fact]
    public void StrictIsSatisfiedWhenNothingWasSkipped()
    {
        Assert.Equal(ExitCode.Ok, Gate.Evaluate(WithSkips(), new Options { Strict = true }).Code);
    }

    [Fact]
    public void StrictBeatsTheBudgetGatesSoAnIncompleteScanIsNeverCalledUnderBudget()
    {
        // The dangerous combination: a scan that missed files but looks cheap.
        var r = Gate.Evaluate(
            WithSkips(new SkippedPath("/corpus/a/SKILL.md", "permission denied")),
            new Options { Strict = true, FailOverBudget = true });

        Assert.Equal(ExitCode.GateFailed, r.Code);
        Assert.Contains("could not be read", r.Message);
    }

    [Fact]
    public void StrictParsesAsAFlag()
    {
        var o = ArgParser.Parse(["--strict"], out var error);

        Assert.Null(error);
        Assert.True(o.Strict);
        Assert.False(ArgParser.Parse([], out _).Strict);
    }

    // ---- end to end through the scanner ------------------------------------

    [Fact]
    public void AnUnreadableSkillFileIsRecordedRatherThanSilentlyDropped()
    {
        Write("skills/readable/SKILL.md", "---\nname: readable\ndescription: d\n---\nbody");
        var locked = Write("skills/locked/SKILL.md", "---\nname: locked\ndescription: d\n---\nbody");

        // Exclusive lock is the portable way to make a read genuinely fail without
        // needing elevation or platform-specific permission APIs.
        using var hold = TryLock(locked);
        Assert.SkipWhen(hold is null, "this platform allows reading a file locked with FileShare.None.");

        var scanner = new SkillScanner(new ApproximateCounter());
        var found = scanner.ScanPath(_root);

        Assert.Single(found);
        Assert.Equal("readable", found[0].Name);

        Assert.Single(scanner.Skipped);
        Assert.Equal(locked, scanner.Skipped[0].Path);
        Assert.NotEmpty(scanner.Skipped[0].Reason);
    }

    [Fact]
    public void AnUnreadableResourceDoesNotAbandonTheRestOfTheDirectory()
    {
        Write("skills/a/SKILL.md", "---\nname: a\ndescription: d\n---\nbody");
        var locked = Write("skills/a/references/locked.md", new string('x', 400));
        Write("skills/a/references/fine.md", new string('x', 400));

        using var hold = TryLock(locked);
        Assert.SkipWhen(hold is null, "this platform allows reading a file locked with FileShare.None.");

        var scanner = new SkillScanner(new ApproximateCounter());
        var found = scanner.ScanPath(_root);

        // The readable sibling must still be counted; it used to be lost with it.
        Assert.Equal(1, found[0].ResourceFiles);
        Assert.Contains(scanner.Skipped, s => s.Path == locked);
    }

    [Fact]
    public void BothOutputModesReportTheSkipEndToEnd()
    {
        Write("skills/readable/SKILL.md", "---\nname: readable\ndescription: d\n---\nbody");
        var locked = Write("skills/locked/SKILL.md", "---\nname: locked\ndescription: d\n---\nbody");

        using var hold = TryLock(locked);
        Assert.SkipWhen(hold is null, "this platform allows reading a file locked with FileShare.None.");

        var scanner = new SkillScanner(new ApproximateCounter());
        var skills = scanner.ScanPath(_root);
        var report = BudgetReport.Create(skills, 200_000, 0.01, 1_536, "approx-chars/4", scanner.Skipped);

        Assert.Contains("could not be read", TextReporter.RenderBudget(report));

        var root = JsonDocument.Parse(JsonReporter.Render(report, "1.2.3")).RootElement;
        Assert.Equal(1, root.GetProperty("totals").GetProperty("skippedCount").GetInt32());
        Assert.Single(root.GetProperty("skipped").EnumerateArray());
    }

    /// <summary>Opens a file exclusively, or null if the platform does not enforce it.</summary>
    private static FileStream? TryLock(string path)
    {
        try
        {
            var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
            try
            {
                // Confirm the lock actually blocks a second reader here; on some
                // platforms FileShare is advisory and this would prove nothing.
                File.ReadAllText(path);
                stream.Dispose();
                return null;
            }
            catch (IOException) { return stream; }
            catch (UnauthorizedAccessException) { return stream; }
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }
}
