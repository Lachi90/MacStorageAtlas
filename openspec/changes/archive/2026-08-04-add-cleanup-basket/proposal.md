## Why

MacStorageAtlas can reveal, preview, or move one selected item to Trash, but users cannot gather several reviewed items into one deliberate cleanup operation. WP-07 in `docs/IMPLEMENTATION_ROADMAP.md` is the next safe-cleanup milestone because filtering and result browsing now provide the views users need before cleanup.

## What Changes

- Add a cleanup basket that lets users add and remove scanned items from the folder tree, treemap, largest-files list, and filtered results.
- Show basket item count, total logical size, expected uniquely reclaimable size, protected-item status, and stale or missing item status before any filesystem mutation.
- Prevent duplicate entries and parent/child overlap from overstating selected or reclaimable size.
- Block protected paths, including macOS system locations and the current scan root, with clear explanations.
- Add a final review dialog that lists the exact Trash operation and affected items before execution.
- Revalidate existence, file identity, and size immediately before moving items to Trash.
- Move approved items to macOS Trash through platform services, report partial failures per item, and update scan results only after confirmed success.

## Non-goals

- Permanent deletion.
- Moving or copying selected items to another destination. That remains WP-08.
- Automatic cleanup recommendations or marking large, old, filtered, duplicated, or categorized files as safe to delete.
- Acting on filtered results merely because a filter is applied.
- Duplicate detection, developer-storage insights, or vendor-specific cleanup actions.
- Persisting the cleanup basket across app restarts or completed rescans.

## Capabilities

### New Capabilities

- `cleanup-basket`: How MacStorageAtlas collects scanned items for a reviewed multi-item Trash operation, computes honest totals, blocks protected paths, revalidates items before mutation, handles cancellation and partial failures, and updates displayed scan results after successful changes.

### Modified Capabilities

None. Existing result-filtering, file-metadata, scan-access-guidance, and storage-measurement requirements continue to apply. Filtering remains factual and does not authorize cleanup; the basket adds a separate explicit review workflow.

## Impact

- `MacStorageAtlas.Core`: cleanup basket domain model, planning service, overlap detection, protected-path policy, preflight status model, and per-item execution result model with no UI dependencies.
- `MacStorageAtlas.App`: basket view model, add/remove commands from all result views, review dialog service, status presentation, command enablement, and scan-result reconciliation after successful Trash operations.
- `MacStorageAtlas.Platform.Mac`: possible extension of Trash service behavior for repeated cancellable Trash operations while preserving recoverable macOS Trash semantics.
- `MacStorageAtlas.Rendering`: unchanged.
- `MacStorageAtlas.Tests`: unit tests for basket planning, overlap, totals, protected paths, stale checks, cancellation, partial failures, and view-model wiring from each result view.
- Documentation: `README.md`, relevant docs under `docs/`, and `docs/index.html` need review and updates for the new multi-item cleanup workflow.

## Dependencies

- WP-03 item inspection and metadata are complete and provide scan-time metadata used for review and revalidation.
- WP-04 filtering is complete and provides filtered result sets that can feed explicit basket actions.
- Existing Trash, Finder reveal, Quick Look, and shared-aware post-Trash refresh behavior must remain intact.

## Risks

- Parent/child overlap can overstate reclaimed space if the basket stores independent selections without planning normalization.
- Shared-aware allocated results can become misleading after Trash unless the app refreshes accounting before showing a completed result.
- Files can be deleted, renamed, modified, replaced, or moved between scan time, basket addition, review, and execution.
- Bulk Trash operations can partially fail and must leave the scan model consistent with confirmed filesystem changes.
- Protected-path policy may need careful macOS compatibility handling across user homes, mounted volumes, system paths, and Intel or Apple Silicon installations.

## Roadmap Estimate

WP-07 is estimated at 6-10 days in `docs/IMPLEMENTATION_ROADMAP.md`.
