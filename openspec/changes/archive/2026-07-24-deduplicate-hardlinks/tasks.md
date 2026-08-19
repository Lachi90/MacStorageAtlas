## 1. Measurement Mode and Platform Metadata

- [x] 1.1 Add the explicit logical, per-path allocated, and hardlink-aware
  allocated choices to Core scan options and progress metadata, then update
  focused tests proving the two existing modes retain their current behavior.
- [x] 1.2 Add the Core-owned filesystem identity and allocated metadata
  contracts, including device-aware identity equality and injectable test
  readers.
- [x] 1.3 Move the architecture-aware macOS `stat(2)` reader from Core to
  Platform.Mac so one call returns allocated bytes, device, inode, and link
  count, then wire the production reader through the App composition root.
- [x] 1.4 Add gated macOS integration tests using isolated temporary normal,
  sparse, and hardlinked files to verify coherent allocated metadata and equal
  identity on both supported entry-point layouts where hardware is available.
- [x] 1.5 Add failure tests proving both allocated modes report unreadable or
  missing native metadata as recoverable scan errors without logical fallback.

## 2. Core Hardlink-Aware Accounting

- [x] 2.1 Extend `DiskItem` with measured allocation and shared-contribution
  state while keeping `SizeBytes` additive as the counted contribution, then
  add model and removal regression tests for logical and per-path results.
- [x] 2.2 Implement scan-local identity accounting so the first successfully
  measured included identity contributes bytes and later paths retain their
  measured allocation with zero additional contribution.
- [x] 2.3 Add Core tests for two hardlinks in one directory, hardlinks across
  sibling directories, a link outside scan scope, distinct device identities,
  zero-byte files, and path-based file counts.
- [x] 2.4 Limit identity retention to multi-link files when symbolic links are
  not followed, track all file identities when they are followed, and add tests
  proving a followed file alias is counted once without changing excluded-link
  behavior.
- [x] 2.5 Apply hardlink-aware accounting through collapsed packages and add
  tests for links within one package and links spanning a collapsed package
  boundary.
- [x] 2.6 Add error-injection and cancellation tests proving failed repeated
  paths do not remove successful contributions and every published partial
  total remains additive and mode-labelled.
- [x] 2.7 Update file-type statistics, largest-file ranking, sorting, and
  treemap inputs to consume counted contributions, with tests proving repeated
  identities do not create additional storage weight.

## 3. Application Options, Settings, and Presentation

- [x] 3.1 Replace the allocated-size checkbox with a compiled-binding
  three-way measurement choice, make hardlink-aware allocated measurement the
  default, and display concise mode labels in progress and completed results.
- [x] 3.2 Persist the selected mode as a named value and migrate legacy
  `MeasureAllocatedSize` settings to hardlink-aware allocated or logical mode,
  with JSON settings tests for legacy true, legacy false, valid new values,
  malformed values, and preservation of unrelated settings.
- [x] 3.3 Retain the completed result's measurement mode and scan options
  independently from preferences selected for the next scan, with view-model
  tests covering post-result preference changes.
- [x] 3.4 Show measured allocation and a shared-storage indication for
  additional hardlink paths in tree rows and selected-item details while
  keeping every path available through tree browsing and search.
- [x] 3.5 Add App tests proving shared paths remain selectable, display their
  measured bytes rather than an unexplained zero, and do not receive duplicate
  treemap or derived-view weight.

## 4. Trash Result Consistency

- [x] 4.1 Refresh a completed hardlink-aware result with its captured options
  after a successful Trash operation, clearing stale completion state before
  the cancellable rescan begins.
- [x] 4.2 Preserve the existing result when confirmation is cancelled or Trash
  fails, and clear the result without rescanning when the scanned root itself
  is moved to Trash.
- [x] 4.3 Add view-model tests for removing the counted representative,
  removing an additional link, a remaining link inside a collapsed package,
  root removal, refresh cancellation, and Trash failure.
- [x] 4.4 Confirm logical and per-path allocated results retain correct
  in-memory removal behavior with focused regression tests.

## 5. Documentation and WP-02 Tracking

- [x] 5.1 Update `docs/STORAGE_MEASUREMENT.md` with hardlink-aware terminology,
  representative-path attribution, scope and reclaimability limits, followed
  symbolic links, APFS clone limitations, settings behavior, and post-Trash
  refresh semantics.
- [x] 5.2 Add a reproducible macOS hardlink fixture using `stat`, ordinary
  `du`, and per-path `du` behavior, run it on available macOS hardware, and
  record the architecture-independent observations and verification date.
- [x] 5.3 Review and update `README.md`, `docs/FEATURES.md`, and
  `docs/index.html` for the new default, three modes, and comparison claims
  without describing hardlink-only results as unique physical storage.
- [x] 5.4 Review the user-visible screenshots and update the scan-options image
  and related alt text when the three-way control makes the existing screenshot
  inaccurate.
- [x] 5.5 Update the WP-02 status and notes in
  `docs/IMPLEMENTATION_ROADMAP.md` to record completion of
  `deduplicate-hardlinks` while leaving APFS investigation and benchmark work
  outstanding.

## 6. Validation

- [x] 6.1 Run `dotnet build MacStorageAtlas.slnx --no-restore` and resolve all
  build and analyzer failures.
- [x] 6.2 Run `dotnet test MacStorageAtlas.slnx --no-build` and resolve all
  Core, platform, rendering, App, settings, cancellation, and Trash regressions.
- [x] 6.3 Run
  `dotnet format MacStorageAtlas.slnx analyzers --diagnostics IDE0005 --verify-no-changes`
  and remove every obsolete import.
- [x] 6.4 Run `openspec validate --all --strict --no-interactive` and resolve
  every artifact or specification error.
- [x] 6.5 Run `git diff --check`, inspect the complete diff for unrelated or
  generated files, and confirm both macOS architectures remain supported.
