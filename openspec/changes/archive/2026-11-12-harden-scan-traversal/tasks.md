# Tasks

## 1. Core Name Handling

- [x] 1.1 Run `openspec validate harden-scan-traversal --type change --strict --no-interactive` before implementation and resolve any artifact errors without changing approved scope; verified by a clean validation run.
- [x] 1.2 Relax `DiskItem` name validation to reject only `null` and empty names; verified by unit tests accepting a single space, a no-break space, and a zero-width space and rejecting `null` and empty.
- [x] 1.3 Derive scanned item names with a fallback to the full path when the derived file name is empty; verified by a scanner unit test with an injected enumerator returning a path that ends in a separator.
- [x] 1.4 Verify whitespace-only directory and file names are measured, aggregated, and browsable; verified by temporary-directory scanner tests for a single-space directory and a no-break-space file in every measurement mode.

## 2. Core Failure Containment

- [x] 2.1 Move per-entry item construction and metadata reads into a helper that records any non-cancellation exception as a scan error and lets traversal continue; verified by a test injecting an `ArgumentException` for one of three files and asserting the other two are counted and the error lists the path and exception type.
- [x] 2.2 Verify cancellation is never recorded as an entry failure; verified by a cancellation test asserting no scan error and no completion.
- [x] 2.3 Verify access-guidance classification is unchanged for permission errors; verified by the existing access-guidance tests passing unmodified.

## 3. App Presentation and Failure Handling

- [x] 3.1 Add a display-name converter that shows a blank-name placeholder listing the code points of whitespace-only or invisible names; verified by converter unit tests.
- [x] 3.2 Switch AXAML name text bindings to the display name while commands, search, export, and reveal keep the real name and path; verified by tests that search and export use the real name.
- [x] 3.3 Catch non-cancellation scan failures in `RunScanAsync`, leave no completion time, and expose a scan failure message in the status area; verified by ViewModel tests with a scanner that throws after publishing progress.
- [x] 3.4 Clear the scan failure message when a new scan starts; verified by a ViewModel test.

## 4. Documentation

- [x] 4.1 Review `README.md`, `docs/STORAGE_MEASUREMENT.md`, `docs/FEATURES.md`, and `docs/index.html` for scan-robustness statements and update them or record that no change was needed; verified by the review notes in the change summary.
- [x] 4.2 Add this change to the WP-02 notes in `docs/IMPLEMENTATION_ROADMAP.md`; verified by the updated roadmap section.

## 5. Validation

- [x] 5.1 Run `dotnet build MacStorageAtlas.slnx --no-restore` with no errors.
- [x] 5.2 Run `dotnet test MacStorageAtlas.slnx --no-build` with all tests passing.
- [x] 5.3 Run `dotnet format MacStorageAtlas.slnx analyzers --diagnostics IDE0005 --verify-no-changes` with no changes reported.
- [x] 5.4 Run `openspec validate harden-scan-traversal --type change --strict --no-interactive` with no errors.
- [x] 5.5 Run `git diff --check` and confirm no generated output or unrelated working-tree changes were introduced.
- [x] 5.6 Re-run a whole-disk scan of `/` with hidden files included and confirm it completes and lists the previously failing entry; verified by the scan result.
