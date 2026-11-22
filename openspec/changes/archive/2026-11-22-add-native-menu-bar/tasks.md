## 1. Spike and ViewModel support

- [x] 1.1 Spike how Avalonia `NativeMenu` gestures interact with a focused `TextBox` on macOS on Apple Silicon and Intel or Rosetta, and record the result in `design.md`.
- [x] 1.2 Add `ScanCommandLabel`, `CleanupBasketCommandLabel`, and `ToggleSelectedItemInCleanupBasketCommand` to `MainWindowViewModel`, raised from the existing selection, scan, and basket notifications.
- [x] 1.3 Add App ViewModel tests for both labels and for the toggle command delegating to add and remove with unchanged enablement.

## 2. Menus

- [x] 2.1 Declare File, Edit, Scan, Item, View, Window, and Help menus on `MainWindow` with compiled bindings to the existing commands, and keep the application menu's update entries for direct-download builds.
- [x] 2.2 Add the File > Open Recent submenu bound to recent locations with Clear Menu.
- [x] 2.3 Add View entries for the measurement mode as radio entries, hidden files, packages, and symbolic links as checkbox entries, Filter and Search, and Scan History.
- [x] 2.4 Add App tests proving every menu entry maps to the intended command and that recent locations appear in order.

## 3. Shortcuts

- [x] 3.1 Register the menu gestures Command-O, Command-E, Shift-Command-E, Command-R, Command-Period, Command-Y, Command-I, and Option-Command-R.
- [x] 3.2 Handle Space, Command-Delete, and Command-B in `TryHandleInspectionShortcut` with the text-field guard. Avalonia cannot show a menu gesture without registering it, so their menu entries show no shortcut and the item context menu shows it instead.
- [x] 3.3 Add App tests showing that each colliding shortcut does nothing to items while a text field has focus and invokes its command otherwise, and that unavailable commands ignore their shortcut.

## 4. Dependent entries

- [x] 4.1 Once `rescan-folder-in-place` is implemented, add Scan > Rescan Folder with Shift-Command-R, which keeps its guarded window handling and is not registered on the menu.
- [x] 4.2 Once `add-item-context-menus` is implemented, add Item > Copy Path with Option-Command-C, which keeps its guarded window handling and is not registered on the menu.
- [x] 4.3 Once `scope-insights-to-selected-folder` is implemented, add View > Show Whole Scan.

## 5. Verification and documentation

- [ ] 5.1 Manually verify the menus, enablement, and shortcuts in the direct-download and App Store builds, including VoiceOver reading of menu entries.
- [x] 5.2 Add a keyboard shortcut table to `README.md` and update `docs/FEATURES.md`.
- [x] 5.3 Review `docs/index.html` and update it if the landing page should mention keyboard shortcuts.
- [x] 5.4 Update the accessibility and interaction notes in `docs/IMPLEMENTATION_ROADMAP.md`.

## 6. Validation

- [x] 6.1 Run `dotnet build MacStorageAtlas.slnx --no-restore`.
- [x] 6.2 Run `dotnet test MacStorageAtlas.slnx --no-build`.
- [x] 6.3 Run `dotnet format MacStorageAtlas.slnx analyzers --diagnostics IDE0005 --verify-no-changes`.
- [x] 6.4 Run `openspec validate --all --strict --no-interactive`.
- [x] 6.5 Run `git diff --check`.
