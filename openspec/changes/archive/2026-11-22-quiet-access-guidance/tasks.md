## 1. Guidance

- [x] 1.1 Return no guidance from `AccessGuidanceClassifier` when the scan has no permission-related inaccessible paths, and remove the `Indeterminate` guidance state, its texts, and its completeness mapping.
- [x] 1.2 Update App tests so an inconclusive probe without blocked paths shows no guidance.

## 2. Probe

- [x] 2.1 Probe `/Library/Application Support/com.apple.TCC` before the per-user folders.
- [x] 2.2 Add a Platform.Mac test for the probe path list.

## 3. Documentation and validation

- [x] 3.1 Review `README.md`, `docs/FEATURES.md`, and `docs/index.html`; none describes the removed banner, so no update is needed.
- [x] 3.2 Run `dotnet build MacStorageAtlas.slnx --no-restore`.
- [x] 3.3 Run `dotnet test MacStorageAtlas.slnx --no-build`.
- [x] 3.4 Run `dotnet format MacStorageAtlas.slnx analyzers --diagnostics IDE0005 --verify-no-changes`.
- [x] 3.5 Run `openspec validate --all --strict --no-interactive`.
- [x] 3.6 Run `git diff --check`.
