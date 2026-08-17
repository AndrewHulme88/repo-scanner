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

## Open questions

- Should the first release scan only the working tree, or also committed Git
  history?
- Which result formats are required initially: terminal, JSON, and/or SARIF?
- How should severity thresholds map to process exit codes?
- Which ignore semantics should be supported first: `.gitignore` only, or also
  scanner-specific exclusions?
- What suppression and baseline format will balance usability with visibility
  of new findings?

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

No issues have been recorded yet.

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
