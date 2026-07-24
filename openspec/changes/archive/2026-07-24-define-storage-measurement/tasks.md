## 1. Core Measurement Contract

- [x] 1.1 Add the `StorageMeasurementMode` Core value and propagate the mode
  captured from `ScanOptions` through every `ScanProgress`, preserving compatible
  construction for existing callers.
- [x] 1.2 Add `DiskScannerTests` for logical and allocated mode propagation,
  basis-consistent file/directory/progress aggregation, and collapsed-package
  and excluded-symbolic-link totals.
- [x] 1.3 Change allocated metadata lookup failures on supported macOS targets
  to enter the scanner's recoverable-error path instead of returning logical
  length.
- [x] 1.4 Add scanner tests proving failed allocated reads are reported and
  excluded from totals while successful siblings retain allocated values.
- [x] 1.5 Add cancellation coverage proving the latest published partial tree,
  byte total, incomplete state, and measurement mode remain consistent.

## 2. Application Result Context

- [x] 2.1 Retain the measurement mode returned by scan progress separately from
  the preference used for the next scan.
- [x] 2.2 Display a concise `Allocated size` or `Logical size` basis next to scan
  totals/results and align the scan-option tooltip with the per-path allocated
  limitation.
- [x] 2.3 Add `MainWindowViewModelTests` proving allocated remains the
  application default, logical scans are labeled correctly, and changing the
  next-scan preference does not relabel an existing result.

## 3. Measurement Reference and macOS Verification

- [x] 3.1 Add `docs/STORAGE_MEASUREMENT.md` with canonical definitions for
  logical, allocated, unique allocated, volume used/free/available/purgeable,
  aggregation scope, errors, cancellation, hardlink/clone limitations, and
  metadata-only cloud-placeholder handling.
- [x] 3.2 Document reproducible normal-file and sparse-file fixture commands and
  comparisons with macOS metadata tools, explaining why Finder and aggregate
  tools are comparison points rather than unique-storage authorities.
- [x] 3.3 Run the documented fixtures on available macOS hardware, record the
  expected architecture-independent observations, and add focused integration
  coverage that skips cleanly when required filesystem behavior is unavailable.
- [x] 3.4 Link the canonical reference from the README and reconcile
  measurement wording in Core XML comments and App help text without claiming
  hardlink or APFS-clone deduplication.

## 4. Validation and Roadmap Tracking

- [x] 4.1 Run `dotnet build` and fix any compile or analyzer regressions.
- [x] 4.2 Run `dotnet test` and confirm all measurement, cancellation, App, and
  existing regression tests pass.
- [x] 4.3 Run
  `openspec validate --all --strict --no-interactive` and resolve every strict
  validation error.
- [x] 4.4 Update the WP-02 status/notes in
  `docs/IMPLEMENTATION_ROADMAP.md` to record completion of the
  measurement-definition slice and name the remaining WP-02 changes.
