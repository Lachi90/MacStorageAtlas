## MODIFIED Requirements

### Requirement: Basket totals stay honest

MacStorageAtlas SHALL show cleanup basket item count, total logical size, and
expected uniquely reclaimable size using the completed scan result's measurement
mode and accounting semantics. Totals MUST NOT double-count duplicate or
descendant-covered entries. The expected uniquely reclaimable size MUST reflect
the operation the user is about to run, and MUST be reported as zero for an
operation that leaves every source item in place.

#### Scenario: Logical result totals

- **GIVEN** the completed scan result uses logical-size mode
- **AND** the cleanup basket contains non-overlapping items
- **WHEN** the basket summary is displayed
- **THEN** the total logical size is the sum of selected logical lengths
- **AND** the expected uniquely reclaimable size uses the same selected logical lengths

#### Scenario: Allocated result totals

- **GIVEN** the completed scan result uses allocated-size mode
- **AND** the cleanup basket contains non-overlapping items
- **WHEN** the basket summary is displayed
- **THEN** the total logical size is the sum of selected logical lengths
- **AND** the expected uniquely reclaimable size uses the selected locally allocated bytes from that scan

#### Scenario: Overlap does not inflate totals

- **GIVEN** a directory is in the cleanup basket
- **AND** a descendant item is covered by that directory
- **WHEN** the basket summary is displayed
- **THEN** the descendant item does not add another count to the basket total
- **AND** the descendant item does not add another size contribution to the basket totals

#### Scenario: Copy operation reclaims nothing locally

- **GIVEN** the cleanup basket contains non-overlapping items
- **AND** the user is preparing a copy to another location
- **WHEN** the basket summary is displayed for that operation
- **THEN** the total logical size still reflects the selected items
- **AND** the expected uniquely reclaimable size is zero

### Requirement: Review is required before filesystem changes

MacStorageAtlas SHALL require a final review before changing the filesystem for
any cleanup basket item. The review MUST show the operation type, the
destination when the operation writes items to a destination, item count, total
logical size, expected uniquely reclaimable size, item names, paths, and
per-item readiness status. Cancelling the review MUST leave the filesystem
unchanged.

#### Scenario: User cancels review

- **GIVEN** the cleanup basket contains executable items
- **AND** the final review is displayed
- **WHEN** the user cancels the review
- **THEN** no cleanup basket item is moved to Trash, moved, or copied
- **AND** the displayed scan result remains unchanged

#### Scenario: Review shows exact Trash operation

- **GIVEN** the cleanup basket contains executable items
- **AND** the selected operation moves items to Trash
- **WHEN** the final review is displayed
- **THEN** MacStorageAtlas identifies the operation as moving items to macOS Trash
- **AND** it lists each executable item path included in the operation
- **AND** it does not describe any item as safe to delete

#### Scenario: Review distinguishes relocation from Trash

- **GIVEN** the cleanup basket contains executable items
- **AND** the selected operation writes items to a chosen destination
- **WHEN** the final review is displayed
- **THEN** MacStorageAtlas identifies the operation as a move or a copy to that destination
- **AND** it does not describe the operation as moving items to Trash
