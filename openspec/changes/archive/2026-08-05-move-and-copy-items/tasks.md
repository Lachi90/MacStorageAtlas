## 1. Core relocation model

- [x] 1.1 Add `CleanupOperationKind` with `Trash`, `Move`, and `Copy` to `src/MacStorageAtlas.Core`
- [x] 1.2 Add `RelocationDestination` describing the chosen destination directory and its normalized path
- [x] 1.3 Add `RelocationDestinationStatusKind` and `RelocationDestinationValidation` covering ready, missing, not a directory, not writable, and insufficient free space, each with a user-visible message
- [x] 1.4 Add `RelocationFreeSpace` expressing available bytes or an explicit unknown state
- [x] 1.5 Extend `CleanupPreflightStatusKind` with `DestinationCollision`, `DestinationInsideSource`, and `AlreadyAtDestination`
- [x] 1.6 Add `CleanupPreflightStatus.ReadyToMove` and `CleanupPreflightStatus.ReadyToCopy` without changing the existing `Ready` value
- [x] 1.7 Add `IItemRelocationService` to Core with `MoveAsync` and `CopyAsync` taking a source path, destination directory, and `CancellationToken`
- [x] 1.8 Build the solution to confirm the new Core types compile

## 2. Core destination validation

- [x] 2.1 Add `RelocationDestinationValidator` producing a `RelocationDestinationValidation` for a chosen destination
- [x] 2.2 Implement missing, not-a-directory, and not-writable detection
- [x] 2.3 Implement free-space comparison against the total executable item size, treating an unavailable figure as unknown and not blocking on it
- [x] 2.4 Add `RelocationDestinationValidatorTests` covering ready, missing, not a directory, read-only, insufficient space, and unknown free space

## 3. Core relocation preflight

- [x] 3.1 Add `RelocationPreflightValidator` composing `CleanupPreflightValidator` and layering destination-aware per-item checks
- [x] 3.2 Implement destination-inside-source and already-at-destination detection reusing `CleanupProtectedPathPolicy.NormalizePath` and the existing ordinal containment comparison
- [x] 3.3 Implement name-collision detection against the destination that blocks the item and never overwrites, merges, or renames
- [x] 3.4 Apply operation-aware ready messages so a relocation review never reports readiness to move to Trash
- [x] 3.5 Add `RelocationPreflightValidatorTests` covering protected, missing, identity changed, size changed, collision, destination inside source, already at destination, and ready outcomes
- [x] 3.6 Add a test asserting that a blocked colliding item does not prevent non-colliding items from being executable

## 4. Core basket summary for relocation

- [x] 4.1 Extend cleanup basket summary calculation so expected uniquely reclaimable size is operation-dependent and reports zero for `Copy`
- [x] 4.2 Keep total logical size and item count unchanged across all three operations
- [x] 4.3 Add tests covering logical mode, allocated mode, overlap deduplication, and the zero-reclaim copy case

## 5. macOS relocation service

- [x] 5.1 Add `MacItemRelocationService` to `src/MacStorageAtlas.Platform.Mac` implementing `IItemRelocationService`
- [x] 5.2 Implement copy by invoking `/bin/cp -Rpc` through `Process` with redirected output, cancellation support, and a clear failure message, following the `MacTrashService` pattern
- [x] 5.3 Implement same-volume move using `File.Move` and `Directory.Move` through an injectable fast-rename delegate
- [x] 5.4 Implement the cross-volume move fallback as copy, then verification by recursive entry count and total byte length, then source removal
- [x] 5.5 Guarantee the source is never removed when the copy fails, is cancelled, or fails verification, and report the destination path of any partial result
- [x] 5.6 Refuse to write when the destination path already exists, so a collision created after preflight cannot overwrite
- [x] 5.7 Add `MacItemRelocationServiceTests` using isolated temporary directories for same-volume file and directory moves, copies, collision refusal, and cancellation
- [x] 5.8 Add fallback-path tests that force the rename delegate to fail, asserting verified-copy-then-delete, source retention on copy failure, and source retention on cancellation
- [x] 5.9 Add a macOS-gated cross-volume integration test backed by a RAM disk, ignored with a clear reason when the RAM disk cannot be created

## 6. App review surface

- [x] 6.1 Add `Operation` and a nullable `Destination` to `CleanupBasketReview` and expose the expected locally reclaimed size for the operation
- [x] 6.2 Update `AvaloniaCleanupBasketReviewService` to present the operation type, destination path, per-item readiness, and reclaimed-size figure
- [x] 6.3 Update `NullCleanupBasketReviewService` for the new review shape
- [x] 6.4 Add a `NullItemRelocationService` App-layer fallback consistent with the existing null service pattern

## 7. ViewModel relocation commands

- [x] 7.1 Rename `IsMovingCleanupBasketToTrash` to `IsRunningCleanupBasketOperation` and update all bindings, notifications, and existing tests
- [x] 7.2 Generalize `ExecuteCleanupBasketTrashAsync` into a shared itemized executor taking the operation kind and a transfer callback, preserving cancellation, unattempted, cancelled, and failed bookkeeping
- [x] 7.3 Add `MoveCleanupBasketToLocationCommand` and `CopyCleanupBasketToLocationCommand` that select a destination through `IFolderPickerService`, validate the destination, run relocation preflight, present the review, and execute
- [x] 7.4 Leave the basket and filesystem unchanged when destination selection is cancelled, when destination validation blocks the operation, or when the review is cancelled
- [x] 7.5 Report per-item relocation progress as the current item name and completed count
- [x] 7.6 Reconcile the displayed scan result after a successful move using the existing shared-aware refresh rule, and skip reconciliation entirely for a successful copy
- [x] 7.7 Inject `IItemRelocationService` through the constructor with the macOS implementation as the composition-root default

## 8. ViewModel tests

- [x] 8.1 Test that switching between Trash, move, and copy leaves basket contents unchanged
- [x] 8.2 Test cancelled destination selection, blocked destination validation, and cancelled review, asserting no filesystem call and no result change
- [x] 8.3 Test full success, partial failure, mid-operation cancellation, and all-blocked outcomes for both move and copy
- [x] 8.4 Test that a successful move updates the displayed logical result and triggers a rescan in shared-aware allocated mode
- [x] 8.5 Test that a successful copy leaves the displayed result, totals, and basket unchanged
- [x] 8.6 Test that the review receives the correct operation kind, destination, and expected reclaimed size, including zero for copy

## 9. UI wiring

- [x] 9.1 Add Move to location and Copy to location actions to the cleanup basket area in `MainWindow.axaml` using existing theme resources and compiled bindings
- [x] 9.2 Bind the shared busy state so the three basket operations cannot run concurrently and cancellation remains reachable during relocation
- [x] 9.3 Surface the relocation status message, destination, and per-item progress in the basket status area

## 10. Documentation and roadmap

- [x] 10.1 Update `README.md` with the move and copy actions, the no-overwrite collision behavior, and the cross-volume move guarantee
- [x] 10.2 Update `docs/index.html` if the described feature set or screenshots changed, and report explicitly when no update was needed
- [x] 10.3 Note in `docs/STORAGE_MEASUREMENT.md` that a same-volume APFS clone copy may consume almost no additional space
- [x] 10.4 Mark WP-08 status in `docs/IMPLEMENTATION_ROADMAP.md` and record that rename and replace collision policies were deliberately deferred

## 11. Validation

- [x] 11.1 Run `dotnet build MacStorageAtlas.slnx --no-restore`
- [x] 11.2 Run `dotnet test MacStorageAtlas.slnx --no-build`
- [x] 11.3 Run `dotnet format MacStorageAtlas.slnx analyzers --diagnostics IDE0005 --verify-no-changes`
- [x] 11.4 Run `git diff --check`
- [x] 11.5 Run `openspec validate --all --strict --no-interactive`
