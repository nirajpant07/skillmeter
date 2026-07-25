using SkillMeter.Cli;
using Xunit;

namespace SkillMeter.Tests;

/// <summary>
/// The exit-code contract. This is the most CI-critical behaviour in the tool and
/// had no test at all, because the decision was inline in Program.Main and could
/// only be observed by spawning a process.
/// </summary>
public sealed class GateTests
{
    private static Options Opts(int? failOn = null, bool failOverBudget = false)
        => new() { FailOn = failOn, FailOverBudget = failOverBudget };

    [Fact]
    public void CleanRunExitsZero()
    {
        var r = Gate.Evaluate(Fixtures.ReportWithMetadata(2_000, 500), Opts());

        Assert.Equal(ExitCode.Ok, r.Code);
        Assert.Null(r.Message);
    }

    [Fact]
    public void OverBudgetWithoutAGateStillExitsZero()
    {
        // Documented: exit 1 fires ONLY when a gate is requested. Measuring is not
        // failing — a pipeline that wants the report must not be broken by it.
        var r = Gate.Evaluate(Fixtures.ReportWithMetadata(2_000, 26_525), Opts());

        Assert.Equal(ExitCode.Ok, r.Code);
        Assert.Null(r.Message);
    }

    [Theory]
    [InlineData(1_000, 999, ExitCode.Ok)]      // under
    [InlineData(1_000, 1_000, ExitCode.Ok)]    // equal: "exceeds" is strictly greater
    [InlineData(1_000, 1_001, ExitCode.GateFailed)]
    public void FailOnTripsOnlyAboveTheThreshold(int threshold, int metadata, int expected)
    {
        var r = Gate.Evaluate(Fixtures.ReportWithMetadata(2_000, metadata), Opts(failOn: threshold));

        Assert.Equal(expected, r.Code);
    }

    [Fact]
    public void FailOnZeroIsALegitimateThreshold()
    {
        // Zero is explicitly allowed by the parser, so any skill at all trips it.
        Assert.Equal(ExitCode.GateFailed,
            Gate.Evaluate(Fixtures.ReportWithMetadata(2_000, 1), Opts(failOn: 0)).Code);

        Assert.Equal(ExitCode.Ok,
            Gate.Evaluate(Fixtures.ReportWithMetadata(2_000, 0), Opts(failOn: 0)).Code);
    }

    [Theory]
    [InlineData(1_999, ExitCode.Ok)]
    [InlineData(2_000, ExitCode.Ok)]           // exactly at budget is not over it
    [InlineData(2_001, ExitCode.GateFailed)]
    public void FailOverBudgetTripsOnlyAboveTheBudget(int metadata, int expected)
    {
        var r = Gate.Evaluate(Fixtures.ReportWithMetadata(2_000, metadata), Opts(failOverBudget: true));

        Assert.Equal(expected, r.Code);
    }

    [Fact]
    public void FailOnTakesPrecedenceInTheMessageWhenBothTrip()
    {
        var r = Gate.Evaluate(
            Fixtures.ReportWithMetadata(2_000, 5_000),
            Opts(failOn: 100, failOverBudget: true));

        Assert.Equal(ExitCode.GateFailed, r.Code);
        Assert.Contains("--fail-on", r.Message);
    }

    [Fact]
    public void GateMessagesNameTheOffendingNumbers()
    {
        var failOn = Gate.Evaluate(Fixtures.ReportWithMetadata(2_000, 5_000), Opts(failOn: 100));
        Assert.Contains("5,000", failOn.Message);
        Assert.Contains("100", failOn.Message);

        var overBudget = Gate.Evaluate(Fixtures.ReportWithMetadata(2_000, 5_000), Opts(failOverBudget: true));
        Assert.Contains("5,000", overBudget.Message);
        Assert.Contains("2,000", overBudget.Message);
    }

    [Fact]
    public void ExitCodesMatchTheDocumentedContract()
    {
        // HelpText: "0 ok  1 over budget  2 usage error  3 runtime error"
        Assert.Equal(0, ExitCode.Ok);
        Assert.Equal(1, ExitCode.GateFailed);
        Assert.Equal(2, ExitCode.UsageError);
        Assert.Equal(3, ExitCode.RuntimeError);
    }
}
