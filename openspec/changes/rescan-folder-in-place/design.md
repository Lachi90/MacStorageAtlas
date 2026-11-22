## Context

`MainWindowViewModel.RunScanAsync` is the only path that produces a result. It
clears all result state, streams `ScanProgress` from `IDiskScanner.ScanAsync`,
and applies the completed root in `ApplyProgress`. Completion also starts the
optional history capture (`StartScanHistoryCaptureAsync`) and rebuilds the
cleanup planner, the protected-path policy, the treemap, and the filtered
insights.

`DiskScanner` keeps per-scan state that makes some measurements depend on
traversal order:

- `ScanState` deduplicates hardlink identities and verified clone data across
  the whole scan in `SharedAwareAllocated` mode.
- `visitedDirectories` prevents cycles across the whole scan when symbolic links
  are followed.
- `SelectOmittedAlternates(rootPath)` decides which firmlink-equivalent
  alternates to omit, relative to the scan root.

`DiskItem` has no parent pointer. Its aggregate sizes are set by the scanner,
and `RemoveDescendant` already adjusts ancestor rollups when an item is
removed. Trash in shared-aware mode already falls back to a full rescan,
because shared accounting cannot be adjusted locally.

## Goals / Non-Goals

Goals:

- Remeasure one directory and splice the result into the displayed tree, with
  traversal rules identical to a full scan of the same root.
- Keep one measurement basis per result and disclose mixed measurement times.
- Keep history, export, errors, completeness, basket, and dependent views
  consistent with the merged tree.
- Keep folder rescans cancellable and streaming, and keep the result unchanged
  until a folder rescan completes.

Non-Goals:

- Incremental accounting for shared-aware mode or followed symbolic links.
- Filesystem watching.
- Snapshot comparison.

## Decisions

### Core: a subtree scan that reuses the full-scan traversal

Add `IDiskScanner.ScanSubtreeAsync(string scanRootPath, string subtreePath,
ScanOptions options, CancellationToken)`. It returns the same
`IAsyncEnumerable<ScanProgress>`. `Root` is the new subtree item, and `Errors`
and `OmittedEquivalentPaths` contain only entries inside the subtree. The
interface provides a default implementation that throws
`NotSupportedException`, so scanners that cannot rescan a folder, such as the
synthetic benchmark scanner and test doubles, need no change.

When the subtree path no longer refers to a directory that a full scan would
include (it is gone, is now a file, is now hidden or an unfollowed symbolic
link, or cannot be read), the stream completes with one progress whose new
`ScanProgress.IsRootPresent` is `false`. A read failure is reported as a
recoverable error at the subtree path, exactly as a full scan would report it.

Internally it builds `ScanState` with `SelectOmittedAlternates(scanRootPath)`,
so omission is decided against the original root, and then runs the existing
`ScanDirectoryAsync` from the subtree. Package expansion for the subtree item
itself follows the same `PrepareEntry` decision that a full scan would make for
that path. The method throws `ArgumentException` when the subtree is not inside
the scan root, or when a full scan of the root would not include it: inside an
omitted firmlink alternate, inside an unexpanded package, inside an excluded
hidden folder, or below a symbolic link. A subtree equal to the scan root runs
the full scan. Validation happens when the method is called, before
enumeration starts. It throws `NotSupportedException` for `SharedAwareAllocated` or
`FollowSymbolicLinks`, so that Core enforces the fallback rule rather than only
the UI.

Alternative considered: seeding a fresh `ScanState` with the hardlink, clone,
and directory identities observed outside the subtree. The current tree does
not retain those identities. Storing them would grow memory for every scan, and
the first-visited attribution would still differ from a full scan. Rejected in
favour of the honest fallback that the Trash flow already uses.

### Core: splicing with ancestor rollup adjustment

Add `ScanResultMerger.ReplaceSubtree(DiskItem root, string subtreePath, DiskItem?
replacement)` in `Core/Scanning`. `ScanResultMerger.FindByPath` walks from the
root by path prefix to the stale item, and a new internal
`DiskItem.ReplaceDescendant(DiskItem descendant, DiskItem? replacement)` in
`Core/Items` swaps or removes it by reference, so `Core/Items` stays free of
scanning path rules. It adds the size deltas (`SizeBytes`, `MeasuredSizeBytes`,
`SharedSizeBytes`) to every ancestor and stably reorders each touched
ancestor's children by counted size, the order `DiskItemSorter` produces. `replacement is null` means
the folder no longer exists. Ancestor identities are preserved, so tree node
ViewModels outside the subtree stay valid.

The merger also merges errors and omitted equivalent paths. It drops entries
whose path is the subtree or lies inside it, then appends the new ones. It
keeps a `RefreshedFolders` list of `(Path, CompletedAt)`. A new entry removes
any entries for descendants of its path, and a full scan starts with an empty
list.

Path comparison for the subtree scan, the merger, and provenance uses one
internal `ScanPath` helper shared with `DiskScanner`, so all of them apply the
same case rules as equivalent-path omission.

### Result provenance as a value

Introduce `ScanResultProvenance(DateTimeOffset ScanCompletedAt,
IReadOnlyList<RefreshedFolder> RefreshedFolders)` in `Core/Scanning`. The App
holds it next to `_scanRoot` and `_resultScanOptions`. History and export read
it from the App rather than inferring it from the tree.

### History schema version 2

Raise `ScanSnapshotSchema.CurrentVersion` to 2 and add `RefreshedFolders` to
`ScanSnapshotMetadata`. `IsSupported` accepts versions 1 and 2. The reader maps
a version 1 snapshot to an empty list. `ScanCompletedAt` keeps its meaning (the
original full scan) and `CapturedAt` is the capture time. Because a refreshed
snapshot shares `ScanCompletedAt` with the snapshot of its original scan,
retention and the history list break ties by `CapturedAt`, so the newer
refreshed snapshot is kept and listed first. The history list shows
a "Contains rescanned folders" marker. Retention, size limits, and atomic writes
are unchanged, because the snapshot is still one whole-root document.

Alternative considered: a snapshot rooted at the rescanned folder. It fits
schema version 1, but it adds a new root to the history each time and breaks
per-root comparability for WP-09. Rejected, and the user chose the whole-root
option.

### Export schema version 2

Raise `ScanExportMetadata.CurrentSchemaVersion` to 2 and add the same
provenance to the JSON writer. `ScanExportRequestFactory.Create` takes the
refreshed folders as an optional trailing parameter. `ScanResultJsonReader` accepts versions 1 and 2.
CSV has no metadata block, so its completion message carries the disclosure.

### App orchestration

`RescanFolderCommand` targets the selected directory from the tree or treemap
and resolves it by path against `_scanRoot`. It runs through a new
`RunFolderRescanAsync`:

1. It refuses when the result's options are shared-aware or follow symbolic
   links. Instead it asks through the existing confirmation-service pattern
   (`IFolderRescanConfirmationService`) for a full rescan with
   `_resultScanOptions`. The command is also unavailable while an export, a
   Trash move, or a cleanup basket operation runs, because each of them reads
   or mutates the displayed tree. Move to Trash and basket operations are in
   turn disabled while any scan, including a folder rescan, runs.
2. It cancels a running duplicate analysis and history capture, sets
   `IsScanning`, and shows progress. It does not clear `TreeItems`,
   `TreemapRectangles`, or the basket, so the result stays browsable while
   mutating commands are disabled through the existing `IsScanning` gates. The
   full-scan overlay binds to `ShowScanOverlay`, which is false during a folder
   rescan, and a non-modal banner shows the folder rescan progress with Stop.
3. It consumes `ScanSubtreeAsync` off the UI thread. Before merging it cancels
   a pending tree preparation and waits for the cancelled tree preparation,
   history capture, and duplicate analysis tasks to finish, so no background
   reader enumerates the tree while it is mutated. It then marshals
   the merge to the UI dispatcher in one step: splice, merge errors and
   omissions, update provenance, rebuild `CleanupProtectedPathPolicy`, rebind
   basket items by path (`CleanupBasketPlanner.Rebind(root, subtreePath)`),
   clear duplicates, clear the treemap cache, reapply the filter, recompute
   access guidance with `CheckFullDiskAccess(scanRoot)`, adjust the file,
   folder, and byte counters by the difference between the stale and refreshed
   subtrees, then restore the selection and expansion by path. Basket entries
   that contain the rescanned folder keep their item and take its refreshed
   total. When the folder no longer exists, its parent is selected.
4. On cancellation or failure it restores `IsScanning` and leaves everything
   else untouched.
5. When history is enabled it starts `StartScanHistoryCaptureAsync` with the
   merged root and provenance.

The Selected item tab gets a "Rescan this folder" button. `MainWindow` handles
Shift-Command-R next to the existing Command-I and Command-F handling. The
context menu entry goes into the `FolderActionsGroup` that
`add-item-context-menus` provides.

### Sandboxed builds

In the App Store build, the user-selected scan root grants read access to its
descendants for the app session, so a folder inside the root is readable
without a new selection. When access was lost, the rescan reports recoverable
errors as usual, and the existing sandbox access guidance applies.

## Risks / Trade-offs

- [Mixed measurement times are misread as one point-in-time scan] → UI summary,
  history marker, export metadata, and CSV message disclose them.
  `ScanCompletedAt` keeps its original meaning.
- [Splicing races the virtualized tree or a filter evaluation] → The merge is
  applied in a single UI-dispatcher step after the subtree is complete, and
  filter work is restarted after it.
- [Omission differs from a full scan] → Core tests compare a subtree rescan
  against the corresponding subtree of a fresh full scan for hidden files,
  packages, and firmlink alternates.
- [Schema bump breaks old data] → Readers accept versions 1 and 2. Tests read
  stored version 1 fixtures.

## Migration Plan

No user data migration. Version 1 snapshots and exports stay readable. Rollback
to a build without this change would report version 2 snapshots as unreadable
versions through the existing requirement, which is the documented behaviour.

## Open Questions

- None blocking. Whether the toolbar Rescan should be relabelled "Rescan all"
  is deferred to the menu bar and toolbar change.
