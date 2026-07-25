namespace SkillMeter.Cli;

public static class HelpText
{
    public const string Full = """
        skillmeter — measure what your agent skills cost in context.

        USAGE
          skillmeter [command] [options]

        COMMANDS
          budget            What the skill listing costs and which skills it evicts. (default)
          cost              Per-skill breakdown, dearest first.
          roots             Show every location scanned, and which ones exist.

        OPTIONS
          -p, --path <dir>        Scan this directory instead of the standard agent
                                  locations. A bare argument works too.
              --json              Emit machine-readable JSON (versioned schema).
          -n, --top <n>           Rows to show in `cost`. Default 25.

          -w, --window <n>        Context window in tokens. Default 200000.
          -f, --fraction <x>      Share of the window reserved for the skill listing.
                                  Default 0.01, matching skillListingBudgetFraction.
              --max-desc-chars <n>  Per-skill listing cap. Default 1536, matching
                                  skillListingMaxDescChars.

              --fail-on <tokens>  Exit 1 if listing metadata exceeds this. For CI.
              --fail-over-budget  Exit 1 if the listing is over the computed budget.
              --strict            Exit 1 if any path could not be read, so an
                                  incomplete scan cannot quietly pass a gate.

              --tokenizer <t>     o200k (default) or approx (chars/4, for comparison).
          -h, --help              Show this help.
          -v, --version           Show version.

        EXIT CODES
          0  ok        1  gate failed        2  usage error        3  runtime error

          1 is returned only when a gate was requested: --fail-on,
          --fail-over-budget or --strict. Measuring is never itself a failure.

        EXAMPLES
          skillmeter                          Measure everything installed.
          skillmeter cost --top 40            The 40 most expensive skills.
          skillmeter ./skills --json          Measure a pack, emit JSON.
          skillmeter --fail-on 2000           Gate a pull request on listing size.
          skillmeter --strict                 Fail if anything could not be read.
          skillmeter --window 1000000         Model a 1M-token context window.

        HOW IT WORKS
          Agent skills load in three layers. skillmeter measures all three:

            every session   name + description of EVERY installed skill. Always paid.
            on activation   the SKILL.md body, when the skill fires.
            on demand       bundled references/ and assets/ files.

          Claude Code reserves only skillListingBudgetFraction (default 1%) of the
          context window for that first layer. Exceed it and descriptions for the
          least-used skills are dropped — the agent sees names it cannot route to.

        NOTES
          skillmeter is strictly read-only. It never writes to or modifies any agent
          configuration.

          Token counts use o200k_base, which is a proxy for Claude's tokenizer rather
          than an exact match — Anthropic publishes no offline tokenizer. Absolute
          numbers are close estimates; comparisons between skills are reliable.

          https://github.com/nirajpant07/skillmeter
        """;
}
