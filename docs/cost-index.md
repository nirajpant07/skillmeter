# Skill pack cost index

What published skill packs cost in Claude Code's listing budget — the tokens paid
on **every session**, before you type anything.

Regenerated weekly by [`cost-index.yml`](../.github/workflows/cost-index.yml).
Every figure is a real scan of a fresh clone; each pack's commit SHA is recorded so
any row can be reproduced. Measured with skillmeter `0.1.0` against the default
budget of **2,000 tokens** (1% of a 200,000-token window).

Last regenerated: **2026-11-16**

| pack | commit | skills | listed | listing tokens | vs budget | go dark | body | resources |
|---|---|--:|--:|--:|--:|--:|--:|--:|
| [obra/superpowers](https://github.com/obra/superpowers) | `22f0b46` | 15 | 15 | **474** | 0.24x | 0 | 32,520 | 47,939 |
| [anthropics/skills](https://github.com/anthropics/skills) | `e4bb5d9` | 20 | 20 | **2,047** | 1.02x | 1 | 68,656 | 461,740 |
| [addyosmani/agent-skills](https://github.com/addyosmani/agent-skills) | `39b723c` | 25 | 25 | **2,060** | 1.03x | 1 | 76,262 | 15,295 |
| [managedcode/dotnet-skills](https://github.com/managedcode/dotnet-skills) | `6c69830` | 203 | 199 | **25,523** | 12.76x | 174 | 456,852 | 1,335,877 |

## All of them installed together

| | |
|---|--:|
| skills found | 263 |
| entering the listing | 259 |
| **listing metadata** | **30,104 tokens** |
| budget | 2,000 tokens |
| over budget by | 28,104 tokens (15.05x) |
| keep their description | 44 |
| **go dark** | **215** |

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
