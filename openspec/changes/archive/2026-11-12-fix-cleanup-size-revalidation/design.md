# Design

## Context

See `proposal.md`. `CleanupItemSnapshot` captures `SizeBytes` (counted) and
`MeasuredSizeBytes` (per-path measured) from the scanned `DiskItem`.
`CleanupFileSystemMetadataReader` reports both as the current logical length,
and `CleanupPreflightValidator` compares the counted size with that length.

## Decisions

### Read the current allocated size in the cleanup metadata reader

`CleanupFileSystemMetadataReader` accepts an optional
`IAllocatedFileMetadataReader`. For files it reports the logical length as
`SizeBytes` and, when the allocated reader succeeds, the allocated size as
`MeasuredSizeBytes`; otherwise `MeasuredSizeBytes` stays the logical length.
The App composition passes `MacFileMetadataReader`, the same reader the scan
uses, so both sides measure allocation the same way.

### Compare in the scan's measurement basis

`CleanupPreflightValidator` receives the result's measurement mode. Logical
mode keeps comparing `SizeBytes` with the logical length. Allocated and
shared-aware allocated modes compare the snapshot's `MeasuredSizeBytes` with
the current `MeasuredSizeBytes`. Comparing the counted size would wrongly
block repeated hardlinks, whose counted contribution is zero.

When the allocated read fails, the current value falls back to the logical
length, which does not match a positive allocated scan size, so the item is
blocked rather than trashed without verification.

Alternatives considered:

- Compare logical lengths in every mode. Rejected: the scan does not retain a
  logical length for files measured in allocated modes.
- Drop the size check. Rejected: revalidation exists to block items changed
  since the scan.

## Risks / Trade-offs

- [Allocation changes without content change] → Item is blocked; a rescan
  makes it ready again.
