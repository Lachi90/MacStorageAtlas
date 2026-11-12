# Spec Delta

## MODIFIED Requirements

### Requirement: Full Disk Access guidance directs the user to macOS settings

Atlas Storage Analyzer SHALL provide an action from the access guidance surface that opens the relevant macOS Privacy & Security settings for granting Full Disk Access when the platform can do so.

#### Scenario: Settings action opens successfully

- **GIVEN** access guidance is visible on macOS
- **AND** Atlas Storage Analyzer does not run inside the App Sandbox
- **WHEN** the user chooses to open Full Disk Access settings
- **THEN** Atlas Storage Analyzer opens the relevant Privacy & Security settings
- **AND** it explains that access must be granted manually
- **AND** it explains that the app may need to be restarted before a rescan sees the change

#### Scenario: Settings action fails

- **GIVEN** access guidance is visible on macOS
- **AND** Atlas Storage Analyzer does not run inside the App Sandbox
- **AND** macOS cannot open the direct settings destination
- **WHEN** the user chooses to open Full Disk Access settings
- **THEN** Atlas Storage Analyzer reports that settings could not be opened automatically
- **AND** it presents manual navigation to Privacy & Security and Full Disk Access
- **AND** the completed scan result remains available

#### Scenario: Full Disk Access cannot be changed inside the app

- **GIVEN** access guidance is visible
- **WHEN** the user reads the guidance
- **THEN** Atlas Storage Analyzer does not request an administrator password
- **AND** it does not claim it can grant Full Disk Access itself

#### Scenario: Sandboxed build guides the user to select the location

- **GIVEN** Atlas Storage Analyzer runs inside the macOS App Sandbox
- **AND** a completed scan has permission-related inaccessible paths
- **WHEN** Atlas Storage Analyzer shows access guidance
- **THEN** the guidance explains that only selected locations can be scanned
- **AND** it directs the user to select the missing location
- **AND** it does not instruct the user to grant Full Disk Access
- **AND** the Full Disk Access settings action is unavailable

#### Scenario: Sandboxed build keeps the rescan action

- **GIVEN** Atlas Storage Analyzer runs inside the macOS App Sandbox
- **AND** access guidance is visible for a completed scan
- **WHEN** the user selects the missing location and rescans from the guidance surface
- **THEN** Atlas Storage Analyzer starts a new scan through the existing scan lifecycle

## ADDED Requirements

### Requirement: Manual Full Disk Access instructions are offered when settings cannot be opened directly

If the settings action fails or macOS does not support the direct destination, Atlas Storage Analyzer MUST present manual fallback instructions.

#### Scenario: Only the general privacy settings open

- **GIVEN** access guidance is visible on macOS
- **AND** Atlas Storage Analyzer does not run inside the App Sandbox
- **AND** macOS opens Privacy & Security but not the direct Full Disk Access destination
- **WHEN** the user chooses to open Full Disk Access settings
- **THEN** Atlas Storage Analyzer presents the manual path System Settings > Privacy & Security > Full Disk Access

### Requirement: Full Disk Access guidance states that access is granted manually

The Full Disk Access guidance MUST state that the user grants access manually in macOS and may need to restart Atlas Storage Analyzer before rescanning.

#### Scenario: Guidance after protected locations were blocked

- **GIVEN** Atlas Storage Analyzer does not run inside the App Sandbox
- **AND** a completed scan indicates that Full Disk Access is likely missing
- **WHEN** Atlas Storage Analyzer shows access guidance
- **THEN** the guidance tells the user to grant Full Disk Access in macOS
- **AND** it tells the user to restart the app if macOS asks before rescanning

### Requirement: Sandboxed builds direct the user to select locations

When Atlas Storage Analyzer runs inside the macOS App Sandbox, Full Disk Access does not widen the app's file access. In that case the guidance MUST explain that only locations the user selects can be scanned, MUST direct the user to select the missing location, MUST NOT offer the Full Disk Access settings action, and MUST NOT present the manual Full Disk Access navigation path.

#### Scenario: Sandboxed guidance omits the manual Full Disk Access path

- **GIVEN** Atlas Storage Analyzer runs inside the macOS App Sandbox
- **AND** access guidance is visible for a completed scan with permission-related inaccessible paths
- **WHEN** the user reads the guidance
- **THEN** it explains that Full Disk Access does not widen what the app can read
- **AND** no manual navigation path to Full Disk Access settings is presented
