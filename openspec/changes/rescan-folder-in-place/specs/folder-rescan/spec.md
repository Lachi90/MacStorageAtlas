## ADDED Requirements

### Requirement: A folder of the displayed result can be rescanned in place

Atlas Storage Analyzer SHALL let the user rescan one directory of the displayed
completed result, including an unexpanded package, without rescanning the rest
of the scan root. The action MUST be available only while a completed result is
displayed, the selected item is a directory, no scan or folder rescan is
running, and no update restart is pending. Rescanning the scan root itself MUST
behave as a full rescan of that root with the result's scan options.

#### Scenario: Rescanning a subfolder refreshes only that folder

- **GIVEN** a completed result for a scan root
- **AND** a file was added inside one of its subfolders after the scan
- **WHEN** the user rescans that subfolder
- **THEN** the subfolder shows the added file and its new total
- **AND** every ancestor of the subfolder up to the scan root shows a total
  adjusted by the subfolder's change
- **AND** items outside the subfolder keep the values they had before

#### Scenario: The action is unavailable for a file

- **GIVEN** a completed result is displayed
- **WHEN** the user selects a file
- **THEN** the folder rescan action is unavailable

#### Scenario: The action is unavailable while scanning

- **GIVEN** a scan or folder rescan is running
- **WHEN** the user selects a directory of the displayed result
- **THEN** the folder rescan action is unavailable

#### Scenario: Rescanning the scan root rescans the whole result

- **GIVEN** a completed result is displayed
- **WHEN** the user rescans the scan root through the folder rescan action
- **THEN** Atlas Storage Analyzer rescans the whole root with the scan options of
  the displayed result

### Requirement: A folder rescan does not overlap operations that use the result

Atlas Storage Analyzer SHALL NOT start a folder rescan while an export, a move
to Trash, or a cleanup basket operation is running, and SHALL NOT start a move
to Trash or a cleanup basket operation while a folder rescan is running,
because each of them reads or changes the displayed result.

#### Scenario: An export blocks a folder rescan

- **GIVEN** an export of the displayed result is running
- **WHEN** the user selects a directory of the result
- **THEN** the folder rescan action is unavailable

#### Scenario: A folder rescan blocks cleanup

- **GIVEN** a folder rescan is running
- **WHEN** the user selects an item of the displayed result
- **THEN** Move to Trash and the cleanup basket operations are unavailable

### Requirement: A folder rescan keeps the result's measurement basis

Atlas Storage Analyzer SHALL measure a rescanned folder with the scan options and
measurement mode that produced the displayed result, even when the scan options
currently selected in the toolbar differ. Every byte count and label of the
merged result MUST use one measurement mode.

#### Scenario: Changed toolbar options do not leak into a folder rescan

- **GIVEN** a completed result measured in logical mode without hidden files
- **AND** the user has since switched the toolbar to allocated mode with hidden
  files included
- **WHEN** the user rescans a subfolder
- **THEN** the subfolder is measured in logical mode without hidden files
- **AND** Atlas Storage Analyzer states that the folder was rescanned with the
  result's original options

### Requirement: A folder rescan applies the same traversal rules as a full scan

Atlas Storage Analyzer SHALL apply hidden-file, package-expansion, symbolic-link,
and firmlink-equivalent omission rules to a rescanned folder exactly as a full
scan of the original scan root would apply them to the same folder. An
equivalent path omitted from the original result MUST remain omitted after the
folder that contains it is rescanned.

#### Scenario: A firmlinked alternate inside the folder stays omitted

- **GIVEN** a completed result whose scan omitted an equivalent alternate path
  located inside a subfolder
- **WHEN** the user rescans that subfolder
- **THEN** the alternate path is still omitted
- **AND** the result still discloses the omission

### Requirement: Order-dependent measurements fall back to a full rescan

Atlas Storage Analyzer SHALL NOT rescan a single folder when the displayed result
was measured in shared-aware allocated mode or with symbolic links followed,
because the bytes attributed to a folder in those scans depend on which paths
were visited before it. In those cases Atlas Storage Analyzer MUST explain why a
folder rescan is not possible and MUST offer a full rescan of the scan root with
the result's scan options instead. It MUST NOT start the full rescan without the
user's confirmation.

#### Scenario: Shared-aware results offer a full rescan

- **GIVEN** a completed result measured in shared-aware allocated mode
- **WHEN** the user asks to rescan a subfolder
- **THEN** Atlas Storage Analyzer explains that hardlinks and clones are
  attributed across the whole scan
- **AND** it offers a full rescan of the scan root
- **AND** the displayed result is unchanged until the user accepts

#### Scenario: Followed symbolic links offer a full rescan

- **GIVEN** a completed result scanned with symbolic links followed
- **WHEN** the user asks to rescan a subfolder
- **THEN** Atlas Storage Analyzer offers a full rescan of the scan root instead

### Requirement: An unfinished folder rescan leaves the result unchanged

Atlas Storage Analyzer SHALL replace the stale folder only after its rescan
completes. A folder rescan that the user cancels, or that fails before it
completes, MUST leave the displayed result, its errors, its cleanup basket, and
its stored history exactly as they were. The displayed result MUST remain
browsable while a folder rescan runs, and the user MUST be able to cancel it.

#### Scenario: Cancelling a folder rescan keeps the previous result

- **GIVEN** a folder rescan is running
- **WHEN** the user stops it
- **THEN** the folder shows its previous contents and totals
- **AND** no snapshot is recorded
- **AND** the cleanup basket is unchanged

#### Scenario: The result stays browsable during a folder rescan

- **GIVEN** a folder rescan is running
- **WHEN** the user browses the result tree
- **THEN** the previous result is shown
- **AND** progress for the folder rescan is visible

### Requirement: A folder that no longer exists is removed from the result

Atlas Storage Analyzer SHALL remove a folder from the displayed result when its
path no longer refers to a directory at the time of the rescan. It MUST adjust
ancestor totals accordingly and MUST tell the user that the folder was removed
because it no longer exists.

#### Scenario: Rescanning a deleted folder removes it

- **GIVEN** a subfolder of the displayed result was deleted outside the app
- **WHEN** the user rescans that subfolder
- **THEN** the subfolder no longer appears in the result
- **AND** its former parent's total no longer includes it
- **AND** Atlas Storage Analyzer states that the folder no longer exists

### Requirement: Errors and completeness reflect the merged result

Atlas Storage Analyzer SHALL replace the recoverable errors and omitted equivalent
paths located inside a rescanned folder with those found by its rescan, keep
those located outside it, and recompute access guidance and the completeness
verdict from the merged set.

#### Scenario: Granting access and rescanning the blocked folder clears its error

- **GIVEN** a completed result with a recoverable access error inside one
  subfolder and no other errors
- **AND** the user has since granted access to that subfolder
- **WHEN** the user rescans that subfolder and it completes without errors
- **THEN** the result lists no recoverable errors
- **AND** its completeness verdict no longer reports the access error

#### Scenario: Errors outside the folder are kept

- **GIVEN** a completed result with recoverable errors in two different
  subfolders
- **WHEN** the user rescans one of those subfolders and it completes without
  errors
- **THEN** the result still lists the error in the other subfolder

### Requirement: Dependent views follow the merged result

Atlas Storage Analyzer SHALL update the result tree, treemap, active filter
results, Largest files, File types, and the selected-item details from the
merged result. It SHALL keep the rescanned folder selected and keep its
ancestors expanded when the folder still exists. It MUST clear exact-duplicate
results that were computed before the folder rescan and MUST tell the user that
duplicate analysis can be run again.

#### Scenario: The treemap and lists show the refreshed sizes

- **GIVEN** a completed result in which a large file inside a subfolder was
  deleted outside the app
- **WHEN** the user rescans that subfolder
- **THEN** the deleted file no longer appears in Largest files
- **AND** the treemap and File types reflect the subfolder's new total

#### Scenario: Duplicate results are cleared after a folder rescan

- **GIVEN** exact-duplicate results are displayed
- **WHEN** a folder rescan completes
- **THEN** the duplicate results are cleared
- **AND** Atlas Storage Analyzer states that duplicate analysis can be run again

### Requirement: The cleanup basket stays honest after a folder rescan

Atlas Storage Analyzer SHALL keep cleanup basket entries outside a rescanned
folder unchanged. Entries inside it MUST be revalidated by path against the
refreshed subtree: entries whose path still exists MUST take the refreshed
size, and entries whose path no longer exists MUST be removed from the basket
with a notice that names how many entries were removed. An entry for a folder
that contains the rescanned folder MUST stay in the basket and take its
refreshed total.

#### Scenario: A basket entry inside the folder takes its new size

- **GIVEN** a basket entry for a file inside a subfolder
- **AND** that file grew after the scan
- **WHEN** the user rescans the subfolder
- **THEN** the basket entry shows the file's refreshed size
- **AND** the basket total includes the refreshed size

#### Scenario: A basket entry that vanished is removed

- **GIVEN** a basket entry for a file inside a subfolder
- **AND** that file was deleted outside the app
- **WHEN** the user rescans the subfolder
- **THEN** the entry is removed from the basket
- **AND** Atlas Storage Analyzer states that one basket entry was removed

### Requirement: A refreshed result discloses that it mixes measurement times

Atlas Storage Analyzer SHALL show, while a result contains rescanned folders, the
time of the original scan and the number of rescanned folders, with a way to
see each rescanned folder and the time of its rescan. A later rescan of an
ancestor MUST supersede the recorded rescans of its descendants, and a full
rescan MUST clear the list.

#### Scenario: The result summary lists refreshed folders

- **GIVEN** a completed result in which two subfolders were rescanned
- **WHEN** the user views the result summary
- **THEN** it states the original scan time
- **AND** it lists both subfolders with their rescan times

#### Scenario: An ancestor rescan supersedes its descendants

- **GIVEN** a result in which a nested subfolder was rescanned
- **WHEN** the user rescans that subfolder's parent
- **THEN** the result lists only the parent as rescanned

### Requirement: Folder rescans stay private and metadata-only

Atlas Storage Analyzer SHALL read only filesystem metadata during a folder rescan,
exactly as during a full scan. It MUST NOT read file contents or send scanned
paths anywhere outside the running application.

#### Scenario: A folder rescan reads no file contents

- **GIVEN** a folder containing files
- **WHEN** the user rescans that folder
- **THEN** no file's contents were read
- **AND** no scanned path left the machine

### Requirement: A folder rescan has a keyboard equivalent

Atlas Storage Analyzer SHALL let the user rescan the selected directory with
Shift-Command-R and SHALL give the on-screen action an accessible name that
states which folder it rescans.

#### Scenario: Rescanning the selected folder from the keyboard

- **GIVEN** a directory is selected in the result tree
- **WHEN** the user presses Shift-Command-R
- **THEN** Atlas Storage Analyzer rescans that directory
