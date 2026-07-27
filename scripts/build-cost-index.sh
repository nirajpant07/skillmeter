#!/usr/bin/env bash
# Regenerates docs/cost-index.md by measuring published skill packs.
#
# Runs identically locally and in CI, so the committed table can always be
# reproduced rather than taken on trust. Every figure comes from a real scan of a
# real clone, and the commit SHA of each pack is recorded beside its numbers —
# packs move, and a figure without a SHA is not reproducible.
#
#   ./scripts/build-cost-index.sh            # uses skillmeter from PATH
#   SKILLMETER=./out/skillmeter ./scripts/build-cost-index.sh
set -euo pipefail

SKILLMETER="${SKILLMETER:-skillmeter}"
OUT="${OUT:-docs/cost-index.md}"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

# One pack per line: "owner/repo".
PACKS="
obra/superpowers
anthropics/skills
addyosmani/agent-skills
managedcode/dotnet-skills
"

VERSION="$("$SKILLMETER" --version 2>/dev/null || echo unknown)"
TODAY="$(date -u +%Y-%m-%d)"

rows=""
combined_dir="$WORK/all"
mkdir -p "$combined_dir"

for repo in $PACKS; do
  [ -z "$repo" ] && continue
  name="${repo#*/}"
  dir="$WORK/$name"

  echo "cloning $repo" >&2
  if ! git clone --depth 1 -q "https://github.com/$repo" "$dir" 2>/dev/null; then
    echo "::warning::could not clone $repo — omitted from the index" >&2
    continue
  fi

  sha="$(git -C "$dir" rev-parse --short HEAD)"
  # Via a file, not argv: a large pack's report exceeds the argument-length limit.
  "$SKILLMETER" "$dir" --json > "$WORK/$name.json"

  row="$(node -e '
    const d = JSON.parse(require("fs").readFileSync(process.argv[1], "utf8"));
    const n = x => x.toLocaleString("en-US");
    const t = d.totals, b = d.budget;
    process.stdout.write(
      `| [${process.argv[2]}](https://github.com/${process.argv[2]}) | \`${process.argv[3]}\` | ` +
      `${n(t.skillCount)} | ${n(t.listedSkillCount)} | **${n(t.metadataTokens)}** | ` +
      `${b.budgetMultiple.toFixed(2)}x | ${n(b.skillsGoingDark)} | ${n(t.bodyTokens)} | ${n(t.resourceTokens)} |`
    );
  ' "$WORK/$name.json" "$repo" "$sha")"
  rows="$rows$row"$'\n'

  cp -r "$dir" "$combined_dir/$name"
done

# Measured as one corpus, not summed: installing several packs together is the
# situation the budget actually cares about, and cross-pack duplicates dedupe.
"$SKILLMETER" "$combined_dir" --json > "$WORK/all.json"

mkdir -p "$(dirname "$OUT")"
{
  cat <<HEADER
# Skill pack cost index

What published skill packs cost in Claude Code's listing budget — the tokens paid
on **every session**, before you type anything.

Regenerated weekly by [\`cost-index.yml\`](../.github/workflows/cost-index.yml).
Every figure is a real scan of a fresh clone; each pack's commit SHA is recorded so
any row can be reproduced. Measured with skillmeter \`$VERSION\` against the default
budget of **2,000 tokens** (1% of a 200,000-token window).

Last regenerated: **$TODAY**

| pack | commit | skills | listed | listing tokens | vs budget | go dark | body | resources |
|---|---|--:|--:|--:|--:|--:|--:|--:|
HEADER
  printf '%s' "$rows"

  node -e '
    const d = JSON.parse(require("fs").readFileSync(process.argv[1], "utf8"));
    const n = x => x.toLocaleString("en-US");
    const t = d.totals, b = d.budget;
    console.log(`\n## All of them installed together\n`);
    console.log(`| | |`);
    console.log(`|---|--:|`);
    console.log(`| skills found | ${n(t.skillCount)} |`);
    console.log(`| entering the listing | ${n(t.listedSkillCount)} |`);
    console.log(`| **listing metadata** | **${n(t.metadataTokens)} tokens** |`);
    console.log(`| budget | 2,000 tokens |`);
    console.log(`| over budget by | ${n(b.overageTokens)} tokens (${b.budgetMultiple.toFixed(2)}x) |`);
    console.log(`| keep their description | ${n(b.skillsThatFit)} |`);
    console.log(`| **go dark** | **${n(b.skillsGoingDark)}** |`);
  ' "$WORK/all.json"

  cat <<'FOOTER'

A skill that goes dark keeps its name in the listing but loses its description, so
the agent sees a name it cannot route to.

## Reading these numbers

**"Go dark" is a best case.** Claude Code evicts least-used skills first, which
needs runtime data skillmeter does not have. The real figure is no better than the
one shown.

**The tokenizer is a proxy.** Counts use `o200k_base`, which is OpenAI's
tokenizer — Anthropic publishes no offline tokenizer for current Claude models.
Absolute numbers are close estimates; comparisons between packs are reliable,
because every pack is measured the same way.

**A large pack is not a badly-built pack.** A pack costing more than the budget is
not doing anything wrong — it is simply larger than one session's allowance, and
the cost is only paid for what you actually install. The point of this table is to
make that cost visible before you install, not to rank quality.

Reproduce any row:

```bash
git clone --depth 1 https://github.com/<owner>/<repo>
skillmeter <repo> --json
```
FOOTER
} > "$OUT"

echo "wrote $OUT" >&2
