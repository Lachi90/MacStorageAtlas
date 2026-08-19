## Why

A completed scan of a home folder or volume produces hundreds of thousands of
results, and the only way to narrow them today is a single substring match over
name and path. Users cannot ask the questions that actually drive cleanup
decisions, such as which files are larger than a gigabyte, which archives have
not been touched in a year, or which results share storage with another path.

This change implements WP-04 from
[`docs/IMPLEMENTATION_ROADMAP.md`](../../../docs/IMPLEMENTATION_ROADMAP.md). It
depends on the scan-time metadata delivered by WP-03, which is now complete, and
it produces the filtered result set that WP-05 exports and WP-07 collects into a
cleanup basket.

## What Changes

- Add a serializable filter model in Core covering name and path text, minimum
  and maximum size, creation, modification, and last-access age, file
  extensions, file categories, and shared-storage status. Filters combine with
  AND semantics.
- Evaluate filters against files. Directories appear in filtered results only as
  ancestors of matching files, which removes the ambiguity of applying a size or
  age predicate to an aggregate directory total.
- Add a versioned extension-to-category taxonomy so users can filter by archive,
  video, image, audio, document, disk image, and code categories rather than
  enumerating extensions.
- Replace the existing search box binding with the filter model's text term.
  Existing search behavior is preserved: the term continues to match name or
  path, case-insensitively.
- Show filtered results consistently across result views. The folder tree, the
  largest-files list, and the file-type summary show matching results only. The
  treemap keeps its full layout and highlights matching rectangles so that
  part-of-whole proportions remain truthful.
- Show, per directory row, the total matched size of its descendants while a
  filter is active, under a distinct column label so it is never confused with
  the directory's full size.
- Report match count, total matched size, and the number of results excluded
  because a required date was unknown.
- Add built-in presets for larger than 1 GB, not modified for one year, large
  archives, and large disk images and installers. Allow users to save, apply,
  rename, and delete their own presets, persisted in application settings.
- Distinguish an invalid filter, such as a minimum size above the maximum size,
  from a valid filter that matches nothing.
- Clear a selected item when a filter change makes it invisible in the view that
  owns the selection.

## Non-goals

- OR, NOT, or grouped boolean composition. The first version is AND only.
- Filtering by hidden status. Hidden results are already governed by the
  `IncludeHiddenFiles` scan option, and a hidden filter would be meaningful only
  when that option was enabled for the scan.
- Filtering by application-package membership. `DiskItem` has no parent
  reference, and deriving membership from path segments is not worth its cost in
  this change.
- A file-versus-folder filter dimension. Files-only match semantics make it
  redundant.
- Filtering that changes scan behavior. Filters operate on a completed scan
  result and never trigger a rescan or a second filesystem traversal.
- Exporting the filtered result. That is WP-05.
- Acting on multiple filtered results at once. That is WP-07.

## Capabilities

### New Capabilities

- `result-filtering`: How MacStorageAtlas narrows a completed scan result by
  size, age, type, text, and shared-storage status; how filtered results are
  presented across result views; how match totals and exclusions are reported;
  and how filter presets are defined, applied, and persisted.

### Modified Capabilities

- `result-tree-browsing`: selection behavior on a search change. The scenario
  requiring the folder-tree selection to be cleared unconditionally is replaced
  by clearing only a selection that is no longer displayed, and keeping one that
  is. Search text becomes a filter criterion in this change, so the previous
  rule would contradict this change's requirement that a still-visible selection
  is preserved.

`file-metadata` and `storage-measurement` are unchanged. Filtering reads the
metadata and measurement values they already specify, without altering how those
are captured or defined.

## Impact

- `MacStorageAtlas.Core`: new filter record, filter evaluator, filter result
  model, and file-category taxonomy. `LargeFilesService` and
  `FileTypeStatisticsService` gain the ability to compute over a supplied set of
  files rather than always walking the whole tree.
- `MacStorageAtlas.App`: new filter view model and filter panel view; changes to
  `MainWindowViewModel` result wiring and selection reconciliation;
  `DiskItemTreeFilter` and `DiskItemTreeNodeViewModel` consume a filter result
  instead of a raw search string; `AppSettings` gains versioned preset
  persistence.
- `MacStorageAtlas.Rendering`: unchanged. Treemap highlighting is a presentation
  concern and does not alter layout calculation.
- `MacStorageAtlas.Platform.Mac`: unchanged. Filtering needs no platform
  integration.
- `MacStorageAtlas.Tests`: new tests for every predicate, boundary values,
  unknown dates, match totals, preset round-tripping, selection reconciliation,
  and cancellation of in-flight filter evaluation.
- Documentation: `README.md`, `docs/FEATURES.md`, and `docs/index.html` describe
  filtering as a user-visible capability, and the roadmap status table records
  WP-04.

## Dependencies

- WP-03 metadata (`add-file-metadata`), complete. Age predicates read
  `DiskItemMetadata` timestamps.
- WP-02 shared-aware accounting (`deduplicate-hardlinks`,
  `investigate-apfs-clone-accounting`), complete. The shared-storage predicate
  reads `DiskItem.IsSizeCountedElsewhere`.
- `virtualize-result-tree` is recommended to land first. Filtering multiplies
  the number of triggers that rebuild the result tree, and that change removes
  the eager view-model materialization that makes each rebuild expensive.
  Filtering is functionally correct without it, but large scans stay less
  responsive.

## Risks

- Filter evaluation and tree rebuild on every control change can make large
  scans feel unresponsive. Mitigated by debouncing input, running evaluation off
  the UI thread with cancellation, and superseding in-flight evaluations.
- A per-directory matched subtotal is a second byte quantity displayed beside
  results measured in the scan's measurement mode. Mitigated by changing the
  column label while a filter is active and keeping the full size available in
  item details.
- Filters make it easy to assemble a large set of old or large files, which is a
  natural precursor to bulk deletion. Mitigated by keeping every preset name
  factual, never implying that a matched result is safe to delete, and leaving
  all cleanup behind the existing per-item Trash confirmation until WP-07
  delivers a reviewed multi-item workflow.
- Persisted presets must survive settings written by older and newer versions.
  Mitigated by a schema version on the persisted filter and by skipping presets
  that cannot be deserialized instead of failing the settings load.

## Estimate

4-7 days, matching the WP-04 roadmap estimate.
