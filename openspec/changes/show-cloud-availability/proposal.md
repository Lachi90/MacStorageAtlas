## Why

Since 1.1.0 the allocated measurement modes count files that exist only in a
cloud provider, such as iCloud Drive, OneDrive, or Dropbox, at roughly zero
bytes, so size totals already reflect what occupies the Mac. The result still
does not say which files are only placeholders. A user who scans a cloud folder
cannot tell whether a small entry is genuinely small or merely not downloaded,
cannot see how much of a folder lives only in the cloud, and cannot list the
files that actually occupy local space.

macOS marks every file whose content is not stored locally with the
filesystem's `dataless` flag. It is the same flag for iCloud Drive and for
providers built on Apple's File Provider framework, and it is part of the
metadata the scan already reads, so the status can be shown without reading
contents or contacting a provider.

## What Changes

- Record a cloud availability for every scanned file: **Local**, **Cloud
  only** (a placeholder whose content is not on this Mac), or **Unknown**
  when the status could not be read. The status comes from the `dataless`
  flag and is read in every measurement mode.
- Derive a folder status: **Local**, **Cloud only**, or **Partly local**, from
  the files the scan included below it.
- Show the status in the folder tree, Largest files, and Selected item details,
  with an icon plus text so it does not rely on color.
- Show a per-result summary with the number of local and cloud-only files and
  the cloud-only size: the combined logical length of cloud-only files. The
  size is always labeled as logical cloud content that does not occupy this
  Mac, so it is never mistaken for a value in the scan's measurement basis.
- Add a filter criterion "Local files only" / "Cloud-only files only" that
  works like the existing criteria and also narrows Largest files and File
  types.
- Record the availability per item in scan history snapshots (schema version
  3) and in JSON and CSV exports (export schema version 3). Earlier versions
  stay readable as Unknown.
- Before implementation, verify that listing a folder whose content a File
  Provider has not fetched yet does not download anything. If it does, such
  folders are not entered and are reported as cloud folders that were not
  listed.

Non-goals:

- Do not change how bytes are measured. Allocated modes already count
  placeholders at their local allocation.
- Do not download, evict, pin, or otherwise change the cloud state of any file.
- Do not identify the provider by name or call any provider API.
- Do not add a "free up space" or "remove download" action.
- Do not compare two scans.

## Capabilities

### New Capabilities

- `cloud-availability`: Per-file and per-folder cloud availability derived
  from filesystem metadata, its presentation, and its summary.

### Modified Capabilities

- `result-filtering`: A cloud availability criterion.
- `scan-history`: Snapshots record each item's cloud availability.
- `result-export`: Exports carry each item's cloud availability.

## Impact

Affected areas:

- `src/MacStorageAtlas.Core/Items`: A `CloudAvailability` value on
  `DiskItemMetadata` and folder aggregation.
- `src/MacStorageAtlas.Core/Scanning`: Reading the status for every file and
  directory in all measurement modes.
- `src/MacStorageAtlas.Platform.Mac`: Reading `st_flags` through the existing
  `stat` interop.
- `src/MacStorageAtlas.Core/Filtering`, `History`, `Export`: The criterion and
  the schema version 3 fields.
- `src/MacStorageAtlas.App`: Tree, list, and details presentation, summary,
  and filter UI.
- Tests in Core, Platform.Mac, and App.
- Documentation: `README.md`, `docs/FEATURES.md`,
  `docs/STORAGE_MEASUREMENT.md`, and `docs/index.html`.

Dependencies:

- Builds on `rescan-folder-in-place` (schema version 2 for history and export)
  and `scope-insights-to-selected-folder` (insight lists honor the filter).

Risks:

- Listing a folder a File Provider has not fetched yet may trigger network
  activity. Task 1 verifies this with iCloud Drive and OneDrive before the
  rest of the work starts.
- Logical mode currently does not call `stat` for files. Reading the flag adds
  one metadata call per file in that mode. The benchmark task measures the
  cost.
- The cloud-only size is a second byte basis next to the scan's measurement
  mode. This is a deliberate, labeled exception to the rule that one result
  uses one basis. The repository rules and `docs/STORAGE_MEASUREMENT.md` are
  updated to describe it.

Roadmap reference:

- WP-14 (direct cloud-storage scans) is the closest work package. This change
  is the local, provider-neutral first step and keeps WP-14's direct provider
  scans out of scope.

Estimate:

- 3-5 days.
