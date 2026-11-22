## ADDED Requirements

### Requirement: A snapshot records each item's cloud availability

Atlas Storage Analyzer SHALL record each item's cloud availability in every snapshot written under schema version 3 or later. Snapshots written under earlier schema versions MUST remain readable and MUST present every item's availability as Unknown.

#### Scenario: Availability reads back unchanged

- **GIVEN** a scan with local and cloud-only files is recorded as a snapshot
- **WHEN** Atlas Storage Analyzer reads the snapshot back
- **THEN** each item's availability equals the value it was written from

#### Scenario: An older snapshot reads as unknown availability

- **GIVEN** a stored snapshot written under schema version 2
- **WHEN** the user views the scan history
- **THEN** the snapshot is listed as readable
- **AND** its items' availability is Unknown
