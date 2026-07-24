## Why

MacStorageAtlas currently offers logical-size and allocated-size scans, but the
meaning and limitations of the reported numbers are spread across code comments
and UI copy. WP-02 requires a stable measurement contract before hardlink,
APFS-clone, volume-capacity, or benchmark work can produce defensible and
comparable results.

## What Changes

- Define logical size, allocated file size, unique allocated size, and the
  volume free/used/available/purgeable terms used by the roadmap.
- Establish the current scan contract: every reported file and directory size
  has an explicit logical or allocated measurement basis, and directory totals
  use the same basis as their descendants.
- Clarify that allocated mode measures allocated blocks per visited path and
  does not yet deduplicate hardlinks or APFS clones.
- Document how symbolic links, application packages, sparse files, cloud
  placeholders, unreadable entries, and cancelled scans affect reported totals.
- Align user-facing scan-option wording and developer documentation with the
  measurement contract.
- Add focused automated coverage for the existing logical and allocated
  behaviors that the contract makes normative.

## Non-goals

- Deduplicate hardlinks or add unique-allocated-size scan mode; that is the
  subsequent `deduplicate-hardlinks` change.
- Detect or account for APFS clone sharing.
- Implement volume-capacity or purgeable-space reporting.
- Add benchmark tooling or optimize filesystem traversal.
- Materialize cloud placeholders to determine their remote logical contents.

## Capabilities

### New Capabilities

- `storage-measurement`: Defines storage-size terminology, scan measurement
  modes, aggregation rules, limitations, and user-visible identification of the
  measurement basis.

### Modified Capabilities

None.

## Dependencies

- The contract must reflect current `DiskScanner`, `NativeFileSize`, scan-option,
  and macOS `stat(2)` behavior.
- Later WP-02 changes depend on these terms remaining stable or being revised
  explicitly through OpenSpec.

## Risks

- Finder, `du`, and volume-capacity displays use context-dependent definitions,
  so implying exact cross-tool equality would make the contract misleading.
- Allocated bytes summed per path can overstate unique physical consumption for
  hardlinks and APFS clones unless that limitation remains prominent.
- Platform fallbacks can blur allocated and logical semantics if they are not
  represented and tested explicitly.

## Estimate

One to two days within WP-02's overall roadmap estimate of 10–18 days.

## Impact

- Affected code: measurement-related Core models and scanner tests; App
  scan-option labels or explanatory text where needed.
- Affected documentation: a canonical storage-measurement reference and
  relevant README/help wording.
- Affected OpenSpec capability: new `storage-measurement` specification.
- No new packages, external services, destructive actions, or network access.
