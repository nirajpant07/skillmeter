# Contributing

Thanks for looking. This document is deliberately blunt so nobody wastes an afternoon.

## Expectations

**This is a solo project maintained in bursts.** Availability is genuinely unpredictable — sometimes a few evenings a week, sometimes nothing for a month. Issues and pull requests get a response when they get one. That is not indifference; it is the honest operating model, and it is better to say so than to imply a service level that will not hold.

If `skillmeter` is useful to you and unmaintained when you need it, fork it. It is MIT for exactly that reason.

## Scope

`skillmeter` measures what agent skills cost in context. That is the whole remit.

**In scope**

- Accuracy of token counting and the budget model
- Support for additional skill locations as agents add them
- Bugs in scanning: symlinks, permissions, unusual layouts, path handling
- Output formats that serve automation
- Platform bugs, especially on Windows

**Out of scope**

- Installing, syncing, or otherwise managing skills — `skills` and `apm` do that well
- Runtime tracing of which skills fired — `skilltrace` does that well
- Linting skill content or quality — `agnix` does that well
- Anything requiring an API key, a daemon, or a network call in the core path
- Anything that writes to agent configuration

Scope refusal is a feature here, not obstruction. A tool that stays small is a tool that still works after six months of neglect.

## Before opening a pull request

Please open an issue first for anything beyond a bug fix. An unsolicited large PR is likelier to be declined than merged, however good it is — not because of its quality, but because every merged feature is maintenance someone has to carry.

## Requirements for a change

- `dotnet test` passes on your platform
- New behaviour has tests
- No new runtime dependencies without discussion. The single-binary, zero-dependency property is a core promise, not an implementation detail
- NativeAOT compatibility is preserved — no reflection, no `Reflection.Emit`, no dynamic serialization. The build treats trim and AOT warnings as errors
- Public behaviour changes are reflected in `--help` and the README

## AI-assisted contributions

Use whatever tools you like. But if you cannot explain what your change does and how it interacts with the rest of the codebase without the assistance of those tools, please do not open the pull request. Review time is the scarce resource here.

## Building

```bash
dotnet build
dotnet test
dotnet publish src/SkillMeter/SkillMeter.csproj -c Release
```

NativeAOT cannot cross-compile, so a given platform's binary must be built on that platform. CI covers Linux, macOS and Windows.

## Security

Please do not open a public issue for a vulnerability. See [SECURITY.md](SECURITY.md).
