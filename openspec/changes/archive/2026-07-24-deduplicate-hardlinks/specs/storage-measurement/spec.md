## ADDED Requirements

### Requirement: Hardlink-aware allocated measurement counts file identities once

In hardlink-aware allocated mode, the scanner SHALL count the allocated bytes
of each successfully measured filesystem file identity once within the active
scan scope. File and directory byte totals, progress totals, and completed
result totals MUST use those counted contributions.

#### Scenario: Two included paths are hardlinks

- **GIVEN** two included paths are hardlinks to the same file
- **WHEN** they are scanned in hardlink-aware allocated mode
- **THEN** the scan file count includes both paths
- **AND** exactly one allocation contributes to the scan byte total
- **AND** directory and completed result totals remain additive

#### Scenario: Equal identity numbers occur on different volumes

- **GIVEN** two included paths refer to different files on different volumes
- **WHEN** they are scanned in hardlink-aware allocated mode
- **THEN** each file contributes its allocated bytes
- **AND** the files are not treated as the same filesystem identity

#### Scenario: Another hardlink is outside the scan scope

- **GIVEN** an included file has another hardlink outside the active scan scope
- **WHEN** the included path is scanned in hardlink-aware allocated mode
- **THEN** the included path contributes its allocated bytes once
- **AND** the external path does not appear in the result or suppress the
  included contribution

### Requirement: Repeated file paths remain interpretable

Hardlink-aware results SHALL retain every included path in the result tree and
MUST distinguish the allocation measured for a path from the bytes that path
contributes to the scan total. A path whose contribution is counted through
another included path SHALL be identified as sharing storage.

#### Scenario: User browses an additional hardlink

- **GIVEN** an included hardlink contributes no additional bytes because the
  same file identity was already counted
- **WHEN** the user finds that path in the tree or by search
- **THEN** the path remains selectable and browsable
- **AND** its item details show its measured allocated bytes
- **AND** its item details identify that its contribution is counted elsewhere

#### Scenario: Derived storage views contain hardlinks

- **GIVEN** a completed hardlink-aware result contains repeated file identities
- **WHEN** the application builds the treemap, file-type totals, or
  largest-file ranking
- **THEN** their byte weights and totals use counted contributions
- **AND** repeated paths do not create additional apparent storage consumption

### Requirement: Hardlink-aware results remain honest after Trash

After a successful Trash operation changes a hardlink-aware result, the
application MUST refresh accounting from the filesystem before presenting an
updated result as complete. A failed or cancelled Trash operation MUST leave
the existing result unchanged.

#### Scenario: Counted hardlink is moved to Trash

- **GIVEN** two included paths share one counted allocation
- **AND** the path currently representing that allocation is selected
- **WHEN** the user confirms and successfully moves that path to Trash
- **THEN** the application refreshes the remaining scan scope
- **AND** a remaining included link contributes the allocation
- **AND** the refreshed result is not reduced as though the underlying storage
  had disappeared

#### Scenario: Trash operation fails

- **GIVEN** a hardlink-aware result is displayed
- **WHEN** moving a selected item to Trash fails
- **THEN** the existing result and its accounting remain displayed unchanged
- **AND** the application reports the failure

## MODIFIED Requirements

### Requirement: Storage terms have canonical meanings

MacStorageAtlas SHALL use the following meanings consistently in product,
developer, and user-facing documentation:

- logical size is the file length visible to an application;
- allocated file size is the local filesystem allocation attributed to one
  visited file path;
- hardlink-aware allocated size counts allocated storage once for each
  filesystem file identity in a stated scan scope but does not account for
  shared physical extents between distinct identities;
- unique allocated size is the allocation attributed once across all file
  identities and shared physical extents in a stated scope;
- volume used space is derived from volume-capacity metadata and is not a sum of
  a MacStorageAtlas scan;
- volume free space is capacity currently reported as unallocated;
- volume available space is capacity currently available for allocation and can
  differ from free space because of reservations or reclaimable storage; and
- volume purgeable space is used capacity that macOS reports as reclaimable
  without deleting user-designated files.

#### Scenario: Reader compares file and volume numbers

- **GIVEN** documentation presents file sizes and volume-capacity terms
- **WHEN** a reader consults their definitions
- **THEN** file-tree totals are distinguished from volume used space
- **AND** free, available, and purgeable space are not presented as synonyms

#### Scenario: Reader encounters unique allocated size

- **GIVEN** a scan reports hardlink-aware allocated size
- **WHEN** the product describes unique allocated size
- **THEN** it identifies shared-extent deduplication as additionally necessary
  for that term
- **AND** it does not describe hardlink-only accounting as unique allocated
  size

#### Scenario: Reader evaluates deletion impact

- **GIVEN** documentation presents a hardlink-aware scan total
- **WHEN** a reader considers moving one path to Trash
- **THEN** the total is described as scan-scoped allocated accounting
- **AND** it is not promised as the bytes that deleting one path will reclaim

### Requirement: Every scan identifies its measurement basis

Every scan progress and result model SHALL identify whether its byte values use
logical, per-path allocated, or hardlink-aware allocated measurement, and the
application MUST keep the displayed mode associated with the scan that produced
the values. The application default SHALL be hardlink-aware allocated
measurement while all three modes remain selectable.

#### Scenario: User completes the default scan

- **GIVEN** the user has not selected another measurement mode
- **WHEN** a scan starts and completes
- **THEN** its allocated bytes use hardlink-aware accounting
- **AND** progress and results identify the mode as hardlink-aware allocated
  size

#### Scenario: User selects per-path allocated measurement

- **GIVEN** the user selects per-path allocated measurement before starting a
  scan
- **WHEN** scan progress or results display byte values
- **THEN** those values are identified as allocated size per path
- **AND** repeated file identities are not deduplicated

#### Scenario: User selects logical measurement

- **GIVEN** the user selects logical measurement before starting a scan
- **WHEN** scan progress or results display byte values
- **THEN** those values are identified as logical size

#### Scenario: Preference changes after a result was produced

- **GIVEN** a completed result was measured using one mode
- **WHEN** the preference for the next scan changes
- **THEN** the existing result retains its original measurement-mode label

#### Scenario: Existing allocated preference is migrated

- **GIVEN** saved settings from an earlier version select allocated measurement
- **WHEN** the application loads those settings after this change
- **THEN** the preference becomes hardlink-aware allocated measurement
- **AND** unrelated saved scan options and recent locations are preserved

### Requirement: Allocated measurement reports local per-path allocation

On supported macOS targets, per-path allocated and hardlink-aware allocated
modes SHALL retain the local filesystem allocation attributed to each
successfully measured file path. They MUST NOT silently substitute logical
length when allocated metadata cannot be obtained. Per-path allocated mode
SHALL count every included path, while hardlink-aware contribution is governed
by filesystem identity.

#### Scenario: Allocated scan contains a sparse file

- **GIVEN** a sparse file has fewer allocated bytes than its logical length
- **WHEN** the file is scanned in either allocated mode
- **THEN** its measured size is the locally allocated byte count

#### Scenario: Allocated metadata is unavailable

- **GIVEN** allocated metadata cannot be read for a file
- **WHEN** the file is scanned in either allocated mode
- **THEN** the scanner records a recoverable scan error for that path
- **AND** it excludes an invented or logical fallback value from the totals

#### Scenario: Per-path allocated scan contains hardlinks

- **GIVEN** two included paths are hardlinks to the same file
- **WHEN** they are scanned in per-path allocated mode
- **THEN** each path contributes its attributed allocated size
- **AND** the result identifies its aggregate as per-path rather than
  hardlink-aware

#### Scenario: Hardlink-aware scan contains APFS clones

- **GIVEN** two included files have different filesystem identities but share
  APFS physical extents
- **WHEN** they are scanned in hardlink-aware allocated mode
- **THEN** each identity contributes its attributed allocated bytes
- **AND** the result discloses that APFS clone extents are not deduplicated

### Requirement: Scan-scope options determine which bytes are aggregated

The scanner SHALL aggregate only entries included by the active hidden-file,
symbolic-link, and package-expansion options. Collapsing an application package
in the result tree MUST NOT change its measured or counted aggregate size.
Hardlink-aware accounting SHALL apply consistently across all included paths,
including descendants hidden by collapsed package presentation.

#### Scenario: Symbolic links are not followed

- **GIVEN** a scan scope contains a symbolic link
- **AND** following symbolic links is disabled
- **WHEN** the scan calculates its totals
- **THEN** the link and its target contribute no bytes through that link path

#### Scenario: Followed file link repeats an included identity

- **GIVEN** following symbolic links is enabled
- **AND** a symbolic-link path resolves to a file identity already included by
  another path
- **WHEN** the scan runs in hardlink-aware allocated mode
- **THEN** both paths remain included
- **AND** the target allocation contributes only once

#### Scenario: Application package is collapsed

- **GIVEN** a scan scope contains an application package
- **AND** package expansion is disabled
- **WHEN** the scan completes
- **THEN** the package is shown as one result item
- **AND** its size still aggregates its included descendants using the scan's
  measurement and accounting mode
- **AND** hardlinks spanning the package boundary contribute only once in
  hardlink-aware allocated mode

### Requirement: Incomplete scans preserve honest totals

Errors and cancellation MUST NOT cause a scan to label unmeasured entries as
zero-byte successes or to present a partial total as complete. Any published
partial tree and progress total SHALL remain internally consistent with its
measurement and accounting mode.

#### Scenario: One entry cannot be measured

- **GIVEN** one entry fails with a recoverable metadata or access error
- **WHEN** scanning continues
- **THEN** the failed path is reported in the scan errors
- **AND** its unknown size is excluded from file, directory, and progress totals
- **AND** successfully measured entries remain available

#### Scenario: Repeated identity cannot be measured

- **GIVEN** one hardlink path was measured successfully
- **AND** metadata for another path to that file cannot be read
- **WHEN** the hardlink-aware scan continues
- **THEN** the failing path is reported as a scan error
- **AND** the successfully measured contribution remains in the partial result

#### Scenario: User cancels a running scan

- **GIVEN** a hardlink-aware scan has reported progress for some entries
- **WHEN** the user cancels the scan
- **THEN** the scan does not report completion
- **AND** any retained partial result sums each successfully identified file
  identity at most once
- **AND** its measurement and accounting mode remains identifiable

### Requirement: Measurement remains metadata-only

Logical, per-path allocated, and hardlink-aware allocated measurement SHALL use
local filesystem metadata and MUST NOT read file contents, contact a storage
provider, or materialize an undownloaded cloud placeholder solely to calculate
a size or determine repeated file identity.

#### Scenario: Allocated scan encounters a cloud placeholder

- **GIVEN** a cloud-managed file has logical content that is not present locally
- **WHEN** the file is scanned in either allocated mode
- **THEN** its measured size is based on currently allocated local storage
- **AND** the scan does not request or trigger download of its remote content

#### Scenario: Hardlink-aware scan determines identity

- **GIVEN** a file is scanned in hardlink-aware allocated mode
- **WHEN** the scanner determines whether its storage was already counted
- **THEN** it uses local filesystem metadata
- **AND** it does not compare, hash, or read file contents

#### Scenario: Logical scan encounters a cloud placeholder

- **GIVEN** a cloud-managed file exposes a logical length through local metadata
- **WHEN** the file is scanned in logical mode
- **THEN** the logical length can be reported without reading or downloading the
  content

### Requirement: Measurement claims are reproducibly documented

Developer documentation SHALL include representative normal-file, sparse-file,
and hardlink examples that explain how to compare MacStorageAtlas logical,
per-path allocated, and hardlink-aware allocated results with macOS metadata
tools. It MUST treat Finder and other aggregate tools as comparison points
rather than authoritative proof of unique physical usage.

#### Scenario: Developer validates a representative fixture

- **GIVEN** a developer creates a documented normal-file, sparse-file, or
  hardlink fixture
- **WHEN** they follow the documented comparison procedure
- **THEN** they can reproduce the expected logical, per-path allocated, and
  hardlink-aware observations
- **AND** differences caused by rounding, scope, hardlinks, clones, or
  volume-level semantics are called out
