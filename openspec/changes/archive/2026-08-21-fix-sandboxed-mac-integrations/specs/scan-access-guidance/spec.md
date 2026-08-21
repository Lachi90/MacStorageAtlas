## MODIFIED Requirements

### Requirement: Full Disk Access guidance directs the user to macOS settings

MacStorageAtlas SHALL provide an action from the access guidance surface that opens the relevant macOS Privacy & Security settings for granting Full Disk Access when the platform can do so. If the settings action fails or macOS does not support the direct destination, MacStorageAtlas MUST present manual fallback instructions. The guidance MUST state that the user grants access manually in macOS and may need to restart MacStorageAtlas before rescanning.

When MacStorageAtlas runs inside the macOS App Sandbox, Full Disk Access does not widen the app's file access. In that case the guidance MUST explain that only locations the user selects can be scanned, MUST direct the user to select the missing location, MUST NOT offer the Full Disk Access settings action, and MUST NOT present the manual Full Disk Access navigation path.

#### Scenario: Settings action opens successfully

- **GIVEN** access guidance is visible on macOS
- **AND** MacStorageAtlas does not run inside the App Sandbox
- **WHEN** the user chooses to open Full Disk Access settings
- **THEN** MacStorageAtlas opens the relevant Privacy & Security settings
- **AND** it explains that access must be granted manually
- **AND** it explains that the app may need to be restarted before a rescan sees the change

#### Scenario: Settings action fails

- **GIVEN** access guidance is visible on macOS
- **AND** MacStorageAtlas does not run inside the App Sandbox
- **AND** macOS cannot open the direct settings destination
- **WHEN** the user chooses to open Full Disk Access settings
- **THEN** MacStorageAtlas reports that settings could not be opened automatically
- **AND** it presents manual navigation to Privacy & Security and Full Disk Access
- **AND** the completed scan result remains available

#### Scenario: Full Disk Access cannot be changed inside the app

- **GIVEN** access guidance is visible
- **WHEN** the user reads the guidance
- **THEN** MacStorageAtlas does not request an administrator password
- **AND** it does not claim it can grant Full Disk Access itself

#### Scenario: Sandboxed build guides the user to select the location

- **GIVEN** MacStorageAtlas runs inside the macOS App Sandbox
- **AND** a completed scan has permission-related inaccessible paths
- **WHEN** MacStorageAtlas shows access guidance
- **THEN** the guidance explains that only selected locations can be scanned
- **AND** it directs the user to select the missing location
- **AND** it does not instruct the user to grant Full Disk Access
- **AND** the Full Disk Access settings action is unavailable

#### Scenario: Sandboxed build keeps the rescan action

- **GIVEN** MacStorageAtlas runs inside the macOS App Sandbox
- **AND** access guidance is visible for a completed scan
- **WHEN** the user selects the missing location and rescans from the guidance surface
- **THEN** MacStorageAtlas starts a new scan through the existing scan lifecycle

### Requirement: Access status classification is conservative

MacStorageAtlas SHALL classify access guidance using scan evidence and platform checks conservatively. A successful read or enumeration of one test path MUST NOT by itself be treated as proof that Full Disk Access is granted. Permission-related inaccessible paths MUST NOT all be classified as Full Disk Access failures when the evidence is insufficient. A sandboxed build MUST NOT classify inaccessible paths as missing Full Disk Access, because that setting cannot change the outcome.

#### Scenario: One readable probe does not prove access

- **GIVEN** a platform access check can read one protected or representative path
- **WHEN** MacStorageAtlas classifies the current access state
- **THEN** it does not report Full Disk Access as definitely granted solely from that result

#### Scenario: Non-privacy errors remain separate

- **GIVEN** a scan completes with recoverable errors that are not permission-related inaccessible-path errors
- **WHEN** MacStorageAtlas classifies the scan result
- **THEN** it does not present those errors as likely missing Full Disk Access
- **AND** the normal scan errors view remains available

#### Scenario: Mixed evidence is reported without certainty

- **GIVEN** a scan includes both permission-related inaccessible paths and other recoverable IO errors
- **WHEN** MacStorageAtlas classifies the scan result
- **THEN** it may recommend checking Full Disk Access
- **AND** it keeps the other scan errors visible without reclassifying them

#### Scenario: Sandboxed build is never classified as missing Full Disk Access

- **GIVEN** MacStorageAtlas runs inside the macOS App Sandbox
- **AND** a completed scan has permission-related inaccessible paths
- **WHEN** MacStorageAtlas classifies the scan result
- **THEN** it classifies the result as requiring the user to select the location
- **AND** it does not classify the result as likely missing Full Disk Access

#### Scenario: Sandboxed build without inaccessible paths shows no guidance

- **GIVEN** MacStorageAtlas runs inside the macOS App Sandbox
- **AND** a scan completes without permission-related inaccessible paths
- **WHEN** MacStorageAtlas classifies the scan result
- **THEN** it shows no access guidance
- **AND** it does not report the access status as indeterminate
