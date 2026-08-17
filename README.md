# Repo Scanner

Repo Scanner is a security-focused command-line tool for finding content that
should not have been committed to a Git repository, including API keys,
credentials, passwords, sensitive files, and files that should normally be
excluded by `.gitignore`.

The project is currently at the initial scaffolding stage. Its first interface
will be a .NET command-line application. The scanning engine will be kept
independent of presentation so it can support a GUI or other front ends later.

## Goals

- Scan repositories locally without uploading their contents.
- Detect known secret formats and suspicious credential-like values.
- Identify sensitive filenames and likely ignore-rule mistakes.
- Redact secret material in every output format.
- Provide useful findings with controllable false positives.
- Support automation through stable exit codes and machine-readable output.
- Remain safe and responsive when scanning untrusted repositories.

## Non-goals for the initial version

- Automatically modifying or deleting repository content.
- Automatically rotating or revoking exposed credentials.
- Uploading source files or findings to a remote service.
- Providing a graphical interface.

These may be reconsidered after the core scanner is reliable and its interfaces
are stable.

## Requirements

- [.NET SDK 10](https://dotnet.microsoft.com/)
- Git is optional for ordinary-directory scans and required for Git-aware
  tracked, untracked, and ignore-rule selection.

## Build and run

From the repository root:

```sh
dotnet build repo-scanner.slnx
dotnet run --project src/RepoScanner.Cli
```

The Phase 1 vertical slice supports this initial scan command:

```sh
dotnet run --project src/RepoScanner.Cli -- scan [path] --fail-on high
```

`path` may be a file or directory and defaults to the current directory. The
failure threshold accepts `low`, `medium`, `high`, or `critical`, and defaults to
`high`.

Current exit codes are:

| Code | Meaning |
| ---: | --- |
| `0` | The scan completed without findings at the configured threshold. |
| `1` | The scan completed with findings at or above the threshold. |
| `2` | The invocation was invalid, cancelled, or could not produce a trustworthy result. |

Directory scans recurse through ordinary directories. Traversal does not follow
symbolic links or reparse points, uses bounded concurrent file reads, and reports
files that were scanned, skipped, or failed. Files larger than 1 MiB, binary
files, and invalid or unsupported text encodings are skipped with diagnostics.
UTF-8 is supported with or without a BOM; UTF-16 and UTF-32 require a BOM.

Inside a Git working tree, the scanner asks Git itself to select tracked files
plus untracked, non-ignored files. Tracked files remain selected even if a later
ignore rule matches them. Ignored untracked files, `.git` object storage, commit
history, and submodule contents are not scanned. Selecting an individual file
explicitly scans that file even when it is ignored.

If Git is not installed, the scanner falls back to ordinary-directory traversal
and reports that fallback. If Git detects a working tree but candidate selection
fails, the scan is incomplete and returns exit code `2` rather than silently
scanning a different scope.

## Test and format

```sh
dotnet format repo-scanner.slnx --verify-no-changes
dotnet test repo-scanner.slnx
```

Unit and integration test projects use xUnit. Package versions are managed
centrally in `Directory.Packages.props`, and shared build settings are defined in
`Directory.Build.props`.

## Project structure

```text
src/
  RepoScanner.Core/              Reusable scanning and detection logic
  RepoScanner.Cli/               Command-line entry point and presentation
tests/
  RepoScanner.Core.Tests/        Fast unit tests
  RepoScanner.IntegrationTests/  Filesystem, Git, and end-to-end tests
docs/                            Specification and implementation plan
```

## Security principles

- Repository data is untrusted input.
- A discovered secret must never be reproduced in full in logs or reports.
- Scans are local and offline by default.
- Skipped and unreadable files are reported rather than treated as clean.
- Test fixtures use synthetic credentials only.

Please do not report a real secret by placing it in an issue, test, screenshot,
or example. Revoke the credential first and provide a redacted reproduction.

## Project documentation

- [`AGENTS.md`](AGENTS.md) defines engineering and collaboration expectations.
- [`DEVELOPMENT.md`](DEVELOPMENT.md) records decisions, blockers, performance
  concerns, investigations, and lessons learned during development.
- [`docs/MVP_SPECIFICATION.md`](docs/MVP_SPECIFICATION.md) defines the agreed
  behavior, boundaries, safety requirements, and acceptance criteria for the
  first release.
- [`docs/IMPLEMENTATION_PLAN.md`](docs/IMPLEMENTATION_PLAN.md) divides the MVP
  into testable implementation phases and dependency decision checkpoints.

## Status

Phase 3 is complete. The scanner now has Git-aware candidate selection on top of
recursive, cancellable, bounded filesystem traversal. The detection pipeline
still contains one synthetic rule, `RS1000`, which recognizes fixtures composed of the
`REPO_SCANNER_TEST_` prefix followed by `SECRET=` and a value. This allows
redaction, severity, output, and exit behavior to be tested safely without this
documentation becoming a finding. It is not a real credential detector.
Production detection rules are planned for Phase 4.
