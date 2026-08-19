## Why

MacStorageAtlas can show which files are large, old, or filtered by metadata, but it cannot answer a common cleanup question: "which files are byte-for-byte copies of each other?" WP-10 in `docs/IMPLEMENTATION_ROADMAP.md` adds exact duplicate detection so users can find redundant copies without treating similarity, matching names, hardlinks, or shared storage as proof of waste.

This needs a focused change because exact duplicate detection is the first planned feature that intentionally reads file contents. It must stay cancellable, local, careful around cloud placeholders, and separate from ordinary metadata-only scanning.

## What Changes

- Add an opt-in duplicate analysis workflow for a completed scan result.
- Group regular files by current logical length, ignoring single-entry groups and zero-length files by default.
- Narrow candidates with beginning and ending byte samples before hashing full contents.
- Hash candidate contents with streaming reads and cancellation instead of buffering whole files in memory.
- Confirm equality before displaying any duplicate group as exact duplicates.
- Identify hardlinks as linked paths rather than reclaimable duplicate copies.
- Revalidate candidate file size, identity, and readability during analysis so changed files are skipped or reported instead of mislabeled.
- Skip cloud-only or dataless files without intentionally downloading their contents, and explain the skip.
- Add a duplicate review view that shows exact groups, non-reclaimable linked paths, skipped files, progress, and a reclaimable total that preserves at least one copy per group.
- Let the user explicitly add selected duplicate files to the existing cleanup basket while never auto-selecting files for cleanup.

## Non-goals

- Fuzzy matching, perceptual image similarity, near-duplicate detection, filename-only matching, or package-level duplicate classification.
- Automatic deletion, automatic selection of a preferred original, or recommendations that a file is safe to delete.
- Running duplicate analysis during every scan or changing normal scan measurement semantics.
- Persisting file hashes, file contents, or duplicate results across app restarts.
- Importing duplicate results into scan history or comparison workflows.
- Provider-specific cloud APIs or a user-directed command to download cloud placeholders.

## Capabilities

### New Capabilities

- `duplicate-detection`: how MacStorageAtlas performs local, cancellable exact duplicate analysis over completed scan results, verifies equality, handles hardlinks and changed files, reports skipped files, computes reclaimable totals, preserves privacy boundaries, and integrates reviewed duplicate selections with the cleanup basket.

### Modified Capabilities

None. `cleanup-basket` already covers explicit user actions on completed scan results and remains the only reviewed cleanup execution path. `storage-measurement`, `file-metadata`, `result-filtering`, and `scan-history` keep their existing requirements; duplicate analysis consumes a completed result without changing scan totals, metadata collection, filtering behavior, or history capture.

## Impact

- `src/MacStorageAtlas.Core`: new duplicate-analysis domain models and services under a focused Core responsibility, plus portable abstractions for current file metadata, identity, sampling, hashing, equality confirmation, progress, cancellation, and skip reasons.
- `src/MacStorageAtlas.Platform.Mac`: macOS file-identity and content-read support where platform metadata is needed to classify hardlinks and dataless files without adding UI dependencies to Core.
- `src/MacStorageAtlas.App`: duplicate analysis commands, cancellation, progress/status presentation, a duplicate review tab or panel, selection wiring, and integration with existing cleanup basket commands.
- `tests/MacStorageAtlas.Core.Tests`, `tests/MacStorageAtlas.Platform.Mac.Tests`, and `tests/MacStorageAtlas.App.Tests`: exact-match fixtures, same-size-different-content fixtures, hardlink handling, changed-file handling, read-error handling, cancellation, streaming behavior, skip reporting, and view-model wiring.
- Documentation: `README.md`, `docs/FEATURES.md`, `docs/index.html`, `docs/STORAGE_MEASUREMENT.md`, and the WP-10 status row in `docs/IMPLEMENTATION_ROADMAP.md` need review and updates.
- Privacy posture: duplicate analysis reads local file contents only after explicit user action, never sends names, paths, hashes, or results externally, and does not persist hashes or contents.

## Dependencies

- WP-03 item metadata and Quick Look are complete and provide the selected-item inspection model this review surface builds near.
- WP-04 filtering is complete and may narrow the visible result context, but duplicate analysis operates over the completed scan scope unless the UI explicitly offers a filtered-scope option in this change.
- WP-07 cleanup basket is complete and provides the reviewed, reversible cleanup path used after the user chooses duplicate files.
- WP-02 storage measurement and shared-aware accounting are complete enough to distinguish size accounting from content equality and to avoid treating hardlinks as reclaimable duplicate copies.

## Risks

- **Performance.** Reading large candidate files can take a long time and wake external disks. Size bucketing, sampling before full hashing, progress reporting, and cancellation keep the workflow deliberate.
- **Correctness.** Same-size files can differ, files can change during analysis, and hardlinks can look like duplicates while consuming no extra storage. Final byte equality confirmation, stale-file checks, and identity grouping are required mitigations.
- **Cloud placeholders.** Opening a dataless file can materialize remote content. The analysis must detect or conservatively skip cloud-only files when it cannot read without intentional download.
- **Privacy.** Hashes derived from file contents are sensitive. They stay process-local, are not exported or persisted, and are not transmitted.
- **UI safety.** A duplicate group can make users overconfident. The review must preserve at least one copy per group in reclaimable totals and never auto-select cleanup items.

## Roadmap Estimate

WP-10 is estimated at 8-14 days in `docs/IMPLEMENTATION_ROADMAP.md`.
