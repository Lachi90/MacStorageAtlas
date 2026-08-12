## Context

Recent scan locations are currently App-layer state loaded from `AppSettings.RecentLocations`, exposed by `MainWindowViewModel.RecentLocations`, persisted through `ISettingsService`, and rendered on the start screen. The list is most-recently-used, capped at `AppSettings.MaxRecentLocations`, and missing paths are removed only when the user attempts to scan them.

This change is UI and settings behavior only. It does not affect scan-domain logic, treemap rendering, macOS platform integrations, cleanup basket execution, relocation, or scan history storage.

Responsibility map:

- Core: no changes.
- Rendering: no changes.
- Platform.Mac: no changes.
- App: commands, status messages, settings persistence, and AXAML layout.
- Tests: App ViewModel and UI-binding coverage where practical.

## Goals / Non-Goals

**Goals:**

- Let users remove one recent location without scanning it.
- Let users clear the full recent-location list.
- Persist recent-location changes immediately.
- Preserve unrelated settings and stored scan history.
- Keep missing-path removal behavior when selecting a stale recent location.
- Keep the UI compact and usable with long paths.

**Non-Goals:**

- Do not change scan execution or scanner options.
- Do not add a new persistence store.
- Do not add platform-specific path validation.
- Do not delete scan history snapshots, cleanup basket contents, files, or folders.
- Do not probe every recent path on startup.

## Decisions

### Recent-location management stays in `MainWindowViewModel`

Add commands to `MainWindowViewModel` for removing one path and clearing all paths, reusing the existing `RemoveRecentLocation` and `SaveSettings` patterns. The feature belongs in App because recent locations are persisted UI convenience state, not scan-domain state.

Alternative considered: add a Core service for recent-location management. That would create an unnecessary dependency surface for behavior that has no domain rules beyond MRU list editing and settings preservation.

### Clear-all does not require confirmation

Clearing recent locations removes shortcuts from app settings only. It does not delete files, move anything to Trash, mutate scan history, or change scan results. Use an immediate command with a clear status message.

Alternative considered: mirror scan-history clearing with a confirmation service. That pattern is appropriate for deleting stored snapshot files and historical records, but it is heavier than needed for removing a small list of path shortcuts.

### Do not validate saved paths during list cleanup

Per-path removal and clear-all operate only on the stored strings. The existing scan-time missing-path check remains the only path-existence cleanup behavior.

Alternative considered: add a "remove missing locations" command or prune missing paths during startup. That can block on network volumes, wake removable drives, interact poorly with cloud-backed paths, and surface permission behavior before the user takes an action.

### Preserve settings through full settings snapshots

Recent-location edits should update `RecentLocations` and call the existing `SaveSettings` method, which writes the current scanner options, history settings, filter presets, window dimensions, and recent locations together.

Alternative considered: add a partial settings update API. The existing settings file is small, and a partial API would add complexity without meaningful benefit for this App-layer change.

### UI uses explicit row and clear actions

Render each recent location as a row with the existing scan action plus a small remove affordance. Add a clear-all action near the recent-locations heading or list footer, visible only when the list is non-empty. Long paths should continue to trim rather than force layout expansion.

Alternative considered: use context menus only. Context menus hide the feature and are less discoverable for a list whose primary problem is that users cannot see how to clean it up.

## Risks / Trade-offs

- Per-row controls make the start screen busier -> Keep the remove affordance compact, align it consistently, and preserve path trimming.
- Clear-all could be clicked accidentally -> The action only removes stored shortcuts, and a later scan naturally repopulates the list.
- Saving the full settings snapshot could regress unrelated settings -> Add tests that clear recent locations while preserving scan options, filter presets, window size, and history settings.
- Case-insensitive path comparison may not match every mounted filesystem -> Continue using the existing recent-location comparison semantics to avoid changing deduplication behavior in this small change.

## Migration Plan

No data migration is required. Existing `RecentLocations` values continue to load as they do today. Users gain new actions to remove individual entries or clear the list after startup.

Rollback can remove the new commands and UI controls while leaving the existing recent-location persistence and scan-time missing-path cleanup intact.

## Open Questions

None.
