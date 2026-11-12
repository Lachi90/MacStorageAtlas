# Spec Delta

## Purpose

Define how a scan walks its root, represents every entry it encounters, and
keeps failures confined to the entries that cause them so one unusual entry
never discards the rest of a scan.

## ADDED Requirements

### Requirement: Entries with unusual names are scanned

The scanner SHALL include every entry the filesystem enumerates, regardless of
whether its name consists only of whitespace or invisible characters. Such
entries SHALL be measured, aggregated, and browsable like any other entry,
and their real names SHALL be preserved for search, filters, export, history,
and cleanup.

#### Scenario: Directory name is a single space

- **GIVEN** the scan root contains a directory named with one space character
- **AND** that directory contains a 10-byte file
- **WHEN** the scan completes
- **THEN** the directory appears in the result with a counted size of 10 bytes
- **AND** the scan reports no error for it

#### Scenario: File name consists of non-ASCII whitespace

- **GIVEN** the scan root contains a file whose name is a no-break space
- **WHEN** the scan completes
- **THEN** the file is included in its parent's totals
- **AND** an export of the result contains the file's real name

### Requirement: Unusual names remain visible in the tree

The result tree SHALL show an entry whose name is empty, whitespace-only, or
invisible with a visible representation that cannot be mistaken for a
different real name, while every non-presentation use keeps the real name.

#### Scenario: User browses a whitespace-only directory

- **GIVEN** a completed result contains a directory named with one space
- **WHEN** the user expands its parent in the tree
- **THEN** the row shows a visible placeholder that indicates the name is
  blank
- **AND** revealing the item in Finder targets the real path

#### Scenario: Name cannot be derived from the path

- **GIVEN** the filesystem enumerates an entry whose path yields no file name
- **WHEN** the scan adds it to the result
- **THEN** the entry is shown using its full path

### Requirement: Entry failures are contained

An unexpected failure while processing one entry MUST be recorded as a scan
error with that entry's path, message, and exception type, and scanning SHALL
continue with the remaining entries. Totals SHALL exclude the failed entry.

#### Scenario: One entry raises an unexpected exception

- **GIVEN** a directory contains three files
- **AND** processing the second file raises an unexpected exception
- **WHEN** the scan completes
- **THEN** the first and third files are included in the totals
- **AND** the scan errors list the second file's path and exception type
- **AND** the scan reports completion

#### Scenario: Scan is cancelled

- **GIVEN** a scan is running
- **WHEN** the user cancels it
- **THEN** cancellation is not recorded as an entry failure
- **AND** the scan does not report completion

### Requirement: Unexpected scan failures do not terminate the app

If a scan fails as a whole for a reason other than cancellation, the app SHALL
stop the scan, remain running, and tell the user that the scan failed. It
MUST NOT present a partial tree as a completed result.

#### Scenario: Scanner fails before completion

- **GIVEN** a scan has published progress for some entries
- **AND** the scanner then fails with an unexpected exception
- **WHEN** the app handles the failure
- **THEN** the app is still running and no longer scanning
- **AND** the user sees that the scan failed
- **AND** no completion time is recorded for the scan
