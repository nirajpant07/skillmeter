using SkillMeter.Model;

namespace SkillMeter.Tests;

/// <summary>
/// Builders for reporter and gate tests, which need a BudgetReport with exact token
/// totals rather than whatever a real scan happens to produce.
/// </summary>
internal static class Fixtures
{
    public static readonly SkillRoot Root = new("/corpus", AgentKind.ClaudeCode, "project");

    public static Skill Skill(
        string name,
        int metadataTokens = 10,
        int bodyTokens = 0,
        int bodyLines = 0,
        int resourceTokens = 0,
        int resourceFiles = 0,
        bool countsTowardListing = true,
        bool hasFrontmatter = true,
        bool nameMismatch = false,
        bool listingTruncated = false) => new()
        {
            Name = name,
            Path = $"/corpus/{name}/SKILL.md",
            Root = Root,
            Description = $"{name} description",
            MetadataTokens = metadataTokens,
            BodyTokens = bodyTokens,
            BodyLines = bodyLines,
            ResourceTokens = resourceTokens,
            ResourceFiles = resourceFiles,
            CountsTowardListing = countsTowardListing,
            HasFrontmatter = hasFrontmatter,
            NameMismatch = nameMismatch,
            ListingTruncated = listingTruncated,
        };

    /// <summary>
    /// A report whose budget is exactly <paramref name="budgetTokens"/>. Window and
    /// fraction are chosen so BudgetTokens lands on that value precisely, because
    /// the boundary cases (exactly at budget, one token of headroom) are the ones
    /// worth asserting.
    /// </summary>
    public static BudgetReport Report(int budgetTokens, params Skill[] skills)
        => BudgetReport.Create(skills, budgetTokens * 100, 0.01, Constants.DefaultMaxDescChars, "o200k_base");

    /// <summary>A report with a single skill carrying the given listing cost.</summary>
    public static BudgetReport ReportWithMetadata(int budgetTokens, int metadataTokens)
        => Report(budgetTokens, Skill("only", metadataTokens));
}
