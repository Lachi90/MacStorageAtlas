## 1. Core Insights

- [x] 1.1 Add a `LargeFilesService` overload that takes a scope directory, an optional inclusion predicate, a limit, and a `CancellationToken`, walks only the scope subtree, and keeps today's ranking.
- [x] 1.2 Add a `FileTypeStatisticsService` overload with the same scope, predicate, and cancellation parameters and today's grouping.
- [x] 1.3 Add Core tests for a nested scope, a file-only scope, an empty scope, a predicate that excludes files, the limit inside a scope, a pruned directory predicate, and cancellation.

## 2. Insight Scope State

- [x] 2.1 Add an `InsightScope` property to `MainWindowViewModel` that defaults to the scan root and is set only from non-null tree and treemap selections, the reset command, scope re-resolution, and a new scan result.
- [x] 2.2 Map a selected file to its containing directory and a file in the scan root to the scan root.
- [x] 2.3 Add App tests proving tree and treemap directory selections set the scope, file selections scope to the containing directory, and Largest files or Duplicates selections keep the scope.
- [x] 2.4 Add App tests proving a filter that hides the scope folder keeps the scope and yields empty tabs.

## 3. Insight Preparation

- [x] 3.1 Split the insight computation out of `Prepare` into a cancellable background insight preparation that runs for scope changes and after every tree preparation.
- [x] 3.2 Pass `FilterResult.Matches` and `FilterResult.IsVisible` when a filter is active so the tabs describe matched files inside the scope.
- [x] 3.3 Add App tests for scope with and without a filter, latest-scope-wins after rapid changes, no filesystem access during scope changes, and unchanged measurement mode labels.
- [x] 3.4 Add App tests proving the filter match count and matched size, export, and scan history capture ignore a narrowed scope.

## 4. Scope Re-resolution

- [x] 4.1 Add a `ResolveInsightScope` helper that returns the directory with the requested path or its nearest remaining ancestor, falling back to the scan root.
- [x] 4.2 Re-resolve the scope after `RemoveTrashedItem` and reset it when a new scan result arrives.
- [x] 4.3 Add App tests for trashing the scope folder, trashing an ancestor of the scope folder, trashing an unrelated item, and a new scan resetting the scope.

## 5. UI

- [x] 5.1 Add a scope caption with the folder name, the full path as tooltip and accessible description, and a "Show whole scan" action to the File types and Largest files tabs in `MainWindow.axaml`.
- [x] 5.2 Add `ResetInsightScopeCommand` with `CanExecute` false while the scope is the scan root, and App tests for its availability and effect.
- [ ] 5.3 Use compiled bindings, existing theme resources, `DisplayNameConverter`, and an empty-scope message on both tabs; verify the layout with long folder names.

## 6. Documentation

- [x] 6.1 Review `README.md` and describe that the Largest files and File types tabs follow the selected folder.
- [x] 6.2 Review `docs/FEATURES.md` and update the File Type Statistics and Large Files View entries.
- [x] 6.3 Review `docs/index.html` and update the file-type and largest-files feature text if it implies whole-disk-only results.
- [x] 6.4 Update the WP-04 status notes in `docs/IMPLEMENTATION_ROADMAP.md`.

## 7. Validation

- [x] 7.1 Run `dotnet build MacStorageAtlas.slnx --no-restore`.
- [x] 7.2 Run `dotnet test MacStorageAtlas.slnx --no-build`.
- [x] 7.3 Run `dotnet format MacStorageAtlas.slnx analyzers --diagnostics IDE0005 --verify-no-changes`.
- [x] 7.4 Run `openspec validate --all --strict --no-interactive`.
- [x] 7.5 Run `git diff --check`.
