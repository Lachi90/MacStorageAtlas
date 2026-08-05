## ADDED Requirements

### Requirement: Basket offers move and copy to a chosen destination

MacStorageAtlas SHALL let the user run a move operation or a copy operation on
the cleanup basket in addition to the existing move-to-Trash operation. Both
relocation operations MUST require the user to choose a destination folder
before any review or filesystem change, and cancelling destination selection
MUST leave the basket and the filesystem unchanged.

#### Scenario: User chooses a destination for a move

- **GIVEN** the cleanup basket contains items
- **WHEN** the user starts a move to another location
- **THEN** MacStorageAtlas asks the user to choose a destination folder
- **AND** no item is moved before a destination is chosen

#### Scenario: User chooses a destination for a copy

- **GIVEN** the cleanup basket contains items
- **WHEN** the user starts a copy to another location
- **THEN** MacStorageAtlas asks the user to choose a destination folder
- **AND** no item is copied before a destination is chosen

#### Scenario: User cancels destination selection

- **GIVEN** the cleanup basket contains items
- **AND** the user started a move or copy to another location
- **WHEN** the user cancels destination selection
- **THEN** no item is moved or copied
- **AND** the cleanup basket contents remain unchanged
- **AND** the displayed scan result remains unchanged

#### Scenario: Basket membership is independent of the operation

- **GIVEN** the cleanup basket contains items
- **WHEN** the user switches between the Trash, move, and copy operations
- **THEN** the basket contents remain unchanged
- **AND** no filesystem change occurs from switching the operation

### Requirement: Destination is validated before relocation review

MacStorageAtlas SHALL validate the chosen destination before presenting the
relocation review. Validation MUST block relocation when the destination does
not exist, is not a directory, is not writable, or is the source item itself or
a descendant of a source item. Each blocking condition MUST produce a
user-visible reason.

#### Scenario: Destination does not exist

- **GIVEN** the user chose a destination folder for a relocation
- **AND** the destination no longer exists when relocation is prepared
- **WHEN** MacStorageAtlas validates the destination
- **THEN** it blocks the relocation
- **AND** it explains that the destination is missing

#### Scenario: Destination is not writable

- **GIVEN** the user chose a destination folder for a relocation
- **AND** the destination is read-only
- **WHEN** MacStorageAtlas validates the destination
- **THEN** it blocks the relocation
- **AND** it explains that the destination cannot be written to

#### Scenario: Destination is inside a source item

- **GIVEN** a basket item is a directory
- **AND** the chosen destination is that directory or a descendant of it
- **WHEN** MacStorageAtlas validates the destination
- **THEN** it blocks that item from relocation
- **AND** it explains that an item cannot be moved or copied into itself

#### Scenario: Destination equals the source parent for a move

- **GIVEN** a basket item is selected for a move
- **AND** the chosen destination already contains that item at its current path
- **WHEN** MacStorageAtlas validates the destination
- **THEN** it blocks that item from the move
- **AND** it explains that the item is already at the destination

### Requirement: Insufficient destination space blocks relocation

MacStorageAtlas SHALL compare the total size to be written against the
destination's reported free space when the platform reports it, and MUST block
relocation when the reported free space is insufficient. When free space cannot
be determined, MacStorageAtlas MUST report the figure as unknown and MUST NOT
present any space estimate as a guarantee.

#### Scenario: Reported free space is insufficient

- **GIVEN** the executable basket items total more bytes than the destination
  reports as free
- **WHEN** MacStorageAtlas prepares the relocation review
- **THEN** it blocks the relocation
- **AND** it explains that the destination does not have enough free space

#### Scenario: Free space cannot be determined

- **GIVEN** the destination volume does not report free space
- **WHEN** MacStorageAtlas prepares the relocation review
- **THEN** it reports the available free space as unknown
- **AND** it does not block the relocation solely because free space is unknown

### Requirement: Existing destination names are never overwritten

MacStorageAtlas SHALL block any basket item whose name already exists at the
chosen destination. A blocked colliding item MUST remain visible with a
user-visible reason, MUST NOT be attempted, and MUST NOT cause MacStorageAtlas
to replace, merge into, or auto-rename any item at the destination.

#### Scenario: Colliding item is blocked

- **GIVEN** a basket item named `Archive.zip` is selected for relocation
- **AND** the chosen destination already contains an item named `Archive.zip`
- **WHEN** MacStorageAtlas prepares the relocation review
- **THEN** that basket item is blocked from execution
- **AND** the review reports that an item with the same name already exists at
  the destination

#### Scenario: Existing destination item is preserved

- **GIVEN** a relocation includes an item that collides with an existing
  destination item
- **WHEN** the user confirms the relocation
- **THEN** the existing destination item is not replaced, merged into, or
  renamed
- **AND** the colliding source item remains at its original path

#### Scenario: Non-colliding items still run

- **GIVEN** a relocation includes one colliding item and one non-colliding item
- **WHEN** the user confirms the relocation
- **THEN** the non-colliding item is relocated
- **AND** the colliding item is reported as blocked

### Requirement: Relocation items are revalidated before execution

MacStorageAtlas SHALL revalidate each basket item immediately before relocation
execution using the same protected-path, existence, identity, and size checks
applied to Trash cleanup, in addition to the destination checks. Items that fail
any check MUST be blocked from execution and MUST remain visible with their
status.

#### Scenario: Protected item is blocked from relocation

- **GIVEN** a basket item resolves to a protected path
- **WHEN** MacStorageAtlas revalidates the basket for relocation
- **THEN** that item is blocked from execution
- **AND** the review reports that the path is protected

#### Scenario: Missing source is blocked from relocation

- **GIVEN** a basket item no longer exists before relocation execution
- **WHEN** MacStorageAtlas revalidates the basket for relocation
- **THEN** that item is blocked from execution
- **AND** the review reports that the item is missing

#### Scenario: Changed source is blocked from relocation

- **GIVEN** a basket item's identity or size no longer matches the completed
  scan result before relocation execution
- **WHEN** MacStorageAtlas revalidates the basket for relocation
- **THEN** that item is blocked from execution
- **AND** the review reports that the item changed since the scan

### Requirement: Review is required before relocation

MacStorageAtlas SHALL require a final review before moving or copying any
cleanup basket item. The review MUST identify the operation as a move or a copy,
show the destination path, list each item path included in the operation, show
per-item readiness status, and show the expected locally reclaimed size for the
operation. Cancelling the review MUST leave the filesystem unchanged.

#### Scenario: Review shows the move operation and destination

- **GIVEN** the cleanup basket contains executable items
- **AND** a destination has been chosen for a move
- **WHEN** the final review is displayed
- **THEN** MacStorageAtlas identifies the operation as moving items to the
  chosen destination
- **AND** it shows the destination path
- **AND** it lists each executable item path included in the operation

#### Scenario: Copy review reports no reclaimed space

- **GIVEN** the cleanup basket contains executable items
- **AND** a destination has been chosen for a copy
- **WHEN** the final review is displayed
- **THEN** MacStorageAtlas identifies the operation as copying items to the
  chosen destination
- **AND** it reports the expected locally reclaimed size as zero

#### Scenario: Move review reports expected reclaimed space

- **GIVEN** the cleanup basket contains executable items
- **AND** a destination on a different volume has been chosen for a move
- **WHEN** the final review is displayed
- **THEN** MacStorageAtlas reports the expected locally reclaimed size using the
  completed scan result's measurement mode

#### Scenario: User cancels the relocation review

- **GIVEN** the relocation review is displayed
- **WHEN** the user cancels the review
- **THEN** no item is moved or copied
- **AND** the displayed scan result remains unchanged

### Requirement: A failed transfer never removes the source

MacStorageAtlas SHALL complete a move across volumes as a copy followed by
removal of the source, and MUST remove the source only after the copy is
verified as complete. A copy that fails, is cancelled, or cannot be verified
MUST leave the source item in place.

#### Scenario: Cross-volume move copies before removing the source

- **GIVEN** a basket item is on a different volume from the destination
- **AND** the user confirms the move
- **WHEN** the item is relocated
- **THEN** the item exists at the destination
- **AND** the source item is removed only after the copy is verified

#### Scenario: Failed cross-volume copy keeps the source

- **GIVEN** a basket item is on a different volume from the destination
- **AND** the user confirms the move
- **WHEN** the copy fails before it is verified
- **THEN** the source item remains at its original path
- **AND** MacStorageAtlas reports the failure for that item
- **AND** it reports the destination path of any partial result

#### Scenario: Cancelled cross-volume copy keeps the source

- **GIVEN** a cross-volume move is in progress for an item
- **WHEN** the user cancels before the copy is verified
- **THEN** the source item remains at its original path
- **AND** MacStorageAtlas reports that item as cancelled

### Requirement: Relocation execution is itemized and cancellable

MacStorageAtlas SHALL execute relocation one item at a time, report per-item
progress, allow cancellation between items, and report success, failure,
cancelled, and unattempted status for every approved item. Failed items MUST
remain visible until the user removes them or rescans.

#### Scenario: All items relocate successfully

- **GIVEN** the relocation review is confirmed for multiple executable items
- **WHEN** every approved item is relocated successfully
- **THEN** MacStorageAtlas reports the operation succeeded
- **AND** each item is reported as succeeded

#### Scenario: One item fails during relocation

- **GIVEN** the relocation review is confirmed for multiple executable items
- **WHEN** one item cannot be relocated
- **THEN** MacStorageAtlas reports that item's failure
- **AND** successfully relocated items remain recorded as successful
- **AND** the failed item remains visible for review

#### Scenario: Operation is cancelled during relocation

- **GIVEN** a relocation is in progress
- **WHEN** the user cancels the operation
- **THEN** MacStorageAtlas stops relocating additional items as soon as
  practical
- **AND** it reports which items succeeded, failed, were cancelled, or were not
  attempted

#### Scenario: Relocation reports per-item progress

- **GIVEN** a relocation is in progress for multiple items
- **WHEN** MacStorageAtlas relocates each item
- **THEN** it reports which item is currently being relocated
- **AND** it reports how many approved items have been completed

### Requirement: Scan results remain consistent after relocation

MacStorageAtlas SHALL update displayed scan results only after the platform
relocation service confirms success for an item. A successful move MUST remove
the item from the displayed result; a successful copy MUST leave the displayed
result unchanged. Shared-aware allocated results MUST be refreshed from the
filesystem after any successful move before being presented as completed updated
results.

#### Scenario: Successful move updates the displayed logical result

- **GIVEN** the completed scan result uses logical-size mode
- **AND** a basket item is successfully moved to the destination
- **WHEN** MacStorageAtlas updates the displayed result
- **THEN** the moved item is no longer shown in the displayed result
- **AND** failed or unattempted basket items remain shown

#### Scenario: Successful copy leaves the displayed result unchanged

- **GIVEN** a completed scan result is displayed
- **AND** a basket item is successfully copied to the destination
- **WHEN** MacStorageAtlas reports the operation result
- **THEN** the copied item is still shown in the displayed result
- **AND** the displayed totals are unchanged

#### Scenario: Shared-aware result refreshes after a successful move

- **GIVEN** the completed scan result uses shared-aware allocated mode
- **AND** at least one basket item is successfully moved to the destination
- **WHEN** MacStorageAtlas updates the displayed result
- **THEN** it refreshes the affected scan scope from the filesystem before
  presenting updated completed totals
- **AND** it does not subtract shared storage as though remaining shared data
  disappeared

#### Scenario: No successful relocation leaves the result unchanged

- **GIVEN** a relocation is cancelled before any item is relocated
- **OR** every approved item fails
- **WHEN** MacStorageAtlas reports the operation result
- **THEN** the displayed scan result remains unchanged

### Requirement: Relocation preserves privacy boundaries

MacStorageAtlas SHALL keep relocation planning, preflight, and review local to
the user's Mac. Planning, preflight, and review MUST NOT read or persist file
contents, hash files, send paths or metadata externally, or materialize
cloud-backed placeholders. Only the transfer the user explicitly approved may
read source contents, and it MUST write them only to the chosen destination.

#### Scenario: Relocation planning uses metadata only

- **GIVEN** items are in the cleanup basket and a destination has been chosen
- **WHEN** MacStorageAtlas prepares the relocation review
- **THEN** it uses scan metadata, filesystem metadata, and destination metadata
- **AND** it does not read or persist file contents
- **AND** it does not send paths or metadata externally

#### Scenario: Preflight does not materialize a cloud placeholder

- **GIVEN** a basket item is a cloud-backed placeholder
- **WHEN** MacStorageAtlas validates the destination and revalidates the item
- **THEN** it does not download the item's contents
- **AND** the completed scan result remains based on the original scan metadata

#### Scenario: Approved transfer writes only to the destination

- **GIVEN** the user confirmed a relocation
- **WHEN** MacStorageAtlas transfers an approved item
- **THEN** it writes the item's contents only under the chosen destination
- **AND** it does not persist file contents anywhere else
