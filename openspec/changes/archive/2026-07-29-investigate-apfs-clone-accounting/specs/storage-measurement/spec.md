## ADDED Requirements

### Requirement: Verified full-clone data is counted once

In shared-aware allocated mode, MacStorageAtlas SHALL count a data allocation
once across distinct included filesystem identities only when supported local
filesystem metadata verifies that the identities are full clones of the same
data stream. Each identity's allocation outside that verified shared data
stream MUST continue to contribute independently.

#### Scenario: Included files are verified full clones

- **GIVEN** two included files have distinct filesystem identities
- **AND** supported metadata verifies that all of their equally sized data
  allocations share one clone identity
- **WHEN** the files are scanned in shared-aware allocated mode
- **THEN** their file count includes both paths
- **AND** exactly one data allocation contributes to the scan total
- **AND** non-data allocation attributed to each distinct filesystem identity
  continues to contribute

#### Scenario: A full clone exists outside the scan scope

- **GIVEN** an included file has a verified full clone outside the active scan
  scope
- **WHEN** the included file is scanned in shared-aware allocated mode
- **THEN** its data allocation contributes once
- **AND** the external clone does not appear in the result or suppress the
  included contribution

#### Scenario: Clone has diverged after creation

- **GIVEN** two included files have distinct identities and still share some
  APFS extents
- **AND** supported metadata does not verify them as full clones of one data
  stream
- **WHEN** the files are scanned in shared-aware allocated mode
- **THEN** each identity contributes its full attributed allocation
- **AND** the result does not infer a shared contribution from equal or similar
  content

#### Scenario: Clone metadata is inconsistent

- **GIVEN** files report a shared clone identity with inconsistent data
  allocation metadata
- **WHEN** the scanner calculates their contributions
- **THEN** it does not suppress the inconsistent allocation
- **AND** it identifies clone-accounting coverage as partial

### Requirement: Results disclose clone-accounting coverage

Every shared-aware progress update and completed result SHALL retain the clone
accounting coverage observed for that scan. The application MUST distinguish
full-clone accounting that was available for all relevant observed entries,
accounting that was unavailable, and accounting that was partial because
capability or metadata coverage was mixed or degraded.

#### Scenario: Capable scan completes

- **GIVEN** every relevant observed allocated entry exposes supported
  full-clone metadata
- **WHEN** a shared-aware scan completes
- **THEN** the result identifies verified full-clone accounting as available
- **AND** it still discloses that divergent clone extents are not deduplicated

#### Scenario: Unsupported scan completes

- **GIVEN** no observed volume exposes supported full-clone mapping
- **WHEN** a shared-aware scan completes
- **THEN** hardlink accounting remains active
- **AND** the result identifies full-clone accounting as unavailable
- **AND** distinct clone identities retain their full contributions

#### Scenario: Scan crosses mixed volumes

- **GIVEN** a shared-aware scan includes entries from volumes with different
  clone-accounting capabilities
- **WHEN** progress or completion is reported
- **THEN** verified sharing is applied only where supported
- **AND** the reported coverage is partial

#### Scenario: Optional clone metadata cannot be read

- **GIVEN** required allocated metadata and filesystem identity are available
- **AND** optional clone metadata is unavailable for an entry
- **WHEN** the entry is scanned in shared-aware allocated mode
- **THEN** the entry contributes according to filesystem-identity accounting
  without clone deduplication
- **AND** scanning continues without substituting an optimistic shared
  contribution
- **AND** the result identifies clone-accounting coverage as partial

## MODIFIED Requirements

### Requirement: Storage terms have canonical meanings

MacStorageAtlas SHALL use the following meanings consistently in product,
developer, and user-facing documentation:

- logical size is the file length visible to an application;
- allocated file size is the local filesystem allocation attributed to one
  visited file path;
- shared-aware allocated size counts allocated storage once for each
  filesystem file identity in a stated scan scope and additionally counts
  verified fully shared data allocation once where supported, while non-data
  allocation and divergent clone extents remain counted per identity;
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

- **GIVEN** a scan reports shared-aware allocated size
- **WHEN** the product describes unique allocated size
- **THEN** it identifies complete shared-extent deduplication as additionally
  necessary for that term
- **AND** it does not describe hardlink and full-clone-only accounting as
  unique allocated size

#### Scenario: Reader evaluates deletion impact

- **GIVEN** documentation presents a shared-aware scan total
- **WHEN** a reader considers moving one path to Trash
- **THEN** the total is described as scan-scoped allocated accounting
- **AND** it is not promised as the bytes that deleting one path will reclaim

### Requirement: Every scan identifies its measurement basis

Every scan progress and result model SHALL identify whether its byte values use
logical, per-path allocated, or shared-aware allocated measurement, and the
application MUST keep the displayed mode and accounting coverage associated
with the scan that produced the values. The application default SHALL be
shared-aware allocated measurement while all three modes remain selectable.

#### Scenario: User completes the default scan

- **GIVEN** the user has not selected another measurement mode
- **WHEN** a scan starts and completes
- **THEN** its allocated bytes use shared-aware accounting
- **AND** progress and results identify the mode as shared-aware allocated size
- **AND** the completed result identifies its clone-accounting coverage

#### Scenario: User selects per-path allocated measurement

- **GIVEN** the user selects per-path allocated measurement before starting a
  scan
- **WHEN** scan progress or results display byte values
- **THEN** those values are identified as allocated size per path
- **AND** repeated file or clone identities are not deduplicated

#### Scenario: User selects logical measurement

- **GIVEN** the user selects logical measurement before starting a scan
- **WHEN** scan progress or results display byte values
- **THEN** those values are identified as logical size

#### Scenario: Preference changes after a result was produced

- **GIVEN** a completed result was measured using one mode and accounting
  coverage
- **WHEN** the preference for the next scan changes
- **THEN** the existing result retains its original measurement-mode label and
  coverage

#### Scenario: Existing hardlink-aware preference is migrated

- **GIVEN** saved settings select the previous hardlink-aware allocated mode
- **WHEN** the application loads those settings after this change
- **THEN** the preference becomes shared-aware allocated measurement
- **AND** unrelated saved scan options and recent locations are preserved

#### Scenario: Existing allocated preference is migrated

- **GIVEN** saved settings from an earlier version select allocated measurement
- **WHEN** the application loads those settings after this change
- **THEN** the preference becomes shared-aware allocated measurement
- **AND** unrelated saved scan options and recent locations are preserved

### Requirement: Allocated measurement reports local per-path allocation

On supported macOS targets, per-path allocated and shared-aware allocated modes
SHALL retain the total local filesystem allocation attributed to each
successfully measured file path. They MUST NOT silently substitute logical
length when required allocated metadata cannot be obtained. Per-path allocated
mode SHALL count every included path, while shared-aware contribution is
governed by filesystem identity and verified full-clone data metadata.

#### Scenario: Allocated scan contains a sparse file

- **GIVEN** a sparse file has fewer allocated bytes than its logical length
- **WHEN** the file is scanned in either allocated mode
- **THEN** its measured size is the locally allocated byte count

#### Scenario: Required allocated metadata is unavailable

- **GIVEN** total allocated metadata or filesystem identity cannot be read for a
  file
- **WHEN** the file is scanned in either allocated mode
- **THEN** the scanner records a recoverable scan error for that path
- **AND** it excludes an invented or logical fallback value from the totals

#### Scenario: Per-path allocated scan contains hardlinks

- **GIVEN** two included paths are hardlinks to the same file
- **WHEN** they are scanned in per-path allocated mode
- **THEN** each path retains and contributes its attributed allocated size
- **AND** the result identifies its aggregate as per-path rather than
  shared-aware

#### Scenario: Shared-aware scan contains a full clone with non-data allocation

- **GIVEN** two included identities have verified fully shared data allocation
- **AND** each identity has additional allocated bytes outside that data
  allocation
- **WHEN** the files are scanned in shared-aware allocated mode
- **THEN** each path retains its total measured allocation
- **AND** the shared data allocation contributes once
- **AND** each identity's additional allocation contributes independently

#### Scenario: Shared-aware scan contains partial APFS clones

- **GIVEN** two included files have different filesystem identities and share
  only some APFS physical extents
- **WHEN** they are scanned in shared-aware allocated mode
- **THEN** each identity contributes its attributed allocated bytes
- **AND** the result discloses that partial clone extents are not deduplicated

### Requirement: Hardlink-aware allocated measurement counts file identities once

In shared-aware allocated mode, the scanner SHALL count the total allocated
bytes of each successfully measured filesystem file identity at most once
within the active scan scope before applying verified full-clone data
accounting. File and directory byte totals, progress totals, and completed
result totals MUST use those counted contributions.

#### Scenario: Two included paths are hardlinks

- **GIVEN** two included paths are hardlinks to the same file
- **WHEN** they are scanned in shared-aware allocated mode
- **THEN** the scan file count includes both paths
- **AND** exactly one total allocation contributes to the scan byte total
- **AND** directory and completed result totals remain additive

#### Scenario: Equal identity numbers occur on different volumes

- **GIVEN** two included paths refer to different files on different volumes
- **WHEN** they are scanned in shared-aware allocated mode
- **THEN** each file contributes its allocated bytes unless a separate verified
  sharing relationship applies within its volume
- **AND** equal inode or clone numbers across volumes do not merge the files

#### Scenario: Another hardlink is outside the scan scope

- **GIVEN** an included file has another hardlink outside the active scan scope
- **WHEN** the included path is scanned in shared-aware allocated mode
- **THEN** the included path contributes its allocated bytes once
- **AND** the external path does not appear in the result or suppress the
  included contribution

### Requirement: Repeated file paths remain interpretable

Shared-aware results SHALL retain every included path in the result tree and
MUST distinguish the allocation measured for a path, the bytes that path
contributes to the scan total, and the bytes represented through another
included path. A path with any shared contribution SHALL be identified as
sharing storage.

#### Scenario: User browses an additional hardlink

- **GIVEN** an included hardlink contributes no additional bytes because the
  same file identity was already counted
- **WHEN** the user finds that path in the tree or by search
- **THEN** the path remains selectable and browsable
- **AND** its item details show its measured allocated bytes
- **AND** its item details identify that all of its allocation is counted
  elsewhere

#### Scenario: User browses an additional full clone

- **GIVEN** an included full clone has shared data bytes and independently
  counted non-data bytes
- **WHEN** the user selects that path
- **THEN** its item details show its total measured allocation
- **AND** its item details distinguish counted contribution from shared bytes
- **AND** it is not presented as an ordinary zero-byte file

#### Scenario: Derived storage views contain shared paths

- **GIVEN** a completed shared-aware result contains repeated identities or
  verified full clones
- **WHEN** the application builds the treemap, file-type totals, or
  largest-file ranking
- **THEN** their byte weights and totals use counted contributions
- **AND** shared bytes do not create additional apparent storage consumption

### Requirement: Scan-scope options determine which bytes are aggregated

The scanner SHALL aggregate only entries included by the active hidden-file,
symbolic-link, and package-expansion options. Collapsing an application package
in the result tree MUST NOT change its measured, counted, or shared aggregate
size. Shared-aware accounting SHALL apply consistently across all included
paths, including descendants hidden by collapsed package presentation.

#### Scenario: Symbolic links are not followed

- **GIVEN** a scan scope contains a symbolic link
- **AND** following symbolic links is disabled
- **WHEN** the scan calculates its totals
- **THEN** the link and its target contribute no bytes through that link path

#### Scenario: Followed file link repeats an included identity

- **GIVEN** following symbolic links is enabled
- **AND** a symbolic-link path resolves to a file identity already included by
  another path
- **WHEN** the scan runs in shared-aware allocated mode
- **THEN** both paths remain included
- **AND** the target allocation contributes only once

#### Scenario: Application package is collapsed

- **GIVEN** a scan scope contains an application package
- **AND** package expansion is disabled
- **WHEN** the scan completes
- **THEN** the package is shown as one result item
- **AND** its size still aggregates its included descendants using the scan's
  measurement and accounting mode
- **AND** hardlinks and verified full-clone data spanning the package boundary
  contribute only once in shared-aware allocated mode

### Requirement: Hardlink-aware results remain honest after Trash

After a successful Trash operation changes a shared-aware result, the
application MUST refresh accounting from the filesystem before presenting an
updated result as complete. A failed or cancelled Trash operation MUST leave
the existing result unchanged.

#### Scenario: Counted shared path is moved to Trash

- **GIVEN** included paths share a counted hardlink or verified full-clone data
  allocation
- **AND** the path currently representing that allocation is selected
- **WHEN** the user confirms and successfully moves that path to Trash
- **THEN** the application refreshes the remaining scan scope
- **AND** a remaining included path contributes the allocation
- **AND** the refreshed result is not reduced as though shared storage had
  disappeared

#### Scenario: Trash operation fails

- **GIVEN** a shared-aware result is displayed
- **WHEN** moving a selected item to Trash fails
- **THEN** the existing result and its accounting remain displayed unchanged
- **AND** the application reports the failure

### Requirement: Incomplete scans preserve honest totals

Errors and cancellation MUST NOT cause a scan to label unmeasured entries as
zero-byte successes or to present a partial total as complete. Any published
partial tree, progress total, and accounting coverage SHALL remain internally
consistent with its measurement and accounting mode.

#### Scenario: One entry cannot be measured

- **GIVEN** one entry fails with a recoverable required-metadata or access error
- **WHEN** scanning continues
- **THEN** the failed path is reported in the scan errors
- **AND** its unknown size is excluded from file, directory, and progress totals
- **AND** successfully measured entries remain available

#### Scenario: Optional clone metadata is unavailable

- **GIVEN** one entry has valid required allocation and identity metadata
- **AND** its optional clone metadata cannot establish verified sharing
- **WHEN** the shared-aware scan continues
- **THEN** the path is counted without clone deduplication
- **AND** accounting coverage records the limitation
- **AND** the path is not presented as an unmeasured zero-byte success

#### Scenario: User cancels a running scan

- **GIVEN** a shared-aware scan has reported progress for some entries
- **WHEN** the user cancels the scan
- **THEN** the scan does not report completion
- **AND** any retained partial result counts each successfully identified
  filesystem identity at most once
- **AND** any verified full-clone data already observed is counted at most once
- **AND** its measurement mode and coverage remain identifiable

### Requirement: Measurement remains metadata-only

Logical, per-path allocated, and shared-aware allocated measurement SHALL use
local filesystem metadata and MUST NOT read file contents, enumerate physical
extent maps, contact a storage provider, or materialize an undownloaded cloud
placeholder solely to calculate a size or determine shared storage.

#### Scenario: Allocated scan encounters a cloud placeholder

- **GIVEN** a cloud-managed file has logical content that is not present locally
- **WHEN** the file is scanned in either allocated mode
- **THEN** its measured size is based on currently allocated local storage
- **AND** the scan does not request or trigger download of its remote content

#### Scenario: Shared-aware scan determines identity

- **GIVEN** a file is scanned in shared-aware allocated mode
- **WHEN** the scanner determines whether its storage was already counted
- **THEN** it uses local filesystem identity, capability, allocation, and clone
  metadata
- **AND** it does not compare, hash, open, or read file contents
- **AND** it does not use physical block addresses

#### Scenario: Logical scan encounters a cloud placeholder

- **GIVEN** a cloud-managed file exposes a logical length through local metadata
- **WHEN** the file is scanned in logical mode
- **THEN** the logical length can be reported without reading or downloading the
  content

### Requirement: Measurement claims are reproducibly documented

Developer documentation SHALL include representative normal-file, sparse-file,
hardlink, ordinary-copy, full-clone, and divergent-clone examples that explain
how to compare MacStorageAtlas logical, per-path allocated, and shared-aware
allocated results with macOS metadata tools. It MUST treat Finder and other
aggregate tools as comparison points rather than authoritative proof of unique
physical usage.

#### Scenario: Developer validates a representative fixture

- **GIVEN** a developer creates a documented normal-file, sparse-file,
  hardlink, ordinary-copy, full-clone, or divergent-clone fixture
- **WHEN** they follow the documented comparison procedure on a supported
  volume
- **THEN** they can reproduce the expected measured, counted, shared, and
  coverage observations
- **AND** differences caused by rounding, scope, capabilities, hardlinks,
  partial clones, non-data allocation, or volume semantics are called out

#### Scenario: Clone fixture is unsupported

- **GIVEN** the current operating system or temporary volume does not expose
  supported full-clone mapping
- **WHEN** the integration fixture runs
- **THEN** it is ignored with a clear platform or capability reason
- **AND** portable fallback behavior remains covered by deterministic injected
  tests
