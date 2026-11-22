## Context

`MainWindow.axaml` renders Select folder, Scan, Rescan, and Stop. It then
renders either `FullActionGroup` (Reveal in Finder, Quick Look, Move to Trash,
Add to basket, Remove from basket, Export CSV, Export JSON as labeled buttons)
or `CompactActionsButton`. `MainWindow.axaml.cs` switches between them at
`FullToolbarActionThreshold = 2240`. The default and minimum window width is
1060, so users effectively always see the compact Actions button.
`RescanCommand` delegates to `ScanFolderCommand` with the same can-execute
method.

## Goals / Non-Goals

Goals:

- Direct one-click item actions at the default window size.
- Fewer, state-aware controls without changing any command's semantics.

Non-Goals:

- New commands, flyout redesign, or theme changes.

## Decisions

### Primary scan control

Use one `Button` bound to `PrimaryScanActionCommand`. Its label, tooltip,
icon, and style come from ViewModel state: `PrimaryScanActionLabel` reads
`ScanCommandLabel` or Stop, `PrimaryScanActionToolTip` names the shortcut,
`IsPrimaryScanActionScan`, `IsPrimaryScanActionRescan`, and
`IsPrimaryScanActionStop` select the icon, and `IsScanning` switches the
`accent` class to `danger`. `PrimaryScanActionCommand` dispatches to
`StopScanCommand` while `IsScanning` is true and to `ScanFolderCommand`
otherwise, and it is enabled exactly when that target is, because it forwards
both targets' `CanExecuteChanged`. Folder rescans set `IsScanning` and assign
the shared `_scanCancellation`, so Stop also cancels a folder rescan.
`RescanCommand` stays for the menu bar and existing tests.

The transition is the double-click interval after an accepted activation:
`PrimaryScanActionCommand` ignores an activation that arrives within
`PrimaryScanActionTransitionInterval` (500 milliseconds, the macOS default
double-click interval) of the previous accepted one. The interval is measured
with a `TimeProvider` injected through the ViewModel constructor, so the rule
is testable without the view. A scan start that is still pending is already
covered by `ScanFolderCommand` being disabled while it runs.

Alternative considered: a split button with Scan as the main action and Rescan
and Stop in its dropdown. Rejected, because Stop must be one click away while
scanning.

### Icon controls with tooltips

Add an `icon` button class in `Styles/Controls.axaml`. Quick Look, Reveal in
Finder, Move to Trash, the basket toggle, and the Export menu are icon
controls with `ToolTip.Tip` showing name and shortcut, `ToolTip.ShowOnDisabled`
so the name is discoverable before an item is selected, and
`AutomationProperties.Name`. Move to Trash keeps the `danger` class.

### Fitting the single row at 1060 points

The toolbar must hold Select folder, the scan control, five icon controls,
Options, History, Filters, and Search in one row at the default and minimum
width of 1060 points. With the previous labeled Export button, the default
button padding of 14 by 7, and a fixed 220-point search field, the row needs
1209.5 points with a four-digit basket count, so it would overlap. Three
adjustments keep Options, History, and Filters labeled and avoid new icons:

- Export is an icon control with a menu, which saves 60 points.
- Labeled toolbar buttons use the `toolbar` class with a padding of 10 by 7,
  which saves 8 points per button. The scan control's `primary-scan` class
  sets a minimum width of 96 points, so Scan, Rescan, and Stop keep one width.
- The search field sits in its own `100*` column with a minimum width of 140
  and a maximum width of 220 points. It keeps 220 points on wide windows and
  shrinks before the row would overflow. 140 points still show the whole
  "Search name or path" placeholder, which needs 113 points of text plus the
  text box padding.

The overflow width was measured with a scratch console program, which is not
part of the repository. It loads `MainWindow` with `SetupWithoutStarting`,
measures and arranges the toolbar through Avalonia layout with the bundled
Inter font, and lowers the width in half-point steps until a control extends
past the row. The window itself is never shown. The parts of the narrowest row
without a basket count, at 991.5 points, are:

| Part | Points |
| --- | --- |
| Border padding, 16 on each side | 32 |
| Six column gaps of 10 | 60 |
| Select folder, 8-point gap, scan control | 231 |
| Group divider, 1 point plus 2-point margins | 5 |
| Five 35-point icon controls, two dividers, six 8-point gaps | 233 |
| Empty spacer column | 0 |
| Group divider | 5 |
| Options, History, Filters, two 8-point gaps | 285.5 |
| Search field at its minimum | 140 |
| Narrowest row without a badge | 991.5 |

The basket badge widens the toggle from 35 to 56.5 points for one digit and
by 7 points per further digit, so the row needs 1013 points with one digit,
1027 with three, and 1041.5 with five. Inter ships with the app and is shaped
by Skia and HarfBuzz on both architectures, and the measurement returned the
same values natively on Apple Silicon and as an x86_64 build under Rosetta.
A physical Intel Mac was not available.
The measurement ran at a render scale of 2. On a display with a scale of 1,
layout rounding can add up to about half a point per control, so
`FullToolbarActionThreshold` is 1050 points: the widest measured row plus an
8-point rounding allowance. The compact Actions button remains for widths
below that, which the 1060-point minimum window width does not reach. The
toolbar height stays 50 points in every measured state, because the badge is
15 points high and fits inside the icon button.

### Basket toggle and Export menu

The basket toggle binds `ToggleSelectedItemInCleanupBasketCommand` and
`CleanupBasketCommandLabel`, which `add-native-menu-bar` also uses.
`CleanupBasketToggleRemovesItem` selects the add or remove icon, and
`CleanupBasketToggleToolTip` adds the Command-B shortcut to the label. A
`count-badge` shows `CleanupBasketItemCount` while `HasCleanupBasketItems` is
true. Export is a `Button` with a `MenuFlyout` that lists CSV and JSON, binds
the existing export commands, and is enabled through `IsExportAvailable`,
which reads the same `CanExport` rule as the export commands and is raised
whenever they re-evaluate.

## Risks / Trade-offs

- [Icons alone are ambiguous] → Tooltips, accessible names, and text labels on
  the two primary controls.
- [State-changing control misclicks] → Transition guard plus the existing
  partial-result behavior on Stop.

## Migration Plan

No data migration. Rolling back restores the previous toolbar markup.

## Open Questions

- None blocking.
