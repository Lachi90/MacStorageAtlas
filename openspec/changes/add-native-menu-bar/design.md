## Context

`App.axaml.cs` builds the application-level `NativeMenu` only under
`DIRECT_UPDATES`, and inserts Check for Updates and Automatically Check for
Updates into it. `MainWindow.axaml.cs` handles Space (Quick Look), Command-I
(details), and Command-F (search focus) in `OnKeyDown`, guarded by
`IsTextEditingSource`. All other commands are `[RelayCommand]`s on
`MainWindowViewModel` with can-execute methods.

## Goals / Non-Goals

Goals:

- Make every main-window command available through the menu bar, with
  enablement identical to its existing control.
- Add conventional shortcuts without breaking text editing.
- Keep the App Store build free of updater references.

Non-Goals:

- Toolbar layout changes, a settings window, and localization.

## Decisions

### Window-level menu bound to the ViewModel

Declare the File, Edit, Scan, Item, View, Window, and Help menus as
`NativeMenu.Menu` on `MainWindow` in AXAML, with compiled bindings to
`MainWindowViewModel` commands. On macOS, Avalonia merges window menus with the
application menu, so the update entries in `App.axaml.cs` stay where they are
and need no change. This keeps the menu declarative and testable through the
ViewModel commands, which this change does not modify.

Alternative considered: building all menus in `App.axaml.cs` code. Rejected,
because it moves view composition into code-behind and duplicates bindings.

### Dynamic labels through ViewModel properties

Add read-only `ScanCommandLabel` ("Scan" or "Rescan") and
`CleanupBasketCommandLabel` ("Add to Cleanup Basket" or "Remove from Cleanup
Basket") on the ViewModel. The basket entry binds to a new
`ToggleSelectedItemInCleanupBasketCommand`, which delegates to the existing add
and remove commands. Labels are raised from the existing selection and basket
change notifications.

### Shortcut placement

Gestures whose keys a text box uses, or that must not act on a scanned item
while a text box has focus, are handled in `TryHandleInspectionShortcut`,
guarded by `IsTextEditingSource`: Space, Command-Delete, Option-Command-C,
Command-B, and Shift-Command-R, whose text guard comes from
`rescan-folder-in-place`. They are not registered on the menu, so their menu
entries show no shortcut. Avalonia cannot show a gesture on a `NativeMenuItem`
without registering it, as the spike below records. The item context menu shows
these shortcuts through its display-only `InputGesture`, and the README lists
them.

All other gestures (Command-O, Command-E, Shift-Command-E, Command-R,
Command-Period, Command-Y, Command-I, Option-Command-R, Command-F, and
Command-M) are registered on the menu items. Command-I moves to the menu, and
its manual handling in code-behind is removed so details open exactly once.
Command-F stays a view concern: View > Filter and Search uses a code-behind
`Click` handler that focuses the search box, and the window's existing
Command-F handling stays as the path for builds without an exported menu.
Because macOS consumes a key equivalent that a menu item handles, the window
never receives that key, and both paths call the same idempotent focus method.

The Edit entries invoke the focused `TextBox` through code-behind `Click`
handlers and do not register Command-Z, Shift-Command-Z, Command-X, Command-C,
Command-V, or Command-A. The text box keeps handling those keys itself, which
leaves text editing exactly as it was.

Native menu check marks do not toggle on their own, so the hidden-file,
package, and symbolic-link entries bind toggle commands and the measurement
entries bind `SelectMeasurementModeCommand` with a mode parameter, while their
check state binds one way to the ViewModel properties.

### Spike result: Avalonia 12 NativeMenu gestures and a focused TextBox

The spike in task 1.1 inspected Avalonia 12.0.4, the version the App project
references, because the GUI cannot be launched from the automated environment.
It covered both slices of the universal `libAvaloniaNative.dylib`.

- `NativeMenuItem.Gesture` is exported through
  `AvnAppMenuItem::SetGesture`, which calls `setKeyEquivalent:` and
  `setKeyEquivalentModifierMask:` on the `NSMenuItem`. Showing a gesture
  therefore always registers it as a native key equivalent. There is no
  display-only gesture on `NativeMenuItem`.
- `AvnView` does not override `performKeyEquivalent:`, and `AvnApplication`
  calls the standard `-[NSApplication sendEvent:]` before its Command-key-up
  forwarding. A key-down with Command therefore follows the AppKit order: the
  key window's views, then the main menu's key equivalents, and only then
  `keyDown:` on the focused view. A registered Command gesture reaches the menu
  before a focused Avalonia `TextBox` sees the key, regardless of text focus.
- `NativeMenuItem` derives from `AvaloniaObject`, has no `DataContext`, and does
  not toggle `IsChecked` when clicked. Compiled bindings resolve against the
  window's data context.
- `NativeMenu` has no `ItemsSource`. Its `NeedsUpdate` event, raised from the
  native menu delegate before a menu opens, is the documented hook for
  rebuilding items.

The behavior is the same on Apple Silicon and on Intel or Rosetta, because both
architecture slices export the same symbols and use the same AppKit dispatch.
The conservative design above follows from these results. Manual confirmation
in the running app remains part of task 5.1.

### Recent locations submenu

Fill an `Open Recent` submenu from `RecentLocations` with
`ScanRecentLocationCommand` entries followed by a Clear Menu entry bound to
`ClearRecentLocationsCommand`. Because `NativeMenu` has no `ItemsSource`, the
window rebuilds the submenu in its `NeedsUpdate` handler and keeps the existing
entries when the locations are unchanged. The order is already maintained by
the ViewModel.

### Dependent entries

Rescan Folder, Copy Path, and Show Whole Scan are added in the tasks of this
change only once the commands from their changes exist. Until then the menus
omit them rather than showing permanently disabled placeholders. All three
commands exist, so the menus include Scan > Rescan Folder, Item > Copy Path,
and View > Show Whole Scan. Rescan Folder and Copy Path keep their guarded
window shortcuts and show no menu shortcut.

### Window and Help menus

The Window menu offers Minimize with Command-M and Zoom, implemented as
window-state changes in code-behind. The Help menu opens the public support
page and the privacy policy through the window's launcher.

## Risks / Trade-offs

- [Gesture interception breaks text input] → The spike in task 1.1 showed
  that registered gestures bypass a focused text box, so colliding keys are not
  registered, plus a text guard test for each colliding key.
- [Colliding shortcuts are less discoverable in the menu bar] → The context
  menu and the README show them.
- [Menu state lags ViewModel state] → Entries bind to `ICommand.CanExecute`
  through command bindings that already raise `CanExecuteChanged`. A test
  covers the selection-dependent labels.
- [Native menus stay active while the window is disabled for a pending update
  restart] → Entries follow their commands' `CanExecute`, and the commands that
  change scan results or files already check the update-restart gate.
- [Store build pulls in updater code] → No change to the `DIRECT_UPDATES`
  boundary. A packaging check still verifies the Store build has no updater
  entries.

## Migration Plan

No data migration. Rolling back removes the menus. All commands stay reachable
through the toolbar.

## Open Questions

- None blocking.
