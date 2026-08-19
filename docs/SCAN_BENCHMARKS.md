# Scan benchmarks

MacStorageAtlas includes a developer benchmark tool for repeatable scanner
measurements. It records scan duration, observed file and directory counts,
byte totals, throughput, progress update count, recoverable error count, peak
managed memory, scan options, runtime, architecture, operating system, fixture
metadata, and timestamp.

Benchmark results are representative for the machine, volume, cache state, and
system load that produced them. Hardware, APFS behavior, external media,
network volumes, Spotlight activity, filesystem cache warmth, and thermal
state can change timings substantially.

## Commands

Create a representative real filesystem fixture:

```shell
dotnet run --project tools/MacStorageAtlas.Benchmarks -- \
  fixture representative \
  --root /tmp/macstorageatlas-benchmark/representative \
  --output /tmp/macstorageatlas-benchmark/fixture.json
```

Run an existing filesystem tree:

```shell
dotnet run --project tools/MacStorageAtlas.Benchmarks -- \
  run \
  --fixture existing \
  --root /tmp/macstorageatlas-benchmark/representative \
  --mode shared-aware \
  --output /tmp/macstorageatlas-benchmark/result.json
```

Run the synthetic one-million-entry stress fixture:

```shell
dotnet run --project tools/MacStorageAtlas.Benchmarks -- \
  run \
  --fixture synthetic \
  --synthetic-files 1000000 \
  --mode logical \
  --output /tmp/macstorageatlas-benchmark/synthetic-1m.json
```

Run a cancellation scenario:

```shell
dotnet run --project tools/MacStorageAtlas.Benchmarks -- \
  run \
  --fixture synthetic \
  --synthetic-files 1000000 \
  --mode logical \
  --cancel-after-progress 2 \
  --output /tmp/macstorageatlas-benchmark/synthetic-cancelled.json
```

Supported modes are `logical`, `allocated`, and `shared-aware`. Supported
fixture types are `existing`, `representative`, and `synthetic`. Use
`--include-hidden`, `--follow-symlinks`, and `--collapse-packages` to match scan
options.

The tool writes JSON only when `--output` is provided. Generated benchmark JSON
is environment-specific and should stay outside the repository unless a
hand-curated summary is intentionally added to documentation.

## Fixtures

The representative fixture creates ordinary files, a sparse file, a hardlink
where supported, a symbolic link where supported, and an `Example.app` package.
Unsupported platform shapes are reported in the fixture result instead of
being silently replaced with misleading data.

The synthetic fixture does not touch the user's filesystem. It streams generated
scan progress through an `IDiskScanner` implementation and builds one result
tree, so it is useful for progress and memory shape. It does not measure macOS
metadata API costs.

Cloud-placeholder fixtures are intentionally omitted by default because creating
them safely depends on provider-specific state and could trigger downloads or
use private user data.

## Local Baseline

The following runs were verified on 2026-07-29 using .NET 10.0.10, arm64,
macOS 26.6.0, and temporary fixtures under `/tmp` on the development Mac. The
real fixture is intentionally tiny, so use it as a correctness and command
smoke test rather than a universal throughput target.

| Scenario | State | Files | Dirs | Progress updates | Errors | Duration ms | Entries/s | Peak managed bytes |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Real fixture, logical, before hot-loop optimization | Completed | 6 | 5 | 3 | 0 | 5.70 | 1,930.54 | 136,600 |
| Real fixture, allocated, before hot-loop optimization | Completed | 6 | 5 | 3 | 0 | 10.56 | 1,041.62 | 137,440 |
| Real fixture, shared-aware, before hot-loop optimization | Completed | 6 | 5 | 3 | 0 | 13.44 | 818.15 | 138,072 |
| Synthetic 1,000,000 files, logical, before hot-loop optimization | Completed | 1,000,000 | 246 | 246 | 0 | 347.32 | 2,879,881.75 | 481,282,288 |
| Synthetic 1,000,000 files, cancelled after two progress updates | Cancelled | 4,096 | 2 | 2 | 0 | 8.75 | 468,342.86 | 1,671,448 |
| Real fixture, logical, after hot-loop optimization | Completed | 6 | 5 | 3 | 0 | 5.52 | 1,990.99 | 135,456 |
| Real fixture, allocated, after hot-loop optimization | Completed | 6 | 5 | 3 | 0 | 10.22 | 1,076.16 | 136,280 |
| Real fixture, shared-aware, after hot-loop optimization | Completed | 6 | 5 | 3 | 0 | 12.23 | 899.19 | 136,912 |
| Synthetic 1,000,000 files, logical, after hot-loop optimization | Completed | 1,000,000 | 246 | 246 | 0 | 352.11 | 2,840,751.69 | 479,972,880 |

## Interpretation

Allocated and shared-aware allocated modes are slower than logical mode on the
real fixture because they use macOS allocation and identity metadata. The
shared-aware mode also maintains identity and clone-accounting state.

The one-million-entry synthetic scan produced 246 progress updates, roughly one
per 4,096 entries plus lifecycle updates, and did not use an unbounded pending
work queue or a duplicate full scan tree. Peak managed memory reflects the one
retained result tree with one million file items.

The accepted Core optimization replaced hot-loop `Enum.HasFlag` attribute
checks with bitwise checks. It is deliberately small and preserves scanner
semantics. The synthetic timing difference is within noise for this run; the
real fixture moved modestly faster.

Bounded parallel metadata reads were investigated at the design and benchmark
analysis level and deferred. The current evidence does not include slower or
external volume runs, and parallel metadata work can increase memory, make
cancellation harder, and hurt slow media. Revisit it only after collecting
larger real-filesystem baselines on internal SSD and slower or external
volumes.

## Comparing System Tools

Use Finder, `du`, and `stat` only when their scope and measurement basis match
the benchmark. `stat` exposes logical length and allocated block counts for
individual paths. Ordinary `du` deduplicates hardlinks, while `du -l` counts
hardlink paths separately. Finder and aggregate tools can differ in rounding,
scope, cloud handling, permissions, and clone accounting. They are comparison
points, not proof of unique physical or reclaimable storage.
