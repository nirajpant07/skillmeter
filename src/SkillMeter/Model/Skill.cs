namespace SkillMeter.Model;

/// <summary>Which agent product a skill directory belongs to.</summary>
public enum AgentKind
{
    /// <summary>The vendor-neutral <c>.agents/skills/</c> path.</summary>
    Portable,
    ClaudeCode,
    Cursor,
    Copilot,
    Codex,
    Gemini,
    Unknown,
}

/// <summary>A root directory that skills were discovered under.</summary>
/// <param name="Path">Absolute path to the directory.</param>
/// <param name="Agent">Which agent reads this location.</param>
/// <param name="Scope">"user" for home-directory roots, "project" for repo-local ones.</param>
public sealed record SkillRoot(string Path, AgentKind Agent, string Scope);

/// <summary>
/// One discovered skill and its measured cost, in the three layers the Agent Skills
/// spec defines for progressive disclosure.
/// </summary>
public sealed record Skill
{
    public required string Name { get; init; }
    public required string Path { get; init; }
    public required SkillRoot Root { get; init; }

    public string Description { get; init; } = "";
    public string WhenToUse { get; init; } = "";

    /// <summary>True if the file had parseable YAML frontmatter.</summary>
    public bool HasFrontmatter { get; init; }

    /// <summary>Frontmatter <c>name</c> did not match the parent directory. Spec violation.</summary>
    public bool NameMismatch { get; init; }

    /// <summary>Listing text exceeded <c>skillListingMaxDescChars</c> and was clipped.</summary>
    public bool ListingTruncated { get; init; }

    /// <summary>
    /// False when <c>disable-model-invocation</c> is set: the skill never enters the
    /// listing, so it costs nothing per session and is excluded from the budget.
    /// </summary>
    public bool CountsTowardListing { get; init; } = true;

    /// <summary>
    /// Layer 1. Tokens for name + description (+ when_to_use), after truncation.
    /// Loaded at startup for EVERY installed skill, on every session. Always paid.
    /// </summary>
    public int MetadataTokens { get; init; }

    /// <summary>Layer 2. Tokens in the SKILL.md body. Paid when the skill activates.</summary>
    public int BodyTokens { get; init; }

    /// <summary>Lines in the SKILL.md body. Spec guidance is to stay under 500.</summary>
    public int BodyLines { get; init; }

    /// <summary>Layer 3. Tokens in bundled markdown under references/ and assets/.</summary>
    public int ResourceTokens { get; init; }

    public int ResourceFiles { get; init; }

    /// <summary>Spec guidance: keep the activated body under ~5,000 tokens.</summary>
    public bool ExceedsBodyGuidance => BodyTokens > Constants.BodyTokenGuidance;

    public bool ExceedsLineGuidance => BodyLines > Constants.BodyLineGuidance;
}

public static class Constants
{
    /// <summary>Claude Code <c>skillListingBudgetFraction</c> default.</summary>
    public const double DefaultBudgetFraction = 0.01;

    /// <summary>Claude Code <c>skillListingMaxDescChars</c> default.</summary>
    public const int DefaultMaxDescChars = 1536;

    /// <summary>Standard Claude context window.</summary>
    public const int DefaultContextWindow = 200_000;

    /// <summary>Spec recommendation for an activated SKILL.md body.</summary>
    public const int BodyTokenGuidance = 5_000;

    /// <summary>Spec recommendation: keep SKILL.md under 500 lines.</summary>
    public const int BodyLineGuidance = 500;
}
