## Context

The archived `define-storage-measurement` change established that the current
logical and allocated modes store one basis-dependent value in
`DiskItem.SizeBytes`, aggregate that value through directories and progress,
and label completed results with `StorageMeasurementMode`. Allocated mode reads
`st_blocks × 512` for every visited path, so two hardlinks currently contribute
twice.

The existing macOS `stat` buffer already exposes device, inode, link count,
logical length, and allocated blocks, but the P/Invoke lives in portable Core
and returns only allocated bytes. The scanner has injectable size delegates for
tests but no file-identity capability. Rendering, file-type summaries, largest
files, tree rows, and post-Trash result mutation all assume that a file's
displayed size and its contribution to aggregate storage are the same value.

Hardlink accounting is scan-scoped. A link outside the selected scope can keep
storage alive even though it does not appear in the result. APFS clones have
distinct file identities, so device-and-inode accounting cannot make the
result fully unique physical storage. Scanning must remain streaming,
cancellable, metadata-only, and compatible with both Apple Silicon and Intel
Macs.

## Goals / Non-Goals

**Goals:**

- Add a distinct hardlink-aware allocated mode without changing the meaning of
  logical or per-path allocated measurement.
- Count one allocated contribution for each included filesystem identity while
  retaining every included path and its measured allocation.
- Keep directory, progress, derived-view, and completed totals additive.
- Make repeated-path semantics visible without implying reclaimable or
  clone-aware physical bytes.
- Move macOS metadata access behind a Core-defined platform capability.
- Preserve streaming progress, recoverable errors, cancellation, package
  presentation, symbolic-link options, and settings compatibility.
- Keep displayed results honest after Trash changes a hardlink group.

**Non-Goals:**

- Detect APFS shared extents or infer clones from equal content.
- Produce volume-capacity values or reclaimable-byte predictions.
- Add exports, benchmark infrastructure, parallel traversal, or content
  hashing.
- Persist file identities or scan results.
- Redesign cleanup confirmation or replace Trash with permanent deletion.

## Decisions

### Add a third explicit measurement and accounting mode

`StorageMeasurementMode` will gain a hardlink-aware allocated value and
`ScanOptions` will select an explicit mode instead of deriving it solely from
`MeasureAllocatedSize`. Logical mode will continue to count logical length for
every included path. Per-path allocated mode will continue to count allocated
blocks for every included path. Hardlink-aware allocated mode will measure the
same per-path allocation but count each filesystem identity once.

The App will replace the allocated-size checkbox with a three-way choice and
make hardlink-aware allocated measurement the default. Result labels will use
the mode captured at scan start rather than the current next-scan preference.

Alternative considered: silently make the existing allocated mode deduplicate
hardlinks. This would change a term whose per-path meaning was deliberately
stabilized by the preceding OpenSpec change and would remove a useful
diagnostic comparison mode.

Alternative considered: call the new mode unique allocated size. This would
overstate the implementation because distinct APFS clone identities can still
share physical extents.

### Read allocated metadata and identity atomically through Platform.Mac

Core will define a small allocated-file metadata contract containing allocated
bytes, a filesystem identity, and link count. The identity will combine device
and inode so equal inode numbers on different volumes remain distinct.
Platform.Mac will implement the contract with one architecture-aware `stat(2)`
call and the App composition root will inject it into `DiskScanner`.

The existing `stat` layouts and the `stat$INODE64` entry point on Intel will be
preserved. Unit tests will inject metadata without depending on macOS, while
gated macOS integration tests will create temporary hardlinks and verify
identity and allocation behavior on the available architecture. No unstable
API or technical spike is required.

Alternative considered: call separate size and identity readers. A path could
be replaced between calls, producing an incoherent identity and allocation.

Alternative considered: keep the P/Invoke inside Core. Moving the native
reader behind a Core-owned interface restores the repository's intended
dependency boundary and prepares the same boundary for the later APFS
investigation.

### Separate measured bytes from counted contribution

`DiskItem.SizeBytes` will remain the value consumed by directory aggregation,
sorting, progress, file-type statistics, largest-file ranking, and Rendering.
In hardlink-aware mode it represents the counted contribution: the
representative path receives the measured allocation and additional paths
receive zero contribution. A separate measured-size value and shared-storage
state will retain the allocation attributed to every file path.

Tree rows and item details will show the measured allocation for an additional
link together with a shared-storage indication instead of presenting it as an
ordinary zero-byte file. The treemap will continue to omit zero-contribution
items because they add no distinct hardlink allocation. Tree browsing and
search remain the complete way to find every included path.

Alternative considered: keep full `SizeBytes` on every hardlink and deduplicate
only the root progress total. That makes parent totals non-additive and causes
treemap children, file-type summaries, and deletion updates to disagree with
the root.

Alternative considered: make every directory independently deduplicate its own
subtree. A file linked across sibling directories would then be counted in each
sibling but once in the parent, again breaking additive layout and aggregation.

### Use the first successfully measured included path as representative

Hardlink-aware scan state will keep a scan-local table keyed by filesystem
identity. The first successfully measured included path contributes its
allocated bytes. A later path with the same identity retains its measured
allocation, is marked as counted elsewhere, and contributes zero. File counts
continue to count paths.

When symbolic-link following is disabled, identities with a filesystem link
count of one do not need to enter the table. When following is enabled, all
successfully measured file identities must be tracked because a symbolic-link
path can repeat a target whose hardlink count is one. The identity table is
released with scan state and is never persisted or logged.

This policy preserves streaming and cancellation without sorting entire
directories or revising already published totals. It means which sibling
directory receives a shared contribution can follow filesystem traversal
order; the complete scan total remains stable and the UI identifies additional
paths.

Alternative considered: select the lexicographically smallest path. That
requires sorting traversal or transferring contributions and ancestor totals
when a smaller path appears later, increasing memory and mutation complexity
for no improvement to the scan-wide total.

### Apply one accounting path to packages, errors, and cancellation

Collapsed packages will still be traversed for measurement while their
descendants remain absent from presentation. Their files participate in the
same scan-wide identity table, so a hardlink spanning a package boundary is
counted once. Package aggregates retain both counted and measured descendant
values needed for an honest collapsed presentation.

An allocated metadata or identity failure will use the existing recoverable
scan-error path and contribute no invented value. Cancellation will stop
traversal through the existing token checks; the latest partial tree and
progress total will contain only successfully measured identities, each
counted at most once.

Alternative considered: ignore identity failures and count the allocated
value. This would silently weaken hardlink-aware mode and could double count
precisely the files it claims to identify.

### Migrate settings explicitly

Persisted settings will store the selected measurement mode as a named enum
value. Loading will prefer a valid new mode, otherwise migrate the legacy
`MeasureAllocatedSize` value: `true` becomes hardlink-aware allocated and
`false` becomes logical. Saving will write the new mode while preserving
unrelated scan options and recent locations.

Alternative considered: map legacy `true` to per-path allocated. That preserves
old totals but leaves existing users on the less defensible mode after the
feature ships. The old preference expressed a desire for on-disk allocation,
which the hardlink-aware default represents more accurately.

### Refresh hardlink-aware results after successful Trash operations

The current in-memory removal subtracts the selected item's contribution from
every ancestor. If the representative hardlink is removed while another
included link remains, that subtraction incorrectly removes the allocation
from the scan total. Collapsed packages make reliable in-memory transfer still
more complex because their descendant items are not retained for browsing.

After a successful Trash operation against a hardlink-aware result, the App
will clear the now-stale complete presentation and rescan the remaining root
with the options captured for that result. If the scanned root itself was
moved, the result will be cleared. A failed or cancelled Trash operation will
not mutate or refresh the result. Logical and per-path allocated results can
retain their current local-removal behavior.

Alternative considered: transfer the contribution to another retained group
member in memory. This fails when the remaining member is inside a collapsed
package and cannot account for concurrent filesystem changes.

### Keep responsibilities within existing project boundaries

- **Core:** owns measurement-mode vocabulary, file-identity and metadata
  contracts, scan-local deduplication, counted and measured result values,
  additive aggregation, errors, and cancellation.
- **Rendering:** continues to consume counted `SizeBytes` without learning
  filesystem identity or hardlink policy.
- **Platform.Mac:** owns architecture-specific `stat(2)` integration and returns
  coherent allocated metadata without reading contents.
- **App:** injects the platform reader, presents the three modes and shared-path
  details, migrates settings, retains result options, and refreshes
  hardlink-aware results after Trash.
- **Tests:** covers injected Core identities, App presentation and migration,
  derived views, errors, cancellation, Trash refresh, and gated macOS
  hardlink/sparse-file fixtures.

## Risks / Trade-offs

- **Identity tracking can increase memory on large scans** → Avoid allocating
  identity entries for single-link files unless symbolic-link following makes
  repeat aliases possible; add focused high-entry-count tests or measurements
  without bundling the separate benchmark change.
- **Representative attribution can vary with traversal order** → Keep the
  scan-wide total invariant, preserve measured values on every path, and state
  the representative policy in user and developer documentation.
- **A scan is not a filesystem snapshot** → Read identity and allocation in one
  metadata operation, treat failures explicitly, and avoid reclaimable-byte
  promises.
- **Automatic post-Trash refresh can be slow** → Limit it to hardlink-aware
  completed results, preserve the result's original options, keep it
  cancellable, and clear stale completion state before scanning.
- **Additional model fields consume memory per result item** → Store compact
  value state on the existing tree rather than retaining a second full tree or
  path list.
- **Settings migration can misread malformed or future values** → Validate the
  stored enum and fall back to the hardlink-aware default without discarding
  unrelated settings.
- **Native struct behavior can differ by architecture** → Retain the existing
  arm64 and x64 entry points and gate integration tests clearly on macOS.
- **File identities and paths are private metadata** → Keep the identity table
  in memory for the scan lifetime, never persist it, and never log file
  contents or identity-to-path mappings.

## Migration Plan

1. Add Core contracts and injected unit coverage while retaining logical and
   per-path allocated behavior.
2. Move the native allocated reader to Platform.Mac, return coherent identity
   metadata, and wire it through the App composition root.
3. Add hardlink-aware aggregation and result metadata, then update all derived
   consumers and cancellation/error tests.
4. Add the three-way App option, legacy settings migration, shared item
   presentation, and captured result options.
5. Add post-Trash refresh behavior and its success, failure, root-removal, and
   cancellation coverage.
6. Update documentation and the WP-02 status, then run build, tests, formatting,
   strict OpenSpec validation, and diff checks.

Rollback is a code-and-documentation revert. Newly saved named measurement
modes must fall back safely when read by a reverted build; no scan database or
user file migration exists. Trash operations remain recoverable through macOS
Trash throughout rollout and rollback.

## Open Questions

None. APFS clone support and benchmark thresholds remain decisions for their
named follow-up changes.
