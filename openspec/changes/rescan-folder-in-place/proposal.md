## Why

Today the only way to refresh part of a result is to rescan the whole scan
root, or to start a new scan rooted at the folder, which throws away the rest of
the result. After cleaning up one project folder, extracting an archive, or
granting access to a single protected folder, users want only that folder
remeasured while the rest of the result stays as it is. On large roots, such as
a whole disk or a home folder, a full rescan takes minutes. A rescan of one
folder takes seconds.

## What Changes

- Add a "Rescan this folder" action for a directory in the current result. It
  remeasures only that folder and its descendants. The refreshed subtree
  replaces the stale one in place, and the rest of the result keeps its
  original measurements.
- The refreshed subtree uses the scan options and measurement mode of the
  displayed result, not the current toolbar options. One result therefore
  keeps one measurement basis.
- Ancestor totals, tree order, treemap, filters, Largest files, and File types
  update from the merged result. Recoverable errors, omitted equivalent paths,
  access guidance, and the completeness verdict are recomputed: entries inside
  the refreshed folder are replaced, and entries outside it are kept.
- The cleanup basket keeps items outside the folder. Items inside it are
  revalidated by path against the refreshed subtree. Items that are gone are
  removed with a notice. Duplicate-analysis results are cleared, because they
  describe the stale subtree.
- In shared-aware allocated mode, and while symbolic links are followed, the
  measurement of a folder depends on the paths visited before it. In those
  cases the app explains this and offers a full rescan of the scan root instead
  of a partial one.
- A cancelled or failed folder rescan leaves the displayed result unchanged.
- When scan history is enabled, a completed folder rescan records a new
  snapshot of the whole merged result. The snapshot states the original scan
  time and every refreshed folder with its refresh time (snapshot schema
  version 2). Version 1 snapshots remain readable.
- JSON and CSV exports of a refreshed result state the refreshed folders in
  their metadata (export schema version 2).
- Entry points: a "Rescan this folder" button on the Selected item tab, the
  Shift-Command-R shortcut, and the item context menu added by
  `add-item-context-menus`.

Non-goals:

- Do not detect filesystem changes automatically or watch folders (no FSEvents).
- Do not support partial rescans in shared-aware allocated mode or while
  symbolic links are followed. Those fall back to a full rescan.
- Do not merge or diff two snapshots. Snapshot comparison remains WP-09
  outstanding work.
- Do not change the existing toolbar Rescan, which still rescans the whole scan
  root with the current options.
- Do not rescan a single file. The action applies to directories, including
  unexpanded packages.
- Do not add a menu bar. Menu bar commands are a separate change.
- Do not scope Largest files or File types to the selection. That is
  `scope-insights-to-selected-folder`.

## Capabilities

### New Capabilities

- `folder-rescan`: Remeasuring one directory of a displayed result in place
  while the rest of the result keeps its measurement basis.

### Modified Capabilities

- `scan-history`: Snapshots of a result with refreshed folders state which
  folders were refreshed and when. Earlier snapshots remain readable.
- `result-export`: Exports of a result with refreshed folders state which
  folders were refreshed and when.

## Impact

Affected areas:

- `src/MacStorageAtlas.Core/Scanning`: A subtree scan entry point on
  `IDiskScanner`/`DiskScanner` that applies equivalent-path omission relative
  to the original scan root, plus a merge step that replaces a subtree and
  adjusts ancestor rollups.
- `src/MacStorageAtlas.Core/Items`: Subtree replacement on `DiskItem` with
  ancestor size adjustment and re-sorting.
- `src/MacStorageAtlas.Core/History` and `Export`: Refreshed-folder metadata,
  schema version 2 writers, and version 1 and 2 readers.
- `src/MacStorageAtlas.Core/Cleanup`: Rebinding basket items to the refreshed
  subtree.
- `src/MacStorageAtlas.App`: Command, progress, cancellation, merged-state
  application, history capture, Selected item button, and keyboard shortcut.
- Tests in Core and App, mirroring those areas.
- Documentation: `README.md`, `docs/FEATURES.md`,
  `docs/STORAGE_MEASUREMENT.md`, `docs/index.html`, and
  `docs/IMPLEMENTATION_ROADMAP.md`.

Dependencies:

- `add-item-context-menus` provides the context menu that this change extends
  with "Rescan this folder". The Selected item button and shortcut work without
  it, so implementation can start first and add the menu entry once the menu
  exists.
- Builds on the archived `persist-scan-history`, `export-scan-results`,
  `attribute-firmlinked-storage`, `harden-scan-traversal`, and
  `fix-cleanup-size-revalidation` behavior.

Risks:

- The merged result mixes measurement times. History, export, and UI must
  disclose this so that a merged result is never read as a single point-in-time
  scan.
- Equivalent-path omission and package expansion must behave exactly as in a
  full scan of the same root. Otherwise a folder rescan could double-count or
  hide firmlinked data.
- Mutating the displayed tree in place must not race the virtualized tree,
  treemap cache, filter evaluation, or a running history capture.
- Raising the history and export schema versions must not make older stored
  snapshots unreadable.

Roadmap reference:

- Extends the baseline "Rescan Selected Folder" feature (`docs/FEATURES.md`
  section 19). Primary WP: WP-09 (scan history), because the snapshot schema
  and capture semantics are the parts with the most lasting effect. It also
  touches WP-02 measurement rules and WP-05 export metadata.

Estimate:

- 4-6 days.
