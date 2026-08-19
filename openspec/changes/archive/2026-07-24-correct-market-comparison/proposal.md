## Why

The current README comparison claims that DaisyDisk does not measure real
on-disk size, while DaisyDisk's official documentation explicitly describes
physical-space accounting for clones and hardlinks. This weakens trust in
MacStorageAtlas and obscures the narrower semantics of its current
allocated-block measurement.

This change implements the documentation-only WP-00 from
`docs/IMPLEMENTATION_ROADMAP.md`.

## What Changes

- Correct unsupported or outdated claims in the README comparison.
- Distinguish allocated file blocks from hardlink/clone-aware unique physical
  storage.
- Add a visible verification date and links to official competitor sources.
- Describe current MacStorageAtlas limitations neutrally and keep roadmap
  features out of the implemented-feature comparison.

## Non-goals

- Implement hardlink or APFS clone accounting; that belongs to WP-02.
- Redesign the product website or README beyond the comparison section.
- Add marketing claims that cannot be verified against current behavior or an
  official primary source.
- Maintain a comprehensive competitor database inside the README.

## Capabilities

### New Capabilities

- `product-comparison`: Defines the accuracy, sourcing, and implemented-versus-
  planned distinction required of the public product comparison.

### Modified Capabilities

None.

## Dependencies

- Official competitor documentation must remain reachable during review.
- The current behavior in `DiskScanner` and `NativeFileSize` must be checked
  before describing MacStorageAtlas measurement semantics.

## Risks

- Competitor functionality and pricing can change after verification.
- Overly detailed comparisons would become stale quickly.

## Estimate

Less than one person-day, matching WP-00.

## Impact

- Affected documentation: `README.md`.
- Affected OpenSpec capability: `product-comparison`.
- No code, API, package, or runtime behavior changes.
