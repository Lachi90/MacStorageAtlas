## ADDED Requirements

### Requirement: Results can be filtered by cloud availability

Atlas Storage Analyzer SHALL let the user restrict a filter to local files or to cloud-only files. The criterion MUST match files only, MUST combine with the other criteria using AND semantics, MUST NOT match files whose availability is Unknown, and MUST be stored with saved presets. A preset saved without the criterion MUST apply no availability restriction.

#### Scenario: Showing only local files

- **GIVEN** a completed result with local and cloud-only files
- **WHEN** the user filters to local files
- **THEN** only local files are matched
- **AND** Largest files lists only local files

#### Scenario: Combining availability with a size criterion

- **GIVEN** a filter for cloud-only files of at least 100 MB
- **WHEN** the filter is applied
- **THEN** only cloud-only files that meet the size criterion are matched

#### Scenario: An older preset applies no availability restriction

- **GIVEN** a preset saved before the availability criterion existed
- **WHEN** the user applies it
- **THEN** files of every availability can match
