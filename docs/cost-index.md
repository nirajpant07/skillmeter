# Skill pack cost index

What published skill packs cost in Claude Code's listing budget — the tokens paid
on **every session**, before you type anything.

Regenerated weekly by [`cost-index.yml`](../.github/workflows/cost-index.yml).
Every figure is a real scan of a fresh clone; each pack's commit SHA is recorded so
any row can be reproduced. Measured with skillmeter `0.1.0` against the default
budget of **2,000 tokens** (1% of a 200,000-token window).

Last regenerated: **2026-10-05**

| pack | commit | skills | listed | listing tokens | vs budget | go dark | body | resources |
|---|---|--:|--:|--:|--:|--:|--:|--:|
| [obra/superpowers](https://github.com/obra/superpowers) | `8ca22db` | 15 | 15 | **465** | 0.23x | 0 | 38,670 | 45,494 |
| [anthropics/skills](https://github.com/anthropics/skills) | `683bc88` | 20 | 20 | **2,047** | 1.02x | 1 | 65,254 | 416,437 |
| [addyosmani/agent-skills](https://github.com/addyosmani/agent-skills) | `1401c8b` | 25 | 25 | **1,776** | 0.89x | 0 | 73,778 | 15,186 |
| [managedcode/dotnet-skills](https://github.com/managedcode/dotnet-skills) | `4fa9c32` | 190 | 187 | **24,431** | 12.22x | 163 | 414,033 | 1,318,592 |

## All of them installed together

| | |
|---|--:|
| skills found | 250 |
| entering the listing | 247 |
| **listing metadata** | **28,719 tokens** |
| budget | 2,000 tokens |
| over budget by | 26,719 tokens (14.36x) |
| keep their description | 45 |
| **go dark** | **202** |

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
