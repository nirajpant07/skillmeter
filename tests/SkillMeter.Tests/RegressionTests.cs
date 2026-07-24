using SkillMeter.Model;
using SkillMeter.Scanning;
using SkillMeter.Tokenizing;
using Xunit;

namespace SkillMeter.Tests;

/// <summary>
/// One test per bug found in review. These exist because the original suite
/// certified the wrong invariant in two places — testing the single case that
/// happened to work — and the headline token figure was 1.22% too high as a result.
/// </summary>
public sealed class RegressionTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "skillmeter-regress-" + Guid.NewGuid().ToString("N"));

    public RegressionTests() => Directory.CreateDirectory(_root);

    public void Dispose() => LinkSupport.DeleteTree(_root);

    private static SkillScanner Scanner() => new(new ApproximateCounter());

    private string Write(string relativePath, string content)
    {
        var full = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
        return full;
    }

    // ---- FrontmatterParser -------------------------------------------------

    [Fact]
    public void FoldedScalarCollapsesToSpaces()
    {
        // Was treated as a literal block and joined with newlines. 116 of 235 corpus
        // descriptions use ">" or ">-", making this the single largest over-count.
        var (fields, _) = FrontmatterParser.Parse(
            "---\nname: x\ndescription: >-\n  first part\n  second part\n---\nbody");

        Assert.Equal("first part second part", fields["description"]);
        Assert.DoesNotContain('\n', fields["description"]);
    }

    [Fact]
    public void LiteralScalarKeepsNewlines()
    {
        var (fields, _) = FrontmatterParser.Parse(
            "---\nname: x\ndescription: |-\n  first\n  second\n---\nbody");

        Assert.Equal("first\nsecond", fields["description"]);
    }

    [Fact]
    public void CommentAfterFirstKeyIsNotAppendedToThePreviousValue()
    {
        var (fields, _) = FrontmatterParser.Parse(
            "---\nname: pdf\ndescription: Handle PDFs.\n# TODO: expand this\n---\nbody");

        Assert.Equal("Handle PDFs.", fields["description"]);
        Assert.DoesNotContain("TODO", fields["description"]);
    }

    [Fact]
    public void CommentBetweenKeysDoesNotCorruptTheKeyBefore()
    {
        var (fields, _) = FrontmatterParser.Parse(
            "---\nname: pdf\n# a note\ndescription: d\n---\nbody");

        Assert.Equal("pdf", fields["name"]);
        Assert.Equal("d", fields["description"]);
    }

    [Fact]
    public void InlineCommentIsStrippedButHashInsideTextIsKept()
    {
        var (a, _) = FrontmatterParser.Parse("---\nname: x # the name\ndescription: d\n---\nb");
        Assert.Equal("x", a["name"]);

        var (b, _) = FrontmatterParser.Parse("---\nname: x\ndescription: uses C#9 features\n---\nb");
        Assert.Equal("uses C#9 features", b["description"]);
    }

    [Fact]
    public void DoubleQuotedEscapesAreUnescaped()
    {
        // Real occurrence: anthropics/skills xlsx SKILL.md.
        var (fields, _) = FrontmatterParser.Parse(
            "---\nname: x\ndescription: \"the \\\"xlsx\\\" in my downloads\"\n---\nbody");

        Assert.Equal("the \"xlsx\" in my downloads", fields["description"]);
    }

    [Fact]
    public void ValueWithInteriorQuotesIsNotMistakenForAQuotedScalar()
    {
        var (fields, _) = FrontmatterParser.Parse(
            "---\nname: x\ndescription: \"a\" and \"b\"\n---\nbody");

        Assert.Equal("\"a\" and \"b\"", fields["description"]);
    }

    [Fact]
    public void IndentedFenceInsideBlockScalarDoesNotEndTheFrontmatter()
    {
        // Previously truncated the description and spilled the remaining keys into
        // the body — a markdown rule inside a block scalar was enough to trigger it.
        var (fields, body) = FrontmatterParser.Parse(
            """
            ---
            name: x
            description: |
              Intro text.
              ---
              Continues after the rule.
            license: MIT
            ---
            real body
            """);

        Assert.Contains("Continues after the rule.", fields["description"]);
        Assert.Equal("MIT", fields["license"]);
        Assert.Equal("real body", body.Trim());
    }

    [Fact]
    public void ValueContainingAColonIsNotSplitAtTheWrongPlace()
    {
        var (fields, _) = FrontmatterParser.Parse(
            "---\nname: x\ndescription: Use for URLs like https://example.com/a\n---\nbody");

        Assert.Equal("Use for URLs like https://example.com/a", fields["description"]);
    }

    // ---- SkillScanner ------------------------------------------------------

    [Fact]
    public void SkillNamedLikeAnExcludedDirectoryIsStillFound()
    {
        // "build", "dist" and friends were dropped silently, exit code 0.
        Write("skills/build/SKILL.md", "---\nname: build\ndescription: d\n---\nbody");
        Write("skills/real/SKILL.md", "---\nname: real\ndescription: d\n---\nbody");

        var found = Scanner().ScanPath(_root);

        Assert.Equal(2, found.Count);
        Assert.Contains(found, s => s.Name == "build");
    }

    [Fact]
    public void ExcludedDirectoriesAreNotBilledAsResources()
    {
        Write("skills/a/SKILL.md", "---\nname: a\ndescription: d\n---\nbody");
        Write("skills/a/node_modules/pkg/readme.md", new string('x', 4_000));
        Write("skills/a/external-sources/vendored.md", new string('x', 4_000));

        var found = Scanner().ScanPath(_root);

        Assert.Equal(0, found[0].ResourceFiles);
        Assert.Equal(0, found[0].ResourceTokens);
    }

    [Fact]
    public void NestedSkillResourcesAreNotAttributedToTheParentAtAnyDepth()
    {
        // The original guard only inspected the immediate parent, so the ordinary
        // references/ layout escaped it and was counted twice.
        Write("skills/outer/SKILL.md", "---\nname: outer\ndescription: d\n---\nbody");
        Write("skills/outer/inner/SKILL.md", "---\nname: inner\ndescription: d\n---\nbody");
        Write("skills/outer/inner/references/ref.md", new string('x', 400));

        var found = Scanner().ScanPath(_root);
        var outer = found.Single(s => s.Name == "outer");
        var inner = found.Single(s => s.Name == "inner");

        Assert.Equal(0, outer.ResourceFiles);
        Assert.Equal(1, inner.ResourceFiles);
    }

    [Fact]
    public void ResourceWalkDoesNotFollowSymlinkLoops()
    {
        Write("skills/a/SKILL.md", "---\nname: a\ndescription: d\n---\nbody");
        Write("skills/a/references/note.md", new string('x', 400));

        LinkSupport.RequireSymlinks();
        Assert.True(
            LinkSupport.TryCreateSymbolicLink(
                Path.Combine(_root, "skills", "a", "references", "up"), ".."),
            "symlink creation succeeded when probed but failed here.");

        var found = Scanner().ScanPath(_root);

        // Was 41 files before the resource walk gained cycle protection.
        Assert.Equal(1, found[0].ResourceFiles);
    }

    [Fact]
    public void SymlinkWithASymlinkedPrefixStillDeduplicates()
    {
        // The /tmp -> /private/tmp shape, routine on macOS.
        Write("realparent/target/myskill/SKILL.md", "---\nname: myskill\ndescription: d\n---\nbody");
        Directory.CreateDirectory(Path.Combine(_root, "roots", "a"));
        Directory.CreateDirectory(Path.Combine(_root, "roots", "b"));

        LinkSupport.RequireSymlinks();
        Assert.True(
            LinkSupport.TryCreateSymbolicLink(Path.Combine(_root, "PA"),
                Path.Combine(_root, "realparent"))
            && LinkSupport.TryCreateSymbolicLink(Path.Combine(_root, "roots", "a", "skills"),
                Path.Combine(_root, "PA", "target"))
            && LinkSupport.TryCreateSymbolicLink(Path.Combine(_root, "roots", "b", "skills"),
                Path.Combine(_root, "realparent", "target")),
            "symlink creation succeeded when probed but failed here.");

        var found = Scanner().ScanPath(Path.Combine(_root, "roots"));

        Assert.Single(found);
    }

    [Fact]
    public void DisableModelInvocationSkillsDoNotConsumeTheListingBudget()
    {
        Write("skills/listed/SKILL.md", "---\nname: listed\ndescription: counted\n---\nbody");
        Write("skills/hidden/SKILL.md",
            "---\nname: hidden\ndescription: not counted\ndisable-model-invocation: true\n---\nbody");

        var found = Scanner().ScanPath(_root);
        var hidden = found.Single(s => s.Name == "hidden");
        var listed = found.Single(s => s.Name == "listed");

        Assert.False(hidden.CountsTowardListing);
        Assert.Equal(0, hidden.MetadataTokens);
        Assert.True(listed.CountsTowardListing);
        Assert.True(listed.MetadataTokens > 0);

        var report = BudgetReport.Create(found, 200_000, 0.01, 1536, "t");
        Assert.Equal(1, report.ListedCount);
        Assert.Equal(1, report.NotListedCount);
    }

    [Fact]
    public void BodyLineCountIgnoresTheTrailingNewline()
    {
        Write("skills/a/SKILL.md", "---\nname: a\ndescription: d\n---\nline1\nline2\n");

        var found = Scanner().ScanPath(_root);

        Assert.Equal(2, found[0].BodyLines);
    }

    // ---- Windows ------------------------------------------------------------
    //
    // The symlink tests above need elevation or Developer Mode on Windows, so on an
    // ordinary developer machine they skip and the link-dedup logic goes unexercised
    // there. Junctions need no privilege and are the same reparse-point mechanism,
    // so these give Windows real coverage of the same three behaviours.

    [Fact]
    public void JunctionMirroringSkillsIsCountedOnce()
    {
        // The .opencode/skills -> ../skills mirror, expressed the Windows way.
        Write("skills/shared/SKILL.md", "---\nname: shared\ndescription: d\n---\nbody");
        Directory.CreateDirectory(Path.Combine(_root, ".opencode"));

        LinkSupport.RequireJunction(
            Path.Combine(_root, ".opencode", "skills"), Path.Combine(_root, "skills"));

        Assert.Single(Scanner().ScanPath(_root));
    }

    [Fact]
    public void ResourceWalkDoesNotFollowJunctionLoops()
    {
        Write("skills/a/SKILL.md", "---\nname: a\ndescription: d\n---\nbody");
        Write("skills/a/references/note.md", new string('x', 400));

        LinkSupport.RequireJunction(
            Path.Combine(_root, "skills", "a", "references", "up"),
            Path.Combine(_root, "skills", "a"));

        var found = Scanner().ScanPath(_root);

        Assert.Equal(1, found[0].ResourceFiles);
    }

    [Fact]
    public void JunctionWithAJunctionedPrefixStillDeduplicates()
    {
        // mklink stores the target verbatim rather than pre-resolving it, so
        // roots/a/skills -> PA/target genuinely forces RealPath to notice that the
        // PA *prefix* is itself a link and restart resolution from the top.
        Write("realparent/target/myskill/SKILL.md", "---\nname: myskill\ndescription: d\n---\nbody");
        Directory.CreateDirectory(Path.Combine(_root, "roots", "a"));
        Directory.CreateDirectory(Path.Combine(_root, "roots", "b"));

        LinkSupport.RequireJunction(Path.Combine(_root, "PA"), Path.Combine(_root, "realparent"));
        LinkSupport.RequireJunction(Path.Combine(_root, "roots", "a", "skills"),
            Path.Combine(_root, "PA", "target"));
        LinkSupport.RequireJunction(Path.Combine(_root, "roots", "b", "skills"),
            Path.Combine(_root, "realparent", "target"));

        Assert.Single(Scanner().ScanPath(Path.Combine(_root, "roots")));
    }

    [Fact]
    public void ListingMetadataIsIndependentOfLineEndings()
    {
        // Git for Windows checks out CRLF by default (core.autocrlf=true). The
        // budget figure is the product's headline number, so it must not move
        // between a CRLF and an LF checkout of the same pack. Measured on the
        // anthropics/skills corpus: metadata identical, bodies +0.4% under CRLF.
        const string lf = "---\nname: a\ndescription: a description that spans\n  two source lines\n---\nline1\nline2\n";

        Write("lf/a/SKILL.md", lf);
        Write("crlf/a/SKILL.md", lf.Replace("\n", "\r\n"));

        var a = Scanner().ScanPath(Path.Combine(_root, "lf"))[0];
        var b = Scanner().ScanPath(Path.Combine(_root, "crlf"))[0];

        Assert.Equal(a.Description, b.Description);
        Assert.Equal(a.MetadataTokens, b.MetadataTokens);
        Assert.Equal(a.BodyLines, b.BodyLines);
    }
}
