# Proposal

## Why

A real whole-disk scan aborted with an unhandled `ArgumentException` because a
directory entry produced a name that `DiskItem` rejects as empty or
whitespace-only. The exception escapes the scanner, ends the entire scan
without a result, and, because the scan command only handles cancellation, can
terminate the app. One unusual entry must not cost the user every other
measured byte; WP-02 requires scans that stay resilient to recoverable
filesystem conditions.

## What Changes

- Entries whose names consist only of whitespace or invisible characters are
  scanned, measured, and shown like any other entry, with a visible
  representation in the tree.
- An entry whose display name cannot be derived from its path is shown using
  its full path instead of failing.
- An unexpected failure while processing one entry is recorded as a scan error
  for that path, and scanning continues with its siblings.
- If a scan still fails unexpectedly as a whole, the app ends the scan, keeps
  the app running, and reports the failure instead of crashing.

## Non-goals

- Change measurement modes, accounting, or the firmlink handling introduced by
  `attribute-firmlinked-storage`.
- Rename, normalize, or otherwise modify entries on disk.
- Retry failed entries or add new access-guidance categories.
- Add crash reporting, telemetry, or logging of scanned paths.

## Capabilities

### New Capabilities

- `scan-traversal`: How the scanner walks a scan root, represents every
  entry it encounters, and contains failures to the entries that cause them.

### Modified Capabilities

None.

## Dependencies

- Builds on the recoverable-error and partial-result behavior in
  `storage-measurement` and the scan error list in `scan-access-guidance`.
- Independent of `attribute-firmlinked-storage`; both touch `DiskScanner`, so
  implement them one after the other.

## Risks

- Catching broader failures per entry could hide programming errors; the
  failure must stay visible as a scan error with its exception type.
- Whitespace-only names are hard to see in the tree; the visible
  representation must not be confused with a different real name.
- Search, filters, export, history, and cleanup use item names; they must
  keep using the real name rather than its visible representation.

## Estimate

One to two days within WP-02's overall roadmap estimate of 10–18 days.

## Impact

- Affected code: Core `DiskItem` name validation and `DiskScanner` per-entry
  failure handling; App scan command failure handling and tree name
  presentation.
- Affected tests: Core scanner tests with whitespace-only and invisible names
  and injected per-entry failures; App tests for an unexpectedly failing scan.
- Affected documentation: review `README.md`, `docs/STORAGE_MEASUREMENT.md`,
  `docs/FEATURES.md`, and `docs/index.html` for scan-robustness statements.
- No new package, network access, content reads, or filesystem mutation.
