using SkillMeter.Model;
using SkillMeter.Scanning;
using SkillMeter.Tokenizing;
using Xunit;

namespace SkillMeter.Tests;

public sealed class SkillScannerTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "skillmeter-tests-" + Guid.NewGuid().ToString("N"));

    public SkillScannerTests() => Directory.CreateDirectory(_root);

    public void Dispose() => LinkSupport.DeleteTree(_root);

    private string WriteSkill(string relativeDir, string name, string description, string body = "body")
    {
        var dir = Path.Combine(_root, relativeDir, name);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "SKILL.md");
        File.WriteAllText(path, $"---\nname: {name}\ndescription: {description}\n---\n{body}\n");
        return path;
    }

    private static SkillScanner Scanner(int maxDescChars = Constants.DefaultMaxDescChars)
        => new(new ApproximateCounter(), maxDescChars);

    [Fact]
    public void FindsSkillsRecursively()
    {
        WriteSkill("skills", "alpha", "does alpha things");
        WriteSkill("skills/nested/deeper", "beta", "does beta things");

        var found = Scanner().ScanPath(_root);

        Assert.Equal(2, found.Count);
        Assert.Contains(found, s => s.Name == "alpha");
        Assert.Contains(found, s => s.Name == "beta");
    }

    [Fact]
    public void SkipsExcludedDirectories()
    {
        WriteSkill("skills", "real", "counted");
        WriteSkill("node_modules/pkg/skills", "vendored", "not counted");
        WriteSkill("skills/external-sources/upstreams", "upstream", "not counted");

        var found = Scanner().ScanPath(_root);

        Assert.Single(found);
        Assert.Equal("real", found[0].Name);
    }

    [Fact]
    public void CountsSymlinkedSkillsOnlyOnce()
    {
        // The real-world case: a pack mirrors skills/ into a per-agent path with a
        // symlink. addyosmani/agent-skills does exactly this (.opencode/skills ->
        // ../skills/), which naively reads as double the true count.
        WriteSkill("skills", "shared", "counted once");

        var linkDir = Path.Combine(_root, ".opencode");
        Directory.CreateDirectory(linkDir);

        // Reports a skip rather than returning early: swallowing the failure made
        // this test PASS without asserting anything on unprivileged Windows.
        LinkSupport.RequireSymlinks();
        Assert.True(
            LinkSupport.TryCreateSymbolicLink(Path.Combine(linkDir, "skills"), "../skills"),
            "symlink creation succeeded when probed but failed here.");

        var found = Scanner().ScanPath(_root);

        Assert.Single(found);
    }

    [Fact]
    public void FallsBackToDirectoryNameWhenFrontmatterMissing()
    {
        var dir = Path.Combine(_root, "skills", "no-frontmatter");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "SKILL.md"), "# Just markdown\n\nNo frontmatter.\n");

        var found = Scanner().ScanPath(_root);

        Assert.Single(found);
        Assert.Equal("no-frontmatter", found[0].Name);
        Assert.False(found[0].HasFrontmatter);
    }

    [Fact]
    public void FlagsNameNotMatchingDirectory()
    {
        var dir = Path.Combine(_root, "skills", "actual-directory");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "SKILL.md"),
            "---\nname: different-name\ndescription: d\n---\nbody");

        var found = Scanner().ScanPath(_root);

        Assert.True(found[0].NameMismatch);
    }

    [Fact]
    public void TruncatesListingTextAtTheConfiguredCap()
    {
        WriteSkill("skills", "verbose", new string('x', 3_000));

        var found = Scanner(maxDescChars: 1_536).ScanPath(_root);

        Assert.True(found[0].ListingTruncated);
        // ApproximateCounter is chars/4, so the capped listing plus the name.
        Assert.True(found[0].MetadataTokens <= (1_536 / 4) + 10);
    }

    [Fact]
    public void CombinesDescriptionAndWhenToUseForTheListingCost()
    {
        var dir = Path.Combine(_root, "skills", "combined");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "SKILL.md"),
            "---\nname: combined\ndescription: aaaa\nwhen_to_use: bbbb\n---\nbody");

        var found = Scanner().ScanPath(_root);

        Assert.Equal("aaaa", found[0].Description);
        Assert.Equal("bbbb", found[0].WhenToUse);
    }

    [Fact]
    public void MeasuresBundledResourcesButNotSkillMdItself()
    {
        var dir = Path.Combine(_root, "skills", "with-refs");
        Directory.CreateDirectory(Path.Combine(dir, "references"));
        File.WriteAllText(Path.Combine(dir, "SKILL.md"), "---\nname: with-refs\ndescription: d\n---\nbody");
        File.WriteAllText(Path.Combine(dir, "references", "REFERENCE.md"), new string('y', 400));

        var found = Scanner().ScanPath(_root);

        Assert.Equal(1, found[0].ResourceFiles);
        Assert.True(found[0].ResourceTokens > 0);
    }

    [Fact]
    public void DoesNotAttributeNestedSkillResourcesToTheParent()
    {
        var parent = Path.Combine(_root, "skills", "parent");
        var child = Path.Combine(parent, "child");
        Directory.CreateDirectory(child);
        File.WriteAllText(Path.Combine(parent, "SKILL.md"), "---\nname: parent\ndescription: d\n---\nbody");
        File.WriteAllText(Path.Combine(child, "SKILL.md"), "---\nname: child\ndescription: d\n---\nbody");
        File.WriteAllText(Path.Combine(child, "notes.md"), new string('z', 400));

        var found = Scanner().ScanPath(_root);
        var parentSkill = found.Single(s => s.Name == "parent");

        Assert.Equal(0, parentSkill.ResourceFiles);
    }

    [Fact]
    public void ThrowsClearlyForAMissingDirectory()
        => Assert.Throws<DirectoryNotFoundException>(
            () => Scanner().ScanPath(Path.Combine(_root, "does-not-exist")));

    [Fact]
    public void ReturnsEmptyForADirectoryWithNoSkills()
    {
        Directory.CreateDirectory(Path.Combine(_root, "empty"));
        Assert.Empty(Scanner().ScanPath(_root));
    }
}
