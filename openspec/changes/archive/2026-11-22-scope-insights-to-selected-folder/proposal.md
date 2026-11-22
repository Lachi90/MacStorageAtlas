## Why

The Largest files and File types tabs always describe the whole scan, no matter
which folder the user is looking at in the folder tree or the treemap. After a
whole-disk or home-folder scan, the user who drills into `~/Library` or a
project folder still sees the largest files and file types of the entire
result, so answering "what is big in this folder" requires a separate scan of
that folder or manual browsing. The insight tabs should follow the folder the
user is examining.

## What Changes

- The Largest files list and the File types summary describe the files inside
  the insight scope: the directory most recently selected in the folder tree or
  the treemap, including all of its descendants.
- Selecting a file in the folder tree or the treemap scopes the tabs to the
  directory that contains that file.
- Selecting an entry in the Largest files list or the Duplicates list does not
  change the insight scope, so a scoped list never collapses onto the entry the
  user just picked from it.
- Without any directory selection, the insight scope is the scan root, which
  matches today's behavior.
- Both tabs state the current scope as text, for example "in “Downloads”", and
  offer a one-click action that returns them to the whole scan.
- An active result filter still applies inside the scope: the tabs describe the
  files that both match the filter and lie inside the scope.
- Recomputing the tabs for a new scope stays off the UI thread, is cancellable,
  never revisits the filesystem, and keeps the scan's measurement mode and
  labels.
- When the scope directory leaves the result, for example because it was moved
  to Trash, the scope falls back to its nearest remaining ancestor directory,
  or to the scan root.

Non-goals:

- Do not scope CSV or JSON export, scan history snapshots, duplicate analysis,
  the cleanup basket, the match count and matched-size summary of the filter
  panel, or the folder tree itself.
- Do not change the treemap layout, its drill-down behavior, or which level it
  displays.
- Do not change the ranking rules, the 100-entry limit of the Largest files
  list, or how file types are grouped.
- Do not add multi-folder scopes, pinned scopes, or persisted scopes across app
  launches.
- Do not implement rescanning a folder in place; the planned
  `rescan-folder-in-place` change owns that and must re-resolve the insight
  scope as described in the design.

## Capabilities

### New Capabilities

- `insight-scope`: Which part of a completed scan result the Largest files and
  File types tabs describe, how the user sees and resets that scope, and how
  the scope survives result changes.

### Modified Capabilities

- `result-filtering`: The requirement that filtered results are presented
  consistently across result views now states that the largest-files list and
  the file-type summary describe matched files inside the insight scope.

## Impact

Affected areas:

- `src/MacStorageAtlas.Core/Insights`: cancellation-aware, scope-and-predicate
  aware overloads of `LargeFilesService` and `FileTypeStatisticsService` that
  walk only the scope subtree.
- `src/MacStorageAtlas.App/ViewModels/MainWindowViewModel.cs`: an insight-scope
  state independent of `SelectedItem`, a separate cancellable insight
  preparation, scope labels, a reset command, and scope re-resolution after
  removals.
- `src/MacStorageAtlas.App/Views/MainWindow.axaml`: scope label and reset
  action on the File types and Largest files tabs.
- `tests/MacStorageAtlas.Core.Tests/Insights` and
  `tests/MacStorageAtlas.App.Tests`: scope, filter intersection, cancellation,
  and scope-transition tests.
- Documentation review: `README.md`, `docs/FEATURES.md`, and `docs/index.html`.

Dependencies:

- Builds on the archived `add-advanced-filters` and `virtualize-result-tree`
  changes for filter results and off-thread result preparation.
- No dependency on unarchived changes. The planned `rescan-folder-in-place`
  change depends on the scope re-resolution rule introduced here.

Risks:

- Users who relied on the tabs always showing the whole scan may be surprised.
  The visible scope label and the one-click reset mitigate this.
- Clearing the tree selection when the user picks a large file must not
  silently reset the scope; the scope is kept as its own state for that reason.
- Frequent selection changes on very large results could queue expensive
  recomputations; latest-selection-wins cancellation and subtree-only walks
  keep this bounded.

Roadmap reference:

- WP-04 in `docs/IMPLEMENTATION_ROADMAP.md`, whose acceptance criteria require
  the tree, the largest-files list, and the summary to stay consistent. This
  change refines that consistency for a selected folder and extends the current
  baseline's "File-type statistics and a largest-files list".

Estimate:

- 1-2 days.
