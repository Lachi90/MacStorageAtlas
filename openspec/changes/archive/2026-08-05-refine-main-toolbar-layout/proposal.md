## Why

The main window chrome currently presents toolbar controls as two stacked bands at the default window width, making the top area feel heavier than necessary and visually disconnected from the scan results. The command surface should read as one compact toolbar row with a clear separator before content so the app feels native and preserves vertical space.

## What Changes

- Refine the main window toolbar so primary scan commands, item actions, export actions, options, filters, and search present as a single toolbar row at the supported default window width.
- Add or preserve a clear divider separating the toolbar from the rest of the window content.
- Define responsive behavior for narrower widths so the toolbar remains usable without overlapping controls or growing unpredictably.
- Keep command enablement, flyouts, search behavior, and keyboard behavior unchanged.

## Capabilities

### New Capabilities

- `main-toolbar-layout`: Main window toolbar layout, grouping, separators, and responsive behavior.

### Modified Capabilities

- None.

## Impact

Affected area: `src/MacStorageAtlas.App/Views/MainWindow.axaml` and any directly related app UI tests or visual checks.

Related roadmap work: WP-04 advanced filters, WP-05 CSV and JSON export, and WP-07 cleanup basket introduced or completed commands that now share the top command surface.

Non-goals:

- No changes to scan, filter, export, Finder, Quick Look, or Trash behavior.
- No changes to Core, Rendering, or Platform.Mac APIs.
- No new toolbar customization, persistence, or user-configurable command ordering.

Dependencies:

- Existing Avalonia layout controls and theme resources.
- Existing command bindings and flyouts in `MainWindow.axaml`.

Risks:

- Fitting all labeled commands in one row may exceed the current minimum width unless lower-priority commands can collapse, overflow, or use icon-only presentation.
- Responsive behavior needs visual verification because toolbar wrapping and flyout placement are easy to regress without checking actual window sizes.

Roadmap estimate: less than 1 day.
