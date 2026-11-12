# Proposal

## Why

`openspec validate --all --strict --no-interactive` fails for seven main specs
because 14 requirement descriptions exceed the CLI's 500-character limit. The
project's validation command is meant to pass for every change and spec, so a
failing baseline hides real regressions. This change restores a green strict
validation as part of WP-00 ("Correct product and competitor documentation"
in `docs/IMPLEMENTATION_ROADMAP.md`), which keeps the product's documented
contract accurate and verifiable.

## What Changes

- Each of the 14 over-long requirements keeps its header and every existing
  scenario unchanged. Its description is cut down to one behavior without
  changing that behavior's meaning.
- Every behavior removed from such a description becomes its own requirement,
  with its normative text carried over and at least one scenario that
  describes behavior the application, packaging, or documentation already
  exhibits.
- No behavior is added, removed, or changed. No source code, test, or user
  documentation changes.

## Non-goals

- Change any product behavior, test, packaging step, or user-facing document.
- Edit `openspec/specs/` directly instead of through an archived change.
- Reword requirements beyond the minimum needed for a moved sentence to stand
  on its own.
- Resolve differences between existing requirement text and current behavior,
  or modify the in-flight changes `attribute-firmlinked-storage` and
  `harden-scan-traversal`.
- Shorten requirements that already pass strict validation.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `in-app-updates`: split "Installation follows the standard user-approved
  update flow".
- `release-packaging`: split "The universal App Store package runs on both
  architectures".
- `result-export`: split "Export scope follows the active filter", "Every
  exported row identifies and describes one scanned item", "Exported byte
  counts state the scan's measurement basis", "An export carries versioned
  scan metadata", and "A comma-separated-values export is readable by
  spreadsheet applications".
- `result-filtering`: split "Date criteria can be expressed relative to
  evaluation time" and "Filter presets can be applied and persisted".
- `scan-access-guidance`: split "Full Disk Access guidance directs the user to
  macOS settings".
- `scan-history`: split "A completed scan is recorded as one snapshot", "A
  snapshot describes every scanned item", and "A snapshot is never truncated
  to fit a limit".
- `storage-measurement`: split "Storage terms have canonical meanings".

## Impact

- Affected artifacts: the seven main specs above, after archive.
- Code, tests, packaging, and documentation: none.
- Dependencies: none. The change is independent of the in-flight changes
  `attribute-firmlinked-storage`, which adds requirements to
  `storage-measurement` under different names, and `harden-scan-traversal`,
  which touches no capability modified here.
- Risks: a split could subtly change a requirement's meaning, or a new
  scenario could describe behavior that does not exist. Every moved sentence
  keeps its normative wording, and every new scenario is grounded in an
  existing scenario, test, packaging script, or document.
- Estimate: half a day, within WP-00's under-one-day roadmap estimate.
