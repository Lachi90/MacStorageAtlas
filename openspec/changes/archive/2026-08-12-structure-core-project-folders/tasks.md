## 1. Baseline and audit

- [x] 1.1 Confirm a clean starting point: `dotnet restore`, `dotnet build MacStorageAtlas.slnx --no-restore`, `dotnet test MacStorageAtlas.slnx --no-build`, and `dotnet format MacStorageAtlas.slnx analyzers --diagnostics IDE0005 --verify-no-changes` all pass before any file moves.
- [x] 1.2 Record the current Core test count from the baseline `dotnet test` run so the post-migration run can be compared against it.
- [x] 1.3 Audit Core for namespace-sensitive strings: confirm no persisted snapshot field, export column, JSON discriminator, or reflection call embeds a namespace or assembly-qualified type name, and note the `GetType().Name` uses in `ScanResultJsonWriter` and `DiskScanner` as namespace-independent.
- [x] 1.4 List every file that imports Core (`using MacStorageAtlas.Core;`) plus the `xmlns:core` mapping in `src/MacStorageAtlas.App/Views/MainWindow.axaml`, to serve as the checklist for import updates.

## 2. Move Core foundation folders

- [x] 2.1 `git mv` the 7 `Items` files into `src/MacStorageAtlas.Core/Items`, change each to `namespace MacStorageAtlas.Core.Items;`, and update every consumer's imports; build and test.
- [x] 2.2 `git mv` the 12 `Scanning` files into `src/MacStorageAtlas.Core/Scanning`, change each to `namespace MacStorageAtlas.Core.Scanning;`, and update every consumer's imports; build and test.
- [x] 2.3 Update the `xmlns:core` mapping in `src/MacStorageAtlas.App/Views/MainWindow.axaml` to the namespaces its referenced Core types now live in, and confirm compiled bindings still resolve at build time.

## 3. Move Core analysis folders

- [x] 3.1 `git mv` the 10 `Filtering` files into `src/MacStorageAtlas.Core/Filtering`, change each to `namespace MacStorageAtlas.Core.Filtering;`, and update every consumer's imports; build and test.
- [x] 3.2 `git mv` `FileTypeStatisticsService`, `FileTypeSummary`, and `LargeFilesService` into `src/MacStorageAtlas.Core/Insights`, change each to `namespace MacStorageAtlas.Core.Insights;`, and update every consumer's imports; build and test.

## 4. Move Core persistence folders

- [x] 4.1 `git mv` `ScanDocumentJson` into `src/MacStorageAtlas.Core/Serialization` with `namespace MacStorageAtlas.Core.Serialization;`, and update its Export and History callers; build and test.
- [x] 4.2 `git mv` the 13 `Export` files into `src/MacStorageAtlas.Core/Export`, change each to `namespace MacStorageAtlas.Core.Export;`, and update every consumer's imports; build and test.
- [x] 4.3 `git mv` the 16 `History` files into `src/MacStorageAtlas.Core/History`, change each to `namespace MacStorageAtlas.Core.History;`, and update every consumer's imports; build and test.
- [x] 4.4 Verify persisted-format stability: run the existing snapshot and export round-trip tests and confirm `ScanSnapshotSchema` version constants, JSON field names, and CSV column headers are byte-identical to the baseline.

## 5. Move Core action and host folders

- [x] 5.1 `git mv` the 20 `Cleanup` files, including `ICleanupFileSystemMetadataReader` and `ITrashService`, into `src/MacStorageAtlas.Core/Cleanup` with `namespace MacStorageAtlas.Core.Cleanup;`, and update every consumer's imports; build and test.
- [x] 5.2 `git mv` the 9 `Relocation` files, including `IItemRelocationService` and `IRelocationDestinationProbe`, into `src/MacStorageAtlas.Core/Relocation` with `namespace MacStorageAtlas.Core.Relocation;`, and update every consumer's imports; build and test.
- [x] 5.3 `git mv` the 4 `Access` files, including `IFullDiskAccessService`, into `src/MacStorageAtlas.Core/Access` with `namespace MacStorageAtlas.Core.Access;`, and update every consumer's imports; build and test.
- [x] 5.4 `git mv` `IFileRevealService` and `IQuickLookService` into `src/MacStorageAtlas.Core/Platform` with `namespace MacStorageAtlas.Core.Platform;`, and update every consumer's imports; build and test.
- [x] 5.5 Confirm `src/MacStorageAtlas.Core` now contains no source file in its root except `Properties/AssemblyInfo.cs`, and that the Core project file and its references are unchanged.

## 6. Mirror the layout in Core tests

- [x] 6.1 `git mv` `DiskScannerTests` into `tests/MacStorageAtlas.Core.Tests/Scanning` with `namespace MacStorageAtlas.Core.Tests.Scanning;`.
- [x] 6.2 `git mv` `DiskItemTests`, `DiskItemSorterTests`, `FileCategoryMapTests`, and `FileSizeFormatterTests` into `tests/MacStorageAtlas.Core.Tests/Items` with `namespace MacStorageAtlas.Core.Tests.Items;`.
- [x] 6.3 `git mv` `BuiltInFilterPresetTests`, `DateCriterionTests`, `DiskItemFilterTests`, `DiskItemFilterEvaluatorTests`, `DiskItemFilterEvaluatorRelativeDateTests`, `DiskItemFilterRelativeValidationTests`, and `FilteredResultViewsTests` into `tests/MacStorageAtlas.Core.Tests/Filtering` with `namespace MacStorageAtlas.Core.Tests.Filtering;`.
- [x] 6.4 `git mv` `FileTypeStatisticsServiceTests` and `LargeFilesServiceTests` into `tests/MacStorageAtlas.Core.Tests/Insights` with `namespace MacStorageAtlas.Core.Tests.Insights;`.
- [x] 6.5 `git mv` `ScanExportModelTests`, `ScanExportRequestFactoryTests`, `ScanExportRowSourceTests`, `ScanResultCsvWriterTests`, and `ScanResultJsonWriterTests` into `tests/MacStorageAtlas.Core.Tests/Export` with `namespace MacStorageAtlas.Core.Tests.Export;`.
- [x] 6.6 `git mv` `FileSystemScanHistoryStoreTests`, `ScanHistoryRetentionPolicyTests`, `ScanSnapshotJsonTests`, and `ScanSnapshotModelTests` into `tests/MacStorageAtlas.Core.Tests/History` with `namespace MacStorageAtlas.Core.Tests.History;`.
- [x] 6.7 `git mv` `CleanupBasketPlannerTests`, `CleanupPreflightValidatorTests`, and `CleanupProtectedPathPolicyTests` into `tests/MacStorageAtlas.Core.Tests/Cleanup` with `namespace MacStorageAtlas.Core.Tests.Cleanup;`.
- [x] 6.8 `git mv` `RelocationDestinationValidatorTests` and `RelocationPreflightValidatorTests` into `tests/MacStorageAtlas.Core.Tests/Relocation` with `namespace MacStorageAtlas.Core.Tests.Relocation;`.
- [x] 6.9 Confirm no empty mirrored folder was created for `Access`, `Platform`, or `Serialization`, and that `tests/MacStorageAtlas.Core.Tests` has no test file left in its root.

## 7. Verify the move changed nothing but structure

- [x] 7.1 Review `git diff -M --stat` and confirm every Core and Core-test file is recorded as a rename rather than a delete-plus-add.
- [x] 7.2 Review the full diff for any hunk touching a method body, member signature, attribute, or literal, and revert anything outside namespace declarations, `using` blocks, and the AXAML `xmlns` mapping.
- [x] 7.3 Confirm the Core test count and pass/skip breakdown match the task 1.2 baseline, and that platform-gated tests are still gated the same way.
- [x] 7.4 Confirm Core's public type names, kinds, accessibility, and members are unchanged, so the only public-surface difference is each type's namespace.

## 8. Documentation

- [x] 8.1 Update `AGENTS.md` and `CLAUDE.md` repository-structure guidance to describe Core's domain-folder layout and the folder-matching namespace convention, keeping the two files synchronized.
- [x] 8.2 Review `README.md`, `docs/` (including `docs/STORAGE_MEASUREMENT.md` and `docs/index.html`), and `openspec/config.yaml` for statements that describe Core as flat or that name a Core namespace, update those that changed, and report explicitly if no update was needed.
- [x] 8.3 Record the structure change in `docs/IMPLEMENTATION_ROADMAP.md` as engineering-structure work that advances no WP, matching how the archived `align-test-project-structure` change was recorded.

## 9. Validation

- [x] 9.1 Run `dotnet build MacStorageAtlas.slnx --no-restore` from the repository root and confirm it succeeds with no new warnings.
- [x] 9.2 Run `dotnet test MacStorageAtlas.slnx --no-build` and confirm every test project passes.
- [x] 9.3 Run `dotnet format MacStorageAtlas.slnx analyzers --diagnostics IDE0005 --verify-no-changes` and confirm no unused `using` directive remains anywhere in the solution.
- [x] 9.4 Run `git diff --check` and confirm no whitespace errors were introduced.
- [x] 9.5 Run `openspec validate --all --strict --no-interactive` and confirm the change and specs pass.
- [x] 9.6 Launch the app once on macOS and confirm a scan, a filter, an export, and the cleanup basket still work, verifying that the AXAML namespace remapping did not break a compiled binding at runtime.
