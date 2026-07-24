# Backlog

Specs for Claude Code to implement, ordered. Each is scoped to one sitting.
Read `CLAUDE.md` first — it holds the invariants and the already-fixed bugs.

Nothing here should be started before **T1**, because T1 may invalidate the others.

---

## T1 — Verify Windows, or drop the claim ✅ DONE (2026-07-25)

Executed on Windows 11 / SDK 10.0.302 and in CI on all three OSes.

- 70 pass / 3 skip locally; the three symlink tests no longer self-skip silently —
  they report a visible skip, and `SKILLMETER_REQUIRE_LINKS=1` makes skipping a
  failure. **In CI all six link tests genuinely execute on Windows**, because a
  GitHub `windows-latest` runner can create symlinks without Developer Mode.
- Junction-based equivalents give unelevated Windows machines real coverage of the
  same reparse-point logic.
- Path forms all verified identical: drive roots (`C:\`, bare `X:`), UNC,
  extended-length (`\\?\`), trailing separators, forward slashes, mixed case.
- **No defect was found in `SkillScanner.RealPath`.** The one bug fixed was in test
  teardown, which broke on reparse points and would have failed an elevated run too.
- Line endings: `metadataTokens` is CRLF-stable; bodies drift +0.4% on a CRLF
  checkout. Documented in CLAUDE.md, locked by a regression test.

Residual: NativeAOT for Windows — see T4.

<details>
<summary>Original spec</summary>

**Blocks the launch. Do this first.**

The README's comparison table claims Windows support against a competitor that lacks
it. It has never been executed on Windows.

- Install .NET SDK 10, run `dotnet test` from an **elevated** prompt or with Developer
  Mode enabled — otherwise `CountsSymlinkedSkillsOnlyOnce`,
  `ResourceWalkDoesNotFollowSymlinkLoops` and `SymlinkWithASymlinkedPrefixStillDeduplicates`
  silently return early and prove nothing.
- `dotnet publish -c Release -r win-x64`, then run the binary against a real skills
  directory and against a cloned pack.
- Check specifically: drive roots (`C:\`), UNC paths (`\\server\share`), extended-length
  paths (`\\?\C:\`), case-insensitive deduplication, CRLF frontmatter, and that
  `%USERPROFILE%` roots resolve in `skillmeter roots`.

**Done when:** all 69 tests pass on Windows *with symlink tests actually executing*,
and the binary produces correct output. If something can't be made to work, change the
README rather than shipping a false claim.

</details>

---

## T2 — Tests for the untested third of the codebase

`JsonReporter`, `TextReporter` and `Program` have zero tests. The JSON schema and exit
codes are public contracts verified only by hand.

- Extract the exit-code decision out of `Program.Main` into a testable pure function
  so `--fail-on` / `--fail-over-budget` can be asserted without spawning a process.
- Assert the full `--json` envelope: `schemaVersion` is 1, every documented key is
  present, `budgetMultiple` rounds to 3 places, `skills` is sorted by
  `metadataTokens` descending, and an empty corpus still emits a valid envelope.
- Add `InternalsVisibleTo` for the test project and test `SkillScanner.RealPath`
  directly — it is the most intricate function here and is currently only exercised
  indirectly.
- Cover `TextReporter` for: over budget, under budget, exactly at budget, empty
  corpus, and the `1 token of headroom` singular case.

**Done when:** every documented contract in `HelpText.cs` has a test asserting it.

---

## T3 — Diagnose skipped files instead of silently under-counting

An unreadable `SKILL.md` or a permission-denied subtree is currently swallowed: the
count drops, the report says nothing, and the process still exits 0. For a tool that
gates CI, silently measuring less than reality is the worst failure mode available.

- Count read failures during the scan (path + reason).
- Surface as a `Findings` line in text output and a `skipped[]` array plus
  `skippedCount` in JSON. Bump `schemaVersion` to 2.
- Add `--strict` to exit non-zero when anything was skipped.

**Done when:** scanning a directory containing an unreadable file reports it in both
output modes.

---

## T4 — Make the CI matrix actually run

**Correction:** the workflows did not exist at all — `.github/` was absent, so they
were never saved. Both were written from scratch on 2026-07-25.

- ✅ `ci.yml` runs green on ubuntu, windows and macos. Smoke steps use a real
  populated corpus built inline (the spec below correctly predicted that pointing at
  `tests/` would only exercise the empty-corpus branch).
- ✅ `SKILLMETER_REQUIRE_LINKS=1` enforced on all three OSes.
- ⏳ `release.yml`: six-RID NativeAOT matrix, GitHub Releases only. Registry
  publishing deliberately excluded until the matrix is proven — NuGet cannot delete
  a published package and npm's unpublish window is narrow, so a botched version
  number is burned permanently on both.
- ☐ npm + NuGet publishing. Needs `NPM_TOKEN`/`NUGET_API_KEY` (or OIDC trusted
  publishing) and the six per-platform npm packages, which do not exist yet. The
  README already advertises `npx skillmeter`, `npm install -g` and
  `dotnet tool install -g`, **none of which work** — that is now a public promise
  the repo does not keep.

<details>
<summary>Original spec</summary>

- Push and let `ci.yml` run on all three OSes; fix what breaks.
- The release matrix is the risky one: cross-OS NativeAOT is unsupported, so each RID
  builds on its own runner, including the ARM runners which are the likeliest to fail.
- Dry-run the release path with `workflow_dispatch` against a scratch version before
  tagging anything real.
- Confirm the CI smoke step exercises a *populated* corpus — it currently points at
  `tests/`, which contains no `SKILL.md`, so it only ever tests the empty-corpus branch.

**Done when:** a tagged pre-release produces six working binaries and publishes to both
registries without manual intervention.

</details>

---

## T5 — Demo GIF

The launch asset. Evidence says demonstrations outperform descriptions by roughly 10x.

Record the two-pack case: run `skillmeter`, land on **1,999 tokens against a 2,000-token
budget**, then install one more pack and watch skills go dark. That contrast is the
whole pitch and it takes about fifteen seconds.

Keep it short, no music, readable at GitHub's rendered width.

---

## Deliberately not doing

- **`collide` / description-collision detection.** Validated as a non-problem: max
  pairwise TF-IDF cosine across 235 real descriptions was 0.574, mean 0.0217, with
  exactly one cross-pack name collision. Skill authors write more distinctly than the
  discourse assumes. It is also the one feature `skillsight` already ships.
- **`usage` / runtime firing data.** `skilltrace` does this well across three agents.
  Joining firing data to cost is the v2 story *if* v1 finds an audience — not before.
- **An MCP server.** Spec revisions land roughly every 158 days; 22.5% of registered
  endpoints are already dead. Wrong archetype for bursty maintenance.
- **A docs site.** A good README is enough, and docs are a maintenance liability with
  a shrinking payoff.
