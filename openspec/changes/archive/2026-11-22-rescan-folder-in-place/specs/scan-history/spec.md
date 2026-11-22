## ADDED Requirements

### Requirement: A completed folder rescan records the whole merged result

Atlas Storage Analyzer SHALL record a completed folder rescan, while scan
history is enabled, as one new snapshot of the whole merged result rooted at
the original scan root. The snapshot MUST follow every rule that applies to a
snapshot of a full scan, including whole-or-nothing writes, no truncation,
retention, completeness, and privacy. A cancelled or failed folder rescan MUST
NOT be recorded.

#### Scenario: A folder rescan is captured as a whole-root snapshot

- **GIVEN** scan history is enabled
- **AND** a completed result for a scan root is displayed
- **WHEN** a folder rescan of one subfolder completes
- **THEN** Atlas Storage Analyzer records one snapshot rooted at the original scan
  root
- **AND** the snapshot covers every item of the merged result

#### Scenario: A cancelled folder rescan is not captured

- **GIVEN** scan history is enabled
- **WHEN** the user cancels a folder rescan
- **THEN** Atlas Storage Analyzer records no snapshot

### Requirement: A snapshot states which folders were rescanned

Atlas Storage Analyzer SHALL record, with every snapshot, the time the original
full scan completed and the list of rescanned folders with the time each rescan
completed. A snapshot of a result without rescanned folders MUST record an empty
list. The scan history MUST visibly distinguish a snapshot that contains
rescanned folders from one that does not.

#### Scenario: A snapshot of a refreshed result lists its refreshed folders

- **GIVEN** a result in which one subfolder was rescanned
- **WHEN** it is recorded as a snapshot
- **AND** Atlas Storage Analyzer reads the snapshot back
- **THEN** the snapshot states the original scan completion time
- **AND** it states the rescanned subfolder and the time its rescan completed

#### Scenario: The history marks refreshed snapshots

- **GIVEN** the history contains one snapshot of a full scan and one of a
  refreshed result
- **WHEN** the user views the scan history
- **THEN** the refreshed snapshot is marked as containing rescanned folders
- **AND** the full-scan snapshot is not

### Requirement: Snapshots written before rescanned folders existed remain readable

Atlas Storage Analyzer SHALL continue to read snapshots written under schema
version 1. It MUST treat such a snapshot as having no rescanned folders and
MUST NOT report it as unreadable.

#### Scenario: A version 1 snapshot reads back

- **GIVEN** a stored snapshot written under schema version 1
- **WHEN** the user views the scan history
- **THEN** the snapshot is listed as readable
- **AND** it is shown as containing no rescanned folders
