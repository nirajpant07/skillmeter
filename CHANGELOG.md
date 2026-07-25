# Changelog

All notable changes to this project are documented here.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/);
versioning follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.0] — unreleased

First release.

### Added
- `budget` — listing cost against the skill-listing budget, and how many skills lose
  their description as a result.
- `cost` — per-skill breakdown across all three progressive-disclosure layers
  (listing metadata, activation body, on-demand resources).
- `roots` — every scanned location and whether it exists.
- `--json` with a versioned schema (`schemaVersion: 2`) for scripting and CI.
- `--fail-on <tokens>`, `--fail-over-budget` and `--strict` as CI gates, with
  documented exit codes.
- Skipped-path reporting: every path the scan could not read is counted and surfaced,
  as a `Findings` block in text output and as `skipped[]` plus `totals.skippedCount`
  in JSON. Previously an unreadable `SKILL.md` or a permission-denied subtree simply
  lowered the count in silence and still exited 0 — so a CI gate could pass *because*
  the scan had failed. `--strict` exits 1 whenever anything was skipped.
- Cross-agent scanning: `.agents/skills/`, `.claude/skills/`, `.cursor/skills/`,
  `.github/skills/`, `.gemini/skills/`, `~/.codex/skills/`, `~/.config/agents/skills/`.
- Real BPE token counting via an embedded `o200k_base` vocabulary — fully offline.
- `--tokenizer approx` to compare against the `chars/4` heuristic.
- NativeAOT single-file binaries for Linux, macOS and Windows on x64 and arm64,
  published to both npm and NuGet.

### Correctness
- Frontmatter parsing verified to agree exactly with PyYAML across a 235-skill corpus
  (0 tokens of difference). Fixed in review: folded block scalars (`>`, `>-`) were
  treated as literal and joined with newlines; comments after the first key were
  appended to the preceding value; double-quoted escapes were left unescaped; and an
  indented `---` inside a block scalar terminated the frontmatter early.
- Skills marked `disable-model-invocation` are excluded from the listing budget —
  they never enter the listing, so they cost nothing per session.
- Symlinks are resolved per path component, restarting resolution whenever a link is
  followed, so a symlinked *prefix* (the `/tmp -> /private/tmp` shape on macOS) no
  longer defeats deduplication.
- Resource accounting shares the discovery walk's exclusions, symlink deduplication
  and nested-skill rules. Previously a `references/up -> ..` link recounted the same
  file 40 times and a vendored `node_modules` was billed as skill weight.
- A skill in a directory named `build`, `dist`, `bin` or similar is no longer silently
  dropped.
- FIFOs and character devices named `SKILL.md` are skipped rather than hanging the
  process or exhausting memory.
- `--fraction NaN` is rejected instead of poisoning the budget and the JSON writer.

### Notes
- Symlinked skill directories are resolved per path component and counted once. Packs
  commonly mirror one `skills/` directory into per-agent paths this way; counting
  naively inflates totals by up to 2x.
- Vendored upstream copies under `external-sources/` are excluded for the same reason.
- Pinned `Microsoft.Bcl.Memory` forward to 10.0.10; the version resolved transitively
  by `Microsoft.ML.Tokenizers` 2.0.0 carries advisory GHSA-73j8-2gch-69rq.
