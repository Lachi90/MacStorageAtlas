# Atlas Storage Analyzer

> **See what's eating your disk — at a glance.** A fast, native macOS disk usage
> analyzer that turns a folder or volume into a sortable tree and an interactive
> treemap, so you can find the space hogs and reclaim gigabytes in seconds.

![Platform: macOS](https://img.shields.io/badge/platform-macOS-black?logo=apple)
![Built with .NET 10](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)
![UI: Avalonia](https://img.shields.io/badge/UI-Avalonia-8B5CF6)
![Arch: Apple Silicon + Intel](https://img.shields.io/badge/arch-arm64%20%2B%20x64-informational)

**Atlas Storage Analyzer** is a free, open-source **disk space analyzer for macOS** — a
native **WinDirStat, DaisyDisk, and GrandPerspective alternative** for Apple
Silicon and Intel Macs. If you're wondering *what is using my disk space on Mac*
and want to *free up storage*, it scans folders and volumes and visualizes the
results as a sortable folder tree, a proportional treemap, file-type statistics,
and a list of the largest files — so you can find the space hogs and reclaim
gigabytes in seconds.

Atlas Storage Analyzer is free on GitHub and free on the
[Mac App Store](https://apps.apple.com/app/id6803195382), for users who
prefer Store installation and updates. The public
[Support](https://lachi90.github.io/MacStorageAtlas/support.html) page explains
how to report issues, and the
[Privacy Policy](https://lachi90.github.io/MacStorageAtlas/privacy.html)
describes the app's local-only data handling.

![Folder tree and interactive treemap of a scanned folder](docs/images/01-overview.png)

## Highlights

### 🗺️ Folder tree + interactive treemap, side by side

Every scan gives you two synchronized views: a folder tree sorted by size and a
proportional treemap where the biggest blocks are the biggest consumers. Click a
block to inspect it — name, full path, item kind, scan-time dates, and size —
and jump straight to it.

![Selected treemap block with item details](docs/images/02-selected-item.png)

### ⚡ Live progress, and stop whenever you want

Scanning is fully async and streams progress as it runs: current path, file and
folder counts, and bytes scanned so far. Big volumes (hundreds of thousands of
files) stay responsive, and a single click cancels — partial results stay
visible.

![Live scan progress overlay with file, folder, and byte counters](docs/images/06-scanning.png)

### 📊 Breakdown by file type and largest files

Two dedicated tabs answer the questions that matter: *which kinds of files take
up the most room?* and *what are the single biggest files on disk?* — each with
counts, sizes, and full paths. Both tabs follow the folder you select in the
folder tree or the treemap, name that folder, and return to the whole scan with
one click on "Show whole scan".

| File types | Largest files |
| --- | --- |
| ![Storage grouped by file extension](docs/images/03-file-types.png) | ![List of the largest files with paths](docs/images/04-largest-files.png) |

### 🧩 Exact duplicate review

After a scan completes, run an opt-in duplicate analysis to find byte-identical
regular files. Atlas Storage Analyzer narrows candidates by current logical length,
samples file edges, hashes surviving content with bounded buffers, and confirms
matches byte for byte before showing them. Analysis can be cancelled, hardlinked
paths are shown as linked paths rather than reclaimable copies, and known
cloud-only placeholders are skipped instead of downloaded. Duplicate entries
can be selected for Quick Look, Finder reveal, and the existing cleanup basket,
but Atlas Storage Analyzer never auto-selects a copy for cleanup.

![Exact duplicate review tab with matching paths and reclaimable size](docs/images/07-duplicates.png)

### 🔎 Preview, reveal, or move to Trash — safely

Found something to inspect? Preview the selected item with Quick Look, reveal it
in Finder, move an eligible item to the Trash after a confirmation, or collect
multiple scanned items in the cleanup basket for a final review. Control-click
any item in the tree, treemap, or result lists to reach these actions from a
context menu, or copy the item's full path. Broad or sensitive containers are
blocked from in-app cleanup with an explanation. Files are never permanently
deleted — approved cleanup items go to the macOS Trash, and partial failures
identify the item that could not be moved.

### 📦 Archive instead of delete

Short on space but not ready to let go? Move or copy the reviewed cleanup basket
to an external or network volume instead of the Trash. The review names the
operation and the destination, and reports the expected locally reclaimed size —
zero for a copy, since a copy frees nothing. Nothing at the destination is ever
replaced: an item whose name already exists there is blocked with a reason
rather than overwritten or auto-renamed. A move across volumes copies first,
verifies the copy, and only then removes the original, so a failed or cancelled
transfer always leaves the source in place.

![Cleanup basket summary with review, move, copy, and clear actions](docs/images/08-cleanup-basket.png)

### ⚙️ Configurable scanning

Fine-tune what gets counted: scan inside `.app` bundles (or treat them as single
items), include hidden files, and follow symbolic links. By default sizes use
shared-aware allocated measurement: repeated hardlink identities count once,
and verified full-clone data counts once on capable volumes. Independently
allocated non-data storage and divergent clone extents remain per identity.
You can instead choose allocated size per path or logical file length. When a
scan of `/` reaches the same data through a firmlinked path such as `/Users`
and through `/System/Volumes/Data`, it is counted once and shown at the
firmlinked path. See
[Storage measurement semantics](docs/STORAGE_MEASUREMENT.md) for capability
coverage and scope. Preferences and manageable recent scan locations are
remembered between runs.

## Features

- Select and scan any folder or volume with live progress reporting, and
  cancel a running scan at any time.
- Work from a single-row toolbar. One scan control reads Scan, Rescan once a
  result for the selected location is shown, and Stop while a scan or folder
  rescan runs, and ignores an accidental second click. At the default window
  size Quick Look, Reveal in Finder, Move to Trash, one cleanup basket toggle
  that shows the basket item count, and one Export menu for CSV and JSON are
  direct icon controls. Each one names its action and shortcut in its tooltip
  and to VoiceOver.
- Browse results as a folder tree sorted by size, or as an interactive treemap.
- Inspect selected file and folder metadata captured during the scan, including
  item kind and available created or modified dates.
- See which files and folders exist only in the cloud. Placeholders from iCloud
  Drive and File Provider apps such as OneDrive carry a cloud icon in the
  folder tree and Largest files whose tooltip shows how much of a folder is
  only in the cloud, the Selected item tab states Local, Only in
  the cloud, Partly local, or Unknown, and the status bar counts local and
  cloud-only files with the cloud-only size, always labeled as logical content
  not on this Mac. The status comes from file metadata in every measurement
  mode, nothing is downloaded, and a cloud folder whose listing was never
  fetched is reported instead of listed.
- Preview the selected item with Quick Look from its toolbar control, and use
  Space or Command-I shortcuts for inspection.
- Control-click or right-click an item in the folder tree, treemap, Largest
  files list, or Duplicates list to open a context menu with Quick Look, Show
  details, Reveal in Finder, Copy path, Rescan this folder for folders,
  cleanup-basket actions, and Move to Trash for that item. Command-Option-C
  copies the selected item's full path.
- See storage broken down by file type and a list of the largest files. Both
  tabs describe the folder last selected in the folder tree or the treemap,
  including its subfolders; selecting a file uses its containing folder.
  Picking an entry in the Largest files or Duplicates list keeps the scope, an
  active filter still applies inside it, and "Show whole scan" returns both
  tabs to the entire result. Export, scan history, duplicate analysis, and the
  filter match totals always cover the whole result.
- Run opt-in exact duplicate analysis after a scan completes. Duplicate
  analysis reads local file contents only when candidates survive metadata and
  sample checks, can be cancelled, skips known cloud-only placeholders, shows
  hardlinks as linked paths, and lets duplicate entries flow through the same
  Quick Look, Finder reveal, and cleanup-basket review commands as other
  scanned items.
- Rescan one folder of a displayed result in place with "Rescan this folder" on
  the Selected item tab, the item context menu, or Shift-Command-R. Only that
  folder is remeasured, with the scan options of the displayed result, while
  the rest of the result keeps its values. Ancestor totals, the treemap,
  filters, insights, errors, and the cleanup basket follow the refreshed
  folder, and the result summary lists each rescanned folder with its rescan
  time. Shared-aware allocated results and scans that follow symbolic links
  offer a confirmed full rescan instead, because their folder totals depend on
  traversal order.
- Search and filter scanned items by name or path, and press Command-F to jump
  to the search box.
- Narrow a completed scan with advanced filters covering size, creation,
  modification and last-access dates, file extension, file category,
  shared-storage status, and cloud availability. Criteria combine with AND, and
  filtering never rescans.
- Set each date bound to a fixed date or to a span before the moment the filter
  runs, such as 18 months ago. A relative bound is stored as the span, so a
  saved preset keeps meaning the same span instead of drifting as time passes.
  The panel shows the date each span resolved to.
- Apply built-in filter presets, or save, rename, delete, and update your own.
  Presets persist between runs, and the panel shows which preset the current
  criteria match and whether they have been edited since.
- See the number of matching files, their total matched size, and how many
  files were excluded because a required date was unknown.
- Export the current result as CSV or JSON to a location you choose. Exporting
  with a filter active writes the matched files only. JSON preserves paths
  exactly and carries the scan's unreadable-path list; CSV is written for
  spreadsheets, with a UTF-8 byte order mark and leading formula characters
  neutralized so no cell executes on open. A cancelled or failed export never
  leaves a partial file behind. JSON exports of a result with rescanned folders
  list those folders and their rescan times, and a CSV export reports that the
  result mixes measurement times. Every exported row states the item's cloud
  availability.
- Optionally record completed scans to a local history, so a later release can
  show what grew or shrank between two points in time. Recording is off until
  you turn it on, snapshots stay on your Mac and never include file contents,
  and the store is capped by snapshot count and total size. A completed folder
  rescan records a snapshot of the whole merged result that states the original
  scan time and each rescanned folder, and the history marks such snapshots.
  You can delete a single recorded scan or clear the whole history at any time.
- Reveal items in Finder, move one eligible item to Trash, or review a
  multi-item cleanup basket before moving approved items to Trash. Broad or
  sensitive containers are blocked from in-app cleanup with an explanation.
- Move or copy the reviewed cleanup basket to another volume instead of the
  Trash. Destination free space, read-only destinations, and moves into an
  item's own subtree are checked before the review, colliding names are blocked
  rather than overwritten, and a cross-volume move only removes the original
  after the copy is verified.
- Inspect files and folders that couldn't be scanned, and copy their paths to
  the clipboard.
- Get access guidance when macOS-protected locations make a scan appear
  incomplete: a shortcut to Privacy & Security with manual fallback
  instructions in the GitHub build, selection guidance in the sandboxed Mac App
  Store build, and a rescan action in both.
- Configurable scanning: hidden files, symbolic links, `.app` package
  expansion, and logical, per-path allocated, or shared-aware allocated size
  measurement.
- Remembers your scanner preferences and recent scan locations between runs,
  with controls to remove individual entries or clear the list.
- Find every command in the macOS menu bar, under File, Edit, Scan, Item,
  View, Window, and Help, with the same enablement as its toolbar or context
  menu control and the keyboard shortcuts listed below.
- Modern, native-feeling UI that follows the system light/dark appearance, with
  a responsive treemap and a live scan-progress overlay.

### Keyboard shortcuts

| Shortcut | Command | Menu |
| --- | --- | --- |
| Command-O | Select Folder… | File |
| Command-E | Export as CSV… | File |
| Shift-Command-E | Export as JSON… | File |
| Command-R | Scan or Rescan the selected location | Scan |
| Shift-Command-R | Rescan the selected folder | Scan > Rescan Folder |
| Command-Period | Stop the running scan | Scan |
| Command-Y or Space | Quick Look | Item |
| Command-I | Show Details | Item |
| Option-Command-R | Reveal in Finder | Item |
| Option-Command-C | Copy Path | Item |
| Command-B | Add to or Remove from Cleanup Basket | Item |
| Command-Delete | Move to Trash… | Item |
| Command-F | Filter and Search | View |
| Command-M | Minimize | Window |

A shortcut does nothing while its command is unavailable. Space,
Shift-Command-R, Option-Command-C, Command-B, and Command-Delete never act on a
scanned item while a text field such as the search box has focus, so they keep
their text-editing behavior there. Because macOS would otherwise deliver them to
the menu before the text field, their menu entries do not display the shortcut;
the item context menu shows them instead. Move to Trash from the menu or
Command-Delete keeps the protected-path check and the Trash confirmation.

> Completed-result screenshots above show Atlas Storage Analyzer analyzing a prepared
> demo folder of photos, caches, archives, design assets, and video projects,
> so the breakdown reflects the kind of library a real user scans.

> Branding artwork lives under `src/MacStorageAtlas.App/Assets/`: `app.ico` (window
> icon), `icon.png` (1024×1024 master), and `MacStorageAtlas.icns` for macOS app
> bundling.

## How it compares

Looking for a **free DaisyDisk alternative** or a **WinDirStat for Mac**?
Atlas Storage Analyzer focuses on the essentials — a fast scan, a treemap, and safe
cleanup — without a subscription or a price tag.

| Capability | Atlas Storage Analyzer | [DaisyDisk][daisydisk] | [GrandPerspective][grandperspective] | [WinDirStat][windirstat] |
| --- | --- | --- | --- | --- |
| Platform | macOS (Apple Silicon + Intel) | [macOS (Apple Silicon + Intel)][daisydisk-pricing] | [macOS (Apple Silicon + Intel)][grandperspective] | [Windows][windirstat] |
| Distribution | [Free, MIT-licensed open source](LICENSE); free on GitHub and the [Mac App Store](https://apps.apple.com/app/id6803195382) | [$9.99 one-time commercial license][daisydisk-pricing] | [Free GPL build; $2.99 App Store build][grandperspective] | [Free, GPLv2 open source][windirstat] |
| Main analysis views | [Folder tree, rectangular treemap, file-type statistics](#highlights) | [Sunburst disk map and sidebar list][daisydisk-map] | [Rectangular treemap][grandperspective] | [Directory/file lists, treemap, and extension statistics][windirstat] |
| File-size measurement | [Logical length, allocated blocks per path, or shared-aware allocated blocks](src/MacStorageAtlas.Platform.Mac/MacFileMetadataReader.cs); hardlinks and verified full-clone data count once where capability coverage permits | [Physical size; hardlinks and full APFS clones are counted once][daisydisk-hardlinks] | [Logical, physical, or file-count sizing][grandperspective-sizes]; [hardlinks counted once per view][grandperspective-hardlinks] | [Logical or physical sizing with hardlink deduplication][windirstat-source] |

**Storage-measurement note:** these products do not use one interchangeable
definition of “real size.” Atlas Storage Analyzer handles sparse files and local cloud
placeholders and, by default, counts hardlinks once plus verified full-clone
data once on capable volumes. It does not deduplicate divergent clone extents
and does not claim unique physical or reclaimable storage. The optional
per-path mode counts every visited path. DaisyDisk documents full APFS-clone
detection on macOS 14 Sonoma and later.

Comparison last verified against the linked first-party sources: **2026-08-27**.
Prices and capabilities can change; follow the links for current product
details.

[daisydisk]: https://daisydiskapp.com/
[daisydisk-pricing]: https://daisydiskapp.com/support/pricing/
[daisydisk-map]: https://daisydiskapp.com/guide/4/en/UnderstandingSunburst/
[daisydisk-hardlinks]: https://daisydiskapp.com/guide/4/en/HardLinks
[grandperspective]: https://grandperspectiv.sourceforge.net/
[grandperspective-sizes]: https://grandperspectiv.sourceforge.net/HelpDocumentation/FileSizes.html
[grandperspective-hardlinks]: https://grandperspectiv.sourceforge.net/HelpDocumentation/HardLinks.html
[windirstat]: https://windirstat.net/
[windirstat-source]: https://github.com/windirstat/windirstat

## Prerequisites

- macOS
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Avalonia templates are **not** required to build or run; all dependencies are
  restored from NuGet.

## Build

```shell
dotnet restore
dotnet build --no-restore
```

## Test

```shell
dotnet test --no-build
```

## Run

```shell
dotnet run --project src/MacStorageAtlas.App
```

## Download options

- **GitHub release:** signed and notarized DMGs remain available for free from
  GitHub Releases.
- **Mac App Store edition:** a
  [free App Store download](https://apps.apple.com/app/id6803195382) for users
  who prefer Store installation and updates.

## Package

Since 1.1.0, the Developer ID release includes Sparkle updates through **Check
for Updates…** in the application menu, with optional automatic checks.
Downloads older than 1.1.0 need one manual installation of 1.1.0. Store updates
continue through the Mac App Store.

Update installation can close in-memory scan results; export them first.
Updater-triggered restart waits for running scans, analysis, exports, and file
operations, then requires an explicit retry. See [update troubleshooting](docs/TROUBLESHOOTING.md#software-updates)
and [release setup](docs/PACKAGING.md#direct-update-release-setup).

Run the packaging script from the repository root. It publishes a self-contained
Release build, wraps it in a `AtlasStorageAnalyzer.app` bundle (with the `.icns` app
icon), and creates a DMG with a drag-to-`Applications` shortcut.

```shell
./build-dmg.sh            # Apple Silicon (default) → AtlasStorageAnalyzer.dmg
./build-dmg.sh arm64      # Apple Silicon (osx-arm64)
./build-dmg.sh x64        # Intel (osx-x64)
./build-dmg.sh both       # both architectures, one DMG each
```

When building `both`, the DMGs are named per architecture
(`AtlasStorageAnalyzer-osx-arm64.dmg` and `AtlasStorageAnalyzer-osx-x64.dmg`). Each build
is self-contained and does **not** run under Rosetta on the other architecture —
pick the DMG that matches the target Mac.

The default packaging commands produce unsigned development DMGs. Official
public release artifacts use the explicit local Developer ID path, which signs
the app with the runtime entitlements required by CoreCLR, notarizes and staples
the DMG, verifies the result, and writes SHA-256 checksum files:

```shell
./build-dmg.sh release both 1.2.3 \
  "Developer ID Application: Example Company (TEAMID)" \
  "MacStorageAtlas-notary"
```

Release DMGs are named `AtlasStorageAnalyzer-<version>-<runtime>.dmg`. See
[Packaging Atlas Storage Analyzer for macOS](docs/PACKAGING.md) for certificate,
notary profile, verification, and GitHub Release upload steps.

Mac App Store packages use Apple Distribution and Mac Installer Distribution
certificates and produce uploadable `.pkg` files:

```shell
./build-dmg.sh appstore both 1.2.3 \
  "Apple Distribution: Lachmann Thiem Software GbR (2G3HGCLNFN)" \
  "3rd Party Mac Developer Installer: Lachmann Thiem Software GbR (2G3HGCLNFN)"
```

## Access to protected locations

macOS can block an app from reading a location, and the remedy differs per
distribution channel. In both cases Atlas Storage Analyzer shows guidance that the
result may be incomplete, keeps the detailed errors visible, and offers a
rescan. Inaccessible paths are not purgeable space, free space, or files that
Atlas Storage Analyzer says are safe to delete.

The **GitHub Release build** is not sandboxed. macOS can block protected
locations such as some Mail, Messages, Safari, Time Machine, and administrative
data. Grant access manually in **System Settings > Privacy & Security > Full
Disk Access**, add or enable Atlas Storage Analyzer, restart the app if macOS asks,
then rescan the same location.

The **Mac App Store build** runs in the macOS App Sandbox and can only read
folders and volumes you select in the open panel. Full Disk Access does not
change that, so the guidance asks you to select the missing location and rescan
instead, and the Full Disk Access action is not shown.

Trash and Finder reveal use macOS file and workspace APIs directly, so both work
in the sandboxed build without Apple event automation.

## Project structure

```text
src/
  MacStorageAtlas.App              Avalonia UI and MVVM shell
  MacStorageAtlas.Core             disk scanning and domain logic, grouped by domain folder
  MacStorageAtlas.Rendering        treemap layout logic
  MacStorageAtlas.Platform.Mac     macOS-specific integrations (reveal, trash, dock icon, access guidance)

tests/
  MacStorageAtlas.Core.Tests       Core NUnit tests mirroring Core domain folders
  MacStorageAtlas.Rendering.Tests  Rendering NUnit tests
  MacStorageAtlas.Platform.Mac.Tests macOS integration NUnit tests
  MacStorageAtlas.App.Tests        App and ViewModel NUnit tests
  MacStorageAtlas.Benchmarks.Tests benchmark tooling NUnit tests

tools/
  MacStorageAtlas.Benchmarks       developer scan benchmark command
```

## Documentation

- Public support page: [`docs/support.html`](docs/support.html)
- Product backlog and feature specifications: [`docs/FEATURES.md`](docs/FEATURES.md)
- Market-driven implementation roadmap: [`docs/IMPLEMENTATION_ROADMAP.md`](docs/IMPLEMENTATION_ROADMAP.md)
- Storage measurement semantics and verification: [`docs/STORAGE_MEASUREMENT.md`](docs/STORAGE_MEASUREMENT.md)
- Troubleshooting: [`docs/TROUBLESHOOTING.md`](docs/TROUBLESHOOTING.md)
- Scan benchmark commands and representative results: [`docs/SCAN_BENCHMARKS.md`](docs/SCAN_BENCHMARKS.md)
- OpenSpec feature workflow: [`docs/OPENSPEC_WORKFLOW.md`](docs/OPENSPEC_WORKFLOW.md)
- macOS packaging and distribution: [`docs/PACKAGING.md`](docs/PACKAGING.md)
- Manual update acceptance and isolated test fixtures: [`docs/UPDATE_TESTING.md`](docs/UPDATE_TESTING.md)

## License

Released under the [MIT License](LICENSE).

---

<sub>Keywords: macOS disk usage analyzer · disk space analyzer for Mac · what is
using my disk space on Mac · free up disk space macOS · WinDirStat for Mac ·
DaisyDisk alternative · GrandPerspective alternative · treemap disk visualizer ·
find largest files Mac · Apple Silicon disk cleanup · open source Mac storage
tool.</sub>
