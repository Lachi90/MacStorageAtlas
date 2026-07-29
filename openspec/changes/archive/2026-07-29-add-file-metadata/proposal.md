## Why

MacStorageAtlas currently shows size and path details for selected results, but it does not preserve basic filesystem metadata that helps users understand what an item is before taking action. This change implements the file metadata portion of WP-03 so details reflect the scan snapshot rather than a later query against a possibly changed filesystem.

## What Changes

- Add scan-result metadata for files and directories, including modification time, creation time where available, last-access time only when reported reliably enough to display, filesystem attributes needed for presentation, and item kind.
- Capture metadata during the existing scan visit and retain it on each included `DiskItem`.
- Display available metadata in the selected-item details view for items selected from the tree, treemap, or largest-files list.
- Show unavailable metadata as unknown instead of fabricating dates or default values.
- Treat recoverable metadata read failures consistently with existing scan errors and keep successfully measured siblings in the result.
- Preserve existing logical, allocated, and shared-aware size semantics.

Non-goals:

- Quick Look preview, Space handling, and Command-I shortcuts.
- Duplicate detection, content hashing, or safety recommendations.
- Exporting metadata outside the local application.
- Replacing Trash-based cleanup or changing destructive-action confirmation.

Dependencies:

- WP-02 storage measurement and shared-aware accounting semantics remain the size source of truth.
- Existing scan traversal and progress behavior remain the integration point.

Risks:

- Additional metadata reads can slow large scans if they introduce extra filesystem calls.
- macOS date availability and reliability can vary by filesystem, mounted volume, and user privacy settings.
- Selected item details can become misleading if metadata is refreshed after scan completion, so the result should present scan-time metadata.

Roadmap estimate:

- WP-03 estimates 3-5 days for Quick Look and file metadata together. This narrower metadata-only change is expected to fit within the lower portion of that range, excluding the later Quick Look work.

## Capabilities

### New Capabilities

- `file-metadata`: Captures scan-time filesystem metadata on result items and displays available metadata in item details.

### Modified Capabilities

- None.

## Impact

- `src/MacStorageAtlas.Core`: Extend scan result item modeling and scanner metadata capture without adding UI or platform dependencies.
- `src/MacStorageAtlas.Platform.Mac`: Expose macOS-specific metadata where needed while preserving Apple Silicon and Intel support.
- `src/MacStorageAtlas.App`: Add selected-item metadata properties, formatting, and AXAML bindings.
- `tests/MacStorageAtlas.Tests`: Add coverage for metadata capture, unavailable metadata, recoverable failures, formatting, UI selection behavior, and scan semantics preservation.
- `docs/`, `README.md`, and `docs/index.html`: Review and update only if user-visible details or limitations change.
