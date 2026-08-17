# Development notes

This is the project's living engineering journal. It records context that would
otherwise be lost between changes: decisions and their rationale, blockers,
investigations, performance concerns, resolved issues, and follow-up work.

Keep entries concise but specific enough for a future contributor to understand
what happened without reconstructing it from commit history. Do not place real
credentials, secret values, or sensitive repository content in this document.

## Current considerations

### Core and presentation separation

- **Status:** Accepted
- **Context:** The project starts as a CLI but may gain a GUI later.
- **Decision:** Keep scanning and detection behavior independent of the command
  line so multiple front ends can reuse it.
- **Reasoning:** This avoids coupling security logic to console input and output
  and makes the core easier to test.
- **Consequences:** A multi-project solution will likely be introduced when the
  first scanner behavior is implemented. Avoid adding layers with no immediate
  responsibility.

### Local and offline scanning by default

- **Status:** Accepted
- **Context:** Repositories may contain confidential source and live secrets.
- **Decision:** Do not transmit repository contents or findings during normal
  scanning.
- **Reasoning:** Local processing minimizes privacy risk and makes behavior
  predictable in restricted development and CI environments.
- **Consequences:** Any future remote integration must be explicit, optional,
  documented, and designed so users understand exactly what leaves the machine.

### Safe handling of findings

- **Status:** Accepted
- **Context:** A security tool can accidentally leak the secrets it discovers
  through terminal output, logs, exceptions, snapshots, or machine-readable
  reports.
- **Decision:** Store and display only the minimum material needed to identify a
  finding, using redaction or a non-reversible fingerprint where appropriate.
- **Reasoning:** Scanner output should not become a second source of credential
  exposure.
- **Consequences:** Redaction must be a domain-level invariant rather than only
  a formatting concern, and tests must use synthetic credentials.

### Initial scan scope

- **Status:** Accepted
- **Context:** Working-tree and Git-history scanning have different correctness,
  performance, and reporting requirements.
- **Decision:** The MVP scans tracked files and untracked, non-ignored files in
  the working tree. It also supports ordinary directories. Git history and
  untracked ignored files are excluded by default.
- **Alternatives:** Scan only tracked files, include all ignored files, or scan
  working trees and history from the first release.
- **Reasoning:** This catches material about to be committed while keeping scope,
  runtime, and false positives manageable. History scanning can be added later
  as an explicit workflow.
- **Consequences:** Tracked files remain candidates even when current ignore
  rules match them. Skipped and failed file counts must remain visible.

### CI failure contract

- **Status:** Accepted
- **Context:** Automation must distinguish detected policy violations from an
  invalid or incomplete scan.
- **Decision:** Use a configurable severity threshold, defaulting to `High`, and
  stable exit codes: `0` for no threshold-level findings, `1` for findings, and
  `2` for invalid invocation or operational failure.
- **Alternatives:** Fail on every finding or use a single nonzero exit code.
- **Reasoning:** A threshold supports different risk tolerances, while distinct
  failure codes prevent a scanner error from being confused with a finding.
- **Consequences:** Severity and scan completeness are part of the public
  behavior and require contract tests.

### Dependency policy

- **Status:** Accepted
- **Context:** Correct Git, command-line, report, and benchmark behavior may be
  costly or risky to recreate, but every dependency increases maintenance and
  supply-chain exposure.
- **Decision:** Allow a production dependency when evaluation shows it is the
  most reliable and maintainable solution to a material problem. Document the
  decision before adoption.
- **Alternatives:** Avoid all dependencies or add libraries whenever convenient.
- **Reasoning:** Deliberate evaluation preserves access to mature implementations
  without growing the dependency graph unnecessarily.
- **Consequences:** Evaluate maintenance, license, transitive footprint,
  untrusted-input risk, cross-platform behavior, and distribution impact at the
  relevant implementation phase.

### Test framework

- **Status:** Accepted
- **Context:** Phase 0 requires cross-platform unit and integration test projects
  that work with standard .NET tooling and CI providers.
- **Decision:** Use xUnit with `Microsoft.NET.Test.Sdk` and the Visual Studio test
  adapter. Manage versions centrally in `Directory.Packages.props`. Do not add a
  coverage collector until coverage reporting is configured.
- **Alternatives:** MSTest, NUnit, or a custom test harness.
- **Reasoning:** xUnit is mature, cross-platform, widely understood in the .NET
  ecosystem, and supports concise isolated tests. The standard test SDK and
  adapter integrate with `dotnet test` and common development tools. Deferring
  coverage avoids carrying an unused dependency.
- **Consequences:** Test projects depend on xUnit, the test SDK, and the adapter.
  Framework-specific features should remain in test code and must not shape
  production APIs.

### Solution and shared build configuration

- **Status:** Accepted
- **Context:** The project needs a modern multi-project structure with consistent
  compiler, analyzer, formatting, and dependency settings.
- **Decision:** Use the .NET 10 `.slnx` solution format, central package version
  management, package lock files, repository-wide `Directory.Build.props`, and a
  root `.editorconfig`. Treat build and analyzer warnings as errors.
- **Alternatives:** Use the legacy `.sln` format and duplicate settings and
  package versions in individual project files.
- **Reasoning:** `.slnx` is concise and supported by the project's required .NET
  10 SDK. Central settings reduce drift, lock files make restores reproducible,
  and warnings-as-errors prevents new quality issues from accumulating.
- **Consequences:** Contributors need tooling that supports `.slnx` and the .NET
  10 SDK. Dependency updates intentionally change committed lock files.

### Initial project boundaries

- **Status:** Accepted
- **Context:** The scanner begins as a CLI but its core must support future front
  ends.
- **Decision:** Create separate Core and CLI projects, with the CLI referencing
  Core and no reverse reference. Keep unit and integration tests separate; both
  currently reference only Core.
- **Alternatives:** Continue with one executable project or introduce additional
  application/infrastructure layers immediately.
- **Reasoning:** This enforces the required presentation boundary without adding
  layers that have no current responsibility.
- **Consequences:** Additional projects or abstractions will be added only when a
  concrete implementation phase requires them.

## Open questions

- Which result formats are required initially: terminal, JSON, and/or SARIF?
- What suppression and baseline format will balance usability with visibility
  of new findings?
- Which test, CLI parsing, Git integration, and benchmark dependencies best meet
  the documented dependency policy?

The test-framework portion of the final question is resolved. CLI parsing, Git
integration, and benchmark dependencies remain open until their implementation
checkpoints.

## Blockers

No blockers are currently known.

## Performance concerns

- Repository traversal must use bounded concurrency rather than creating one
  task per file.
- Large and binary files should be identified without loading their entire
  contents into memory.
- Detection rules, especially regular expressions, need predictable runtime on
  adversarial input.
- File-count, file-size, and cancellation limits should be observable so a
  partial scan cannot be mistaken for a complete clean scan.

No performance measurements or budgets have been established yet. Add benchmarks
only after representative workloads and a concrete performance question exist.
At that point, introduce a dedicated benchmark project, record the test machine
and workload characteristics with results, and establish a measured baseline
before defining regression thresholds.

## Issues and investigations

### 2026-08-17 — Build-time IDE0005 enforcement required XML documentation

- **Status:** Resolved
- **Observed:** The first Phase 0 build failed with Roslyn's
  `EnableGenerateDocumentationFile` diagnostic after `IDE0005` was explicitly
  elevated to a warning in `.editorconfig`.
- **Impact:** All projects containing C# source failed under
  warnings-as-errors, despite having no unnecessary using directives.
- **Cause:** Roslyn requires `GenerateDocumentationFile=true` to enforce
  `IDE0005` during compilation. Enabling documentation generation now would also
  require suppressing or resolving documentation diagnostics before public APIs
  exist.
- **Resolution:** Removed the explicit build-time severity for `IDE0005`.
  Unnecessary using directives remain checked by
  `dotnet format --verify-no-changes`.
- **Verification:** Re-run formatting, build, and tests for the complete solution.
- **Follow-up:** Reconsider XML documentation generation when the Core public API
  is introduced and its documentation policy can be defined deliberately.

When adding an entry, use this structure:

```md
### YYYY-MM-DD — Short title

- **Status:** Investigating | Blocked | Resolved | Deferred
- **Observed:** What happened, including a minimal safe reproduction.
- **Impact:** Correctness, security, performance, or developer impact.
- **Cause:** Confirmed cause, or current hypothesis if still investigating.
- **Resolution:** What changed and why.
- **Verification:** Tests, measurements, or checks used to confirm the result.
- **Follow-up:** Remaining work, owner, or conditions for revisiting it.
```

## Decision template

Use this structure for decisions that affect architecture, security, public
behavior, dependencies, or future development:

```md
### Decision title

- **Status:** Proposed | Accepted | Superseded
- **Context:** The problem or constraint that requires a decision.
- **Decision:** What was chosen.
- **Alternatives:** Other viable options considered.
- **Reasoning:** Why this option best fits the current evidence.
- **Consequences:** Benefits, costs, risks, and follow-up work.
```
