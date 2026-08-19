## Purpose

Define how MacStorageAtlas presents incomplete-scan guidance for macOS access
limitations, directs users to grant Full Disk Access manually, and preserves raw
scan errors, privacy, scan semantics, and cleanup safety boundaries.

## Requirements

### Requirement: Incomplete scans surface access guidance

MacStorageAtlas SHALL surface user-facing access guidance when the current scan result contains permission-related inaccessible paths or when the application cannot determine whether macOS access is sufficient for the selected scan scope. The guidance MUST identify that the scan may be incomplete, MUST state the number of inaccessible paths known from the scan, and MUST NOT describe inaccessible space as purgeable, free, available, or safe to delete.

#### Scenario: Completed scan has permission-related inaccessible paths

- **GIVEN** a scan completes with recoverable errors caused by inaccessible paths
- **WHEN** MacStorageAtlas displays the completed scan result
- **THEN** it shows guidance that the scan may be incomplete
- **AND** it shows how many paths were inaccessible
- **AND** it does not describe those paths as purgeable space or safe cleanup candidates

#### Scenario: Completed scan has no inaccessible paths

- **GIVEN** a scan completes without inaccessible-path errors
- **WHEN** MacStorageAtlas displays the completed scan result
- **THEN** it does not show incomplete-scan access guidance
- **AND** the completed scan result remains visible normally

#### Scenario: Access status is indeterminate

- **GIVEN** MacStorageAtlas cannot determine whether macOS access is sufficient for the selected scan scope
- **WHEN** it presents access guidance
- **THEN** the guidance states that access may be incomplete
- **AND** it does not claim Full Disk Access is granted or denied

### Requirement: Raw scan errors remain available

MacStorageAtlas SHALL keep the normal scan error list visible and usable whenever access guidance is shown. Access guidance MUST summarize the problem without replacing the path-level scan errors, and copied error paths MUST remain the exact paths captured by the scan.

#### Scenario: Guidance appears with detailed errors

- **GIVEN** a completed scan result has inaccessible paths
- **WHEN** MacStorageAtlas shows access guidance
- **THEN** the scan errors view still lists the inaccessible paths
- **AND** each listed error still includes its path, message, and exception type

#### Scenario: Copying an inaccessible path

- **GIVEN** access guidance is visible
- **AND** a scan error is selected in the scan errors view
- **WHEN** the user copies the error path
- **THEN** MacStorageAtlas copies the selected scan error's captured path
- **AND** the access guidance summary does not alter the copied value

### Requirement: Full Disk Access guidance directs the user to macOS settings

MacStorageAtlas SHALL provide an action from the access guidance surface that opens the relevant macOS Privacy & Security settings for granting Full Disk Access when the platform can do so. If the settings action fails or macOS does not support the direct destination, MacStorageAtlas MUST present manual fallback instructions. The guidance MUST state that the user grants access manually in macOS and may need to restart MacStorageAtlas before rescanning.

#### Scenario: Settings action opens successfully

- **GIVEN** access guidance is visible on macOS
- **WHEN** the user chooses to open Full Disk Access settings
- **THEN** MacStorageAtlas opens the relevant Privacy & Security settings
- **AND** it explains that access must be granted manually
- **AND** it explains that the app may need to be restarted before a rescan sees the change

#### Scenario: Settings action fails

- **GIVEN** access guidance is visible on macOS
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

### Requirement: Access status classification is conservative

MacStorageAtlas SHALL classify access guidance using scan evidence and platform checks conservatively. A successful read or enumeration of one test path MUST NOT by itself be treated as proof that Full Disk Access is granted. Permission-related inaccessible paths MUST NOT all be classified as Full Disk Access failures when the evidence is insufficient.

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

### Requirement: Rescanning after access changes reuses the scan lifecycle

MacStorageAtlas SHALL allow the user to rescan the same root from the access guidance surface when a completed scan result exists. The rescan MUST use the existing scan lifecycle, cancellation behavior, scan options, progress reporting, and one-scan-at-a-time protection.

#### Scenario: User rescans after granting access

- **GIVEN** access guidance is visible for a completed scan
- **AND** no scan is currently running
- **WHEN** the user chooses to rescan
- **THEN** MacStorageAtlas starts a new scan for the same root
- **AND** it uses the same scan options as the completed scan
- **AND** scan progress is reported through the normal progress surface

#### Scenario: Rescan is unavailable while scanning

- **GIVEN** a scan is running
- **WHEN** the user views access guidance or scan controls
- **THEN** MacStorageAtlas does not allow a second scan to start

#### Scenario: Rescan preserves existing recovery behavior

- **GIVEN** access guidance is visible for a completed scan
- **WHEN** the user starts a rescan and then cancels it
- **THEN** cancellation is handled through the existing scan cancellation behavior
- **AND** cancellation is not reported as a scan error

### Requirement: Access guidance preserves scan privacy and safety boundaries

MacStorageAtlas SHALL keep access guidance local to the user's Mac and MUST NOT read file contents, hash files, persist protected-path contents, send scan data externally, intentionally materialize cloud placeholders, or perform cleanup actions. Access guidance MUST NOT alter completed scan totals, measurement mode, selected item metadata, filters, exports, Finder reveal behavior, Quick Look behavior, or Trash confirmation behavior.

#### Scenario: Guidance does not inspect file contents

- **GIVEN** MacStorageAtlas evaluates whether to show access guidance
- **WHEN** it examines scan errors or platform access checks
- **THEN** it does not read file contents
- **AND** it does not hash files or persist protected-path contents

#### Scenario: Guidance does not change scan results

- **GIVEN** a completed scan result is displayed with access guidance
- **WHEN** the user views the guidance
- **THEN** scan totals and measurement labels remain based on the completed scan
- **AND** selected-item metadata remains based on the scan-time metadata snapshot
- **AND** active filters and exported values remain based on the same completed scan result

#### Scenario: Guidance does not authorize cleanup

- **GIVEN** access guidance is visible
- **WHEN** the user chooses to move a selected item to Trash
- **THEN** MacStorageAtlas uses the existing recoverable Trash workflow
- **AND** existing confirmation behavior remains required
