## Why

WP-03 still lacks the native macOS inspection flow that lets users preview a selected result before revealing it in Finder or moving it to Trash. Adding Quick Look and the matching keyboard shortcuts completes the item-inspection workflow without changing scan measurement, metadata capture, or cleanup semantics.

## What Changes

- Add a Quick Look action for the currently selected scan result item.
- Allow the selected item to be previewed from the folder tree, treemap, and largest-files result views.
- Bind Space to Quick Look when a scan result item is selected.
- Bind Command-I to show and focus the existing selected-item details surface for the current selection.
- Show a friendly status message when the selected path no longer exists or macOS cannot start Quick Look.
- Preserve existing Reveal in Finder, Move to Trash, selected-item metadata, and scan-result measurement behavior.

Non-goals:

- Embedding a Quick Look preview panel inside the Avalonia window.
- Opening files in their default applications.
- Capturing additional metadata, reading file contents in MacStorageAtlas, hashing files, or downloading cloud placeholders.
- Changing Trash confirmation, deletion safety, or post-Trash refresh behavior.
- Adding multi-selection preview or cleanup workflows.

Dependencies:

- WP-03 metadata details from `add-file-metadata` are complete and remain the selected-item information source.
- Existing selection behavior continues to expose one selected result item across the tree, treemap, and largest-files views.
- macOS Quick Look availability is treated as a platform integration behind an application-facing service.

Risks:

- Quick Look startup can fail for removed paths, unsupported file types, unavailable system services, or privacy-restricted locations.
- Keyboard handling can conflict with text input or existing control focus if bindings are too broad.
- Previewing dataless cloud placeholders can cause system-level materialization outside MacStorageAtlas control; the app must not perform its own file-content reads or downloads.
- Platform-specific Quick Look behavior can vary across supported macOS versions and architectures.

Roadmap estimate:

- This is the remaining Quick Look and shortcut portion of WP-03. Since metadata is already complete, this change should fit within the remaining portion of the original 3-5 day WP-03 estimate.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `file-metadata`: Extend selected-item inspection requirements to include Quick Look preview and native keyboard shortcuts for preview/details access.

## Impact

- `src/MacStorageAtlas.Core`: Add a small platform-neutral Quick Look service abstraction if needed for App command testing.
- `src/MacStorageAtlas.Platform.Mac`: Add macOS Quick Look launch behavior while preserving Apple Silicon and Intel support.
- `src/MacStorageAtlas.App`: Add Quick Look command wiring, Space and Command-I bindings, status messaging, and details focus/selection behavior.
- `tests/MacStorageAtlas.Tests`: Add ViewModel command enablement, success, missing-path, platform-failure, and shortcut/focus behavior coverage; add macOS platform service failure coverage.
- `docs/`, `README.md`, and `docs/index.html`: Review and update if user-visible shortcuts or feature descriptions change.
