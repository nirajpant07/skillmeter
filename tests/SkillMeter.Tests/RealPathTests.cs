using SkillMeter.Scanning;
using Xunit;

namespace SkillMeter.Tests;

/// <summary>
/// Direct tests for SkillScanner.RealPath, the most intricate function in the
/// codebase. It was previously reachable only indirectly through a full scan, so a
/// wrong answer showed up as a puzzling skill count rather than a clear failure.
/// </summary>
public sealed class RealPathTests : IDisposable
{
    private readonly string _root;

    public RealPathTests()
    {
        var raw = Path.Combine(Path.GetTempPath(), "skillmeter-realpath-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(raw);

        // Resolved before use as an expected value. On macOS GetTempPath returns
        // /var/folders/..., and /var is itself a symlink to /private/var — so
        // comparing against the raw string would fail against a *correct* answer.
        // This is the /tmp -> /private/tmp shape RealPath exists to handle, and the
        // test fixture has to respect it too.
        _root = SkillScanner.RealPath(raw);
    }

    public void Dispose() => LinkSupport.DeleteTree(_root);

    private string Dir(params string[] parts)
    {
        var p = Path.Combine([_root, .. parts]);
        Directory.CreateDirectory(p);
        return p;
    }

    [Fact]
    public void AnOrdinaryDirectoryResolvesToItself()
    {
        var d = Dir("plain");

        Assert.Equal(d, SkillScanner.RealPath(d));
    }

    [Fact]
    public void TrailingSeparatorIsNormalisedAway()
    {
        var d = Dir("trailing");

        Assert.Equal(d, SkillScanner.RealPath(d + Path.DirectorySeparatorChar));
    }

    [Fact]
    public void DotAndDotDotSegmentsAreResolved()
    {
        var d = Dir("a", "b");

        Assert.Equal(d, SkillScanner.RealPath(Path.Combine(_root, "a", ".", "b")));
        Assert.Equal(d, SkillScanner.RealPath(Path.Combine(_root, "a", "b", "..", "b")));
    }

    [Fact]
    public void RelativePathsBecomeAbsolute()
    {
        var resolved = SkillScanner.RealPath(".");

        Assert.True(Path.IsPathRooted(resolved));
        Assert.Equal(SkillScanner.RealPath(Directory.GetCurrentDirectory()), resolved);
    }

    [Fact]
    public void APathThatDoesNotExistIsStillNormalised()
    {
        // Resolution must not depend on existence: the resource walk asks about
        // paths it has not visited yet.
        var missing = Path.Combine(_root, "nope", "still-nope");

        Assert.Equal(missing, SkillScanner.RealPath(missing));
    }

    [Fact]
    public void TheRootItselfIsStable()
    {
        var root = Path.GetPathRoot(Path.GetFullPath(_root))!;

        // Idempotent: resolving an already-resolved path changes nothing.
        Assert.Equal(SkillScanner.RealPath(root), SkillScanner.RealPath(SkillScanner.RealPath(root)));
    }

    [Fact]
    public void ResolutionIsIdempotent()
    {
        var d = Dir("idem", "nested");
        var once = SkillScanner.RealPath(d);

        Assert.Equal(once, SkillScanner.RealPath(once));
    }

    // ---- links -------------------------------------------------------------

    [Fact]
    public void ASymlinkResolvesToItsTarget()
    {
        var target = Dir("real-target");
        var link = Path.Combine(_root, "link");

        LinkSupport.RequireSymlinks();
        Assert.True(LinkSupport.TryCreateSymbolicLink(link, target));

        Assert.Equal(SkillScanner.RealPath(target), SkillScanner.RealPath(link));
    }

    [Fact]
    public void ARelativeSymlinkTargetResolvesAgainstTheLinksOwnDirectory()
    {
        // Not the process working directory. FileSystemInfo.ResolveLinkTarget gets
        // this wrong, which is the reason RealPath is hand-rolled at all.
        var target = Dir("parent", "sibling");
        Dir("parent", "here");
        var link = Path.Combine(_root, "parent", "here", "up-and-over");

        LinkSupport.RequireSymlinks();
        Assert.True(LinkSupport.TryCreateSymbolicLink(link, Path.Combine("..", "sibling")));

        Assert.Equal(SkillScanner.RealPath(target), SkillScanner.RealPath(link));
    }

    [Fact]
    public void ALinkedPrefixIsResolvedNotJustTheFinalComponent()
    {
        // The /tmp -> /private/tmp shape: the leaf is not a link at all, only an
        // ancestor is. Resolving just the last component misses this entirely.
        var target = Dir("realparent", "child", "grandchild");
        var linkedParent = Path.Combine(_root, "PA");

        LinkSupport.RequireSymlinks();
        Assert.True(LinkSupport.TryCreateSymbolicLink(linkedParent, Path.Combine(_root, "realparent")));

        var throughLink = Path.Combine(linkedParent, "child", "grandchild");

        Assert.Equal(SkillScanner.RealPath(target), SkillScanner.RealPath(throughLink));
    }

    [Fact]
    public void AJunctionedPrefixIsResolved()
    {
        var target = Dir("jparent", "child");
        var linked = Path.Combine(_root, "JP");

        LinkSupport.RequireJunction(linked, Path.Combine(_root, "jparent"));

        Assert.Equal(SkillScanner.RealPath(target),
                     SkillScanner.RealPath(Path.Combine(linked, "child")));
    }

    [Fact]
    public void ASymlinkCycleTerminatesInsteadOfHanging()
    {
        // Two links pointing at each other. The hop limit must stop resolution
        // rather than spinning forever.
        var a = Path.Combine(_root, "cycle-a");
        var b = Path.Combine(_root, "cycle-b");

        LinkSupport.RequireSymlinks();
        Assert.True(LinkSupport.TryCreateSymbolicLink(a, b));
        Assert.True(LinkSupport.TryCreateSymbolicLink(b, a));

        // The contract is only that it returns; the value for a broken cycle is
        // unspecified.
        _ = SkillScanner.RealPath(a);
    }

    [Fact]
    public void PathsDifferingOnlyByCaseAgreeWhereTheFilesystemIsCaseInsensitive()
    {
        var d = Dir("CaseTest", "Inner");

        var upper = SkillScanner.RealPath(Path.Combine(_root, "CASETEST", "INNER"));
        var actual = SkillScanner.RealPath(d);

        if (OperatingSystem.IsLinux())
        {
            // Case-sensitive: these are genuinely different paths.
            Assert.NotEqual(actual, upper);
        }
        else
        {
            // Windows and macOS: the scanner's comparer folds case, which is what
            // makes deduplication correct there.
            Assert.Equal(actual, upper, StringComparer.OrdinalIgnoreCase);
        }
    }
}
