## Purpose

Define how the MacStorageAtlas main window toolbar presents scan commands,
selected-item actions, cleanup basket actions, export actions, scan options,
result filters, and search while preserving a compact single-row command surface
and existing command behavior.

## Requirements

### Requirement: Main commands use a single toolbar row

MacStorageAtlas SHALL present the main window command surface as a single toolbar row at the default supported window size. The toolbar MUST include the primary scan controls, selected-item actions, cleanup basket actions, export actions, scan options, result filters, and search entry point without splitting those controls into separate toolbar bands at that size.

#### Scenario: Default window shows one toolbar row

- **GIVEN** the main window is opened at its default supported size
- **WHEN** the top command surface is displayed
- **THEN** the toolbar controls are presented in one row
- **AND** options, filters, and search are not displayed in a separate row above the scan controls

#### Scenario: Command grouping remains recognizable

- **GIVEN** the main window toolbar is displayed
- **WHEN** the user scans the toolbar from left to right
- **THEN** scan commands, selected-item actions, cleanup basket actions, export actions, and utility controls are visually distinguishable as groups

### Requirement: Toolbar is separated from content

MacStorageAtlas SHALL render a visible separator between the main toolbar and the content or status area below it.

#### Scenario: Toolbar divider is visible

- **GIVEN** the main window toolbar is displayed
- **WHEN** no transient status banner is visible below the toolbar
- **THEN** a visible separator divides the toolbar from the main content area

#### Scenario: Toolbar divider remains clear with status banners

- **GIVEN** a status or guidance banner is visible below the toolbar
- **WHEN** the top chrome is displayed
- **THEN** the toolbar remains visually separated from the banner area

### Requirement: Narrow widths remain usable

MacStorageAtlas SHALL keep toolbar controls usable when the available width is too narrow to display every command with its full label. The toolbar MUST NOT overlap controls, clip essential command affordances, or increase to an unpredictable height.

#### Scenario: Narrow width does not overlap toolbar controls

- **GIVEN** the main window width is reduced within the supported range
- **WHEN** the toolbar is displayed
- **THEN** toolbar controls do not overlap each other
- **AND** the toolbar remains usable

#### Scenario: Essential controls stay reachable

- **GIVEN** the main window width is reduced within the supported range
- **WHEN** some toolbar controls use compact or overflow presentation
- **THEN** the scan controls, result filters, scan options, and search entry point remain reachable

#### Scenario: Wide width shows direct secondary actions

- **GIVEN** the main window is wide enough to display the full toolbar command set without overlap
- **WHEN** the toolbar is displayed
- **THEN** selected-item actions, cleanup basket actions, and export actions are displayed as direct toolbar controls
- **AND** those actions are not hidden only inside a compact actions menu

### Requirement: Existing command behavior is preserved

MacStorageAtlas SHALL preserve existing toolbar command semantics while changing layout. Moving, grouping, compacting, or overflowing a toolbar control MUST NOT change when its command is enabled, what it invokes, or what data it uses.

#### Scenario: Toolbar layout does not change command enablement

- **GIVEN** a toolbar command is enabled or disabled because of the current scan and selection state
- **WHEN** the toolbar layout changes between full and compact presentation
- **THEN** the command preserves the same enabled or disabled state

#### Scenario: Search behavior is unchanged

- **GIVEN** the user enters search text through the toolbar search entry point
- **WHEN** the search text changes
- **THEN** MacStorageAtlas filters the displayed result tree using the existing search behavior
