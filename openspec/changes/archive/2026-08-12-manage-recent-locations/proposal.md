## Why

Recent scan locations are remembered and shown in the UI, but users can only remove stale entries by attempting to scan a path that no longer exists. This leaves private path shortcuts lingering in settings and makes the list harder to keep useful.

## What Changes

- Add explicit recent-location management so users can remove one saved location without scanning it.
- Add a clear-all action for recent locations that updates the list immediately and persists the empty list.
- Keep automatic removal of a missing location when the user attempts to scan it.
- Preserve all unrelated settings, scan history, filter presets, and scan results when recent locations are removed.
- Avoid startup filesystem probing of recent locations.

Non-goals:

- Do not change how locations are added after successful scans.
- Do not change the maximum recent-location count.
- Do not add scan-history deletion or cleanup-basket behavior.
- Do not validate or canonicalize every stored path during app startup.

## Capabilities

### New Capabilities

- `recent-locations`: Explicit management of persisted recent scan locations.

### Modified Capabilities

- None.

## Impact

Affected areas:

- `src/MacStorageAtlas.App`: `MainWindowViewModel`, settings persistence through existing `ISettingsService`, and the recent-locations UI in `MainWindow.axaml`.
- `tests/MacStorageAtlas.App.Tests`: ViewModel tests for removing one recent location, clearing all recent locations, persistence, and preservation of unrelated settings.
- Documentation review: `README.md`, `docs/FEATURES.md`, and `docs/index.html`.

Dependencies:

- Uses the existing settings model and settings service.
- Uses existing Avalonia and CommunityToolkit.Mvvm command patterns.

Risks:

- Adding per-row actions could make the compact start screen visually noisy if not laid out carefully.
- Clearing recent locations must not accidentally reset scanner preferences, history settings, saved filter presets, window size, or scan history.
- Eager filesystem validation could create latency or permission friction for network, removable, cloud-backed, or protected locations, so this change avoids that behavior.

Roadmap reference:

- Recent scan locations and persisted settings are part of the current baseline in `docs/IMPLEMENTATION_ROADMAP.md`.
- WP-09 requires scan-history clearing to preserve recent locations; this change keeps recent-location management separate from scan-history deletion.

Estimate:

- Less than 1 day.
