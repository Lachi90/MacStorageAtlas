## 1. Core Protection Policy

- [x] 1.1 Add a distinct cleanup protection reason for sensitive user locations.
- [x] 1.2 Extend `CleanupProtectedPathPolicy` to classify the current user home directory, standard user folder containers, and sensitive user Library subtrees using normalized paths and scan metadata only.
- [x] 1.3 Add Core tests for user home blocking, standard user folder container blocking, ordinary descendant eligibility, sensitive Library subtree prefix blocking, and existing scan-root/system/Trash/outside-result behavior.
- [x] 1.4 Confirm the policy does not read file contents, hash files, enumerate sensitive folders, or materialize cloud placeholders during classification.

## 2. Selected-Item Trash Flow

- [x] 2.1 Route the single selected-item Move to Trash command through `CleanupProtectedPathPolicy` before confirmation.
- [x] 2.2 Show the protected-path reason through the existing Trash status surface and skip confirmation plus platform Trash execution when the selected item is protected.
- [x] 2.3 Preserve existing confirmation, platform Trash execution, result reconciliation, error reporting, and shared-aware rescan behavior for eligible selected items.
- [x] 2.4 Add App view-model tests proving selected scan root and selected sensitive paths are blocked before confirmation and eligible selected files still follow the existing confirmation flow.
- [x] 2.5 Update tests that currently expect direct scan-root Trash to clear the completed scan result.

## 3. Cleanup Basket Integration

- [x] 3.1 Keep cleanup basket addition and preflight wired to the shared protected-path policy.
- [x] 3.2 Add or update basket tests covering the expanded sensitive-location rules and user-visible protected reasons.
- [x] 3.3 Verify blocked basket items remain visible with protected status and are not sent to `ITrashService`.

## 4. Documentation And Roadmap

- [x] 4.1 Review `README.md` for user-visible cleanup safety wording and update it if behavior changed.
- [x] 4.2 Review relevant documentation under `docs/`, including `docs/FEATURES.md`, `docs/IMPLEMENTATION_ROADMAP.md`, and `docs/index.html`, and update cleanup safety wording or roadmap status where needed.
- [x] 4.3 Report explicitly if any reviewed documentation did not require changes.

## 5. Validation

- [x] 5.1 Run `dotnet build MacStorageAtlas.slnx --no-restore`.
- [x] 5.2 Run `dotnet test MacStorageAtlas.slnx --no-build`.
- [x] 5.3 Run `dotnet format MacStorageAtlas.slnx analyzers --diagnostics IDE0005 --verify-no-changes`.
- [x] 5.4 Run `openspec validate --all --strict --no-interactive`.
- [x] 5.5 Run `git diff --check`.
