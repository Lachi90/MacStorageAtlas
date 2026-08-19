## Context

MacStorageAtlas scanning is intentionally metadata-only. `DiskScanner` produces a `DiskItem` tree with paths, directory aggregation, scan-time metadata, and the selected measurement basis. In logical mode `SizeBytes` is logical length; in allocated modes `SizeBytes` and `MeasuredSizeBytes` describe storage accounting rather than content length. The completed result is then consumed by insight services such as largest files and file-type statistics, both of which walk the in-memory tree without revisiting the filesystem.

Exact duplicate detection is different from existing insight work because it intentionally opens and reads file contents after the user asks for analysis. It must therefore be a separate post-scan workflow with its own progress, cancellation, skip reporting, privacy rules, and stale-file checks. Folding it into normal scanning would violate existing storage-measurement and file-metadata contracts.

The cleanup basket already provides the reviewed, reversible path for filesystem mutations. Duplicate detection should help users choose candidates, but it must not decide that any file is safe to remove or bypass basket preflight.

## Goals / Non-Goals

**Goals:**

- Detect only byte-identical duplicate files from a completed scan result.
- Avoid reading file contents for files that cannot have a duplicate by current logical length.
- Keep analysis cancellable and off the UI thread.
- Report duplicate groups, linked hardlink paths, skipped files, read errors, changed files, and progress.
- Compute reclaimable duplicate totals while preserving at least one copy per exact group.
- Let users explicitly add reviewed duplicate files to the cleanup basket.
- Keep hashes and duplicate results local and process-only.

**Non-Goals:**

- Changing scan totals, measurement modes, filtering semantics, or history capture.
- Near-duplicate detection, content previews, automatic cleanup selection, or deletion recommendations.
- Persisting duplicate results or hashes.
- Adding a provider-specific cloud-download workflow.
- Building a reusable whole-app file index in this change.

## Decisions

### Decision 1: Duplicate analysis is post-scan, not a scan option

Duplicate analysis starts only after a completed scan is available. The App owns command enablement and cancellation; Core owns the duplicate-analysis service and result models. The scanner remains metadata-only and keeps its existing progress and measurement semantics.

Alternatives considered:

- **Scan option that hashes during scan.** Rejected because every scan would risk content reads, cloud placeholder materialization, slower progress, and a broader failure surface.
- **Background analysis automatically after scan completion.** Rejected for the first version because content reads can be expensive and privacy-sensitive; the user should explicitly choose to start.

### Decision 2: Core gets a dedicated duplicate-detection responsibility

Add a Core folder and namespace such as `MacStorageAtlas.Core.Duplicates` for domain models and the analyzer. Models should describe duplicate groups, group entries, linked paths, skipped candidates, analysis progress, and summary totals. The analyzer consumes a `DiskItem` root plus an adapter for current file metadata and content reads.

This keeps the behavior testable without Avalonia and avoids expanding the generic `Insights` namespace with content-reading behavior that has stronger safety rules than file-type or largest-file summaries.

### Decision 3: Build a temporary flat candidate list, not a second scan tree

The analyzer walks the completed `DiskItem` tree once, collects regular file paths, and immediately revalidates current logical length and basic file state. It then groups by logical length and discards zero-length and single-entry buckets by default. The flat candidate list is analysis-local and discarded when the operation completes or is cancelled.

Alternatives considered:

- **Reuse only `DiskItem.SizeBytes`.** Rejected because allocated-mode scans do not retain logical length as the primary size value.
- **Add a persistent flat index to every scan result.** Rejected for this change because duplicate analysis is optional and the repository explicitly avoids duplicate full copies of scan trees.

### Decision 4: Use a staged verification pipeline

Candidate groups move through these stages:

```text
scan tree
  -> current logical-length buckets
  -> identity grouping for hardlinks
  -> beginning/end sample buckets
  -> streaming full-content hash buckets
  -> final byte equality confirmation
  -> exact duplicate groups
```

The beginning/end sample stage exists to avoid full hashing for same-size files that differ near the edges. Full hashing uses a bounded buffer and incremental hash API so large files are never buffered in memory. Final equality confirmation protects against hash collision and changed-file races before any group is presented as exact duplicates.

Alternatives considered:

- **Hash every candidate without sampling.** Simpler, but slower on common same-size installer, VM, archive, and media cases.
- **Trust cryptographic hash equality without final comparison.** Low practical collision risk, but the product promise is exact duplicates. A final compare is cheaper than making a false-positive story part of the UX.

### Decision 5: File identity is current analysis metadata

Hardlink classification should use current file identity rather than relying on scan-time `DiskItem` data. `MacFileMetadataReader` already exposes identity for allocated measurement, but `DiskItem` does not retain it and logical scans never need it. Duplicate detection should introduce a narrow abstraction that can return current logical length, file identity when available, link count when available, and a dataless or not-local status when the platform can determine it.

Core treats identical file identities as linked paths inside the same content group, not reclaimable duplicate copies. If identity is unavailable on a platform or path, Core can still verify content equality, but it must avoid asserting hardlink status for that path.

### Decision 6: Dataless cloud handling is conservative and spike-backed

The repo currently documents cloud-placeholder safety but does not expose a reusable placeholder detector. Platform.Mac should add the smallest adapter needed for duplicate analysis to avoid intentional downloads. The spike for this change verified on 2026-08-12 against Apple Foundation documentation that `NSURLIsUbiquitousItemKey` identifies iCloud storage items, and `NSURLUbiquitousItemDownloadingStatusKey` reports whether a local copy exists and whether it is current. `NSURLUbiquitousItemDownloadingStatusNotDownloaded` is treated as not local, while `Current` and `Downloaded` are local enough for duplicate analysis. Missing or unavailable ubiquitous metadata is treated as indeterminate, not as cloud-only.

If the adapter can determine that content is not local, the analyzer skips the file with a cloud-placeholder reason. If the adapter cannot determine safety and opening the file would be the only way to know, the analyzer should skip conservatively rather than intentionally materialize remote content. Ordinary local read errors remain separate skip reasons.

### Decision 7: Duplicate groups choose a retained representative for totals only

The UI needs a reclaimable total, but choosing a representative must not become an automatic deletion recommendation. Each exact group computes a default retained copy for arithmetic only, using a deterministic and explainable ordering such as shortest path depth then ordinal path. All other non-linked entries contribute to the group reclaimable total. The user still chooses which files, if any, to add to the cleanup basket.

Hardlinked entries do not contribute duplicate waste because moving one hardlink path to Trash does not necessarily reclaim the file's storage while another link remains.

### Decision 8: App adds a duplicate review surface next to result detail tabs

`MainWindowViewModel` should own duplicate-analysis command state, cancellation source, selected duplicate entry, progress text, and result collections. The UI should add a Duplicates result tab or panel near Selected item, File types, Largest files, and Errors. A duplicate entry selection should reuse selected-item details and existing reveal, Quick Look, and cleanup-basket commands where practical.

The duplicate view should show empty, running, cancelled, complete, and skipped/error states. It should not use a modal review as the primary view because analysis may take minutes and users need a persistent result surface.

### Decision 9: Cleanup remains basket-driven

Duplicate detection integrates by selecting scanned `DiskItem` files and invoking the existing basket planner. It does not create a separate cleanup path, does not mutate the filesystem, and does not bypass basket protected-path and stale-file preflight. Adding duplicate entries to the basket remains explicit user action.

Because cleanup-basket already permits explicit additions from result views, no cleanup-basket requirement change is needed.

### Decision 10: Export and history do not include duplicate data

Duplicate results and hashes are derived from current contents at analysis time and are discarded when the scan is replaced or the app exits. Scan history remains a metadata snapshot, and result export remains a user-directed scan result export. This avoids persisting content-derived hashes and avoids making duplicate results stale without a fresh content read.

## Risks / Trade-offs

- **Slow analysis on large candidate sets** -> Stage by logical length and samples first, stream hashes, throttle progress updates, and support cancellation at every filesystem read boundary.
- **Files change during analysis** -> Re-read size and identity around content verification and skip candidates whose size or identity changed.
- **Hash collision or race** -> Confirm byte equality after hash bucketing before displaying a group.
- **Cloud placeholders materialize** -> Add a Platform.Mac spike and conservative skip behavior before content reads.
- **Hardlink totals mislead users** -> Represent linked paths separately and exclude them from reclaimable duplicate totals.
- **Duplicate view encourages unsafe cleanup** -> Preserve one copy in totals, never auto-select files, and route cleanup through the existing basket review.
- **Memory pressure from many candidates** -> Keep candidate records small, do not copy file contents, and discard analysis state when the result is replaced.

## Migration Plan

There is no stored data to migrate. The feature adds new analysis state and UI only. Rollback removes the duplicate analyzer and review surface without changing scan result, export, history, or cleanup-basket formats.

During implementation, keep the solution buildable after each task group. Documentation updates should explicitly say duplicate analysis is local, opt-in, exact-only, and may skip cloud-only files.

## Open Questions

- Should the first version analyze the full completed scan only, or also offer "visible filtered results only"? The proposal assumes full completed scan scope unless a filtered-scope option is deliberately added.
- Should zero-length duplicates remain hidden by default only, or should the UI expose an option to include them? The roadmap says ignore by default; adding an option is acceptable only if it does not expand scope substantially.
