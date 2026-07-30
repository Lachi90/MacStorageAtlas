## 1. Baseline

- [x] 1.1 Record a baseline for tree preparation by counting the `DiskItemTreeNodeViewModel` instances materialized for a large synthetic in-memory tree, measured in the tests project
- [x] 1.2 Confirm the existing `DiskItemTreeFilterTests` and `MainWindowViewModelTests` pass unmodified and identify which assertions describe observable behavior that must not change

## 2. Lazy tree materialization

- [x] 2.1 Make `DiskItemTreeNodeViewModel.Children` materialize child view models on first access instead of in the constructor
- [x] 2.2 Retain the internal constructor accepting a pre-built child list for filtered results
- [x] 2.3 Keep the unfiltered tree lazy from the root in `DiskItemTreeFilter`, and keep filtered subtrees eagerly built from the known match set
- [x] 2.4 Add tests asserting that an unexpanded subtree does not materialize descendant view models
- [x] 2.5 Add tests asserting that expanding a node materializes exactly its children and that the same items are displayed as before

## 3. Off-thread, cancellable preparation

- [x] 3.1 Replace the synchronous `ApplySearch` path in `MainWindowViewModel` with preparation through `Task.Run` and a `CancellationToken`
- [x] 3.2 Debounce search-text changes so a burst of keystrokes produces one preparation
- [x] 3.3 Cancel the in-flight preparation on each new input and discard superseded results before assigning `TreeItems`
- [x] 3.4 Marshal prepared results back through `IUiDispatcher`
- [x] 3.5 Choose the debounce interval within the design's stated range and record the chosen value in the change's design notes
- [x] 3.6 Add view-model tests for debouncing, for a superseded preparation never updating the displayed tree, and for cancellation
- [x] 3.7 Add a test asserting the displayed tree corresponds to the most recent search text after rapid successive changes

## 4. Behavior preservation

- [x] 4.1 Add tests asserting empty search text displays the complete scan result and that clearing search text restores it without a rescan
- [x] 4.2 Add tests asserting case-insensitive name and path matching is unchanged
- [x] 4.3 Add tests asserting search text matching nothing yields an empty tree with the scan result still available
- [x] 4.4 Add tests asserting tree preparation starts no scan, reads no file contents, and leaves the completed scan result unchanged
- [x] 4.5 Add tests asserting displayed byte values remain based on the completed scan's measurement mode
- [x] 4.6 Add a test asserting the tree selection is still cleared on a search change

## 5. Validation

- [x] 5.1 Compare the materialized view-model count against the task 1.1 baseline and record the improvement
- [x] 5.2 Update the WP-04 row in the `docs/IMPLEMENTATION_ROADMAP.md` status table to note this preparatory change
- [x] 5.3 Confirm no user-facing documentation update is required and report that explicitly
- [x] 5.4 Run `dotnet build MacStorageAtlas.slnx --no-restore`
- [x] 5.5 Run `dotnet test MacStorageAtlas.slnx --no-build`
- [x] 5.6 Run `dotnet format MacStorageAtlas.slnx analyzers --diagnostics IDE0005 --verify-no-changes`
- [x] 5.7 Run `git diff --check`
- [x] 5.8 Run `openspec validate --all --strict --no-interactive`
