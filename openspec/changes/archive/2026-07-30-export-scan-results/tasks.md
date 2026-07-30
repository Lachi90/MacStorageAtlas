## 1. Save-picker spike and scan completion time

- [x] 1.1 Spike Avalonia's `SaveFilePickerAsync` on macOS and record in `design.md` whether the chosen destination is created or truncated before the application writes, and whether `TryGetLocalPath` returns a usable path
- [x] 1.2 If the picker materializes the destination, either adjust the picker adapter so the destination is not created before the export completes, or revise the "existing file survives cancellation" scenario in `specs/result-export/spec.md` and re-validate the change before continuing
- [x] 1.3 Add a `ScanCompletedAt` observable property to `MainWindowViewModel`, stamped from the injected `_referenceTimeProvider` when a progress update reports completion, and cleared at the start of each scan
- [x] 1.4 Add view-model tests asserting that `ScanCompletedAt` is set from the injected provider on completion, is null before any scan completes, and is cleared when a new scan starts

## 2. Core export model

- [x] 2.1 Add `ScanExportScope` with `Full` and `Filtered` to `MacStorageAtlas.Core`
- [x] 2.2 Add `ScanExportRow` carrying path, name, kind, depth, measurement mode, measured size, counted size, shared size, shared-storage flag, extension, category, and the three timestamps
- [x] 2.3 Add `ScanExportMetadata` carrying schema version, root path, scan completion time, scan options, measurement mode, clone-accounting coverage, scope, filter, item count, and total counted bytes, with the schema version pinned at 1
- [x] 2.4 Add `ScanExportRequest` binding a metadata value, a row sequence, and the recoverable scan errors into the single input both writers accept
- [x] 2.5 Add tests asserting that a row exposes the enum member names rather than display labels for kind and category, and that an unknown timestamp stays null rather than defaulting to an instant

## 3. Row enumeration and ordering

- [x] 3.1 Add `ScanExportRowSource.EnumerateFull(DiskItem root, StorageMeasurementMode mode, CancellationToken)` yielding rows depth-first pre-order, ordering each directory's children by counted size descending then ordinal path ascending, into a local array without mutating the scan tree
- [x] 3.2 Add `ScanExportRowSource.EnumerateFiltered(IReadOnlyList<DiskItem> matchedFiles, string rootPath, StorageMeasurementMode mode, CancellationToken)` yielding matched files only, in the same order, with depth derived from each path relative to the scan root
- [x] 3.3 Add `ScanExportRowSource.Summarize` returning the item count and total counted bytes for a scope, and use `FilterResult.MatchCount` and `FilterResult.MatchedBytes` when the scope is filtered
- [x] 3.4 Add tests asserting that a directory is emitted immediately before its descendants, that equal-size siblings order by ordinal path, that enumerating the same tree twice yields identical sequences, that the source does not reorder the input tree, that filtered enumeration emits no directory rows, that filtered depth matches the item's distance below the root, and that cancellation stops enumeration promptly

## 4. CSV writer

- [x] 4.1 Add `ScanResultCsvWriter.WriteAsync(ScanExportRequest, TextWriter, CancellationToken)` emitting the header row and then each row, writing fields directly rather than composing a line per row
- [x] 4.2 Escape fields per RFC 4180, quoting any field containing a comma, a quotation mark, or a line break, and doubling embedded quotation marks
- [x] 4.3 Prefix `Path`, `Name`, and `Extension` values beginning with `=`, `+`, `-`, `@`, tab, or carriage return with an apostrophe inside the quoted field
- [x] 4.4 Format timestamps as ISO 8601 in UTC and unknown timestamps as an empty field, using the invariant culture for every field
- [x] 4.5 Add golden-file tests covering a name containing a comma, a quotation mark, and a line break; a name beginning with each formula character; non-ASCII names; unknown timestamps; a directory row with empty extension and category; and a shared-storage row
- [x] 4.6 Add a test asserting that the header names and their order match the field list in `design.md`

## 5. JSON writer

- [x] 5.1 Add `ScanResultJsonWriter.WriteAsync(ScanExportRequest, Stream, CancellationToken)` using `Utf8JsonWriter` to emit the metadata envelope, the error list, and the item array without buffering the document
- [x] 5.2 Write every field with the exact scanned value, applying none of the CSV formula substitutions, and serialize the active filter alongside the scope
- [x] 5.3 Add a model-level reader that reads a JSON export back into its metadata and row values, for round-trip verification
- [x] 5.4 Add a round-trip test asserting that metadata and every row field survive a write and read unchanged, including a path beginning with a formula character, unknown timestamps, and a scan with recoverable errors
- [x] 5.5 Add golden-file tests for the JSON envelope shape, including `schemaVersion`, an empty item array under a filter that matches nothing, and the error list

## 6. Metadata and totals agreement

- [x] 6.1 Compose the metadata from the view model's result state so root path, scan completion time, scan options, measurement mode, clone-accounting coverage, scope, and filter are recorded together
- [x] 6.2 Add tests asserting that the metadata item count equals the number of emitted rows and that the metadata total equals the sum of the emitted counted sizes, for both scopes

## 7. Save-file picker service

- [x] 7.1 Add `ISaveFilePickerService` to `MacStorageAtlas.App.Services`, returning the chosen destination path or null when the picker is dismissed
- [x] 7.2 Add `AvaloniaSaveFilePickerService` implementing it over `IStorageProvider`, following the `AvaloniaFolderPickerService` shape and applying the spike outcome from task 1.1
- [x] 7.3 Add `NullSaveFilePickerService` returning null, matching the existing null-service pattern used by the view-model constructor defaults
- [x] 7.4 Register the Avalonia implementation in `App.axaml.cs`

## 8. Export commands and state

- [x] 8.1 Add `IsExporting`, an export status message, and a `CancelExportCommand` to `MainWindowViewModel`, mirroring the scan cancellation pattern
- [x] 8.2 Add `ExportCsvCommand` and `ExportJsonCommand`, each gated on a completed result and no scan or export in progress, opening the picker with the matching file type and a suggested name of `MacStorageAtlas-<root folder>-<yyyyMMdd-HHmmss>`
- [x] 8.3 Run the write on `Task.Run` with its own `CancellationTokenSource`, marshalling every UI state change through `IUiDispatcher`
- [x] 8.4 Write to `<destination>.<token>.tmp` in the destination directory and move it over the destination with overwrite only after the write completes, deleting the temporary file in a `finally` on cancellation or failure
- [x] 8.5 Report completion with the exported item count and, when the scan had recoverable errors, their count; report cancellation and write failure distinctly from success
- [x] 8.6 Add view-model tests covering a dismissed picker writing nothing and reporting no failure, a successful export of both formats, export scope following the active filter, cancellation mid-write leaving no file at the destination, a write failure leaving no partial file and reporting the failure, the error count appearing in the completion message, and both commands being disabled while scanning and while exporting

## 9. View integration

- [x] 9.1 Add the two export entry points to `MainWindow.axaml` beside the existing result actions, using compiled bindings, existing theme resources, and existing icon styles
- [x] 9.2 Surface the export status message and the cancel action in the existing status area, following how scan and trash status messages are presented

## 10. Documentation and roadmap

- [x] 10.1 Amend the WP-05 field list in `docs/IMPLEMENTATION_ROADMAP.md` to the three measurement-basis size fields, note why a single scan cannot report both a logical and an allocated size, and record WP-05 in the roadmap status table
- [x] 10.2 Add the exported field mapping to `docs/STORAGE_MEASUREMENT.md`, stating which measurement mode produces which size field
- [x] 10.3 Describe export in `README.md` and `docs/FEATURES.md`, including the filtered-scope rule, that JSON is the fidelity-preserving and error-carrying format, and that CSV neutralizes leading formula characters
- [x] 10.4 Review `docs/index.html` and update it if export changes what the landing page claims, or record that no update was needed

## 11. Validation

- [x] 11.1 Run `dotnet build MacStorageAtlas.slnx --no-restore`
- [x] 11.2 Run `dotnet test MacStorageAtlas.slnx --no-build`
- [x] 11.3 Run `dotnet format MacStorageAtlas.slnx analyzers --diagnostics IDE0005 --verify-no-changes`
- [x] 11.4 Run `git diff --check`
- [x] 11.5 Run `openspec validate --all --strict --no-interactive`
- [ ] 11.6 Export a scan of a folder containing non-ASCII names, a name with a comma and a quotation mark, and a name beginning with an equals sign, then open the CSV in Numbers and confirm the columns align, the characters display correctly, and no cell evaluates as a formula
