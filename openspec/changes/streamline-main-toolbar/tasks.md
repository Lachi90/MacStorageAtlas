## 1. ViewModel presentation state

- [x] 1.1 Add `ScanCommandLabel`, `CleanupBasketCommandLabel`, and `ToggleSelectedItemInCleanupBasketCommand` if `add-native-menu-bar` has not already added them. Already added by `add-native-menu-bar` in `MainWindowViewModel.MenuCommands.cs` and reused without duplication.
- [x] 1.2 Add `PrimaryScanActionCommand`, which dispatches to scan or stop according to scan state and ignores activations during a transition.
- [x] 1.3 Add App ViewModel tests for the Scan, Rescan, and Stop labels, for the dispatch target, for double activation, and for toggle add and remove with unchanged enablement.

## 2. Styles

- [x] 2.1 Add an icon button style and a count badge style in `Styles/Controls.axaml` using existing theme resources.
- [x] 2.2 Add any missing icon geometries to `Styles/Icons.axaml`.

## 3. Toolbar markup

- [x] 3.1 Replace Scan, Rescan, and Stop with the primary scan control.
- [x] 3.2 Replace the item, basket, and export buttons with icon controls, the basket toggle with badge, and the Export menu button, each with a tooltip and accessible name.
- [x] 3.3 Measure the single-row overflow width on Apple Silicon and Intel or Rosetta, lower `FullToolbarActionThreshold` to it, and keep the compact Actions button only below it. Measured through Avalonia layout without showing the window, natively on Apple Silicon and as an x86_64 build under Rosetta; see design.md. No physical Intel Mac was available.
- [x] 3.4 Add App tests proving the toolbar controls bind the intended commands.

## 4. Verification and documentation

- [ ] 4.1 Visually verify the toolbar at the default, minimum, and a wide window size in light and dark appearance.
- [ ] 4.2 Verify tooltips and VoiceOver names for every icon control.
- [ ] 4.3 Update the screenshots and toolbar descriptions in `README.md`, `docs/FEATURES.md`, and `docs/index.html`. Toolbar descriptions are updated; the screenshots still show the previous toolbar and need to be regenerated from the running app.

## 5. Validation

- [x] 5.1 Run `dotnet build MacStorageAtlas.slnx --no-restore`.
- [x] 5.2 Run `dotnet test MacStorageAtlas.slnx --no-build`.
- [x] 5.3 Run `dotnet format MacStorageAtlas.slnx analyzers --diagnostics IDE0005 --verify-no-changes`.
- [x] 5.4 Run `openspec validate --all --strict --no-interactive`.
- [x] 5.5 Run `git diff --check`.
