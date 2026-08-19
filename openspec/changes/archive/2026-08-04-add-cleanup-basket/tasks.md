## 1. Core Cleanup Planning

- [x] 1.1 Add Core cleanup basket item, snapshot, add outcome, protection status, preflight status, summary, and operation result types
- [x] 1.2 Add a Core cleanup basket planner that adds, removes, and lists scanned items without cloning scan subtrees
- [x] 1.3 Add duplicate-path tests asserting a second add leaves the basket unchanged and returns an already-selected outcome
- [x] 1.4 Add parent-child overlap handling for descendant rejection and ancestor replacement
- [x] 1.5 Add overlap tests asserting descendants covered by an ancestor do not contribute separately to item count or totals
- [x] 1.6 Add logical, allocated, and shared-aware summary calculations for item count, total logical size, and expected uniquely reclaimable size
- [x] 1.7 Add summary tests for non-overlapping selections, overlapping directory selections, zero-byte shared entries, and mixed file and directory baskets

## 2. Protection and Preflight

- [x] 2.1 Add a Core protected-path policy covering the current scan root, macOS system locations, Trash locations, and paths outside the completed scan result
- [x] 2.2 Add protected-path tests for root blocking, system path blocking, Trash path blocking, outside-path blocking, and ordinary scanned user paths
- [x] 2.3 Add preflight revalidation that checks existence, protected status, available filesystem identity, and size immediately before execution
- [x] 2.4 Add preflight tests for missing items, replaced items, changed-size items, protected items, and executable unchanged items
- [x] 2.5 Ensure preflight and protection logic uses metadata only and does not read file contents, hash files, or materialize cloud placeholders
- [x] 2.6 Add privacy-boundary tests using test doubles that fail if cleanup planning opens file content streams

## 3. App Basket State and Commands

- [x] 3.1 Add cleanup basket state and summary presentation to `MainWindowViewModel` while preserving existing single-item Reveal in Finder, Quick Look, and Move to Trash command behavior
- [x] 3.2 Add selected-item add and remove commands for the folder tree, treemap, largest-files list, and filtered result presentations
- [x] 3.3 Add view-model tests showing explicit add and remove commands change the basket and do not alter the displayed scan result or filesystem
- [x] 3.4 Add view-model tests showing applying filters, changing selection, switching tabs, Quick Look, and Reveal in Finder do not populate the basket
- [x] 3.5 Clear basket contents when a completed scan result is replaced by rescan, scan option change, or selected-folder scan
- [x] 3.6 Add view-model tests for basket clearing after result replacement and for basket preservation across filter changes and result-tab switches

## 4. Review Flow

- [x] 4.1 Add an App review service abstraction and a null implementation for tests and design-time construction
- [x] 4.2 Add an Avalonia final review dialog showing Trash operation, item count, total logical size, expected uniquely reclaimable size, names, paths, and readiness status
- [x] 4.3 Add command flow that runs preflight before review and disables execution when no executable items remain
- [x] 4.4 Add view-model tests asserting cancelling review performs no Trash operation and leaves scan results unchanged
- [x] 4.5 Add view-model tests asserting missing, replaced, changed, or protected items are shown as blocked and are not sent to Trash
- [x] 4.6 Review user-facing review strings to ensure no item is described as safe to delete

## 5. Trash Execution and Partial Results

- [x] 5.1 Add basket Trash execution orchestration that calls the platform Trash service item by item with cancellation token propagation
- [x] 5.2 Add per-item progress and result reporting for succeeded, failed, cancelled, and unattempted items
- [x] 5.3 Add view-model tests for all-success execution, single-item failure, multiple failures, and cancellation after partial success
- [x] 5.4 Keep failed and unattempted items visible in the basket after execution and remove or mark successful items consistently
- [x] 5.5 Add tests asserting failed and unattempted items remain available for review after partial execution
- [x] 5.6 Add or update macOS Trash integration tests with isolated temporary files where practical, gated on supported platforms

## 6. Scan Result Reconciliation

- [x] 6.1 Reuse existing post-Trash result removal for successful basket items in logical and non-shared allocated results
- [x] 6.2 Trigger a scan refresh after any successful basket Trash operation in shared-aware allocated mode before presenting updated completed totals
- [x] 6.3 Add tests asserting logical results remove successful items while failed and unattempted items remain displayed
- [x] 6.4 Add tests asserting shared-aware results refresh after partial or full success and remain unchanged when no item succeeds
- [x] 6.5 Add tests asserting root-level basket cleanup is blocked by protected-path policy rather than clearing the current scan result

## 7. User Interface Integration

- [x] 7.1 Add cleanup basket controls to `MainWindow.axaml` using existing theme resources, compiled bindings where practical, and accessible names
- [x] 7.2 Add add-to-basket and remove-from-basket actions near existing result-view item actions without hiding Reveal in Finder or Quick Look
- [x] 7.3 Show basket item count, total logical size, expected reclaimable size, blocked status, stale status, and partial failure messages
- [x] 7.4 Add disabled and empty states for no scan result, empty basket, protected selected item, stale basket items, and execution in progress
- [x] 7.5 Verify the basket and review surfaces fit supported desktop window sizes without overlapping existing result controls

## 8. Documentation and Roadmap

- [x] 8.1 Review `README.md` and update it for the cleanup basket workflow, final review, and partial failure behavior if user-visible text is affected
- [x] 8.2 Review relevant documentation under `docs/` and update cleanup safety, roadmap, and feature descriptions where behavior changed
- [x] 8.3 Review and update `docs/index.html` for the multi-item safe cleanup capability if landing-page copy is affected
- [x] 8.4 Update the WP-07 row in `docs/IMPLEMENTATION_ROADMAP.md` status table when implementation is complete

## 9. Validation

- [x] 9.1 Run `dotnet build MacStorageAtlas.slnx --no-restore`
- [x] 9.2 Run `dotnet test MacStorageAtlas.slnx --no-build`
- [x] 9.3 Run `dotnet format MacStorageAtlas.slnx analyzers --diagnostics IDE0005 --verify-no-changes`
- [x] 9.4 Run `git diff --check`
- [x] 9.5 Run `openspec validate --all --strict --no-interactive`
