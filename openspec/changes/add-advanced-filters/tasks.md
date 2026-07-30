## 1. Core filter model

- [ ] 1.1 Add `FileCategory` enum and `FileCategoryMap` in Core mapping lowercase extensions to categories, with a version constant
- [ ] 1.2 Add tests covering category lookup, case-insensitive extensions, extensions belonging to no category, and files without an extension
- [ ] 1.3 Add `DiskItemFilter` sealed record in Core with text term, minimum and maximum size, created/modified/last-access bounds, extensions, categories, and shared-storage criteria
- [ ] 1.4 Add filter validation to `DiskItemFilter` reporting contradictory criteria such as a minimum size above the maximum, plus a property distinguishing an inactive filter from an active one
- [ ] 1.5 Add tests for validation states and for the inactive-filter case

## 2. Filter evaluation

- [ ] 2.1 Add `FilterResult` in Core carrying matched files, match count, total matched size, matched byte subtotals per directory, and the count of files excluded for unknown dates
- [ ] 2.2 Add `DiskItemFilterEvaluator` in Core that walks a `DiskItem` root, evaluates criteria against files only, and produces a `FilterResult`
- [ ] 2.3 Compare size criteria against `DiskItem.SizeBytes` with inclusive bounds
- [ ] 2.4 Exclude a file from matches when a required date is unavailable and count it as an unknown-date exclusion
- [ ] 2.5 Propagate `CancellationToken` through evaluation so a superseded run stops promptly
- [ ] 2.6 Add unit tests for each predicate in isolation, including boundary values at both inclusive size bounds
- [ ] 2.7 Add unit tests for AND combinations across two or more criteria
- [ ] 2.8 Add unit tests for unknown dates, including that missing dates do not exclude a file when no date criterion is active
- [ ] 2.9 Add unit tests asserting a directory is never matched by size, age, extension, category, or shared-storage criteria
- [ ] 2.10 Add unit tests for a file whose counted size is zero because its storage is shared, and for the shared-storage criterion selecting it
- [ ] 2.11 Add unit tests for matched directory subtotals and for the empty filter matching every file
- [ ] 2.12 Add a cancellation test for in-progress evaluation

## 3. Filtered result views in Core

- [ ] 3.1 Extend `LargeFilesService` to compute over a supplied set of files while preserving the existing whole-tree behavior
- [ ] 3.2 Extend `FileTypeStatisticsService` to compute over a supplied set of files while preserving the existing whole-tree behavior
- [ ] 3.3 Add tests confirming both services produce results consistent with a `FilterResult` and unchanged results when no filter is supplied

## 4. Filter view model

- [ ] 4.1 Add `ResultFilterViewModel` in App exposing bindable criteria, built-in and user presets, validation state, and match summary
- [ ] 4.2 Debounce criteria changes, evaluate through `Task.Run` with cancellation, supersede in-flight evaluations, and marshal results through `IUiDispatcher`
- [ ] 4.3 Re-express `MainWindowViewModel.SearchText` as the filter's text term, preserving case-insensitive name and path matching
- [ ] 4.4 Expose match count, total matched size, and unknown-date exclusion count
- [ ] 4.5 Distinguish no active filter, invalid filter with an explanation, and valid filter with zero matches
- [ ] 4.6 Add view-model tests for debouncing, superseded evaluations, the three filter states, and the reported totals

## 5. Result view integration

- [ ] 5.1 Change `DiskItemTreeFilter` to build the folder tree from a `FilterResult`, displaying directories only as ancestors of matching files
- [ ] 5.2 Add a filter-aware matched subtotal to `DiskItemTreeNodeViewModel` and keep the full size available for the selected-item details
- [ ] 5.3 Wire the largest-files list and file-type summary to the active `FilterResult`
- [ ] 5.4 Add a matching flag to treemap rectangle presentation without changing `TreemapLayoutService` or invalidating `_treemapLayoutCache`
- [ ] 5.5 Reconcile selection per result view after each evaluation, clearing only a selection that is no longer visible in the view owning it
- [ ] 5.6 Add tests for ancestor-only directory display, matched subtotals, list-view consistency, layout-cache preservation, and selection reconciliation for each view

## 6. Filter user interface

- [ ] 6.1 Add a filter panel in AXAML with compiled bindings and `x:DataType`, using existing theme resources and control styles
- [ ] 6.2 Label the folder tree size column as a matched size while a filter is active and restore the full-size label when it is cleared
- [ ] 6.3 Render treemap matches with reduced opacity on non-matching rectangles plus an outline on matching ones, so the distinction does not rely on color alone
- [ ] 6.4 Show the match summary, the unknown-date exclusion count, the invalid-filter explanation, and the empty-result state
- [ ] 6.5 Add a keyboard path to focus the filter controls consistent with the roadmap's Command-F convention
- [ ] 6.6 Provide accessible names for icon-only filter controls

## 7. Presets

- [ ] 7.1 Add built-in presets for larger than 1 GB, not modified for one year, large archives, and large disk images and installers, using factual names
- [ ] 7.2 Add versioned preset persistence to `AppSettings` holding a name and a serialized `DiskItemFilter`
- [ ] 7.3 Make `JsonSettingsService` skip a preset that cannot be deserialized without failing the remaining settings load
- [ ] 7.4 Add save, apply, rename, and delete commands for user presets
- [ ] 7.5 Add tests for preset round-tripping, restart persistence, deletion, unreadable-preset tolerance, settings written without presets, and applying a preset to a different scan root

## 8. Safety and privacy verification

- [ ] 8.1 Add tests asserting that applying, changing, or clearing a filter starts no scan and reads no file contents
- [ ] 8.2 Add tests asserting the existing Trash confirmation still applies with a filter active and that filtering modifies no matched file
- [ ] 8.3 Review every user-visible filter, preset, and summary string to confirm none describes a matched item as safe to delete

## 9. Documentation and validation

- [ ] 9.1 Update `README.md` to describe result filtering and presets
- [ ] 9.2 Update `docs/FEATURES.md` with the delivered filter dimensions and the documented non-goals
- [ ] 9.3 Review and update the GitHub Pages landing page at `docs/index.html`, and report if no update was necessary
- [ ] 9.4 Update the WP-04 row in the `docs/IMPLEMENTATION_ROADMAP.md` status table, noting the dropped file-versus-folder, hidden-status, and package-membership dimensions and the replaced downloads preset
- [ ] 9.5 Run `dotnet build MacStorageAtlas.slnx --no-restore`
- [ ] 9.6 Run `dotnet test MacStorageAtlas.slnx --no-build`
- [ ] 9.7 Run `dotnet format MacStorageAtlas.slnx analyzers --diagnostics IDE0005 --verify-no-changes`
- [ ] 9.8 Run `git diff --check`
- [ ] 9.9 Run `openspec validate --all --strict --no-interactive`
