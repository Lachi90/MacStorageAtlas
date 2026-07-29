## 1. Core Measurement Contracts

- [x] 1.1 Run `openspec validate --all --strict --no-interactive` before implementation and resolve any artifact errors without changing approved scope.
- [x] 1.2 Rename the third measurement mode to shared-aware allocated measurement while preserving explicit logical and per-path allocated modes.
- [x] 1.3 Extend allocated metadata with data allocation, optional opaque shared-data identity, and clone-accounting availability without exposing APFS types in Core.
- [x] 1.4 Add clone-accounting coverage to progress and completed result contracts and cover available, unavailable, and partial accumulation with unit tests.
- [x] 1.5 Replace or supplement boolean shared state with quantitative shared bytes on result items and cover measured, counted, and shared invariants with unit tests.

## 2. Shared-Aware Scan Accounting

- [x] 2.1 Implement scan-local filesystem-identity accounting followed by verified shared-data accounting, with the first included group member representing shared data.
- [x] 2.2 Add unit tests for ordinary identities, repeated hardlinks, full-clone groups, clones outside scope, equal clone numbers on different volumes, and traversal-order-independent root totals.
- [x] 2.3 Count non-data allocation independently for each distinct identity and add tests where a later full clone has both shared data bytes and a positive counted contribution.
- [x] 2.4 Fail closed for missing, invalid, or inconsistent optional clone metadata and add tests proving no optimistic byte suppression and partial coverage reporting.
- [x] 2.5 Preserve additive directory, progress, treemap, file-type, and largest-file totals and add focused derived-view tests for mixed hardlink and full-clone groups.
- [x] 2.6 Cover followed symbolic links, collapsed packages, hidden-entry scope, recoverable required-metadata errors, optional clone-metadata gaps, and cancellation with internally consistent partial results.

## 3. macOS Metadata Capability

- [x] 3.1 Add Platform.Mac volume capability probing and cache clone-mapping support by mounted-volume identity with safe fallback for macOS 11 through 13 and unsupported filesystems.
- [x] 3.2 Add a coherent public-API metadata read for total allocation, data allocation, device, file identifier, link count, returned attributes, clone identifier, clone reference count, and sharing flags.
- [x] 3.3 Validate native buffer lengths, returned attribute masks, fixed-width field conversion, and Apple Silicon and Intel ABI handling before returning verified shared-data identity.
- [x] 3.4 Preserve required allocated metadata through the existing fallback when optional clone capability or attributes are unavailable, and cover supported, unsupported, malformed, and native-error paths with tests.
- [x] 3.5 Wire the extended metadata capability through the App composition root without adding Platform.Mac or UI dependencies to Core.

## 4. Settings and Presentation

- [x] 4.1 Migrate the stored `HardlinkAwareAllocated` mode name to the shared-aware mode, preserve legacy allocated-choice migration and unrelated settings, and add load/save/fallback tests.
- [x] 4.2 Update scan controls and captured result labels to shared-aware terminology and show available, unavailable, or partial clone-accounting coverage with ViewModel and converter tests.
- [x] 4.3 Update tree rows and item details to distinguish measured allocation, counted contribution, and shared bytes for hardlinks and full clones with presentation tests.
- [x] 4.4 Preserve zero-contribution rendering behavior while allowing partially shared clone items with positive non-data contribution to remain weighted correctly.
- [x] 4.5 Retain refresh-after-Trash behavior for shared-aware results and cover representative hardlink or clone removal, root removal, failure, and cancellation.

## 5. Reproducible macOS Verification

- [x] 5.1 Add isolated temporary APFS fixtures for an ordinary copy, a verified full clone, a clone made divergent by a small write, a hardlink, a sparse file, and a clone with non-data allocation.
- [x] 5.2 Gate clone integration tests on macOS and advertised volume capability, ignore unsupported environments with a clear reason, and always clean fixture directories.
- [x] 5.3 Verify full-clone data is counted once, divergent clones retain full contributions, hardlinks remain counted once, non-data allocation remains per identity, and every path remains browsable.
- [x] 5.4 Verify fixture inspection remains metadata-only and does not use content comparison, hashing, physical extent enumeration, or permanent deletion.

## 6. Documentation and WP-02 Tracking

- [x] 6.1 Update `docs/STORAGE_MEASUREMENT.md` with shared-aware terminology, capability coverage, full-versus-partial clone semantics, non-data allocation, scan scope, and reproducible clone fixtures.
- [x] 6.2 Update `README.md` and its primary-source comparison wording and verification date without claiming unique physical or reclaimable storage.
- [x] 6.3 Review and update `docs/FEATURES.md` and `docs/index.html` for changed user-visible behavior, limitations, choices, and screenshots where necessary.
- [x] 6.4 Update WP-02 status and notes in `docs/IMPLEMENTATION_ROADMAP.md`, leaving `benchmark-and-optimize-scans` as the remaining WP-02 change.
- [x] 6.5 Reconcile the final implementation with this change's proposal, design, delta specification, and completed task checkboxes.

## 7. Validation

- [x] 7.1 Run `dotnet build MacStorageAtlas.slnx --no-restore`.
- [x] 7.2 Run `dotnet test MacStorageAtlas.slnx --no-build`.
- [x] 7.3 Run `dotnet format MacStorageAtlas.slnx analyzers --diagnostics IDE0005 --verify-no-changes`.
- [x] 7.4 Run `openspec validate --all --strict --no-interactive`.
- [x] 7.5 Run `git diff --check` and confirm no generated output or unrelated working-tree changes were introduced.
