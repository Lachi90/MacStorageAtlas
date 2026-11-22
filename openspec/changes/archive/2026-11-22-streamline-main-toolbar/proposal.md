## Why

The main toolbar shows selected-item, cleanup-basket, and export actions
directly only at window widths of 2240 points or more. At the default and
minimum width of 1060 points they are always hidden behind a generic Actions
button. The most frequent item actions therefore cost two clicks on almost
every screen. At the same time the toolbar spends space on controls that
duplicate each other:

- Scan and Rescan invoke the same command with the same enablement.
- Stop sits permanently next to them, although only one of the three is
  meaningful at any time.
- Add to basket and Remove from basket are mutually exclusive for the selected
  item.
- Export CSV and Export JSON are two buttons for one task.

## What Changes

- Replace Scan, Rescan, and Stop with one primary scan control. It reads Scan
  before a result exists for the selected location, Rescan once one does, and
  Stop while a scan or folder rescan runs. It keeps the accent style for Scan
  and Rescan and uses the danger style for Stop.
- Show the selected-item actions Quick Look, Reveal in Finder, and Move to
  Trash, plus one cleanup basket toggle, as direct icon controls at the default
  window size. Each has a tooltip with its name and shortcut and an accessible
  name. The Actions overflow is used only below the default width, if at all.
- Merge Add to basket and Remove from basket into one toggle control whose
  icon, tooltip, and accessible name state the action it will perform. Show the
  basket item count on it.
- Merge Export CSV and Export JSON into one Export menu button.
- Keep Select folder, Options, Filters, History, and Search, and the single-row
  rule and group separators from `main-toolbar-layout`.

Non-goals:

- Do not add or remove commands, and do not change when a command is enabled
  or what it does.
- Do not add the menu bar. That is `add-native-menu-bar`.
- Do not redesign the Options, Filters, or History flyouts.
- Do not change theme colors, typography, or icons beyond adding the icons the
  merged controls need.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `main-toolbar-layout`: A single state-dependent scan control, direct icon
  item actions at the default window size, a basket toggle, and one Export
  menu.

## Impact

Affected areas:

- `src/MacStorageAtlas.App/Views/MainWindow.axaml` and `MainWindow.axaml.cs`:
  Toolbar markup, and removal or lowering of `FullToolbarActionThreshold`.
- `src/MacStorageAtlas.App/ViewModels/MainWindowViewModel.cs`: Read-only
  presentation properties for the scan control state and the basket toggle.
  This overlaps with `add-native-menu-bar`, which needs the same labels;
  whichever change lands first adds them.
- `src/MacStorageAtlas.App/Styles`: Icon-button style and badge style built on
  existing theme resources.
- `tests/MacStorageAtlas.App.Tests`: Tests for the scan control state and the
  toggle mapping.
- Documentation: Screenshots and toolbar descriptions in `README.md`,
  `docs/FEATURES.md`, and `docs/index.html`.

Dependencies:

- Builds on the archived `refine-main-toolbar-layout`.
- Shares `ScanCommandLabel`, `CleanupBasketCommandLabel`, and
  `ToggleSelectedItemInCleanupBasketCommand` with `add-native-menu-bar`.

Risks:

- Icon-only controls are less self-explanatory. Mitigated by tooltips that show
  name and shortcut, accessible names, and keeping text labels on the primary
  scan control and Select folder.
- A control that changes from Scan to Stop can be clicked twice by accident.
  The control ignores a second activation within the same scan transition, and
  Stop keeps partial results.

Roadmap reference:

- Follows `refine-main-toolbar-layout`, which was related to WP-04, WP-05, and
  WP-07. Primary WP: WP-07, because the cleanup basket toggle and the
  selected-item Trash action are the most visible changes.

Estimate:

- 1-2 days.
