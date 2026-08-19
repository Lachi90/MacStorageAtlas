## 1. Core Metadata Model

- [x] 1.1 Add a Core metadata value model for scan-time item kind, timestamps, attributes, and explicit unknown values.
- [x] 1.2 Extend scan result items to retain metadata without changing existing size properties or child ownership.
- [x] 1.3 Add Core tests that verify metadata can be attached to file and directory results while existing size behavior remains unchanged.

## 2. Scanner Capture

- [x] 2.1 Capture file metadata during the existing scan visit for logical, allocated, and shared-aware allocated modes.
- [x] 2.2 Capture directory metadata during the existing scan visit without adding a second traversal.
- [x] 2.3 Preserve metadata snapshots after completion and avoid refreshing selected result metadata from the live filesystem.
- [x] 2.4 Report recoverable metadata failures as scan errors while allowing successfully measured siblings to remain in the result.
- [x] 2.5 Preserve cancellation behavior so partial results contain only metadata observed before cancellation.
- [x] 2.6 Add scanner tests for file metadata, directory metadata, unavailable metadata, recoverable metadata failures, cancellation, and measurement-mode preservation.

## 3. Platform Compatibility

- [x] 3.1 Decide whether stable .NET filesystem metadata is sufficient for the initial item kind and timestamp requirements.
- [x] 3.2 Add or extend Platform.Mac metadata support only if needed for macOS-specific behavior, preserving Apple Silicon and Intel compatibility.
- [x] 3.3 Add platform-gated tests for any macOS-specific metadata behavior introduced by this change.

## 4. App Presentation

- [x] 4.1 Add selected-item ViewModel properties for item kind and available metadata with explicit unknown formatting.
- [x] 4.2 Update the selected-item details UI to show metadata for tree, treemap, and largest-file selections.
- [x] 4.3 Ensure metadata display does not alter treemap, file-type, largest-file, progress, directory-total, or shared-size labels.
- [x] 4.4 Keep Trash and Reveal command behavior unchanged after metadata is displayed.
- [x] 4.5 Add ViewModel tests for metadata formatting, unknown values, selection source consistency, command enablement, and destructive-action safety.

## 5. Documentation And Roadmap

- [x] 5.1 Review `README.md`, `docs/`, and `docs/index.html` for user-visible metadata details or limitations and update them if needed.
- [x] 5.2 Update `docs/IMPLEMENTATION_ROADMAP.md` to reflect completion or remaining scope for the metadata portion of WP-03.
- [x] 5.3 Record that Quick Look remains out of scope for this change.

## 6. Validation

- [x] 6.1 Run `dotnet build MacStorageAtlas.slnx --no-restore`.
- [x] 6.2 Run `dotnet test MacStorageAtlas.slnx --no-build`.
- [x] 6.3 Run `dotnet format MacStorageAtlas.slnx analyzers --diagnostics IDE0005 --verify-no-changes`.
- [x] 6.4 Run `openspec validate --all --strict --no-interactive`.
- [x] 6.5 Run `git diff --check`.
