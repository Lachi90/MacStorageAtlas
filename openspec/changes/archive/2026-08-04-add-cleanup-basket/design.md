## Context

MacStorageAtlas currently exposes one selected scan result across the folder tree, treemap, and largest-files views. A selected item can be revealed in Finder, previewed with Quick Look, or moved to Trash after a per-item confirmation. The single-item Trash flow lives in `MainWindowViewModel`, calls `ITrashService.MoveToTrashAsync`, and updates the scan result only after the platform service succeeds.

WP-07 expands this from one selected item to one reviewed multi-item cleanup operation. The change crosses Core, App, Platform.Mac, and Tests because selection planning, review, revalidation, execution, and result reconciliation must stay separate. Rendering remains unchanged.

The strongest constraints are safety and honesty: filtered results are factual matches only, cleanup remains recoverable through macOS Trash, protected paths cannot be accidentally selected, path and identity must be revalidated immediately before mutation, and shared-aware allocated results must not show misleading post-Trash totals.

## Goals / Non-Goals

**Goals:**

- Let users collect scanned items into a cleanup basket from every existing result view.
- Keep basket planning in Core without Avalonia or macOS UI dependencies.
- Prevent duplicate paths and parent/child overlap from overstating item counts or expected reclaimable bytes.
- Classify protected paths before basket addition and again before execution.
- Present a final App-owned review dialog before any Trash operation.
- Execute a reviewed Trash plan through platform services with cancellation and per-item failure reporting.
- Reconcile the displayed scan result only after each successful platform mutation.
- Preserve existing single-item Reveal in Finder, Quick Look, filtering, export, measurement, and Trash safety behavior.

**Non-Goals:**

- Permanent deletion.
- Move or copy to another destination.
- Automatic selection based on filters or metadata.
- Duplicate detection or developer-storage cleanup insights.
- Persisting basket contents after app restart or after a completed rescan.
- Adding UI dependencies to Core or platform dependencies to Rendering.

## Decisions

### Add a Core cleanup planning model

Core will own a small cleanup model around scanned item references, path identity snapshots, protection status, totals, preflight status, and per-item operation results. App view models will pass selected `DiskItem` instances into this model and bind to the resulting plan summaries.

Alternative considered: keep the basket entirely in `MainWindowViewModel` as a list of selected `DiskItem` instances. That would be faster to wire, but it would put overlap, protected-path, and total-calculation rules inside App, making those safety rules harder to test and reuse for WP-08.

### Normalize the plan instead of allowing overlapping active entries

When a user attempts to add an item that overlaps an existing basket entry, the planner will produce a deterministic outcome before the basket accepts the item. The preferred behavior is:

- Adding an exact duplicate leaves the basket unchanged and reports that the item is already selected.
- Adding a descendant of an already selected directory is rejected with an explanation.
- Adding a directory that contains existing basket entries replaces those descendant entries after explicit user intent from the add action, with messaging that the broader directory now covers them.

This preserves honest totals while keeping common cleanup work efficient. The implementation can expose the planner outcome to App so the UI can show a concise status message.

Alternative considered: allow overlapping entries and compute unique totals only in the summary. That keeps all selected rows visible, but it makes review confusing because the list contains items that do not independently contribute to the operation.

### Use logical size and expected uniquely reclaimable size as distinct values

The basket summary will show total logical size for selected planned items and expected uniquely reclaimable size based on the scan's current measurement and accounting mode. In logical mode, unique reclaimable size tracks logical bytes after overlap normalization. In allocated mode, it tracks measured allocated bytes after overlap normalization. In shared-aware allocated mode, it remains an estimate and successful Trash requires a rescan before the app presents updated completed totals.

Alternative considered: show only one total. That would be simpler, but the roadmap explicitly calls out both total logical size and expected uniquely reclaimable size, and the app already preserves a distinction between logical length and measured local allocation.

### Revalidate before execution with conservative stale handling

The review flow will run a preflight immediately before enabling execution. Each planned item must still exist, must still match the scan-time identity where identity data is available, and must not have changed size materially from the scan snapshot. Missing, changed, or identity-mismatched items remain visible in the review but are blocked from execution until removed or the user rescans and adds them again.

Alternative considered: attempt Trash and rely on platform errors. That catches missing paths, but it does not catch path replacement or size drift before the user reviews the final operation.

### Keep Trash execution itemized

The platform boundary will still move individual paths to Trash through `ITrashService`. App orchestration can call the service once per executable plan item, collect per-item success or failure, and stop promptly on cancellation. A future implementation can add a batch method only if it preserves per-item reporting and cancellation semantics.

Alternative considered: add a batch Trash API immediately. That could reduce process launches, but Finder or AppleScript failure attribution can become opaque. Itemized execution is easier to test and keeps partial failure reporting exact.

### Refresh after multi-item Trash when accounting requires it

For logical and non-shared allocated results, successful items can be removed from the in-memory scan tree after the platform service confirms success. For shared-aware allocated results, any successful Trash operation will trigger a scan refresh of the original root before the app presents completed updated totals. Failed and cancelled items stay in the displayed result until a later rescan says otherwise.

Alternative considered: remove every successful item from the current tree regardless of measurement mode. That would preserve apparent responsiveness but can understate remaining shared allocations.

### Keep protected-path policy local and conservative

Core will expose a protected-path classifier with reasons for system roots, critical macOS directories, Trash locations, the active scan root, and any path outside the current scan result. The policy will compare normalized full paths without reading file contents. App will surface the reason where the user attempts to add a blocked item or reviews a stale basket entry.

Alternative considered: rely on macOS permission failures. That is too late in the workflow and would make protected-path behavior dependent on process entitlements and launch context.

### UI surface: basket panel plus final dialog

App will add visible basket controls near the existing item actions and a review surface that is separate from filtering. Existing result views get explicit add/remove basket commands. Applying a filter alone will not add items to the basket. The final review dialog will list operation type, path, displayed name, size, status, and failures.

Alternative considered: overload `Move to Trash` to act on the basket when non-empty. That increases accidental-action risk because the same command would switch scope from one selected item to many items based on hidden state.

## Risks / Trade-offs

- Path replacement between scan and Trash could move the wrong item -> Capture scan-time identity when available and block mismatches during preflight.
- Shared-aware allocated totals can be wrong after partial Trash success -> Refresh the original scan root after any successful Trash in shared-aware mode before presenting completed totals.
- Multi-item Trash can be slow because each item uses platform services separately -> Keep execution cancellable, report progress, and only consider batch service optimization after correctness is covered.
- Protected-path rules can be too strict for expert users -> Prefer conservative blocking for WP-07; users can still reveal items in Finder and act outside the app.
- Basket contents can become stale after rescan or scan option changes -> Clear the basket when a completed result is replaced.
- Large baskets could duplicate scan tree data if they store full result copies -> Store references and snapshots only, not cloned subtrees.
- AppleScript Trash behavior may vary across macOS versions -> Continue using recoverable Finder Trash behavior and keep integration tests focused on temporary files where practical.

## Migration Plan

1. Add Core planning types and tests without changing the existing single-item UI.
2. Add App basket view-model state and command wiring while preserving current selection commands.
3. Add final review and execution orchestration behind new services.
4. Reconcile scan updates and shared-aware refresh behavior.
5. Update documentation and roadmap status.

Rollback can remove the cleanup basket Core types, App view model state, review services, and UI bindings while leaving the existing single-item Trash service and confirmation flow intact.

## Open Questions

- Which exact macOS paths should be blocked in the first protected-path policy beyond system roots, Trash folders, and the active scan root?
- Should broad directory replacement of descendant entries require an additional mini-confirmation, or is the final review sufficient?
- Should a reviewed item with a changed but still same identity and smaller size be blocked, or allowed with an updated warning?
- Should the existing single-item `Move to Trash` command remain visible beside basket actions, or should all Trash operations route through the basket review after this change?
