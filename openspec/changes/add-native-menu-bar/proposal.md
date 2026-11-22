## Why

The macOS menu bar of Atlas Storage Analyzer contains only the system
application menu and, in direct-download builds, two update entries. None of
the app's commands can be found there, and apart from Space, Command-I, and
Command-F none has a keyboard shortcut. macOS users rely on the menu bar to
discover commands and learn their shortcuts. Assistive technologies and the
system Help search also index it. The roadmap already requires a keyboard
equivalent for every pointer action and names Command-R for rescanning, but
that shortcut does not exist.

## What Changes

- Add the standard macOS menu structure to the main window: File, Edit, Scan,
  Item, View, Window, and Help. These sit next to the existing application
  menu, which keeps its update entries in direct-download builds.
- Every menu entry invokes the same command as the matching toolbar, context
  menu, or panel control, and is enabled exactly when that command is enabled.
- Add shortcuts that follow Finder and common macOS conventions:

  | Menu | Entry | Shortcut |
  | --- | --- | --- |
  | File | Select Folder… | Command-O |
  | File | Open Recent | submenu of recent locations |
  | File | Export as CSV… | Command-E |
  | File | Export as JSON… | Shift-Command-E |
  | Scan | Scan / Rescan | Command-R |
  | Scan | Rescan Folder | Shift-Command-R |
  | Scan | Stop | Command-Period |
  | Scan | Find Duplicates | none |
  | Item | Quick Look | Command-Y and Space |
  | Item | Show Details | Command-I |
  | Item | Reveal in Finder | Option-Command-R |
  | Item | Copy Path | Option-Command-C |
  | Item | Add to / Remove from Cleanup Basket | Command-B |
  | Item | Move to Trash… | Command-Delete |
  | View | Filter and Search | Command-F |
  | View | Show Whole Scan | none |
  | View | Measurement mode and scan options | radio and checkbox entries |
  | View | Scan History | none |
  | Window | Minimize | Command-M |
  | Window | Zoom | none |
  | Help | Support and Privacy Policy pages | none |

- The Edit menu provides the standard Undo, Redo, Cut, Copy, Paste, and Select
  All entries for text fields.
- Shortcuts that would conflict with text editing, such as Space,
  Command-Delete, and Option-Command-C, never act on a scanned item while a
  text field has focus. Avalonia registers every shown menu shortcut as a
  native key equivalent, so these shortcuts stay in window key handling and
  their menu entries show no shortcut.
- Move to Trash from the menu keeps the existing protected-path check and
  confirmation.

Non-goals:

- Do not change the toolbar layout. That is `streamline-main-toolbar`.
- Do not add a separate settings window. Command-Comma is reserved until one
  exists.
- Do not add the commands themselves where they do not exist yet. Rescan Folder
  comes from `rescan-folder-in-place`, Copy Path from `add-item-context-menus`,
  and Show Whole Scan from `scope-insights-to-selected-folder`. Their menu
  entries are added once those changes are implemented.
- Do not change update behavior or how update entries are excluded from the
  App Store build.
- Do not localize menu titles.

## Capabilities

### New Capabilities

- `menu-bar-commands`: The application's commands exposed in the native macOS
  menu bar, with consistent enablement and keyboard shortcuts.

### Modified Capabilities

- None.

## Impact

Affected areas:

- `src/MacStorageAtlas.App`: Window-level `NativeMenu` in `MainWindow.axaml`
  bound to `MainWindowViewModel` commands, the coexistence of
  `App.axaml.cs` update entries with the new menus, shortcut handling in
  `MainWindow.axaml.cs`, and a recent-locations submenu.
- `tests/MacStorageAtlas.App.Tests`: Tests for the menu command mapping, the
  text-field shortcut guard, and the recent-locations submenu.
- Documentation: `README.md` (keyboard shortcuts), `docs/FEATURES.md`, and
  `docs/index.html`.

Dependencies:

- Uses the existing commands in `MainWindowViewModel`.
- Optional entries depend on `rescan-folder-in-place`, `add-item-context-menus`,
  and `scope-insights-to-selected-folder`. The menu ships without an entry until
  its command exists.

Risks:

- Avalonia's `NativeMenu` gesture handling can intercept keys before a focused
  text box sees them. Shortcuts that collide with text editing therefore keep
  their item behavior in window key handling, which is guarded by the
  text-field check.
- Dynamic entry labels, such as Add versus Remove from Cleanup Basket and Scan
  versus Rescan, must update without rebuilding the menu.
- Menus in the App Store sandbox build must not reference updater types.

Roadmap reference:

- Cross-cutting "Accessibility and interaction" requirements in
  `docs/IMPLEMENTATION_ROADMAP.md`. Primary WP: WP-03, which owns the
  selected-item inspection shortcuts (Space, Command-I) that this change
  extends.

Estimate:

- 2-3 days.
