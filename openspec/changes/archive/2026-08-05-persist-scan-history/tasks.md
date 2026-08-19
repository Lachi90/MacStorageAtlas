## 1. Shared row serialization

- [x] 1.1 Extract the per-row JSON writing currently inside
  `ScanResultJsonWriter` into a shared internal row-writing helper in
  `MacStorageAtlas.Core`, leaving the export document shape byte-identical
- [x] 1.2 Extract the per-row JSON reading currently inside
  `ScanResultJsonReader` into the matching shared helper
- [x] 1.3 Confirm the existing export round-trip and determinism tests in
  `tests/MacStorageAtlas.Core.Tests` pass unchanged against the extracted
  helpers

## 2. Snapshot model

- [x] 2.1 Add `ScanCompleteness` to `MacStorageAtlas.Core` distinguishing a
  scan that read everything, one that hit recoverable errors, one blocked by
  access restrictions, and one whose completeness is undetermined
- [x] 2.2 Add `ScanSnapshotMetadata` recording the snapshot identity, capture
  instant, scan root, scan completion time, scan options, measurement mode,
  clone-accounting coverage, item count, total counted bytes, error count, and
  completeness
- [x] 2.3 Add `ScanSnapshotSchema` with the current version constant and add
  `ScanSnapshotDocument` pairing metadata with rows and recoverable errors
- [x] 2.4 Add `ScanSnapshotDescriptor` carrying the listing fields a history
  entry shows without inflating a snapshot body
- [x] 2.5 Add tests covering metadata construction, required-field guards, and
  completeness values

## 3. Snapshot writer and reader

- [x] 3.1 Add `ScanSnapshotJsonWriter` writing `schemaVersion`, `scan`,
  `errors`, then `items` through a `GZipStream`, streaming rows from
  `ScanExportRowSource.EnumerateFull` and honouring a `CancellationToken`
- [x] 3.2 Add `ScanSnapshotJsonReader` reading a full snapshot document back
  into metadata, rows, and errors
- [x] 3.3 Add a descriptor-only read path that inflates the leading metadata
  and stops once `scan` closes, without reading `items`
- [x] 3.4 Make the reader refuse an unrecognised schema version with a result
  that states the version found, rather than throwing or parsing optimistically
- [x] 3.5 Add round-trip tests proving every metadata and item field equals the
  value it was written from, including absent timestamps and absent categories
- [x] 3.6 Add tests for the descriptor-only read, for cancellation mid-write,
  for a truncated or corrupt body, and for an unreadable schema version

## 4. Retention policy

- [x] 4.1 Add `ScanHistoryRetentionPolicy` in `MacStorageAtlas.Core` taking
  existing descriptors, the incoming snapshot size, and the limits, returning
  the snapshots to prune or a refusal
- [x] 4.2 Implement oldest-first pruning that removes no more snapshots than
  required and never prunes across a scan root to satisfy another root's count
  limit
- [x] 4.3 Implement refusal when a single snapshot exceeds the total store size
  limit on its own
- [x] 4.4 Add tests for count-limit pruning, total-size pruning, combined
  limits, the minimal-pruning guarantee, refusal, and lowering a limit bringing
  an existing store within it

## 5. History store

- [x] 5.1 Add `IScanHistoryStore` in `MacStorageAtlas.Core` exposing list,
  capture, read, delete, clear, and total-size operations with
  `CancellationToken` propagation
- [x] 5.2 Add `FileSystemScanHistoryStore` taking its root directory as a
  constructor argument, creating the directory with owner-only access and
  writing a `.metadata_never_index` marker
- [x] 5.3 Implement capture as write-to-pending, measure, apply retention, then
  publish by move, deleting the pending file on cancellation, failure, or
  refusal
- [x] 5.4 Sweep orphaned pending files on store construction and before each
  capture
- [x] 5.5 Set owner-only permissions on each published snapshot file
- [x] 5.6 Implement listing that reports unreadable snapshots as entries the
  user can delete without discarding the readable ones
- [x] 5.7 Add store tests on isolated temporary directories covering capture and
  publish, pending cleanup after cancellation and failure, refusal of an
  oversized snapshot, pruning on capture, delete, clear, an unreadable snapshot
  among readable ones, and an entirely unreadable store

## 6. Application settings

- [x] 6.1 Add scan-history enablement, per-root snapshot limit, and total store
  size limit to `AppSettings` with history disabled by default
- [x] 6.2 Verify `JsonSettingsService` loads an existing settings file without
  the new fields and persists the new fields on save
- [x] 6.3 Add tests for defaults, round-trip persistence, and loading a settings
  file written before this change

## 7. Capture wiring

- [x] 7.1 Compose the history store in the App composition root against the
  Application Support location
- [x] 7.2 Map `AccessGuidanceStatus` and the scan's recoverable errors onto
  `ScanCompleteness` at capture time
- [x] 7.3 Start capture from the scan orchestration path after the completed
  result is displayed, off the UI thread, with its own cancellation source
- [x] 7.4 Cancel a capture in progress when a new scan starts
- [x] 7.5 Capture the full result regardless of the active filter, and capture
  nothing for a cancelled or failed scan
- [x] 7.6 Surface capture outcome as status, including refusal because the scan
  was too large and failure without disturbing the displayed result
- [x] 7.7 Add view-model tests for capture on completion, no capture when
  history is disabled, no capture for a cancelled scan, full-result capture
  under an active filter, cancellation on a new scan, and failure leaving the
  displayed result usable

## 8. History user interface

- [x] 8.1 Add a history view model exposing snapshots grouped by scan root with
  completion time, item count, stored size, measurement mode, and completeness
- [x] 8.2 Present the store location and total store size, and an empty state
  when no scan has been recorded
- [x] 8.3 Add the history entry point to the existing scan-options surface
  rather than a new top-level toolbar group
- [x] 8.4 Add enablement and retention-limit controls, applying a lowered limit
  immediately
- [x] 8.5 Add delete-one and clear-all actions with explicit confirmation for
  clearing, leaving scan options, presets, and recent locations untouched
- [x] 8.6 Add compiled bindings with `x:DataType` and reuse existing theme
  resources for the history surfaces
- [x] 8.7 Add tests for grouping and listing, the empty state, unreadable-entry
  presentation, delete-one, clear-all leaving other settings intact, and a
  lowered limit pruning immediately

## 9. Documentation

- [x] 9.1 Document scan history in `docs/FEATURES.md`, including what a snapshot
  records, that capture is opt-in, where the store lives, its default limits,
  that it is gzip-compressed JSON readable with `gunzip`, and that Time Machine
  backs it up
- [x] 9.2 Review and update `README.md` and `docs/index.html` for the new
  user-visible capability, and report if no update was necessary
- [x] 9.3 Update the WP-09 status row in `docs/IMPLEMENTATION_ROADMAP.md` to
  record that the persistence half is delivered and comparison remains
  outstanding

## 10. Reveal the store and tolerate external change

- [x] 10.1 Skip a snapshot file that disappears while the history is being
  listed instead of reporting it as a snapshot that could not be read
- [x] 10.2 Add store tests for the store directory being removed between
  operations, a snapshot removed mid-listing, and capture recreating a removed
  store
- [x] 10.3 Add a reveal-store command to the history view model backed by the
  existing `IFileRevealService`, unavailable while nothing is stored
- [x] 10.4 Add the reveal action beside the store location in the history
  surface and pass the reveal service through the composition root
- [x] 10.5 Add view-model tests for revealing the store, for the action being
  unavailable when the history is empty, and for a failed reveal being reported
- [x] 10.6 Document manual removal through Finder in `docs/FEATURES.md`

## 11. Validation

- [x] 11.1 Run `dotnet build MacStorageAtlas.slnx --no-restore`
- [x] 11.2 Run `dotnet test MacStorageAtlas.slnx --no-build`
- [x] 11.3 Run
  `dotnet format MacStorageAtlas.slnx analyzers --diagnostics IDE0005 --verify-no-changes`
- [x] 11.4 Run `git diff --check`
- [x] 11.5 Run `openspec validate --all --strict --no-interactive`
