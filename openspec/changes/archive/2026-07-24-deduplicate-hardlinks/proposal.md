## Why

WP-02 requires MacStorageAtlas to stop overstating allocated scan totals when
multiple included paths refer to the same filesystem object. The existing
measurement contract now distinguishes per-path allocation from unique
allocation, so hardlink-aware accounting can be added without implying that
APFS clone sharing is already solved.

## What Changes

- Add a hardlink-aware allocated scan mode that counts one allocation for each
  included filesystem identity while retaining every included path in the
  result tree.
- Preserve each path's measured allocated bytes and identify paths whose
  storage contribution is counted through another path.
- Keep file counts path-based while making directory, progress, treemap,
  file-type, and largest-file byte totals use the hardlink-aware contribution.
- Make hardlink-aware allocated measurement the application default, retain
  logical and per-path allocated choices, and migrate the existing allocated
  preference without discarding user settings.
- Treat followed symbolic-link paths that resolve to an already counted file
  identity consistently with other repeated identities.
- Refresh hardlink-aware results after a successful Trash operation so a
  remaining link can assume the counted contribution and displayed totals do
  not become stale.
- Document that hardlink-aware totals remain scan-scoped, can depend on which
  path represents a shared identity, do not deduplicate APFS clones, and are
  not a promise of bytes reclaimable by deleting one path.

## Non-goals

- Detect or deduplicate APFS clone extents.
- Claim fully unique physical-storage accounting before the APFS investigation
  is complete.
- Add volume capacity, used, available, or purgeable reporting.
- Add scan benchmark tooling, parallel traversal, or other performance work.
- Add export functionality; a later export change must preserve the shared
  storage semantics introduced here.
- Read file contents, hash files, contact cloud providers, or materialize cloud
  placeholders.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `storage-measurement`: Add hardlink-aware allocated measurement, repeated
  file-identity accounting, shared-path presentation, settings behavior, and
  post-Trash result consistency while retaining the existing logical and
  per-path allocated modes.

## Dependencies

- Depends on the archived `define-storage-measurement` change and the current
  `storage-measurement` specification.
- Uses stable macOS file metadata available through `stat(2)` on both supported
  architectures.
- The later `investigate-apfs-clone-accounting` change must preserve or
  explicitly revise the terminology and result model established here.

## Risks

- Tracking identities increases managed memory use, especially when symbolic
  links are followed or a scan contains many hardlinks.
- Assigning one counted contribution to a representative path makes
  per-directory attribution dependent on the representative policy even
  though the complete scan total remains deduplicated.
- Refreshing a large result after Trash can take materially longer than the
  current in-memory subtraction.
- Filesystem contents can change during a scan, so identity and size metadata
  must be read coherently and incomplete results must remain explicit.

## Estimate

Three to five days within WP-02's overall roadmap estimate of 10–18 days.

## Impact

- Affected code: Core scan options, measurement modes, result items,
  aggregation, statistics, and scanner tests; macOS metadata integration;
  application settings, composition, item details, scan options, and
  post-Trash refresh behavior; treemap and view-model tests where counted and
  measured bytes differ.
- Affected documentation: `README.md`, `docs/STORAGE_MEASUREMENT.md`,
  `docs/IMPLEMENTATION_ROADMAP.md`, and `docs/index.html` after explicit review.
- Affected OpenSpec capability: `storage-measurement`.
- No new packages, external services, persistent scan database, permanent
  deletion, or network access.
