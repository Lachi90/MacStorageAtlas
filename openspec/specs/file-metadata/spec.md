## Purpose

Define how MacStorageAtlas captures and displays scan-time filesystem metadata
for result items without changing scan measurement semantics, reading file
contents, or authorizing cleanup actions.

## Requirements

### Requirement: Scan results retain metadata snapshots

MacStorageAtlas SHALL retain scan-time filesystem metadata for every included file and directory result. The retained metadata MUST describe the state observed during the scan and MUST remain associated with that result after the scan completes.

#### Scenario: File metadata is captured during a completed scan

- **GIVEN** a scan includes a successfully measured file with available filesystem metadata
- **WHEN** the scan completes
- **THEN** the file result includes its scan-time modification time
- **AND** the file result includes its creation time when that value is available
- **AND** the file result identifies the item as a file
- **AND** the file result remains selectable from result views

#### Scenario: Directory metadata is captured during a completed scan

- **GIVEN** a scan includes a successfully visited directory
- **WHEN** the scan completes
- **THEN** the directory result includes its scan-time modification time when available
- **AND** the directory result identifies the item as a directory
- **AND** the directory result remains selectable from result views

#### Scenario: Result metadata does not refresh after scan completion

- **GIVEN** a completed scan result contains metadata for a selected item
- **AND** the filesystem item changes after the scan completes
- **WHEN** the user views the selected item details without rescanning
- **THEN** MacStorageAtlas displays the metadata retained in the scan result
- **AND** it does not silently replace the result metadata with newer filesystem state

### Requirement: Metadata availability is explicit

MacStorageAtlas SHALL distinguish available metadata from unknown metadata. The application MUST NOT display fabricated timestamps, default timestamps, or inferred values when metadata is unavailable or unreliable.

#### Scenario: Creation time is unavailable

- **GIVEN** a scanned item does not provide a creation time
- **WHEN** the user views item details
- **THEN** the creation time is shown as unknown
- **AND** no default date is displayed in its place

#### Scenario: Last-access time is unreliable

- **GIVEN** last-access time is unavailable or cannot be treated as reliable for a scanned item
- **WHEN** the user views item details
- **THEN** the last-access time is omitted or shown as unknown
- **AND** no inferred access time is displayed

### Requirement: Selected item details display metadata

MacStorageAtlas SHALL display available metadata for the selected scan result item from every result view that can select an item. Metadata presentation MUST preserve the existing size basis associated with the scan.

#### Scenario: Tree selection displays metadata

- **GIVEN** a completed scan result is visible in the folder tree
- **WHEN** the user selects an item in the folder tree
- **THEN** the selected-item details show its name, path, size details, item kind, and available metadata

#### Scenario: Treemap selection displays metadata

- **GIVEN** a completed scan result is visible in the treemap
- **WHEN** the user selects an item in the treemap
- **THEN** the selected-item details show its name, path, size details, item kind, and available metadata

#### Scenario: Largest-file selection displays metadata

- **GIVEN** a completed scan result has largest-file entries
- **WHEN** the user selects an item in the largest-files list
- **THEN** the selected-item details show its name, path, size details, item kind, and available metadata

#### Scenario: Metadata display preserves measurement basis

- **GIVEN** a completed scan result was produced with a selected measurement mode
- **WHEN** the user views item details with metadata
- **THEN** measured, counted, and shared byte labels remain based on the completed scan's measurement mode
- **AND** metadata display does not change treemap, file-type, largest-file, progress, or directory totals

### Requirement: Metadata collection preserves scan safety

MacStorageAtlas SHALL collect metadata without reading file contents, hashing files, contacting cloud providers, or intentionally materializing dataless cloud placeholders. Recoverable metadata failures MUST be reported without stopping the scan.

#### Scenario: Metadata read fails for one item

- **GIVEN** a scan encounters a recoverable metadata read failure for one included path
- **WHEN** scanning continues
- **THEN** MacStorageAtlas reports a scan error for that path
- **AND** successfully measured siblings remain in the result
- **AND** no invented metadata is attached to the failed item

#### Scenario: Scan is cancelled while collecting metadata

- **GIVEN** a scan is collecting metadata for included paths
- **WHEN** cancellation is requested
- **THEN** scanning stops without reporting completion
- **AND** any retained partial result contains only metadata observed before cancellation
- **AND** partial byte totals remain consistent with the scan measurement mode

#### Scenario: Cloud placeholder is encountered

- **GIVEN** a scan encounters a dataless cloud placeholder
- **WHEN** metadata is collected
- **THEN** MacStorageAtlas uses metadata-only operations
- **AND** it does not intentionally download or materialize the placeholder

### Requirement: Metadata does not authorize destructive actions

MacStorageAtlas SHALL present metadata as factual item details only. The application MUST NOT describe an item as safe to delete because it is old, large, or has a particular kind.

#### Scenario: Old file is selected

- **GIVEN** a selected item has an old modification time
- **WHEN** the user views item details
- **THEN** MacStorageAtlas displays the available date metadata
- **AND** it does not label the item as safe to delete
- **AND** existing Trash confirmation behavior remains required before cleanup

#### Scenario: Trash operation follows metadata display

- **GIVEN** the user has viewed metadata for a selected item
- **WHEN** the user chooses to move that item to Trash
- **THEN** MacStorageAtlas uses the existing recoverable Trash workflow
- **AND** metadata display does not bypass confirmation or path validation behavior
