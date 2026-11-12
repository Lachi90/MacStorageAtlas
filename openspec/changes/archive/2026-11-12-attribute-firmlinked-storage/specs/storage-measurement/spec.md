# Spec Delta

## ADDED Requirements

### Requirement: Firmlink-equivalent paths contribute once per scan

When a scan scope includes both a user-facing firmlinked path and the
Data-volume path that the running system defines as its equivalent, the scan
SHALL attribute that content to the user-facing path and SHALL NOT traverse
the Data-volume alternate. This SHALL hold in every measurement mode and
independently of traversal order.

#### Scenario: Whole-disk shared-aware scan includes a home folder

- **GIVEN** the system defines `/Users` as equivalent to
  `/System/Volumes/Data/Users`
- **AND** both paths resolve to the same directory identity
- **WHEN** the user scans `/` in shared-aware allocated mode
- **THEN** each home folder under `/Users` shows its storage as counted rather
  than as shared through another path
- **AND** `/System/Volumes/Data/Users` is not traversed
- **AND** the counted root total equals the total the same scan reported
  before omission

#### Scenario: Whole-disk logical scan includes firmlinked data

- **GIVEN** the system defines firmlink equivalents for paths inside `/`
- **WHEN** the user scans `/` in logical or per-path allocated mode
- **THEN** content reachable through a firmlinked path contributes once
- **AND** the Data-volume alternate of that content contributes nothing

#### Scenario: Data-volume alternate is enumerated first

- **GIVEN** a scan of `/` would reach `/System/Volumes/Data/Users` before
  `/Users`
- **WHEN** the scan completes
- **THEN** the storage is attributed under `/Users`

### Requirement: Content without a firmlinked equivalent remains scanned

Omitting a Data-volume alternate MUST NOT omit Data-volume content that the
system does not define as reachable through a firmlinked path in the same scan
scope. Such content SHALL remain measured, aggregated, and browsable at its
Data-volume path.

#### Scenario: Data volume contains a directory outside the firmlink list

- **GIVEN** `/System/Volumes/Data` contains a directory that no firmlink
  definition names
- **WHEN** the user scans `/`
- **THEN** that directory is traversed under `/System/Volumes/Data`
- **AND** its bytes contribute to the totals of its ancestors

#### Scenario: Scan scope contains only the Data-volume side

- **GIVEN** the user scans `/System/Volumes/Data`
- **WHEN** the scan runs
- **THEN** every included directory under that root is traversed
- **AND** no content is omitted as firmlink-equivalent

#### Scenario: Scan scope contains only the firmlinked side

- **GIVEN** the user scans `/Users` or a home folder
- **WHEN** the scan runs
- **THEN** the result is the same as before this change

### Requirement: Equivalence is verified before omission

An alternate path SHALL be omitted only when the running system's firmlink
definition names it and both paths currently resolve to the same directory
identity. When the definition is missing, unreadable, or malformed, or the
identities differ or cannot be read, the scan MUST visit both paths and MUST
NOT report a scan error for that check.

#### Scenario: Firmlink definition is unavailable

- **GIVEN** the system firmlink definition cannot be read
- **WHEN** the user scans `/`
- **THEN** both the firmlinked paths and the Data-volume paths are traversed
- **AND** shared-aware accounting still counts each file identity once

#### Scenario: Paths resolve to different directories

- **GIVEN** a firmlink definition names an alternate path
- **AND** the alternate and the user-facing path resolve to different directory
  identities
- **WHEN** the scan runs
- **THEN** the alternate path is traversed

#### Scenario: Scan is cancelled after an omission was decided

- **GIVEN** a scan of `/` has decided to omit a Data-volume alternate
- **WHEN** the user cancels the scan
- **THEN** the scan does not report completion
- **AND** any retained partial result contains no bytes from the omitted
  alternate

### Requirement: Results disclose omitted equivalent paths

A completed or partial result SHALL identify each Data-volume path it omitted
as firmlink-equivalent together with the user-facing path where that content
is shown. The disclosure MUST remain local and MUST NOT read file contents.

#### Scenario: User reviews a whole-disk result

- **GIVEN** a scan of `/` omitted `/System/Volumes/Data/Users` as equivalent to
  `/Users`
- **WHEN** the user reviews the scan result
- **THEN** the result explains that the Data-volume path is shown at `/Users`

#### Scenario: Scan omitted nothing

- **GIVEN** a scan omitted no equivalent paths
- **WHEN** the user reviews the scan result
- **THEN** no omission disclosure is shown
