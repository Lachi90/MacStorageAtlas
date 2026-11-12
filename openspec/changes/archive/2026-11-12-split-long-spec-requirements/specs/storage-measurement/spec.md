# Spec Delta

## MODIFIED Requirements

### Requirement: Storage terms have canonical meanings

Atlas Storage Analyzer SHALL use the following meanings consistently in product,
developer, and user-facing documentation:

- logical size is the file length visible to an application;
- allocated file size is the local filesystem allocation attributed to one
  visited file path; and
- unique allocated size is the allocation attributed once across all file
  identities and shared physical extents in a stated scope.

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

## ADDED Requirements

### Requirement: Shared-aware allocated size has a canonical meaning

Atlas Storage Analyzer SHALL use shared-aware allocated size consistently in
product, developer, and user-facing documentation to mean allocated storage
counted once for each filesystem file identity in a stated scan scope,
additionally counting verified fully shared data allocation once where
supported, while non-data allocation and divergent clone extents remain counted
per identity.

#### Scenario: Reader encounters shared-aware allocated size

- **GIVEN** documentation presents shared-aware allocated size
- **WHEN** a reader consults its definition
- **THEN** each filesystem file identity in the stated scan scope is described
  as counted once
- **AND** only verified fully shared data allocation is described as
  additionally counted once
- **AND** allocation outside the verified data stream is described as counted
  for each identity

### Requirement: Volume used space has a canonical meaning

Atlas Storage Analyzer SHALL use volume used space consistently in product,
developer, and user-facing documentation to mean space derived from
volume-capacity metadata, which is not a sum of an Atlas Storage Analyzer scan.

#### Scenario: A scan total is not presented as volume used space

- **GIVEN** a completed folder or volume scan reports its total
- **WHEN** the product or its documentation presents that total
- **THEN** the total is not labeled as volume used space

### Requirement: Volume free, available, and purgeable space have canonical meanings

Atlas Storage Analyzer SHALL use the following volume-capacity meanings
consistently in product, developer, and user-facing documentation:

- volume free space is capacity currently reported as unallocated;
- volume available space is capacity currently available for allocation and can
  differ from free space because of reservations or reclaimable storage; and
- volume purgeable space is used capacity that macOS reports as reclaimable
  without deleting user-designated files.

#### Scenario: Reader distinguishes available space from free space

- **GIVEN** documentation presents volume free space and volume available space
- **WHEN** a reader consults their definitions
- **THEN** available space is described as capacity currently available for
  allocation
- **AND** it is described as able to differ from free space because of
  reservations or reclaimable storage

#### Scenario: Reader encounters purgeable space

- **GIVEN** documentation presents volume purgeable space
- **WHEN** a reader consults its definition
- **THEN** purgeable space is described as used capacity that macOS reports as
  reclaimable
- **AND** it is not described as free space
