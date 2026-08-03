## Why

WP-06 is still missing the user-facing guidance that explains why a macOS scan may be incomplete when protected locations cannot be read. Users can see raw scan errors today, but they have to infer whether Full Disk Access is relevant, where to grant it, and what to do after changing the setting.

## What Changes

- Add a Full Disk Access guidance surface that appears when a scan has permission-related inaccessible paths or the access status is indeterminate.
- Distinguish an incomplete scan from purgeable space, free space, or space that is safe to delete.
- Show the inaccessible path count while keeping the normal scan errors view available as the detailed source of truth.
- Add a macOS settings action that opens Privacy & Security where Full Disk Access can be granted, with a documented manual fallback when the settings URL fails or changes.
- Explain that macOS requires the user to grant access manually and that the app may need to be restarted before a rescan sees the new permission.
- Allow the user to rescan the same root after changing access, reusing the existing scan lifecycle.
- Add conservative access-state classification for granted, likely missing access, indeterminate, and settings-open-failure cases without treating one readable probe path as proof that Full Disk Access is granted.
- Preserve existing scanner resilience: scans continue after recoverable filesystem errors and the app remains usable without Full Disk Access.

## Non-goals

- Automatically granting Full Disk Access or requesting an administrator password inside MacStorageAtlas.
- Changing macOS entitlements, enabling the App Sandbox, or adding privileged helpers.
- Reclassifying every `UnauthorizedAccessException` as a Full Disk Access problem.
- Hiding raw scan errors once guidance is shown.
- Changing scan measurement modes, hidden-file behavior, symbolic-link behavior, package expansion, cleanup behavior, export formats, or result filtering.
- Reading file contents, hashes, or protected app data to test access.
- Estimating the byte size of inaccessible paths.

## Capabilities

### New Capabilities

- `scan-access-guidance`: How MacStorageAtlas detects and presents likely incomplete scans caused by inaccessible paths; how it guides users to grant Full Disk Access manually; how settings failures and indeterminate access states are handled; and how guidance preserves privacy, scan errors, and existing scan semantics.

### Modified Capabilities

None. The scanner already records recoverable permission errors and this change interprets them for the macOS application without changing scan traversal, measurement, filtering, export, or metadata requirements.

## Impact

- `src/MacStorageAtlas.Core`: no expected scanner behavior change; may add small platform-neutral records or enums only if needed to keep App tests independent from macOS services.
- `src/MacStorageAtlas.Platform.Mac`: add a macOS service for access-status probing and opening Privacy & Security settings, including fallback behavior when the deep link cannot be opened.
- `src/MacStorageAtlas.App`: add ViewModel state, classification, commands, and Avalonia UI for the Full Disk Access guidance surface and rescan path.
- `tests/MacStorageAtlas.Tests`: add unit coverage for permission-error classification, ViewModel guidance state, settings-open success and failure, and rescan behavior after guidance is shown.
- `README.md`, `docs/FEATURES.md`, `docs/index.html`, and relevant documentation under `docs/`: review and update user-facing Full Disk Access guidance, limitations, and troubleshooting.

## Dependencies

- WP-04 result browsing and scan-error presentation are complete enough for the guidance surface to link back to detailed inaccessible paths.
- Existing scan lifecycle and recent-location rescan behavior are reused rather than replaced.
- macOS provides user-managed Full Disk Access in Privacy & Security, but does not provide a simple authoritative app-level status API that this project can treat as a single source of truth.

## Risks

- macOS settings deep links can change between releases. The platform service must report failure and documentation must include a manual navigation fallback.
- A readable probe path does not prove Full Disk Access is granted. Guidance must remain conservative and may report an indeterminate state.
- Some inaccessible paths are caused by ordinary Unix permissions, missing files, removable volumes, network failures, or transient IO errors rather than Full Disk Access. Classification must avoid overstating certainty.
- Permission prompts and protected locations can differ across macOS versions, Apple Silicon, Intel, signed, unsigned, launched-from-terminal, and launched-from-Finder builds.
- Adding guidance near scan completion can make users think inaccessible paths are purgeable or safe to delete. The UI and docs must keep those concepts separate.

## Estimate

5-9 days, matching the WP-06 roadmap estimate.
