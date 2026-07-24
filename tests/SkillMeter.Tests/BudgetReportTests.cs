using SkillMeter.Model;
using Xunit;

namespace SkillMeter.Tests;

public class BudgetReportTests
{
    private static Skill Make(string name, int metadata, int body = 0, int lines = 0) => new()
    {
        Name = name,
        Path = $"/tmp/{name}/SKILL.md",
        Root = new SkillRoot("/tmp", AgentKind.Portable, "test"),
        MetadataTokens = metadata,
        BodyTokens = body,
        BodyLines = lines,
        HasFrontmatter = true,
    };

    private static BudgetReport Report(params Skill[] skills) =>
        BudgetReport.Create(skills, Constants.DefaultContextWindow,
            Constants.DefaultBudgetFraction, Constants.DefaultMaxDescChars, "test");

    [Fact]
    public void BudgetIsOnePercentOfContextWindowByDefault()
    {
        // The documented Claude Code default: 0.01 x 200,000 = 2,000 tokens.
        Assert.Equal(2_000, Report(Make("a", 10)).BudgetTokens);
    }

    [Fact]
    public void DetectsOverBudgetAndReportsOverage()
    {
        var r = Report(Make("a", 1_500), Make("b", 1_000));

        Assert.True(r.IsOverBudget);
        Assert.Equal(2_500, r.MetadataTokens);
        Assert.Equal(500, r.OverageTokens);
        Assert.Equal(1.25, r.BudgetMultiple, 3);
    }

    [Fact]
    public void UnderBudgetReportsNoOverage()
    {
        var r = Report(Make("a", 100), Make("b", 200));

        Assert.False(r.IsOverBudget);
        Assert.Equal(0, r.OverageTokens);
        Assert.Equal(0, r.SkillsGoingDark);
    }

    [Fact]
    public void ExactlyAtBudgetIsNotOver()
    {
        // Boundary matters: the real corpus lands at 1,999 against 2,000.
        var r = Report(Make("a", 2_000));

        Assert.False(r.IsOverBudget);
        Assert.Equal(0, r.SkillsGoingDark);
    }

    [Fact]
    public void CountsSkillsThatFitSmallestFirst()
    {
        // 100 + 200 + 300 + 400 = 1,000; adding 1,500 would exceed 2,000.
        var r = Report(Make("a", 1_500), Make("b", 100), Make("c", 200),
                       Make("d", 300), Make("e", 400));

        Assert.Equal(4, r.SkillsThatFit);
        Assert.Equal(1, r.SkillsGoingDark);
    }

    [Fact]
    public void LargerContextWindowRaisesBudgetProportionally()
    {
        var skills = new[] { Make("a", 5_000) };

        var small = BudgetReport.Create(skills, 200_000, 0.01, 1536, "t");
        var large = BudgetReport.Create(skills, 1_000_000, 0.01, 1536, "t");

        Assert.True(small.IsOverBudget);
        Assert.Equal(2_000, small.BudgetTokens);

        Assert.False(large.IsOverBudget);
        Assert.Equal(10_000, large.BudgetTokens);
    }

    [Fact]
    public void EmptyCorpusDoesNotDivideByZero()
    {
        var r = Report();

        Assert.Equal(0, r.MetadataTokens);
        Assert.False(r.IsOverBudget);
        Assert.Equal(0, r.SkillsGoingDark);
        Assert.Equal(0, r.PercentOfWindow);
    }

    [Fact]
    public void FlagsBodiesOverSpecGuidance()
    {
        var under = Make("small", 10, body: 4_999, lines: 100);
        var over = Make("large", 10, body: 5_001, lines: 600);

        Assert.False(under.ExceedsBodyGuidance);
        Assert.True(over.ExceedsBodyGuidance);
        Assert.False(under.ExceedsLineGuidance);
        Assert.True(over.ExceedsLineGuidance);
    }

    [Fact]
    public void PercentOfWindowIsComputedAgainstTheWholeWindow()
    {
        var r = Report(Make("a", 2_000));
        Assert.Equal(1.0, r.PercentOfWindow, 3);
    }
}
