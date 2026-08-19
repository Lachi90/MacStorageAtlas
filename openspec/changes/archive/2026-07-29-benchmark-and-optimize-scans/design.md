## Context

WP-02 has already defined storage measurement semantics and implemented
shared-aware hardlink and verified full-clone accounting. The remaining gap is
performance: MacStorageAtlas has no repeatable benchmark command, no documented
large-scan baseline, and no measured basis for deciding whether traversal,
metadata reads, progress dispatch, or completion-time derived views are the
right optimization targets.

The current scanner is a depth-first async stream in `MacStorageAtlas.Core`.
It enumerates each directory, reads attributes for each entry, measures files
using logical or macOS allocated metadata readers, maintains shared-aware
accounting sets when enabled, mutates one `DiskItem` tree, throttles progress,
and sorts the completed tree before the final progress result. The Avalonia
ViewModel runs the scanner off the UI thread, dispatches each progress update,
and performs completion work such as search tree construction, treemap layout,
file-type summaries, and largest-file ranking.

Performance work crosses Core, Platform.Mac, App, documentation, and tests.
The design must preserve existing privacy and storage-accounting constraints:
measurement remains metadata-only, cancellation and recoverable errors remain
honest, and shared-aware totals must keep their existing scan-scoped meaning.

## Goals / Non-Goals

**Goals:**

- Add a repeatable benchmark command that can run real filesystem fixtures and
  synthetic large-entry stress cases.
- Capture baseline metrics before optimization and document the environment,
  command, fixture, scan options, and verification date.
- Separate scanner traversal time from completion/rendering time where possible
  so user-perceived scan delays can be diagnosed.
- Optimize benchmark-proven hot paths while preserving the public scan result
  model, cancellation behavior, recoverable-error behavior, and measurement
  semantics.
- Prove that one-million-entry scans do not rely on unbounded queues, duplicate
  full scan trees, or excessive UI progress updates.

**Non-Goals:**

- No content reads, hashing, physical extent enumeration, or cloud placeholder
  materialization.
- No APFS partial-clone deduplication and no unique physical storage or
  reclaimable-storage claim.
- No permanent deletion or cleanup workflow changes.
- No production dependency addition unless benchmark data proves it is required
  for a scanner or UI optimization.
- No unbounded parallel traversal.

## Decisions

### Add Benchmark Tooling Outside App Runtime

Add benchmark and fixture tooling outside the Avalonia app runtime, preferably
as a repo-owned console project under a tooling area. The tool should reference
the scanner and metadata abstractions directly and emit machine-readable
results plus concise console output.

Alternatives considered:

- Embed benchmarking in the app. Rejected because benchmark behavior is
  developer tooling and would add UI complexity to the production product.
- Use only ad hoc shell scripts. Rejected because metrics, options, and
  environment metadata need stable schemas for reproducible comparison.
- Use a test-only benchmark hidden in NUnit. Rejected as the primary interface
  because performance runs need command-line control over fixture shape,
  output path, warm/cold runs, and real filesystem targets. NUnit tests can
  still verify fixture and metric behavior.

### Record Structured Metrics and Keep Raw Results Out of Generated Artifacts

Benchmark runs should emit structured results containing at least scan mode,
scan options, fixture type, fixture counts, file and directory counts observed,
bytes scanned, progress update count, duration, throughput, peak managed
memory, error count, runtime, process architecture, OS version, and volume or
filesystem notes when available.

Raw benchmark output should be written only to explicitly requested output
paths. Documentation should summarize selected results and commands rather than
committing generated benchmark logs.

Alternatives considered:

- Commit every benchmark output. Rejected because results are environment
  specific and generated output should not churn the repo.
- Document only human-readable prose. Rejected because future comparisons need
  stable machine-readable fields.

### Use Real Fixtures and Synthetic Stress Fixtures

Real filesystem fixtures should cover normal files, sparse files, hardlinks,
symbolic links, and application packages. APFS full-clone and divergent-clone
fixtures can be reused or invoked where capability-gated support exists.
Cloud-placeholder fixtures should remain optional and must not force downloads.

Synthetic stress fixtures should exercise one-million-entry scanner behavior
through existing or carefully extended scanner injection points without
requiring developers to create one million real files for routine validation.
They are for resource-shape and progress behavior, not for proving platform
metadata correctness.

Alternatives considered:

- Use only real million-entry directories. Rejected as the default because setup
  time and disk churn make routine validation expensive.
- Use only synthetic data. Rejected because macOS metadata cost, APFS behavior,
  and volume-specific performance require real filesystem runs.

### Baseline Before Optimization

The implementation should first add the benchmark command, fixture generation,
and documentation, then record baseline numbers for logical, per-path
allocated, and shared-aware allocated scans. Only after baseline data exists
should optimization tasks modify scanner or completion behavior.

Alternatives considered:

- Optimize traversal immediately. Rejected because the current bottleneck could
  be filesystem enumeration, metadata APIs, shared-aware dictionaries,
  completion-time derived views, or UI dispatch.
- Define a fixed throughput target now. Rejected because repository-local
  hardware, external media, and cache state can vary enough that the first
  change should establish reproducible measurement before setting release
  gates.

### Keep Optimization Targets Conservative

Initial optimization candidates should favor low-risk, measured changes:
reducing duplicate metadata calls, avoiding repeated full-tree walks where
derived completion data can be accumulated without semantic drift, keeping top
largest-file candidates without sorting all files, and tuning progress update
flow.

Bounded parallel metadata reads should be treated as a spike and accepted only
if benchmarks show a material benefit without unbounded queues, broken
cancellation, inconsistent shared-aware accounting, or worse behavior on slow
volumes.

Alternatives considered:

- Parallelize directory traversal broadly. Rejected for initial scope because
  the scanner mutates one tree and one shared-aware accounting state, and
  parallel ordering would complicate progress readability and cancellation.
- Preserve strict filesystem progress order. Rejected as a hard requirement
  because the roadmap allows progress to be ordered enough to understand
  without requiring exact filesystem order.

### Preserve Responsibility Boundaries

- Core owns scanner behavior, synthetic scanner injection seams, accounting,
  progress throttling, and portable tests.
- Platform.Mac owns macOS allocated metadata timing and any platform fixture
  operations that require macOS APIs.
- App owns UI dispatch and completion-derived view performance.
- Rendering remains limited to treemap layout behavior and should only change
  if benchmark data shows layout itself is a bottleneck.
- Tests own deterministic coverage for fixture generation, cancellation,
  errors, resource bounds, and optimized behavior.
- Documentation owns benchmark commands, interpretation guidance, comparison
  caveats, and verification dates.

## Risks / Trade-offs

- Benchmark variance from cache warmth, Spotlight, thermal state, and volume
  type -> Record environment metadata, run repeated samples where practical,
  and document results as representative rather than universal.
- Synthetic fixtures missing macOS metadata costs -> Use synthetic fixtures for
  scale/resource assertions and real fixtures for platform timing.
- Parallel metadata reads increasing memory or hurting slow volumes -> Keep
  parallelism bounded, benchmark against sequential traversal, and retain the
  sequential path if evidence is weak.
- Derived completion optimizations drifting from scan semantics -> Add tests
  comparing optimized derived views with the existing tree-based results for
  logical, allocated, shared-aware, hidden-file, package, and symbolic-link
  options.
- UI measurements becoming brittle -> Count dispatches and completion work in
  deterministic ViewModel tests, and reserve wall-clock UI timing for benchmark
  documentation.
- Benchmark-only dependency creep -> Prefer the base class library; if a
  benchmark package is added, scope it to tooling and document why it is not a
  production dependency.

## Migration Plan

1. Add benchmark and fixture tooling without changing scanner behavior.
2. Add deterministic tests for metric capture, fixture shape, cancellation, and
   progress/resource behavior.
3. Run and document baseline benchmarks.
4. Apply measured optimizations one at a time, keeping the solution buildable
   and tests passing after each task group.
5. Re-run benchmark scenarios after each optimization and document before/after
   results.
6. Update WP-02 roadmap status when benchmark evidence and accepted
   optimizations are complete.

Rollback is straightforward for benchmark-only additions because they do not
affect app runtime behavior. Any scanner or ViewModel optimization should be
kept in focused commits or task groups so it can be reverted independently
while retaining benchmark tooling.

## Open Questions

- What local machines and volumes should provide the documented baseline: only
  the development Mac, or both internal SSD and an external or slower volume as
  the roadmap suggests?
- Should benchmark output use JSON only, or JSON plus Markdown summaries from
  the command?
- Should the first implementation include bounded parallel metadata reads if
  baseline data suggests a bottleneck, or should that remain a separate follow
  up after benchmark infrastructure lands?
