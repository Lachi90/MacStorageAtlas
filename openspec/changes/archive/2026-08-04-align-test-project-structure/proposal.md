## Why

The test suite is currently concentrated in one `MacStorageAtlas.Tests` project that references Core, Rendering, Platform.Mac, App, and benchmark tooling. That makes tests easy to run but hides ownership boundaries, lets project dependencies blur, and makes it harder to see whether a test belongs to portable domain logic, rendering layout, macOS integration, Avalonia application behavior, or benchmark tooling.

## What Changes

- Split the current single test project into test projects aligned with production ownership:
  - `MacStorageAtlas.Core.Tests`
  - `MacStorageAtlas.Rendering.Tests`
  - `MacStorageAtlas.Platform.Mac.Tests`
  - `MacStorageAtlas.App.Tests`
  - `MacStorageAtlas.Benchmarks.Tests`
- Move existing test files into the project that owns the code under test, preserving test names, behavior, fixtures, and platform gates.
- Replace broad test project references with narrow references matching each test project's target production assembly.
- Update `InternalsVisibleTo` declarations to point at the new test assembly names only where internals are actually required.
- Keep solution-level test execution working through `MacStorageAtlas.slnx`.
- Remove the old umbrella `MacStorageAtlas.Tests` project after its tests have been moved.

## Non-goals

- Changing product behavior, test assertions, or feature scope.
- Rewriting tests beyond namespace, project reference, fixture helper, and build-structure changes needed for the split.
- Changing NUnit, NSubstitute, coverage, SDK, target framework, or package versions unless unavoidable for the split.
- Introducing separate unit versus integration test projects beyond the production-assembly split.
- Moving production code between projects.

## Capabilities

### New Capabilities

- `test-project-structure`: How MacStorageAtlas organizes automated tests so test projects mirror production project ownership, keep references narrow, preserve platform gating, and remain runnable through the solution.

### Modified Capabilities

None. This is an engineering structure change and does not modify existing product requirements.

## Impact

- `tests/`: replace `MacStorageAtlas.Tests` with assembly-aligned test projects and move test files accordingly.
- `MacStorageAtlas.slnx`: include the new test projects and remove the old umbrella test project.
- `src/*/Properties/AssemblyInfo.cs`: update `InternalsVisibleTo` declarations from `MacStorageAtlas.Tests` to the applicable new test assemblies.
- `tools/MacStorageAtlas.Benchmarks`: no production behavior change, but benchmark tests move to a dedicated test assembly.
- Validation commands remain solution-level and must continue to pass.

## Dependencies

- Existing tests must be green before the split so failures introduced by project movement are visible.
- The split depends on the current production project boundaries: Core, Rendering, Platform.Mac, App, and Benchmarks.
- Shared test fixtures may need to be duplicated or extracted into a small test-support project only if duplication becomes materially worse than another test-only assembly.

## Risks

- Moving tests can accidentally broaden references again if each project copies the old umbrella reference set.
- `InternalsVisibleTo` updates can break tests that currently depend on internals without making that dependency explicit.
- Shared fixtures from the current flat project can create cross-project friction.
- Platform.Mac tests can become harder to run if macOS-only gating is disturbed.
- Solution-level validation can miss a test project if it is not added to `MacStorageAtlas.slnx`.

## Roadmap Estimate

This is not a product roadmap work package. Estimated implementation effort is 1-2 days because it is primarily project-file creation, file movement, namespace updates, and validation.
