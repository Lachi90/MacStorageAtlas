## 1. ViewModel Commands

- [x] 1.1 Add a `RemoveRecentLocationCommand` to `MainWindowViewModel` that removes the supplied path from `RecentLocations`, preserves remaining order, and saves settings.
- [x] 1.2 Add a `ClearRecentLocationsCommand` to `MainWindowViewModel` that clears `RecentLocations`, saves settings, and does not start, cancel, or alter a scan.
- [x] 1.3 Update recent-location status messages so explicit removal, clear-all, and missing-path scan removal report clear outcomes without overwriting unrelated scan state.
- [x] 1.4 Add App ViewModel tests for removing one listed location, removing an unknown location, clearing a populated list, clearing an empty list, and preserving the current scan result.

## 2. Persistence and State Preservation

- [x] 2.1 Add tests proving individual removal persists across a new ViewModel instance using the existing settings service.
- [x] 2.2 Add tests proving clear-all persists an empty recent-location list across a new ViewModel instance.
- [x] 2.3 Add tests proving recent-location cleanup preserves scanner options, measurement mode, saved filter presets, window size, scan history settings, and stored scan history.
- [x] 2.4 Confirm startup continues to load stored recent locations without existence checks or automatic path pruning.

## 3. UI

- [x] 3.1 Update `MainWindow.axaml` so each recent-location row exposes both scan and remove actions while preserving long-path trimming.
- [x] 3.2 Add a clear-all action for the recent-location list that is visible only when recent locations exist.
- [x] 3.3 Use existing theme resources, icon resources, compiled binding patterns, and accessibility names or tooltips for the new actions.
- [x] 3.4 Verify the start screen remains compact and usable with one, many, and long recent paths.

## 4. Documentation

- [x] 4.1 Review `README.md` and update it if explicit recent-location cleanup changes user-facing behavior described there.
- [x] 4.2 Review `docs/FEATURES.md` and update the Recent Scan Locations entry for remove and clear-all behavior.
- [x] 4.3 Review `docs/index.html` and update it only if the landing page should mention recent-location management.
- [x] 4.4 Review `docs/IMPLEMENTATION_ROADMAP.md` and update roadmap status only if this cleanup changes the documented baseline or WP-09 notes.

## 5. Validation

- [x] 5.1 Run `dotnet build MacStorageAtlas.slnx --no-restore`.
- [x] 5.2 Run `dotnet test MacStorageAtlas.slnx --no-build`.
- [x] 5.3 Run `dotnet format MacStorageAtlas.slnx analyzers --diagnostics IDE0005 --verify-no-changes`.
- [x] 5.4 Run `openspec validate --all --strict --no-interactive`.
- [x] 5.5 Run `git diff --check`.
