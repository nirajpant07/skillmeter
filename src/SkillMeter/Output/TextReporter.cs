using System.Text;
using SkillMeter.Model;

namespace SkillMeter.Output;

public static class TextReporter
{
    private const int NameWidth = 38;

    /// <summary>The headline view: what the listing costs and what it evicts.</summary>
    public static string RenderBudget(BudgetReport r)
    {
        var sb = new StringBuilder();

        sb.AppendLine();
        sb.AppendLine($"  {r.ListedCount} skills   {Num(r.MetadataTokens)} tokens of listing metadata");
        sb.AppendLine($"  budget: {Num(r.BudgetTokens)} tokens  ({r.BudgetFraction:P1} of a {Num(r.ContextWindow)}-token window)");
        sb.AppendLine();

        if (r.IsOverBudget)
        {
            sb.AppendLine($"  OVER BUDGET by {Num(r.OverageTokens)} tokens — {r.BudgetMultiple:0.0}x the allowance.");
            sb.AppendLine();
            sb.AppendLine($"  {r.SkillsThatFit} skills keep their description.");
            sb.AppendLine($"  {r.SkillsGoingDark} go dark — the agent sees a name it cannot route to.");
            sb.AppendLine();
            sb.AppendLine("  (Best case. Claude Code evicts least-used first, which needs runtime");
            sb.AppendLine("   data skillmeter does not have, so the real figure is no better.)");
        }
        else
        {
            var headroom = r.BudgetTokens - r.MetadataTokens;
            sb.AppendLine($"  Within budget. {Num(headroom)} {Plural(headroom, "token")} of headroom " +
                          $"({(long)headroom * 100 / Math.Max(1, r.BudgetTokens)}% remaining).");
            if (headroom < r.BudgetTokens * 0.15)
                sb.AppendLine("  Close to the edge — the next pack you install will start evicting skills.");
        }

        sb.AppendLine();
        sb.AppendLine(Rule());
        sb.AppendLine("  Cost layers");
        sb.AppendLine(Rule());
        sb.AppendLine($"  every session   {Num(r.MetadataTokens),9} tokens   listing metadata ({r.PercentOfWindow:0.00}% of window)");
        sb.AppendLine($"  on activation   {Num(r.BodyTokens),9} tokens   SKILL.md bodies, if all fired");
        sb.AppendLine($"  on demand       {Num(r.ResourceTokens),9} tokens   {Num(r.ResourceFiles)} bundled resource files");
        sb.AppendLine();

        AppendWarnings(sb, r);
        AppendFooter(sb, r);
        return sb.ToString();
    }

    /// <summary>Per-skill breakdown, dearest first.</summary>
    public static string RenderCost(BudgetReport r, int limit)
    {
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine($"  {"skill".PadRight(NameWidth)} {"listing".PadLeft(8)} {"body".PadLeft(8)} {"files".PadLeft(6)} {"agent".PadLeft(11)}");
        sb.AppendLine(Rule());

        var ordered = r.Skills.OrderByDescending(s => s.MetadataTokens).ToList();
        foreach (var s in ordered.Take(limit))
        {
            var flags = new StringBuilder();
            if (s.ExceedsBodyGuidance) flags.Append(" !body");
            if (s.ListingTruncated) flags.Append(" !trunc");
            if (!s.HasFrontmatter) flags.Append(" !nofm");
            if (s.NameMismatch) flags.Append(" !name");

            sb.AppendLine(
                $"  {Trunc(s.Name, NameWidth).PadRight(NameWidth)} " +
                $"{Num(s.MetadataTokens).PadLeft(8)} " +
                $"{Num(s.BodyTokens).PadLeft(8)} " +
                $"{Num(s.ResourceFiles).PadLeft(6)} " +
                $"{s.Root.Agent.ToString().PadLeft(11)}{flags}");
        }

        if (ordered.Count > limit)
            sb.AppendLine($"  … {ordered.Count - limit} more (use --top {ordered.Count} to see all)");

        sb.AppendLine(Rule());
        sb.AppendLine($"  {"TOTAL".PadRight(NameWidth)} {Num(r.MetadataTokens).PadLeft(8)} {Num(r.BodyTokens).PadLeft(8)} {Num(r.ResourceFiles).PadLeft(6)}");
        sb.AppendLine();

        AppendWarnings(sb, r);
        AppendFooter(sb, r);
        return sb.ToString();
    }

    private static void AppendWarnings(StringBuilder sb, BudgetReport r)
    {
        var fat = r.Skills.Where(s => s.ExceedsBodyGuidance).OrderByDescending(s => s.BodyTokens).ToList();
        var noFm = r.Skills.Count(s => !s.HasFrontmatter);
        var mismatch = r.Skills.Count(s => s.NameMismatch);
        var trunc = r.Skills.Count(s => s.ListingTruncated);

        if (fat.Count == 0 && noFm == 0 && mismatch == 0 && trunc == 0 && r.SkippedCount == 0) return;

        sb.AppendLine(Rule());
        sb.AppendLine("  Findings");
        sb.AppendLine(Rule());

        // Listed first and stated plainly: if anything was skipped, every other
        // number in this report is a lower bound, which changes how to read them.
        if (r.SkippedCount > 0)
        {
            sb.AppendLine($"  {r.SkippedCount} path(s) could not be read — the figures above are a LOWER BOUND:");
            foreach (var s in r.Skipped.Take(5))
            {
                sb.AppendLine($"      {TruncLeft(s.Path, 68)}");
                sb.AppendLine($"          {Trunc(s.Reason, 64)}");
            }
            if (r.SkippedCount > 5)
                sb.AppendLine($"      … {r.SkippedCount - 5} more (use --json to see all)");
            sb.AppendLine("  Use --strict to make this exit non-zero.");
        }

        if (fat.Count > 0)
        {
            sb.AppendLine($"  {fat.Count} skill(s) exceed the {Num(Constants.BodyTokenGuidance)}-token body guidance:");
            foreach (var s in fat.Take(5))
                sb.AppendLine($"      {Trunc(s.Name, 34),-34} {Num(s.BodyTokens),8} tokens ({s.BodyLines} lines)");
        }

        if (trunc > 0)
            sb.AppendLine($"  {trunc} skill(s) have listing text clipped at {Num(r.MaxDescChars)} chars.");
        if (noFm > 0)
            sb.AppendLine($"  {noFm} skill(s) have no parseable frontmatter.");
        if (mismatch > 0)
            sb.AppendLine($"  {mismatch} skill(s) have a name that differs from the directory (spec violation).");

        sb.AppendLine();
    }

    private static void AppendFooter(StringBuilder sb, BudgetReport r)
    {
        sb.AppendLine($"  tokenizer: {r.Tokenizer}" +
                      (r.Tokenizer == "o200k_base"
                          ? "  (proxy for Claude's tokenizer — see README)"
                          : ""));
    }

    private static string Rule() => "  " + new string('-', 78);

    private static string Plural(int n, string word) => n == 1 ? word : word + "s";

    private static string Trunc(string s, int n)
        => s.Length <= n ? s : s[..(n - 1)] + "…";

    /// <summary>
    /// Truncates from the left, keeping the tail. For a path the distinctive part is
    /// the end — a column of identical temp-directory prefixes identifies nothing.
    /// </summary>
    private static string TruncLeft(string s, int n)
        => s.Length <= n ? s : "…" + s[^(n - 1)..];

    private static string Num(int n) => n.ToString("N0");
}
