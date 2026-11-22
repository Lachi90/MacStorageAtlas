## ADDED Requirements

### Requirement: One scan control follows the scan state

Atlas Storage Analyzer SHALL present starting, repeating, and stopping a scan as
one primary toolbar control. The control MUST read Scan while no completed
result for the selected location is displayed, Rescan while one is displayed,
and Stop while a scan or folder rescan runs. It MUST invoke the command matching
the label it shows, and a second activation during the same transition MUST
have no effect.

#### Scenario: The control starts a scan

- **GIVEN** a folder is selected and no result is displayed
- **WHEN** the user views the toolbar
- **THEN** the scan control reads Scan
- **AND** activating it starts a scan of that folder

#### Scenario: The control becomes Stop while scanning

- **GIVEN** a scan is running
- **WHEN** the user views the toolbar
- **THEN** the scan control reads Stop
- **AND** activating it stops the scan with the existing partial-result
  behavior

#### Scenario: The control stops a folder rescan

- **GIVEN** a folder of the displayed result is being rescanned
- **WHEN** the user activates the scan control, which reads Stop
- **THEN** the folder rescan stops
- **AND** the displayed result is not changed

#### Scenario: The control offers Rescan after a result

- **GIVEN** a completed result for the selected folder is displayed
- **WHEN** the user views the toolbar
- **THEN** the scan control reads Rescan

#### Scenario: A double activation does not restart the scan

- **GIVEN** the scan control reads Scan
- **WHEN** the user activates it twice in quick succession
- **THEN** exactly one scan is started
- **AND** that scan is not stopped by the second activation

### Requirement: The cleanup basket action is one toggle

Atlas Storage Analyzer SHALL present adding the selected item to the cleanup
basket and removing it as one toolbar control. Its icon, tooltip, and
accessible name MUST state the action it will perform for the selected item,
and it MUST show the number of items in the basket.

#### Scenario: The toggle adds an item

- **GIVEN** the selected item is not in the cleanup basket
- **WHEN** the user activates the basket control
- **THEN** the item is added to the basket
- **AND** the control now states that it removes the item

#### Scenario: The toggle shows the basket count

- **GIVEN** the cleanup basket contains three items
- **WHEN** the user views the toolbar
- **THEN** the basket control shows the count three

### Requirement: Export formats share one control

Atlas Storage Analyzer SHALL offer CSV and JSON export from one Export toolbar
control that opens a choice between the formats. The control MUST be enabled
exactly when export is available.

#### Scenario: Choosing a format from the Export control

- **GIVEN** a completed result is displayed
- **WHEN** the user opens the Export control and chooses JSON
- **THEN** Atlas Storage Analyzer starts the existing JSON export

### Requirement: Icon-only controls explain themselves

Atlas Storage Analyzer SHALL give every icon-only toolbar control a tooltip
with its action name and, where one exists, its keyboard shortcut, and an
accessible name that states its action.

#### Scenario: Hovering an icon control

- **GIVEN** the toolbar shows the Reveal in Finder icon control
- **WHEN** the pointer rests on it
- **THEN** a tooltip names the action and its shortcut

#### Scenario: VoiceOver reads an icon control

- **GIVEN** VoiceOver is running
- **WHEN** focus moves to the Move to Trash icon control
- **THEN** VoiceOver announces Move to Trash

## MODIFIED Requirements

### Requirement: Narrow widths remain usable

Atlas Storage Analyzer SHALL keep toolbar controls usable when the available width is too narrow to display every command with its full label. The toolbar MUST NOT overlap controls, clip essential command affordances, or increase to an unpredictable height. At the default supported window size, the selected-item actions, the cleanup basket toggle, and the Export control MUST be displayed as direct toolbar controls and MUST NOT be reachable only through a compact actions menu.

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

#### Scenario: Default width shows direct item actions

- **GIVEN** the main window is opened at its default supported size
- **WHEN** the toolbar is displayed
- **THEN** Quick Look, Reveal in Finder, the cleanup basket toggle, Move to Trash, and Export are direct toolbar controls
- **AND** none of them is reachable only through a compact actions menu
