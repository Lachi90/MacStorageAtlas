## MODIFIED Requirements

### Requirement: Tree preparation preserves scan results and selection behavior

MacStorageAtlas SHALL prepare the folder tree without starting a scan, revisiting the filesystem, reading file contents, or changing the completed scan result. A search change MUST clear the folder-tree selection only when the selected item is no longer displayed.

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

#### Scenario: Search change clears a selection that is no longer displayed

- **GIVEN** an item is selected in the folder tree
- **WHEN** the search text changes so that the item is no longer displayed
- **THEN** the folder tree selection is cleared

#### Scenario: Search change keeps a selection that is still displayed

- **GIVEN** an item is selected in the folder tree
- **WHEN** the search text changes and the item remains displayed
- **THEN** the item remains selected
