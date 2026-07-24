# Security Policy

## Reporting a vulnerability

Please report privately through GitHub's private vulnerability reporting:

**https://github.com/nirajpant07/skillmeter/security/advisories/new**

Do not open a public issue for a security problem.

This is a solo project maintained in bursts, so please do not expect a same-day acknowledgement. A realistic expectation is an initial response within two weeks. If a report is valid and I cannot fix it promptly, I will say so publicly rather than leave users exposed.

## Supported versions

The latest released version only.

## Threat model

`skillmeter` is a local, read-only measurement tool. Understanding its boundaries is most of the security story:

- **It makes no network requests.** Not at install, not at runtime. The tokenizer vocabulary is embedded in the binary; there is no model call, no telemetry, and no update check.
- **It requires no credentials.** There is no API key, token, or configuration file.
- **It never writes to your filesystem.** No config is modified, no cache is created, no agent settings are touched. It opens files for reading and writes a report to stdout.
- **It does not execute anything it finds.** Skills may bundle scripts under `scripts/`. `skillmeter` never runs them. It reads `SKILL.md` and bundled `.md` files as text and counts tokens.

The realistic risks are therefore narrow:

1. **Untrusted input.** Scanning a hostile skills pack means parsing attacker-controlled markdown and YAML frontmatter. The parser is a small hand-rolled reader with no code paths for anchors, aliases, or type coercion, which removes the classic YAML deserialization risks. Pathological inputs causing excessive CPU or memory during a scan would still be a valid report.
2. **Path traversal and symlinks.** The scanner follows symlinks in order to deduplicate mirrored skill directories, with resolved-path tracking to prevent cycles. A case where scanning escapes the directory you pointed it at, or fails to terminate, would be a valid report.
3. **Supply chain.** Releases are built in GitHub Actions with build provenance attestations, and npm packages are published with `--provenance`. Dependencies are limited to `Microsoft.ML.Tokenizers` and its embedded vocabulary data.

## Out of scope

- Vulnerabilities in the skills you scan — `skillmeter` measures them, it does not vet them. For malicious-skill detection, use a dedicated scanner.
- Inaccurate token counts. `o200k_base` is documented as a proxy for Claude's tokenizer, not an exact match. That is a known limitation, stated in the README, not a vulnerability.
