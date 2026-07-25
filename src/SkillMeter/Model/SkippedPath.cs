namespace SkillMeter.Model;

/// <summary>
/// A path the scan could not read, and why.
///
/// Silently measuring less than reality is the worst failure mode available to a
/// tool that gates CI: an unreadable SKILL.md or a permission-denied subtree used to
/// drop the count, say nothing, and still exit 0 — so the gate passed *because* the
/// scan failed. Every swallowed IO error is now recorded here and surfaced in both
/// output modes.
/// </summary>
/// <param name="Path">The path that could not be read.</param>
/// <param name="Reason">Short human-readable cause, safe to print.</param>
public sealed record SkippedPath(string Path, string Reason);
