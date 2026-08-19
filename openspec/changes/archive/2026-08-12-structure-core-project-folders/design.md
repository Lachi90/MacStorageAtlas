## Context

`MacStorageAtlas.Core` holds 97 source files directly in `src/MacStorageAtlas.Core`, and `MacStorageAtlas.Core.Tests` holds 28 test files directly in `tests/MacStorageAtlas.Core.Tests`. Every Core type is declared in the single namespace `MacStorageAtlas.Core`, so 93 files across App, Platform.Mac, the benchmark tool, and the test projects import Core with one `using MacStorageAtlas.Core;`, and `src/MacStorageAtlas.App/Views/MainWindow.axaml` maps Core with one `xmlns:core="using:MacStorageAtlas.Core"`.

`MacStorageAtlas.App` already groups its code into `Assets`, `Controls`, `Converters`, `Models`, `Properties`, `Services`, `Styles`, `ViewModels`, and `Views`, and declares folder-matching namespaces such as `MacStorageAtlas.App.Services`. Core is the largest project and the only one that does not follow that convention.

Constraints that shape this design:

- The repository has no `.editorconfig`, so the folder-namespace convention is enforced by the compiler and by the existing `dotnet format ... --diagnostics IDE0005 --verify-no-changes` gate rather than by an IDE0130 rule.
- The archived `align-test-project-structure` change already fixed the *boundary* of `MacStorageAtlas.Core.Tests`; this change only reshapes its inside.
- The Core project file uses the default SDK glob, so moving files needs no `.csproj` edit and adds no project to the solution.
- Core persists scan snapshots, scan history entries, and CSV/JSON exports. Those formats must survive the move byte-compatibly.

## Goals / Non-Goals

**Goals:**

- Give every Core source file a folder that names the responsibility it serves.
- Make Core's namespaces match its folders, matching the App project's existing convention.
- Mirror the Core folder layout inside `MacStorageAtlas.Core.Tests`.
- Keep the change mechanically verifiable: a reviewer should be able to confirm that only file locations, namespace declarations, and import lines changed.
- Leave Core's public type names, members, behavior, and persisted formats untouched.

**Non-Goals:**

- Splitting Core into several assemblies, or introducing an `Abstractions` assembly.
- Restructuring `Rendering`, `Platform.Mac`, `App`, or their test projects.
- Adding `[assembly: TypeForwardedTo]`, namespace aliases, or `global using` shims to soften the namespace move.
- Introducing an `.editorconfig` or new analyzer package to enforce the convention.
- Any change to test assertions, coverage, or package versions.

## Decisions

### Decision 1: Group by capability domain, not by technical kind

Core is folded into folders named after the capability each type serves, matching the capability specs already in `openspec/specs/`.

| Folder | Namespace | Files | Contents |
| --- | --- | --- | --- |
| `Scanning` | `MacStorageAtlas.Core.Scanning` | 12 | `AllocatedFileMetadata`, `CloneAccountingCoverage`, `DiskScanner`, `FileIdentity`, `IAllocatedFileMetadataReader`, `IDiskScanner`, `ScanCompleteness`, `ScanError`, `ScanOptions`, `ScanProgress`, `SharedDataIdentity`, `StorageMeasurementMode` |
| `Items` | `MacStorageAtlas.Core.Items` | 7 | `DiskItem`, `DiskItemKind`, `DiskItemMetadata`, `DiskItemSorter`, `FileCategory`, `FileCategoryMap`, `FileSizeFormatter` |
| `Filtering` | `MacStorageAtlas.Core.Filtering` | 10 | `AbsoluteDateCriterion`, `BuiltInFilterPresets`, `DateCriterion`, `DiskItemFilter`, `DiskItemFilterEvaluator`, `DiskItemFilterValidation`, `FilterPreset`, `FilterResult`, `RelativeDateCriterion`, `RelativeDateUnit` |
| `Insights` | `MacStorageAtlas.Core.Insights` | 3 | `FileTypeStatisticsService`, `FileTypeSummary`, `LargeFilesService` |
| `Cleanup` | `MacStorageAtlas.Core.Cleanup` | 20 | all `Cleanup*` types, `ICleanupFileSystemMetadataReader`, `ITrashService` |
| `Relocation` | `MacStorageAtlas.Core.Relocation` | 9 | all `Relocation*` types, `FileSystemRelocationDestinationProbe`, `IRelocationDestinationProbe`, `IItemRelocationService` |
| `Export` | `MacStorageAtlas.Core.Export` | 13 | all `ScanExport*` types, `ScanResultCsvWriter`, `ScanResultJsonReader`, `ScanResultJsonWriter` |
| `History` | `MacStorageAtlas.Core.History` | 16 | all `ScanHistory*` and `ScanSnapshot*` types, `IScanHistoryStore`, `FileSystemScanHistoryStore` |
| `Serialization` | `MacStorageAtlas.Core.Serialization` | 1 | `ScanDocumentJson` |
| `Access` | `MacStorageAtlas.Core.Access` | 4 | `FullDiskAccessAssessment`, `FullDiskAccessSettingsResult`, `FullDiskAccessStatus`, `IFullDiskAccessService` |
| `Platform` | `MacStorageAtlas.Core.Platform` | 2 | `IFileRevealService`, `IQuickLookService` |
| `Properties` | `MacStorageAtlas.Core` | 1 | `AssemblyInfo` (unchanged) |

Grouping by capability keeps a feature's model, validator, and abstraction adjacent, so a reviewer working on cleanup or export reads one folder. The rejected alternative was grouping by technical kind (`Models`, `Services`, `Abstractions`, `Enums`), which reads tidily but scatters each feature across four folders and would put `CleanupBasketItem`, `CleanupBasketPlanner`, `CleanupOperationKind`, and `ITrashService` in four different places. A second rejected alternative was a shallow two-level layout (`Scanning`, `Cleanup`, `Everything else`), which does not remove the flat-directory problem.

`Insights` covers the two aggregate-analysis services that operate on a completed scan rather than on the filter pipeline. `LargeFilesService` and `FileTypeStatisticsService` are grouped there rather than under `Filtering` because they summarize a scan instead of narrowing it.

### Decision 2: An abstraction lives with its consumer; orphans go to `Platform`

Core declares six abstractions that `MacStorageAtlas.Platform.Mac` implements: `IAllocatedFileMetadataReader`, `IFullDiskAccessService`, `IItemRelocationService`, `ITrashService`, `IFileRevealService`, and `IQuickLookService`. Four of them are consumed by a Core responsibility and are filed with it: the metadata reader with `Scanning`, the Full Disk Access service with `Access`, the relocation service with `Relocation`, and the Trash service with `Cleanup`. `IFileRevealService` and `IQuickLookService` are invoked only from the App layer and belong to no Core responsibility, so they go to `Platform`.

The rejected alternative was a single `Abstractions` folder holding all six. That gives a uniform rule but separates `ITrashService` from `CleanupPreflightValidator`, which is exactly the adjacency Decision 1 is buying. The `Platform` folder is deliberately narrow: it is where a host abstraction goes when no Core responsibility consumes it, not a general dumping ground.

### Decision 3: `ScanDocumentJson` gets its own `Serialization` folder

`ScanDocumentJson` is an `internal static` helper that writes and reads the shared `options` and measurement-mode JSON block, and it is used by both `ScanResultJsonWriter`/`ScanResultJsonReader` (Export) and `ScanSnapshotJsonWriter`/`ScanSnapshotJsonReader` (History). Filing it under either feature would make the other feature's folder depend on a helper it does not own.

A one-file folder is the cost of keeping that shared-plumbing status visible. The rejected alternative was leaving it in `Export` and letting `History` reach into it; because the type is `internal`, that compiles fine, but the layout would then misstate ownership. If more cross-feature JSON plumbing appears later it joins this folder rather than forcing another decision.

### Decision 4: Namespaces follow folders, with no compatibility shims

Each folder gets the matching file-scoped namespace, and consumers update their `using` directives to the specific namespaces they need. No type forwarders, no `global using MacStorageAtlas.Core.*`, and no namespace aliases.

The rejected alternative was keeping every type in the flat `MacStorageAtlas.Core` namespace and using folders for organization only. That produces a zero-churn diff, but it diverges from the App project's convention, leaves the folders cosmetic, and means the namespace still tells a reader nothing. A second rejected alternative was adding `global using` declarations for all Core namespaces in each consuming project, which would restore one-line imports but hide which parts of Core a file actually depends on — the opposite of what this change is for.

Consequence: the migration must remove now-unused imports, because `dotnet format MacStorageAtlas.slnx analyzers --diagnostics IDE0005 --verify-no-changes` is part of the required validation and will fail on a leftover `using MacStorageAtlas.Core;`.

### Decision 5: Tests mirror the Core folders one-to-one

`tests/MacStorageAtlas.Core.Tests` gains the same folder names with namespaces `MacStorageAtlas.Core.Tests.<Folder>`.

| Test folder | Test files |
| --- | --- |
| `Scanning` | `DiskScannerTests` |
| `Items` | `DiskItemTests`, `DiskItemSorterTests`, `FileCategoryMapTests`, `FileSizeFormatterTests` |
| `Filtering` | `BuiltInFilterPresetTests`, `DateCriterionTests`, `DiskItemFilterTests`, `DiskItemFilterEvaluatorTests`, `DiskItemFilterEvaluatorRelativeDateTests`, `DiskItemFilterRelativeValidationTests`, `FilteredResultViewsTests` |
| `Insights` | `FileTypeStatisticsServiceTests`, `LargeFilesServiceTests` |
| `Cleanup` | `CleanupBasketPlannerTests`, `CleanupPreflightValidatorTests`, `CleanupProtectedPathPolicyTests` |
| `Relocation` | `RelocationDestinationValidatorTests`, `RelocationPreflightValidatorTests` |
| `Export` | `ScanExportModelTests`, `ScanExportRequestFactoryTests`, `ScanExportRowSourceTests`, `ScanResultCsvWriterTests`, `ScanResultJsonWriterTests` |
| `History` | `FileSystemScanHistoryStoreTests`, `ScanHistoryRetentionPolicyTests`, `ScanSnapshotJsonTests`, `ScanSnapshotModelTests` |

`Access`, `Platform`, and `Serialization` get no mirrored test folder, because Core has no dedicated tests for those types today: Full Disk Access assessment is covered from the App test project, the reveal and Quick Look abstractions are interfaces with no Core logic, and `ScanDocumentJson` is exercised indirectly through the export and snapshot round-trip tests. Empty mirror folders are not created.

`ScanResultJsonWriterTests` covers both `ScanResultJsonWriter` and `ScanResultJsonReader` round-trips, so it stays a single file under `Export` rather than being split.

### Decision 6: Migrate folder by folder, keeping the solution buildable

The migration runs one folder at a time, in dependency order, rather than as one atomic rewrite: `Items`, then `Scanning`, then `Filtering`, `Insights`, `Serialization`, `Export`, `History`, `Cleanup`, `Relocation`, `Access`, `Platform`. After each folder the solution builds and the tests pass.

The alternative — moving all 97 files and then fixing the fallout — produces a compiler error avalanche in which a genuine mistake is invisible. Folder-at-a-time keeps each build failure attributable. It does mean intermediate states where Core has both a flat namespace and folder namespaces, which is fine because the C# compiler resolves both.

Use `git mv` for every move so the history stays a rename rather than a delete-plus-add, which keeps the diff reviewable and preserves `git log --follow`.

## Risks / Trade-offs

- **A stale `using MacStorageAtlas.Core;` survives in a consumer that no longer needs it** → `dotnet format MacStorageAtlas.slnx analyzers --diagnostics IDE0005 --verify-no-changes` is run as the final gate and is already part of the repository's required validation.
- **A namespace appears in a persisted format or in reflection and breaks silently** → audited before the move: Core uses `GetType().Name` only, in `ScanResultJsonWriter` for a date-criterion discriminator and in `DiskScanner` for an exception label, and neither writes a namespace. Snapshot and export schemas store no assembly-qualified names. `ScanSnapshotSchema` version constants are not touched.
- **The AXAML `xmlns:core="using:MacStorageAtlas.Core"` mapping in `MainWindow.axaml` breaks a compiled binding without an obvious error** → the mapping is updated in the same task group that moves the types it references, and the App build with compiled bindings is the check.
- **A 97-file move hides an accidental behavior edit inside a large diff** → moves use `git mv`, edits are restricted to the namespace line and `using` block, and `git diff -M` is reviewed for any hunk that touches a method body.
- **The chosen folder boundaries stop matching how Core grows, forcing another restructure** → the boundaries are taken from the capability specs already in `openspec/specs/`, and the new `core-project-structure` spec states the rule for placing a new type, so growth has a defined answer.
- **A one-file `Serialization` folder looks like over-engineering** → accepted, because the alternative misstates ownership of a helper that Export and History both use.
- **Trade-off: consumers now carry several `using` lines instead of one** → accepted, and it is the point: an import list that names `Cleanup` and `Relocation` documents what a file depends on.

## Migration Plan

1. Confirm a green baseline: `dotnet build`, `dotnet test`, and `dotnet format ... --verify-no-changes` all pass before any file moves.
2. For each folder in the Decision 6 order: `git mv` the Core files, update their namespace declarations, update the imports in every consuming file, and build plus test before moving on.
3. Update `MainWindow.axaml`'s `xmlns:core` mapping when the types it references move.
4. Mirror the same process in `MacStorageAtlas.Core.Tests`, folder by folder.
5. Run the full validation set from the repository root, including `openspec validate --all --strict --no-interactive`.
6. Review `AGENTS.md`, `CLAUDE.md`, `README.md`, `docs/`, and `docs/index.html` for statements about Core's layout and update those that describe a flat project.

Rollback is a `git revert` of the change's commits. Because no persisted format, package version, project reference, or solution entry changes, reverting restores the previous state exactly, and a snapshot or export written by either version is readable by the other.

## Open Questions

- None. The two decisions that were genuinely open — whether namespaces follow folders, and whether the change extends past Core — were settled during proposal: folder-matching namespaces, and Core plus `MacStorageAtlas.Core.Tests` only.
