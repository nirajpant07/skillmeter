using SkillMeter.Output;
using Xunit;

namespace SkillMeter.Tests;

/// <summary>
/// The human-facing output. Untested until now, which is why the singular/plural
/// boundary and the exactly-at-budget case were never checked.
/// </summary>
public sealed class TextReporterTests
{
    [Fact]
    public void UnderBudgetReportsHeadroom()
    {
        var text = TextReporter.RenderBudget(Fixtures.ReportWithMetadata(2_000, 500));

        Assert.Contains("Within budget", text);
        Assert.Contains("1,500 tokens of headroom", text);
        Assert.DoesNotContain("OVER BUDGET", text);
    }

    [Fact]
    public void OneTokenOfHeadroomIsSingular()
    {
        // The launch headline is 1,999 against 2,000. "1 tokens" would be visible
        // on the front page of the README.
        var text = TextReporter.RenderBudget(Fixtures.ReportWithMetadata(2_000, 1_999));

        Assert.Contains("1 token of headroom", text);
        Assert.DoesNotContain("1 tokens of headroom", text);
    }

    [Fact]
    public void ExactlyAtBudgetIsWithinBudgetNotOver()
    {
        var text = TextReporter.RenderBudget(Fixtures.ReportWithMetadata(2_000, 2_000));

        Assert.Contains("Within budget", text);
        Assert.Contains("0 tokens of headroom", text);
        Assert.DoesNotContain("OVER BUDGET", text);
    }

    [Fact]
    public void NearTheEdgeWarnsAboutTheNextPack()
    {
        var near = TextReporter.RenderBudget(Fixtures.ReportWithMetadata(2_000, 1_900));
        Assert.Contains("Close to the edge", near);

        var comfortable = TextReporter.RenderBudget(Fixtures.ReportWithMetadata(2_000, 100));
        Assert.DoesNotContain("Close to the edge", comfortable);
    }

    [Fact]
    public void OverBudgetReportsOverageAndWhatGoesDark()
    {
        var report = Fixtures.Report(
            2_000,
            Fixtures.Skill("a", metadataTokens: 1_500),
            Fixtures.Skill("b", metadataTokens: 1_500));

        var text = TextReporter.RenderBudget(report);

        Assert.Contains("OVER BUDGET by 1,000 tokens", text);
        Assert.Contains("go dark", text);
        // The fit figure is a best case and the output must say so, because the
        // real eviction order needs runtime data skillmeter does not have.
        Assert.Contains("Best case", text);
    }

    [Fact]
    public void EmptyCorpusRendersWithoutBlowingUp()
    {
        var text = TextReporter.RenderBudget(Fixtures.Report(2_000));

        Assert.Contains("0 skills", text);
        Assert.Contains("Within budget", text);
        Assert.Contains("2,000 tokens of headroom", text);
    }

    [Fact]
    public void EmptyCorpusCostViewRendersWithoutBlowingUp()
    {
        var text = TextReporter.RenderCost(Fixtures.Report(2_000), limit: 25);

        Assert.Contains("TOTAL", text);
        Assert.DoesNotContain("more (use --top", text);
    }

    [Fact]
    public void CostViewIsSortedDearestFirstAndTruncatesWithACount()
    {
        var report = Fixtures.Report(
            2_000,
            Fixtures.Skill("cheap", metadataTokens: 1),
            Fixtures.Skill("dear", metadataTokens: 900),
            Fixtures.Skill("middling", metadataTokens: 50));

        var text = TextReporter.RenderCost(report, limit: 2);

        Assert.True(text.IndexOf("dear", StringComparison.Ordinal)
                    < text.IndexOf("middling", StringComparison.Ordinal));
        Assert.DoesNotContain("cheap", text);
        Assert.Contains("1 more (use --top 3", text);
    }

    [Fact]
    public void FindingsAreOmittedEntirelyWhenThereIsNothingToReport()
    {
        var clean = TextReporter.RenderBudget(Fixtures.ReportWithMetadata(2_000, 100));

        Assert.DoesNotContain("Findings", clean);
    }

    [Fact]
    public void FindingsReportEachSpecViolation()
    {
        var report = Fixtures.Report(
            2_000,
            Fixtures.Skill("fat", bodyTokens: 9_000, bodyLines: 600),
            Fixtures.Skill("nofm", hasFrontmatter: false),
            Fixtures.Skill("mismatch", nameMismatch: true),
            Fixtures.Skill("clipped", listingTruncated: true));

        var text = TextReporter.RenderBudget(report);

        Assert.Contains("Findings", text);
        Assert.Contains("exceed the 5,000-token body guidance", text);
        Assert.Contains("no parseable frontmatter", text);
        Assert.Contains("differs from the directory", text);
        Assert.Contains("clipped at 1,536 chars", text);
    }

    [Fact]
    public void FooterFlagsTheTokenizerAsAProxyOnlyForO200k()
    {
        Assert.Contains("proxy for Claude's tokenizer",
            TextReporter.RenderBudget(Fixtures.ReportWithMetadata(2_000, 100)));

        var approx = SkillMeter.Model.BudgetReport.Create(
            [Fixtures.Skill("a")], 200_000, 0.01, 1_536, "approx-chars/4");
        Assert.DoesNotContain("proxy for Claude's tokenizer", TextReporter.RenderCost(approx, 25));
        Assert.Contains("approx-chars/4", TextReporter.RenderCost(approx, 25));
    }
}
