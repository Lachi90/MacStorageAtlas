## 1. Core subtree scan

- [x] 1.1 Add `ScanSubtreeAsync` to `IDiskScanner` and `DiskScanner`, reusing `ScanDirectoryAsync` with equivalent-path omission selected for the original scan root, and rejecting subtrees outside the root, shared-aware mode, and followed symbolic links.
- [x] 1.2 Add Core tests proving a subtree rescan equals the corresponding subtree of a fresh full scan for hidden files, expanded and unexpanded packages, and an omitted firmlink alternate inside the subtree.
- [x] 1.3 Add Core tests for cancellation, a recoverable error inside the subtree, a subtree path that no longer exists, a subtree outside the root, and the rejected measurement modes.

## 2. Core merge and provenance

- [x] 2.1 Add internal `DiskItem.ReplaceDescendant` and `ScanResultMerger.ReplaceSubtree` with ancestor size deltas for counted, measured, and shared bytes, removal when the replacement is absent, and re-sorting of touched ancestors.
- [x] 2.2 Add `ScanResultProvenance` and `RefreshedFolder`, with ancestor-supersedes-descendant merging and an empty list for full scans.
- [x] 2.3 Merge errors and omitted equivalent paths by subtree path.
- [x] 2.4 Add Core tests for size deltas in growth, shrink, and removal, for ordering after a merge, for preserved ancestor identity, for error and omission merging inside and outside the subtree, and for provenance superseding.

## 3. Cleanup basket rebinding

- [x] 3.1 Add `CleanupBasketPlanner.Rebind(DiskItem root, string subtreePath)`, which rebinds entries inside the subtree by path, removes vanished entries, and reports the removed count.
- [x] 3.2 Add Core tests for kept, resized, and removed entries and for unchanged entries outside the subtree.

## 4. History and export schema version 2

- [x] 4.1 Add refreshed-folder provenance to `ScanSnapshotMetadata`, write schema version 2, and read versions 1 and 2.
- [x] 4.2 Add Core tests for version 2 round-trip, version 1 fixtures read as having no refreshed folders, and an unsupported version reported as before.
- [x] 4.3 Add provenance to `ScanExportMetadata` and the JSON writer as export schema version 2, and read versions 1 and 2 in `ScanResultJsonReader`.
- [x] 4.4 Add Core tests for export version 2 round-trip and version 1 compatibility.

## 5. App orchestration

- [x] 5.1 Add `RescanFolderCommand` with can-execute rules for a completed result, a selected directory, no running scan, and no pending update restart, and route a root selection to a full rescan with the result's options.
- [x] 5.2 Add the shared-aware and followed-symlink fallback, which explains the limitation and offers a confirmed full rescan with the result's options.
- [x] 5.3 Implement `RunFolderRescanAsync` with off-thread streaming, visible progress, cancellation, and one UI-dispatcher merge step that updates the tree, treemap, filter, insights, errors, access guidance, protected-path policy, basket, duplicates, provenance, selection, and expansion.
- [x] 5.4 Start history capture with the merged root and provenance when history is enabled, and cancel a running capture when a folder rescan starts.
- [x] 5.5 Add App ViewModel tests for success, a deleted folder, cancellation leaving state unchanged, failure leaving state unchanged, the option-basis notice, the fallback prompt accepted and declined, basket notices, cleared duplicates, a captured snapshot with provenance, and no capture on cancel.

## 6. UI

- [x] 6.1 Add a "Rescan this folder" button with an accessible name to the Selected item tab, using existing theme resources and the `IconRefresh` icon.
- [x] 6.2 Handle Shift-Command-R in `MainWindow` next to Command-I and Command-F.
- [x] 6.3 Show the original scan time and the rescanned-folder list in the result summary, and the "Contains rescanned folders" marker in the history list.
- [x] 6.4 Once `add-item-context-menus` is implemented, add "Rescan this folder" to its folder actions group.
- [ ] 6.5 Manually verify browsing during a folder rescan, preserved expansion, and VoiceOver names on macOS.

## 7. Documentation and roadmap

- [x] 7.1 Update `docs/STORAGE_MEASUREMENT.md` with folder-rescan semantics and the shared-aware and followed-symlink fallback.
- [x] 7.2 Update `docs/FEATURES.md` section 19 and `README.md` for the folder rescan, the shortcut, and the history and export metadata.
- [x] 7.3 Review `docs/index.html` and update it if the landing page should mention folder rescans.
- [x] 7.4 Update the `docs/IMPLEMENTATION_ROADMAP.md` status notes for WP-09.

## 8. Validation

- [x] 8.1 Run `dotnet build MacStorageAtlas.slnx --no-restore`.
- [x] 8.2 Run `dotnet test MacStorageAtlas.slnx --no-build`.
- [x] 8.3 Run `dotnet format MacStorageAtlas.slnx analyzers --diagnostics IDE0005 --verify-no-changes`.
- [x] 8.4 Run `openspec validate --all --strict --no-interactive`.
- [x] 8.5 Run `git diff --check`.
