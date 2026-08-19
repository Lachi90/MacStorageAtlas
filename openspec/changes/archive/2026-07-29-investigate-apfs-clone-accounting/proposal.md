## Why

WP-02 requires a defensible answer for APFS clone accounting because the
current hardlink-aware total still counts shared storage once for every
distinct clone identity. Public macOS metadata can verify fully shared clone
data on capable volumes, so MacStorageAtlas can improve common APFS results
without claiming that divergent clones or every physical extent are uniquely
accounted.

## What Changes

- Extend the default allocated accounting mode to count verified fully shared
  APFS data allocation once within the active scan scope, in addition to
  counting hardlinked file identities once.
- Enable full-clone accounting only when the volume advertises supported clone
  mapping and the file metadata verifies the relationship; otherwise fail
  closed by counting the allocation normally.
- Preserve per-path measured allocation and represent the number of bytes whose
  contribution is counted through another included path, including cases where
  only a file's data allocation is shared.
- Capture the clone-accounting coverage achieved by each progress update and
  completed result so the UI can distinguish supported, unsupported, and
  degraded scans.
- Keep divergent or partially shared APFS clones counted per filesystem
  identity and disclose that limitation wherever the measurement basis is
  explained.
- Add reproducible macOS integration fixtures for ordinary copies, full clones,
  divergent clones, hardlinks, sparse files, non-data allocation, and
  unsupported capability fallback.
- Update user-facing storage terminology and comparisons without introducing
  or claiming unique allocated size.

## Non-goals

- Deduplicate partially shared APFS extents or enumerate physical block maps.
- Infer clone sharing from equal contents, names, dates, sizes, or hashes.
- Predict bytes reclaimed by Trash or deletion.
- Read file contents, open files for extent inspection, contact cloud
  providers, or materialize cloud placeholders.
- Change logical or per-path allocated measurement.
- Add benchmark infrastructure or parallel traversal; those remain in
  `benchmark-and-optimize-scans`.
- Drop support for macOS 11 through 13, Intel Macs, non-APFS volumes, or volumes
  without clone mapping.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `storage-measurement`: Extend scan-scoped allocated accounting with verified
  full-clone data deduplication, explicit coverage metadata, partial-sharing
  fallback, shared-byte presentation, and reproducible clone fixtures.

## Dependencies

- Depends on the archived `define-storage-measurement` and
  `deduplicate-hardlinks` changes and the current `storage-measurement`
  specification.
- Uses public macOS volume capabilities and extended file attributes while
  keeping all APFS-specific interpretation in `MacStorageAtlas.Platform.Mac`.
- Preserves the existing post-Trash refresh behavior because removing a
  representative clone can transfer the counted contribution to another
  included path.

## Risks

- macOS and filesystem capability differences can produce different accounting
  coverage for otherwise similar scans, so coverage must travel with the result
  and remain visible.
- Clone identifiers describe file data streams rather than every non-data
  allocation, so treating the whole per-path allocation as shared could
  undercount storage.
- Tracking clone identities increases scan-local memory use and makes
  representative attribution traversal-order dependent, although the complete
  scan total remains stable.
- Optional clone metadata can be unavailable for individual entries; silent
  optimistic deduplication would undercount, while safe fallback can still
  overcount shared storage.
- Native attribute layouts and availability must be verified on both Apple
  Silicon and Intel and must not weaken the existing coherent metadata read.

## Estimate

Four to seven days within WP-02's overall roadmap estimate of 10–18 days.

## Impact

- Affected code: Core allocated metadata and result contracts, scan-local
  accounting, progress and item shared-byte state; macOS volume capability and
  file-attribute integration; App measurement labels and item details.
- Affected tests: Core accounting, aggregation, errors, cancellation,
  symbolic-link and package behavior, App presentation, and gated macOS clone
  fixtures on supported architectures and volumes.
- Affected documentation: `README.md`, `docs/STORAGE_MEASUREMENT.md`,
  `docs/FEATURES.md`, `docs/IMPLEMENTATION_ROADMAP.md`, and `docs/index.html`
  after explicit review.
- Affected OpenSpec capability: `storage-measurement`.
- No new package, external service, persistent scan database, content hashing,
  permanent deletion, or network access.
