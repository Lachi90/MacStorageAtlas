## Context

MacStorageAtlas currently builds a single streaming scan tree of `DiskItem` results. Each item retains name, path, directory state, children, and the size fields needed for logical, allocated, and shared-aware measurement. The selected-item panel displays path and size details, while derived views such as treemap, file types, and largest files all point back to the same scan result objects.

WP-03 calls for users to determine what a file is and whether it is old before acting on it. This change implements the metadata portion only. Quick Look and keyboard shortcuts remain outside this change.

The main constraint is that metadata must describe the scan result, not a later filesystem state. Scanning must remain streaming, cancellable, privacy-local, and resilient to recoverable filesystem errors.

## Goals / Non-Goals

**Goals:**

- Retain basic scan-time metadata on every included file and directory result.
- Display available metadata in selected-item details from every result view.
- Represent unavailable metadata explicitly as unknown.
- Preserve existing storage measurement semantics and scan inclusion behavior.
- Keep Core portable and independent of Avalonia and macOS platform services.
- Keep metadata reads local and metadata-only, without opening file contents or materializing cloud placeholders.

**Non-Goals:**

- Quick Look preview, Space handling, and Command-I item details shortcuts.
- Duplicate detection, hashing, content inspection, deletion recommendations, or cleanup automation.
- Complete macOS Finder kind parity.
- Exporting, persisting, or synchronizing scan metadata.
- Reporting unique physical storage or changing shared-aware accounting.

## Decisions

### Store metadata on scan result items

Each included scan result item will carry an immutable metadata value captured during the scan. The metadata should include modified time, creation time when available, last-access time only when the implementation can identify it as displayable, attributes needed for presentation, item kind, and any size-related metadata that is available without changing the active measurement mode.

Alternative considered: query metadata when the user selects an item. That is simpler initially, but it can display metadata from after the scan, fail noisily for moved items, and make one result view inconsistent with another. Scan-time snapshots better match the existing result model.

### Keep size semantics centralized

The existing `DiskItem` measured, counted, and shared size properties remain the authoritative source for size totals and result ordering. Metadata may expose descriptive size fields for item details, but it must not redefine progress totals, directory aggregation, treemap area, file-type totals, or largest-file ordering.

Alternative considered: fold all size fields into the new metadata object. That would blur the existing storage-measurement contract and create unnecessary migration risk across Core, Rendering, and App.

### Use the existing filesystem visit

Metadata will be collected during the scanner's existing per-entry attribute and size work. The implementation should avoid a second full traversal and should avoid retaining duplicate path lists or duplicate scan trees.

Alternative considered: build metadata after scan completion with a tree walk. That keeps the scanner smaller, but it adds another filesystem pass, creates stale-data windows, and complicates cancellation.

### Start with stable metadata sources

Portable metadata should come from .NET filesystem APIs already used by Core when those APIs provide enough information. Platform.Mac may extend or normalize metadata only when macOS-specific behavior is needed and can be tested on Apple Silicon and Intel.

No technical spike is required before implementation if the change uses stable .NET metadata. A spike is required before adopting a new native macOS API for kind strings, Finder labels, quarantine state, or other extended metadata.

### Map responsibilities by project

Core owns the metadata value model, scanner capture, failure treatment, and formatting-independent result data. Rendering remains unchanged and continues to consume item sizes only. Platform.Mac owns any macOS-specific metadata reader extension that cannot live portably in Core. App owns selected-item presentation, date formatting, labels, and command enablement updates. Tests cover Core behavior, App view-model behavior, and macOS-specific behavior behind platform gates where applicable.

## Risks / Trade-offs

- Additional filesystem calls can slow large scans -> Reuse data already read during traversal where possible and include scan-performance preservation tests for representative fixtures.
- Last-access time may be unreliable or disabled on some volumes -> Treat it as optional and display unknown when reliability cannot be established.
- Metadata failures can become too noisy -> Report recoverable failures only when required metadata for an included item cannot be captured; avoid inventing fallback values.
- Platform-specific metadata can accidentally leak into Core -> Keep platform-only normalization behind interfaces or App-level presentation.
- Users may infer that old files are safe to delete -> Present metadata factually and keep existing Trash confirmation behavior unchanged.
- Files can change after a scan -> Display scan-time metadata and do not silently refresh selected-item details.

## Migration Plan

This is an additive result-model and UI change. There is no persisted scan-result format to migrate. Existing saved settings remain compatible.

Implementation can be rolled back by removing the metadata display and metadata fields while retaining existing size fields. No user data migration is required.

## Open Questions

- Should item kind be intentionally simple, such as file, folder, application bundle, package, symbolic link, or hidden item, or should it attempt localized Finder-like labels later?
- Should last-access time be omitted entirely for the first implementation if reliability cannot be determined consistently?
- Should directory timestamps be displayed with the same prominence as file timestamps, given that directory modified times can be confusing after nested changes?
