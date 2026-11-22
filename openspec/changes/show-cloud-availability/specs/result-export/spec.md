## ADDED Requirements

### Requirement: Exported rows state each item's cloud availability

Atlas Storage Analyzer SHALL write each item's cloud availability in JSON exports under export schema version 3 and as an availability column in CSV exports. JSON exports written under earlier export schema versions MUST remain readable and MUST present every item's availability as Unknown.

#### Scenario: A CSV export carries availability

- **GIVEN** a completed result with a cloud-only file
- **WHEN** the user exports it as CSV
- **THEN** that file's row states Cloud only in the availability column

#### Scenario: An older JSON export reads as unknown availability

- **GIVEN** a JSON export written under export schema version 2
- **WHEN** Atlas Storage Analyzer reads it
- **THEN** it reads successfully
- **AND** every item's availability is Unknown
