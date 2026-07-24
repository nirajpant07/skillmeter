using SkillMeter.Cli;
using Xunit;

namespace SkillMeter.Tests;

public class ArgParserTests
{
    private static Options Parse(params string[] args)
    {
        var o = ArgParser.Parse(args, out var error);
        Assert.Null(error);
        return o;
    }

    [Fact]
    public void DefaultsToBudgetCommand()
        => Assert.Equal(Command.Budget, Parse().Command);

    [Theory]
    [InlineData("budget", Command.Budget)]
    [InlineData("cost", Command.Cost)]
    [InlineData("roots", Command.Roots)]
    public void ParsesVerbs(string verb, Command expected)
        => Assert.Equal(expected, Parse(verb).Command);

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("help")]
    public void ParsesHelp(string arg)
        => Assert.Equal(Command.Help, Parse(arg).Command);

    [Fact]
    public void TreatsBareArgumentAsPath()
        => Assert.Equal("./skills", Parse("./skills").Path);

    [Fact]
    public void TreatsBareArgumentAfterVerbAsPath()
    {
        var o = Parse("cost", "./skills");
        Assert.Equal(Command.Cost, o.Command);
        Assert.Equal("./skills", o.Path);
    }

    [Fact]
    public void ParsesBudgetTuningFlags()
    {
        var o = Parse("--window", "1000000", "--fraction", "0.05", "--max-desc-chars", "512");

        Assert.Equal(1_000_000, o.ContextWindow);
        Assert.Equal(0.05, o.Fraction, 5);
        Assert.Equal(512, o.MaxDescChars);
    }

    [Fact]
    public void ParsesCiGates()
    {
        Assert.Equal(2_000, Parse("--fail-on", "2000").FailOn);
        Assert.True(Parse("--fail-over-budget").FailOverBudget);
    }

    [Fact]
    public void ParsesJsonAndTop()
    {
        var o = Parse("cost", "--json", "--top", "50");
        Assert.True(o.Json);
        Assert.Equal(50, o.Top);
    }

    [Fact]
    public void SelectsApproximateTokenizerOnRequest()
    {
        Assert.True(Parse("--tokenizer", "approx").UseApproxTokenizer);
        Assert.False(Parse("--tokenizer", "o200k").UseApproxTokenizer);
    }

    [Theory]
    [InlineData("--unknown-flag")]
    [InlineData("--window")]           // missing value
    [InlineData("--window", "0")]      // must be positive
    [InlineData("--window", "abc")]
    [InlineData("--fraction", "0")]    // must be > 0
    [InlineData("--fraction", "2")]    // must be <= 1
    [InlineData("--tokenizer", "nope")]
    [InlineData("--fail-on", "-5")]
    public void ReportsUsageErrors(params string[] args)
    {
        ArgParser.Parse(args, out var error);
        Assert.NotNull(error);
    }

    [Fact]
    public void DefaultsMatchDocumentedClaudeCodeSettings()
    {
        var o = Parse();
        Assert.Equal(200_000, o.ContextWindow);
        Assert.Equal(0.01, o.Fraction, 5);
        Assert.Equal(1536, o.MaxDescChars);
    }
}
