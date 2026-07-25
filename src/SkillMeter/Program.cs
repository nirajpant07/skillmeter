using SkillMeter.Cli;
using SkillMeter.Model;
using SkillMeter.Output;
using SkillMeter.Scanning;
using SkillMeter.Tokenizing;

namespace SkillMeter;

public static class Program
{
    /// <summary>
    /// Comes from MSBuild <c>$(Version)</c> via the generated <c>BuildInfo</c>, so a
    /// release stamped with <c>-p:Version=x</c> actually reports x. This was a
    /// hardcoded literal, which meant every published binary reported the number
    /// that happened to be checked in — including in the <c>toolVersion</c> field
    /// of the --json contract.
    /// </summary>
    public const string Version = BuildInfo.Version;

    // Exit codes live in ExitCode, and the gating decision in Gate.Evaluate, so the
    // contract can be tested without spawning a process.
    private const int Ok = ExitCode.Ok;
    private const int UsageError = ExitCode.UsageError;
    private const int RuntimeError = ExitCode.RuntimeError;

    public static int Main(string[] args)
    {
        var options = ArgParser.Parse(args, out var error);

        if (error is not null)
        {
            Console.Error.WriteLine($"skillmeter: {error}");
            return UsageError;
        }

        switch (options.Command)
        {
            case Command.Help:
                Console.WriteLine(HelpText.Full);
                return Ok;
            case Command.Version:
                Console.WriteLine(Version);
                return Ok;
        }

        try
        {
            return Run(options);
        }
        catch (DirectoryNotFoundException ex)
        {
            Console.Error.WriteLine($"skillmeter: {ex.Message}");
            return UsageError;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"skillmeter: unexpected error: {ex.Message}");
            return RuntimeError;
        }
    }

    private static int Run(Options o)
    {
        ITokenCounter counter = o.UseApproxTokenizer
            ? new ApproximateCounter()
            : new TiktokenCounter();

        var projectDir = Directory.GetCurrentDirectory();

        if (o.Command == Command.Roots)
            return ShowRoots(projectDir, o.Json);

        var scanner = new SkillScanner(counter, o.MaxDescChars);

        var skills = o.Path is not null
            ? scanner.ScanPath(o.Path)
            : scanner.ScanDefaultRoots(projectDir);

        var report = BudgetReport.Create(
            skills, o.ContextWindow, o.Fraction, o.MaxDescChars, counter.Name, scanner.Skipped);

        // JSON always emits a full envelope, empty corpus included, so a pipeline
        // never has to special-case "found nothing".
        if (skills.Count == 0 && !o.Json)
        {
            Console.Error.WriteLine("skillmeter: no skills found.");
            Console.Error.WriteLine();
            Console.Error.WriteLine("  Searched the standard agent locations. Run 'skillmeter roots' to see them,");
            Console.Error.WriteLine("  or point at a directory directly:  skillmeter ./path/to/skills");

            // Finding nothing because nothing is installed and finding nothing
            // because the directory was unreadable look identical otherwise, and
            // they call for opposite reactions.
            if (report.SkippedCount > 0)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine($"  {report.SkippedCount} path(s) could not be read:");
                foreach (var s in report.Skipped.Take(5))
                    Console.Error.WriteLine($"      {s.Path}  —  {s.Reason}");
            }

            return Evaluate(report, o);
        }

        Console.WriteLine(o.Json
            ? JsonReporter.Render(report, Version)
            : o.Command == Command.Cost
                ? TextReporter.RenderCost(report, o.Top)
                : TextReporter.RenderBudget(report));

        return Evaluate(report, o);
    }

    /// <summary>Applies the CI gates and reports why, if one tripped.</summary>
    private static int Evaluate(BudgetReport report, Options o)
    {
        var gate = Gate.Evaluate(report, o);
        if (gate.Message is not null) Console.Error.WriteLine($"skillmeter: {gate.Message}");
        return gate.Code;
    }

    private static int ShowRoots(string projectDir, bool json)
    {
        var roots = SkillScanner.CandidateRoots(projectDir).ToList();

        // --json is documented as a global option, so honour it here too rather
        // than printing prose into someone's jq pipeline.
        if (json)
        {
            Console.WriteLine(JsonReporter.RenderRoots(roots, Version));
            return Ok;
        }

        Console.WriteLine();
        Console.WriteLine("  Locations skillmeter scans (read-only; nothing is ever written):");
        Console.WriteLine();

        foreach (var root in roots)
        {
            var mark = Directory.Exists(root.Path) ? "found  " : "       ";
            Console.WriteLine($"  {mark} {root.Agent,-10} {root.Scope,-8} {root.Path}");
        }

        Console.WriteLine();
        return Ok;
    }
}
