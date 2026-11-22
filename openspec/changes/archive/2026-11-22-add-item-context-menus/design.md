## Context

`MainWindowViewModel.SelectedItem` resolves the acting item from four
mutually exclusive selections: `SelectedTreeItem`, `SelectedTreemapRectangle`,
`SelectedLargeFile`, and `SelectedDuplicateEntry`. Setting one clears the
others in the generated `On…Changed` partial methods. All selected-item
commands, including `RevealInFinderCommand`, `QuickLookCommand`,
`ShowSelectedItemDetailsCommand`, `MoveToTrashCommand`,
`AddSelectedItemToCleanupBasketCommand`, and
`RemoveSelectedItemFromCleanupBasketCommand`, act on `SelectedItem` and are
already re-evaluated whenever any of the four selections changes.

The folder tree is an Avalonia 12 `TreeView`, the Largest files and Duplicates
views are `ListBox` controls with two-way selection bindings, and the treemap is
the custom `TreemapControl`, which only selects on a primary-button press.
`IClipboardService` already exists and is used by `CopyErrorPathCommand`.
Keyboard shortcuts for Space and Command-I are handled in
`MainWindow.TryHandleInspectionShortcut`.

## Goals / Non-Goals

Goals:

- Act on items where the user sees them, with the same commands and checks as
  the toolbar.
- Guarantee that a menu acts on the item it was opened on.
- Add Copy path with a Finder-compatible shortcut.
- Leave a named place for folder-only actions.

Non-Goals:

- Rescan this folder, which `rescan-folder-in-place` adds to the reserved group.
- Native menu bar or toolbar redesign.
- Multi-selection.

## Decisions

### Menus bind to existing commands, not new per-item commands

Each menu entry binds to the existing `MainWindowViewModel` command with no
command parameter. Because opening the menu selects the item first, the command
acts on `SelectedItem`, and enablement, protected-path checks, Trash
confirmation, status messages, and the update-restart gate stay identical to the
toolbar by construction.

Alternative considered: parameterized commands that take the right-clicked
`DiskItem`. This would duplicate every `CanExecute` rule and status-message
path and would let the menu act on an item that is not highlighted. Rejected.

### Selecting before the menu opens

- `TreeView` and `ListBox` select the item under the pointer on a
  secondary-button press. The menu is attached per item through the item
  container style so it opens only over an item, and the container's selection
  happens before the menu opens.
- `TreemapControl` hit-tests on `PointerUpdateKind.RightButtonPressed` and sets
  `SelectedRectangle` exactly as a primary press does. When the hit test finds
  no block, the press is not handled, the selection is unchanged, and no item
  menu opens. The control opens its `ContextMenu` only when a block was hit:
  Avalonia raises `ContextRequested` on the secondary-button release, and a
  tunnel handler on the control marks the request handled when no block lies
  under the pointer, or, for a request without a pointer position, when no
  block is selected, so the attached menu does not open.
- Control-click arrives from macOS as a secondary-button press, so the same path
  covers both gestures.

### One shared menu definition

The menu is defined once as a resource in `MainWindow.axaml` and referenced by
the tree, treemap, Largest files, and Duplicates item containers. The menu's
data context is the window's `MainWindowViewModel`, reached through a compiled
binding to the window, so item templates keep their own `x:DataType`.

Entry order and groups, separated by menu separators:

1. Inspection: Quick Look (Space), Show details (Command-I), Reveal in Finder.
2. Path: Copy path (Command-Option-C).
3. Folder actions: a named, initially empty group (`FolderActionsGroup`) that
   is hidden while it has no visible entries. The group starts at the
   separator named `FolderActionsGroup`, and its entries are the menu items
   that follow it up to the next separator. When the menu opens, the view hides
   every separator that would be leading, trailing, or adjacent to another
   separator, so the empty group and its separator stay invisible.
   `rescan-folder-in-place` adds Rescan this folder after that separator and
   shows it only for directories.
4. Cleanup basket: Add to cleanup basket, Remove from cleanup basket.
5. Destructive: Move to Trash…, last and separated, styled with the existing
   danger resources.

Labels and accessible names reuse the toolbar wording. Shortcuts are shown with
`InputGesture` so the menu displays them without handling them twice.

Add to cleanup basket keeps the toolbar's enablement: it stays enabled for an
item that is already in the basket, and choosing it reports that the item is
already in the basket instead of adding it twice. Disabling it would change an
existing command's enablement, which this change excludes.

### Copy path

`CopySelectedItemPathCommand` is a `[RelayCommand]` on `MainWindowViewModel`
that writes `SelectedItem.Path` through `IClipboardService.SetTextAsync`. Its
`CanExecute` requires a selected item and no pending update restart. It is
notified from the same selection partial methods as the other selected-item
commands, through a shared `NotifySelectedItemCommandsCanExecuteChanged`
helper, and from the update-restart state change. `TryHandleInspectionShortcut`
handles Command-Option-C, with exactly the Command and Option modifiers, when
the event source is not a text-editing control, matching the Space rule. The
Space, Command-I, and Command-Option-C handling lives in the static
`MainWindow.TryHandleSelectedItemShortcut` so tests can exercise it without a
native window.

### Result replacement closes the menu

When a scan starts, `RunScanAsync` already clears every selection, which
disables every entry. The view additionally closes any open item context menu
when `TreeItems`, `TreemapRectangles`, `LargeFiles`, or the duplicate groups
are replaced, so a stale menu cannot remain visible over a new result. The
window observes the view model's property changes for this purpose; the view
model does not observe itself.

## Project Responsibilities

- Core: no change.
- Rendering: no change.
- Platform.Mac: no change; existing Finder, Quick Look, and Trash services are
  reused.
- App: shared context menu resource, item container wiring, treemap
  secondary-press selection, `CopySelectedItemPathCommand`, and the
  Command-Option-C shortcut.
- Tests: App ViewModel tests, shortcut and menu-structure tests, and
  `TreemapControl` input tests. Without a headless Avalonia platform, a
  detached control cannot resolve pointer positions, so the treemap tests drive
  the control's internal press and context-request decisions directly.

## macOS Compatibility

Control-click and two-finger click both reach Avalonia as secondary-button
presses on Apple Silicon and Intel Macs. Context menus render with the Fluent
theme inside the window rather than as native `NSMenu` instances, so VoiceOver
reads them as Avalonia accessibility nodes. Because VoiceOver's own "show menu"
action is not guaranteed to open Avalonia context menus, every entry keeps a
toolbar or keyboard equivalent, which the spec requires.

## Performance

The menu is a single shared resource. Opening it performs no filesystem access;
enablement is evaluated from existing in-memory state. Large trees remain
virtualized because the menu is attached through the container style rather
than per realized template instance.

## Privacy

Copy path writes only to the local system clipboard. No path is logged or
persisted by this change, and no file contents are read.

## Risks / Trade-offs

- The shared menu must act on the window's view model rather than on the
  item's data context. An ancestor binding does not resolve reliably from a
  popup, so the window sets the menu's `DataContext` to its own view model
  whenever its data context changes.
- Selecting on secondary press in the treemap changes the highlighted block on
  right-click. This matches Finder and is required so the menu target is
  visible.

## Rollback

Removing the menu resource, the container wiring, the treemap secondary-press
branch, and `CopySelectedItemPathCommand` restores the previous behavior. No
settings, history, or persisted data are affected.

## Open Questions

- None blocking. Whether Show details belongs in the menu was decided in favor
  of inclusion because it is an existing selected-item command with a shortcut.
