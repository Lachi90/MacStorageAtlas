## Context

The archived `define-storage-measurement` and `deduplicate-hardlinks` changes
established three measurement modes, made hardlink-aware allocated measurement
the default, separated per-path measured allocation from counted contribution,
and moved coherent allocated metadata reads into Platform.Mac. The current
macOS reader uses one `stat(2)` call to return `st_blocks × 512`, device, inode,
and link count. Core tracks device-and-inode identities for the scan lifetime
and assigns the entire allocation to the first included path.

Distinct APFS clone identities can share data blocks, so the default mode still
overstates common copy-on-write copies. A time-boxed spike on arm64 macOS 26.5.2
confirmed that public extended attributes expose full-clone identity, reference
count, private allocation, and sharing flags on a capable APFS volume. The same
spike showed that a small write gives the divergent file a new clone identity
while most physical extents remain shared. Physical extent enumeration can
observe that overlap, but it requires opening files for reading, scales with
fragmentation, does not cover every allocation represented by the current
per-path total, and would weaken cloud-placeholder and permission behavior.

The application supports macOS 11 and later on Apple Silicon and Intel.
Full-clone mapping must therefore be runtime- and volume-capability based.
Scanning must remain streaming, cancellable, metadata-only, additive, and
honest when optional clone metadata is unavailable.

## Goals / Non-Goals

**Goals:**

- Count verified fully shared APFS data allocation once within the active scan
  scope while retaining hardlink correctness.
- Keep non-data allocation counted once per filesystem identity unless a future
  public capability proves that allocation is also shared.
- Fail closed by counting bytes normally when clone capability or metadata is
  unavailable, incomplete, or inconsistent.
- Preserve every included path, its measured allocation, its counted
  contribution, and the number of bytes represented elsewhere.
- Capture accounting coverage with progress and completed results.
- Keep APFS interpretation in Platform.Mac and portable accounting policy in
  Core.
- Preserve existing privacy, cloud-placeholder, cancellation, package,
  symbolic-link, error, and post-Trash behavior.

**Non-Goals:**

- Enumerate or globally deduplicate physical extents.
- Deduplicate divergent or partially shared clone data.
- Treat clone reference count or private size as an allocation divisor.
- Infer sharing from file contents or metadata resemblance.
- Report unique physical, reclaimable, volume-used, or purgeable storage.
- Add traversal parallelism, bulk directory enumeration, or benchmark tooling.
- Persist clone identities or scan results.

## Decisions

### Rename the user-facing default to shared-aware allocated size

The third measurement mode will be called shared-aware allocated size in
product copy and canonical documentation. It will count hardlinked identities
once everywhere and verified fully shared data once where supported. Logical
and per-path allocated modes remain unchanged and selectable.

The internal enum will be renamed to a clear shared-aware value. Settings
loading will accept the existing `HardlinkAwareAllocated` stored name and map it
to the renamed value; saving will write the new name. Existing legacy
`MeasureAllocatedSize` migration remains intact. Each result will retain both
the selected measurement mode and the accounting coverage observed during that
scan.

Alternative considered: retain the hardlink-aware label. That would conceal the
new behavior and leave the canonical term narrower than the bytes actually
counted.

Alternative considered: add a fourth clone-aware mode. Full-clone accounting is
strictly more accurate when it is verified and fails closed otherwise, so
asking users to select the less accurate default adds complexity without a
useful measurement basis.

### Represent optional verified shared-data metadata in a Core-owned contract

Core's allocated metadata contract will retain total allocated bytes,
filesystem identity, and link count and add data-fork allocated bytes plus an
optional opaque shared-data identity. The shared-data identity will combine a
volume identity with the platform-provided clone identity so equal clone
numbers on different volumes cannot collide. Platform.Mac will supply the
identity only when the volume advertises full-clone mapping and the file reports
metadata that verifies all data blocks are shared with at least one clone.

Platform.Mac will query and cache volume capability by stable mounted-volume
identity for the scan process. On capable volumes it will use a coherent
`getattrlist(2)` metadata read for total allocation, data allocation, device,
file identifier, link count, clone identifier, clone reference count, returned
attributes, and extended flags. Unsupported volumes and operating systems will
continue through a metadata path that provides the existing hardlink contract.
Capability or optional clone-attribute failure will omit shared-data identity
rather than fail allocated measurement. Failure to read required total
allocation or filesystem identity will remain a recoverable scan error.

Alternative considered: expose APFS clone identifiers directly from Core.
That would reverse the intended dependency boundary and make portable scan
logic responsible for macOS capability semantics.

Alternative considered: use Foundation URL resource values. The native
attribute call exposes the complete capability, allocation, identity, and clone
state needed for one coherent contract without adding an Objective-C bridge.

### Deduplicate data allocation only after filesystem identity accounting

Scan state will maintain a set of counted filesystem identities and a
dictionary from shared-data identity to its verified data allocation. For each
successfully measured path:

1. A repeated filesystem identity contributes zero bytes because all of its
   allocation was already represented by an included hardlink or followed
   symbolic-link path.
2. The first occurrence of a filesystem identity validates that data allocation
   is between zero and total allocation.
3. A path without verified shared-data identity contributes its full total
   allocation.
4. The first included filesystem identity in a verified clone group contributes
   its full total allocation.
5. A later filesystem identity with the same verified clone identity and the
   same data allocation contributes only `total allocation - data allocation`.
6. An inconsistent clone group is invalidated and the current and later
   identities fail closed by contributing their full totals.

The item stores measured total, counted contribution, and shared bytes derived
as their non-negative difference. Directory totals, progress, treemaps,
file-type statistics, and largest-file ranking continue to consume counted
contribution and remain additive. The first included group member remains the
representative, preserving streaming without retroactive ancestor changes.

Clone reference count classifies a potential full-clone group but never divides
bytes or suppresses the first included contribution. Clones outside scan scope
therefore do not reduce the included total. Non-data allocation remains counted
for each distinct filesystem identity because clone identity describes the data
stream and the spike demonstrated that resource-fork allocation can diverge
without changing that identity.

Alternative considered: deduplicate each path's entire allocated total by clone
identity. That can undercount independently allocated resource forks or other
non-data storage.

Alternative considered: add private allocation to one shared group total.
Private size is deletion- and snapshot-sensitive, is not additive, and does not
identify the shared base that must be counted once.

### Track capability coverage as an accumulating result property

Progress and completed results will carry clone-accounting coverage derived
from the volumes and entries observed so far. The model will distinguish:

- full-clone accounting available for all relevant observed allocated entries;
- unavailable because no observed volume exposes supported clone mapping; and
- partial because capable and incapable volumes are mixed or optional metadata
  was unavailable or inconsistent.

The App will present the completed result's captured coverage rather than the
current machine preference. Shared-aware labels will state that hardlinks are
always counted once, verified full clones are counted once only where coverage
permits, and divergent clones may still be counted more than once.

Alternative considered: expose a single boolean. A boolean cannot distinguish a
machine that does not support clone mapping from a mixed scan that partially
used it, and it cannot disclose degraded metadata safely.

### Keep item presentation quantitative

The existing boolean shared-storage indication will be replaced or
supplemented by shared byte count. A full repeated hardlink has shared bytes
equal to its measured total, while a full data clone with distinct non-data
allocation can have both a positive contribution and positive shared bytes.
Tree rows and details will retain measured allocation and explain the counted
contribution. Rendering continues to consume only `SizeBytes` and does not learn
clone policy.

Alternative considered: mark every later clone as a zero-byte contribution.
That would discard independently allocated non-data bytes and make the
aggregate undercount.

### Retain refresh-after-Trash behavior

A successful Trash operation against a shared-aware result will continue to
clear stale completion state and rescan with the captured options. Removing a
representative full clone can transfer its data contribution to another path,
and a resource-fork or partial-clone relationship can change concurrently. A
failed or cancelled Trash operation will leave the existing result unchanged.

Alternative considered: transfer clone contributions in memory. Collapsed
packages, partial non-data contributions, and concurrent filesystem changes
make that result unreliable.

### Reject physical extent enumeration for product accounting

The implementation will not call `F_LOG2PHYS_EXT`, open file data forks for
clone inspection, or retain physical extent intervals. Although the spike
confirmed that the API can reveal shared data extents in a controlled fixture,
it requires read access, can interact poorly with dataless files, produces
mutable point-in-time mappings, scales with extent count, and does not map
cleanly to every byte in the existing all-forks allocation.

Alternative considered: introduce an exact unique-allocated mode backed by
physical addresses. The result would not satisfy the existing metadata-only,
bounded-memory, all-included-file contract and could falsely imply
snapshot-grade consistency.

### Keep responsibilities within existing project boundaries

- **Core:** owns shared-aware vocabulary, optional opaque shared-data identity,
  scan-local deduplication, coverage aggregation, measured/counted/shared byte
  state, errors, cancellation, and settings-independent result semantics.
- **Rendering:** continues to consume additive counted sizes only.
- **Platform.Mac:** owns volume capability checks, native attribute layouts,
  architecture and OS fallback, and conversion to Core metadata.
- **App:** owns settings migration, labels, coverage disclosure, item details,
  result-option capture, and post-Trash refresh presentation.
- **Tests:** owns injected accounting cases, App presentation and migration,
  derived-view behavior, failure and cancellation coverage, and gated
  disposable macOS clone fixtures.

## Risks / Trade-offs

- **Optional metadata can disappear or vary by volume** → Fail closed per
  identity, aggregate partial coverage, and never downgrade required allocated
  metadata failures silently.
- **Clone dictionaries add scan-local memory** → Allocate them only in
  shared-aware mode, store compact value identities rather than paths, and
  release them with scan state.
- **Traversal order selects the representative path** → Keep the complete total
  invariant, expose shared bytes, and avoid sorting or retroactive transfer.
- **Native buffer packing differs from `stat` integration** → Use fixed-width
  fields, validate returned attribute sets and buffer sizes, and gate
  integration tests on both architectures where available.
- **A scan crosses capable and incapable mounts** → Key identities by volume,
  preserve hardlink correctness independently, and report partial coverage.
- **A file changes between entries** → Read required allocation, identity, and
  clone metadata coherently for each path, fail inconsistent groups closed, and
  retain the existing non-snapshot disclaimer.
- **Public copy becomes too absolute** → Use verified-full-clone language and
  continue stating that partial clone extents and reclaimable bytes are not
  deduplicated.
- **Cloud placeholders materialize during inspection** → Use metadata APIs
  only, never open file contents or enumerate physical extents, and retain
  dedicated placeholder tests where practical.

## Migration Plan

1. Add Core contracts, accounting coverage, quantitative shared-byte state, and
   injected tests while retaining the current macOS reader.
2. Add Platform.Mac capability probing and coherent extended metadata with
   unsupported and degraded fallback tests.
3. Enable verified full-clone data accounting and update derived views,
   cancellation, package, symbolic-link, and error tests.
4. Rename the user-facing mode, migrate the stored mode name, add coverage and
   shared-byte presentation, and verify post-Trash refresh.
5. Add gated disposable APFS integration fixtures and update storage,
   comparison, feature, roadmap, and landing-page documentation.
6. Run solution build, tests, analyzer formatting, strict OpenSpec validation,
   and diff checks.

Rollback is a code-and-documentation revert. Settings loading in the preceding
release will safely fall back if it encounters the new stored mode name, and
the new loader accepts both names. No persistent scan data or filesystem
mutation is introduced.

## Open Questions

None. The technical spike resolved the API boundary: use capability-gated
full-clone metadata and reject physical extent enumeration for this change.
