## Purpose

Define the size basis and scope of every storage number reported by
MacStorageAtlas so users can interpret scan totals without mistaking
per-path allocated bytes for unique physical storage or volume capacity.

## ADDED Requirements

### Requirement: Storage terms have canonical meanings

MacStorageAtlas SHALL use the following meanings consistently in product,
developer, and user-facing documentation:

- logical size is the file length visible to an application;
- allocated file size is the local filesystem allocation attributed to one
  visited file path;
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

- **GIVEN** a scan reports allocated file size per visited path
- **WHEN** the product describes unique allocated size
- **THEN** it identifies hardlink and shared-extent deduplication as necessary
  for that term
- **AND** it does not describe the current per-path total as unique allocated
  size

### Requirement: Every scan identifies its measurement basis

Every scan progress and result model SHALL identify whether its byte values use
logical or allocated measurement, and the application MUST keep the displayed
basis associated with the scan that produced the values.

#### Scenario: User completes the default scan

- **GIVEN** the application default is allocated measurement
- **WHEN** a user completes a scan without changing the measurement option
- **THEN** the displayed scan values are identified as allocated size

#### Scenario: User selects logical measurement

- **GIVEN** a user selects logical measurement before starting a scan
- **WHEN** scan progress or results display byte values
- **THEN** those values are identified as logical size

#### Scenario: Preference changes after a result was produced

- **GIVEN** a completed result was measured using one basis
- **WHEN** the preference for the next scan changes
- **THEN** the existing result retains its original measurement-basis label

### Requirement: Logical measurement reports included file lengths

In logical mode, the scanner SHALL report each successfully measured file's
logical length and SHALL aggregate directory and progress totals from those
same included values.

#### Scenario: Logical scan contains nested files

- **GIVEN** a scan scope contains successfully measured files in nested
  directories
- **WHEN** the scan runs in logical mode
- **THEN** each file reports its logical length
- **AND** every directory total equals the sum of its included descendants
- **AND** the progress byte total uses the same logical values

#### Scenario: Logical scan contains a sparse file

- **GIVEN** a sparse file has a logical length greater than its local allocation
- **WHEN** the file is scanned in logical mode
- **THEN** the reported file size is its logical length

### Requirement: Allocated measurement reports local per-path allocation

On supported macOS targets, allocated mode SHALL report the local filesystem
allocation attributed to each successfully measured file path. It MUST NOT
silently substitute logical length when allocated metadata cannot be obtained.

#### Scenario: Allocated scan contains a sparse file

- **GIVEN** a sparse file has fewer allocated bytes than its logical length
- **WHEN** the file is scanned in allocated mode
- **THEN** its reported size is the locally allocated byte count

#### Scenario: Allocated metadata is unavailable

- **GIVEN** allocated metadata cannot be read for a file
- **WHEN** the file is scanned in allocated mode
- **THEN** the scanner records a recoverable scan error for that path
- **AND** it excludes an invented or logical fallback value from the totals

#### Scenario: Two paths share physical storage

- **GIVEN** two included paths are hardlinks or files with shared physical
  extents
- **WHEN** they are scanned in allocated mode before unique accounting is
  implemented
- **THEN** each path retains its attributed allocated size
- **AND** the result discloses that the aggregate is not unique physical
  storage

### Requirement: Scan-scope options determine which bytes are aggregated

The scanner SHALL aggregate only entries included by the active hidden-file,
symbolic-link, and package-expansion options. Collapsing an application package
in the result tree MUST NOT change the package's measured aggregate size.

#### Scenario: Symbolic links are not followed

- **GIVEN** a scan scope contains a symbolic link
- **AND** following symbolic links is disabled
- **WHEN** the scan calculates its totals
- **THEN** the link and its target contribute no bytes through that link path

#### Scenario: Application package is collapsed

- **GIVEN** a scan scope contains an application package
- **AND** package expansion is disabled
- **WHEN** the scan completes
- **THEN** the package is shown as one result item
- **AND** its size still aggregates its included descendants using the scan's
  measurement basis

### Requirement: Incomplete scans preserve honest totals

Errors and cancellation MUST NOT cause a scan to label unmeasured entries as
zero-byte successes or to present a partial total as complete. Any published
partial tree and progress total SHALL remain internally consistent.

#### Scenario: One entry cannot be measured

- **GIVEN** one entry fails with a recoverable metadata or access error
- **WHEN** scanning continues
- **THEN** the failed path is reported in the scan errors
- **AND** its unknown size is excluded from file, directory, and progress totals
- **AND** successfully measured entries remain available

#### Scenario: User cancels a running scan

- **GIVEN** a scan has reported progress for some entries
- **WHEN** the user cancels the scan
- **THEN** the scan does not report completion
- **AND** any retained partial result sums only the entries successfully
  measured before cancellation
- **AND** its measurement basis remains identifiable

### Requirement: Measurement remains metadata-only

Size measurement SHALL use local filesystem metadata and MUST NOT read file
contents, contact a storage provider, or materialize an undownloaded cloud
placeholder solely to calculate a size.

#### Scenario: Allocated scan encounters a cloud placeholder

- **GIVEN** a cloud-managed file has logical content that is not present locally
- **WHEN** the file is scanned in allocated mode
- **THEN** its size is based on currently allocated local storage
- **AND** the scan does not request or trigger download of its remote content

#### Scenario: Logical scan encounters a cloud placeholder

- **GIVEN** a cloud-managed file exposes a logical length through local metadata
- **WHEN** the file is scanned in logical mode
- **THEN** the logical length can be reported without reading or downloading the
  content

### Requirement: Measurement claims are reproducibly documented

Developer documentation SHALL include representative normal-file and
sparse-file examples that explain how to compare MacStorageAtlas logical and
allocated results with macOS metadata tools. It MUST treat Finder and other
aggregate tools as comparison points rather than authoritative proof of unique
physical usage.

#### Scenario: Developer validates a representative fixture

- **GIVEN** a developer creates a documented normal-file or sparse-file fixture
- **WHEN** they follow the documented comparison procedure
- **THEN** they can reproduce the expected logical and allocated values
- **AND** differences caused by rounding, scope, hardlinks, clones, or
  volume-level semantics are called out
