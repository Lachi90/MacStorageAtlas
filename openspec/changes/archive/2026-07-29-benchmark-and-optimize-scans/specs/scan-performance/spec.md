## ADDED Requirements

### Requirement: Benchmarks Record Reproducible Scan Metrics

MacStorageAtlas SHALL provide a developer benchmark workflow that records scan
performance metrics together with enough scan, fixture, runtime, platform, and
environment metadata to reproduce and compare results.

#### Scenario: Benchmark completes successfully

- **GIVEN** a developer runs a scan benchmark against a supported fixture
- **WHEN** the benchmark completes
- **THEN** the result records scan duration, observed file count, observed
  directory count, observed byte total, throughput, progress update count,
  recoverable error count, peak managed memory, measurement mode, scan options,
  runtime version, process architecture, operating system version, fixture
  description, and benchmark timestamp
- **AND** the result identifies whether the run used a real filesystem fixture
  or synthetic scanner fixture

#### Scenario: Benchmark encounters recoverable scan errors

- **GIVEN** a benchmark fixture includes paths that produce recoverable scan
  errors
- **WHEN** the benchmark completes
- **THEN** the result records the error count
- **AND** the completed metrics remain internally consistent with the scanner's
  reported progress and measurement mode

#### Scenario: Benchmark is cancelled

- **GIVEN** a benchmark run is cancelled before scanner completion
- **WHEN** cancellation is observed
- **THEN** the result is not reported as a completed scan
- **AND** any partial metrics identify the cancellation
- **AND** partial byte totals remain based only on successfully measured entries

### Requirement: Fixture Generation Covers Representative Scan Shapes

MacStorageAtlas SHALL provide repeatable benchmark fixtures that exercise
ordinary files, sparse files, hardlinks, symbolic links, application packages,
and large-entry scan shapes without depending on a user's real files.

#### Scenario: Representative fixture is generated

- **WHEN** a developer creates the representative benchmark fixture
- **THEN** the fixture contains ordinary files, sparse files, hardlinks,
  symbolic links, and an application package
- **AND** the fixture records the expected entry shape needed to interpret
  benchmark results
- **AND** the fixture is created under a developer-selected or temporary
  directory rather than modifying unrelated user files

#### Scenario: Platform-specific fixture is unsupported

- **GIVEN** the current platform or volume cannot create a requested
  platform-specific fixture shape
- **WHEN** fixture generation runs
- **THEN** the fixture workflow reports that limitation clearly
- **AND** it does not silently replace the unsupported shape with misleading
  data

#### Scenario: Cloud placeholder fixture is unavailable

- **GIVEN** no safe local cloud-placeholder fixture can be created without
  provider-specific downloads or user data
- **WHEN** benchmark fixtures are generated
- **THEN** the benchmark workflow may omit that fixture
- **AND** documentation identifies the omission and its reason

### Requirement: Large Scans Use Bounded Progress And Work Queues

MacStorageAtlas SHALL keep large scans streaming and bounded so one-million
entry scan scenarios do not require unbounded pending work, duplicate full scan
trees, or excessive UI progress dispatches.

#### Scenario: Synthetic one-million-entry scan runs

- **GIVEN** a synthetic benchmark fixture exposes one million file entries
- **WHEN** the scanner consumes that fixture
- **THEN** scan progress is streamed without materializing a second full list of
  all entries
- **AND** progress updates remain throttled
- **AND** the completed result contains one scan tree rather than duplicate full
  tree copies

#### Scenario: Large scan is cancelled

- **GIVEN** a large scan is running
- **WHEN** cancellation is requested
- **THEN** the scanner stops accepting new work promptly
- **AND** any active work drains without reporting the scan as complete
- **AND** any retained partial result remains consistent with its measurement
  mode

#### Scenario: UI receives large-scan progress

- **GIVEN** the application consumes progress from a large scan
- **WHEN** progress is dispatched to the UI layer
- **THEN** progress dispatches remain bounded by the scanner's throttling
  behavior
- **AND** completion-time view construction does not require a second retained
  full copy of the scan tree

### Requirement: Optimizations Preserve Scan Semantics

Any scan-performance optimization SHALL preserve existing scan inclusion,
measurement, accounting, recoverable-error, progress, and cancellation
semantics.

#### Scenario: Optimized scan uses logical measurement

- **GIVEN** an optimized scanner path is used
- **WHEN** a logical scan completes
- **THEN** file, directory, progress, and completed byte totals match the
  logical sizes of included successfully measured files

#### Scenario: Optimized scan uses shared-aware allocated measurement

- **GIVEN** an optimized scanner path is used
- **WHEN** a shared-aware allocated scan includes repeated file identities or
  verified full-clone data
- **THEN** counted, measured, and shared byte totals match the storage
  measurement requirements
- **AND** every included path remains browsable in the completed result

#### Scenario: Optimized scan applies inclusion options

- **GIVEN** an optimized scanner path is used
- **WHEN** hidden-file, symbolic-link, or package-expansion options exclude or
  collapse entries
- **THEN** the scan aggregates only entries included by those options
- **AND** collapsed package size remains based on its included descendants

#### Scenario: Optimized scan encounters recoverable errors

- **GIVEN** an optimized scanner path encounters a recoverable access,
  enumeration, or metadata error
- **WHEN** scanning continues
- **THEN** the error is reported
- **AND** unknown sizes are excluded from totals
- **AND** successfully measured siblings remain in the result

### Requirement: Benchmark Results Are Documented With Caveats

MacStorageAtlas SHALL document benchmark commands, representative results,
measurement caveats, environment details, and the verification date so future
developers can rerun and interpret scan-performance results.

#### Scenario: Developer reads benchmark documentation

- **WHEN** a developer reads the benchmark documentation
- **THEN** it describes how to generate fixtures, run benchmarks, and interpret
  output metrics
- **AND** it records representative baseline and optimized results with the
  verification date
- **AND** it explains that hardware, volume type, filesystem cache warmth,
  Spotlight activity, thermal state, and external media can affect timings

#### Scenario: Developer compares with system tools

- **GIVEN** benchmark documentation compares MacStorageAtlas results with
  Finder, `du`, or `stat`
- **WHEN** a developer reads that comparison
- **THEN** the documentation identifies which measurement modes are comparable
  to each tool
- **AND** it does not present Finder or aggregate system tools as authoritative
  proof of unique physical storage
