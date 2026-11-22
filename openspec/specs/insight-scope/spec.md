# insight-scope Specification

## Purpose
Define how the Largest files and File types tabs follow the folder selected in
the result tree or treemap: what sets and resets the scope, how the scope is
shown, how it follows changes to the result, how it combines with filters, and
which result features deliberately ignore it.

## Requirements

### Requirement: Insight tabs describe the selected folder and its descendants

Atlas Storage Analyzer SHALL limit the Largest files list and the File types
summary to the files inside the insight scope, including files in every
descendant directory of the scope. Before the user has selected a directory in
the current result, the insight scope MUST be the scan root.

#### Scenario: A completed scan starts with the whole result in scope

- **GIVEN** a scan has completed
- **AND** the user has not selected anything in the result
- **WHEN** the user views the Largest files and File types tabs
- **THEN** both describe every file in the scan result

#### Scenario: Selecting a folder in the tree scopes both tabs

- **GIVEN** a completed scan result containing the folder “Downloads” with
  nested subfolders
- **WHEN** the user selects “Downloads” in the folder tree
- **THEN** the Largest files list contains only files inside “Downloads” or its
  subfolders
- **AND** the File types summary counts only files inside “Downloads” or its
  subfolders

#### Scenario: Selecting a folder in the treemap scopes both tabs

- **GIVEN** a completed scan result whose treemap shows the folder “Projects”
- **WHEN** the user selects “Projects” in the treemap
- **THEN** both tabs describe only files inside “Projects” or its subfolders

#### Scenario: A scoped list keeps its size limit

- **GIVEN** the selected folder contains more files than the Largest files
  list shows
- **WHEN** the user views the Largest files list
- **THEN** it shows the largest files of that folder up to the usual limit
- **AND** files outside that folder do not take any of those places

#### Scenario: A folder without files yields empty tabs

- **GIVEN** the user selects a folder that contains no files at any depth
- **WHEN** the user views the Largest files and File types tabs
- **THEN** both state that the scope contains no files

### Requirement: Selecting a file scopes the tabs to its containing folder

When the user selects a file in the folder tree or the treemap, Atlas Storage
Analyzer SHALL set the insight scope to the directory that directly contains
that file. Selecting a file located directly in the scan root MUST set the
scope to the scan root.

#### Scenario: Selecting a file in a nested folder

- **GIVEN** a completed scan result with the file “movie.mov” inside the
  folder “Videos”
- **WHEN** the user selects “movie.mov” in the folder tree
- **THEN** both tabs describe the files inside “Videos” and its subfolders

#### Scenario: Selecting a file in the scan root

- **GIVEN** a file located directly in the scan root
- **WHEN** the user selects it in the treemap
- **THEN** both tabs describe the whole scan result

### Requirement: Selecting inside an insight or duplicate list keeps the scope

Atlas Storage Analyzer SHALL NOT change the insight scope when the user selects
an entry in the Largest files list or the Duplicates list, even though that
selection replaces the folder-tree or treemap selection. The insight scope MUST
change only through a selection in the folder tree or the treemap, the reset
action, or a change of the result itself.

#### Scenario: Picking a large file keeps the list stable

- **GIVEN** the insight scope is the folder “Downloads”
- **WHEN** the user selects a file in the Largest files list
- **THEN** the insight scope remains “Downloads”
- **AND** the Largest files list keeps its entries and order

#### Scenario: Picking a duplicate keeps the scope

- **GIVEN** the insight scope is the folder “Photos”
- **WHEN** the user selects an entry in the Duplicates list that lies outside
  “Photos”
- **THEN** the insight scope remains “Photos”

#### Scenario: A filter that hides the selected folder keeps the scope

- **GIVEN** the insight scope is the folder “Archive”
- **WHEN** the user applies a filter that hides “Archive” in the folder tree
- **THEN** the insight scope remains “Archive”
- **AND** both tabs state that no matching files exist in that scope

### Requirement: The insight scope is visible and can be reset in one step

Atlas Storage Analyzer SHALL state the current insight scope as text on both the
Largest files and the File types tabs, naming the scope folder, and SHALL offer
an action on both tabs that returns the scope to the whole scan result. The
reset action MUST be available only while the scope is narrower than the scan
root, and the scope statement MUST NOT rely on color alone.

#### Scenario: A narrowed scope is named

- **GIVEN** the insight scope is the folder “Downloads”
- **WHEN** the user views either tab
- **THEN** the tab states that it shows results in “Downloads”
- **AND** the full path of “Downloads” is discoverable from the statement

#### Scenario: Returning to the whole scan

- **GIVEN** the insight scope is the folder “Downloads”
- **WHEN** the user activates the reset action
- **THEN** both tabs describe the whole scan result
- **AND** the folder-tree selection and the treemap level are unchanged
- **AND** the reset action is no longer available

#### Scenario: The whole scan needs no reset

- **GIVEN** the insight scope is the scan root
- **WHEN** the user views either tab
- **THEN** the tab states that it shows results for the whole scan
- **AND** no reset action is offered

### Requirement: The scope follows the result when it changes

Atlas Storage Analyzer SHALL keep the insight scope pointing at a directory that
exists in the displayed result. When the scope directory is removed from the
result, the scope MUST move to its nearest ancestor directory that remains in
the result. When part of the result is replaced, the scope MUST be resolved to
the directory with the same path in the replaced result, or to its nearest
remaining ancestor if that path no longer exists. A new scan result MUST reset
the scope to its scan root.

#### Scenario: The scope folder is moved to Trash

- **GIVEN** the insight scope is the folder “Old Projects” inside “Documents”
- **WHEN** the user moves “Old Projects” to Trash and the result is updated
  without a rescan
- **THEN** the insight scope becomes “Documents”
- **AND** both tabs no longer list files from “Old Projects”

#### Scenario: A replaced subtree keeps the scope by path

- **GIVEN** the insight scope is the folder “Caches”
- **WHEN** the part of the result containing “Caches” is replaced with newly
  measured contents that still include “Caches”
- **THEN** the insight scope is the newly measured “Caches”
- **AND** both tabs describe its newly measured files

#### Scenario: A replaced subtree no longer contains the scope folder

- **GIVEN** the insight scope is the folder “Build” inside “Project”
- **WHEN** the part of the result containing “Project” is replaced and “Build”
  no longer exists
- **THEN** the insight scope becomes “Project”

#### Scenario: A new scan resets the scope

- **GIVEN** the insight scope is narrower than the scan root
- **WHEN** a new scan completes
- **THEN** the insight scope is the new scan root

### Requirement: Scoping insights stays responsive and does not rescan

Atlas Storage Analyzer SHALL recompute the scoped Largest files list and File
types summary from the completed scan result without reading the filesystem,
without blocking the user interface, and without changing the scan's
measurement mode or size labels. When the scope changes again before a
recomputation finishes, only the result for the latest scope MUST be shown.

#### Scenario: Changing scope does not touch the filesystem

- **GIVEN** a completed scan result
- **WHEN** the user selects a different folder
- **THEN** the tabs update without reading any scanned path
- **AND** byte values use the measurement mode of the scan that produced the
  result

#### Scenario: Rapid selection changes show the latest scope

- **GIVEN** a very large scan result
- **WHEN** the user selects several folders in quick succession
- **THEN** the interface stays responsive
- **AND** the tabs finally describe the last selected folder

### Requirement: Other result features ignore the insight scope

Atlas Storage Analyzer SHALL NOT narrow CSV or JSON export, scan history
snapshots, duplicate analysis, the cleanup basket, the folder tree, the treemap,
or the filter panel's match count and matched size by the insight scope.

#### Scenario: Export ignores a narrowed scope

- **GIVEN** the insight scope is the folder “Downloads”
- **WHEN** the user exports the result
- **THEN** the export covers the same items as it would with the scan root in
  scope

#### Scenario: Match totals ignore a narrowed scope

- **GIVEN** a filter is active and the insight scope is the folder “Downloads”
- **WHEN** the user views the filter match count and matched size
- **THEN** they describe every match in the whole scan result
