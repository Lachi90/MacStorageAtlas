## Why

A completed scan lives only in memory. Users who want to archive what a volume
looked like before a cleanup, share a finding with someone else, pivot the
numbers in a spreadsheet, or diff two scans by hand have no way to get the
result out of the application. Everything they can see today is lost the moment
the app closes or the next scan starts.

This change implements WP-05 from
[`docs/IMPLEMENTATION_ROADMAP.md`](../../../docs/IMPLEMENTATION_ROADMAP.md). It
depends on the metadata from WP-03 and the filtered result set from WP-04, both
complete, and it produces the archived scan artifacts that WP-09 will later
persist and compare.

## What Changes

- Add a CSV export and a JSON export of the current scan result, written through
  a native save-file picker.
- Export a flat, one-row-per-item shape in both formats. CSV and JSON carry the
  same fields so the two formats are the same export, not two different views of
  it.
- Export the full result when no filter is active: files and directories, with
  directory rows carrying their subtree totals.
- Export only the matched files when a filter is active. Directory rows are
  omitted, because a directory's full subtree total is not the total of the
  matched rows beneath it and publishing both in one column would produce a file
  whose stated totals contradict its own rows.
- Report byte counts under the scan's own measurement basis rather than the
  roadmap's original `Logical size` plus `Allocated/unique size` column pair. A
  single scan produces exactly one measurement basis, so the export emits
  `MeasuredSizeBytes`, `CountedSizeBytes`, and `SharedSizeBytes` alongside the
  mode that produced them. **This amends the WP-05 field list in the roadmap.**
- Record scan metadata with the export: schema version, root path, scan
  completion time, scan options, measurement mode, clone-accounting coverage,
  item and byte totals, export scope, and the active filter.
- Record recoverable scan errors in the JSON export, and report the error count
  to the user on completion so a CSV export is never mistaken for a complete
  picture of the volume.
- Capture a scan completion timestamp, which the application does not record
  today, so an exported file is identifiable after the fact.
- Write exports as a stream, off the UI thread, under cancellation, and publish
  the result atomically so a cancelled or failed export never leaves a truncated
  file where the user asked for a complete one.
- Neutralize leading spreadsheet formula characters in CSV text fields, so that
  a path controlled by whoever created the file cannot execute on open. JSON
  keeps the exact byte values and is the fidelity-preserving format.

## Non-goals

- Exporting the file-type summary or the largest-files list as separate
  documents. Both are aggregates over rows the export already contains.
- Importing an export back into the application, or using an export as a scan
  source. Scan history and scan comparison are WP-09.
- Export presets, scheduled exports, command-line export, or remembering the
  last export directory.
- Reporting both a logical and an allocated size for the same scan. That would
  require the scanner to read both per file, which changes the measurement
  semantics defined by WP-02 and adds a syscall per file to a scan path that
  WP-02 just finished optimizing.
- Exporting the treemap layout, or any other rendering-level artifact.
- Sending an export anywhere. The picker writes to a local path the user chose,
  and nothing leaves the machine.

## Capabilities

### New Capabilities

- `result-export`: How MacStorageAtlas writes a completed scan result to a local
  CSV or JSON file; which fields each row carries and how they relate to the
  scan's measurement basis; how the export scope follows the active filter; what
  scan metadata and errors accompany the rows; how rows are ordered; how text is
  escaped for spreadsheet safety; and how cancellation and write failures are
  handled without presenting a partial file as complete.

### Modified Capabilities

None. Export reads the scan result, metadata, measurement values, and filter
result that `storage-measurement`, `file-metadata`, `result-filtering`, and
`result-tree-browsing` already specify, and changes none of their requirements.

## Impact

- `MacStorageAtlas.Core`: new export row and metadata models, a CSV writer, and
  a JSON writer. Both writers target a supplied `TextWriter` or `Stream` rather
  than a path, so neither takes a dependency on file pickers or the filesystem.
- `MacStorageAtlas.App`: a new save-file picker service and its Avalonia
  implementation, following the existing `IFolderPickerService` shape; an export
  command and export state on `MainWindowViewModel`; a scan completion timestamp
  sourced from the existing injected time provider; export entry points in
  `MainWindow.axaml`.
- `MacStorageAtlas.Rendering`: unchanged.
- `MacStorageAtlas.Platform.Mac`: unchanged. Export needs no platform
  integration beyond the picker Avalonia already provides.
- `MacStorageAtlas.Tests`: golden-file tests for CSV escaping and JSON shape, a
  JSON round-trip test at the model level, streaming and cancellation tests over
  a large synthetic tree, filtered-scope tests, and view-model tests for picker
  cancellation, write failure, and status reporting.
- Documentation: `README.md`, `docs/FEATURES.md`, and `docs/index.html` describe
  export as a user-visible capability; `docs/STORAGE_MEASUREMENT.md` gains the
  mapping from exported size fields to measurement modes; the roadmap's WP-05
  field list is amended and its status table records WP-05.

## Dependencies

- WP-03 metadata (`add-file-metadata`), complete. Exported creation,
  modification, and last-access fields read `DiskItemMetadata`.
- WP-04 filtering (`add-advanced-filters`, `improve-filter-presets`), complete.
  The filtered export scope reads `FilterResult.MatchedFiles`, and the exported
  filter description reads `DiskItemFilter`.
- WP-02 measurement (`define-storage-measurement`, `deduplicate-hardlinks`,
  `investigate-apfs-clone-accounting`), complete. The three exported size fields
  and the clone-accounting coverage value are defined there.

## Risks

- An exported CSV states byte totals without the surrounding explanation the app
  provides, so a reader can mistake shared-aware allocated bytes for unique
  physical storage. Mitigated by naming the size fields for what they measure,
  repeating the measurement mode on every row so a row is self-describing after
  a spreadsheet sort or a concatenation of two exports, and documenting the
  mapping in `docs/STORAGE_MEASUREMENT.md`.
- A CSV alone carries no error list, so an export from a scan that could not
  read thousands of paths looks complete. Mitigated by reporting the error count
  in the completion status message and by carrying the full error list in JSON.
- Exports of very large scans can allocate heavily or block the UI. Mitigated by
  streaming row by row to the output, running the write off the UI thread, and
  propagating a cancellation token through it.
- A cancelled or failed write can leave a partial file at the user's chosen
  path. Mitigated by writing to a temporary file in the destination directory
  and moving it over the target only after the write completes.
- An export deliberately persists file paths, which the project otherwise treats
  as private data that stays in memory. This is user-initiated, writes only to a
  path the user picked, and sends nothing anywhere; the spec states it
  explicitly so the intent is not mistaken for a lapse.
- The temporary-file approach assumes the application can resolve a local path
  from the picker's result, which holds for an unsandboxed distribution.
  Developer ID signing and notarization under WP-01 do not enable the App
  Sandbox, so this remains valid; the assumption is recorded in `design.md` as a
  revisit point should the app ever be sandboxed.

## Estimate

1-2 days, matching the WP-05 roadmap estimate.
