using SkillMeter.Model;

namespace SkillMeter.Cli;

public enum Command { Budget, Cost, Roots, Help, Version }

public sealed record Options
{
    public Command Command { get; init; } = Command.Budget;
    public string? Path { get; init; }
    public bool Json { get; init; }
    public int ContextWindow { get; init; } = Constants.DefaultContextWindow;
    public double Fraction { get; init; } = Constants.DefaultBudgetFraction;
    public int MaxDescChars { get; init; } = Constants.DefaultMaxDescChars;
    public int Top { get; init; } = 25;

    /// <summary>Exit non-zero when listing metadata exceeds this many tokens. For CI.</summary>
    public int? FailOn { get; init; }

    /// <summary>Exit non-zero whenever the listing is over the configured budget.</summary>
    public bool FailOverBudget { get; init; }

    public bool UseApproxTokenizer { get; init; }
}

/// <summary>
/// Hand-rolled argument parsing. System.CommandLine would be the obvious choice,
/// but it is a third-party dependency for something this small, and part of the
/// pitch is that skillmeter is a single self-contained binary.
/// </summary>
public static class ArgParser
{
    public static Options Parse(string[] args, out string? error)
    {
        error = null;
        var o = new Options();

        if (args.Length == 0) return o;

        var i = 0;

        // Optional leading verb.
        switch (args[0].ToLowerInvariant())
        {
            case "budget": o = o with { Command = Command.Budget }; i = 1; break;
            case "cost": o = o with { Command = Command.Cost }; i = 1; break;
            case "roots": o = o with { Command = Command.Roots }; i = 1; break;
            case "help" or "--help" or "-h": return o with { Command = Command.Help };
            case "version" or "--version" or "-v": return o with { Command = Command.Version };
        }

        for (; i < args.Length; i++)
        {
            var a = args[i];

            switch (a)
            {
                case "--json": o = o with { Json = true }; break;
                case "--fail-over-budget": o = o with { FailOverBudget = true }; break;
                case "--help" or "-h": return o with { Command = Command.Help };
                case "--version" or "-v": return o with { Command = Command.Version };

                case "--path" or "-p":
                    if (!TryValue(args, ref i, out var p)) { error = "--path needs a directory"; return o; }
                    o = o with { Path = p };
                    break;

                case "--window" or "-w":
                    if (!TryInt(args, ref i, out var w)) { error = "--window needs a positive integer"; return o; }
                    o = o with { ContextWindow = w };
                    break;

                case "--fraction" or "-f":
                    // double.IsFinite excludes NaN and the infinities. Every NaN
                    // comparison is false, so a bare range check lets NaN straight
                    // through and it then poisons the budget and the JSON writer.
                    if (!TryDouble(args, ref i, out var f) || !double.IsFinite(f) || f is <= 0 or > 1)
                    { error = "--fraction needs a finite number greater than 0 and at most 1"; return o; }
                    o = o with { Fraction = f };
                    break;

                case "--max-desc-chars":
                    if (!TryInt(args, ref i, out var m)) { error = "--max-desc-chars needs a positive integer"; return o; }
                    o = o with { MaxDescChars = m };
                    break;

                case "--top" or "-n":
                    if (!TryInt(args, ref i, out var n)) { error = "--top needs a positive integer"; return o; }
                    o = o with { Top = n };
                    break;

                case "--fail-on":
                    // Zero is a legitimate threshold, so this one allows it.
                    if (!TryInt(args, ref i, out var fo, allowZero: true))
                    { error = "--fail-on needs a token count of 0 or more"; return o; }
                    o = o with { FailOn = fo };
                    break;

                case "--tokenizer":
                    if (!TryValue(args, ref i, out var t)) { error = "--tokenizer needs a value"; return o; }
                    o = t.ToLowerInvariant() switch
                    {
                        "o200k" or "o200k_base" or "exact" => o with { UseApproxTokenizer = false },
                        "approx" or "chars4" => o with { UseApproxTokenizer = true },
                        _ => o,
                    };
                    if (t.ToLowerInvariant() is not ("o200k" or "o200k_base" or "exact" or "approx" or "chars4"))
                    { error = $"unknown tokenizer '{t}' (use o200k or approx)"; return o; }
                    break;

                default:
                    // A bare argument is treated as the path, so `skillmeter ./skills` works.
                    if (!a.StartsWith('-') && o.Path is null) { o = o with { Path = a }; break; }
                    error = $"unknown option '{a}' (try --help)";
                    return o;
            }
        }

        return o;
    }

    private static bool TryValue(string[] args, ref int i, out string value)
    {
        if (i + 1 >= args.Length) { value = ""; return false; }
        value = args[++i];
        return true;
    }

    private static bool TryInt(string[] args, ref int i, out int value, bool allowZero = false)
    {
        value = 0;
        return TryValue(args, ref i, out var s)
               && int.TryParse(s, out value)
               && (allowZero ? value >= 0 : value > 0);
    }

    private static bool TryDouble(string[] args, ref int i, out double value)
    {
        value = 0;
        return TryValue(args, ref i, out var s)
               && double.TryParse(s, System.Globalization.NumberStyles.Float,
                                  System.Globalization.CultureInfo.InvariantCulture, out value);
    }
}
