## 1. Sandbox-Compatible Trash

- [x] 1.1 Replace the AppleScript Finder implementation in `MacTrashService` with `NSFileManager trashItemAtURL:resultingItemURL:error:` through Objective-C interop.
- [x] 1.2 Add an internal seam for the native Trash call so success, macOS failure, and missing-path behavior are unit-testable without touching the real Trash.
- [x] 1.3 Keep `MoveToTrashAsync` off the calling thread for the native call and observe the cancellation token before each item.
- [x] 1.4 Surface the `NSError` description as the reported failure reason for an item.

## 2. Sandbox-Compatible Finder Reveal

- [x] 2.1 Replace the `/usr/bin/open -R` implementation in `MacFileRevealService` with `NSWorkspace activateFileViewerSelectingURLs:` through Objective-C interop.
- [x] 2.2 Add an internal seam for the native reveal call so existing-item and missing-item behavior are unit-testable.

## 3. App Sandbox Detection

- [x] 3.1 Add an app-sandbox detection contract to `MacStorageAtlas.Core/Access`.
- [x] 3.2 Implement macOS detection in `MacStorageAtlas.Platform.Mac` using the sandbox container environment with a container-home fallback.
- [x] 3.3 Add a `SandboxRestricted` status to `FullDiskAccessStatus` and report it from `MacFullDiskAccessService` instead of probing when the process is sandboxed.
- [x] 3.4 Wire the detection through the composition root without introducing a service locator.

## 4. Sandbox-Aware Access Guidance

- [x] 4.1 Add a sandboxed guidance status and classify `SandboxRestricted` with inaccessible paths into it.
- [x] 4.2 Show no access guidance for a sandboxed build that completed without permission-related inaccessible paths.
- [x] 4.3 Add sandboxed guidance title and message text that directs the user to select the missing location and does not mention Full Disk Access.
- [x] 4.4 Hide the Full Disk Access settings action and the manual settings fallback in a sandboxed build, and keep the rescan action available.

## 5. Tests

- [x] 5.1 Replace the ignored-on-failure macOS Trash integration test with one that fails when the platform Trash API fails, keeping the non-macOS ignore.
- [x] 5.2 Add `MacTrashService` unit tests for success, macOS failure reason mapping, and missing paths.
- [x] 5.3 Add `MacFileRevealService` unit tests for reveal performed and missing item.
- [x] 5.4 Add sandbox detection tests for the sandboxed and unsandboxed cases.
- [x] 5.5 Add access-guidance classifier and ViewModel tests for sandboxed guidance, hidden settings action, available rescan, and no guidance without inaccessible paths.

## 6. App Store Submission Response

- [x] 6.1 Add `docs/APP_STORE_REVIEW_NOTES.md` with the App Review Information reply covering the app's purpose and audience, setup and access instructions, external services, regional consistency, regulated-industry status, and tested hardware and macOS versions.
- [x] 6.2 Document the screen-recording flow App Review requested, beginning with launching the app and covering the core features and every macOS access prompt.
- [x] 6.3 Record the sandbox behavior that the review notes rely on, including that scanning is limited to user-selected locations.

## 7. Documentation

- [x] 7.1 Update `docs/TROUBLESHOOTING.md` so Full Disk Access guidance applies to the Developer ID build and selection guidance applies to the Mac App Store build.
- [x] 7.2 Update `README.md` where it describes Full Disk Access, Trash, and Finder reveal behavior across both distribution channels.
- [x] 7.3 Update `docs/PACKAGING.md` with the App Store verification steps that must pass before an upload.
- [x] 7.4 Review `docs/FEATURES.md` and `docs/index.html` for claims the sandboxed build cannot deliver, and report when no update was necessary.

## 8. Validation

- [x] 8.1 Run `dotnet build MacStorageAtlas.slnx --no-restore`, `dotnet test MacStorageAtlas.slnx --no-build`, and `dotnet format MacStorageAtlas.slnx analyzers --diagnostics IDE0005 --verify-no-changes`.
- [x] 8.2 Run `openspec validate --all --strict --no-interactive`.
- [x] 8.3 Verify Trash and Finder reveal in an ad-hoc signed sandboxed bundle carrying the App Store entitlements.
