## ADDED Requirements

### Requirement: Basket collects explicit scanned items

MacStorageAtlas SHALL let the user add and remove items from a cleanup basket only through explicit user actions on completed scan results. Applying a filter, changing selection, switching result views, previewing an item, or revealing an item in Finder MUST NOT add items to the cleanup basket.

#### Scenario: Add selected item from a result view

- **GIVEN** a completed scan result is displayed
- **AND** an item is selected in a result view
- **WHEN** the user adds the selected item to the cleanup basket
- **THEN** the basket contains a reference to that scanned item
- **AND** the displayed scan result remains unchanged

#### Scenario: Filtering does not populate the basket

- **GIVEN** a completed scan result is displayed
- **WHEN** the user applies a filter that matches multiple items
- **THEN** the cleanup basket is unchanged
- **AND** no matched file or folder is moved, copied, deleted, or selected for cleanup

#### Scenario: Remove item from the basket

- **GIVEN** an item is already in the cleanup basket
- **WHEN** the user removes that item from the cleanup basket
- **THEN** the basket no longer contains that item
- **AND** the filesystem remains unchanged

### Requirement: Basket prevents duplicate and overlapping cleanup entries

MacStorageAtlas SHALL prevent duplicate entries and parent-child overlap from causing the basket to overstate the cleanup operation. The basket MUST present at most one active planned entry for any path covered by a selected ancestor.

#### Scenario: Duplicate item is added

- **GIVEN** an item is already in the cleanup basket
- **WHEN** the user tries to add the same scanned item again
- **THEN** the basket remains unchanged
- **AND** MacStorageAtlas reports that the item is already in the basket

#### Scenario: Descendant of selected directory is added

- **GIVEN** a directory is already in the cleanup basket
- **AND** a scanned descendant of that directory is visible in a result view
- **WHEN** the user tries to add the descendant to the cleanup basket
- **THEN** the basket remains unchanged
- **AND** MacStorageAtlas reports that the descendant is already covered by the selected directory

#### Scenario: Directory covering selected descendants is added

- **GIVEN** one or more scanned items are already in the cleanup basket
- **AND** a visible directory contains those items
- **WHEN** the user adds that directory to the cleanup basket
- **THEN** the basket contains the directory as the active planned entry
- **AND** descendant entries covered by that directory no longer contribute separately to item count or size totals

### Requirement: Basket totals stay honest

MacStorageAtlas SHALL show cleanup basket item count, total logical size, and expected uniquely reclaimable size using the completed scan result's measurement mode and accounting semantics. Totals MUST NOT double-count duplicate or descendant-covered entries.

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

### Requirement: Protected paths are blocked from basket cleanup

MacStorageAtlas SHALL block protected paths from being added to an executable cleanup basket. Protected-path decisions MUST include a user-visible reason and MUST include the current scan root, macOS system locations, Trash locations, and paths outside the completed scan result.

#### Scenario: Current scan root is protected

- **GIVEN** a completed scan result is displayed
- **WHEN** the user tries to add the current scan root to the cleanup basket
- **THEN** MacStorageAtlas does not add the scan root as an executable cleanup item
- **AND** it explains that the scan root is protected from basket cleanup

#### Scenario: macOS system path is protected

- **GIVEN** a completed scan result includes a macOS system location
- **WHEN** the user tries to add that location to the cleanup basket
- **THEN** MacStorageAtlas does not add the path as an executable cleanup item
- **AND** it explains that the path is protected

#### Scenario: Outside path is protected

- **GIVEN** a path is not part of the completed scan result
- **WHEN** that path is evaluated for cleanup basket inclusion
- **THEN** MacStorageAtlas blocks the path from basket cleanup
- **AND** it explains that the path is outside the scanned result

### Requirement: Review is required before filesystem changes

MacStorageAtlas SHALL require a final review before moving any cleanup basket item to Trash. The review MUST show the operation type, item count, total logical size, expected uniquely reclaimable size, item names, paths, and per-item readiness status.

#### Scenario: User cancels review

- **GIVEN** the cleanup basket contains executable items
- **AND** the final review is displayed
- **WHEN** the user cancels the review
- **THEN** no cleanup basket item is moved to Trash
- **AND** the displayed scan result remains unchanged

#### Scenario: Review shows exact Trash operation

- **GIVEN** the cleanup basket contains executable items
- **WHEN** the final review is displayed
- **THEN** MacStorageAtlas identifies the operation as moving items to macOS Trash
- **AND** it lists each executable item path included in the operation
- **AND** it does not describe any item as safe to delete

### Requirement: Basket items are revalidated before Trash execution

MacStorageAtlas SHALL revalidate each cleanup basket item immediately before Trash execution. Missing, replaced, changed, protected, or otherwise invalid items MUST be blocked from execution and remain visible with their status.

#### Scenario: Item is missing before execution

- **GIVEN** an item was added to the cleanup basket from a completed scan result
- **AND** the item no longer exists before Trash execution
- **WHEN** MacStorageAtlas revalidates the basket
- **THEN** that item is blocked from execution
- **AND** the review reports that the item is missing

#### Scenario: Item identity changed before execution

- **GIVEN** an item was added to the cleanup basket from a completed scan result
- **AND** the path now refers to a different filesystem item before Trash execution
- **WHEN** MacStorageAtlas revalidates the basket
- **THEN** that item is blocked from execution
- **AND** the review reports that the item changed since the scan

#### Scenario: Item size changed before execution

- **GIVEN** an item was added to the cleanup basket from a completed scan result
- **AND** the item's size no longer matches the completed scan result before Trash execution
- **WHEN** MacStorageAtlas revalidates the basket
- **THEN** that item is blocked from execution
- **AND** the review reports that the item changed since the scan

### Requirement: Trash execution is recoverable and itemized

MacStorageAtlas SHALL move approved cleanup basket items to macOS Trash rather than permanently deleting them. The operation MUST report success or failure for each item and MUST keep failed items visible until the user removes them or rescans.

#### Scenario: All items move to Trash

- **GIVEN** the cleanup basket contains executable items
- **AND** the user confirms the final review
- **WHEN** all approved items are moved to Trash successfully
- **THEN** MacStorageAtlas reports the operation succeeded
- **AND** no item is permanently deleted by MacStorageAtlas

#### Scenario: One item fails

- **GIVEN** the cleanup basket contains multiple executable items
- **AND** the user confirms the final review
- **WHEN** one item cannot be moved to Trash
- **THEN** MacStorageAtlas reports that item's failure
- **AND** successfully moved items remain recorded as successful
- **AND** failed items remain visible for review

#### Scenario: Operation is cancelled during execution

- **GIVEN** the cleanup basket is moving approved items to Trash
- **WHEN** the user cancels the operation
- **THEN** MacStorageAtlas stops moving additional items as soon as practical
- **AND** it reports which items succeeded, failed, or were not attempted

### Requirement: Scan results remain consistent after basket cleanup

MacStorageAtlas SHALL update displayed scan results only after the platform Trash operation confirms success for an item. Shared-aware allocated results MUST be refreshed from the filesystem after any successful basket Trash operation before being presented as completed updated results.

#### Scenario: Successful item is removed from displayed logical result

- **GIVEN** the completed scan result uses logical-size mode
- **AND** a cleanup basket item is successfully moved to Trash
- **WHEN** MacStorageAtlas updates the displayed result
- **THEN** the trashed item is no longer shown in the displayed result
- **AND** failed or unattempted basket items remain shown

#### Scenario: Shared-aware result refreshes after success

- **GIVEN** the completed scan result uses shared-aware allocated mode
- **AND** at least one cleanup basket item is successfully moved to Trash
- **WHEN** MacStorageAtlas updates the displayed result
- **THEN** it refreshes the affected scan scope from the filesystem before presenting updated completed totals
- **AND** it does not subtract shared storage as though remaining shared data disappeared

#### Scenario: No successful Trash operation leaves result unchanged

- **GIVEN** a cleanup basket operation is cancelled before any item moves to Trash
- **OR** every approved item fails before being moved to Trash
- **WHEN** MacStorageAtlas reports the operation result
- **THEN** the displayed scan result remains unchanged

### Requirement: Basket preserves privacy and cleanup boundaries

MacStorageAtlas SHALL keep cleanup basket planning and review local to the user's Mac. Basket operations MUST NOT read file contents, hash files, persist file contents, send scan data externally, intentionally materialize cloud placeholders, or bypass existing Trash confirmation and recoverability boundaries.

#### Scenario: Basket review uses metadata only

- **GIVEN** items are in the cleanup basket
- **WHEN** MacStorageAtlas prepares the final review
- **THEN** it uses scan metadata and filesystem metadata needed for revalidation
- **AND** it does not read or persist file contents
- **AND** it does not send paths or metadata externally

#### Scenario: Cloud placeholder remains unmaterialized

- **GIVEN** a cleanup basket item is a cloud-backed placeholder
- **WHEN** MacStorageAtlas prepares or executes the cleanup basket operation
- **THEN** it does not intentionally download the item's file contents
- **AND** any cleanup action remains limited to the local filesystem item represented by the scanned path
