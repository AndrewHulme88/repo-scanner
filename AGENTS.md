# Repository guidance

## Project purpose

This repository contains a security-focused .NET repository scanner. It begins
as a command-line tool, but its scanning engine must remain reusable by future
front ends such as a desktop GUI or service.

## Working agreements

- Inspect the relevant code and documentation before making changes.
- Keep changes focused, reviewable, and no larger than the requested outcome.
- Preserve user changes and do not perform destructive Git operations.
- Explain meaningful security, compatibility, and architecture tradeoffs.
- Record durable decisions, blockers, resolved issues, and performance concerns
  in `DEVELOPMENT.md` as part of the change that introduces them.
- Update `README.md` when setup, usage, configuration, or supported behavior
  changes.
- Ask before adding a production dependency unless the task explicitly requires
  it. Document why each production dependency is needed.

## Architecture

- Keep repository traversal, detection, configuration, and result models
  independent of console presentation.
- Core scanning code must not depend on a CLI or future GUI project.
- Prefer small, composable types and explicit dependencies over global state.
- Avoid speculative abstractions. Add extension points when there is a concrete
  use case.
- Use asynchronous APIs for genuinely asynchronous I/O, with cancellation
  support for operations that may scan many files.

## Security and privacy

- Treat repository paths, names, and contents as untrusted input.
- Never log, print, snapshot, or include a complete detected secret in an
  exception. Findings must expose only a safe redacted preview or fingerprint.
- Use synthetic, unmistakably fake credentials in tests and documentation.
- Scanning is local and offline by default. Do not transmit repository content
  or findings unless a feature explicitly requires it and the user approves it.
- Keep traversal bounded. Account for symbolic links, path escape, binary files,
  large files, unusual encodings, cancellation, and inaccessible files.
- Design regular expressions and parsers to resist excessive backtracking and
  resource exhaustion.
- Do not silently treat an unreadable or skipped file as successfully scanned.

## C# conventions

- Keep nullable reference types enabled and do not suppress warnings without a
  documented reason.
- Follow standard .NET naming and formatting conventions.
- Prefer immutable models where practical.
- Use clear domain terminology; avoid generic names such as `Helper` or `Utils`.
- Add XML documentation when a public API's contract is not self-evident.

## Tests

- Add or update tests for every behavior change.
- Cover successful detection, non-detection, boundary cases, and false-positive
  behavior.
- Add regression tests when fixing a defect.
- Tests must not contain valid credentials or depend on network access.
- Prefer deterministic tests and temporary directories owned by the test.

## Performance testing

- Introduce performance benchmarks once representative scanning functionality
  and workloads exist; benchmark scaffolding is not required during initial
  project setup.
- Benchmark behavior that materially affects scan time or resource use, such as
  repository traversal, file reading, rule evaluation, and large-file handling.
- Use synthetic repositories and data with documented file counts, file sizes,
  and rule sets so results are reproducible and contain no sensitive content.
- Measure throughput, elapsed time, allocations, and peak memory where relevant.
- Establish a measured baseline before setting performance budgets or regression
  thresholds; do not choose arbitrary targets without evidence.
- Keep benchmarks separate from normal unit tests and document how to run and
  interpret them in `README.md` when they are introduced.
- Record significant results, regressions, tradeoffs, and optimizations in
  `DEVELOPMENT.md`.

## Verification

Before finishing a code change, run the checks that exist for the repository:

```sh
dotnet format --verify-no-changes
dotnet build
dotnet test
```

If a check cannot run or no tests exist yet, report that clearly. Review the
final diff for secret exposure, security regressions, unrelated changes, and
missing documentation.

## Definition of done

A change is complete when its requested behavior is implemented, relevant tests
pass, formatting and build checks pass, documentation is current, and known
limitations or follow-up work are recorded in `DEVELOPMENT.md`.
