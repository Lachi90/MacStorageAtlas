# Tasks

## 1. Delta specs

- [x] 1.1 Write `specs/in-app-updates/spec.md`, splitting "Installation follows the standard user-approved update flow" into one modified and two added requirements, and verify each new scenario against `packaging/updater-bridge.m`, `packaging/update-metadata.py`, and `docs/TROUBLESHOOTING.md`
- [x] 1.2 Write `specs/release-packaging/spec.md`, splitting "The universal App Store package runs on both architectures" into one modified and three added requirements, and verify each new scenario against `docs/PACKAGING.md` and `build-dmg.sh`
- [x] 1.3 Write `specs/result-export/spec.md`, splitting its five over-long requirements into five modified and seven added requirements, and verify each new scenario against `tests/MacStorageAtlas.Core.Tests/Export/` and `MainWindowViewModel` export behavior
- [x] 1.4 Write `specs/result-filtering/spec.md`, splitting its two over-long requirements into two modified and five added requirements, and verify each new scenario against `tests/MacStorageAtlas.Core.Tests/Filtering/` and `tests/MacStorageAtlas.App.Tests/FilterPresetPersistenceTests.cs`
- [x] 1.5 Write `specs/scan-access-guidance/spec.md`, splitting "Full Disk Access guidance directs the user to macOS settings" into one modified and three added requirements, and verify each new scenario against `MainWindowViewModelAccessGuidanceTests` and `MacFullDiskAccessServiceTests`
- [x] 1.6 Write `specs/scan-history/spec.md`, splitting its three over-long requirements into three modified and four added requirements, and verify each new scenario against `MainWindowViewModelScanHistoryTests` and the Core history tests
- [x] 1.7 Write `specs/storage-measurement/spec.md`, splitting "Storage terms have canonical meanings" into one modified and three added requirements, and verify each new scenario against `docs/STORAGE_MEASUREMENT.md`

## 2. Change validation

- [x] 2.1 Verify that every modified requirement keeps its header and all scenarios verbatim and that its shortened description contains only sentences from the original description
- [x] 2.2 Verify that every requirement description in the change is at most 500 characters and that `openspec validate split-long-spec-requirements --type change --strict --no-interactive` passes
- [x] 2.3 Verify that `git status --porcelain -- openspec/specs` reports no change and that the in-flight changes `attribute-firmlinked-storage` and `harden-scan-traversal` are untouched
- [x] 2.4 Confirm that `README.md`, `docs/`, `docs/index.html`, and the roadmap status table need no update because no behavior, command, option, or limitation changes

## 3. Archive simulation

- [x] 3.1 Copy `openspec/` to a scratch directory, run `openspec archive split-long-spec-requirements --yes` there, and verify that the change is archived with all seven specs updated
- [x] 3.2 Verify that `openspec validate --all --strict --no-interactive` passes in the scratch copy for every main spec and for the remaining in-flight changes

## Workflow follow-up

- Archive the change in the repository after review with
  `openspec archive split-long-spec-requirements`.
- Run `openspec validate --all --strict --no-interactive` after the archive.
