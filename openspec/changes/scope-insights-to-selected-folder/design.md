## Context

`MainWindowViewModel.Prepare` builds the folder tree, the Largest files list,
and the File types summary together in one cancellable background preparation.
Without a filter it calls `LargeFilesService.GetLargestFiles(root)` and
`FileTypeStatisticsService.Calculate(root)` on the scan root. With a filter it
passes `FilterResult.MatchedFiles`, the matched files of the whole result.
Both insight tabs therefore always describe the entire scan.

`SelectedItem` merges four mutually exclusive selections: the folder-tree
node, the treemap rectangle, the Largest files entry, and the Duplicates entry.
Selecting one clears the other three in the `OnSelected*Changed` partial
methods. Deriving the scope from `SelectedItem` would make the Largest files
list rescope itself to a single file whenever the user picks an entry from it,
and selecting a large file would clear the tree selection and reset the scope.

`RemoveTrashedItem` mutates the result in place and calls `ApplySearch`, while
a new scan replaces `_scanRoot` and clears all selections. The planned
`rescan-folder-in-place` change will replace a subtree of `_scanRoot` with
newly measured items.

## Goals / Non-Goals

**Goals:**

- Scope both insight tabs to a directory chosen in the folder tree or the
  treemap, including its descendants.
- Keep the scope stable while the user works inside the insight and duplicate
  lists.
- Show the scope and allow a one-step return to the whole scan.
- Keep scope changes cheap, cancellable, off the UI thread, and free of
  filesystem access.
- Define how the scope survives removal and subtree replacement.

**Non-Goals:**

- Scoping export, history snapshots, duplicate analysis, the cleanup basket,
  the folder tree, the treemap, or the filter match summary.
- Changing ranking, the 100-entry limit, extension grouping, or displayed size
  fields.
- Persisting the scope across launches.

## Decisions

### Store the insight scope as its own state

The view model holds `InsightScope`, a `DiskItem` directory that defaults to
the scan root. It is set only in `OnSelectedTreeItemChanged` and
`OnSelectedTreemapRectangleChanged` when the new value is not null, by the
reset command, by scope re-resolution after a result change, and by a new scan
result. Clearing the tree or treemap selection because another list took the
selection does not touch it.

Alternative considered: derive the scope from `SelectedItem`. Rejected because
the merged selection is cleared and replaced by list picks, which would either
collapse the list onto one file or reset the scope.

### A selected file scopes to its containing directory

Selecting a file sets the scope to its parent directory, found by walking the
result from the root along the file's ancestors. This keeps the scope a pure
function of the latest tree or treemap selection, so the user can predict it
without remembering earlier clicks.

Alternative considered: keep the previous scope when a file is selected.
Rejected because the outcome would depend on selection history; selecting a
deep file after having selected the root would leave the tabs describing the
whole scan, which contradicts the folder the user is visibly inside.

### Separate insight preparation from tree preparation

A scope change runs only an insight preparation, not the tree rebuild. The
insight preparation has its own `CancellationTokenSource`, cancelled by the
next scope change, by a new filter preparation, by a synchronous result
refresh, and by a new scan. The latest preparation wins in the same way
`_treePreparationCancellation` works today, and it is applied only while its
scope and `FilterResult` are still current.

The insight computation is a separate `PrepareInsights` step. A tree
preparation captures the scope when it starts and runs `PrepareInsights` right
after the tree rebuild in the same background task, so a filter change still
produces a consistent tree and insight pair in one step. When the scope
changed while that tree preparation ran, applying it schedules a fresh insight
preparation for the current scope with the new `FilterResult`. The synchronous
refresh after a scan, a removal, or a folder rescan computes the insights for
the re-resolved scope inline, as it does for the tree today.

The insight preparation reads the `DiskItem` tree off the UI thread, so it is
tracked like the tree preparation: `HasActiveResultReaders` includes it, and
`WaitForResultReadersAsync` cancels and awaits it before a folder-rescan merge
mutates the tree.

### Walk only the scope subtree in Core

`LargeFilesService` and `FileTypeStatisticsService` gain overloads that take a
scope directory, an optional inclusion predicate, and a `CancellationToken`.
They walk only the scope subtree and check cancellation for every directory.
The inclusion predicate is applied to every visited item: a file for which it
returns false is skipped, and a directory for which it returns false is not
descended into. With a filter, the App passes a predicate that uses
`FilterResult.Matches` for files and `FilterResult.IsVisible` for directories,
so the cost is bounded by the visible part of the scope subtree. The existing
overloads remain and keep their behavior.

Alternative considered: intersect `FilterResult.MatchedFiles` with the scope by
path prefix. Rejected because it scans every match of the whole result for
each scope change and needs separator-aware, case-correct path comparison,
while identity-based subtree walking does not.

### Scope label and reset in the tab header area

Each of the two tabs shows a caption line above its content: "Showing the whole
scan" or "Showing files in “<folder name>”", with the full path as a tooltip
and accessible description, and a "Show whole scan" button bound to a
`ResetInsightScopeCommand` whose `CanExecute` is false while the scope is the
scan root. Names use `DisplayNameConverter`, and styling uses existing theme
resources. An empty scope shows "No files in “<folder name>”." or, with an
active filter, "No matching files in “<folder name>”.".

The item context menu closes when the Largest files list is replaced only
while a Largest files entry is selected. A rescoped list follows a tree or
treemap selection, so it must not close a menu that was just opened on that
tree node or treemap rectangle.

### Re-resolve the scope after result changes

A shared `ResolveInsightScope(string path)` helper finds the deepest directory
in the current result whose path equals the requested path or is its nearest
ancestor, and falls back to the scan root. `ApplySearch`, the synchronous
refresh that `RemoveTrashedItem` and the `rescan-folder-in-place` merge both
call after changing the result, re-resolves the scope by its path before it
prepares the tree and insights. When the scope was the removed item or lies
inside it, the walk stops at the nearest remaining ancestor. After a folder
rescan, the merge then selects the refreshed folder, or its parent when the
folder no longer exists, in the tree, which sets the scope through the normal
tree-selection rule. A new scan result sets the scope to the new root before
that refresh, matching how selections are cleared today.

## Responsibilities

- Core: scope-aware, predicate-aware, cancellation-aware overloads in
  `MacStorageAtlas.Core.Insights`. No new dependencies.
- Rendering: no change.
- Platform.Mac: no change.
- App: `InsightScope` state, insight preparation, labels, reset command, scope
  re-resolution, and AXAML for both tabs.
- Tests: Core insight tests and App ViewModel tests.

## Risks / Trade-offs

- [Users expect global tabs] → The scope statement is always visible and the
  reset is one click away.
- [Many quick selections on a huge result] → Cancellation and subtree-only
  walks keep work proportional to the selected subtree.
- [A hidden scope folder under a filter shows empty tabs] → The tabs state that
  no matching files exist in the named scope, and the reset is offered.
- [Inconsistency between sort basis and displayed size] → Unchanged by this
  change; the list keeps today's ranking and size column.

## Compatibility, privacy, and rollback

- Works identically on Apple Silicon and Intel; no platform APIs are involved.
- No filesystem reads, no new persisted data, and no transmission of paths.
- Rollback is removing the scope state; the existing whole-root overloads keep
  working unchanged.

## Open Questions

- None blocking. A later change may offer scoping export to the insight scope.
