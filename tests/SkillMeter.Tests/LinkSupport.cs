using System.Diagnostics;
using Xunit;

namespace SkillMeter.Tests;

/// <summary>
/// Filesystem-link helpers for the tests that exercise symlink deduplication.
///
/// Why this exists: on Windows, Directory.CreateSymbolicLink needs either an
/// elevated process or Developer Mode. The three symlink tests used to catch that
/// failure and <c>return</c>, so an unprivileged Windows run reported them as
/// PASSED while asserting nothing — 69 green tests that proved less than they
/// looked. They now skip *visibly*, and CI can demand they really run.
///
/// Junctions are the compensating cover. Windows creates them with no privilege at
/// all, they are reparse points that DirectoryInfo.LinkTarget reports exactly as it
/// reports a symlink, and mklink records the target verbatim rather than
/// pre-resolving it — so a junction pointing through another junction still
/// exercises RealPath's restart-after-following-a-link path. That makes the
/// symlink-dedup logic testable on an ordinary developer machine.
///
/// The one branch junctions cannot reach is a *relative* link target, because
/// Windows always stores junction targets absolute.
/// </summary>
internal static class LinkSupport
{
    /// <summary>
    /// When set to 1, a machine that cannot create symbolic links fails the test
    /// instead of skipping it. CI sets this so the coverage cannot quietly lapse.
    /// </summary>
    private static bool LinksRequired =>
        Environment.GetEnvironmentVariable("SKILLMETER_REQUIRE_LINKS") == "1";

    /// <summary>Creates a directory symlink, reporting whether the platform allowed it.</summary>
    public static bool TryCreateSymbolicLink(string path, string target)
    {
        try
        {
            Directory.CreateSymbolicLink(path, target);
            return true;
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    /// <summary>
    /// Skips the calling test — as a reported skip, not a silent pass — when this
    /// machine cannot create symbolic links.
    /// </summary>
    public static void RequireSymlinks()
    {
        var link = Path.Combine(Path.GetTempPath(), "skillmeter-linkprobe-" + Guid.NewGuid().ToString("N"));
        var target = link + "-target";
        Directory.CreateDirectory(target);

        try
        {
            if (TryCreateSymbolicLink(link, target)) return;

            const string why =
                "this machine cannot create symbolic links. On Windows that needs an "
                + "elevated prompt or Developer Mode";

            Assert.False(LinksRequired,
                $"SKILLMETER_REQUIRE_LINKS=1 demands real link coverage, but {why}.");
            Assert.Skip(
                $"Skipped: {why}. This test asserts nothing here — set "
                + "SKILLMETER_REQUIRE_LINKS=1 to make that a failure instead.");
        }
        finally
        {
            try { Directory.Delete(link); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            try { Directory.Delete(target); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    /// <summary>
    /// Creates a Windows directory junction. Unlike a symlink this needs no
    /// privilege, so it runs on any Windows machine including unelevated CI.
    /// </summary>
    public static bool TryCreateJunction(string path, string target)
    {
        if (!OperatingSystem.IsWindows()) return false;

        // mklink is a cmd.exe builtin rather than a standalone executable, so it has
        // to be invoked through cmd. No package reference, consistent with the
        // zero-third-party-dependency rule.
        var psi = new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{path}\" \"{target}\"")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        try
        {
            using var proc = Process.Start(psi);
            if (proc is null) return false;
            proc.WaitForExit();
            return proc.ExitCode == 0 && Directory.Exists(path);
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    /// <summary>
    /// Recursively deletes a test tree that may contain links.
    ///
    /// Directory.Delete(recursive: true) is not usable here: on Windows it throws
    /// UnauthorizedAccessException when the tree contains a junction, and the naive
    /// recursive walk would otherwise descend *through* a link and delete the
    /// target's contents. A reparse point is therefore unlinked directly and never
    /// recursed into.
    /// </summary>
    public static void DeleteTree(string path)
    {
        if (!Directory.Exists(path)) return;

        try
        {
            foreach (var dir in Directory.GetDirectories(path))
            {
                var info = new DirectoryInfo(dir);
                if (info.Attributes.HasFlag(FileAttributes.ReparsePoint)) info.Delete();
                else DeleteTree(dir);
            }

            foreach (var file in Directory.GetFiles(path)) File.Delete(file);

            Directory.Delete(path);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    /// <summary>Skips the calling test unless it is running on Windows with a usable junction.</summary>
    public static void RequireJunction(string path, string target)
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "junctions are a Windows-only construct.");
        Assert.True(TryCreateJunction(path, target),
            $"mklink /J failed for '{path}' -> '{target}'; junctions need no privilege, so this is a real failure.");
    }
}
