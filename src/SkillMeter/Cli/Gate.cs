using SkillMeter.Model;

namespace SkillMeter.Cli;

/// <summary>
/// Process exit codes. Part of the CLI contract that CI pipelines depend on, so
/// these values are as public as the JSON schema and change only with a good reason.
/// </summary>
public static class ExitCode
{
    public const int Ok = 0;

    /// <summary>
    /// A gate the caller explicitly asked for did not hold: --fail-on,
    /// --fail-over-budget or --strict. Never returned unless one was requested.
    /// </summary>
    public const int GateFailed = 1;

    public const int UsageError = 2;
    public const int RuntimeError = 3;
}

/// <summary>An exit code plus the stderr line that explains it, if any.</summary>
public readonly record struct GateResult(int Code, string? Message)
{
    public static GateResult Pass => new(ExitCode.Ok, null);
}

/// <summary>
/// The CI gating decision, lifted out of Program.Main so it can be asserted
/// directly. It used to live inline, which meant the only way to check the exit-code
/// contract was to spawn a process and scrape stderr — so in practice it was checked
/// by hand, and the most CI-critical behaviour in the tool had no test at all.
/// </summary>
public static class Gate
{
    /// <summary>
    /// Decides the exit code for a completed scan.
    ///
    /// Exit 1 fires ONLY when a gate was explicitly requested. A plain run that
    /// happens to be over budget still exits 0: reporting a number is not the same
    /// as failing, and a tool that exited non-zero just for measuring would be
    /// unusable in any pipeline that also wants the report.
    ///
    /// --fail-on is evaluated before --fail-over-budget so the explicit threshold
    /// wins the message when both are supplied and both trip.
    /// </summary>
    public static GateResult Evaluate(BudgetReport report, Options options)
    {
        // Checked first, and deliberately so. If part of the corpus could not be
        // read, every number below is a lower bound, and "under budget" from an
        // incomplete scan is a pass caused by the failure itself. That is the one
        // verdict most worth refusing to give.
        if (options.Strict && report.SkippedCount > 0)
        {
            return new GateResult(
                ExitCode.GateFailed,
                $"{report.SkippedCount} path(s) could not be read and the measurement is " +
                "therefore a lower bound; --strict was requested.");
        }

        if (options.FailOn is { } threshold && report.MetadataTokens > threshold)
        {
            return new GateResult(
                ExitCode.GateFailed,
                $"listing metadata is {report.MetadataTokens:N0} tokens, " +
                $"over the --fail-on threshold of {threshold:N0}.");
        }

        if (options.FailOverBudget && report.IsOverBudget)
        {
            return new GateResult(
                ExitCode.GateFailed,
                $"listing metadata is {report.MetadataTokens:N0} tokens, " +
                $"over the {report.BudgetTokens:N0}-token budget.");
        }

        return GateResult.Pass;
    }
}
