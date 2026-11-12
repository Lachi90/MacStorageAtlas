# Proposal

## Why

A sandbox acceptance test of 1.1.1 showed that the cleanup basket never moves
files to the Trash in the default shared-aware allocated mode. Basket
revalidation compares the size captured by the scan, which is an allocated
size in allocated modes, with the file's current logical length. The two
almost never match, so every file is reported as changed since the scan. The
defect exists since the cleanup basket was introduced and also blocks Move to
and Copy to, which reuse the same source revalidation. This belongs to WP-07
(cleanup basket) in `docs/IMPLEMENTATION_ROADMAP.md`.

## What Changes

- Basket revalidation compares a file's current size with the size captured
  by the scan in the same measurement basis: logical length in logical mode,
  locally allocated bytes in allocated and shared-aware allocated modes.
- Unchanged files in allocated modes become ready for Trash, Move to, and Copy
  to again; files whose allocation changed stay blocked.
- When the current allocated size cannot be read, revalidation fails closed and
  blocks the item.

## Non-goals

- Change basket totals or labels, protected-path rules, identity checks, or
  directory revalidation.
- Change the single-item Move to Trash action, which does not use basket
  revalidation.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `cleanup-basket`: Size revalidation uses the scan's measurement basis.

## Dependencies

- Uses the existing allocated metadata reader from `MacStorageAtlas.Platform.Mac`
  through the Core `IAllocatedFileMetadataReader` abstraction.

## Risks

- Allocated size can change without a content change, for example after APFS
  compression or a cloud provider evicting local data; such items are blocked
  conservatively and can be rescanned.

## Estimate

Half a day within WP-07.

## Impact

- Affected code: Core cleanup metadata reader and preflight validator; App
  validator wiring.
- Affected tests: Core validator and reader tests for each measurement mode;
  App basket Trash test in shared-aware allocated mode.
- Documentation: review `README.md`, `docs/FEATURES.md`, and `docs/index.html`
  for cleanup statements.
