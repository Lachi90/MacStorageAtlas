## 1. Benchmark Tooling Foundation

- [x] 1.1 Choose the benchmark tool location and project shape, keeping it
  outside the Avalonia app runtime and scoped away from production packages.
- [x] 1.2 Add benchmark result models that capture duration, observed entry
  counts, byte totals, throughput, progress update count, recoverable error
  count, peak managed memory, measurement mode, scan options, runtime,
  architecture, operating system, fixture description, and timestamp.
- [x] 1.3 Add tests for benchmark result serialization and required metric
  fields.
- [x] 1.4 Implement the benchmark runner for real filesystem scans across
  logical, per-path allocated, and shared-aware allocated modes.
- [x] 1.5 Add tests proving completed, failed, and cancelled benchmark runs
  report completion state, errors, cancellation, and partial metrics honestly.

## 2. Fixture Generation

- [x] 2.1 Implement a representative real filesystem fixture generator for
  ordinary files, sparse files, hardlinks, symbolic links, and application
  packages under an explicit temporary or developer-selected root.
- [x] 2.2 Add platform-gated handling for unsupported fixture shapes so the
  tool reports limitations instead of substituting misleading data.
- [x] 2.3 Add tests for generated fixture shape, expected counts, cleanup, and
  unsupported-platform reporting.
- [x] 2.4 Implement a synthetic large-entry scan fixture or provider suitable
  for one-million-entry resource and progress stress runs.
- [x] 2.5 Add tests proving the synthetic fixture can stream large entry counts
  without prebuilding a full entry list.

## 3. Baseline Measurement

- [x] 3.1 Add benchmark command-line options for fixture type, root path,
  measurement mode, scan options, output path, run count, and cancellation
  threshold.
- [x] 3.2 Run baseline benchmarks for representative real fixtures in logical,
  per-path allocated, and shared-aware allocated modes.
- [x] 3.3 Run baseline synthetic large-entry benchmarks, including a
  one-million-entry scenario and a cancellation scenario.
- [x] 3.4 Record baseline findings in documentation with commands, environment
  details, measurement caveats, and verification date.

## 4. Benchmark-Gated Optimization

- [x] 4.1 Analyze baseline output to identify whether traversal, metadata
  reads, shared-aware accounting, progress dispatch, sorting, or completion
  derived views dominate measured time or memory.
- [x] 4.2 Implement the lowest-risk benchmark-proven Core scanner optimization
  while preserving inclusion, measurement, accounting, recoverable-error,
  progress, and cancellation semantics.
- [x] 4.3 Add or update Core tests covering optimized logical, per-path
  allocated, shared-aware allocated, hidden-file, symbolic-link, package,
  recoverable-error, and cancellation behavior.
- [x] 4.4 Implement benchmark-proven App completion or progress-dispatch
  optimization if baseline data shows user-perceived scan completion work is a
  bottleneck.
- [x] 4.5 Add or update ViewModel tests proving progress dispatches remain
  bounded and completion-derived views match existing scan semantics.
- [x] 4.6 Run before-and-after benchmarks for every accepted optimization and
  document the result.
- [x] 4.7 If bounded parallel metadata reads are investigated, document the
  result and either implement the bounded path with cancellation and memory
  tests or explicitly defer it with benchmark evidence.

## 5. Documentation And Validation

- [x] 5.1 Review and update `README.md`, relevant files under `docs/`, and
  `docs/index.html` for benchmark commands, results, caveats, limitations, and
  any user-visible scan behavior changes.
- [x] 5.2 Update WP-02 status and notes in `docs/IMPLEMENTATION_ROADMAP.md`.
- [x] 5.3 Run `dotnet build MacStorageAtlas.slnx --no-restore`.
- [x] 5.4 Run `dotnet test MacStorageAtlas.slnx --no-build`.
- [x] 5.5 Run `dotnet format MacStorageAtlas.slnx analyzers --diagnostics IDE0005 --verify-no-changes`.
- [x] 5.6 Run `git diff --check`.
- [x] 5.7 Run `openspec validate --all --strict --no-interactive`.
- [x] 5.8 Confirm generated benchmark output is not committed unless it is a
  hand-curated documentation summary.
