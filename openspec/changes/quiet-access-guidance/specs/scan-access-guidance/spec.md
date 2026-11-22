## MODIFIED Requirements

### Requirement: Incomplete scans surface access guidance

Atlas Storage Analyzer SHALL surface user-facing access guidance only when the current scan result contains permission-related inaccessible paths. A scan result without such paths MUST NOT show access guidance, even when the application cannot determine whether macOS access is sufficient. The guidance MUST identify that the scan may be incomplete, MUST state the number of inaccessible paths known from the scan, and MUST NOT describe inaccessible space as purgeable, free, available, or safe to delete.

#### Scenario: Completed scan has permission-related inaccessible paths

- **GIVEN** a scan completes with recoverable errors caused by inaccessible paths
- **WHEN** Atlas Storage Analyzer displays the completed scan result
- **THEN** it shows guidance that the scan may be incomplete
- **AND** it shows how many paths were inaccessible
- **AND** it does not describe those paths as purgeable space or safe cleanup candidates

#### Scenario: Completed scan has no inaccessible paths

- **GIVEN** a scan completes without inaccessible-path errors
- **WHEN** Atlas Storage Analyzer displays the completed scan result
- **THEN** it does not show incomplete-scan access guidance
- **AND** the completed scan result remains visible normally

#### Scenario: Access status is indeterminate

- **GIVEN** Atlas Storage Analyzer cannot determine whether macOS access is sufficient
- **AND** a scan completes without permission-related inaccessible paths
- **WHEN** Atlas Storage Analyzer displays the completed scan result
- **THEN** it shows no access guidance
- **AND** it does not claim Full Disk Access is granted or denied
