## ADDED Requirements

### Requirement: The folder tree presents matching results and their ancestors

MacStorageAtlas SHALL display the completed scan result as a folder tree. When search text is present, the tree MUST display items whose name or path contains that text, matched case-insensitively, together with the ancestor directories needed to reach them. When search text is absent, the tree MUST display the complete scan result.

#### Scenario: Absent search text shows the whole result

- **GIVEN** a completed scan result is displayed
- **WHEN** the search text is empty
- **THEN** the folder tree displays the complete scan result

#### Scenario: Search text shows matches and their ancestors

- **GIVEN** a completed scan result contains an item whose name contains the search text
- **WHEN** the search text is applied
- **THEN** the folder tree displays that item
- **AND** it displays the ancestor directories needed to reach that item

#### Scenario: Matching ignores letter case

- **GIVEN** a completed scan result contains an item whose name differs from the search text only by letter case
- **WHEN** the search text is applied
- **THEN** that item is displayed

#### Scenario: Search text matching nothing shows an empty tree

- **GIVEN** a completed scan result contains no item whose name or path contains the search text
- **WHEN** the search text is applied
- **THEN** the folder tree displays no items
- **AND** the completed scan result remains available for clearing the search text

### Requirement: The displayed tree reflects the most recent search text

MacStorageAtlas SHALL display the folder tree corresponding to the most recently entered search text. When search text changes while the tree is still being prepared, MacStorageAtlas MUST abandon the superseded work and MUST NOT allow it to replace a newer result.

#### Scenario: Rapid edits yield the latest tree

- **GIVEN** the folder tree is being prepared for one search text
- **WHEN** the user changes the search text again before preparation completes
- **THEN** the superseded preparation does not update the displayed tree
- **AND** the displayed tree corresponds to the most recent search text

#### Scenario: Clearing search text restores the whole result

- **GIVEN** search text is active and the folder tree displays matches
- **WHEN** the user clears the search text
- **THEN** the folder tree displays the complete scan result again
- **AND** no rescan occurs

### Requirement: Browsing a large result stays responsive

MacStorageAtlas SHALL keep the user interface responsive while preparing and browsing the folder tree for a completed scan result, including results containing a very large number of items. Preparation of the folder tree MUST NOT block the user interface thread.

#### Scenario: Typing stays responsive on a large result

- **GIVEN** a completed scan result contains a very large number of items
- **WHEN** the user types search text
- **THEN** the user interface remains responsive
- **AND** the user can continue editing the search text

#### Scenario: Expanding a directory stays responsive

- **GIVEN** a completed scan result contains a directory with a very large number of descendants
- **WHEN** the user expands that directory in the folder tree
- **THEN** the user interface remains responsive
- **AND** the directory's children are displayed

### Requirement: Tree preparation preserves scan results and selection behavior

MacStorageAtlas SHALL prepare the folder tree without starting a scan, revisiting the filesystem, reading file contents, or changing the completed scan result. Selection behavior on a search change MUST remain unchanged.

#### Scenario: Preparing the tree does not touch the filesystem

- **GIVEN** a completed scan result is displayed
- **WHEN** the search text changes and the folder tree is prepared again
- **THEN** MacStorageAtlas does not start a scan
- **AND** it does not read file contents
- **AND** the completed scan result is unchanged

#### Scenario: Byte values remain based on the scan's measurement mode

- **GIVEN** a completed scan result was produced with a selected measurement mode
- **WHEN** the folder tree is prepared for any search text
- **THEN** every displayed byte value remains based on that completed scan's measurement mode

#### Scenario: Search change clears the tree selection

- **GIVEN** an item is selected in the folder tree
- **WHEN** the search text changes
- **THEN** the folder tree selection is cleared
