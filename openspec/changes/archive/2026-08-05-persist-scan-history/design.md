## Context

MacStorageAtlas keeps a completed scan only in memory. `MainWindowViewModel`
holds the result tree in `_scanRoot`, the options in `_resultScanOptions`, and
the completion instant in `ScanCompletedAt`; all of it is discarded when the
app closes or the next scan starts. Nothing on disk records what storage looked
like at a point in time.

The `result-export` capability already solves most of the serialization problem
for user-directed exports. `ScanExportRowSource.EnumerateFull` walks the result
tree lazily in a deterministic order, `ScanExportRow` carries every per-item
field a snapshot needs, `ScanResultJsonWriter` streams a document without
materializing it, `ScanResultJsonReader` round-trips it exactly, and
`MainWindowViewModel` already writes to a temporary path and publishes only on
success. This change reuses that machinery rather than inventing a parallel
one, but it does not reuse the export *format*, for reasons set out below.

Two existing capabilities feed the snapshot's trustworthiness.
`storage-measurement` establishes that one scan produces one measurement basis,
so a snapshot must carry its basis or its numbers are uninterpretable.
`scan-access-guidance` classifies whether a scan was blocked by missing Full
Disk Access, which is the difference between "this folder is empty" and "I was
not allowed to look".

Constraints that shape everything here: Core must not depend on Avalonia;
filesystem work stays off the UI thread; cancellation propagates; no new
package dependencies; scanned paths are private user data.

## Goals / Non-Goals

**Goals:**

- Define a stored snapshot format that is versioned, self-describing, and rich
  enough that a later comparison feature never needs a rescan or a format
  change.
- Bound the store's size and make its cost visible, so a storage analyzer never
  becomes a storage problem.
- Make persistence opt-in, discoverable, and reversible, because this is the
  first feature that writes scanned paths to a location the user did not pick.
- Ensure a snapshot can never be read in a way that invents a change that did
  not happen.
- Reuse the export pipeline's row enumeration and write discipline rather than
  duplicating them.

**Non-Goals:**

- Comparing snapshots or computing deltas. That is the follow-up change; this
  design only guarantees the data it will need is present.
- Plumbing `FileIdentity` onto `DiskItem` for move detection.
- Any summarized, truncated, or rolled-up snapshot variant.
- Migration of existing data. There is no existing history to migrate.

## Decisions

### Snapshot format is a gzip-compressed JSON document, not a summary

A full-fidelity snapshot of a 500k-item home directory is roughly 150 MB of raw
JSON. Compressed it is roughly 12 MB, because file paths share long prefixes
and JSON property names repeat on every row — exactly the redundancy DEFLATE
removes. `System.IO.Compression.GZipStream` at `CompressionLevel.Optimal`
delivers that from the base class library with no new dependency, and it
composes with the existing streaming writer: rows are written into the
compressor as they are enumerated, so neither the document nor the compressed
bytes are ever fully in memory.

Alternatives considered:

- **Directory rows plus top-N files per directory.** Roughly 40× smaller, but a
  file only appears in a comparison if it was in the top N at *both* ends,
  which makes "what grew" quietly unreliable in precisely the long-tail cases
  (caches, node_modules, build output) that motivate the feature.
- **All directories plus files above a size threshold.** Similar savings and a
  cleaner rule, but a file crossing the threshold between two scans is
  indistinguishable from a newly created file, so growth gets reported as
  creation.
- **Depth cap.** Cheapest, but the interesting growth lives deep
  (`~/Library/Caches/<app>/<hash>/…`), so it truncates exactly the wrong part
  of the tree.

Full fidelity was chosen because every lossy option converts a storage saving
into a correctness liability in the feature that consumes it, and gzip makes
the saving unnecessary.

### A snapshot is written whole or refused, never truncated

Because the store is bounded, capture must handle a scan whose snapshot does
not fit. Truncating to fit would make an omitted item indistinguishable from a
deleted one, so a later comparison would report deletions that never happened.
Capture therefore writes to a pending temporary file inside the store, measures
the finished compressed size, and only then applies retention: prune the oldest
snapshots if that brings the store within its limits and publish by moving the
pending file into place; otherwise delete the pending file and report that the
scan was too large to record. Measuring after writing rather than estimating
before is what makes this exact — the compressed size of a scan is not
predictable from its item count.

### History uses its own schema version, separate from the export schema

`ScanExportMetadata.CurrentSchemaVersion` is a documented contract with
consumers of user-directed exports. Scan history needs fields the export does
not have (a snapshot identity, a capture instant, a completeness verdict) and
will need more as comparison lands. Sharing one version number would force
export consumers to react to history-only changes and vice versa.

Decision: a distinct `ScanSnapshotDocument` / `ScanSnapshotMetadata` pair with
`ScanSnapshotSchema.CurrentVersion`, reusing `ScanExportRow` as the per-item
shape and `ScanExportRowSource.EnumerateFull` as the row source. The per-row
JSON serialization currently inside `ScanResultJsonWriter` is extracted into a
shared internal helper so the two writers cannot drift in how a row is encoded.

Alternative considered: reuse `ScanResultJsonWriter` verbatim and store export
documents as snapshots. Rejected for the coupling above, and because an export
document records an export *scope* and *filter*, concepts a snapshot must not
have (a snapshot is always the full result).

### The document orders metadata before items so listing stays cheap

The history list shows completion time, item count, stored size, measurement
mode, and completeness for every snapshot. Inflating a 12 MB body to read five
fields would make the list unusable. The document therefore writes
`schemaVersion`, then `scan` (root, completion time, options, measurement mode,
clone-accounting coverage, item count, total counted bytes, error count,
completeness), then `errors`, then `items`. A listing read inflates only the
leading bytes with a streaming reader and stops once `scan` closes; stored size
comes from the directory entry without reading the file at all.

Alternative considered: an uncompressed sidecar metadata file per snapshot, or
a central index file. Both add a second source of truth that can diverge from
the payload — a central index in particular turns one corrupt file into a
broken store, which the spec forbids. A single self-describing file per
snapshot means corruption is always scoped to one snapshot.

### Store layout: one flat directory, one file per snapshot, grouped in memory

The store lives at `~/Library/Application Support/MacStorageAtlas/history/`,
alongside the existing `settings.json`. Each snapshot is one file named from
its sortable completion timestamp plus a short random suffix; pending captures
use a `.pending` extension in the same directory and are swept on startup and
before each capture, which is how an interrupted capture cleans up after
itself.

Grouping by scan root happens in memory from the metadata each file already
carries. A per-root subdirectory was considered and rejected: the directory
name would have to encode the root path, which would publish a list of scanned
locations into a directory listing, or a hash of it, which would make the store
harder to reason about for no benefit at the tens-of-files scale retention
enforces.

### Completeness is classified in App, represented in Core

A snapshot must state whether its scan could read everything. That judgement
already exists as `AccessGuidanceClassifier` in
`MacStorageAtlas.App.ViewModels`, which combines the scan's recoverable errors
with a `FullDiskAccessAssessment`. Core must not depend on App, so Core defines
the vocabulary — a `ScanCompleteness` enum distinguishing a scan that read
everything, one that hit recoverable errors, one that was blocked by access
restrictions, and one whose completeness could not be determined — and App maps
its existing `AccessGuidanceStatus` onto it at capture time. Core owns the
representation and the guarantee; App owns the classification it already
performs.

### Store abstraction sits in Core, its location is supplied by App

Core gets `IScanHistoryStore` and a `FileSystemScanHistoryStore` that takes its
root directory as a constructor argument, mirroring how `JsonSettingsService`
takes its settings path. App resolves the Application Support location and
composes the store in the composition root. This keeps every store test on an
isolated temporary directory and keeps path resolution out of the domain.

Retention is a separate pure component, `ScanHistoryRetentionPolicy`, that
takes the existing snapshot descriptors, the incoming snapshot's size, and the
limits, and returns which snapshots to prune or a refusal. Keeping it free of
I/O makes the pruning rules — oldest first, prune no more than required, refuse
rather than truncate — directly testable.

### Capture runs after the result is displayed, and a new scan cancels it

`ApplyProgress` marshals completion onto the UI thread and must stay fast, so
it does not capture. The scan orchestration path starts capture as a background
operation once the result is displayed, holding its own
`CancellationTokenSource` in the view model. Starting a new scan cancels it;
the pending file is deleted and nothing is published. Capture failure surfaces
as a status message and never disturbs the displayed result, which is already
complete and usable by the time capture begins.

### History settings extend AppSettings; the entry point avoids the toolbar

`AppSettings` gains `ScanHistoryEnabled` (default false), a per-root snapshot
count limit (default 10), and a total store size limit (default 500 MB).
`JsonSettingsService` already tolerates absent and unknown fields, so older
settings files load unchanged and the feature stays off for existing users.

The history entry point goes in the existing scan-options surface rather than
becoming a new top-level toolbar group. The `main-toolbar-layout` capability
requires the primary command surface to stay a single row with recognizable
groups; adding a sixth top-level group would put pressure on that requirement
at narrow widths. Placing history with the other configuration surfaces keeps
that capability's requirements satisfied without modification, which is why
this change declares no modified capabilities.

### Manual removal is a supported route, so the store points at itself

The store is a flat directory of independent files with no index precisely so
that deleting any subset of them by hand leaves the rest usable. That route is
only real if the user can reach it: `~/Library` is hidden in Finder by default,
so printing the path as text serves people who already know to press
Command-Shift-G and nobody else. The history surface therefore reveals the
store through the existing `IFileRevealService`, which runs `open -R` and is
already used for revealing scanned items — no new abstraction and no new
platform code.

`MacFileRevealService.Reveal` returns false for a path that does not exist, and
the store directory is not created until the first capture. Rather than
creating a directory for a user who may never complete a scan, the reveal
action is unavailable while nothing is stored; the empty state already explains
that no scans have been recorded.

Tolerating external change follows from the same decision. Listing skips a file
that disappears while the listing runs rather than reporting it as corrupt,
because a snapshot the user just deleted is not a damaged snapshot, and capture
recreates the store directory, so removing the whole store leaves the feature
working rather than broken.

### Store permissions and indexing

The store directory is created with owner-only access and each published
snapshot is set to owner-only via `UnixFileMode`, so another account on the
same machine cannot read a user's filename index. A `.metadata_never_index`
marker is placed in the store directory so Spotlight does not make the history
itself searchable — the store would otherwise turn a private record into a
system-wide search corpus.

## Risks / Trade-offs

- **A 500 MB default store on a machine the user is trying to free space on** →
  Capture is opt-in, the store's total size is shown wherever history is
  listed, both limits are user-adjustable, and lowering a limit prunes
  immediately.
- **Compressed size is unknown until the snapshot is written, so a doomed
  capture still costs the write** → Accepted. The write is off the UI thread
  and cancellable, and the alternative (estimating from item count) would
  either refuse captures that would have fit or admit ones that do not.
- **gzip makes a snapshot opaque to command-line inspection** → Accepted; the
  format is plain JSON under one standard compression layer, readable with
  `gunzip`. Documentation states this.
- **A durable index of every filename in the user's home directory** →
  Off by default, owner-only permissions, excluded from Spotlight indexing,
  location stated in the UI and the docs, deletable per snapshot or in whole.
  Time Machine will still back the store up; the documentation says so rather
  than pretending otherwise.
- **Extracting shared row serialization touches the shipped export writer** →
  The extraction is behavior-preserving and the existing export round-trip and
  determinism tests guard it; they must pass unchanged.
- **Path-only matching defers a real limitation to the comparison change** →
  Accepted deliberately. `FileIdentity` exists today only in shared-aware
  allocated mode (`DiskScanner` tracks it solely for deduplication and discards
  it), so recording it would make any identity-based behavior silently
  measurement-mode-dependent. The snapshot format's versioning is what makes
  adding it later a schema bump rather than a redesign.
- **Snapshots outlive the code that wrote them** → Explicit schema version, and
  an unknown version is reported with its number rather than parsed
  optimistically.

## Migration Plan

There is no existing history to migrate. The feature ships disabled; an
existing `settings.json` without the new fields loads unchanged and no store is
created until the user opts in.

Rollback is removing the store directory: no other feature reads it, and
clearing history is specified not to touch scan options, presets, or recent
locations. If the change is reverted in code, a store left on disk is inert
data that nothing opens.

## Open Questions

- Whether the default per-root limit of 10 snapshots and total limit of 500 MB
  are right. They are guesses until real snapshots exist, which is part of why
  persistence ships before comparison.
- Whether capture should be suppressed for very short scans of small roots,
  where a snapshot per scan produces near-duplicate entries. Deferred until the
  history list shows whether this is actually noisy.
- Whether an incomplete scan should be capturable at all, or only recorded with
  its verdict as specified here. Recording it with the verdict is the current
  decision, because refusing would leave a user without Full Disk Access unable
  to use the feature at all.
