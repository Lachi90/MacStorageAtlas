## ADDED Requirements

### Requirement: Selected items can be previewed with Quick Look

MacStorageAtlas SHALL allow the user to request a native macOS Quick Look preview for the currently selected scan result item. Preview requests MUST use the current selected item from whichever result view owns selection.

#### Scenario: Tree selection is previewed

- **GIVEN** a completed scan result is visible in the folder tree
- **AND** the user has selected an existing item in the folder tree
- **WHEN** the user requests Quick Look
- **THEN** MacStorageAtlas opens a native Quick Look preview for that selected item
- **AND** the selected item remains selected in the scan result

#### Scenario: Treemap selection is previewed

- **GIVEN** a completed scan result is visible in the treemap
- **AND** the user has selected an existing treemap item
- **WHEN** the user requests Quick Look
- **THEN** MacStorageAtlas opens a native Quick Look preview for that selected item
- **AND** the selected item remains selected in the scan result

#### Scenario: Largest-file selection is previewed

- **GIVEN** a completed scan result has largest-file entries
- **AND** the user has selected an existing largest-file entry
- **WHEN** the user requests Quick Look
- **THEN** MacStorageAtlas opens a native Quick Look preview for that selected file
- **AND** the selected item remains selected in the scan result

#### Scenario: No selection is available

- **GIVEN** no scan result item is selected
- **WHEN** the user tries to request Quick Look
- **THEN** MacStorageAtlas does not start a preview
- **AND** the Quick Look action is unavailable or ignored

### Requirement: Quick Look failures are recoverable

MacStorageAtlas SHALL handle Quick Look failures without crashing, changing scan totals, or clearing the completed scan result. Failure messages MUST be friendly and MUST NOT expose platform diagnostics as the primary user-facing text.

#### Scenario: Selected path has been removed

- **GIVEN** a completed scan result contains a selected item
- **AND** that item's path no longer exists on disk
- **WHEN** the user requests Quick Look
- **THEN** MacStorageAtlas does not start a preview
- **AND** it reports that the selected item no longer exists or could not be previewed
- **AND** the completed scan result remains available

#### Scenario: macOS cannot start Quick Look

- **GIVEN** a completed scan result contains a selected item whose path still exists
- **AND** macOS cannot start a Quick Look preview for that item
- **WHEN** the user requests Quick Look
- **THEN** MacStorageAtlas reports that the selected item could not be previewed
- **AND** the completed scan result remains available

### Requirement: Selected item inspection has native keyboard access

MacStorageAtlas SHALL provide keyboard access for selected-item inspection using native macOS shortcuts. Space MUST request Quick Look for the current selected item when keyboard focus is not editing text, and Command-I MUST show the selected-item details surface for the current selected item.

#### Scenario: Space previews selected item

- **GIVEN** a completed scan result contains a selected item
- **AND** keyboard focus is not editing text
- **WHEN** the user presses Space
- **THEN** MacStorageAtlas requests Quick Look for the selected item

#### Scenario: Space while editing text does not preview

- **GIVEN** a completed scan result contains a selected item
- **AND** keyboard focus is editing search text
- **WHEN** the user presses Space
- **THEN** MacStorageAtlas keeps editing text
- **AND** it does not request Quick Look

#### Scenario: Command-I shows selected item details

- **GIVEN** a completed scan result contains a selected item
- **WHEN** the user presses Command-I
- **THEN** MacStorageAtlas shows the selected-item details surface
- **AND** the selected item remains selected
- **AND** scan-time metadata remains the source of the displayed details

### Requirement: Quick Look preserves scan safety and cleanup boundaries

MacStorageAtlas SHALL treat Quick Look as an inspection action only. Quick Look MUST NOT authorize cleanup, bypass Trash confirmation, change result measurement basis, read file contents inside MacStorageAtlas, hash files, persist file contents, or intentionally materialize cloud placeholders.

#### Scenario: Preview precedes Trash operation

- **GIVEN** the user has previewed a selected item with Quick Look
- **WHEN** the user chooses to move that item to Trash
- **THEN** MacStorageAtlas uses the existing recoverable Trash workflow
- **AND** existing confirmation behavior remains required

#### Scenario: Preview does not change result data

- **GIVEN** a completed scan result was produced with a selected measurement mode
- **AND** the user has selected an item
- **WHEN** the user requests Quick Look for that item
- **THEN** measured, counted, and shared byte labels remain based on the completed scan's measurement mode
- **AND** metadata display remains based on the scan-time metadata snapshot
- **AND** treemap, file-type, largest-file, progress, and directory totals remain unchanged

#### Scenario: Cloud placeholder is previewed

- **GIVEN** a selected item is a dataless cloud placeholder
- **WHEN** the user requests Quick Look
- **THEN** MacStorageAtlas delegates preview to macOS
- **AND** MacStorageAtlas does not intentionally read the item's file contents
- **AND** MacStorageAtlas does not intentionally request that a cloud provider download the item
