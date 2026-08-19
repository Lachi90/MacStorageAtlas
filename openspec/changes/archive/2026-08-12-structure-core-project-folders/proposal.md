## Why

`MacStorageAtlas.Core` has grown to 97 source files in one flat directory and `MacStorageAtlas.Core.Tests` to 28 flat test files, so nothing in the layout tells a reader which types belong to scanning, filtering, cleanup, relocation, export, history, or access guidance. `MacStorageAtlas.App` already groups its code into `Services`, `ViewModels`, `Models`, `Views`, `Controls`, and `Converters` with folder-matching namespaces, and Core is the only large project that does not follow that convention. Doing this now, while Core is between roadmap work packages, avoids restructuring on top of in-flight feature branches.

## What Changes

- Group `src/MacStorageAtlas.Core` source files into domain folders that reflect Core's existing capability boundaries: `Scanning`, `Items`, `Filtering`, `Insights`, `Cleanup`, `Relocation`, `Export`, `History`, `Serialization`, `Access`, and `Platform`.
- **BREAKING** (source-level, internal to this repository): give each folder a folder-matching namespace such as `MacStorageAtlas.Core.Scanning`, replacing the single flat `MacStorageAtlas.Core` namespace. Every consuming file in `MacStorageAtlas.App`, `MacStorageAtlas.Platform.Mac`, `tools/MacStorageAtlas.Benchmarks`, and the test projects updates its `using` directives, and the one AXAML reference to a Core type updates its `xmlns` mapping. No public API shape, type name, member, or behavior changes.
- Mirror the same folder names in `tests/MacStorageAtlas.Core.Tests`, so each test file sits in the folder matching the Core folder it exercises, with a matching `MacStorageAtlas.Core.Tests.<Folder>` namespace.
- Keep every test name, assertion, fixture, platform gate, and temporary-directory pattern unchanged; this change moves and renamespaces files only.
- Leave `MacStorageAtlas.Rendering`, `MacStorageAtlas.Platform.Mac`, `MacStorageAtlas.App`, and their test projects untouched.

## Non-goals

- Changing product behavior, public type names, member signatures, serialization formats, or persisted snapshot and export schemas.
- Splitting `MacStorageAtlas.Core` into multiple assemblies or moving production code between projects.
- Restructuring `Rendering`, `Platform.Mac`, `App`, or their test projects, or extracting a shared test-support project.
- Rewriting tests, adding new coverage beyond what the move requires, or changing NUnit, NSubstitute, SDK, target framework, or package versions.
- Adding folder-level `namespace` aliasing, global usings, or type forwarders to soften the namespace move.

## Capabilities

### New Capabilities

- `core-project-structure`: How `MacStorageAtlas.Core` organizes its source files into domain folders with folder-matching namespaces, where cross-cutting host abstractions live, and how the layout stays stable as Core grows.

### Modified Capabilities

- `test-project-structure`: adds the requirement that a test project's internal folder and namespace layout mirrors the folder layout of the production project it tests, alongside the existing assembly-alignment requirements.

## Impact

- `src/MacStorageAtlas.Core/`: all 97 source files move into domain folders and receive folder-matching namespaces. `Properties/AssemblyInfo.cs` stays where it is.
- `tests/MacStorageAtlas.Core.Tests/`: all 28 test files move into mirrored folders with mirrored namespaces.
- `src/MacStorageAtlas.App/`, `src/MacStorageAtlas.Platform.Mac/`, `tools/MacStorageAtlas.Benchmarks/`, `tests/MacStorageAtlas.App.Tests/`, `tests/MacStorageAtlas.Platform.Mac.Tests/`, `tests/MacStorageAtlas.Rendering.Tests/`, `tests/MacStorageAtlas.Benchmarks.Tests/`: 93 files that carry `using MacStorageAtlas.Core;` update their imports; unused imports must be removed so `dotnet format ... --diagnostics IDE0005 --verify-no-changes` stays clean.
- One AXAML file that references a Core type through an `xmlns` mapping updates that mapping.
- No `.csproj` change is required, because the SDK globs `**/*.cs`; no solution change is required, because no project is added or removed.
- `AGENTS.md`, `CLAUDE.md`, `README.md`, and `docs/` entries that describe repository structure are reviewed and updated where they describe Core's flat layout.

## Dependencies

- The working tree must be green before the move so any failure is attributable to the restructure.
- Depends on the already-delivered assembly-aligned test project split (`test-project-structure`); this change refines the inside of `MacStorageAtlas.Core.Tests` rather than its boundary.
- No external package, SDK, or Apple tooling dependency.

## Risks

- A wide mechanical `using` rewrite can silently leave a stale or unused import; the IDE0005 verification step is the guard.
- Namespace-qualified strings in serialization, reflection, or AXAML `xmlns` mappings can break without a compiler error. Core's snapshot and export schemas must be checked for type-name qualification before the move.
- A partial move can leave two types with the same name reachable through different namespaces and produce confusing ambiguity errors mid-migration.
- Renaming namespaces creates a large diff that can hide an accidental behavior edit; the change must contain moves and import edits only.
- Choosing folder boundaries that do not match how Core actually grows would force another restructure later; the taxonomy is derived from existing capability specs rather than invented.

## Roadmap Estimate

This is not a product roadmap work package and does not advance any WP in `docs/IMPLEMENTATION_ROADMAP.md`; it is engineering-structure work of the same kind as the archived `align-test-project-structure` change. Estimated implementation effort is 1-2 days, dominated by mechanical file movement, namespace and import updates, and full-solution validation.
