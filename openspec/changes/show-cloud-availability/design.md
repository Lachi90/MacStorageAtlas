## Context

`DiskScanner.MeasureFile` calls `stat` through `IAllocatedFileMetadataReader`
only in the allocated modes. Logical mode reads the length through
`FileInfo`. `DiskItemMetadata` holds kind, attributes, and timestamps. On
APFS, a file whose content is not stored locally carries the `SF_DATALESS`
flag (`0x40000000`) in `st_flags`. A check on 2026-11-22 on a Mac with iCloud
Drive and OneDrive found the flag on placeholders of both providers, with the
full logical length and zero allocated blocks.

## Goals / Non-Goals

Goals:

- Show which files and folders are only in the cloud, in every measurement
  mode, from metadata only.
- Keep the scan streaming, cancellable, and free of downloads.

Non-Goals:

- Provider-specific integration, cloud state changes, and new byte bases.

## Decisions

### Status source: the `dataless` flag

Core defines `ICloudAvailabilityReader` (`Scanning`), implemented by
`MacFileMetadataReader` with the existing `stat` interop: `SF_DATALESS` set
means Cloud only, clear means Local, and a failed `stat` means Unknown. Core
stays portable: a scanner without the reader reports Unknown, and the reader
reports Unknown off macOS. In the allocated modes the value comes from the
metadata call that already runs: the `stat` fallback, or `ATTR_CMN_FLAGS` and
`ATTR_FILE_TOTALSIZE`, which the `getattrlist` read on clone-capable volumes
now also requests. `AllocatedFileMetadata.CloudAvailability` carries it, and
the scanner adds a `stat` only when that read did not return the flag. In
logical mode the `stat` that reads the flag also supplies the logical length
(`st_size`, the value `FileInfo.Length` reported), so no extra call is needed;
followed symbolic links and files whose status is Unknown keep the previous
length read. Directories read their own flag with one `stat` each.

Alternative considered: `NSURLUbiquitousItemDownloadingStatusKey`, which the
duplicate analysis already uses. Rejected as the primary source, because it
needs an Objective-C round trip per file and its coverage of File Provider
domains other than iCloud is not established. It remains in the duplicate
analysis.

### Folders

A directory's availability is aggregated while the scan unwinds:

- Cloud only if every included file below it is cloud only.
- Local if none is.
- Partly local otherwise, which includes cloud-only files next to files whose
  status is Unknown.
- Unknown if any file is Unknown and no file is cloud only.

An empty directory is Local and contributes nothing to its parent.
`DiskItem.CloudTotals` (`CloudAvailabilityTotals`) holds the local,
cloud-only, and unknown file counts, the cloud-only logical size, and the
number of unlisted cloud folders. The scan adds a child's totals to its parent
while it unwinds, like the size rollups, so `ScanSubtreeAsync` aggregates the
same way. `DiskItem.ReplaceDescendant` (used by `ScanResultMerger`) and
`DiskItem.RemoveDescendant` (Trash) apply the totals delta along the ancestor
path, so availability and cloud-only sizes stay correct after a folder rescan
or a Trash operation. Directories allocate the totals lazily, and local files
store none. A directory that itself carries `SF_DATALESS`
(a File Provider folder whose listing was never fetched) is handled by the
spike decision below.

### Spike: listing unfetched File Provider folders

Task 1 lists a never-opened OneDrive folder and a never-opened iCloud Drive
folder with the scanner's enumeration call while network monitoring is active,
and records whether the listing or any file content was fetched. If listing
triggers a fetch, the scanner does not enter directories that carry
`SF_DATALESS`. It reports them as cloud folders that were not listed, distinct
from access errors, and the completeness verdict treats them like an
incomplete scan. The outcome is recorded here before task 2 starts.

Spike result, 2026-11-22, on an Apple Silicon Mac with OneDrive and iCloud
Drive: `find -flags +dataless -prune` over the complete OneDrive tree (19,420
folders) and the complete iCloud Drive tree (9,940 folders) found no folder
carrying `SF_DATALESS`. Both providers keep the folder structure local and
evict only file contents, so listing their folders cannot trigger a fetch on
this system. 23 iCloud Drive files and 45 OneDrive files in the top two levels
carried `SF_DATALESS` with zero allocated blocks. The unfetched-folder case
could not be reproduced, so the conservative rule applies: the scanner never
enters a directory that carries `SF_DATALESS` and reports it as a cloud folder
that was not listed. That rule costs nothing when such folders do not occur.

Implementation: such a directory stays in the tree with no children, marked
Cloud only, and counts as cloud content for its ancestors. The scan records it
in `ScanProgress.UnlistedCloudFolders`, separate from `Errors`, and
`ScanResultMerger.MergeUnlistedCloudFolders` replaces the entries inside a
rescanned folder like errors. `ScanCompleteness` gains
`IncompleteCloudFoldersNotListed` (value 4), written only by schema version 3
snapshots. The completeness classifier keeps access restrictions and
recoverable errors first and reports this value when they are absent. The
status bar names the count and lists the paths in a tooltip; snapshots record
only the completeness value, not the list.

### Presentation

The tree and Largest files show a small cloud icon with a tooltip and an
accessible name ("Only in the cloud", "Partly local"). Cloud only uses a
filled cloud (`IconCloud`) and Partly local an outlined cloud
(`IconCloudPartial`), so the two differ without color. The marker's tooltip and accessible name
repeat the selected-item availability text, so hovering a folder shows how many
files and how many logical bytes are only in the cloud without selecting it.
Local items show
nothing, to keep the common case quiet. Selected item details show an
"Availability" row. The result summary shows, when M is greater than zero:
"N files local · M files only in the cloud (X logical, not on this Mac)".

The summary covers the whole result, not the insight scope. It sits in the
status bar next to the other whole-result totals (files, folders, measured
bytes), where the logical-mode note about the total belongs, and it does not
change when the Largest files and File types tabs are narrowed. The Selected
item details give the same figures for the selected folder or file.

### Cloud-only size as a labeled second basis

The user decided on 2026-11-22 that the summary also shows the size of
cloud-only content. That size is the sum of the logical lengths of cloud-only
files below the result root, or below the insight scope where the summary is
scoped. It is never added to, subtracted from, or shown in a column with the
scan's measured bytes, and its label always contains "logical" and "not on
this Mac". In logical mode the same bytes are also part of the scan total; the
label still applies, and the summary states that the total includes them.
Directories keep the cloud-only size in their internal totals so the summary
and the selected-folder details stay correct after rescans and Trash, but no
tree, treemap, list, or export column shows it.

The scan reads the logical length of cloud-only files from the same `stat`
call (`st_size`), so no extra call is needed in the allocated modes.

This is a deliberate exception to the repository rule that every byte count of
one scan uses one measurement mode. `AGENTS.md`, `CLAUDE.md`, and
`docs/STORAGE_MEASUREMENT.md` are updated to name it.

### Filter

`DiskItemFilter` gains `CloudAvailability` with the values Any, LocalOnly, and
CloudOnly. It matches files and combines with AND like the other criteria. A
preset stores the value as a string. Presets without it read as Any. A preset
that sets the criterion is written with preset schema version 3, so an older
version skips it instead of applying it without the restriction.

### History and export

Snapshots and JSON exports add an `availability` field per item, with schema
version 3. CSV adds an `availability` column. Readers map versions 1 and 2 to
Unknown.

## Risks / Trade-offs

- [Network activity on unfetched folders] → Spike first, then skip such
  folders if needed.
- [Logical-mode cost] → One `stat` per file. The scan benchmark must show no
  more than 10 percent slowdown on the benchmark tree, otherwise the read is
  restricted to allocated modes and logical mode shows Unknown. See the
  benchmark result below.
- [Folder availability after merges] → Totals are applied as deltas along the
  ancestor path on rescans and Trash; covered by Core and App tests.
- [Cloud-only size mistaken for measured bytes] → Fixed label wording, never
  combined with measured totals, documented exception.
- [Misreading Cloud only as safe to delete] → Wording never suggests deletion
  or space recovery. Cleanup and Trash behavior is unchanged.

### Benchmark result

Run on 2026-11-22 with the benchmark tool (`run --fixture existing --mode
logical`, Release build, .NET 10.0.15, arm64, macOS 27.2.1) against a tree of
50,000 one-byte files in 200 folders under the session scratch directory.
Seven runs per build, two interleaved rounds, medians in milliseconds:

| Build | Round 1 | Round 2 |
| --- | ---: | ---: |
| Before the change | 296.9 | 305.2 |
| Flag read added as an extra `stat` | 368.8 | 381.6 |
| Before the change, repeated | 294.7 | 292.5 |
| Flag and length from one `stat` | 283.8 | 284.3 |

An extra `stat` cost about 25 percent, above the 10 percent limit. Taking the
logical length from the same `stat` removes the extra call and leaves logical
mode about 3 percent faster than before, with identical file counts and byte
totals, so the fallback to Unknown in logical mode is not needed.

## Open Questions

- None. The cloud-only size question was decided on 2026-11-22.
