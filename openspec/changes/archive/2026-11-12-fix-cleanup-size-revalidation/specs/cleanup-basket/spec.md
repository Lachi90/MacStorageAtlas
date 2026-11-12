# Spec Delta

## ADDED Requirements

### Requirement: Basket size revalidation uses the scan's measurement basis

When revalidating a basket file before Trash, Move to, or Copy to, Atlas
Storage Analyzer SHALL compare the file's current size with the size the
completed scan measured for it, using the same measurement basis as that scan.
If the current size in that basis cannot be determined, the item MUST be
blocked.

#### Scenario: Unchanged file in shared-aware allocated mode

- **GIVEN** a completed scan in shared-aware allocated mode measured a 10-byte file as 4,096 allocated bytes
- **AND** the file is unchanged
- **WHEN** Atlas Storage Analyzer revalidates the basket
- **THEN** the file is ready for Trash execution

#### Scenario: Allocation changed in allocated mode

- **GIVEN** a completed scan in allocated mode measured a file as 4,096 allocated bytes
- **AND** the file now allocates 8,192 bytes
- **WHEN** Atlas Storage Analyzer revalidates the basket
- **THEN** that file is blocked from execution
- **AND** the review reports that the item changed since the scan

#### Scenario: Unchanged file in logical mode

- **GIVEN** a completed scan in logical mode measured a file as 10 bytes
- **AND** the file is unchanged
- **WHEN** Atlas Storage Analyzer revalidates the basket
- **THEN** the file is ready for Trash execution

#### Scenario: Allocated size cannot be read

- **GIVEN** a completed scan in allocated mode
- **AND** the current allocated size of a basket file cannot be read
- **WHEN** Atlas Storage Analyzer revalidates the basket
- **THEN** that file is blocked from execution
