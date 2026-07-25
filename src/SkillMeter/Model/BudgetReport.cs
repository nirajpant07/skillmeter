namespace SkillMeter.Model;

/// <summary>
/// Models Claude Code's skill-listing budget.
///
/// Documented behaviour (code.claude.com/docs/en/settings):
///   skillListingBudgetFraction  default 0.01  - fraction of the context window
///                                               reserved for the skill listing
///   skillListingMaxDescChars    default 1536  - per-skill cap on the combined
///                                               description + when_to_use text
///
/// When the listing exceeds its budget, descriptions for the least-used skills are
/// dropped and only their names remain. A warning goes to the debug log, and
/// /doctor reports an estimate. What no tool does is attribute the cost per skill,
/// across agents, in a form you can gate a pull request on.
/// </summary>
public sealed record BudgetReport
{
    public required int ContextWindow { get; init; }
    public required double BudgetFraction { get; init; }
    public required int MaxDescChars { get; init; }
    public required string Tokenizer { get; init; }

    public required IReadOnlyList<Skill> Skills { get; init; }

    /// <summary>
    /// Paths the scan could not read. A non-empty list means every figure here is a
    /// lower bound, which is why --strict exists.
    /// </summary>
    public IReadOnlyList<SkippedPath> Skipped { get; init; } = [];

    public int SkippedCount => Skipped.Count;

    public int BudgetTokens => (int)(ContextWindow * BudgetFraction);

    /// <summary>Total always-paid listing cost across every installed skill.</summary>
    public int MetadataTokens => Skills.Sum(s => s.MetadataTokens);

    public int BodyTokens => Skills.Sum(s => s.BodyTokens);
    public int ResourceTokens => Skills.Sum(s => s.ResourceTokens);
    public int ResourceFiles => Skills.Sum(s => s.ResourceFiles);

    public int OverageTokens => Math.Max(0, MetadataTokens - BudgetTokens);
    public bool IsOverBudget => MetadataTokens > BudgetTokens;

    public double BudgetMultiple =>
        BudgetTokens == 0 ? 0 : (double)MetadataTokens / BudgetTokens;

    /// <summary>
    /// Best case: how many skills keep their descriptions if the cheapest are kept
    /// first. Real eviction order is least-used-first, which skillmeter cannot know
    /// without runtime data — so this is an upper bound, and we say so in output.
    /// </summary>
    public int SkillsThatFit
    {
        get
        {
            var running = 0;
            var fit = 0;
            foreach (var s in Listed.OrderBy(s => s.MetadataTokens))
            {
                if (running + s.MetadataTokens > BudgetTokens) break;
                running += s.MetadataTokens;
                fit++;
            }
            return fit;
        }
    }

    /// <summary>Skills that actually enter the listing and so compete for the budget.</summary>
    public IEnumerable<Skill> Listed => Skills.Where(s => s.CountsTowardListing);

    public int ListedCount => Listed.Count();

    /// <summary>Skills excluded from the listing via <c>disable-model-invocation</c>.</summary>
    public int NotListedCount => Skills.Count - ListedCount;

    /// <summary>Skills that lose their description, best case. These stop being routable.</summary>
    public int SkillsGoingDark => ListedCount - SkillsThatFit;

    public double PercentOfWindow =>
        ContextWindow == 0 ? 0 : (double)MetadataTokens / ContextWindow * 100.0;

    public static BudgetReport Create(
        IReadOnlyList<Skill> skills,
        int contextWindow,
        double fraction,
        int maxDescChars,
        string tokenizer,
        IReadOnlyList<SkippedPath>? skipped = null) => new()
        {
            Skills = skills,
            ContextWindow = contextWindow,
            BudgetFraction = fraction,
            MaxDescChars = maxDescChars,
            Tokenizer = tokenizer,
            Skipped = skipped ?? [],
        };
}
