## Context

The README currently presents a small comparison between MacStorageAtlas and
other disk analyzers. Its `Real on-disk size` row marks DaisyDisk as lacking the
capability, while DaisyDisk's official product documentation states that it
calculates physically used space and excludes clones and hardlinks that do not
consume additional space.

MacStorageAtlas currently reads allocated file blocks on macOS, which correctly
handles sparse files and local cloud placeholders but is not yet equivalent to
unique physical-storage accounting across hardlinks and APFS clones. WP-02 will
address that implementation gap; this WP-00 change only corrects the public
description.

## Goals / Non-Goals

**Goals:**

- Make every retained comparison claim defensible from a current primary source.
- Describe MacStorageAtlas measurement semantics precisely.
- Keep shipped behavior separate from roadmap behavior.
- Keep the README comparison small enough to maintain.

**Non-Goals:**

- Change scanner behavior in Core or Platform.Mac.
- Create an automatically synchronized competitor database.
- Redesign the documentation site.
- Commit to a new product price or distribution model.

## Decisions

### Use capability-oriented rows with qualified wording

The comparison will use durable capability categories and short qualifications
where a checkmark would hide an important semantic difference.

Alternative considered: retain a binary checkmark matrix. This is simpler, but
it cannot truthfully represent the difference between allocated blocks and
unique physical storage.

### Link claims to official sources and record verification date

Competitor claims will link to official documentation and include a visible
`Last verified` date. The initial DaisyDisk correction will use its official
product or technical-specification page.

Alternative considered: link to review articles. Reviews are easier to read but
are secondary sources and become stale without the vendor changing them.

### Keep implementation responsibility in documentation only

This change touches `README.md` and the OpenSpec documentation capability. Core,
Rendering, Platform.Mac, App, and Tests have no runtime responsibility in this
change. The scanner's existing behavior is inspected only to avoid overstating
the documentation.

Alternative considered: bundle hardlink deduplication with the documentation
fix. That would turn a sub-day documentation correction into the larger WP-02
storage-accounting change and make review less focused.

### Treat the correction as immediately reversible

Rollback is a normal documentation revert. There is no data migration, runtime
compatibility impact, privacy impact, or macOS version dependency.

## Risks / Trade-offs

- **Competitor behavior changes after verification** -> Keep the verification
  date visible and review the small table during release preparation.
- **Qualified text makes the table wider** -> Limit the table to durable
  differentiators and move explanatory detail below it where necessary.
- **Official marketing wording is ambiguous** -> Prefer technical documentation
  and omit claims that cannot be verified precisely.

## Migration Plan

1. Replace the unsupported binary claim in the README.
2. Add official-source links and a verification date.
3. Review every remaining row against current MacStorageAtlas behavior.
4. Revert the documentation commit if a factual error is found; no runtime
   rollback is required.

## Open Questions

None. The exact compact table wording can be finalized during implementation as
long as it satisfies the delta specification.
