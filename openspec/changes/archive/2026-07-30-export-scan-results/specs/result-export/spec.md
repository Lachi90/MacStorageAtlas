## ADDED Requirements

### Requirement: A completed scan result can be exported to a local file

MacStorageAtlas SHALL allow the user to export the current scan result as a
comma-separated-values document or as a JSON document. The user MUST choose the
destination through the system save-file interface, and MacStorageAtlas MUST NOT
write the export anywhere other than the chosen destination. Export MUST be
available only while a completed scan result is displayed and no scan is
running, and it MUST NOT start a scan, revisit the filesystem, or read file
contents.

#### Scenario: Exporting a completed result

- **GIVEN** a completed scan result is displayed
- **WHEN** the user exports the result and chooses a destination
- **THEN** MacStorageAtlas writes the export to that destination
- **AND** it reports that the export completed and how many items it contains
- **AND** it does not start a scan or read file contents

#### Scenario: Export is unavailable without a completed result

- **GIVEN** no scan has completed, or a scan is running
- **WHEN** the user looks for the export action
- **THEN** the export action is unavailable

#### Scenario: Dismissing the destination picker exports nothing

- **GIVEN** a completed scan result is displayed
- **WHEN** the user starts an export and dismisses the destination picker
- **THEN** no file is written
- **AND** the displayed result is unchanged
- **AND** MacStorageAtlas does not report a failure

### Requirement: Export scope follows the active filter

MacStorageAtlas SHALL export the full scan result when no filter is active, and
only the matched files when a filter is active. A full export MUST contain one
row for every scanned file and directory, and each directory row MUST carry that
directory's subtree totals. A filtered export MUST contain one row for each
matched file and MUST NOT contain directory rows, because a directory's subtree
total is not the total of the matched rows beneath it and reporting both under
one field would state totals that its own rows contradict. Every export MUST
record which of the two scopes produced it.

#### Scenario: Exporting with no filter active

- **GIVEN** a completed scan result is displayed with no active filter
- **WHEN** the user exports the result
- **THEN** the export contains one row for every scanned file and directory
- **AND** each directory row reports that directory's subtree totals
- **AND** the export records that its scope is the full result

#### Scenario: Exporting with a filter active

- **GIVEN** a completed scan result is displayed with an active filter
- **WHEN** the user exports the result
- **THEN** the export contains one row for each matched file
- **AND** it contains no directory rows
- **AND** the export records that its scope is the filtered result
- **AND** it records the criteria of the filter that produced it

#### Scenario: A filter that matches nothing exports no rows

- **GIVEN** a completed scan result is displayed with an active filter that
  matches no files
- **WHEN** the user exports the result
- **THEN** the export contains its scan metadata and no rows
- **AND** MacStorageAtlas reports that the export contains no items

### Requirement: Every exported row identifies and describes one scanned item

MacStorageAtlas SHALL write, for each exported item, its full path, its name,
its item kind, its depth below the scan root, its size fields, its shared-storage
indicator, its file extension, its file category, and its creation,
modification, and last-access timestamps. A timestamp that the scan could not
determine MUST be written as an empty value and MUST NOT be written as a
substitute instant. Item kind and file category MUST be written as stable tokens
that do not vary with the display language.

#### Scenario: A file row carries its metadata

- **GIVEN** a scanned file whose creation, modification, and last-access times
  are known
- **WHEN** the result is exported
- **THEN** the file's row states its full path, name, kind, and depth
- **AND** it states its extension and file category
- **AND** it states each timestamp as an unambiguous instant including its
  offset from UTC

#### Scenario: An unknown timestamp is written as empty

- **GIVEN** a scanned file whose creation time could not be determined
- **WHEN** the result is exported
- **THEN** the file's creation field is empty
- **AND** the file's remaining known fields are still written

#### Scenario: Depth locates an item without a hierarchy field

- **GIVEN** a full export of a scan result
- **WHEN** a consumer reads any row
- **THEN** the row's depth states how many levels below the scan root the item
  lies
- **AND** the scan root's own row has a depth of zero

### Requirement: Exported byte counts state the scan's measurement basis

MacStorageAtlas SHALL export three byte fields per item: the size the scan's
measurement mode measured, the size counted against that item, and the size
attributed to another path in the same scan. Every row MUST also state the
measurement mode that produced its byte fields, so that a row remains
self-describing after it is sorted, filtered, or combined with rows from another
export. MacStorageAtlas MUST NOT export a byte value measured under any mode
other than the one that produced the scan.

#### Scenario: Byte fields follow the completed scan's mode

- **GIVEN** a scan completed in shared-aware allocated mode
- **WHEN** the result is exported
- **THEN** every row's measured size is the allocated size the scan measured
- **AND** every row's counted size excludes the bytes attributed elsewhere
- **AND** every row's shared size states the bytes attributed elsewhere
- **AND** every row states that its measurement mode is shared-aware allocated

#### Scenario: A row remains interpretable in isolation

- **GIVEN** exports produced by two scans that used different measurement modes
- **WHEN** their rows are combined into one document by a consumer
- **THEN** each row still states the measurement mode that produced its byte
  fields

#### Scenario: The shared-storage indicator agrees with the shared size

- **GIVEN** a scanned file whose storage is counted against another path in the
  same scan
- **WHEN** the result is exported
- **THEN** the file's row marks it as sharing storage
- **AND** its shared size is greater than zero

### Requirement: An export carries versioned scan metadata

MacStorageAtlas SHALL record, with every export, a schema version, the scan root
path, the time the scan completed, the scan options that produced the result,
the measurement mode, the clone-accounting coverage, the total number of
exported items, the total exported bytes, and the export scope. The item total
MUST count every exported row. The byte total MUST sum the counted size of the
exported file rows only, because a directory row reports the total of its own
subtree and adding it to its descendants would count each file once per
ancestor. The schema version MUST change whenever a field is added, removed, or
redefined, so that a consumer can tell which shape it is reading.

#### Scenario: Metadata identifies the scan after the fact

- **GIVEN** an export written from a completed scan
- **WHEN** a consumer reads the export without access to the application
- **THEN** the export states the scan root, the scan completion time, and the
  scan options that produced it
- **AND** it states the measurement mode and the clone-accounting coverage
- **AND** it states its own schema version

#### Scenario: Metadata totals agree with the exported rows

- **GIVEN** an export of a completed scan result
- **WHEN** a consumer sums the counted size of every exported file row
- **THEN** the sum equals the total exported bytes stated in the metadata
- **AND** the number of exported rows equals the item total stated in the
  metadata

#### Scenario: Directory rollups are not added to their descendants

- **GIVEN** a full export whose rows include directories and the files beneath
  them
- **WHEN** a consumer compares the metadata byte total against the rows
- **THEN** the total equals the counted size of the scan root
- **AND** it does not include a directory row's subtree total in addition to the
  descendant rows that produced it

### Requirement: Recoverable scan errors accompany a JSON export

MacStorageAtlas SHALL record every recoverable error from the scan in a JSON
export, each with the path it occurred on and a description of what failed. A
comma-separated-values export MUST NOT be presented as a complete picture of the
scanned location when the scan had recoverable errors; MacStorageAtlas MUST
report the number of such errors to the user when an export completes.

#### Scenario: A JSON export lists the scan's errors

- **GIVEN** a scan completed with paths it could not read
- **WHEN** the user exports the result as JSON
- **THEN** the export lists each unreadable path and what failed on it

#### Scenario: A CSV export reports the errors it cannot carry

- **GIVEN** a scan completed with paths it could not read
- **WHEN** the user exports the result as comma-separated values
- **THEN** MacStorageAtlas reports how many paths the scan could not read
- **AND** the reported count is the number of recoverable errors from that scan

### Requirement: Export row order is deterministic

MacStorageAtlas SHALL write exported rows in an order that is fully determined
by the scan result and the export scope, so that exporting the same result twice
produces byte-identical documents. A full export MUST place each directory
immediately before its descendants and MUST order siblings by descending counted
size. A filtered export MUST order matched files by descending counted size.
Items that compare equal MUST be ordered by their path.

#### Scenario: Exporting the same result twice

- **GIVEN** a completed scan result
- **WHEN** the user exports it twice to two destinations without rescanning
- **THEN** the two documents are identical

#### Scenario: A full export keeps descendants under their directory

- **GIVEN** a full export of a scan result
- **WHEN** a consumer reads the rows in order
- **THEN** every directory row is followed by the rows for its descendants
- **AND** sibling rows appear in descending order of counted size

### Requirement: A comma-separated-values export is readable by spreadsheet applications

MacStorageAtlas SHALL write a comma-separated-values export with a single header
row naming each field, and MUST quote and escape any field containing a comma, a
quotation mark, or a line break so that the document parses correctly.
Non-ASCII text MUST be encoded so that spreadsheet applications display it
correctly. A text field whose value begins with a character that a spreadsheet
application would interpret as the start of a formula MUST be written so that
the application treats it as text.

#### Scenario: A path containing separator characters is escaped

- **GIVEN** a scanned file whose name contains a comma, a quotation mark, and a
  line break
- **WHEN** the result is exported as comma-separated values
- **THEN** the file's row parses as a single row with the correct field count
- **AND** the name reads back exactly as it was scanned

#### Scenario: A path containing non-ASCII characters is preserved

- **GIVEN** a scanned file whose name contains non-ASCII characters
- **WHEN** the export is opened in a spreadsheet application
- **THEN** the name displays with its original characters

#### Scenario: A path that starts with a formula character is inert

- **GIVEN** a scanned file whose name begins with a formula character such as an
  equals sign
- **WHEN** the export is opened in a spreadsheet application
- **THEN** the cell displays the name as text
- **AND** the application does not evaluate it as a formula

### Requirement: A JSON export preserves exact values and round-trips

MacStorageAtlas SHALL write every JSON export field with the exact value the
scan produced, without the text substitutions a comma-separated-values export
applies for spreadsheet safety. A JSON export MUST be readable back into the
same metadata and item values it was written from.

#### Scenario: JSON keeps the exact path

- **GIVEN** a scanned file whose name begins with a formula character
- **WHEN** the result is exported as JSON
- **THEN** the exported path is exactly the scanned path, with no added or
  altered characters

#### Scenario: A JSON export reads back unchanged

- **GIVEN** a JSON export of a completed scan result
- **WHEN** it is read back into scan metadata and item values
- **THEN** every metadata field and every item field equals the value it was
  written from

### Requirement: Export streams, stays responsive, and is cancellable

MacStorageAtlas SHALL write an export incrementally as it walks the result, and
MUST NOT assemble the whole document in memory before writing it. The export
MUST run without blocking the user interface, MUST leave the displayed scan
result unchanged, and the user MUST be able to cancel an export in progress.

#### Scenario: Exporting a very large result

- **GIVEN** a completed scan result containing hundreds of thousands of items
- **WHEN** the user exports it
- **THEN** the application remains responsive while the export runs
- **AND** the export does not hold the whole document in memory
- **AND** the displayed scan result is unchanged when the export completes

#### Scenario: Cancelling an export in progress

- **GIVEN** an export is running
- **WHEN** the user cancels it
- **THEN** the export stops
- **AND** MacStorageAtlas reports that the export was cancelled
- **AND** the displayed scan result is unchanged

### Requirement: A cancelled or failed export never leaves a file presented as complete

MacStorageAtlas SHALL publish an export at the chosen destination only after the
whole document has been written. If the export is cancelled or the write fails,
MacStorageAtlas MUST NOT leave a partial document at the chosen destination, and
MUST report what went wrong rather than reporting success. If a file already
exists at the chosen destination, it MUST remain untouched unless the export
completes.

#### Scenario: A write failure leaves no partial file

- **GIVEN** an export is running to a destination that becomes unwritable
- **WHEN** the write fails
- **THEN** no partial document remains at the destination
- **AND** MacStorageAtlas reports that the export failed and why
- **AND** it does not report that the export completed

#### Scenario: Cancellation leaves an existing file intact

- **GIVEN** a file already exists at the chosen destination
- **AND** the user confirms replacing it
- **WHEN** the export is cancelled before it completes
- **THEN** the existing file is unchanged

### Requirement: An export stays on the user's machine

MacStorageAtlas SHALL treat an export as a user-initiated copy of scan metadata
that the user has chosen to persist. It MUST write only to the destination the
user chose, MUST NOT transmit the export or any part of it anywhere, and MUST
NOT read or include the contents of any scanned file.

#### Scenario: Export writes only where the user chose

- **GIVEN** the user exports a scan result to a chosen destination
- **WHEN** the export completes
- **THEN** the only file MacStorageAtlas created or modified is that destination
- **AND** no scan data was transmitted off the machine

#### Scenario: Export never reads file contents

- **GIVEN** a completed scan result containing files of any type
- **WHEN** the result is exported
- **THEN** the export contains only paths, sizes, and filesystem metadata
- **AND** no scanned file's contents were read
