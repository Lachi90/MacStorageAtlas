# Spec Delta

## MODIFIED Requirements

### Requirement: A completed scan is recorded as one snapshot

Atlas Storage Analyzer SHALL record a snapshot only from a scan that ran to
completion. A cancelled scan and a scan that failed before completing MUST NOT
be recorded.

#### Scenario: A completed scan is captured

- **GIVEN** scan history is enabled
- **WHEN** a scan completes
- **THEN** Atlas Storage Analyzer records one snapshot for that scan
- **AND** the displayed scan result is unchanged
- **AND** no scanned file's contents were read

#### Scenario: A cancelled scan is not captured

- **GIVEN** scan history is enabled
- **WHEN** the user cancels a scan before it completes
- **THEN** Atlas Storage Analyzer records no snapshot
- **AND** the history store is unchanged

#### Scenario: An active filter does not narrow the snapshot

- **GIVEN** scan history is enabled
- **AND** a filter is active when a scan completes
- **WHEN** Atlas Storage Analyzer records the snapshot
- **THEN** the snapshot covers every scanned file and directory
- **AND** it does not omit items the active filter excluded

### Requirement: A snapshot describes every scanned item

Atlas Storage Analyzer SHALL record, for each item in the scan, its full path,
its name, its item kind, its depth below the scan root, its size fields, its
shared-storage indicator, its file extension, its file category, and its
creation, modification, and last-access timestamps. A timestamp the scan could
not determine MUST be recorded as absent and MUST NOT be recorded as a
substitute instant.

#### Scenario: An item is recorded with its metadata

- **GIVEN** a scanned file whose timestamps are known
- **WHEN** the scan is recorded as a snapshot
- **THEN** the snapshot states that file's path, name, kind, and depth
- **AND** it states its extension, category, and size fields
- **AND** it states each timestamp as an unambiguous instant including its
  offset from UTC

#### Scenario: An unknown timestamp is recorded as absent

- **GIVEN** a scanned file whose creation time could not be determined
- **WHEN** the scan is recorded as a snapshot
- **THEN** the snapshot states that the file's creation time is absent
- **AND** the file's remaining known fields are still recorded

#### Scenario: A snapshot reads back unchanged

- **GIVEN** a stored snapshot
- **WHEN** Atlas Storage Analyzer reads it back
- **THEN** every recorded item field equals the value it was written from
- **AND** every recorded scan field equals the value it was written from

### Requirement: A snapshot is never truncated to fit a limit

Atlas Storage Analyzer SHALL record every item of the scan in a snapshot or
record no snapshot at all. It MUST NOT omit, summarize, or roll up items in
order to fit a size limit, because a later reader cannot distinguish an item
that was never recorded from an item that was deleted, and reporting the first
as the second would state a change that never happened.

#### Scenario: A scan too large for the retention limits is declined

- **GIVEN** scan history is enabled
- **AND** a completed scan whose snapshot would exceed the configured total
  store size on its own
- **WHEN** Atlas Storage Analyzer attempts to record it
- **THEN** no snapshot is recorded
- **AND** Atlas Storage Analyzer reports that the scan was too large to record and
  what limit it exceeded
- **AND** the previously stored snapshots are unchanged

#### Scenario: Every scanned item appears in the snapshot

- **GIVEN** a recorded snapshot of a completed scan
- **WHEN** Atlas Storage Analyzer reads it back
- **THEN** the number of recorded items equals the number of items the scan
  found
- **AND** no item was omitted on account of its size or depth

## ADDED Requirements

### Requirement: Capture neither revisits the filesystem nor alters the displayed result

Snapshot capture MUST NOT revisit the filesystem, MUST NOT read the contents of
any scanned file, and MUST NOT alter the displayed scan result.

#### Scenario: A failed capture leaves the displayed result usable

- **GIVEN** scan history is enabled
- **AND** the history store fails while a completed scan is being recorded
- **WHEN** capture ends
- **THEN** Atlas Storage Analyzer reports that the scan was not recorded
- **AND** the displayed scan result remains browsable and can still be exported

### Requirement: The active filter does not narrow a snapshot

A snapshot MUST cover the whole scan result and MUST NOT be narrowed by whatever
filter is active in the user interface when the scan completes, because a
filtered view describes the user's current question rather than the state of the
scanned location.

#### Scenario: A snapshot recorded under a text filter matches an unfiltered one

- **GIVEN** scan history is enabled
- **AND** a text filter that matches only some items is active
- **WHEN** the same location is scanned again and recorded
- **THEN** the snapshot's item count equals the item count recorded for that
  location without a filter

### Requirement: Snapshots record items at the same fidelity for every scan

A snapshot MUST record items at the same fidelity for every scan, so that two
snapshots of the same root describe the same kinds of items.

#### Scenario: Two snapshots of one root carry the same item fields

- **GIVEN** two completed scans of the same root are recorded
- **WHEN** both snapshots are read back
- **THEN** every item in each snapshot carries the same set of recorded fields

### Requirement: A scan too large to record is declined with a reason

When a scan cannot be recorded at full fidelity within the retention limits,
Atlas Storage Analyzer MUST decline to record it and MUST tell the user why.

#### Scenario: A declined scan leaves the displayed result usable

- **GIVEN** scan history is enabled
- **AND** a completed scan whose snapshot would exceed the configured total
  store size on its own
- **WHEN** Atlas Storage Analyzer declines to record it
- **THEN** it reports that the scan was not recorded because it was too large
- **AND** the displayed scan result remains browsable and can still be exported
- **AND** no stored snapshot is pruned to make room for it
