## 1. Copy Path Command

- [x] 1.1 Add `CopySelectedItemPathCommand` to `MainWindowViewModel`, writing `SelectedItem.Path` through `IClipboardService`, disabled without a selected item and while an update restart is pending.
- [x] 1.2 Notify `CopySelectedItemPathCommand` from the tree, treemap, large-file, and duplicate-entry selection partial methods and from the update-restart state change.
- [x] 1.3 Add App ViewModel tests for copying the selected path from each of the four selection sources, for the disabled state without a selection, and for the disabled state during a pending update restart.

## 2. Keyboard Shortcut

- [x] 2.1 Handle Command-Option-C in `MainWindow.TryHandleInspectionShortcut` when the source is not a text-editing control and the command can execute.
- [x] 2.2 Add tests proving the shortcut copies the selected path, leaves the clipboard unchanged without a selection, and is ignored while the search field has focus.

## 3. Treemap Secondary-Press Selection

- [x] 3.1 Update `TreemapControl` to hit-test on a secondary-button press, set `SelectedRectangle` when a block is hit, and open the item context menu only in that case.
- [x] 3.2 Leave the press unhandled and the selection unchanged when no block is hit.
- [x] 3.3 Add tests proving a secondary press on a block selects it, a secondary press on empty space keeps the existing selection, and a primary press keeps its current behavior.

## 4. Shared Context Menu

- [x] 4.1 Define one item context menu resource in `MainWindow.axaml` with the inspection, path, folder-actions, cleanup-basket, and Move to Trash groups in the order recorded in `design.md`.
- [x] 4.2 Bind each entry to the existing selected-item command with compiled bindings, show shortcuts with input gestures, reuse toolbar labels, and set accessible names.
- [x] 4.3 Add the named `FolderActionsGroup` with no entries and hide it, including its separator, while it has no visible entries.
- [x] 4.4 Attach the menu to tree, Largest files, and Duplicates item containers and to `TreemapControl`, so it opens only over an item.
- [x] 4.5 Add App ViewModel tests proving that selecting an item through each source enables exactly the commands the toolbar enables for it, including basket add and remove enablement and the scan-root protection message for Move to Trash.

## 5. Result Replacement

- [x] 5.1 Close any open item context menu when the displayed tree, treemap, Largest files list, or duplicate groups are replaced.
- [x] 5.2 Add App ViewModel tests proving that starting a scan clears the selection and disables every selected-item command, including `CopySelectedItemPathCommand`.

## 6. Manual Verification

- [ ] 6.1 Verify right-click, Control-click, and two-finger click open the menu on the clicked item in the tree, treemap, Largest files, and Duplicates views on macOS.
- [ ] 6.2 Verify Move to Trash from the menu shows the existing confirmation for an eligible file and the protection message for the scan root.
- [ ] 6.3 Verify VoiceOver announces each menu entry by name.

## 7. Documentation

- [x] 7.1 Review `README.md` and document item context menus and Copy path if selected-item actions are described there.
- [x] 7.2 Review `docs/FEATURES.md` and update the selected-item action entries for the context menu and Copy path.
- [x] 7.3 Review `docs/index.html` and update it only if the landing page lists selected-item actions or shortcuts.
- [x] 7.4 Update the treemap caption in `MainWindow.axaml` if it should mention right-click actions.
- [x] 7.5 Add an `add-item-context-menus` note to the WP-03 row of the roadmap status table in `docs/IMPLEMENTATION_ROADMAP.md`.

## 8. Validation

- [x] 8.1 Run `dotnet build MacStorageAtlas.slnx --no-restore`.
- [x] 8.2 Run `dotnet test MacStorageAtlas.slnx --no-build`.
- [x] 8.3 Run `dotnet format MacStorageAtlas.slnx analyzers --diagnostics IDE0005 --verify-no-changes`.
- [x] 8.4 Run `openspec validate --all --strict --no-interactive`.
- [x] 8.5 Run `git diff --check`.
