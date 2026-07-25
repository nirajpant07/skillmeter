using SkillMeter.Cli;
using SkillMeter.Model;
using Xunit;

namespace SkillMeter.Tests;

/// <summary>
/// HelpText states defaults as fact. These pin the code to what the help promises,
/// so a changed default cannot silently make the documentation wrong.
/// </summary>
public sealed class HelpContractTests
{
    private static Options Parsed(params string[] args)
    {
        var o = ArgParser.Parse(args, out var error);
        Assert.Null(error);
        return o;
    }

    [Fact]
    public void DocumentedDefaultsMatchTheParser()
    {
        var o = Parsed();

        Assert.Equal(Command.Budget, o.Command);   // "budget ... (default)"
        Assert.Equal(25, o.Top);                   // "Default 25."
        Assert.Equal(200_000, o.ContextWindow);    // "Default 200000."
        Assert.Equal(0.01, o.Fraction);            // "Default 0.01"
        Assert.Equal(1_536, o.MaxDescChars);       // "Default 1536"
        Assert.False(o.UseApproxTokenizer);        // "o200k (default)"
        Assert.False(o.Json);
        Assert.Null(o.FailOn);
        Assert.False(o.FailOverBudget);
    }

    [Fact]
    public void DocumentedDefaultsMatchTheConstants()
    {
        Assert.Equal(200_000, Constants.DefaultContextWindow);
        Assert.Equal(0.01, Constants.DefaultBudgetFraction);
        Assert.Equal(1_536, Constants.DefaultMaxDescChars);
    }

    [Fact]
    public void HelpTextStatesTheDefaultsItIsPinnedTo()
    {
        // Guards the other direction: if someone edits the prose, these fail too.
        Assert.Contains("Default 25.", HelpText.Full);
        Assert.Contains("Default 200000.", HelpText.Full);
        Assert.Contains("Default 0.01", HelpText.Full);
        Assert.Contains("Default 1536", HelpText.Full);
        Assert.Contains("0  ok", HelpText.Full);
    }

    [Theory]
    [InlineData("budget", Command.Budget)]
    [InlineData("cost", Command.Cost)]
    [InlineData("roots", Command.Roots)]
    public void DocumentedCommandsParse(string verb, Command expected)
    {
        Assert.Equal(expected, Parsed(verb).Command);
    }

    [Fact]
    public void ABareArgumentIsTreatedAsThePath()
    {
        // "A bare argument works too."
        Assert.Equal("./skills", Parsed("./skills").Path);
        Assert.Equal("./skills", Parsed("cost", "./skills").Path);
    }

    [Fact]
    public void DocumentedExamplesAllParse()
    {
        Assert.Equal(40, Parsed("cost", "--top", "40").Top);

        var json = Parsed("./skills", "--json");
        Assert.True(json.Json);
        Assert.Equal("./skills", json.Path);

        Assert.Equal(2_000, Parsed("--fail-on", "2000").FailOn);
        Assert.Equal(1_000_000, Parsed("--window", "1000000").ContextWindow);
    }

    [Fact]
    public void HelpAndVersionWinFromAnyPosition()
    {
        foreach (var flag in new[] { "--help", "-h" })
        {
            Assert.Equal(Command.Help, Parsed(flag).Command);
            Assert.Equal(Command.Help, Parsed("cost", "./x", flag).Command);
        }

        foreach (var flag in new[] { "--version", "-v" })
        {
            Assert.Equal(Command.Version, Parsed(flag).Command);
            Assert.Equal(Command.Version, Parsed("cost", "./x", flag).Command);
        }
    }

    [Fact]
    public void TheVersionReportedIsTheVersionTheBinaryWasBuiltWith()
    {
        // Regression: Program.Version was a hardcoded literal, so every published
        // binary reported the checked-in number regardless of -p:Version.
        Assert.False(string.IsNullOrWhiteSpace(Program.Version));
        Assert.Matches(@"^\d+\.\d+\.\d+", Program.Version);
    }
}
