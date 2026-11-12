# Proposal

## Why

Scanning `/` visits the same user data twice on macOS 10.15 and later: once
through firmlinked paths such as `/Users`, `/Applications`, and `/Library`, and
once through the Data volume at `/System/Volumes/Data`. In shared-aware
allocated mode the scanner counts each file identity at the path it reaches
first. When `/System` sorts before `/Users`, every home folder shows
`0 B counted` and all of its storage as shared, while the real contribution is
attributed under `/System/Volumes/Data`. In logical and per-path allocated
modes, the same scan counts firmlinked data twice. Both outcomes undermine the
WP-02 goal of defensible storage numbers in the most natural whole-disk scan.

## What Changes

- When one scan includes both a firmlinked path and the Data-volume path it
  resolves to, the scan attributes that content to the user-facing firmlinked
  path and does not traverse the Data-volume alternate.
- Content on the Data volume that has no firmlinked equivalent, such as
  `/System/Volumes/Data/home` or other directories outside the firmlink list,
  remains scanned and browsable.
- An alternate is omitted only when the system firmlink list names it and both
  paths currently resolve to the same directory identity. Otherwise the scan
  keeps today's behavior and visits both paths.
- Scans whose scope contains only one side of a firmlink, such as a scan of
  `/Users`, `~`, or `/System/Volumes/Data`, are unchanged.
- Completed results disclose which alternate paths were omitted as equivalent,
  so a smaller `/System/Volumes/Data` is explained rather than surprising.
- Logical and per-path allocated scans of `/` stop double-counting firmlinked
  data. Shared-aware allocated scans of `/` keep the same counted total but
  attribute it to the visible paths regardless of traversal order.

## Non-goals

- Change how hardlinks, APFS clones, or file identities are counted.
- Stop traversal at volume or mount-point boundaries, or change how `/Volumes`,
  other mounted volumes, or `/System/Volumes/*` volumes other than Data are
  scanned.
- Change symbolic-link handling, including links such as `/home`, `/etc`, or
  `/tmp`.
- Infer equivalence from names, sizes, or contents, or maintain a hard-coded
  list that ignores the running system's firmlink definition.
- Rewrite or migrate stored scan-history snapshots.
- Show volume capacity, used, or purgeable space.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `storage-measurement`: Add a requirement that firmlink-equivalent system
  paths contribute once per scan, are attributed to the user-facing path,
  remain verifiable against current directory identity, and are disclosed in
  the result.

## Dependencies

- Builds on the archived `define-storage-measurement`, `deduplicate-hardlinks`,
  and `investigate-apfs-clone-accounting` changes and the current
  `storage-measurement` specification.
- Relies on the macOS system firmlink definition and on directory identity
  reads that already exist in `MacStorageAtlas.Platform.Mac`.
- No dependency on Full Disk Access; omitted alternates behave the same with or
  without it.

## Risks

- Apple can change the firmlink definition format or location; the scan must
  fall back to visiting both paths rather than omit content it cannot verify.
- Scan totals for `/` in logical and per-path allocated modes decrease, and
  per-path measured aggregates decrease in every mode, which makes comparisons
  with older history snapshots of `/` look like large deletions.
- Users who intentionally browse `/System/Volumes/Data` from a `/` scan see a
  smaller subtree; the disclosure must make that understandable.
- Omitting a subtree that is not truly equivalent would hide storage, so the
  identity check must be strict and fail open.

## Estimate

Two to four days within WP-02's overall roadmap estimate of 10–18 days.

## Impact

- Affected code: Core scan options or scanner inputs for equivalent-path
  omission, completed-result disclosure, Platform.Mac firmlink discovery and
  identity verification, App composition root and result summary
  presentation.
- Affected tests: Core traversal, attribution, totals per measurement mode,
  cancellation, and fallback; Platform.Mac parsing and identity verification
  with gated macOS integration tests; App presentation of omitted alternates.
- Affected documentation: `docs/STORAGE_MEASUREMENT.md`, `README.md`,
  `docs/FEATURES.md`, `docs/IMPLEMENTATION_ROADMAP.md`, and `docs/index.html`
  after explicit review.
- Affected OpenSpec capability: `storage-measurement`.
- No new package, network access, content reads, persistent database, or
  filesystem mutation.
