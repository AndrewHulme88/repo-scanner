# MVP specification

## 1. Purpose

Repo Scanner is a local, offline command-line tool that examines a Git working
tree or ordinary directory for material that should not be committed. The MVP
focuses on useful secret detection, safe reporting, predictable automation, and
a reusable scanning core.

This document defines the required behavior of the MVP. Future functionality is
out of scope unless it is explicitly added here.

## 2. Intended users

The initial user is the project author during development. The finished product
is intended for public use by developers and in continuous-integration systems.
The MVP must therefore be cross-platform, safe with untrusted repositories, and
clear enough to operate without knowledge of its implementation.

## 3. Goals

- Scan a local working tree or directory without transmitting its contents.
- Inspect tracked and untracked, non-ignored files.
- Detect high-confidence known secret formats and sensitive tracked files.
- Return actionable findings without exposing the detected secret.
- Distinguish a clean scan, policy-violating findings, and scan failure.
- Remain bounded and responsive on large or hostile directory trees.
- Keep scanning logic reusable by future interfaces, including a GUI.
- Provide deterministic behavior that can be covered by automated tests.

## 4. Non-goals

The MVP will not:

- Scan Git commit history, branches, tags, or remote repositories.
- Rotate, revoke, delete, move, or otherwise remediate credentials or files.
- Rewrite Git history.
- Upload source code, filenames, findings, or telemetry.
- Provide a graphical interface or hosted service.
- Guarantee that a repository is free of secrets.
- Use generic entropy detection until its accuracy and false-positive behavior
  can be evaluated against representative fixtures.
- Provide every possible report format. Terminal output is required first;
  JSON and SARIF are planned extensions.

## 5. Terminology

- **Scan root:** The canonical directory selected for scanning.
- **Candidate file:** A file selected for content inspection.
- **Tracked file:** A file present in the Git index.
- **Ignored file:** An untracked file excluded by applicable Git ignore rules.
- **Rule:** A detector that evaluates a candidate and may produce findings.
- **Finding:** A safe description of a possible security problem.
- **Diagnostic:** A non-finding event that affects scan completeness, such as an
  unreadable or unsupported file.
- **Failure threshold:** The minimum finding severity that causes the findings
  exit code.
- **Complete scan:** A scan in which every selected candidate was evaluated or
  accounted for by an explicit diagnostic.

## 6. Command-line contract

The initial public command shape is:

```text
repo-scanner scan [path] [options]
```

`path` defaults to the current directory. Exact option spelling is finalized
when the CLI is implemented, but the MVP must support:

- A scan root path.
- A configurable failure threshold.
- A non-interactive mode suitable for CI.
- Standard help and version output.

Planned options that need not ship in the first executable increment include
`--include-ignored`, machine-readable output selection, and explicit resource
limits. Defaults must remain safe when these options are absent.

### 6.1 Exit codes

The process must return stable, documented exit codes:

| Code | Meaning |
| ---: | --- |
| `0` | Scan completed and no finding met the failure threshold. |
| `1` | Scan completed and one or more findings met the failure threshold. |
| `2` | Invalid invocation, invalid configuration, or an operational failure prevented a trustworthy result. |

Warnings below the failure threshold do not change exit code `0`. A materially
incomplete scan must not be reported as clean.

## 7. File selection

### 7.1 Git working trees

When the scan root is inside a Git working tree:

- Scan tracked files that exist in the working tree.
- Scan untracked files that are not ignored.
- Scan a tracked file even if a current ignore rule matches its path.
- Skip untracked ignored files by default.
- Do not inspect `.git` object storage or Git history.
- Report counts of scanned, ignored, unsupported, and failed files.

### 7.2 Ordinary directories

When no Git working tree is available, scan eligible files beneath the selected
root using the scanner's default exclusions. Git must improve selection but must
not be a prerequisite for content scanning.

### 7.3 Safety requirements

- Canonicalize and validate the scan root before traversal.
- Do not follow symbolic links or reparse points outside the scan root.
- Prevent traversal cycles.
- Treat inaccessible paths as diagnostics rather than silently ignoring them.
- Skip known binary content and files above the configured/default size limit,
  recording the reason.
- Use bounded concurrency and support cancellation.
- Do not load an unbounded number of files or unbounded file contents into
  memory simultaneously.
- Define behavior consistently across Windows, macOS, and Linux, accounting for
  platform path and case-sensitivity differences.

The exact default size and concurrency limits will be chosen using functional
tests and early measurements, then documented before release.

## 8. Detection scope

### 8.1 Required MVP rule categories

1. **Known-format secrets:** High-confidence patterns with enough structural
   validation to avoid relying on a broad keyword match alone.
2. **Credential assignments:** Suspicious values assigned to credential-related
   names such as password, token, secret, or API key, with conservative context
   and placeholder filtering.
3. **Sensitive tracked files:** Files whose names or paths commonly contain
   credentials and which are tracked when they should normally be excluded.

The initial rule catalogue will be documented alongside its implementation.
Each rule must have a stable identifier, description, default severity, and
tests covering matches and representative non-matches.

### 8.2 Deferred detection

- Generic entropy-based detection.
- Validation of credentials against external services.
- Organization-specific policy packs.
- Deep parsing of every configuration and programming language.

## 9. Finding model

Each finding must contain enough information to locate, understand, suppress in
a future release, and automate around the problem without retaining the secret.
The model must support at least:

- Stable rule identifier.
- Severity.
- Short title and safe explanation.
- Path relative to the scan root.
- Line and column when known.
- Safe redacted preview or non-reversible fingerprint when required.
- Remediation guidance.

The model must not store or expose the complete detected value after rule
evaluation. Exception messages, debug output, test snapshots, and serialized
results are subject to the same restriction.

## 10. Severity

The MVP uses four ordered finding severities:

1. `Low`
2. `Medium`
3. `High`
4. `Critical`

The default failure threshold is `High`. Severity represents likely security
impact combined with detection confidence; it is not a guarantee that a value
is valid. Rule severities must be documented and tested.

## 11. Output

### 11.1 Terminal output

Terminal output must:

- Present findings in a stable, readable order.
- Show rule, severity, relative location, and remediation guidance.
- Never display a complete detected secret.
- End with counts for findings, scanned files, skipped files, diagnostics, and
  elapsed time.
- State clearly when a scan is incomplete.
- Avoid ANSI formatting when output is redirected or non-interactive output is
  requested.

### 11.2 Future formats

JSON and SARIF formatters will consume the same safe finding model. Adding a
formatter must not require detection or traversal logic to depend on a specific
presentation format.

## 12. Error handling and diagnostics

- Invalid paths and options are operational failures.
- Individual inaccessible or changed-during-scan files produce diagnostics.
- The scan result records whether diagnostics made the result incomplete.
- User-facing errors are concise and must not contain sensitive file contents.
- Optional detailed diagnostics, when introduced, must follow the same redaction
  guarantees.
- Cancellation stops new work promptly and returns a non-success result without
  presenting the scan as clean.

## 13. Configuration and suppression

The first increment may use CLI options only. Configuration files, baselines,
and inline suppression are deferred until rule behavior is stable.

When suppression is introduced, it must:

- Be explicit and reviewable.
- Prefer stable rule and location identities over matching raw secret values.
- Avoid storing the complete secret.
- Distinguish existing accepted findings from newly introduced findings.
- Make suppressed-finding counts visible.

## 14. Dependencies

Production dependencies are allowed when they are the most reliable and
maintainable solution to a material problem. Before adoption, document:

- The problem the dependency solves and alternatives considered.
- Maintenance activity and API maturity.
- License compatibility with public distribution.
- Direct and transitive dependency footprint.
- Security implications of processing untrusted input.
- Cross-platform, trimming, and distribution impact where relevant.

Versions must be pinned by normal .NET dependency resolution and auditable.
Dependency choice must not silently introduce network access during scanning.

## 15. Quality requirements

- Nullable reference types remain enabled.
- Production builds complete without warnings introduced by project code.
- Unit tests cover every rule, redaction behavior, severity behavior, and core
  domain invariant.
- Integration tests cover traversal, Git-aware selection, inaccessible files,
  cancellation, limits, and representative end-to-end scans.
- All fixtures use synthetic values that cannot authenticate to a real service.
- Tests are deterministic and do not require network access.
- Formatting, build, and tests pass before a feature is considered complete.

## 16. Performance requirements

The MVP must use bounded resource consumption, but numerical targets will not be
invented before representative workloads exist. Once traversal and detection
are functional:

- Add a dedicated benchmark project.
- Define small, medium, and large synthetic repository workloads.
- Measure elapsed time, throughput, allocations, and relevant memory behavior.
- Record the machine/runtime and workload with published measurements.
- Establish a baseline before defining regression thresholds.

## 17. Security and privacy requirements

- Scanning is offline and local by default.
- Repository content is untrusted input.
- No telemetry is included in the MVP.
- Complete findings and source contents are never transmitted.
- Secret material is redacted before it crosses the rule evaluation boundary.
- Regular expressions and parsers must have bounded or predictable behavior on
  hostile input.
- A clean result means no configured rule found a reportable issue within the
  explicitly reported scope; it is not a security guarantee.

## 18. MVP acceptance criteria

The MVP is complete when:

1. A user can build and run the CLI on Windows, macOS, and Linux.
2. A user can scan a Git working tree or ordinary directory.
3. Tracked and untracked non-ignored files are selected according to this spec.
4. The initial documented rule catalogue detects its synthetic positive cases
   without matching its representative negative cases.
5. No finding or failure path exposes a complete detected value.
6. Terminal output identifies actionable findings and scan completeness.
7. Exit codes follow the documented contract.
8. Traversal is bounded, cancellable, and safe around links and inaccessible
   files.
9. Unit and integration tests pass without network access or real credentials.
10. Formatting and build checks pass without project-code warnings.
11. README usage and limitations match the executable behavior.
12. Initial benchmark results and workload definitions are recorded after the
    functional pipeline is stable.

## 19. Post-MVP candidates

- Git-history and branch scanning.
- JSON and SARIF output.
- Configuration files, baselines, and suppressions.
- Ignored-file audit mode.
- Entropy and richer language-aware detection.
- Parallel repository scanning.
- Package distribution, CI integrations, and a GUI.
- Optional credential validation designed around explicit consent and strict
  data-handling guarantees.
