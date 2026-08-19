## ADDED Requirements

### Requirement: User can remove one recent location

MacStorageAtlas SHALL let the user remove an individual recent scan location without starting a scan. Removing one recent location MUST update the displayed recent-location list immediately and MUST persist the updated list across application restarts.

#### Scenario: Removing one listed location

- **GIVEN** multiple recent scan locations are displayed
- **WHEN** the user removes one recent location
- **THEN** that location is removed from the displayed recent-location list
- **AND** the remaining recent locations keep their existing order

#### Scenario: Removed location stays removed after restart

- **GIVEN** multiple recent scan locations are stored in settings
- **WHEN** the user removes one recent location and restarts MacStorageAtlas
- **THEN** the removed location is not displayed
- **AND** the remaining recent locations are displayed in their persisted order

#### Scenario: Removing an unknown location is ignored

- **GIVEN** recent scan locations are displayed
- **WHEN** a remove command is invoked for a location that is not in the recent-location list
- **THEN** the displayed recent-location list is unchanged

### Requirement: User can clear all recent locations

MacStorageAtlas SHALL let the user clear every recent scan location without deleting scan history, changing scan results, or modifying files on disk. Clearing recent locations MUST update the displayed recent-location list immediately and MUST persist the empty list across application restarts.

#### Scenario: Clearing a populated list

- **GIVEN** one or more recent scan locations are displayed
- **WHEN** the user clears recent locations
- **THEN** no recent scan locations are displayed

#### Scenario: Cleared locations stay cleared after restart

- **GIVEN** one or more recent scan locations are stored in settings
- **WHEN** the user clears recent locations and restarts MacStorageAtlas
- **THEN** no recent scan locations are displayed

#### Scenario: Clearing an empty list is harmless

- **GIVEN** no recent scan locations are displayed
- **WHEN** a clear recent locations command is invoked
- **THEN** no recent scan locations are displayed
- **AND** no scan is started

### Requirement: Recent-location cleanup preserves unrelated app state

MacStorageAtlas MUST preserve scanner preferences, measurement mode, saved filter presets, window size, scan history settings, stored scan history, and the current scan result when the user removes or clears recent scan locations.

#### Scenario: Removing one location preserves unrelated settings

- **GIVEN** recent scan locations and unrelated app settings are stored
- **WHEN** the user removes one recent location
- **THEN** the updated recent-location list is persisted
- **AND** unrelated app settings are unchanged

#### Scenario: Clearing locations preserves scan history

- **GIVEN** recent scan locations and scan history snapshots are stored
- **WHEN** the user clears recent locations
- **THEN** the recent-location list is empty
- **AND** the stored scan history snapshots remain available

#### Scenario: Clearing locations preserves the current result

- **GIVEN** a scan result is displayed and recent scan locations are displayed
- **WHEN** the user clears recent locations
- **THEN** the current scan result remains displayed
- **AND** no scan is started or cancelled

### Requirement: Recent-location cleanup avoids unsolicited filesystem probing

MacStorageAtlas MUST NOT check whether every saved recent location exists merely because the app loads settings, displays the recent-location list, removes one recent location, or clears the recent-location list. MacStorageAtlas SHALL continue to handle a missing recent location gracefully when the user attempts to scan that location.

#### Scenario: Startup displays stored recent locations without existence checks

- **GIVEN** settings contain recent scan locations
- **WHEN** MacStorageAtlas starts
- **THEN** the recent scan locations are displayed from settings
- **AND** MacStorageAtlas does not remove locations solely because they are unavailable at startup

#### Scenario: Removing one location does not validate remaining locations

- **GIVEN** recent scan locations are displayed
- **WHEN** the user removes one recent location
- **THEN** MacStorageAtlas removes only the chosen stored location
- **AND** MacStorageAtlas does not validate the remaining locations before displaying them

#### Scenario: Missing selected location is still removed

- **GIVEN** a recent scan location no longer exists
- **WHEN** the user attempts to scan that recent location
- **THEN** MacStorageAtlas removes that location from recent locations
- **AND** MacStorageAtlas reports that the location no longer exists
