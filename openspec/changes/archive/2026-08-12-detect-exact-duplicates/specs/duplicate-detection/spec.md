## ADDED Requirements

### Requirement: Duplicate analysis is explicit and post-scan

MacStorageAtlas SHALL let the user start exact duplicate analysis only for a completed scan result. Starting duplicate analysis MUST NOT start a new scan, change scan totals, change the scan measurement mode, apply cleanup actions, or modify the filesystem.

#### Scenario: Start analysis from completed scan

- **GIVEN** a completed scan result is displayed
- **WHEN** the user starts duplicate analysis
- **THEN** MacStorageAtlas analyzes files from that completed scan result
- **AND** the displayed scan totals and measurement basis remain unchanged
- **AND** no filesystem item is moved, copied, deleted, or selected for cleanup

#### Scenario: Analysis is unavailable during scanning

- **GIVEN** a scan is running
- **WHEN** the user views available actions
- **THEN** the duplicate analysis action is unavailable or ignored
- **AND** no file contents are read for duplicate analysis

### Requirement: Candidate selection avoids unnecessary content reads

MacStorageAtlas SHALL consider regular files as duplicate candidates by current logical length. Files without another candidate of the same current logical length MUST NOT have their contents read for duplicate analysis. Zero-length files MUST be excluded by default.

#### Scenario: Unique size is not read

- **GIVEN** a completed scan result contains a regular file whose current logical length is unique within the analysis scope
- **WHEN** duplicate analysis runs
- **THEN** MacStorageAtlas does not read that file's contents
- **AND** the file is not reported as part of a duplicate group

#### Scenario: Zero-length files are excluded by default

- **GIVEN** a completed scan result contains multiple zero-length files
- **WHEN** duplicate analysis runs with default settings
- **THEN** those zero-length files are not reported as duplicate waste
- **AND** they do not contribute to duplicate group count or reclaimable size

#### Scenario: Allocated-mode scan still uses logical length for candidates

- **GIVEN** a completed scan result was produced in an allocated measurement mode
- **AND** two regular files have the same current logical length
- **WHEN** duplicate analysis runs
- **THEN** MacStorageAtlas can treat those files as same-length duplicate candidates
- **AND** it does not use allocated byte counts as proof of duplicate content

### Requirement: Exact duplicate groups require verified byte equality

MacStorageAtlas SHALL report files as exact duplicates only after verifying byte-for-byte equality. Matching names, paths, extensions, timestamps, sizes, samples, or hashes alone MUST NOT be sufficient to present a duplicate group as exact.

#### Scenario: Same-size files with different contents are not duplicates

- **GIVEN** two regular files have the same current logical length
- **AND** their contents differ
- **WHEN** duplicate analysis completes
- **THEN** MacStorageAtlas does not report those files as an exact duplicate group

#### Scenario: Equal contents are reported together

- **GIVEN** two regular files have the same current logical length
- **AND** their contents are byte-for-byte identical
- **WHEN** duplicate analysis completes
- **THEN** MacStorageAtlas reports those files in the same exact duplicate group

#### Scenario: Hash equality is confirmed before display

- **GIVEN** two candidate files produce the same content hash during analysis
- **WHEN** MacStorageAtlas prepares the duplicate result
- **THEN** it confirms byte-for-byte equality before displaying them as exact duplicates

### Requirement: Analysis progress is cancellable and bounded

MacStorageAtlas SHALL report duplicate-analysis progress while candidate files are examined. The user MUST be able to cancel analysis. Cancelling analysis MUST stop additional duplicate-analysis work as soon as practical and MUST leave the completed scan result and filesystem unchanged.

#### Scenario: Progress is reported while hashing candidates

- **GIVEN** duplicate analysis is reading candidate file contents
- **WHEN** progress is displayed
- **THEN** MacStorageAtlas reports that duplicate analysis is running
- **AND** it reports progress in terms of analyzed candidates, bytes, groups, or current path

#### Scenario: User cancels analysis

- **GIVEN** duplicate analysis is running
- **WHEN** the user cancels duplicate analysis
- **THEN** MacStorageAtlas stops reading additional duplicate candidates as soon as practical
- **AND** it does not display the cancelled partial result as a completed duplicate result
- **AND** the completed scan result remains displayed
- **AND** the filesystem remains unchanged

### Requirement: Changed and unreadable files are not mislabeled

MacStorageAtlas SHALL revalidate candidate files during duplicate analysis. A file that is missing, unreadable, replaced, or changed in size or identity during analysis MUST NOT be reported as an exact duplicate. Such files MUST be reported as skipped or failed with a user-visible reason.

#### Scenario: Candidate changes size during analysis

- **GIVEN** a candidate file's current logical length changes while duplicate analysis is running
- **WHEN** MacStorageAtlas revalidates the candidate
- **THEN** the file is not reported as an exact duplicate
- **AND** the duplicate result explains that the file changed during analysis

#### Scenario: Candidate cannot be read

- **GIVEN** a same-length candidate file cannot be opened or read
- **WHEN** duplicate analysis reaches that file
- **THEN** the file is not reported as an exact duplicate
- **AND** the duplicate result reports a read failure for that path
- **AND** analysis continues for other readable candidates unless cancelled

#### Scenario: Candidate is replaced during analysis

- **GIVEN** a candidate path refers to a different file identity than the one being analyzed
- **WHEN** MacStorageAtlas revalidates the candidate
- **THEN** the file is not reported as an exact duplicate
- **AND** the duplicate result explains that the file changed during analysis

### Requirement: Hardlinks are distinguished from reclaimable duplicates

MacStorageAtlas SHALL identify known hardlinked paths within duplicate analysis results. Hardlinked paths MUST NOT be counted as reclaimable duplicate copies while another link to the same file identity remains in the group.

#### Scenario: Hardlinked paths are shown as linked

- **GIVEN** two scanned paths refer to the same current file identity
- **WHEN** duplicate analysis reports matching content for that identity
- **THEN** MacStorageAtlas identifies the paths as linked paths
- **AND** it does not present them as separate reclaimable duplicate copies

#### Scenario: Hardlink does not inflate reclaimable total

- **GIVEN** a duplicate result contains two hardlinked paths and one ordinary duplicate copy
- **WHEN** MacStorageAtlas computes reclaimable duplicate size
- **THEN** the hardlinked paths contribute at most one retained copy to the group
- **AND** only ordinary duplicate copies beyond the retained copy contribute to reclaimable size

### Requirement: Duplicate review preserves one copy per group

MacStorageAtlas SHALL present duplicate groups with enough information for user review, including file names, paths, sizes, linked-path status when known, skipped-file counts or reasons, and reclaimable size. Reclaimable size MUST preserve at least one non-linked copy in every exact duplicate group.

#### Scenario: Reclaimable total preserves one copy

- **GIVEN** an exact duplicate group contains three ordinary files with equal logical length
- **WHEN** MacStorageAtlas displays the group reclaimable size
- **THEN** the reclaimable size is no greater than the size of two files
- **AND** one copy is preserved in the group arithmetic

#### Scenario: Duplicate review does not claim files are safe to delete

- **GIVEN** an exact duplicate group is displayed
- **WHEN** the user reviews the group
- **THEN** MacStorageAtlas describes the files as exact duplicates
- **AND** it does not describe any file as safe to delete
- **AND** it does not automatically select a file for cleanup

#### Scenario: No duplicates found

- **GIVEN** duplicate analysis completes without finding any exact duplicate groups
- **WHEN** the duplicate review is displayed
- **THEN** MacStorageAtlas reports that no exact duplicates were found
- **AND** cleanup basket contents remain unchanged

### Requirement: Cleanup integration remains explicit and reviewed

MacStorageAtlas SHALL integrate duplicate review with the cleanup basket only through explicit user actions. Duplicate analysis MUST NOT add items to the cleanup basket automatically. Any duplicate item added to the cleanup basket MUST follow existing cleanup-basket protected-path, overlap, stale-file, review, and recoverability rules.

#### Scenario: User adds selected duplicate file to basket

- **GIVEN** an exact duplicate group is displayed
- **AND** the user selects one duplicate file
- **WHEN** the user adds the selected file to the cleanup basket
- **THEN** the basket contains a reference to that scanned item if existing basket rules allow it
- **AND** the duplicate result remains displayed
- **AND** the filesystem remains unchanged

#### Scenario: Completing analysis does not populate basket

- **GIVEN** duplicate analysis finds one or more exact duplicate groups
- **WHEN** analysis completes
- **THEN** the cleanup basket is unchanged
- **AND** no duplicate file is moved, copied, deleted, or marked for cleanup

#### Scenario: Basket review remains required

- **GIVEN** the user added duplicate files to the cleanup basket
- **WHEN** the user starts a cleanup basket operation
- **THEN** MacStorageAtlas requires the existing final cleanup review
- **AND** it does not bypass cleanup preflight because the files came from duplicate analysis

### Requirement: Duplicate analysis preserves privacy and cloud-placeholder safety

MacStorageAtlas SHALL keep duplicate analysis local to the user's Mac. Duplicate analysis MUST NOT send paths, names, hashes, file contents, or results externally; MUST NOT persist file hashes or file contents; and MUST NOT intentionally materialize dataless cloud placeholders. Files whose contents are not locally available MUST be skipped with a user-visible reason.

#### Scenario: Analysis keeps hashes local and temporary

- **GIVEN** duplicate analysis hashes candidate file contents
- **WHEN** analysis completes or is cancelled
- **THEN** MacStorageAtlas does not persist the content hashes
- **AND** it does not transmit hashes, paths, names, file contents, or duplicate results externally

#### Scenario: Cloud-only file is skipped

- **GIVEN** a candidate file's contents are not locally available
- **WHEN** duplicate analysis evaluates that candidate
- **THEN** MacStorageAtlas does not intentionally download or materialize the file contents
- **AND** it does not report the file as an exact duplicate
- **AND** it reports that the file was skipped because its contents were not local

#### Scenario: Duplicate results are discarded with the scan result

- **GIVEN** duplicate analysis completed for a scan result
- **WHEN** the displayed scan result is replaced by another scan
- **THEN** the duplicate result is cleared
- **AND** no duplicate-analysis hash cache is retained for the old scan
