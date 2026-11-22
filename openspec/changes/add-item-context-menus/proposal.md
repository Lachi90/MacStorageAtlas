## Why

Every action on a scanned item currently lives in the main toolbar or in its
compact Actions overflow. To act on an item the user has to select it in the
folder tree, treemap, Largest files list, or Duplicates list, then move the
pointer to the top of the window and find the right button. macOS users expect
to Control-click or right-click an item and act on it where they see it, as
they do in Finder. The missing context menu makes the app slower to use and
hides actions such as Quick Look and the cleanup basket from users who never
open the overflow.

## What Changes

- Add a context menu to result items in the folder tree, the treemap, the
  Largest files list, and the Duplicates list.
- Opening the context menu on an item first makes that item the selected item,
  so the menu always acts on the item the user sees highlighted.
- The menu offers the existing selected-item actions: Quick Look, Show details,
  Reveal in Finder, Add to cleanup basket, Remove from cleanup basket, and
  Move to Trash. Each entry is enabled exactly when the matching toolbar command
  is enabled and invokes the same command.
- Add a new Copy path action that copies the item's full path to the clipboard
  as plain text. It is available from the context menu and through the
  Command-Option-C shortcut, matching Finder's Copy as Pathname.
- Move to Trash from the context menu keeps the existing protected-path check
  and confirmation and only moves items to macOS Trash.
- Every context-menu entry has an accessible name and a keyboard or toolbar
  equivalent.

Non-goals:

- Rescanning a folder is added by `rescan-folder-in-place`. This change only
  reserves a folder-actions group in the menu for it.
- Do not add a native menu bar redesign, new toolbar layout, or new toolbar
  commands.
- Do not add multi-selection or bulk actions from the context menu.
- Do not add context menus to scan errors, scan history entries, recent
  locations, the cleanup basket review, or file-type rows.
- Do not change when existing commands are enabled, what they act on, or how
  Trash, protected paths, Quick Look, or the cleanup basket behave.
- Do not add permanent deletion.

## Capabilities

### New Capabilities

- `result-item-context-menu`: Context menus on scan result items that expose the
  selected-item actions for the item they were opened on, plus copying an
  item's path.

### Modified Capabilities

- None.

## Impact

Affected areas:

- `src/MacStorageAtlas.App`: `MainWindow.axaml` context menus for the folder
  tree, Largest files, and Duplicates lists; `TreemapControl` selection on a
  secondary-button press; a `CopySelectedItemPathCommand` on
  `MainWindowViewModel` using the existing `IClipboardService`; the
  Command-Option-C shortcut in `MainWindow.axaml.cs`.
- `tests/MacStorageAtlas.App.Tests`: ViewModel tests for selection targets,
  command availability, Copy path, and result replacement while a menu is open;
  a control-level test for treemap secondary-button selection.
- Documentation review: `README.md`, `docs/FEATURES.md`, `docs/index.html`, and
  the roadmap status table in `docs/IMPLEMENTATION_ROADMAP.md`.

Dependencies:

- Uses the existing selected-item commands, the protected-path policy, Trash
  confirmation, the cleanup basket planner, and `IClipboardService`.
- `rescan-folder-in-place` consumes the folder-actions group defined here.

Risks:

- A menu opened on one item could act on another if selection and the menu
  drift apart. The change makes opening the menu select the item and closes the
  menu when the result is replaced.
- Avalonia's VoiceOver exposure of context menus on macOS may be limited. Every
  entry therefore keeps a toolbar or keyboard equivalent.
- A right-click on the treemap previously did nothing. Selecting on a secondary
  press changes which block is highlighted, which must not trigger a layout
  change beyond what a primary click already does.

Roadmap reference:

- Primary: WP-03 (Quick Look and file metadata), which owns selected-item
  inspection actions and the keyboard shortcuts for them.
- Supports the roadmap's accessibility rule that every pointer action needs a
  keyboard equivalent.

Estimate:

- 1-2 days.
