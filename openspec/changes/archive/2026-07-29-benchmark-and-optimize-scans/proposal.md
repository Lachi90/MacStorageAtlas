## Why

WP-02 still lacks repeatable scan-performance evidence after the measurement,
hardlink, and APFS full-clone accounting work. MacStorageAtlas needs a
benchmark baseline and measured optimization path so large scans remain
responsive without weakening storage-accounting correctness.

## What Changes

- Add reproducible scan benchmark tooling that records duration, entry counts,
  throughput, peak managed memory, progress update counts, error counts, scan
  options, runtime, architecture, operating system, and fixture metadata.
- Add fixture generation for representative scan shapes, including normal
  files, sparse files, hardlinks, symbolic links, application packages, and a
  synthetic large-entry provider suitable for one-million-entry stress runs.
- Document baseline benchmark results and comparison procedures for Finder,
  `du`, and `stat` where those comparisons are meaningful for the selected
  measurement mode.
- Optimize only benchmark-proven scan hot paths while preserving streaming,
  cancellation, recoverable-error handling, shared-aware accounting, package
  behavior, symbolic-link behavior, and tree interpretability.
- Ensure one-million-entry scans do not create unbounded work queues, duplicate
  full scan trees, or excessive UI progress dispatches.
- Update roadmap and relevant docs with benchmark usage, observed results,
  limits, and the verification date.

## Capabilities

### New Capabilities

- `scan-performance`: Covers repeatable scan benchmarks, large-scale scan
  responsiveness, bounded resource use, and benchmark-driven optimization
  requirements.

### Modified Capabilities

- None.

## Impact

- Affected code: scanner traversal and completion paths in
  `src/MacStorageAtlas.Core`, macOS metadata measurement in
  `src/MacStorageAtlas.Platform.Mac`, scan orchestration and completion work in
  `src/MacStorageAtlas.App`, and benchmark or fixture tooling added under an
  appropriate repo-owned project or tool directory.
- Affected tests: NUnit coverage for benchmark fixture generation, progress
  throttling, cancellation, recoverable errors, and any optimized scanner or
  completion behavior.
- Documentation: `README.md`, relevant `docs/` files, `docs/index.html`, and
  WP-02 roadmap status must be reviewed and updated when user-visible commands,
  results, or limitations change.
- Dependencies: no production package dependency is expected. A benchmark-only
  dependency may be added only if it is scoped away from app runtime packages
  and justified by repeatability or measurement quality.
- Non-goals: no permanent deletion behavior, no content reads or hashing, no
  APFS partial-extent deduplication, no unique physical storage claim, no
  network-provider-specific cloud placeholder downloads, and no unbounded
  parallel traversal.
- Risks: benchmark results can vary by volume, thermal state, Spotlight and
  other system activity, filesystem cache warmth, APFS capability coverage, and
  external or network media behavior. Parallel metadata work can reduce
  readability of progress and make slow volumes worse if not bounded.
- Roadmap estimate: WP-02 remains the parent roadmap item; this change covers
  the remaining benchmark and performance phases and should fit within the
  residual WP-02 estimate after completed measurement, hardlink, and APFS clone
  changes.
