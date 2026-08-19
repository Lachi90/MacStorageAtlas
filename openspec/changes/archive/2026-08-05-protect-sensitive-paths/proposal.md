## Why

WP-07 added a conservative protected-path policy for cleanup basket operations, but the existing single-item Move to Trash command can still move the current scan root or other sensitive paths after only a confirmation. This change closes that safety gap so all in-app cleanup actions share the same protected-path boundary before filesystem mutation.

## What Changes

- Apply protected-path classification to every MacStorageAtlas-initiated Trash cleanup path, including the single selected-item Move to Trash command and cleanup basket execution.
- Expand the protected-path model from "basket cleanup only" to a shared cleanup safety contract with user-visible reasons.
- Block broad or sensitive containers from in-app cleanup, including the active scan root, macOS system locations, Trash locations, paths outside the completed scan result, the user home directory, broad user library containers, and broad user media/data folders when selected as containers.
- Preserve inspection workflows: scanning, filtering, export, Reveal in Finder, Quick Look, and scan access guidance remain unchanged.
- Preserve recoverability: allowed cleanup still moves items to macOS Trash and never permanently deletes files.
- Update documentation where user-visible cleanup safety behavior changes.

Non-goals:

- Permanent deletion.
- Hiding sensitive paths from scan results.
- Preventing users from acting on files outside MacStorageAtlas through Finder or Terminal.
- Reading file contents, hashing files, or probing protected app data to decide cleanup safety.
- Adding a force-delete, expert override, or allow-list UI in this change.

Dependencies:

- WP-07 cleanup basket and protected-path policy are already complete.
- No new package or platform dependency is expected.

Risks:

- Conservative blocking may reject cleanup of a folder an expert user intentionally selected.
- A path allow/block list can become stale as macOS changes protected locations.
- Blocking standard user folders as containers must not prevent cleanup of ordinary descendant files found during a scan.

Roadmap estimate:

- WP-07 follow-up; expected size is smaller than the original WP-07 estimate because the cleanup basket foundation already exists.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `cleanup-basket`: Broaden protected-path cleanup requirements so sensitive paths are blocked consistently across single-item Trash and cleanup basket workflows.

## Impact

- `MacStorageAtlas.Core`: protected-path reason model and classifier rules.
- `MacStorageAtlas.App`: selected-item Trash command gating, status messaging, and command refresh behavior.
- `MacStorageAtlas.Platform.Mac`: no expected behavior change; platform Trash service remains the recoverable mutation boundary.
- `tests/MacStorageAtlas.Core.Tests`: classifier coverage for sensitive user containers and ordinary descendants.
- `tests/MacStorageAtlas.App.Tests`: single-item Trash blocking tests and updates to existing scan-root Trash expectations.
- `README.md`, relevant files under `docs/`, and `docs/index.html`: review for user-visible cleanup safety wording.
