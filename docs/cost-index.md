# Skill pack cost index

What published skill packs cost in Claude Code's listing budget — the tokens paid
on **every session**, before you type anything.

Regenerated weekly by [`cost-index.yml`](../.github/workflows/cost-index.yml).
Every figure is a real scan of a fresh clone; each pack's commit SHA is recorded so
any row can be reproduced. Measured with skillmeter `0.1.0` against the default
budget of **2,000 tokens** (1% of a 200,000-token window).

Last regenerated: **2026-11-02**

| pack | commit | skills | listed | listing tokens | vs budget | go dark | body | resources |
|---|---|--:|--:|--:|--:|--:|--:|--:|
| [obra/superpowers](https://github.com/obra/superpowers) | `2ce8aa1` | 15 | 15 | **474** | 0.24x | 0 | 32,037 | 50,583 |
| [anthropics/skills](https://github.com/anthropics/skills) | `ca45fc4` | 20 | 20 | **2,047** | 1.02x | 1 | 66,296 | 461,731 |
| [addyosmani/agent-skills](https://github.com/addyosmani/agent-skills) | `301b02b` | 25 | 25 | **1,892** | 0.95x | 0 | 74,957 | 15,274 |
| [managedcode/dotnet-skills](https://github.com/managedcode/dotnet-skills) | `497cc9a` | 201 | 197 | **25,219** | 12.61x | 172 | 446,217 | 1,332,266 |

## All of them installed together

| | |
|---|--:|
| skills found | 261 |
| entering the listing | 257 |
| **listing metadata** | **29,632 tokens** |
| budget | 2,000 tokens |
| over budget by | 27,632 tokens (14.82x) |
| keep their description | 45 |
| **go dark** | **212** |

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
