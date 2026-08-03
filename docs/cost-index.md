# Skill pack cost index

What published skill packs cost in Claude Code's listing budget — the tokens paid
on **every session**, before you type anything.

Regenerated weekly by [`cost-index.yml`](../.github/workflows/cost-index.yml).
Every figure is a real scan of a fresh clone; each pack's commit SHA is recorded so
any row can be reproduced. Measured with skillmeter `0.1.0` against the default
budget of **2,000 tokens** (1% of a 200,000-token window).

Last regenerated: **2026-08-03**

| pack | commit | skills | listed | listing tokens | vs budget | go dark | body | resources |
|---|---|--:|--:|--:|--:|--:|--:|--:|
| [obra/superpowers](https://github.com/obra/superpowers) | `44c9b2d` | 14 | 14 | **361** | 0.18x | 0 | 28,916 | 35,242 |
| [anthropics/skills](https://github.com/anthropics/skills) | `b29e7cf` | 18 | 18 | **1,638** | 0.82x | 0 | 53,095 | 227,162 |
| [addyosmani/agent-skills](https://github.com/addyosmani/agent-skills) | `7829ffd` | 24 | 24 | **1,299** | 0.65x | 0 | 64,693 | 6,731 |
| [managedcode/dotnet-skills](https://github.com/managedcode/dotnet-skills) | `902c435` | 179 | 175 | **23,294** | 11.65x | 151 | 357,832 | 988,770 |

## All of them installed together

| | |
|---|--:|
| skills found | 235 |
| entering the listing | 231 |
| **listing metadata** | **26,592 tokens** |
| budget | 2,000 tokens |
| over budget by | 24,592 tokens (13.30x) |
| keep their description | 48 |
| **go dark** | **183** |

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
