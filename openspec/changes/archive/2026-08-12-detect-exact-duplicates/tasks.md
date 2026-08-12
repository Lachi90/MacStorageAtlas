## 1. Platform Spike and Boundaries

- [x] 1.1 Verify a macOS metadata-only way to classify dataless or not-local files without opening content, covering supported macOS versions where practical
- [x] 1.2 Record the chosen dataless-file signal and fallback behavior in `design.md` if the spike changes the conservative skip strategy
- [x] 1.3 Confirm the duplicate analyzer remains post-scan and does not require changes to `DiskScanner`, `ScanOptions`, scan progress, scan export, or scan history

## 2. Core Duplicate Models and Abstractions

- [x] 2.1 Add a Core duplicate-detection namespace and models for analysis options, progress, result summary, duplicate groups, group entries, linked paths, skipped candidates, and skip reasons
- [x] 2.2 Add Core abstractions for current candidate metadata and file-content reading with `CancellationToken` propagation
- [x] 2.3 Add tests for model construction, required-field guards, summary totals, skip reasons, and progress values
- [x] 2.4 Add tests using throwing test doubles to assert model and planning code does not read file contents before the analysis stage requires it

## 3. Candidate Collection

- [x] 3.1 Implement traversal from a completed `DiskItem` root that collects regular files without cloning scan subtrees
- [x] 3.2 Re-read current logical length through the duplicate metadata abstraction instead of relying on allocated-mode scan sizes
- [x] 3.3 Exclude zero-length files and single-entry current-length buckets by default before content reads
- [x] 3.4 Add tests proving unique-length files and default zero-length groups do not open content streams
- [x] 3.5 Add tests proving allocated-mode scan results still group candidates by current logical length
- [x] 3.6 Add cancellation tests for candidate traversal and metadata reads

## 4. Exact Verification Pipeline

- [x] 4.1 Implement beginning and ending sample comparison for same-length candidate buckets
- [x] 4.2 Implement streaming full-content hashing for surviving candidates with bounded buffers and cancellation
- [x] 4.3 Implement final byte-for-byte equality confirmation before creating exact duplicate groups
- [x] 4.4 Add tests for equal files, same-size different-content files, files that differ only at the beginning, files that differ only at the end, and files that differ only in the middle
- [x] 4.5 Add a large-file streaming test proving full file contents are not buffered in memory
- [x] 4.6 Add cancellation tests for sampling, hashing, and final equality confirmation

## 5. Stale, Error, Cloud, and Hardlink Handling

- [x] 5.1 Revalidate candidate size and available identity around content verification and skip changed, missing, replaced, or unreadable files
- [x] 5.2 Report skipped and failed candidates with user-visible reasons without stopping analysis for unrelated candidates
- [x] 5.3 Identify known hardlinked paths by current file identity and represent them as linked paths rather than reclaimable duplicate copies
- [x] 5.4 Skip known dataless or not-local cloud files without intentionally downloading contents
- [x] 5.5 Add tests for changed-size, replaced-identity, missing-file, read-error, and continued-analysis behavior
- [x] 5.6 Add hardlink tests asserting linked paths are shown and do not inflate reclaimable totals
- [x] 5.7 Add cloud-placeholder tests with platform/test doubles proving content streams are not opened for not-local files

## 6. Platform.Mac Support

- [x] 6.1 Add a macOS implementation of the duplicate candidate metadata and content-read abstractions, reusing existing file-identity metadata where appropriate
- [x] 6.2 Preserve Apple Silicon and Intel compatibility for any native metadata calls
- [x] 6.3 Add Platform.Mac tests for current logical length, file identity, hardlink identity, missing files, and dataless classification where a safe fixture is available
- [x] 6.4 Gate macOS-only integration tests and ignore them with a clear reason on unsupported platforms or unavailable fixture capabilities

## 7. App ViewModel Integration

- [x] 7.1 Compose the duplicate analyzer and Platform.Mac adapters in the App composition root with null or test implementations where needed
- [x] 7.2 Add `MainWindowViewModel` state for duplicate analysis running status, cancellation, progress, results, selected duplicate entry, skipped candidates, and status messages
- [x] 7.3 Add start and cancel commands enabled only when a completed scan result is available and analysis is not already in the incompatible state
- [x] 7.4 Run duplicate analysis off the UI thread, marshal state updates through `IUiDispatcher`, and clear duplicate results when the scan result is replaced
- [x] 7.5 Add view-model tests for start, unavailable during scan, progress update, cancellation, completion, no-duplicates result, skipped-file result, and clearing on rescan
- [x] 7.6 Add view-model tests proving duplicate analysis completion does not populate the cleanup basket or change the filesystem

## 8. Duplicate Review UI and Selection

- [x] 8.1 Add a Duplicates review surface near the existing result detail tabs using existing theme resources and compiled bindings where practical
- [x] 8.2 Show empty, running, cancelled, completed, no-duplicates, skipped-file, and read-error states without overlapping existing result controls
- [x] 8.3 Display duplicate groups with file names, paths, sizes, linked-path status, retained-copy arithmetic, and reclaimable totals that preserve one copy per group
- [x] 8.4 Wire duplicate entry selection into selected-item details, Reveal in Finder, Quick Look, and cleanup basket add/remove commands where the entry maps to a scanned item
- [x] 8.5 Add UI or view-model tests for duplicate selection, selected-item command enablement, add-to-basket behavior, already-selected behavior, and protected-path behavior through existing basket rules
- [x] 8.6 Review duplicate review text to ensure it never describes a duplicate file as safe to delete and never implies automatic cleanup selection

## 9. Documentation and Roadmap

- [x] 9.1 Review and update `README.md` for exact duplicate detection, opt-in local content reads, hardlink handling, cloud-placeholder skips, cancellation, and cleanup-basket integration
- [x] 9.2 Review and update relevant documentation under `docs/`, including `docs/FEATURES.md` and `docs/STORAGE_MEASUREMENT.md`, where duplicate behavior affects user-visible limitations or comparisons
- [x] 9.3 Review and update `docs/index.html` for the new duplicate review capability if landing-page copy is affected
- [x] 9.4 Update the WP-10 status row in `docs/IMPLEMENTATION_ROADMAP.md` when implementation is complete

## 10. Validation

- [x] 10.1 Run `dotnet build MacStorageAtlas.slnx --no-restore`
- [x] 10.2 Run `dotnet test MacStorageAtlas.slnx --no-build`
- [x] 10.3 Run `dotnet format MacStorageAtlas.slnx analyzers --diagnostics IDE0005 --verify-no-changes`
- [x] 10.4 Run `git diff --check`
- [x] 10.5 Run `openspec validate --all --strict --no-interactive`
