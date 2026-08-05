## Purpose

Define how MacStorageAtlas records completed scans as local snapshots once the
user opts in: what each snapshot states about every scanned item, what
measurement basis, scan options, recoverable errors, and completeness verdict
identify the scan it came from, how snapshots are written whole or not at all
and never truncated to fit a limit, how retention bounds the store by snapshot
count and total size, how the user inspects and removes stored history, how the
store tolerates unreadable snapshots and changes made outside the application,
how each snapshot declares its schema version, and how capture stays
responsive, cancellable, and private to the user's machine.

## Requirements

### Requirement: Scan history is off until the user turns it on

MacStorageAtlas SHALL NOT record any scan to the history store until the user
has enabled scan history. While history is disabled, MacStorageAtlas MUST NOT
create the history store, MUST NOT write any scanned path to it, and MUST
behave exactly as it does today when a scan completes. Turning history off MUST
stop further capture and MUST leave already stored snapshots untouched until
the user removes them.

#### Scenario: A scan completes while history is disabled

- **GIVEN** the user has not enabled scan history
- **WHEN** a scan completes
- **THEN** MacStorageAtlas records nothing to the history store
- **AND** no scanned path is written outside the running application

#### Scenario: Enabling history begins capture

- **GIVEN** the user enables scan history
- **WHEN** the next scan completes
- **THEN** MacStorageAtlas records that scan as a snapshot

#### Scenario: Disabling history stops capture and keeps stored snapshots

- **GIVEN** scan history is enabled and snapshots are stored
- **WHEN** the user disables scan history
- **AND** a later scan completes
- **THEN** MacStorageAtlas records no new snapshot
- **AND** the previously stored snapshots remain stored

### Requirement: A completed scan is recorded as one snapshot

MacStorageAtlas SHALL record a snapshot only from a scan that ran to
completion. A cancelled scan and a scan that failed before completing MUST NOT
be recorded. Capture MUST NOT revisit the filesystem, MUST NOT read the
contents of any scanned file, and MUST NOT alter the displayed scan result. A
snapshot MUST cover the whole scan result and MUST NOT be narrowed by whatever
filter is active in the user interface when the scan completes, because a
filtered view describes the user's current question rather than the state of
the scanned location.

#### Scenario: A completed scan is captured

- **GIVEN** scan history is enabled
- **WHEN** a scan completes
- **THEN** MacStorageAtlas records one snapshot for that scan
- **AND** the displayed scan result is unchanged
- **AND** no scanned file's contents were read

#### Scenario: A cancelled scan is not captured

- **GIVEN** scan history is enabled
- **WHEN** the user cancels a scan before it completes
- **THEN** MacStorageAtlas records no snapshot
- **AND** the history store is unchanged

#### Scenario: An active filter does not narrow the snapshot

- **GIVEN** scan history is enabled
- **AND** a filter is active when a scan completes
- **WHEN** MacStorageAtlas records the snapshot
- **THEN** the snapshot covers every scanned file and directory
- **AND** it does not omit items the active filter excluded

### Requirement: A snapshot describes every scanned item

MacStorageAtlas SHALL record, for each item in the scan, its full path, its
name, its item kind, its depth below the scan root, its size fields, its
shared-storage indicator, its file extension, its file category, and its
creation, modification, and last-access timestamps. A timestamp the scan could
not determine MUST be recorded as absent and MUST NOT be recorded as a
substitute instant. A snapshot MUST record items at the same fidelity for every
scan, so that two snapshots of the same root describe the same kinds of items.

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
- **WHEN** MacStorageAtlas reads it back
- **THEN** every recorded item field equals the value it was written from
- **AND** every recorded scan field equals the value it was written from

### Requirement: A snapshot states the basis on which its scan was measured

MacStorageAtlas SHALL record, with every snapshot, the scan root path, the time
the scan completed, the scan options that produced the result, the measurement
mode, the clone-accounting coverage, the number of recorded items, and the
total counted bytes. These fields exist so that a later reader can tell whether
two snapshots describe comparable measurements, and MacStorageAtlas MUST NOT
record a snapshot that omits any of them.

#### Scenario: A snapshot identifies its scan after the fact

- **GIVEN** a stored snapshot
- **WHEN** MacStorageAtlas reads it back
- **THEN** the snapshot states the scan root and the scan completion time
- **AND** it states the scan options that produced the result
- **AND** it states the measurement mode and the clone-accounting coverage

#### Scenario: Snapshots taken under different options remain distinguishable

- **GIVEN** two snapshots of the same scan root recorded under different scan
  options
- **WHEN** MacStorageAtlas reads both back
- **THEN** each snapshot states the options that produced it
- **AND** the difference between them is discoverable without rescanning

### Requirement: A snapshot states whether its scan could see everything

MacStorageAtlas SHALL record, with every snapshot, every recoverable error from
the scan together with the path it occurred on, and a completeness verdict
stating whether the scan read every location it walked. A scan that was blocked
from reading part of the scanned location measures what the application could
see rather than what is on the disk, and a snapshot MUST carry that distinction
so that a later reader is never able to mistake an unreadable subtree for an
absent one.

#### Scenario: A snapshot of a complete scan is marked complete

- **GIVEN** a scan that completed with no recoverable errors and full access to
  the scanned location
- **WHEN** the scan is recorded as a snapshot
- **THEN** the snapshot states that the scan was complete

#### Scenario: A snapshot of a partial scan records what it could not read

- **GIVEN** a scan that completed with paths it could not read
- **WHEN** the scan is recorded as a snapshot
- **THEN** the snapshot states that the scan was incomplete
- **AND** it lists each unreadable path and what failed on it

#### Scenario: A partial scan is visibly distinguished in the history

- **GIVEN** a stored snapshot recorded from a scan that could not read part of
  the scanned location
- **WHEN** the user views the stored history
- **THEN** MacStorageAtlas indicates that the scan behind that snapshot was
  incomplete

### Requirement: A snapshot is recorded whole or not at all

MacStorageAtlas SHALL add a snapshot to the history store only after the whole
snapshot has been written. If capture is cancelled or the write fails,
MacStorageAtlas MUST NOT leave a partial snapshot in the store, MUST leave the
rest of the history unchanged, and MUST report what went wrong rather than
reporting that the scan was recorded. A failed capture MUST NOT invalidate or
alter the displayed scan result.

#### Scenario: A write failure leaves no partial snapshot

- **GIVEN** scan history is enabled
- **WHEN** capture fails part-way through writing
- **THEN** no partial snapshot remains in the store
- **AND** MacStorageAtlas reports that the scan was not recorded and why
- **AND** the previously stored snapshots are unchanged

#### Scenario: A failed capture leaves the result usable

- **GIVEN** a scan has completed and is displayed
- **WHEN** capture fails
- **THEN** the displayed scan result is unchanged
- **AND** the user can still browse, filter, export, and clean up from it

### Requirement: A snapshot is never truncated to fit a limit

MacStorageAtlas SHALL record every item of the scan in a snapshot or record no
snapshot at all. It MUST NOT omit, summarize, or roll up items in order to fit
a size limit, because a later reader cannot distinguish an item that was never
recorded from an item that was deleted, and reporting the first as the second
would state a change that never happened. When a scan cannot be recorded at
full fidelity within the retention limits, MacStorageAtlas MUST decline to
record it and MUST tell the user why.

#### Scenario: A scan too large for the retention limits is declined

- **GIVEN** scan history is enabled
- **AND** a completed scan whose snapshot would exceed the configured total
  store size on its own
- **WHEN** MacStorageAtlas attempts to record it
- **THEN** no snapshot is recorded
- **AND** MacStorageAtlas reports that the scan was too large to record and
  what limit it exceeded
- **AND** the previously stored snapshots are unchanged

#### Scenario: Every scanned item appears in the snapshot

- **GIVEN** a recorded snapshot of a completed scan
- **WHEN** MacStorageAtlas reads it back
- **THEN** the number of recorded items equals the number of items the scan
  found
- **AND** no item was omitted on account of its size or depth

### Requirement: Retention bounds the history store

MacStorageAtlas SHALL bound the history store by a maximum number of snapshots
per scan root and by a maximum total size across the whole store. When
recording a new snapshot would exceed either limit, MacStorageAtlas MUST remove
the oldest snapshots first until the store is within both limits, and MUST NOT
remove a snapshot that it is not required to remove. The user MUST be able to
change both limits, and lowering a limit MUST bring the store within it.

#### Scenario: Exceeding the snapshot count prunes the oldest

- **GIVEN** a scan root whose stored snapshots have reached the maximum count
- **WHEN** a new scan of that root is recorded
- **THEN** the oldest snapshot for that root is removed
- **AND** the new snapshot is stored
- **AND** snapshots of other scan roots are unchanged

#### Scenario: Exceeding the total store size prunes the oldest

- **GIVEN** a history store at its maximum total size
- **WHEN** a new scan is recorded
- **THEN** MacStorageAtlas removes the oldest snapshots until the store is
  within its total size limit
- **AND** it removes no more snapshots than required

#### Scenario: Lowering a limit brings the store within it

- **GIVEN** a history store within its configured limits
- **WHEN** the user lowers the maximum number of snapshots per scan root
- **THEN** MacStorageAtlas removes the oldest snapshots until the store is
  within the new limit

### Requirement: The user can inspect the stored history

MacStorageAtlas SHALL show the user what the history store contains, grouped by
scan root. For each stored snapshot it MUST state when the scan completed, how
many items it covers, how much space it occupies in the store, the measurement
mode it was recorded under, and whether the scan behind it was complete.
MacStorageAtlas MUST also state where the store is located on disk and how much
space it occupies in total.

#### Scenario: Viewing stored snapshots

- **GIVEN** snapshots are stored for more than one scan root
- **WHEN** the user views the scan history
- **THEN** MacStorageAtlas lists the snapshots grouped by scan root
- **AND** each entry states its completion time, item count, stored size, and
  measurement mode

#### Scenario: The store's location and size are discoverable

- **GIVEN** scan history is enabled
- **WHEN** the user views the scan history
- **THEN** MacStorageAtlas states where the history store is located
- **AND** it states how much space the store occupies in total

#### Scenario: The store can be opened in the file browser

- **GIVEN** snapshots are stored
- **WHEN** the user asks to see the history store on disk
- **THEN** MacStorageAtlas reveals the store in the system file browser
- **AND** the user can remove stored snapshots there without using the
  application

#### Scenario: Revealing is unavailable while nothing is stored

- **GIVEN** no snapshot has been recorded
- **WHEN** the user views the scan history
- **THEN** the action that reveals the store on disk is unavailable

#### Scenario: An empty history says so

- **GIVEN** no snapshot has been recorded
- **WHEN** the user views the scan history
- **THEN** MacStorageAtlas reports that no scans have been recorded

### Requirement: The user can remove stored history

MacStorageAtlas SHALL let the user delete an individual snapshot and clear the
entire history store. Clearing history MUST remove every stored snapshot and
MUST NOT change the user's scan options, measurement mode, saved filter
presets, recent locations, window size, or whether scan history is enabled.
Removal MUST take effect immediately and MUST NOT require restarting the
application.

#### Scenario: Deleting one snapshot

- **GIVEN** several snapshots are stored
- **WHEN** the user deletes one snapshot
- **THEN** that snapshot is removed from the store
- **AND** the remaining snapshots are unchanged

#### Scenario: Clearing the whole history

- **GIVEN** snapshots are stored
- **WHEN** the user clears the scan history
- **THEN** the store contains no snapshots
- **AND** the user's scan options, measurement mode, filter presets, and recent
  locations are unchanged

#### Scenario: Clearing history leaves the setting enabled

- **GIVEN** scan history is enabled and snapshots are stored
- **WHEN** the user clears the scan history
- **AND** a later scan completes
- **THEN** MacStorageAtlas records that scan as a snapshot

### Requirement: The store survives an unreadable snapshot

MacStorageAtlas SHALL keep the rest of the history usable when one stored
snapshot cannot be read. It MUST report the unreadable snapshot to the user,
MUST let the user remove it, and MUST NOT delete it automatically, discard the
whole store, or fail to start on account of it.

#### Scenario: A corrupt snapshot does not break the history

- **GIVEN** several stored snapshots, one of which is corrupt
- **WHEN** the user views the scan history
- **THEN** MacStorageAtlas lists the readable snapshots
- **AND** it reports that one snapshot could not be read
- **AND** it does not delete the unreadable snapshot on its own

#### Scenario: A corrupt store does not block scanning

- **GIVEN** the history store cannot be read at all
- **WHEN** the user starts the application and runs a scan
- **THEN** the scan runs and completes normally
- **AND** MacStorageAtlas reports that the history store could not be read

### Requirement: The store tolerates being changed outside the application

MacStorageAtlas SHALL treat the history store as a plain directory that the
user may inspect and modify with the file browser or the shell, because
removing stored scan data must never require the application to be running.
Removing the store, or any snapshot within it, from outside MacStorageAtlas
MUST NOT cause an error, MUST NOT be reported as a corrupt snapshot, and MUST
NOT prevent a later scan from being recorded.

#### Scenario: The store is removed while the application is running

- **GIVEN** snapshots are stored and the user is viewing the scan history
- **WHEN** the store is removed outside MacStorageAtlas
- **AND** the user views the scan history again
- **THEN** MacStorageAtlas reports that no scans have been recorded
- **AND** it does not report a failure

#### Scenario: Recording resumes after the store is removed

- **GIVEN** the store has been removed outside MacStorageAtlas
- **AND** scan history is enabled
- **WHEN** a scan completes
- **THEN** MacStorageAtlas records that scan as a snapshot

#### Scenario: A snapshot removed mid-listing is not reported as corrupt

- **GIVEN** snapshots are stored
- **WHEN** one snapshot is removed outside MacStorageAtlas while the history is
  being listed
- **THEN** that snapshot is absent from the listing
- **AND** it is not presented as a snapshot that could not be read

### Requirement: A snapshot states its own schema version

MacStorageAtlas SHALL record an explicit schema version with every snapshot and
MUST change that version whenever a recorded field is added, removed, or
redefined. When it encounters a snapshot whose schema version it cannot read,
MacStorageAtlas MUST report which version the snapshot uses and what the user
can do about it, and MUST NOT interpret the snapshot's fields as though they
were the shape it expects.

#### Scenario: A snapshot records its version

- **GIVEN** a recorded snapshot
- **WHEN** MacStorageAtlas reads it back
- **THEN** the snapshot states the schema version it was written under

#### Scenario: An unreadable schema version is reported, not guessed

- **GIVEN** a stored snapshot written under a schema version this application
  cannot read
- **WHEN** the user views the scan history
- **THEN** MacStorageAtlas reports that the snapshot's version is not readable
- **AND** it states the version the snapshot uses
- **AND** it does not present the snapshot's contents as though they were
  readable

### Requirement: Stored scan history stays private and local

MacStorageAtlas SHALL treat the history store as private user data. It MUST
write the store only within the application's own support location, MUST NOT
transmit any snapshot or part of one anywhere, MUST NOT record the contents of
any scanned file, and MUST restrict the store so that other users of the same
machine cannot read it. The documentation MUST state what scan history stores,
where it stores it, and how to remove it.

#### Scenario: Snapshots stay on the machine

- **GIVEN** scan history is enabled and snapshots are recorded
- **WHEN** scans complete
- **THEN** the only files MacStorageAtlas created are within its own history
  store
- **AND** no scan data was transmitted off the machine

#### Scenario: A snapshot contains no file contents

- **GIVEN** a recorded snapshot of a scan covering files of any type
- **WHEN** the snapshot is read back
- **THEN** it contains only paths, sizes, and filesystem metadata
- **AND** no scanned file's contents were recorded

#### Scenario: The store is not readable by other users

- **GIVEN** a history store containing snapshots
- **WHEN** another user of the same machine attempts to read it
- **THEN** the store's contents are not readable by that user

### Requirement: Capture keeps the application responsive

MacStorageAtlas SHALL write a snapshot without blocking the user interface and
MUST write it incrementally rather than assembling the whole snapshot in memory
first. The user MUST be able to keep working with the completed scan result
while capture runs, MUST be able to cancel capture, and MUST be able to start a
new scan; starting a new scan MUST cancel a capture still in progress rather
than recording a snapshot of a result that is no longer displayed.

#### Scenario: Capturing a very large result

- **GIVEN** a completed scan result containing hundreds of thousands of items
- **WHEN** MacStorageAtlas records it
- **THEN** the application remains responsive while capture runs
- **AND** capture does not hold the whole snapshot in memory

#### Scenario: Cancelling capture

- **GIVEN** capture is running
- **WHEN** the user cancels it
- **THEN** capture stops
- **AND** no partial snapshot remains in the store
- **AND** the displayed scan result is unchanged

#### Scenario: Starting a new scan cancels capture in progress

- **GIVEN** capture of a completed scan is running
- **WHEN** the user starts a new scan
- **THEN** the running capture is cancelled
- **AND** no partial snapshot remains in the store
- **AND** the new scan starts normally
