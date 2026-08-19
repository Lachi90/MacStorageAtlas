## 1. Baseline and Project Setup

- [x] 1.1 Run the current full test suite to establish a passing baseline before moving files
- [x] 1.2 Create `MacStorageAtlas.Core.Tests`, `MacStorageAtlas.Rendering.Tests`, `MacStorageAtlas.Platform.Mac.Tests`, `MacStorageAtlas.App.Tests`, and `MacStorageAtlas.Benchmarks.Tests` with the existing NUnit, NSubstitute, analyzer, adapter, and coverage package references
- [x] 1.3 Add narrow project references to each new test project according to the design reference map
- [x] 1.4 Add all new test projects to `MacStorageAtlas.slnx` under the tests solution folder while keeping the old test project temporarily

## 2. Core Test Migration

- [x] 2.1 Move Core-owned tests into `tests/MacStorageAtlas.Core.Tests`
- [x] 2.2 Update moved Core test namespaces to `MacStorageAtlas.Core.Tests`
- [x] 2.3 Update Core `InternalsVisibleTo` from the umbrella test assembly to `MacStorageAtlas.Core.Tests` while preserving the benchmark tooling grant if still needed
- [x] 2.4 Build and run `MacStorageAtlas.Core.Tests`

## 3. Rendering and Platform Test Migration

- [x] 3.1 Move Rendering-owned tests into `tests/MacStorageAtlas.Rendering.Tests`
- [x] 3.2 Update moved Rendering test namespaces to `MacStorageAtlas.Rendering.Tests`
- [x] 3.3 Build and run `MacStorageAtlas.Rendering.Tests`
- [x] 3.4 Move Platform.Mac-owned tests into `tests/MacStorageAtlas.Platform.Mac.Tests`
- [x] 3.5 Update moved Platform.Mac test namespaces to `MacStorageAtlas.Platform.Mac.Tests`
- [x] 3.6 Preserve existing macOS-only and environment-sensitive `Assert.Ignore` gates during the move
- [x] 3.7 Update Platform.Mac `InternalsVisibleTo` from the umbrella test assembly to `MacStorageAtlas.Platform.Mac.Tests`
- [x] 3.8 Build and run `MacStorageAtlas.Platform.Mac.Tests`

## 4. App and Benchmark Test Migration

- [x] 4.1 Move App-owned tests into `tests/MacStorageAtlas.App.Tests`
- [x] 4.2 Update moved App test namespaces to `MacStorageAtlas.App.Tests`
- [x] 4.3 Keep App tests using service abstractions and substitutes rather than adding a Platform.Mac project reference
- [x] 4.4 Update App `InternalsVisibleTo` from the umbrella test assembly to `MacStorageAtlas.App.Tests`
- [x] 4.5 Build and run `MacStorageAtlas.App.Tests`
- [x] 4.6 Move benchmark-tool tests into `tests/MacStorageAtlas.Benchmarks.Tests`
- [x] 4.7 Update moved benchmark test namespaces to `MacStorageAtlas.Benchmarks.Tests`
- [x] 4.8 Build and run `MacStorageAtlas.Benchmarks.Tests`

## 5. Remove Umbrella Test Project

- [x] 5.1 Confirm `tests/MacStorageAtlas.Tests` contains no remaining test files
- [x] 5.2 Remove `MacStorageAtlas.Tests.csproj` and the empty umbrella test directory
- [x] 5.3 Remove the old `MacStorageAtlas.Tests` project from `MacStorageAtlas.slnx`
- [x] 5.4 Search the repository for `MacStorageAtlas.Tests` references and remove or update all remaining references

## 6. Documentation and OpenSpec

- [x] 6.1 Review `README.md` for test or validation references and update it if the old umbrella test project is mentioned
- [x] 6.2 Review relevant documentation under `docs/` and update any testing or validation guidance affected by the split
- [x] 6.3 Review `docs/index.html` and report if no update is necessary because test project layout is not user-facing
- [x] 6.4 Add or update the main `test-project-structure` spec during archive sync

## 7. Validation

- [x] 7.1 Run `dotnet build MacStorageAtlas.slnx --no-restore`
- [x] 7.2 Run `dotnet test MacStorageAtlas.slnx --no-build`
- [x] 7.3 Run `dotnet format MacStorageAtlas.slnx analyzers --diagnostics IDE0005 --verify-no-changes`
- [x] 7.4 Run `git diff --check`
- [x] 7.5 Run `openspec validate --all --strict --no-interactive`
