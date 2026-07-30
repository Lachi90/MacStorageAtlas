## Context

A completed scan produces a `DiskItem` tree in Core. `MainWindowViewModel`
derives four presentations from it: a folder tree of
`DiskItemTreeNodeViewModel`, treemap rectangles from `TreemapLayoutService`,
file-type summaries from `FileTypeStatisticsService`, and a largest-files list
from `LargeFilesService`. Only the folder tree responds to input, through
`DiskItemTreeFilter.Filter`, which takes a search string and rebuilds a view-model
subtree.

Three properties of the current code shape this design.

`DiskItemTreeFilter.Filter` keeps a node when the node itself matches or any
descendant matches, and it matches on both `Name` and `Path`. Because a file's
path already contains its ancestors' names, a text term such as `node_modules`
already selects everything beneath a directory of that name. Subtree scoping
therefore exists without a dedicated control.

`DiskItem` exposes three byte quantities. `SizeBytes` is the counted size, which
shared-aware accounting sets to zero for a path whose storage is attributed to
another path in the same scan. `MeasuredSizeBytes` is the allocated size observed
for that path. `SharedSizeBytes` records the bytes counted elsewhere. The folder
tree and the largest-files list display `SizeBytes`; the selected-item details
show all three.

`DiskItem` has no parent reference, and `DiskItemTreeNodeViewModel` materializes
a view model for every descendant eagerly. `OnSearchTextChanged` re-runs
filtering synchronously on the UI thread.

## Goals / Non-Goals

**Goals:**

- Define filter criteria as a serializable Core record that carries no UI types,
  so presets, and later export and cleanup selection, can reuse it.
- Give filtering one unambiguous matching rule that holds for every criterion.
- Keep every displayed byte value truthful and clearly labelled.
- Keep the UI responsive on large scans, with cancellable evaluation off the UI
  thread.
- Keep Rendering and Platform.Mac untouched.

**Non-Goals:**

- Boolean composition beyond AND.
- A flat file index shared by all result views. See Decision 7.
- Changing how scanning, measurement, or metadata capture work.
- Export of filtered results (WP-05) or multi-item cleanup (WP-07).

## Decisions

### Decision 1: Filters live in Core as a record plus a pure evaluator

`DiskItemFilter` is a `sealed record` in Core holding the criteria. A
`DiskItemFilterEvaluator` walks a `DiskItem` root and returns a `FilterResult`
containing the matched files, the match count, the total matched size, matched
byte subtotals per directory, and the count of files excluded because a required
date was unknown. Both are free of Avalonia types and directly unit-testable.

The App layer owns a `ResultFilterViewModel` that binds controls, debounces
input, builds a `DiskItemFilter`, and invokes the evaluator.

*Alternative considered:* implementing filtering entirely in the App view-model
layer, as `DiskItemTreeFilter` does today. Rejected because presets must be
serialized, WP-05 must export "the current filtered result", and WP-07 must build
a cleanup basket from it. All three want the criteria as a portable value, and
Core is where portable domain logic belongs.

### Decision 2: Files are the unit of matching; directories are scaffolding

Every criterion is evaluated against files only. A directory is displayed when
one of its descendant files matches.

The alternative is to evaluate criteria against directories too. That fails for
aggregate criteria: every ancestor of a 1 GB file has an aggregate size above
1 GB, so a "larger than 1 GB" filter would retain nearly the whole tree and
accomplish nothing. Introducing a `Files / Folders / Both` selector would fix
that but adds a control whose behavior needs explaining and whose folder mode is
rarely what a storage user wants.

Files-only matching also makes the roadmap's "file versus folder" dimension
redundant, so it is dropped. The only behavioral loss is that an empty directory
whose name matches a text term no longer appears. For a storage tool a zero-byte
directory disappearing from a size-oriented view is acceptable, and directories
remain selectable as displayed ancestors, so no per-item action is lost.

### Decision 3: Size criteria compare against `SizeBytes`

Size bounds compare against the counted size, which is the value the folder tree
and largest-files list already display. What the user sees is what the filter
compares.

The consequence is deliberate: in shared-aware mode a second hardlink to a 4 GB
file has a counted size of zero and is therefore not matched by "larger than
1 GB". That is correct for a storage tool, because deleting that path reclaims
nothing. The dedicated shared-storage criterion is how a user finds those paths.

*Alternative considered:* comparing against `MeasuredSizeBytes`. Rejected because
it would surface paths under a size filter whose displayed size contradicts the
criterion, and because it would make "larger than 1 GB" mean something different
from the number in the adjacent column.

### Decision 4: The treemap highlights; it does not filter

The treemap keeps its full layout while a filter is active and distinguishes
matching rectangles.

This is both the more truthful and the cheaper option. A treemap encodes
part-of-whole relationships; removing non-matching rectangles and re-tiling the
remainder produces areas that no longer mean what the visual implies. Keeping the
layout also leaves the existing `_treemapLayoutCache` valid across every filter
change, so no re-layout occurs at all, and `MacStorageAtlas.Rendering` needs no
modification.

Highlighting is also the feature's most distinctive payoff: it answers where
matching files are located, which no list can show.

Per the roadmap's accessibility requirement, matches must be distinguished by
more than color. The rendering uses reduced opacity on non-matching rectangles
plus an outline on matching ones, and the match count appears as text.

### Decision 5: Filtered directory rows show a matched subtotal under a changed label

While a filter is active, the folder tree's size column is labelled as a matched
size and each directory row shows the sum of its matching descendants.

A matched subtotal is a real sum of real matched values in the scan's
measurement mode, so it does not violate the project's rule against fabricated
numbers. The risk is that it is mistaken for the directory's full size, which the
column label change resolves. The full size remains visible in the selected-item
details.

*Alternative considered:* keeping the full directory size in filtered rows.
Rejected because a row reading `40 GB` beside a filtered view invites the reading
"40 GB matched", which is the more misleading of the two options.

### Decision 6: A versioned category taxonomy in Core

`FileCategory` enumerates archive, video, image, audio, document, disk image, and
code. `FileCategoryMap` maps lowercase extensions to categories and carries a
version constant. Extensions not covered by any category belong to no category
and are matched only by the extension criterion.

The map is data rather than branching logic so contributors can extend it, and
the version constant lets persisted presets record which taxonomy they were
authored against.

### Decision 7: No flat file index in this change

An earlier option was to build one flat array of file records at scan completion
and have filtering, largest-files, file types, and export all read it.

It is not adopted here. The cost that makes large scans feel slow is the eager
materialization of a view model per node in `DiskItemTreeNodeViewModel`, not the
tree walk; evaluating predicates over a `DiskItem` tree is comparatively cheap.
Introducing a second full representation of the scan would also work against the
convention of avoiding duplicate copies of scan trees, and it would widen this
change well past one reviewable intent.

The index earns its place in WP-10, where duplicate detection needs
size-bucketed access across every file. It should be reconsidered there.

### Decision 8: Debounced, cancellable evaluation off the UI thread

Text input is debounced before evaluation. Evaluation runs through `Task.Run`
with a `CancellationToken`; a new criteria change cancels the in-flight
evaluation before starting the next. Results are marshalled back through
`IUiDispatcher`. A superseded evaluation never writes to the displayed result.

### Decision 9: The search box becomes the filter's text term

`MainWindowViewModel.SearchText` is re-expressed as the `DiskItemFilter` text
term rather than kept as a parallel mechanism. Observable behavior is preserved:
the term still matches name or path, case-insensitively.

### Decision 10: Presets persist as versioned records in `AppSettings`

`AppSettings` gains a list of named presets, each holding a serialized
`DiskItemFilter` and a schema version. `JsonSettingsService` skips a preset it
cannot deserialize and continues loading the remaining settings, following the
tolerant pattern already established by `MeasurementMode` and
`MeasureAllocatedSize`.

Built-in presets are code-defined and not persisted, so they can be corrected in
a later release without migrating stored data. They are: larger than 1 GB; not
modified for one year; large archives; and large disk images and installers.

The roadmap's "large downloads" preset is replaced by "large disk images and
installers". Filters evaluate within the current scan root, so a preset tied to a
Downloads location would return nothing whenever another root was scanned, which
is indistinguishable from a defect. The replacement is root-agnostic and captures
the same intent.

### Decision 11: Validation is separate from emptiness

`DiskItemFilter` exposes validation that reports contradictory criteria, such as
a minimum size above the maximum. The view model distinguishes three states: no
active filter, invalid filter with an explanation, and valid filter with a match
count that may be zero.

### Decision 12: Selection reconciliation per view

`MainWindowViewModel.SelectedItem` derives from three sources. After each filter
evaluation, each source is checked independently against the new filtered result
for its own view, and cleared only when its item is no longer visible there. A
selection that remains visible is preserved, so tuning a filter does not
repeatedly blank the details pane.

## Risks / Trade-offs

- **Filter changes trigger a full tree rebuild, and there are now many more
  triggers than one search box** → Debounce input, evaluate off the UI thread
  with cancellation, and land `virtualize-result-tree` first so each rebuild
  stops materializing view models for invisible nodes.
- **The matched subtotal is a second byte quantity displayed beside scan values**
  → Change the column label while a filter is active and keep the full size in
  the selected-item details.
- **Excluding files with unknown dates silently shrinks results** → Report the
  excluded count alongside the match count so the omission is visible.
- **Counted size of zero for shared paths surprises users filtering by size** →
  Provide the shared-storage criterion as the intended way to find those paths,
  and keep shared and counted sizes visible in item details.
- **Filters make large sets of old or large files easy to assemble, which
  precedes bulk deletion** → Keep preset and summary wording factual, never
  imply that a match is safe to delete, and leave cleanup behind the existing
  per-item Trash confirmation until WP-07.
- **Presets written by a different version may not deserialize** → Version the
  persisted filter, skip unreadable presets, and never fail the settings load.
- **The category taxonomy will drift as file formats change** → Keep it as
  versioned data in Core with direct tests, so extending it is a small
  contribution.

## Migration Plan

No data migration is required. `AppSettings` gains an optional preset list;
settings files written by earlier versions load with no presets. Settings written
by this version remain readable by earlier versions, which ignore the unknown
field.

Filtering is additive at runtime. With no active filter, every result view
behaves exactly as it does today, which is the rollback path if the feature is
disabled or reverted.

## Open Questions

- Should the file-type summary continue to show the unfiltered distribution as a
  secondary reference when a category filter is active, given that filtering by
  category makes the filtered summary partly tautological? Resolving this does
  not block implementation; the specified behavior is a filtered summary.
- Should applying a preset overwrite in-progress criteria silently or require
  confirmation when the current criteria are unsaved? The specified behavior is
  to overwrite; a confirmation can be added later if user feedback asks for it.
