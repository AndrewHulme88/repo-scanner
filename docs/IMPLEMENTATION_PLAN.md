# MVP implementation plan

## Progress

- **Phase 0 — Project foundation:** Complete.
- **Phase 1 — Safe domain model and first vertical slice:** Complete.
- **Phase 2 — Bounded filesystem traversal:** Complete.
- **Phase 3 — Git-aware candidate selection:** Not started.

## 1. Planning principles

- Build the smallest end-to-end vertical slice before expanding the rule set.
- Keep core scanning behavior independent of CLI and output presentation.
- Make safe redaction a domain invariant from the first finding model.
- Add dependencies only after evaluating and documenting why they are the best
  available solution.
- Introduce performance benchmarks after representative traversal and detection
  behavior exists.
- Complete each phase with tests, documentation, and an updated development log.

The phases below are ordered, but a phase may be split into smaller changes. Do
not begin a later phase when an unresolved earlier decision would invalidate it.

## 2. Proposed solution shape

The initial multi-project structure is expected to be:

```text
repo-scanner.slnx
src/
  RepoScanner.Core/
  RepoScanner.Cli/
tests/
  RepoScanner.Core.Tests/
  RepoScanner.IntegrationTests/
benchmarks/                         # Added when Phase 8 begins
  RepoScanner.Benchmarks/
docs/
  MVP_SPECIFICATION.md
  IMPLEMENTATION_PLAN.md
```

Responsibilities:

- **RepoScanner.Core:** Scan requests/results, traversal, candidate selection,
  detection rules, safe findings, diagnostics, and orchestration.
- **RepoScanner.Cli:** Argument parsing, process exit codes, cancellation wiring,
  and terminal presentation.
- **Core tests:** Fast domain, rule, redaction, and orchestration unit tests.
- **Integration tests:** Filesystem, Git-aware selection, limits, cancellation,
  and end-to-end behavior using isolated temporary repositories.
- **Benchmarks:** Reproducible synthetic workloads, separate from correctness
  tests.

This is a target shape, not permission to add abstractions without a current
responsibility.

## 3. Phase 0 — Project foundation

### Outcomes

- Create the solution and project structure.
- Add a repository `.editorconfig` with enforceable C# conventions.
- Select a test framework and document the dependency decision.
- Enable consistent build settings, nullable analysis, and warnings appropriate
  for an early public project.
- Establish a basic CI workflow after the Git repository and hosting location
  are ready.

### Verification

- A clean checkout can restore, format, build, and test.
- Project references enforce that Core does not depend on CLI.
- The existing hello-world behavior is either preserved in the new CLI project
  or replaced with documented help output.

### Decisions required

- Test framework.
- Solution format supported by the intended developer tooling.
- Initial CI provider once the repository is hosted.

## 4. Phase 1 — Safe domain model and first vertical slice

### Outcomes

- Define scan request, scan result, finding, severity, rule identity, location,
  and diagnostic models.
- Define redacted evidence/fingerprint behavior that cannot expose a complete
  detected value.
- Define minimal rule and candidate-source boundaries.
- Implement one synthetic high-confidence rule end to end.
- Run it from the CLI against an explicitly selected file or small directory.
- Implement exit codes `0`, `1`, and `2`.

### Verification

- Unit tests prove redaction invariants and severity threshold behavior.
- End-to-end tests prove a synthetic secret is found but never reproduced.
- Clean, findings, invalid-path, and cancellation paths return the correct code.

### Exit criterion

The tool can safely scan a controlled directory through the complete pipeline,
even though traversal and the rule catalogue are still minimal.

## 5. Phase 2 — Bounded filesystem traversal

### Outcomes

- Canonicalize and validate the scan root.
- Enumerate files without materializing an entire large tree at once.
- Add bounded concurrency and cancellation.
- Detect/skip binary and over-limit files with visible diagnostics.
- Handle inaccessible, disappearing, and changing files.
- Define safe symbolic-link and reparse-point behavior on supported platforms.
- Produce scan accounting for selected, scanned, skipped, and failed files.

### Verification

- Integration tests cover deep trees, empty files, large files, binary files,
  inaccessible paths where the platform permits, links, cancellation, and files
  removed during scanning.
- A partial scan is never presented as a complete clean scan.
- Cross-platform differences are documented rather than hidden in brittle tests.

### Dependency checkpoint

Evaluate whether filesystem behavior can be implemented reliably with the .NET
base class library. Add no traversal dependency without a demonstrated gap.

## 6. Phase 3 — Git-aware candidate selection

### Outcomes

- Detect whether the scan root belongs to a Git working tree.
- Select tracked files plus untracked, non-ignored files.
- Continue to select tracked files that match current ignore rules.
- Skip `.git` internals and untracked ignored files by default.
- Fall back cleanly to ordinary-directory behavior when Git is unavailable.
- Define diagnostics for an invalid or unusual working-tree state.

### Verification

- Integration tests create isolated temporary repositories covering tracked,
  untracked, ignored, negated-ignore, nested-ignore, and tracked-but-ignored
  cases.
- Filenames containing spaces, Unicode, and platform-valid special characters
  are covered.
- Git-history objects are not scanned.

### Dependency checkpoint

Compare invoking Git safely, using a maintained Git library, and implementing
only the required semantics. Document correctness, maintenance, process-safety,
licensing, performance, and distribution tradeoffs before choosing.

## 7. Phase 4 — Initial detection catalogue

### Outcomes

- Define the initial list of known-format secret rules.
- Add conservative credential-assignment detection with placeholder filtering.
- Detect sensitive files that are tracked when they would normally be excluded.
- Give every rule a stable identifier, severity, explanation, and remediation.
- Use bounded regex execution or non-regex parsing where appropriate.
- Document supported rules and known limitations.

### Verification

- Every rule has synthetic positive, negative, boundary, and redaction tests.
- Common examples, placeholders, documentation snippets, and generated values
  are included in negative fixtures where appropriate.
- Adversarial inputs are tested for excessive rule-evaluation time.
- Rule ordering does not make output nondeterministic.

### Exit criterion

The scanner provides useful working-tree findings with an explainable false-
positive profile and no known secret-disclosure path.

## 8. Phase 5 — CLI and terminal experience

### Outcomes

- Finalize `scan [path]` argument behavior, help, and version output.
- Implement configurable failure threshold with `High` as the default.
- Render deterministic terminal findings and a scan summary.
- Detect redirected output and support plain, non-interactive behavior.
- Handle Ctrl+C through cancellation and a trustworthy exit result.
- Document usage, exit codes, examples, and limitations in the README.

### Verification

- CLI parsing and exit-code behavior have automated tests.
- Output snapshots contain synthetic data and remain redacted.
- Redirected output contains no required ANSI control sequences.
- Error messages are actionable and contain no scanned content.

### Dependency checkpoint

Evaluate mature command-line parsing packages against a minimal implementation.
Prefer a maintained package if it materially improves help, validation,
completion, or long-term public API consistency without unacceptable cost.

## 9. Phase 6 — Hardening and public-readiness pass

### Outcomes

- Threat-model the implemented scan pipeline.
- Review exception, logging, debugging, and serialization paths for leakage.
- Fuzz or property-test high-risk parsing and path-handling boundaries where it
  provides clear value.
- Verify behavior on Windows, macOS, and Linux.
- Add packaging metadata, license, contribution guidance, and `SECURITY.md`
  before inviting public use.
- Enable dependency auditing and a repeatable release build.

### Verification

- CI runs formatting, build, unit tests, and integration tests on supported
  operating systems.
- A security-focused review has no unresolved release-blocking findings.
- Documentation clearly states what a clean scan does and does not guarantee.

## 10. Phase 7 — Machine-readable output

### Outcomes

- Add versioned JSON output based only on the safe result model.
- Add SARIF if it is valuable for the first public integrations.
- Preserve exit-code behavior independently of output format.
- Define compatibility expectations for serialized fields.

### Verification

- Schema/contract tests prevent accidental breaking changes.
- Serialized outputs never contain complete detected values.
- Large finding sets can be written without avoidable memory growth.

## 11. Phase 8 — Performance baseline

### Entry condition

Traversal, Git-aware selection, and a representative initial rule catalogue are
stable enough to measure realistic work.

### Outcomes

- Add `RepoScanner.Benchmarks` using the selected benchmark tooling.
- Define small, medium, and large synthetic repositories with documented file
  counts, sizes, content mixes, and rule-hit rates.
- Benchmark traversal, candidate selection, content scanning, rule evaluation,
  and representative end-to-end runs.
- Measure elapsed time, throughput, allocations, and memory where reliable.
- Record the environment and baseline results in `DEVELOPMENT.md`.
- Set regression thresholds only after repeated measurements show normal
  variance.

### Verification

- Benchmarks are reproducible and contain no real credentials.
- Benchmark execution is separate from normal unit-test execution.
- Any performance optimization preserves correctness and includes regression
  tests where behavior previously failed.

## 12. Deferred roadmap

After the MVP acceptance criteria are met, prioritize based on real usage:

1. Configuration files, suppressions, and baselines.
2. Ignored-file audit mode.
3. Git-history scanning as a separate, explicit workflow.
4. Expanded rules and measured entropy detection.
5. CI templates and distribution channels.
6. GUI built against the same Core APIs.

Git-history scanning must receive its own specification because it changes the
scope, performance model, duplicate handling, and remediation guidance.

## 13. Per-phase completion checklist

- Requested outcomes and relevant specification requirements are satisfied.
- Unit and integration tests cover new behavior and failure modes.
- Secret redaction and incomplete-scan behavior have been reviewed.
- `dotnet format --verify-no-changes`, `dotnet build`, and `dotnet test` pass.
- README behavior and examples are current.
- Decisions, dependencies, issues, and measurements are recorded in
  `DEVELOPMENT.md`.
- The final diff contains no unrelated changes or sensitive fixtures.
