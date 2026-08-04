## Context

MacStorageAtlas production code is separated into Core, Rendering, Platform.Mac, App, and benchmark tooling. The automated tests are currently centralized in `tests/MacStorageAtlas.Tests`, which references all production projects plus `tools/MacStorageAtlas.Benchmarks`.

This flat test project worked while the codebase was smaller, but it now hides ownership boundaries. Core-only tests can accidentally reference App or Platform.Mac, App view-model tests sit beside macOS adapter tests, and `InternalsVisibleTo("MacStorageAtlas.Tests")` grants one broad test assembly access to internals across multiple projects.

The change is structural. It should preserve test behavior and validation commands while making test ownership visible in project names, references, namespaces, and solution layout.

## Goals / Non-Goals

**Goals:**

- Align test projects with production assembly ownership.
- Keep each test project reference set as narrow as practical.
- Preserve all existing test names, assertions, fixtures, platform gates, and coverage intent.
- Keep `dotnet test MacStorageAtlas.slnx --no-build` as the canonical full-suite command.
- Make internal-access grants explicit per tested assembly.
- Leave product behavior unchanged.

**Non-Goals:**

- Rewriting tests for style or broad fixture cleanup.
- Splitting unit and integration tests into separate projects in this change.
- Adding new test frameworks or changing package versions.
- Moving production code between projects.
- Changing benchmark tool behavior.

## Decisions

### Split by production ownership first

Create these test projects:

- `tests/MacStorageAtlas.Core.Tests`
- `tests/MacStorageAtlas.Rendering.Tests`
- `tests/MacStorageAtlas.Platform.Mac.Tests`
- `tests/MacStorageAtlas.App.Tests`
- `tests/MacStorageAtlas.Benchmarks.Tests`

Each project owns tests whose primary subject is the matching production assembly. Tests may reference lower-level assemblies when the production assembly itself depends on them.

Alternative considered: split into `UnitTests` and `IntegrationTests`. That labels execution style but does not address the current dependency-boundary problem. Platform gates and categories can still identify integration-style tests inside the aligned project.

### Keep references narrow and directional

The target reference shape is:

```text
Core.Tests
  -> Core

Rendering.Tests
  -> Rendering
  -> Core only if needed for public types used by Rendering tests

Platform.Mac.Tests
  -> Platform.Mac
  -> Core

App.Tests
  -> App
  -> Core
  -> Rendering only where existing App tests use treemap types

Benchmarks.Tests
  -> tools/MacStorageAtlas.Benchmarks
  -> Core and Platform.Mac only if the benchmark project requires them transitively for test construction
```

Alternative considered: give each new test project the old umbrella reference set initially, then trim later. That would reduce migration friction but preserve the exact smell this change is meant to remove.

### Use mirrored namespaces

Move test namespaces from `MacStorageAtlas.Tests` to the matching assembly namespace:

- `MacStorageAtlas.Core.Tests`
- `MacStorageAtlas.Rendering.Tests`
- `MacStorageAtlas.Platform.Mac.Tests`
- `MacStorageAtlas.App.Tests`
- `MacStorageAtlas.Benchmarks.Tests`

This makes stack traces and IDE test explorers show ownership without inspecting file paths.

Alternative considered: keep `MacStorageAtlas.Tests` namespaces after moving files. That minimizes edits but leaves identity ambiguous.

### Preserve platform gates in Platform.Mac.Tests

macOS-only and environment-sensitive tests remain in `MacStorageAtlas.Platform.Mac.Tests`, gated with existing runtime checks and `Assert.Ignore` behavior. Native filesystem integration tests should move with Platform.Mac because their reason to exist is validating macOS-specific metadata and Trash behavior.

Alternative considered: create `MacStorageAtlas.Platform.Mac.IntegrationTests`. That may be useful later, but this change should first correct assembly ownership without multiplying axes.

### Avoid a shared test-support project unless needed

Start by moving helpers with the tests that use them. Extract a `MacStorageAtlas.TestSupport` project only if multiple new test assemblies need non-trivial shared fixtures and duplication would become harder to maintain than another project.

Alternative considered: introduce shared test support up front. That creates a new dependency surface before the real sharing pressure is known.

### Update internal visibility narrowly

Replace broad `InternalsVisibleTo("MacStorageAtlas.Tests")` grants with the smallest needed grants:

- Core grants `MacStorageAtlas.Core.Tests`, grants `MacStorageAtlas.App.Tests` for App-facing Core domain fixtures, and keeps `MacStorageAtlas.Benchmarks` if benchmark tooling still needs internals.
- App grants `MacStorageAtlas.App.Tests`.
- Platform.Mac grants `MacStorageAtlas.Platform.Mac.Tests`.
- Rendering should not need an internal grant unless tests prove otherwise.

Alternative considered: grant all new test assemblies internal access everywhere. That would keep migration simple but undermine dependency discipline.

## Risks / Trade-offs

- File movement can hide accidental assertion changes -> Move files first, then make only compile-required namespace and project-reference edits.
- Some tests may have mixed ownership -> Assign by primary behavior under test and document any intentionally cross-assembly reference in the project file shape.
- App tests may need Core and Rendering references because App view models expose Core and treemap types -> Allow those references but keep Platform.Mac out of App.Tests through service abstractions and substitutes.
- App tests depend on Core internal fixture construction for ViewModel-facing disk trees -> Grant Core internals to `MacStorageAtlas.App.Tests` rather than rewriting broad test fixtures during this structural change.
- Benchmark tests may depend on internal Core access indirectly -> Preserve only the existing `MacStorageAtlas.Benchmarks` internal grant unless the new benchmark test assembly directly needs one.
- IDE or CI scripts may reference the old test project path -> Search and update repository references; keep solution-level commands unchanged.
- macOS integration tests may be accidentally run on unsupported systems -> Preserve existing platform guards during movement.

## Migration Plan

1. Create new test project files with shared NUnit package references and narrow project references.
2. Move test files into the target projects according to primary code ownership.
3. Update namespaces and any compile-required usings.
4. Update `InternalsVisibleTo` declarations.
5. Update `MacStorageAtlas.slnx` to include new test projects and remove `MacStorageAtlas.Tests`.
6. Remove the old test project after it is empty.
7. Run focused builds if needed, then full solution validation.

Rollback can remove the new test projects, restore `tests/MacStorageAtlas.Tests`, restore the original `InternalsVisibleTo` declarations, and put the solution test project entry back.

## Open Questions

- Should `AccessGuidanceClassifierTests` stay in App.Tests because the classifier lives under App, or should access guidance classification move to Core in a later product-architecture change?
- Should `FilteredResultViewsTests` remain Core.Tests or move to App.Tests if future result-view projection moves toward view-model responsibilities?
- Should benchmark tests get their own test assembly now, or should they move under Core.Tests until benchmark tooling grows? The proposal chooses a dedicated benchmark test assembly for clarity.
