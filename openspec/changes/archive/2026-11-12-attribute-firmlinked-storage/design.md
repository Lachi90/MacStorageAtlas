# Design

## Context

See `proposal.md` for motivation and `specs/storage-measurement/spec.md` for the
required behavior.

`DiskScanner` walks the scan root depth-first using
`Directory.EnumerateFileSystemEntries`. It skips symbolic links unless they are
followed, guards followed directory links with a path-based visited set, and,
in shared-aware allocated mode, counts each file identity at the first path it
reaches. Firmlinks are not symbolic links: `/Users` and
`/System/Volumes/Data/Users` report the same device and inode, and the
directory-attribute check treats both as ordinary directories. A scan of `/`
therefore traverses the Data volume twice.

macOS publishes its firmlink definition in `/usr/share/firmlinks`. Each line
holds an absolute user-facing path and, separated by a tab, the corresponding
path relative to the Data volume root, which is mounted at
`/System/Volumes/Data`. The file exists on every supported macOS release, on
Apple Silicon and Intel alike.

## Goals / Non-Goals

**Goals:**

- Decide omission once per scan, before traversal, from the running system's
  definition and current directory identities.
- Keep the per-entry cost of omission to one set lookup for directory entries,
  and zero when nothing is omitted.
- Keep all knowledge of firmlinks and the Data volume in
  `MacStorageAtlas.Platform.Mac`; Core sees only a list of verified equivalent
  path pairs.

**Non-Goals:**

- Persist omitted equivalents in scan-history snapshots or exports. History
  comparison already works on per-path rows, and the omission explanation is
  needed only while a result is displayed.
- Detect equivalence generically for arbitrary bind mounts, nullfs mounts, or
  network shares.

## Decisions

### Supply verified equivalent paths through a Core-owned contract

Core gains a small `EquivalentPath(VisiblePath, AlternatePath)` record and an
`IEquivalentPathProvider` abstraction in `MacStorageAtlas.Core.Scanning`.
`DiskScanner` accepts an optional provider through its constructors; without
one, it behaves exactly as today.

Alternatives considered:

- Hard-code `/System/Volumes/Data` in Core and skip it under `/`. Rejected:
  it omits Data-volume content that has no firmlinked equivalent and puts
  macOS layout knowledge in Core.
- Stop at volume boundaries like `du -x`. Rejected: `/` is the sealed system
  volume, so `/Users` and `/Applications` would be excluded entirely.
- Keep traversing both paths and re-attribute counted bytes to the visible path
  after the scan. Rejected: it still doubles traversal time, memory, and
  permission errors, and it does not fix logical-mode totals.

### Verify equivalence in Platform.Mac at the start of each scan

`MacFirmlinkEquivalentPathProvider` reads `/usr/share/firmlinks`, ignores blank
or malformed lines, and maps each entry to
`/System/Volumes/Data/<relative path>`. It returns a pair only when both paths
exist as directories and `stat` reports the same device and inode for both.
It reuses the existing architecture-specific `stat` entry points. Any read or
parse failure yields an empty list for that entry or for the whole file, never
an exception and never a scan error.

The scanner calls the provider once per scan on the scan's background thread,
because mounts and firmlink targets can change between scans. The roughly
twenty `stat` calls are negligible compared with a whole-disk traversal.

Alternatives considered:

- Read the definition once at app start. Rejected: a stale verification could
  omit a path that no longer resolves to the same directory.
- Trust the definition without an identity check. Rejected: an omission based
  on an unverified mapping could hide storage, which the specification forbids.

### Select pairs by scan scope and omit only alternates

At scan start, the scanner keeps a pair only when both its visible path and
its alternate path are equal to or below the normalized scan root, using the
existing platform path comparer. The alternates of the kept pairs form an
omission set. During traversal, a directory entry whose full path is in that
set is skipped before it is measured, added to the tree, or enumerated, and
the omission is recorded.

Because selection depends only on the scan root, a scan of `/System` still
omits `/System/Volumes/Data/System/Library/Caches` in favor of
`/System/Library/Caches`, while scans of `/Users`, a home folder, or
`/System/Volumes/Data` omit nothing.

Omission applies before measurement, so every measurement mode benefits and
shared-aware accounting never sees the alternate identities. Attribution to
the visible path no longer depends on enumeration order.

### Disclose omissions on progress and completed results

`ScanProgress` gains an `OmittedEquivalentPaths` list, defaulting to empty and
containing the pairs actually skipped so far. Partial results after
cancellation therefore disclose exactly what they skipped. The App shows a
short note in the scan status bar, next to the clone-accounting coverage
label, when the list is not empty, for example
"`/System/Volumes/Data/Users` is shown at `/Users`", with the full list in its
tooltip. The note reuses the existing caption style.

### Keep responsibilities within existing project boundaries

- Core: equivalent-path contract, scope selection, traversal omission,
  progress disclosure, and unit tests with a fake provider.
- Platform.Mac: firmlink definition parsing, Data-volume mapping, identity
  verification, and gated macOS integration tests.
- App: composition-root wiring in `App.axaml.cs` and the default
  `MainWindowViewModel` constructors, plus the summary note and its tests.
- Rendering: unchanged.

## Risks / Trade-offs

- [Apple changes the definition format or the Data-volume mount point] → The
  identity check fails, the provider returns no pairs, and scans fall back to
  today's behavior.
- [Totals for `/` change compared with older history snapshots] → Document the
  change in `docs/STORAGE_MEASUREMENT.md` and the release notes; snapshots are
  not rewritten.
- [A firmlink changes during a running scan] → The omission decided at start
  stays in effect for that scan; the next scan re-verifies. This matches how
  other scan-start decisions, such as the measurement mode, are captured.
- [Users browsing `/System/Volumes/Data` from a `/` scan miss expected
  folders] → The result summary names each omitted path and where it is shown.

## Migration Plan

No settings or stored data change. The new behavior applies to scans started
after the update. Rolling back means removing the provider from the composition
root, which restores the previous traversal without touching other code.

## Open Questions

- Whether the omission note should also appear in item details when the user
  selects `/System/Volumes/Data` can be decided during implementation without
  changing the specification.
