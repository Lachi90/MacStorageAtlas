## 1. Access Model and Classification

- [x] 1.1 Define the access-guidance state model needed by the App layer, including no-guidance, incomplete-scan, likely Full Disk Access issue, indeterminate, and settings-open-failure states.
- [x] 1.2 Implement a classifier that derives guidance from the current scan root, completed scan errors, and platform access status without changing `DiskScanner` behavior.
- [x] 1.3 Add classifier tests for no errors, permission-related inaccessible paths, non-permission IO errors, mixed recoverable errors, and one-readable-probe-does-not-prove-granted behavior.
- [x] 1.4 Verify existing scan-error tests still prove raw `UnauthorizedAccessException` and `IOException` values flow through unchanged.

## 2. macOS Access Service

- [x] 2.1 Run a short compatibility spike for the exact macOS Privacy & Security settings URL and record the chosen direct URL plus fallback path in the design or documentation.
- [x] 2.2 Add an application-facing access service abstraction that can report conservative access status and open Full Disk Access settings.
- [x] 2.3 Implement the macOS access service in `MacStorageAtlas.Platform.Mac` using documented, non-private mechanisms and metadata/enumeration-only probes.
- [x] 2.4 Add deterministic tests for granted, likely missing, indeterminate, settings-open success, and settings-open failure outcomes through test doubles.
- [x] 2.5 Ensure the platform service never reads file contents, hashes files, requests administrator credentials, or queries private TCC databases.

## 3. ViewModel Guidance Flow

- [x] 3.1 Inject the access service through App composition and ViewModel constructors while preserving design-time and test defaults.
- [x] 3.2 Update scan completion handling to publish access guidance for the completed scan without changing scan totals, measurement mode, filters, selected metadata, or scan errors.
- [x] 3.3 Add commands for opening Full Disk Access settings and rescanning the completed root from the guidance surface.
- [x] 3.4 Reuse the existing scan lifecycle for guidance-triggered rescans, including scan options, cancellation, progress, and one-scan-at-a-time protection.
- [x] 3.5 Add ViewModel tests for guidance visibility, inaccessible-path counts, settings-open success and failure messages, manual fallback text state, rescan command state, and cancellation behavior.

## 4. Avalonia UI

- [x] 4.1 Add a concise Full Disk Access guidance surface that appears with completed-scan results when guidance is active.
- [x] 4.2 Show the inaccessible-path count, incomplete-scan explanation, manual grant/restart guidance, Open Settings action, and Rescan action.
- [x] 4.3 Keep the existing Scan errors tab visible and copy-path behavior available whenever guidance is shown.
- [x] 4.4 Ensure guidance text distinguishes inaccessible paths from purgeable, free, available, and safe-to-delete space.
- [x] 4.5 Verify the guidance surface fits cleanly at supported desktop window sizes without overlapping existing result controls.

## 5. Documentation and Roadmap

- [x] 5.1 Update `README.md`, `docs/FEATURES.md`, and `docs/index.html` for user-visible Full Disk Access guidance and limitations.
- [x] 5.2 Add or update troubleshooting documentation with manual navigation to Privacy & Security > Full Disk Access and the restart/rescan expectation.
- [x] 5.3 Update WP-06 status in `docs/IMPLEMENTATION_ROADMAP.md` when implementation is complete.
- [x] 5.4 Confirm no changes are needed to `docs/STORAGE_MEASUREMENT.md`, or update it if guidance text introduces any new storage terminology.

## 6. Validation

- [x] 6.1 Manually test on a clean macOS user account without Full Disk Access, then grant access, restart if needed, and rescan the same root.
- [x] 6.2 Manually test settings-open fallback behavior on supported macOS versions where practical.
- [x] 6.3 Run `dotnet build MacStorageAtlas.slnx --no-restore`.
- [x] 6.4 Run `dotnet test MacStorageAtlas.slnx --no-build`.
- [x] 6.5 Run `dotnet format MacStorageAtlas.slnx analyzers --diagnostics IDE0005 --verify-no-changes`.
- [x] 6.6 Run `openspec validate --all --strict --no-interactive`.
- [x] 6.7 Run `git diff --check`.
