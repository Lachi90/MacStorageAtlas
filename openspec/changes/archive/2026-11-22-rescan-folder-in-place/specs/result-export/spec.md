## ADDED Requirements

### Requirement: An export states which folders were rescanned

Atlas Storage Analyzer SHALL record, with every JSON export, the time the
original full scan completed and the list of rescanned folders with the time
each rescan completed. A result without rescanned folders MUST record an empty
list. A CSV export of a result with rescanned folders MUST report, when the
export completes, that the result mixes measurement times and that the JSON
format carries the list of rescanned folders.

#### Scenario: A JSON export lists rescanned folders

- **GIVEN** a result in which one subfolder was rescanned
- **WHEN** the user exports it as JSON
- **THEN** the export metadata states the original scan completion time
- **AND** it states the rescanned subfolder and the time its rescan completed

#### Scenario: A CSV export reports the mixed measurement times

- **GIVEN** a result in which one subfolder was rescanned
- **WHEN** the user exports it as CSV
- **THEN** Atlas Storage Analyzer reports that the result mixes measurement times
- **AND** it states that the JSON export lists the rescanned folders

### Requirement: Exports written before rescanned folders existed remain readable

Atlas Storage Analyzer SHALL continue to read JSON exports written under export
schema version 1 and MUST treat them as having no rescanned folders.

#### Scenario: A version 1 export reads back

- **GIVEN** a JSON export written under export schema version 1
- **WHEN** Atlas Storage Analyzer reads it
- **THEN** it reads successfully
- **AND** it states no rescanned folders
