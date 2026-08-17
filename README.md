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

## Build and run

From the repository root:

```sh
dotnet build
dotnet run --project repo-scanner.csproj
```

The current program is still the default console application; scanning commands
will be documented here as they are implemented.

## Test and format

```sh
dotnet format --verify-no-changes
dotnet test
```

There is not yet a test project. The test command is included as the expected
workflow once tests are introduced.

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

Early development. No scanner functionality has been implemented yet.
