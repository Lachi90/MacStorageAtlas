## Context

A completed scan is held as a single `DiskItem` tree on `MainWindowViewModel`,
already sorted by descending counted size at the end of `DiskScanner.ScanAsync`.
Alongside it the view model holds the scan options that produced the result, the
resulting measurement mode, the clone-accounting coverage, the recoverable scan
errors, and — while a filter is active — a `FilterResult` carrying the matched
files and per-directory matched byte totals.

Everything the export needs is therefore already in memory. The work is deciding
what shape to write, which of the several in-memory quantities each written
field corresponds to, and how to get bytes onto disk without blocking the UI or
leaving a truncated file behind.

Three constraints shape the design. `MacStorageAtlas.Core` must not reference
Avalonia, so the writers cannot own the file picker. Every byte value produced
by one scan must use that scan's measurement mode, so the export cannot invent a
second basis. And scanned paths are private user data, so the export must write
only where the user pointed it.

## Goals / Non-Goals

**Goals:**

- One row shape shared by both formats, so CSV and JSON are the same export.
- Rows that remain interpretable after being sorted, filtered, or merged with
  rows from a different scan.
- Constant memory with respect to result size, and a responsive UI throughout.
- Reproducible output, so golden-file tests are meaningful and two exports of
  one result are identical.
- No partial file at the destination after cancellation or failure.

**Non-Goals:**

- A hierarchical JSON document. See the flat-shape decision below.
- Reading an export back into a live scan result. Round-tripping is required at
  the model level only, to prove the JSON shape is lossless.
- Any change to `DiskScanner`, `ScanProgress`, or the measurement semantics
  defined by WP-02.
- Reporting logical and allocated sizes for the same scan.

## Decisions

### Responsibility split

| Project | Responsibility |
| --- | --- |
| `MacStorageAtlas.Core` | Row and metadata models, row enumeration and ordering, CSV writer, JSON writer |
| `MacStorageAtlas.App` | Save-file picker abstraction and its Avalonia adapter, export commands, export state, scan completion timestamp |
| `MacStorageAtlas.Platform.Mac` | Unchanged |
| `MacStorageAtlas.Rendering` | Unchanged |
| `MacStorageAtlas.Tests` | Golden files, round-trip, streaming, cancellation, atomicity, and view-model tests |

The writers accept a `TextWriter` or a `Stream` rather than a path. This is what
keeps Core free of picker and filesystem concerns, and it is what makes
golden-file tests a string comparison rather than a temporary-directory dance.
It mirrors the existing split between `DiskScanner` in Core and
`IFolderPickerService` in App.

### Row enumeration is separate from formatting

A `ScanExportRowSource` produces `IEnumerable<ScanExportRow>` lazily, from either
a root `DiskItem` (full scope) or a matched-file list (filtered scope). Both
writers consume that sequence and know nothing about scope, filters, or trees.

Alternative considered: each writer walking the tree itself. Rejected because
the scope rules and the ordering rules would then exist twice and could drift
apart, and because a hand-built row sequence is the cheapest way to test a
writer against an awkward input.

### Flat rows in both formats, not nested JSON

Both formats emit one record per item, with a `Depth` field giving the item's
distance below the scan root.

Nested JSON reads better and mirrors the domain, but round-tripping it means
reconstructing parent links, and `DiskItem.AddChild` is `internal`. Satisfying
the round-trip requirement would mean adding tree-construction API to Core for
the benefit of a test. Flat keeps the round-trip at the model level, keeps CSV
and JSON genuinely identical in content, and streams without a writer stack.
`Depth` recovers grouping for pivot tables at the cost of one integer per row.

### Three size fields plus a per-row measurement mode

The roadmap's WP-05 field list asks for a logical size column and an
allocated/unique size column side by side. `DiskScanner` produces exactly one
measurement basis per scan: in logical mode `MeasuredSizeBytes` is the logical
length, in the allocated modes it is the allocated size, and no scan holds both.
The export therefore names the fields for what the model actually contains:

| Field | Source | Meaning |
| --- | --- | --- |
| `MeasurementMode` | `ResultMeasurementMode` | Basis for the three byte fields |
| `MeasuredSizeBytes` | `DiskItem.MeasuredSizeBytes` | What the mode measured |
| `CountedSizeBytes` | `DiskItem.SizeBytes` | Charged to this path |
| `SharedSizeBytes` | `DiskItem.SharedSizeBytes` | Attributed to another path |
| `IsSharedStorage` | `DiskItem.IsSizeCountedElsewhere` | Convenience flag for spreadsheet filtering |

Alternative considered: making the scanner read both logical and allocated
metadata per file. Rejected — it adds a syscall per file to the path WP-02 just
optimized, and it would change the measurement semantics that
`docs/STORAGE_MEASUREMENT.md` and the `storage-measurement` spec define.

Alternative considered: mode-specific column names, so a logical scan emits
`LogicalSizeBytes` and an allocated scan emits `AllocatedSizeBytes`. Rejected
because the header would then vary by scan, which is hostile to any script or
saved spreadsheet reading the file.

Repeating `MeasurementMode` on every row is redundant against a path field that
is already far larger. It buys rows that stay self-describing after a sort or
after someone concatenates two exports, and it avoids preamble lines above the
CSV header — which is the thing that actually breaks spreadsheet import.

### Field list

```text
Path, Name, Kind, Depth, MeasurementMode,
MeasuredSizeBytes, CountedSizeBytes, SharedSizeBytes, IsSharedStorage,
Extension, Category, CreatedUtc, ModifiedUtc, LastAccessedUtc
```

`Kind` and `Category` are written as the enum member names, not the localized
display labels used in the details pane, so a consumer can match on a stable
token. Timestamps are ISO 8601 in UTC, and empty when the scan could not
determine them. `Extension` and `Category` are empty for directories.

The JSON envelope wraps the same records:

```json
{
  "schemaVersion": 1,
  "scan": {
    "rootPath": "…",
    "completedAt": "…",
    "options": { "includeHiddenFiles": false, "followSymbolicLinks": false,
                 "treatPackagesAsDirectories": true },
    "measurementMode": "SharedAwareAllocated",
    "cloneAccountingCoverage": "Available",
    "scope": "Filtered",
    "filter": { "…": "…" },
    "itemCount": 128432,
    "totalCountedSizeBytes": 1234567890
  },
  "errors": [ { "path": "…", "message": "…", "exceptionType": "…" } ],
  "items": [ { "path": "…", "…": "…" } ]
}
```

### Totals come from a counting pre-pass

The metadata block precedes the rows in JSON, and the spec requires its totals to
agree with them. A streaming writer does not know the totals until it finishes.

The byte total sums the counted size of the **file** rows only. Directory rows
report their own subtree totals, so adding them to their descendants would count
every file once per ancestor and produce a number that means nothing. Summing
files instead makes the full-scope total equal the scan root's counted size, and
matches `FilterResult.MatchedBytes`, which is already the sum of the matched
files. The item total, by contrast, counts every emitted row.

The export therefore walks the in-memory tree once to count items and sum counted
bytes, then streams the rows. The tree is already resident, so the pre-pass is
pointer chasing with no filesystem access, negligible against the cost of
formatting and writing every row. In filtered scope the pre-pass is free:
`FilterResult.MatchCount` and `FilterResult.MatchedBytes` already hold both
values.

Alternative considered: a trailing summary object after the rows. Rejected — it
forces every consumer to read the whole document before it can interpret the
first row.

### Explicit ordering, computed without mutating the scan tree

Rows are ordered by counted size descending, then by ordinal path ascending.
Full scope walks depth-first pre-order, emitting a directory immediately before
its descendants; filtered scope emits matched files in that same order, flat.

The scan tree is already size-sorted, but `List.Sort` is not stable, so equal
sizes have no defined relative order and two scans of the same unchanged folder
could produce different exports. The export orders each directory's children
into a local array as it walks, leaving `_scanRoot` untouched so the displayed
tree does not shift under the user. Cost is one small sort per directory over an
in-memory list.

For filtered scope, `Depth` is derived from the item's path relative to the scan
root, since matched files carry no parent reference.

### CSV is spreadsheet-safe; JSON is byte-exact

CSV fields are quoted and escaped per RFC 4180, and the file is written with a
UTF-8 byte order mark — without it, non-ASCII paths garble in Excel, and the
acceptance criterion is that the export opens correctly in common spreadsheet
tools.

Text fields beginning with `=`, `+`, `-`, `@`, tab, or carriage return are
prefixed with an apostrophe inside the quoted field. macOS permits all of these
as the first character of a filename, and an export can carry names that came
from an archive someone else produced, so this is not purely self-inflicted
risk. The guard applies to `Path`, `Name`, and `Extension` only; the numeric
fields are never negative.

This does alter the text, which is precisely why JSON applies no such
substitution and is documented as the fidelity-preserving format. Any consumer
that needs the exact path reads JSON.

### Errors are carried by JSON only

The JSON envelope has a natural place for the scan's recoverable errors. CSV has
none that does not break its own parse. Rather than emit a second CSV file or a
preamble, the completion status message states the error count for both formats,
so a CSV user learns that the scan could not read part of the volume even though
the file cannot say so itself.

### Two commands, one per format

`ExportCsvCommand` and `ExportJsonCommand`, each opening the picker with the
matching file type and a suggested name of
`MacStorageAtlas-<root folder>-<yyyyMMdd-HHmmss>`.

Alternative considered: one export command with a format popup in the save
panel. Rejected because Avalonia's `FileTypeChoices` does not reliably report
which choice the user selected, which would leave the format inferred from the
typed extension — a silent failure mode when the user types nothing.

### Atomic publish via a temporary file in the destination directory

Verified against Avalonia 12.0.4 before implementation: on macOS,
`StorageProviderImpl.SaveFilePickerAsync` delegates to
`StorageProviderApi.SaveFileDialog`, which resolves the chosen URI through
`TryGetStorageItem(uri, create: true)`. The `create` flag only causes a wrapper
to be returned for a path that does not exist; it constructs a `FileInfo` and
never touches the filesystem. `NSSavePanel` presents its own replace
confirmation but does not remove or truncate an existing file. The destination
is therefore untouched when the picker returns, and
`IStorageItem.TryGetLocalPath()` resolves to `FileSystemInfo.FullName`, which is
valid for a file that does not exist yet.

Two consequences: the atomic-publish approach below is sound, and the
requirement that an existing file survives a cancelled export is achievable
without qualification.

The writer targets `<destination>.<token>.tmp` in the destination's own
directory, then `File.Move(temp, destination, overwrite: true)` once the write
completes. A same-volume move is atomic on APFS and HFS+. Cancellation or
failure deletes the temporary file in a `finally` and reports the outcome; the
destination is never touched.

Alternative considered: writing through the picker's `IStorageFile` stream and
deleting the file on failure. That survives the App Sandbox, which the
temporary-file approach may not, but it leaves a window in which a partial
document sits at exactly the path the user asked for. The application ships
unsandboxed, and the Developer ID signing and notarization planned in WP-01 do
not enable the App Sandbox, so the local path stays resolvable. **If the app is
ever sandboxed, this decision must be revisited**: `StorageProviderApi` returns a
security-scoped `StorageFile` rather than a `BclStorageFile` when the sandbox is
enabled, and a write to a sibling temporary path would fall outside the granted
scope.

### Scan completion time is stamped in the view model

`MainWindowViewModel` records `ScanCompletedAt` from its existing injected
`_referenceTimeProvider` when a progress update reports completion. Nothing in
Core changes.

Alternative considered: adding a timestamp to `ScanProgress`. Rejected because
it widens a Core record owned by the scan-performance work for the benefit of a
single consumer, and because the injected provider is what makes the value
deterministic under test.

Scan duration is deliberately omitted; it needs a start stamp too, and WP-09
will want it defined properly alongside scan history.

### Threading and cancellation

The export runs on `Task.Run` with its own `CancellationTokenSource`, mirroring
`RunScanAsync`. UI state — `IsExporting`, the status message, command
`CanExecute` — is marshalled through `IUiDispatcher`. The token is passed to the
row enumeration and to every asynchronous write.

## Risks / Trade-offs

- **The Avalonia save picker could have created or truncated the destination on
  macOS before the application writes a byte** → Resolved by inspection of
  Avalonia 12.0.4 before implementation; it does neither. Recorded above under
  the atomic-publish decision. Revisit on an Avalonia major upgrade.
- **A CSV read in isolation can be mistaken for a complete picture of the
  volume** → Report the recoverable error count on completion, carry the full
  list in JSON, and document that JSON is the archival format.
- **Shared-aware allocated bytes in a spreadsheet can be read as unique physical
  storage** → Name the fields for what they measure, repeat the mode per row,
  and extend `docs/STORAGE_MEASUREMENT.md` with the field mapping.
- **The formula guard alters exported text** → Confine it to CSV and to the
  three text fields, and document JSON as byte-exact.
- **A very large export could still allocate heavily through per-row string
  formatting** → Write fields directly to a buffered writer rather than
  composing a line per row, and cover it with a large-tree streaming test that
  asserts the export completes without materializing the document.
- **The counting pre-pass walks the tree twice** → Bounded, allocation-free, no
  filesystem access, and free in filtered scope.
- **An export deliberately persists private paths** → User-initiated, written
  only to a chosen destination, transmitted nowhere, and stated explicitly in the
  spec so the intent is unambiguous.

## Migration Plan

Purely additive. No persisted state, no settings schema change, no change to
scan behavior or to any existing spec's requirements. Rolling the change back
removes the export commands and their services; previously written export files
are unaffected, and `schemaVersion` identifies the shape of any file already
written.

## Open Questions

- Should `Category` for an item whose extension is unrecognised be empty or a
  reserved token such as `Other`? Empty is assumed; a reserved token would make
  spreadsheet grouping easier at the cost of inventing a value the domain does
  not have.
