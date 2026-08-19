## Context

MacStorageAtlas already chooses between logical length and allocated bytes with
`ScanOptions.MeasureAllocatedSize`. `DiskScanner` writes the selected value into
the generic `DiskItem.SizeBytes` field and propagates it into directory and
progress totals. The App remembers the preference and describes it in the scan
options, but a `ScanProgress` or completed tree does not carry its measurement
basis. Changing the preference after a scan can therefore separate displayed
values from the context needed to interpret them.

The macOS allocated reader uses filesystem metadata and handles sparse files and
local cloud placeholders without reading contents. It currently falls back to
logical length if native metadata lookup fails, which makes an allocated result
indistinguishable from a fallback. Allocated totals also sum each visited path;
they do not yet deduplicate hardlinks or APFS clones.

This is the first independently reviewable slice of WP-02. It establishes the
contract on which hardlink deduplication, APFS investigation, volume reporting,
and benchmarks can rely.

## Goals / Non-Goals

**Goals:**

- Establish one canonical glossary for file and volume storage terms.
- Make the logical/allocated basis part of scan progress and result context.
- Preserve one measurement basis through file, directory, and progress totals.
- Prevent a failed allocated lookup from silently becoming a logical value.
- Document reproducible comparisons for representative normal and sparse files.
- Preserve metadata-only, local scanning and current cancellation behavior.

**Non-Goals:**

- Implement unique allocated accounting, file identity, or hardlink
  deduplication.
- Detect APFS shared extents or clone identity.
- Report volume capacity, available space, or purgeable space.
- Build the WP-02 fixture generator or benchmark command.
- Parallelize or otherwise optimize traversal.

## Decisions

### Add explicit measurement metadata without replacing the current option

Core will define a small `StorageMeasurementMode` value with `Logical` and
`Allocated` cases. `ScanProgress` will carry the mode captured when the scan
starts, and the App will retain the mode associated with the displayed result.
`DiskItem.SizeBytes` remains the basis-dependent value to avoid duplicating
every tree or storing two sizes per item.

`ScanOptions.MeasureAllocatedSize` remains the input for this focused change.
Replacing it with an enum now would create avoidable source and settings
migration work; the later unique-allocated change can evolve the option when a
third executable mode actually exists.

Alternative considered: infer the basis from the current App preference. This
fails when the preference changes during or after a scan and leaves Core results
self-ambiguous.

Alternative considered: store both logical and allocated bytes on every
`DiskItem`. This would require two metadata reads and broaden memory/API impact
without being necessary to define the active scan's numbers.

### Treat allocated lookup failure as missing data

On supported macOS targets, an allocated-mode lookup that cannot obtain
allocated metadata will enter the scanner's existing recoverable-error path.
It will not substitute the file's logical length. This keeps every included
byte under one basis and makes incomplete totals observable.

The existing stable `stat(2)` integration remains the macOS mechanism for this
change on both Apple Silicon and Intel. No unstable API or technical spike is
required. A later platform-boundary change may move native identity and size
capabilities into Platform.Mac alongside hardlink work.

Alternative considered: retain the logical fallback. It maximizes the number of
files with a value, but it produces a mixed-basis total labeled as allocated.

### Keep aggregation basis-agnostic

Core will continue to aggregate the one selected `SizeBytes` value through the
tree and progress counters. Focused tests will prove that logical and allocated
readers feed the same aggregation path, package collapsing does not alter
totals, excluded symbolic links contribute nothing, and recoverable failures
contribute unknown rather than zero-success values.

Rendering remains unchanged: it lays out the already measured `SizeBytes` and
must not reinterpret the value. This avoids duplicate trees and preserves
current performance characteristics.

Alternative considered: give Rendering separate logical and allocated layout
paths. That would duplicate policy outside Core and risk presenting a layout
whose labels use a different basis.

### Put terminology in a canonical developer document and concise UI labels

A storage-measurement reference will define logical, allocated, unique
allocated, free, used, available, and purgeable terms; document per-path
hardlink/clone limitations; and provide a reproducible macOS comparison for
normal and sparse fixtures. README and scan-option/help text will link to or
summarize this contract instead of claiming equivalence with Finder or `du`.

The App will expose the captured mode near scan totals or result context so the
basis remains visible without requiring users to reopen preferences.

Alternative considered: leave the definitions only in XML comments. Those
comments do not help users interpret results and cannot serve as a stable
cross-change WP-02 contract.

### Assign responsibilities along existing project boundaries

- **Core:** owns measurement-mode vocabulary, progress metadata, reader failure
  semantics, and basis-consistent aggregation.
- **Rendering:** consumes `SizeBytes` unchanged and adds no measurement policy.
- **Platform.Mac:** adds no new API in this slice; its future file-identity/shared
  extent capabilities must use the glossary established here.
- **App:** captures the selected option at scan start, preserves the result's
  returned mode, and labels displayed values accurately.
- **Tests:** covers Core mode propagation and aggregation, macOS normal/sparse
  metadata behavior, App label persistence, errors, and cancellation.

## Risks / Trade-offs

- **Changing failed native lookups from logical fallback to scan errors can make
  some totals smaller** → Report the affected path as an error and document that
  incomplete allocated data is preferable to a mixed-basis total.
- **Adding progress metadata affects test doubles and positional construction**
  → Add the field compatibly at the end of the progress contract where possible
  and update all in-repository fixtures.
- **Users may read “allocated” as unique physical usage** → Keep the per-path
  limitation adjacent to the canonical definition and result explanation.
- **Finder and `du` can differ because of scope, rounding, hardlinks, or APFS
  behavior** → Document comparisons as diagnostic checks, not exact universal
  equivalence.
- **Extra result labeling could crowd the UI** → Use a short mode label and keep
  the detailed explanation in tooltip/help documentation.
- **Platform-native struct behavior could differ across macOS architectures**
  → Retain architecture-aware integration and exercise representative fixtures
  on both supported release architectures.
- **Privacy regression through validation fixtures** → Generate fixtures
  locally, inspect metadata only, and never automate cloud-provider downloads.

## Migration Plan

1. Add the Core measurement-mode type and propagate the captured mode through
   scan progress without changing the persisted boolean preference.
2. Make allocated lookup failures explicit and add reader/error aggregation
   tests before changing App presentation.
3. Retain and display the returned mode with scan results; add App tests that
   change the next-scan preference without relabeling the current result.
4. Add the canonical documentation and representative macOS validation
   procedure, then align concise README and UI wording.
5. Run the full build, test suite, and strict OpenSpec validation on macOS.

Rollback is a normal code-and-documentation revert. No scan database or user
data migration exists. The persisted `MeasureAllocatedSize` setting remains
compatible, so rollback does not require settings cleanup.

## Open Questions

None for this change. Exact hardlink identity, APFS clone support, volume APIs,
and benchmark thresholds remain decisions for their named follow-up changes.
