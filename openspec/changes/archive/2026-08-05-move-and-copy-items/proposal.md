## Why

MacStorageAtlas can currently only reclaim local space by moving items to Trash,
which forces users to choose between keeping data on a full internal disk and
losing it. Roadmap item WP-08 closes that gap so users can archive large data to
an external or network volume and reclaim local space without deleting anything.
WP-07 has shipped the cleanup basket, protected-path policy, preflight
revalidation, and review dialog, so the safety machinery this feature depends on
already exists.

## What Changes

- Add Move to another location and Copy to another location as cleanup basket
  operations alongside the existing Move to Trash operation. The basket becomes
  operation-agnostic: the same collected items can be trashed, moved, or copied.
- Add a destination folder selection step that uses the existing native folder
  picker service.
- Extend preflight validation for relocation with destination-aware checks:
  destination missing, destination not writable, insufficient free space where
  the platform reports it, destination inside the source item, and an existing
  name collision at the destination.
- Block colliding destination names instead of overwriting. MacStorageAtlas
  never replaces an existing item at the destination.
- Extend the final review to state the operation type, the destination path, the
  item list, per-item readiness, and the expected locally reclaimed size, which
  is zero for copy operations.
- Execute relocation itemwise with cancellation between items and itemized
  success, failure, cancelled, and unattempted results, matching the Trash
  operation's reporting.
- Perform cross-volume moves as copy-then-verified-delete so a failed copy never
  removes the source.
- Update the displayed scan result only after a successful move confirms, and
  refresh shared-aware allocated results from the filesystem, matching the
  existing basket Trash behavior. A successful copy leaves the scan result
  unchanged.

## Capabilities

### New Capabilities

- `item-relocation`: Moving and copying reviewed cleanup basket items to a
  user-chosen destination folder, including destination preflight, collision
  blocking, cross-volume move semantics, itemized and cancellable execution, and
  scan-result consistency after relocation.

### Modified Capabilities

- `cleanup-basket`: The basket is no longer Trash-only. Review must identify
  which of the three operations is about to run and, for relocation, the chosen
  destination. Preflight readiness becomes operation-dependent, and the expected
  reclaimable total must reflect the selected operation.

## Impact

- `src/MacStorageAtlas.Core`: new relocation domain types (operation kind,
  destination plan, destination preflight status kinds, relocation service
  interface), extensions to `CleanupPreflightValidator`,
  `CleanupPreflightStatus`, `CleanupPreflightStatusKind`, and
  `CleanupBasketSummary`.
- `src/MacStorageAtlas.Platform.Mac`: a relocation service implementing
  same-volume move, cross-volume copy-then-verified-delete, and copy with
  metadata preserved where macOS supports it.
- `src/MacStorageAtlas.App`: `MainWindowViewModel` relocation commands and
  state, `CleanupBasketReview` gains operation and destination, the review
  service and its null implementation change shape, and `MainWindow.axaml` gains
  the two basket actions.
- `tests/`: Core preflight and planning tests, Platform.Mac relocation
  integration tests gated to macOS, and App ViewModel tests.
- Documentation: `README.md`, `docs/index.html`, and the WP-08 roadmap entry.
- No new package dependencies.

## Non-goals

- No replace or overwrite collision policy. The roadmap listed skip, rename, and
  replace; this change ships blocking only, so no destination item is ever
  replaced or auto-renamed. Rename and replace remain open for a later change.
- No relocation of the single selected result item outside the basket.
- No content verification by hashing after a cross-volume copy. Verification is
  limited to metadata the platform reports.
- No background, queued, or resumable transfers.
- No hardlink or APFS clone deduplication changes.

## Dependencies

- WP-07 cleanup basket, protected-path policy, preflight validator, and review
  dialog, all already implemented.
- `IFolderPickerService` for native destination selection.

## Risks

- Long-running copies of large directories block the basket operation. Mitigated
  by per-item progress, cancellation between items, and keeping filesystem work
  off the UI thread.
- Free-space reporting is unreliable for network volumes and cloud-backed
  destinations. Mitigated by treating an unavailable free-space figure as
  unknown rather than as a failure, and by never presenting an estimate as a
  guarantee.
- A cross-volume move that fails after copying leaves a partial copy at the
  destination. Mitigated by never deleting the source unless the copy verified,
  and by reporting the destination path of the partial result.

## Roadmap estimate

WP-08: 3-6 days.
