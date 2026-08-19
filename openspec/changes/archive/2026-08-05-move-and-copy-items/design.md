## Context

WP-07 shipped the cleanup basket with a single terminal operation: move to
Trash. The pieces it introduced — `CleanupBasketPlanner` for overlap-free item
collection, `CleanupProtectedPathPolicy` for path classification,
`CleanupPreflightValidator` for immediate-before-execution revalidation,
`CleanupBasketReview` plus `ICleanupBasketReviewService` for the confirmation
dialog, and the itemized `CleanupOperationItemResult` execution loop in
`MainWindowViewModel` — are all operation-agnostic in substance but Trash-shaped
in naming and in a few hardcoded strings.

WP-08 adds two more terminal operations on the same basket: move to a chosen
destination and copy to a chosen destination. The constraint is that the safety
guarantees WP-07 established must hold identically for relocation. The
constraint that shapes the platform layer is macOS-specific: a move within one
volume is a rename, a move across volumes is a copy followed by a delete, and
the delete must never run against an unverified copy.

`IFolderPickerService` already exists in the App layer for selecting scan roots
and can be reused for destination selection without change.

## Goals / Non-Goals

**Goals:**

- Make the cleanup basket carry three operations while keeping one review path,
  one preflight path, and one itemized execution path.
- Guarantee that a failed or cancelled transfer never removes a source item.
- Guarantee that MacStorageAtlas never replaces, merges into, or auto-renames an
  item at the destination.
- Keep all relocation planning and validation in Core with no UI or platform
  types, and keep the actual filesystem transfer behind a platform service
  interface.
- Keep the scan-result update rule unchanged: mutate the displayed result only
  after the platform service confirms success.

**Non-Goals:**

- Rename and replace collision policies. Blocking is the only policy in this
  change.
- Content hashing to verify a cross-volume copy.
- Sub-item progress inside a single large directory transfer.
- Background, queued, or resumable transfers.
- Relocating the single selected result item outside the basket.
- Preserving extended attributes and resource forks beyond what the chosen macOS
  copy primitive already preserves.

## Decisions

### Operation kind is a Core enum, and the basket stays operation-free

Add `CleanupOperationKind { Trash, Move, Copy }` to Core. The planner and the
basket item list stay unaware of it; the operation is a parameter to preflight,
review, and execution only. Switching operation therefore cannot change basket
membership, which is what the spec requires.

*Alternative considered:* modelling three separate basket types or a basket that
stores its intended operation. Rejected because it would let a user assemble a
basket for one operation and lose it when switching, and because it would
duplicate `CleanupBasketPlanner`'s overlap logic per operation.

### Relocation preflight composes the Trash validator rather than replacing it

Add `RelocationPreflightValidator` in Core that takes the existing
`CleanupPreflightValidator` plus a destination and runs the source checks first,
then the destination checks. The Trash path keeps calling
`CleanupPreflightValidator` directly and is untouched.

`CleanupPreflightStatusKind` gains `DestinationCollision`,
`DestinationInsideSource`, and `AlreadyAtDestination`. `CleanupPreflightStatus`
gains `ReadyToMove` and `ReadyToCopy` alongside the existing `Ready`, because
`Ready`'s message is the literal string `"Ready to move to Trash."` and would
be wrong on a relocation review.

*Alternative considered:* generalizing `CleanupPreflightValidator` with an
operation parameter. Rejected because it would push destination knowledge into
the Trash path and force every existing Core test to pass a destination.

### Destination-level failures are separate from per-item failures

Destination missing, destination not a directory, destination not writable, and
insufficient free space are properties of the operation, not of individual
items, so they produce a `RelocationDestinationValidation` result that blocks the
whole operation before per-item preflight runs. Collision, destination-inside-
source, and already-at-destination are per-item and flow through the per-item
preflight status so they render in the same review list as missing and changed
items.

Free space comes from `DriveInfo.AvailableFreeSpace` for the destination root.
Any exception, or a volume that reports zero while clearly writable, is treated
as unknown: the review shows free space as unknown and does not block. Network
and cloud-backed volumes are the reason for this; blocking on an unreliable
figure would be worse than not checking.

### Path comparisons reuse the existing normalization

`CleanupProtectedPathPolicy.NormalizePath` and the ordinal prefix comparison
already used for parent-child overlap in `CleanupBasketPlanner` are reused for
destination-inside-source and already-at-destination. This keeps relocation
consistent with basket overlap detection on case-sensitive and case-insensitive
APFS volumes alike, rather than introducing a second, subtly different notion of
containment.

Collision detection is a `File.Exists` or `Directory.Exists` check on the
candidate destination path, performed during preflight immediately before
execution. A collision created between preflight and the transfer is caught
again by the platform service, which never overwrites.

### Platform transfer shells out to `/bin/cp` for copies and uses rename for same-volume moves

Add `IItemRelocationService` to Core with `MoveAsync` and `CopyAsync`, and
`MacItemRelocationService` to Platform.Mac.

- **Copy** invokes `/bin/cp -Rpc` through `Process`, matching the pattern
  `MacTrashService` already uses for `osascript`. `-p` preserves mode,
  timestamps, ACLs, and extended attributes, which .NET's `File.Copy` does not.
  `-c` requests an APFS clone and falls back to a byte copy when cloning is not
  possible.
- **Move** calls `File.Move` or `Directory.Move` first. On Unix `File.Move`
  already falls back to copy-and-delete across devices; `Directory.Move` throws
  on a cross-device rename. When `Directory.Move` fails that way,
  `MacItemRelocationService` performs the copy, verifies it, and only then
  removes the source.

Verification before source removal compares recursive entry count and total byte
length between source and destination. No hashing, per the non-goals.

*Alternative considered:* a pure managed recursive copy. Rejected because it
silently drops extended attributes and resource forks and cannot clone on APFS,
making a same-volume copy of a large folder consume full space and take minutes
instead of being near-instant.

*Consequence to document:* an APFS clone copy on the same volume consumes almost
no additional space at creation time. The copy review already reports zero
locally reclaimed space, so nothing in the review is wrong, but
`docs/STORAGE_MEASUREMENT.md` should note that a same-volume copy may not
increase used space the way a naive expectation suggests.

### Cross-volume behaviour is testable through a seam

Cross-volume transfer is the highest-risk path and the hardest to exercise.
`MacItemRelocationService` takes the fast-rename primitive as an injectable
delegate defaulting to `Directory.Move`, so the fallback path, its verification,
and its refusal to delete an unverified source are unit-testable on any machine
with temporary directories. A macOS-gated integration test additionally creates a
RAM disk with `hdiutil` and `diskutil` to exercise the real cross-device path,
and is ignored with a clear reason when the RAM disk cannot be created.

*Alternative considered:* only integration-testing cross-volume behaviour.
Rejected because a test that silently ignores on CI leaves the
never-delete-an-unverified-source guarantee unverified.

### One busy flag and one execution loop in the ViewModel

`IsMovingCleanupBasketToTrash` is renamed to `IsRunningCleanupBasketOperation`
and `ExecuteCleanupBasketTrashAsync` is generalized to take a transfer callback,
so Trash, move, and copy share the cancellation token source, the unattempted
and cancelled bookkeeping, and `CleanupBasketOperationResults`. Two new commands
`MoveCleanupBasketToLocationCommand` and `CopyCleanupBasketToLocationCommand`
differ only in the operation kind they pass.

The rename touches `MainWindow.axaml` and existing App tests. That churn is
accepted over adding a second and third busy flag, which would make the three
mutually exclusive operations independently startable.

`CleanupBasketReview` gains `Operation` and a nullable `Destination`.
`ICleanupBasketReviewService.ConfirmCleanupAsync` keeps its signature because the
review record now carries everything the dialog needs.

### Scan-result reconciliation reuses the existing rule, keyed on operation

A successful move reuses `ReconcileCleanupBasketSuccessesAsync` unchanged: remove
succeeded items from the basket, then either drop them from the displayed tree or
rescan when the result is shared-aware allocated. A successful copy skips
reconciliation entirely and leaves both the basket and the displayed result
alone, because nothing left the scanned scope.

### Per-item progress is per item, not per byte

The relocation loop reports the current item name and a completed count, and
checks cancellation between items. `/bin/cp` for a single huge directory is not
interruptible mid-item; cancelling during it takes effect when that item
finishes. The spec wording — "stops relocating additional items as soon as
practical" — is written to match this honestly rather than implying byte-level
cancellation the implementation does not provide.

## Risks / Trade-offs

- **A cross-volume move fails after copying and leaves a partial copy at the
  destination** → The source is never removed unless verification passed, and
  the failure result reports the destination path of the partial result so the
  user can remove it. MacStorageAtlas does not delete the partial copy itself,
  because deleting at a user-chosen destination is not something a failed
  operation should do unprompted.
- **A large directory transfer makes the operation feel unresponsive** → Work
  runs off the UI thread through the existing dispatcher abstraction, progress
  names the current item, and cancellation is offered continuously even though
  it lands between items.
- **`DriveInfo.AvailableFreeSpace` is wrong or unavailable on network and
  cloud-backed volumes** → Unknown is a first-class outcome that does not block,
  and the review never presents free space as a guarantee.
- **`/bin/cp` behaviour differs from the managed path and is harder to unit
  test** → The service is behind `IItemRelocationService`, so ViewModel and Core
  tests use a substitute; the real implementation is covered by macOS-gated
  integration tests over temporary directories.
- **Copying a cloud-backed placeholder downloads its contents** → Planning,
  preflight, and review never touch contents, so materialization can only happen
  inside a transfer the user explicitly approved after seeing the destination and
  item list. No detection of placeholder items is added in this change; that is
  left to a future change if it proves needed.
- **Renaming `IsMovingCleanupBasketToTrash` breaks existing tests and AXAML
  bindings** → The rename is mechanical, compile-checked, and covered by the
  existing App test suite; the alternative of three parallel flags carries a real
  correctness risk.

## Migration Plan

No data migration and no persisted state changes. The feature is additive at the
UI level: the existing Move to Trash basket action keeps its behaviour and its
review wording. Rollback is reverting the change; nothing outside the process
retains relocation state.

## Open Questions

- Whether the macOS-gated RAM disk integration test is reliable enough to keep in
  the default test run or should be opt-in via an environment variable. Resolve
  when the test is first written; default to gated-and-ignorable.
- Whether `docs/STORAGE_MEASUREMENT.md` needs a full section on APFS clone copies
  or a single note under existing allocated-size caveats. Resolve during the
  documentation task.
