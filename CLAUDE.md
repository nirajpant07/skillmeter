# CLAUDE.md — working context for skillmeter

Read this before changing anything. It records decisions that are expensive to
rediscover and invariants that are easy to break silently.

## What this is

A CLI that measures what agent skills cost in context. It is **not** a skill
installer, linter, or runtime tracer — those niches have strong incumbents
(`skills`/`apm`, `agnix`, `skilltrace`). Cost accounting is the one thing nobody
else does, and it is the entire reason this project exists.

Positioning, verbatim: *`skillsight` tells you what you have. `skilltrace` tells you
what fired. Neither tells you what it costs.*

## Non-negotiable invariants

Breaking any of these breaks the product's reason to exist. Do not trade them away
for convenience.

1. **Read-only.** Never write, create, or modify any file outside stdout. No config
   mutation, no cache, no state directory. A competitor shipped a config-rewriting
   `init` command that its author admitted was never tested against a real config;
   not doing that is a selling point.
2. **No network in the core path.** No API key, no telemetry, no update check, no
   model call. The tokenizer vocabulary is embedded in the binary. This is what makes
   the tool usable in CI, in a fork, and offline.
3. **Zero third-party dependencies.** Only `Microsoft.ML.Tokenizers` and its data
   package, both first-party. The single-binary property is a promise, not a detail.
   Adding a dependency needs a very good reason.
4. **NativeAOT-compatible.** No reflection, no `Reflection.Emit`, no dynamic
   `JsonSerializer` overloads — JSON goes through `SkillMeterJsonContext` source
   generation. The build sets `TreatWarningsAsErrors` with the trim and AOT
   analyzers on; keep it that way.
5. **Never execute anything found on disk.** Skills bundle `scripts/`. We read
   `.md` as text and count tokens. That is all.
6. **Cross-agent, not Claude-Code-only.** Scanning `.agents/skills/` and the other
   agent roots is the durable differentiator a first-party tool will never have.

## Verified numbers — do not restate from memory

These were measured, then re-derived after bug fixes. If you change the scanner or
parser, **re-measure and update the README**, don't guess.

| | value |
|---|---|
| Budget model | `skillListingBudgetFraction` 0.01 x 200,000 = **2,000 tokens** |
| Per-skill listing cap | `skillListingMaxDescChars` = **1,536 chars** |
| Launch headline | `obra/superpowers` + `anthropics/skills` = **1,999 tokens vs 2,000** |
| 4-pack corpus | 235 found / **230 listed** / 5 excluded → **26,525 tok, 13.3x**, 182 dark |
| `managedcode/dotnet-skills` | 179 files / 174 listed → 23,227 tok, **11.6x**, 150 dark |
| Bodies / resources | 502,247 tok / 1,256,645 tok across 593 files |
| `chars/4` over-count | ~19% (26,525 real vs 32,566 approximate) |
| Binary | 4.9 MB, libc-only, **3 ms cold start**, 743 ms for 235 skills |

**Figures of 27,355 / 13.7x / 154-dark appear in older notes and are WRONG.** They
predate the frontmatter fixes and the `disable-model-invocation` exclusion.

## Correctness bar

Frontmatter parsing agrees **exactly with PyYAML across all 230 listed skills — 0
tokens of difference.** That is the standard to hold. If you touch
`FrontmatterParser`, re-run that comparison before trusting the output.

Reproduce it by scanning a corpus with `--json` and comparing `metadataTokens`
per skill against `yaml.safe_load` + `tiktoken` `o200k_base` on the same files.

## Bugs already found and fixed — do not reintroduce

Every one of these was live, and each has a regression test in `RegressionTests.cs`.

- **Folded scalars.** `>` and `>-` fold to spaces; only `|` preserves newlines.
  Treating folded as literal was the largest single source of over-counting — 116 of
  235 corpus descriptions use `>`, against 1 using `|-`.
- **Comments.** A full-line `#` comment *anywhere* is skipped, not just before the
  first key. Previously one after a key was appended to that key's value.
- **Quoted scalars.** Double-quoted values are unescaped; a value with interior
  quotes (`"a" and "b"`) is not a wrapped scalar and must be left alone.
- **Closing fence at column 0 only.** Trimming before comparing let an indented
  `---` inside a block scalar end the frontmatter early, truncating the description
  and spilling keys into the body.
- **Symlinked prefixes.** `RealPath` resolves *every* path component and restarts
  resolution after following a link. Resolving only the final component misses the
  common case — a symlinked parent, e.g. `/tmp -> /private/tmp` on macOS, or
  `.opencode/skills -> ../skills/` which packs use to mirror one directory into
  several agent paths. Getting this wrong double-counts.
- **Resource walk.** It must apply the same exclusions, symlink dedup and
  nested-skill rules as discovery. `EnumerateFiles(AllDirectories)` applied none:
  a `references/up -> ..` link counted one file 41 times.
- **Excluded directory names.** `build`, `dist`, `bin` etc. exclude *contents*, but a
  skill legitimately named `build` must still be found.
- **`disable-model-invocation`.** Those skills never enter the listing, so they cost
  zero per session and are excluded from the budget.
- **Device files.** A FIFO named `SKILL.md` hangs `ReadAllText` forever; `/dev/zero`
  exhausts memory. `IsRegularFile` guards both.
- **`--fraction NaN`.** Every NaN comparison is false, so a range check alone lets it
  through, and it then poisons the budget and the JSON writer. Use `double.IsFinite`.
- **`BodyLines`.** A trailing newline terminates the last line; don't report 501.
- **Hardcoded version.** `Program.Version` was a hand-maintained const, so
  `dotnet publish -p:Version=x` moved the assembly metadata while the binary kept
  reporting the checked-in number — from the version flag and from `toolVersion` in
  the JSON contract. Every release would have shipped binaries that misreport
  themselves. It now comes from MSBuild `$(Version)` through a generated
  `BuildInfo` const (generated, not reflected, because
  `AssemblyInformationalVersionAttribute` lookup is the reflection AOT disallows).
  `release.yml` asserts the built binary reports the version it was stamped with.

## Known gaps

- **Windows: executed and working, with two residuals.** Verified on Windows 11 /
  SDK 10.0.302: 70 pass, 3 skip, build clean. `budget`, `cost`, `roots`, `--json`
  and every exit code behave. Path handling was probed directly — drive roots
  (`C:\`, bare `X:`), UNC (`\\host\C$\...`), extended-length (`\\?\C:\`), trailing
  separators, forward slashes and mixed case all produce byte-identical results
  (18 skills / 1,638 tokens on `anthropics/skills`). Residuals:
  - ~~**Real symlinks are still unexecuted.**~~ **Resolved in CI.** A GitHub
    `windows-latest` runner is elevated enough to create symlinks without Developer
    Mode, so all six link tests — three symlink, three junction — genuinely execute
    there, and `SKILLMETER_REQUIRE_LINKS=1` is now enforced on all three OSes so the
    coverage cannot lapse silently again. An ordinary *unelevated* Windows dev
    machine still skips the symlink three and relies on the junction three
    (`JunctionMirroringSkillsIsCountedOnce`, `ResourceWalkDoesNotFollowJunctionLoops`,
    `JunctionWithAJunctionedPrefixStillDeduplicates`), which cover the same
    reparse-point logic including the linked-*prefix* restart, because `mklink /J`
    stores its target verbatim. The one branch junctions cannot reach is a
    **relative** link target; Windows junctions are always absolute.
  - ~~**NativeAOT has never been compiled for Windows.**~~ **Resolved in CI.** All
    six RIDs now build under NativeAOT and each runner smoke-runs its own binary
    (`--help`, `--version`, a real scan) before archiving. Sizes land at 4.9–5.1 MB,
    matching the figure above. Locally, `dotnet publish -r win-x64` still fails
    without the MSVC linker — that is a workstation prerequisite, not a product gap
    (see Build).

- **Line endings shift layers 2 and 3, not the budget.** Frontmatter is `\r`-stripped,
  so `metadataTokens` — the headline number and what `--fail-on` gates — is identical
  between an LF and a CRLF checkout. Bodies are not stripped, so a Git-for-Windows
  default checkout (`core.autocrlf=true`) reads +0.4% body and +0.7% resource tokens.
  That is faithful to what is on disk; `ListingMetadataIsIndependentOfLineEndings`
  locks the part that must not move.

- **Case-insensitivity finds slightly more on Windows.** `Directory.GetFiles(dir,
  "*.md")` matches `NOTES.MD`, and `File.Exists(".../SKILL.md")` matches `Skill.md`.
  Both are inherent filesystem semantics rather than defects, but a pack with
  odd-cased names measures marginally higher on Windows than on Linux. (The Win32
  3-character-extension wildcard quirk does *not* apply: `*.md` is two characters, and
  `.mdx`/`.markdown` were confirmed unmatched.)
- **CI workflows are untested.** The 6-runner NativeAOT release matrix has never run.
  Cross-OS AOT compilation is unsupported, which is why each RID gets its own runner.
- **`JsonReporter`, `TextReporter` and `Program` have no tests.** Exit codes and the
  JSON schema are verified only by hand and by the CI smoke step.
- **`SkillScanner.RealPath` is `internal`** with no `InternalsVisibleTo`, so the most
  intricate function here is only tested indirectly.

## Conventions

- Comments explain *why*, especially where the code looks odd — the hand-rolled
  frontmatter reader and `RealPath` both look like reinvention until you know what
  they work around. Keep those explanations.
- `--json` is a contract. Bump `schemaVersion` on any breaking change.
- Exit codes are a contract: `0` ok, `1` over budget, `2` usage error, `3` runtime.
  `1` only ever fires when a gate is requested.
- Prefer refusing scope over adding it. A tool that stays small still works after six
  months of neglect, which is the actual maintenance model here.

## Build

```bash
dotnet build
dotnet test
dotnet publish src/SkillMeter/SkillMeter.csproj -c Release -r win-x64
```

Requires .NET SDK 10+ (`ToolPackageRuntimeIdentifiers` is a v10 feature).

On Windows the AOT publish additionally needs the **Desktop development with C++**
workload (the MSVC linker); without it the publish fails with "Platform linker not
found" after the managed build succeeds. `dotnet build` and `dotnet test` do not
need it. To exercise the tool on Windows without that workload:

```bash
dotnet publish src/SkillMeter/SkillMeter.csproj -c Release -r win-x64 --self-contained -p:PublishAot=false
```

Run the suite from an elevated prompt, or with Developer Mode on, so the symlink
tests execute; set `SKILLMETER_REQUIRE_LINKS=1` to make skipping them a failure.

`Microsoft.Bcl.Memory` is pinned forward to 10.0.10 because the version
`Microsoft.ML.Tokenizers` 2.0.0 resolves transitively carries advisory
GHSA-73j8-2gch-69rq. Remove the pin once the tokenizer package raises its floor.
