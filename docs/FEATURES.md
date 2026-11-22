# Atlas Storage Analyzer Feature Backlog

Atlas Storage Analyzer is a macOS disk usage analyzer inspired by WinDirStat-style tools.

The app helps users understand what consumes storage on their Mac by scanning folders, visualizing disk usage, and showing large files, folders, file types, and scan errors.

> The market-driven implementation sequence for future work is maintained in
> [`IMPLEMENTATION_ROADMAP.md`](IMPLEMENTATION_ROADMAP.md). This file documents
> the original feature specifications and remains the reference for already
> implemented baseline behavior.

## Architecture

```text
src/
  MacStorageAtlas.App              Avalonia UI and MVVM shell
  MacStorageAtlas.Core             disk scanning and domain logic, grouped by domain folder
  MacStorageAtlas.Rendering        treemap layout logic
  MacStorageAtlas.Platform.Mac     macOS-specific integrations

tests/
  MacStorageAtlas.Core.Tests       Core NUnit tests mirroring Core domain folders
  MacStorageAtlas.Rendering.Tests  Rendering NUnit tests
  MacStorageAtlas.Platform.Mac.Tests macOS integration NUnit tests
  MacStorageAtlas.App.Tests        App and ViewModel NUnit tests
  MacStorageAtlas.Benchmarks.Tests benchmark tooling NUnit tests
```

## Backlog Rules

Each feature should be implemented separately.

Before implementing a feature:

1. Read this file.
2. Read the existing project structure.
3. Keep responsibilities separated by project.
4. Add or update tests where practical.
5. Keep the solution buildable.
6. Do not introduce unrelated changes.

---

# 1. Folder Selection

## Purpose

Allow the user to select a folder or volume to scan.

## Acceptance Criteria

- User can click `Select Folder`.
- A native folder picker opens.
- The selected path is stored in the view model.
- The selected path is visible in the UI.
- Cancelled selection is handled gracefully.
- No scan starts automatically unless explicitly requested.

## Affected Projects

- `MacStorageAtlas.App`

## Implementation Notes

Use Avalonia's storage provider API from the top-level window.

## Codex Prompt

```text
Implement the Folder Selection feature for Atlas Storage Analyzer.

Requirements:
- Use Avalonia's storage provider API to open a native folder picker.
- Add a SelectFolderCommand to the main view model.
- Store the selected folder path in a bindable property.
- Show the selected folder path in the main window.
- Handle cancelled selection gracefully.
- Do not start scanning yet.
- Keep UI logic in MacStorageAtlas.App.
- Add tests where practical for the view model logic.
```

---

# 2. Async Disk Scanner

## Purpose

Scan the selected folder recursively and calculate file and folder sizes.

## Acceptance Criteria

- Scanner can scan a root folder.
- File sizes are aggregated into parent directories.
- Scanner does not crash on inaccessible files.
- Scanner exposes async streaming progress.
- Scanner supports cancellation.

## Affected Projects

- `MacStorageAtlas.Core`
- `MacStorageAtlas.*.Tests`

## Implementation Notes

Implement `IDiskScanner`. Avoid following symbolic links by default.

## Codex Prompt

```text
Implement the Async Disk Scanner feature.

Requirements:
- Implement IDiskScanner in MacStorageAtlas.Core.
- Recursively scan files and directories.
- Aggregate child sizes into parent directory sizes.
- Return progress using IAsyncEnumerable<ScanProgress>.
- Handle UnauthorizedAccessException and IOException by collecting ScanError entries.
- Respect ScanOptions.
- Do not follow symbolic links unless FollowSymbolicLinks is true.
- Support CancellationToken.
- Add NUnit tests using temporary directories.
- Use AAA structure and Assert.That.
```

---

# 3. Scan Progress Reporting

## Purpose

Show the user that scanning is active and provide progress information.

## Acceptance Criteria

- UI displays current scanned path.
- UI displays number of files scanned.
- UI displays number of directories scanned.
- UI displays total bytes scanned.
- UI updates while scan is running.

## Affected Projects

- `MacStorageAtlas.App`
- `MacStorageAtlas.Core`

## Implementation Notes

Consume `IAsyncEnumerable<ScanProgress>` in the view model and marshal UI-bound updates to the UI thread.

## Codex Prompt

```text
Implement Scan Progress Reporting.

Requirements:
- Add scan progress properties to the main scan view model.
- Bind progress values to the main window.
- Consume IDiskScanner.ScanAsync from the view model.
- Update UI while scanning is running.
- Ensure updates happen safely on the UI thread.
- Add a basic IsScanning property.
- Keep scanner logic out of the App project.
```

---

# 4. Permission Error Collection

## Purpose

Track paths that could not be scanned because of missing permissions or IO errors.

## Acceptance Criteria

- Scanner records inaccessible paths.
- UI can display scan errors.
- Scan continues after recoverable errors.
- Error entries include path, message, and exception type.

## Affected Projects

- `MacStorageAtlas.Core`
- `MacStorageAtlas.App`
- `MacStorageAtlas.*.Tests`

## Implementation Notes

Represent recoverable failures as domain data rather than allowing them to terminate enumeration.

## Codex Prompt

```text
Implement Permission Error Collection.

Requirements:
- Ensure scan errors are collected during scanning.
- Include path, message, and exception type.
- Continue scanning after recoverable UnauthorizedAccessException and IOException.
- Expose scan errors to the App view model.
- Add a placeholder error list in the UI.
- Add tests verifying that scan errors do not stop the scan.
```

---

# 4a. Full Disk Access Guidance

## Purpose

Help users understand incomplete macOS scans when protected locations cannot be
read, and guide them to the remedy that fits their build: granting Full Disk
Access manually, or selecting the missing location in the sandboxed Mac App
Store build.

## Acceptance Criteria

- App shows guidance when a completed scan has permission-related inaccessible
  paths.
- Guidance states the scan may be incomplete and shows the inaccessible path
  count.
- Guidance keeps the normal scan errors view visible.
- App can open macOS Privacy & Security settings or show the manual path:
  System Settings > Privacy & Security > Full Disk Access.
- Guidance explains that the user grants access manually and may need to restart
  the app before rescanning.
- App can rescan the same root with the same scan options after access changes.
- Guidance does not treat inaccessible paths as purgeable, free, available, or
  safe-to-delete space.
- App does not request administrator credentials or read file contents for
  access checks.
- In the sandboxed Mac App Store build, guidance asks the user to select the
  missing location, does not mention Full Disk Access, and does not offer the
  settings action.

## Affected Projects

- `MacStorageAtlas.App`
- `MacStorageAtlas.Platform.Mac`
- `MacStorageAtlas.Core`
- `MacStorageAtlas.*.Tests`

## Implementation Notes

Keep scanner errors factual and platform-neutral. Interpret likely Full Disk
Access issues in the App layer, and keep macOS settings and metadata-only access
checks in Platform.Mac.

---

# 5. Folder Tree View

## Purpose

Display scanned folders and files in a hierarchical tree.

## Acceptance Criteria

- Tree shows folder/file names.
- Tree shows formatted size.
- Tree supports nested items.
- Selected tree item is stored in view model.

## Affected Projects

- `MacStorageAtlas.App`
- `MacStorageAtlas.Core`

## Implementation Notes

Project domain items into bindable view-model nodes without adding UI dependencies to Core.

## Codex Prompt

```text
Implement Folder Tree View.

Requirements:
- Display the scanned DiskItem tree in an Avalonia TreeView.
- Show item name and formatted size.
- Bind selection to the main view model.
- Keep view model mapping simple and testable.
- Do not implement sorting in this step unless already available.
```

---

# 6. Sort Folder Tree by Size

## Purpose

Show largest folders and files first.

## Acceptance Criteria

- Tree items are sorted by size descending.
- Directories and files are consistently ordered.
- Sorting happens after scan completion.
- Sorting can be unit tested.

## Affected Projects

- `MacStorageAtlas.Core`
- `MacStorageAtlas.App`
- `MacStorageAtlas.*.Tests`

## Implementation Notes

Keep recursive ordering deterministic by defining a stable tie-breaker such as name or path.

## Codex Prompt

```text
Implement Sort Folder Tree by Size.

Requirements:
- Add a service or helper to sort DiskItem children by SizeBytes descending.
- Apply sorting recursively after scan completion.
- Keep the sorting logic in MacStorageAtlas.Core.
- Add NUnit tests for recursive sorting.
```

---

# 7. File Size Formatting

## Purpose

Display byte sizes in readable units.

## Acceptance Criteria

- Bytes are shown as B, KB, MB, GB, TB.
- Formatting is consistent.
- Edge cases are tested.

## Affected Projects

- `MacStorageAtlas.Core`
- `MacStorageAtlas.*.Tests`

## Implementation Notes

Use a culture-aware formatter and define whether units use binary or decimal thresholds.

## Codex Prompt

```text
Improve File Size Formatting.

Requirements:
- Ensure FileSizeFormatter supports B, KB, MB, GB, TB.
- Use one decimal place for KB and larger.
- Avoid unnecessary decimals for bytes.
- Add NUnit tests for 0 B, bytes, KB, MB, GB, and TB.
```

---

# 8. Treemap Layout Algorithm

## Purpose

Calculate proportional rectangles for disk usage visualization.

## Acceptance Criteria

- Input file/folder sizes generate rectangles.
- Larger items get larger rectangles.
- Rectangles stay within the provided bounds.
- Zero-size items are ignored or handled safely.
- Algorithm is unit tested.

## Affected Projects

- `MacStorageAtlas.Rendering`
- `MacStorageAtlas.*.Tests`

## Implementation Notes

Start with a simple slice-and-dice layout. Later replace it with a squarified treemap.

## Codex Prompt

```text
Implement the first Treemap Layout Algorithm.

Requirements:
- Implement ITreemapLayoutService in MacStorageAtlas.Rendering.
- Use a simple slice-and-dice treemap algorithm for now.
- Accept a list of TreemapItem values and layout bounds.
- Return TreemapRect values.
- Rectangles must not exceed the provided bounds.
- Ignore or safely handle zero-size items.
- Add NUnit tests for proportional layout, bounds safety, and empty input.
```

---

# 9. Treemap Avalonia Control

## Purpose

Render treemap rectangles in the UI.

## Acceptance Criteria

- Treemap control draws rectangles.
- Rectangles are based on layout service output.
- Control updates when selected root changes.
- Large numbers of rectangles do not create thousands of child controls.

## Affected Projects

- `MacStorageAtlas.App`
- `MacStorageAtlas.Rendering`

## Implementation Notes

Draw directly through `DrawingContext`; keep layout calculations independent of Avalonia.

## Codex Prompt

```text
Implement Treemap Avalonia Control.

Requirements:
- Create a custom Avalonia Control for rendering treemap rectangles.
- Draw rectangles directly in Render(DrawingContext).
- Bind a collection of TreemapRect values to the control.
- Do not create one Avalonia control per rectangle.
- Add basic hover or selection preparation if simple.
- Keep layout calculation separate from rendering.
```

---

# 10. Treemap Item Selection

## Purpose

Allow users to click a treemap rectangle and see details.

## Acceptance Criteria

- User can click a treemap rectangle.
- Selected item is shown in the details area.
- Selected item syncs with view model.
- Selection does not crash on empty areas.

## Affected Projects

- `MacStorageAtlas.App`

## Implementation Notes

Perform hit testing against the same rectangle collection used by rendering and define overlap behavior.

## Codex Prompt

```text
Implement Treemap Item Selection.

Requirements:
- Add hit testing to the Treemap Avalonia control.
- Detect which TreemapRect was clicked.
- Expose the selected item via an event or bindable property.
- Update the main view model with the selected item.
- Show selected item name, path, and formatted size in the details area.
- Handle clicks on empty space gracefully.
```

---

# 11. File Type Statistics

## Status

Delivered. With `scope-insights-to-selected-folder`, the File types tab
describes the insight scope: the folder last selected in the folder tree or the
treemap, including its subfolders, or the folder that contains a selected file.
The tab names the scope, offers "Show whole scan", applies an active filter
inside the scope, and states when the scope contains no files.

## Purpose

Summarize disk usage by file extension.

## Acceptance Criteria

- File extensions are grouped.
- Total size per extension is calculated.
- File count per extension is calculated.
- UI displays extension, count, and formatted size.

## Affected Projects

- `MacStorageAtlas.Core`
- `MacStorageAtlas.App`
- `MacStorageAtlas.*.Tests`

## Implementation Notes

Normalize extension casing and define a stable label for files without an extension.

## Codex Prompt

```text
Implement File Type Statistics.

Requirements:
- Add a FileTypeSummary model.
- Add a service that calculates file type statistics from a DiskItem tree.
- Group files by extension.
- Use a special group for files without extension.
- Calculate total size and file count.
- Display the statistics in the bottom panel.
- Add NUnit tests for grouping and size aggregation.
```

---

# 12. Search and Filter

## Status

Delivered. Text search shipped earlier; advanced filters and presets shipped
with WP-04 (`add-advanced-filters`). Relative date bounds, preset renaming in the
filter panel, and applied-preset reporting shipped with
`improve-filter-presets`.

## Purpose

Allow users to find files or folders by name/path, and narrow a completed scan
to an actionable subset.

## Acceptance Criteria

- Search input filters visible items.
- Matching items can be selected.
- Clearing search restores normal view.
- Search is case-insensitive.

## Delivered filter dimensions

- Name and path text.
- Minimum and maximum size, compared against the counted size shown for a
  result, with inclusive bounds.
- Creation, modification, and last-access date ranges.
- File extension.
- File category, from a versioned extension taxonomy covering archives, video,
  images, audio, documents, disk images and installers, and code.
- Shared-storage status, selecting results whose storage is counted against
  another path in the same scan.
- Cloud availability, selecting local files only or cloud-only files only.
  Files whose availability is unknown match neither, and presets saved before
  the criterion existed apply no availability restriction.

Criteria combine with AND. Filters are evaluated against files; directories
appear only as ancestors of matching files.

## Documented non-goals

- OR, NOT, or grouped boolean composition.
- Filtering by hidden status, which the `IncludeHiddenFiles` scan option
  already governs.
- Filtering by application-package membership.
- A file-versus-folder dimension, which files-only matching makes redundant.
- Exporting or acting in bulk on a filtered result. Those belong to WP-05 and
  WP-07.

## Affected Projects

- `MacStorageAtlas.App`
- `MacStorageAtlas.Core`

## Implementation Notes

Keep matching separate from presentation and avoid mutating the source scan tree.

## Codex Prompt

```text
Implement Search and Filter.

Requirements:
- Add a search text input to the toolbar.
- Filter scanned DiskItem entries by name or path.
- Search must be case-insensitive.
- Show matching items in a result list or filtered tree.
- Clearing search restores the full tree.
- Keep search logic testable.
```

---

# 13. Reveal in Finder

## Purpose

Open Finder and reveal the selected file or folder.

## Acceptance Criteria

- User can reveal selected item in Finder.
- Works for files and folders.
- Handles missing paths gracefully.

## Affected Projects

- `MacStorageAtlas.Platform.Mac`
- `MacStorageAtlas.App`

## Implementation Notes

Expose platform behavior through `IFileRevealService` and keep process invocation out of the App project.

## Codex Prompt

```text
Implement Reveal in Finder.

Requirements:
- Wire IFileRevealService into the App project.
- Add a Reveal in Finder command for the selected item.
- Use the macOS implementation from MacStorageAtlas.Platform.Mac.
- Handle missing or deleted paths gracefully.
- Disable the command when no item is selected.
```

---

# 14. Move to Trash

## Purpose

Safely remove unwanted files by moving them to macOS Trash.

## Acceptance Criteria

- User can move selected item to Trash.
- User can collect multiple scanned items into a cleanup basket.
- User can review basket totals and item paths before moving basket items to Trash.
- App blocks protected selected items and protected or stale basket items from cleanup.
- App asks for confirmation first.
- App does not permanently delete files.
- UI updates after successful trash operation.
- Partial failures and errors are shown clearly per affected item.

## Affected Projects

- `MacStorageAtlas.Platform.Mac`
- `MacStorageAtlas.App`

## Implementation Notes

Use a native trash API behind an abstraction; never fall back to permanent deletion.

## Codex Prompt

```text
Implement Move to Trash.

Requirements:
- Add ITrashService abstraction.
- Implement macOS Move to Trash behavior in MacStorageAtlas.Platform.Mac.
- Do not permanently delete files.
- Add a confirmation dialog before moving anything to Trash.
- Disable the command when no item is selected.
- After successful trash operation, remove or mark the item in the UI.
- Show a clear error message if the operation fails.
- Add a cleanup basket for explicit multi-item review.
- Prevent duplicate or overlapping basket entries from overstating totals.
- Block protected selected items and protected, missing, or changed basket items before Trash execution.
- Report partial basket failures without hiding failed or unattempted items.
```

---

# 15. Package Handling for `.app` Bundles

## Purpose

Control whether macOS packages such as `.app` are scanned as folders or treated as single items.

## Acceptance Criteria

- `.app` bundles can be treated as single package items.
- User can toggle package expansion.
- Scanner respects `TreatPackagesAsDirectories`.

## Affected Projects

- `MacStorageAtlas.Core`
- `MacStorageAtlas.App`
- `MacStorageAtlas.*.Tests`

## Implementation Notes

Keep package detection configurable and case-insensitive without introducing a macOS UI dependency into Core.

## Codex Prompt

```text
Implement Package Handling for .app Bundles.

Requirements:
- Respect ScanOptions.TreatPackagesAsDirectories.
- When false, treat .app bundles as package items instead of expanding their children.
- When true, scan .app bundles as normal directories.
- Add a UI toggle for this option.
- Add tests using temporary package-like directories ending in .app.
```

---

# 16. Hidden Files Toggle

## Purpose

Allow users to include or exclude hidden files.

## Acceptance Criteria

- Hidden files are excluded by default.
- User can enable hidden file scanning.
- Scanner respects `IncludeHiddenFiles`.

## Affected Projects

- `MacStorageAtlas.Core`
- `MacStorageAtlas.App`
- `MacStorageAtlas.*.Tests`

## Implementation Notes

Account for Unix dotfiles and available filesystem hidden attributes while keeping behavior testable.

## Codex Prompt

```text
Implement Hidden Files Toggle.

Requirements:
- Respect ScanOptions.IncludeHiddenFiles.
- Exclude hidden files and folders by default.
- Add a UI toggle for including hidden files.
- Add tests for hidden file inclusion and exclusion.
```

---

# 17. Symbolic Link Handling

## Purpose

Avoid accidental recursion loops and misleading size calculations.

## Acceptance Criteria

- Symbolic links are not followed by default.
- User can opt into following symbolic links.
- Scanner avoids cycles.
- Symlink behavior is tested.

## Affected Projects

- `MacStorageAtlas.Core`
- `MacStorageAtlas.App`
- `MacStorageAtlas.*.Tests`

## Implementation Notes

When following links, track canonical filesystem identities or resolved paths to prevent cycles.

## Codex Prompt

```text
Implement Symbolic Link Handling.

Requirements:
- Respect ScanOptions.FollowSymbolicLinks.
- Do not follow symlinks by default.
- If symlinks are followed, avoid cycles.
- Add a UI toggle for following symlinks.
- Add tests for default symlink exclusion.
```

---

# 18. Scan Cancellation

## Purpose

Allow long scans to be stopped.

## Acceptance Criteria

- The toolbar scan control reads Stop while a scan or folder rescan runs and
  cancels it.
- UI exits scanning state.
- Partial results remain visible if available.
- Cancellation does not show as an error.

## Affected Projects

- `MacStorageAtlas.App`
- `MacStorageAtlas.Core`

## Implementation Notes

Own the active `CancellationTokenSource` in the coordinating view model and dispose it between scans.

## Codex Prompt

```text
Implement Scan Cancellation.

Requirements:
- Add a Stop command to the main view model.
- Use CancellationTokenSource for active scans.
- Stop the scan when the user clicks Stop.
- Keep partial results visible if available.
- Do not treat cancellation as a scan error.
- Update IsScanning correctly.
```

---

# 19. Rescan Selected Folder

## Status

The toolbar Rescan, now one state of the scan control from
`streamline-main-toolbar`, is part of the baseline. Rescanning one folder of a
displayed result in place is delivered by `rescan-folder-in-place`. Browsing
during a folder rescan, preserved expansion, and VoiceOver names on macOS still
need manual verification.

## Purpose

Allow the user to scan the same folder again, or to remeasure one folder of the
displayed result without rescanning the rest of the scan root.

## Acceptance Criteria

- The toolbar scan control reads Rescan while a completed result for the
  selected folder is shown and starts a new scan of that folder.
- Existing scan is cancelled or blocked before starting another.
- UI state resets correctly.
- "Rescan this folder" on the Selected item tab, in the item context menu, or
  through Shift-Command-R remeasures the selected directory, including an
  unexpanded package. It is available only while a completed result is shown,
  the selection is a directory, and no scan, export, cleanup operation, or
  update restart is pending. Rescanning the scan root is a full rescan with the
  result's options.
- The folder is measured with the scan options and measurement mode of the
  displayed result, and traversal rules, including firmlink-equivalent
  omission, match a full scan of the original root.
- Ancestor totals, tree order, treemap, filters, Largest files, File types,
  errors, omitted equivalent paths, access guidance, and the completeness
  verdict follow the merged result. The folder stays selected and its
  ancestors stay expanded.
- A folder that no longer exists is removed with a notice. Cleanup basket
  entries inside the folder are revalidated by path, vanished entries are
  removed with a notice that names how many, and duplicate results are cleared.
- Shared-aware allocated results and results scanned with symbolic links
  followed explain why a folder cannot be remeasured on its own and offer a
  confirmed full rescan with the result's options.
- The result stays browsable while a folder rescan runs. Stopping or failing a
  folder rescan leaves the result, the basket, and the history unchanged.
- The result summary states the original scan time and lists each rescanned
  folder with its rescan time. An ancestor rescan supersedes its descendants.

## Affected Projects

- `MacStorageAtlas.Core`
- `MacStorageAtlas.App`

## Implementation Notes

Reuse the normal scan orchestration path so initial scans and rescans have identical lifecycle behavior.

Folder rescans use `IDiskScanner.ScanSubtreeAsync`, which runs the full-scan
traversal from the folder with equivalent-path omission selected for the
original root. `ScanResultMerger` splices the refreshed subtree into the
displayed tree in one UI-dispatcher step, adjusts ancestor rollups, and merges
errors and omissions by path. `ScanResultProvenance` records the original scan
time and the rescanned folders for the summary, history, and export.

## Codex Prompt

```text
Implement Rescan Selected Folder.

Requirements:
- Add a Rescan command to the main view model.
- Rescan the currently selected folder path.
- Prevent two scans from running at the same time.
- Reset progress and errors before a new scan.
- Disable Rescan when no folder is selected.
```

---

# 20. Large Files View

## Status

Delivered. With `scope-insights-to-selected-folder`, the Largest files tab
lists the largest files of the insight scope, up to the usual limit of 100, and
shares the scope caption and "Show whole scan" action with the File types tab.
Selecting an entry in this list or in the Duplicates list keeps the scope.
Moving the scope folder to Trash or rescanning it in place moves the scope to
the same path or its nearest remaining parent folder, and a new scan resets it
to the scan root. Scope changes are computed from the completed result without
reading the filesystem.

## Purpose

Show the largest files found in the scan.

## Acceptance Criteria

- App lists largest files.
- User can configure or use a default limit.
- List shows name, path, and formatted size.
- User can reveal selected large file in Finder.

## Affected Projects

- `MacStorageAtlas.Core`
- `MacStorageAtlas.App`
- `MacStorageAtlas.*.Tests`

## Implementation Notes

Keep extraction in Core and avoid retaining a second full copy of the scan tree.

## Codex Prompt

```text
Implement Large Files View.

Requirements:
- Add a service that extracts the largest files from a DiskItem tree.
- Default to the top 100 largest files.
- Display large files in a dedicated UI list or tab.
- Show name, path, and formatted size.
- Allow Reveal in Finder for selected large files.
- Add tests for largest-file extraction.
```

---

# 21. Scan Error View

## Purpose

Show folders/files that could not be scanned.

## Acceptance Criteria

- Scan errors are visible in the UI.
- User can copy error paths.
- Error count is visible.
- Empty state is shown when there are no errors.

## Affected Projects

- `MacStorageAtlas.App`

## Implementation Notes

Bind directly to the error data exposed by scan orchestration and use Avalonia's clipboard API.

## Codex Prompt

```text
Implement Scan Error View.

Requirements:
- Add a scan errors panel or tab.
- Show path, message, and exception type.
- Show total error count.
- Add an empty state when no errors exist.
- Allow copying the selected error path to clipboard if simple.
```

---

# 22. Basic App Settings

## Purpose

Persist user preferences.

## Acceptance Criteria

- App stores simple settings.
- Settings survive app restart.
- Settings include scanner options.

## Affected Projects

- `MacStorageAtlas.App`

## Implementation Notes

Store version-tolerant JSON under the user's macOS application-data directory and recover safely from malformed files.

## Codex Prompt

```text
Implement Basic App Settings.

Requirements:
- Add a simple settings service.
- Persist settings to a JSON file in the user's application data folder.
- Store scanner options:
  - IncludeHiddenFiles
  - FollowSymbolicLinks
  - TreatPackagesAsDirectories
- Load settings on app startup.
- Save settings when changed.
```

---

# 23. Recent Scan Locations

## Purpose

Allow users to quickly rescan previous locations.

## Acceptance Criteria

- Recent selected folders are stored.
- Recent folders are shown in the UI.
- Missing folders are handled gracefully.
- Duplicate entries are avoided.
- Individual recent folders can be removed without scanning them.
- The entire recent-folder list can be cleared without changing scan history or
  scanner preferences.

## Affected Projects

- `MacStorageAtlas.App`

## Implementation Notes

Persist most-recently-used order through the settings service and compare paths using macOS-appropriate semantics.

## Codex Prompt

```text
Implement Recent Scan Locations.

Requirements:
- Store recently selected folder paths in app settings.
- Show recent locations in the UI.
- Avoid duplicate entries.
- Limit recent entries to 10.
- Allow user to select a recent location for scanning.
- Handle paths that no longer exist.
- Allow user to remove one recent location without scanning it.
- Allow user to clear all recent locations without changing scan history or
  scanner preferences.
```

---

# 24. Unit Tests for Scanner

## Purpose

Ensure scanner behavior stays correct.

## Acceptance Criteria

- Tests cover recursive scanning.
- Tests cover file size aggregation.
- Tests cover hidden files.
- Tests cover package handling.
- Tests cover cancellation where practical.

## Affected Projects

- `MacStorageAtlas.*.Tests`
- `MacStorageAtlas.Core`

## Implementation Notes

Isolate each temporary filesystem fixture and clean it up reliably, including after failed tests.

## Codex Prompt

```text
Add Unit Tests for Scanner.

Requirements:
- Use NUnit and NSubstitute where useful.
- Use temporary directories and files.
- Test recursive scanning.
- Test file size aggregation.
- Test hidden file behavior.
- Test package handling.
- Test cancellation if practical.
- Use AAA structure.
- Use Assert.That and Assert.Multiple.
- Keep tests concise.
```

---

# 25. Unit Tests for Treemap Layout

## Purpose

Ensure treemap layout is deterministic and bounded.

## Acceptance Criteria

- Tests cover empty input.
- Tests cover proportional sizing.
- Tests cover bounds safety.
- Tests cover zero-size items.

## Affected Projects

- `MacStorageAtlas.*.Tests`
- `MacStorageAtlas.Rendering`

## Implementation Notes

Use tolerances for floating-point comparisons and assert invariants rather than implementation details.

## Codex Prompt

```text
Add Unit Tests for Treemap Layout.

Requirements:
- Test empty input.
- Test single item fills the available bounds.
- Test multiple items stay inside bounds.
- Test larger items receive larger area than smaller items.
- Test zero-size items are ignored or safely handled.
- Use NUnit.
- Use Assert.That and Assert.Multiple.
```

---

# 26. macOS Packaging

## Purpose

Prepare the app for distribution on macOS.

## Acceptance Criteria

- App can be published for macOS.
- Build instructions are documented.
- Local Developer ID signing and notarization steps are documented for release
  builds.

## Affected Projects

- repository root
- `MacStorageAtlas.App`
- `README.md`

## Implementation Notes

Document Apple Silicon and Intel runtime identifiers separately. Keep Developer
ID certificates and notarization credentials local to the release machine.

## Codex Prompt

```text
Prepare macOS Packaging Documentation.

Requirements:
- Add documentation for publishing the Avalonia app on macOS.
- Include dotnet publish command examples.
- Document that public distribution should use Developer ID signing and notarization.
- Preserve unsigned local DMG creation for development.
- Document the local signed release flow for distributable DMGs.
```

---

# 27. App Icon and Branding

## Purpose

Give the app a recognizable product identity.

## Acceptance Criteria

- App has placeholder icon assets.
- App name is consistently Atlas Storage Analyzer.
- README contains product description.

## Affected Projects

- `MacStorageAtlas.App`
- repository root

## Implementation Notes

Keep placeholder assets clearly replaceable and avoid treating generated artwork as final branding.

## Codex Prompt

```text
Add App Icon and Branding placeholders.

Requirements:
- Ensure the app display name is Atlas Storage Analyzer.
- Add placeholder icon assets if the project structure supports them.
- Add product name and short description to README.md.
- Do not generate final production artwork.
```

---

# 28. README Setup Documentation

## Purpose

Document how to build, test, and run the project.

## Acceptance Criteria

- README explains prerequisites.
- README explains build command.
- README explains test command.
- README explains run command.
- README explains project structure.

## Affected Projects

- repository root

## Implementation Notes

Keep commands runnable from the repository root and update them when project tooling changes.

## Codex Prompt

```text
Create README setup documentation.

Requirements:
- Add README.md at the repository root.
- Explain what Atlas Storage Analyzer is.
- List prerequisites:
  - macOS
  - .NET 10 SDK
  - Avalonia templates if needed
- Show build command.
- Show test command.
- Show run command.
- Explain project structure.
- Link to docs/FEATURES.md.
```

---

# 29. On-Disk vs. Logical Size

## Purpose

Report the storage a file actually occupies on disk instead of its logical
length, so undownloaded cloud placeholders (iCloud Drive, OneDrive, kDrive) are
not counted at full size.

## Acceptance Criteria

- Files can be measured by logical length, allocated size per path
  (`st_blocks × 512` fallback), or shared-aware allocated size.
- Shared-aware allocated measurement is the application default.
- Device-and-inode identities are counted once within the selected scan scope.
- Every included hardlink path remains browsable and retains its measured
  allocation.
- Verified full-clone data is counted once on capable volumes while non-data
  allocation remains counted per identity.
- Clone-accounting coverage is captured as available, unavailable, or partial.
- Scanning never materializes or downloads dataless cloud files.
- Scanner respects `ScanOptions.MeasurementMode`.
- User can select all three behaviors in scan options.
- Divergent APFS clone extents remain counted separately.
- A scan that includes both a firmlinked path, such as `/Users`, and its
  verified `/System/Volumes/Data` alternate counts that content once at the
  firmlinked path and discloses the omitted alternate.

## Affected Projects

- `MacStorageAtlas.Core`
- `MacStorageAtlas.Platform.Mac`
- `MacStorageAtlas.App`
- `MacStorageAtlas.*.Tests`

## Implementation Notes

Platform.Mac probes mounted-volume clone-mapping capability. On capable volumes
it reads allocation, identity, and full-clone attributes coherently through
public `getattrlist(2)` metadata; unsupported and degraded paths retain the
`stat(2)` fallback and x86_64 64-bit-inode ABI. Core owns scan-scoped identity
and shared-data accounting and treats unavailable required metadata as a
recoverable scan error rather than silently substituting logical length.

## Codex Prompt

```text
Implement On-Disk vs. Logical Size.

Requirements:
- Add explicit logical, per-path allocated, and shared-aware allocated modes.
- Read allocated size and device/inode identity through macOS metadata APIs.
- Count each included identity once and verified full-clone data once where
  supported while retaining all paths.
- Never download cloud placeholders or silently mix measurement bases.
- Default the app to shared-aware allocated size; expose all three choices.
- Disclose clone-accounting coverage and quantitative shared bytes.
- Add unit and macOS integration tests for identity-aware and full-clone
  measurement.
```

---

# 30. Scan Result Export

## Status

Delivered by WP-05 (`export-scan-results`).

## Purpose

Let users analyze, archive, or share a completed scan outside the app, so a
result survives the next scan and can be opened in a spreadsheet or read by a
script.

## Acceptance Criteria

- The current result can be written to a local CSV or JSON file through the
  system save-file interface.
- With no filter active the export contains every scanned file and directory,
  and each directory row reports its subtree totals.
- With a filter active the export contains the matched files only, without
  directory rows, and records the filter that produced it.
- Both formats carry the same flat, one-record-per-item shape.
- Byte fields report the scan's own measurement basis, and every row states
  which mode produced them.
- Scan metadata accompanies every export: schema version, root path, completion
  time, scan options, measurement mode, clone-accounting coverage, scope,
  filter, and totals.
- JSON carries the recoverable scan errors; a CSV export reports their count to
  the user instead.
- Export schema version 2 adds the rescanned folders of a refreshed result, with
  their rescan times, to the JSON metadata. A CSV export of a refreshed result
  reports that the result mixes measurement times. Version 1 JSON exports
  remain readable.
- Export schema version 3 adds each item's cloud availability as an
  `availability` JSON field and an `Availability` CSV column. Version 1 and 2
  JSON exports remain readable and present every item's availability as
  Unknown.
- Row order is fully determined by the result, so exporting twice produces
  identical files.
- CSV parses correctly for paths containing commas, quotation marks, and line
  breaks, displays non-ASCII names correctly, and does not let a leading formula
  character execute.
- JSON preserves exact values and reads back into the same metadata and rows.
- Exports stream, keep the UI responsive, and can be cancelled.
- A cancelled or failed export leaves no partial file at the destination, and an
  existing file there is untouched unless the export completes.

## Documented non-goals

- Exporting the file-type summary or largest-files list as separate documents.
- Importing an export back into the app. Scan history and comparison are WP-09.
- Export presets, scheduled exports, or command-line export.
- Reporting both a logical and an allocated size for one scan.

## Affected Projects

- `MacStorageAtlas.Core`
- `MacStorageAtlas.App`
- `MacStorageAtlas.*.Tests`

## Implementation Notes

Core owns the row and metadata models, row enumeration and ordering, and both
writers, which target a supplied `TextWriter` or `Stream` rather than a path.
The App owns the save-file picker abstraction, the export commands, and atomic
publication through a temporary file in the destination directory.

---

# 31. Scan History

## Status

Persistence delivered by WP-09 (`persist-scan-history`). Comparison between two
recorded scans is a separate, still outstanding change.

## Purpose

Keep a local record of what storage looked like at earlier points in time, so
that a later comparison feature can answer which folders grew or shrank and so
that a user can confirm a cleanup actually held.

## Acceptance Criteria

- Nothing is recorded until the user turns scan history on. It is off by
  default.
- Every completed scan is recorded as one snapshot while history is enabled. A
  cancelled or failed scan is never recorded.
- A snapshot covers the whole scan result and is not narrowed by the filter
  that happens to be active when the scan completes.
- A snapshot records each item's path, name, kind, depth, size fields,
  shared-storage indicator, extension, category, and creation, modification,
  and last-access timestamps.
- A snapshot records the scan root, the completion time, the scan options, the
  measurement mode, the clone-accounting coverage, the recoverable errors, and
  a completeness verdict.
- A snapshot is never truncated to fit a limit. A scan that cannot be recorded
  at full fidelity within the limits is declined with a stated reason.
- Retention bounds the store by snapshots per location and by total store size,
  pruning oldest first and pruning no more than required.
- The user can view stored snapshots grouped by scan root, delete one snapshot,
  and clear the whole history without changing scan options, filter presets, or
  recent locations.
- An unreadable snapshot is reported and remains deletable without breaking the
  rest of the history.
- A completed folder rescan records one snapshot of the whole merged result.
  Snapshots use schema version 2, which records the original scan completion
  time and every rescanned folder with its rescan time. The history marks
  snapshots that contain rescanned folders. Version 1 snapshots remain readable
  and are shown without rescanned folders.
- Snapshots written under schema version 3 also record each item's cloud
  availability. Version 1 and 2 snapshots remain readable and present every
  item's availability as Unknown. A scan with cloud folders that were not
  listed is recorded as a partial scan.

## What a snapshot stores, and where

Snapshots live in `~/Library/Application Support/MacStorageAtlas/history/`, next
to `settings.json`. Each snapshot is one gzip-compressed JSON file named for the
scan's completion time. You can read one directly:

```shell
gunzip -c ~/Library/Application\ Support/MacStorageAtlas/history/<name>.msascan.gz
```

A snapshot stores paths, sizes, and filesystem metadata. It never stores the
contents of any scanned file, and it is never transmitted anywhere.

Because a snapshot lists every file name under the scanned location, the store
is treated as private user data. The directory and its files are created
readable only by their owner, and a `.metadata_never_index` marker keeps
Spotlight from indexing the history itself. Time Machine will still include the
store in its backups if the Application Support directory is backed up.

## Defaults and limits

- Recording is off until enabled.
- At most 10 snapshots are kept per scanned location.
- The store is capped at 500 MB in total.

Both limits are adjustable. Lowering either one prunes immediately rather than
waiting for the next scan.

Full-fidelity snapshots compress well because file paths share long prefixes: a
scan of roughly 500,000 items produces about 12 MB rather than the roughly
150 MB the same document would occupy uncompressed.

## Removing scan history

- Delete a single recorded scan from the history list.
- Clear the whole history from the same list. Clearing asks for confirmation
  first.
- Use **Show in Finder** in the history panel to open the store directly.
  `~/Library` is hidden in Finder by default, so this is the practical way to
  reach the store by hand. The action is unavailable while nothing is recorded.
- Delete the store directly from Finder or the shell. The store is a flat
  directory of independent files with no index, so removing any subset of them
  leaves the rest usable:

  ```shell
  rm -rf ~/Library/Application\ Support/MacStorageAtlas/history
  ```

Removing snapshots outside the app is fully supported. Atlas Storage Analyzer treats a
snapshot that disappears as gone rather than damaged, and recreates the store
the next time a scan is recorded.

Recorded scans are deleted permanently rather than moved to Trash. Clearing
history is usually done for privacy, and moving a complete index of a user's
file names into `~/.Trash` would leave it fully readable on disk.

## Documented non-goals

- Comparing two snapshots, and any added, removed, grown, or shrunk reporting.
  That is the follow-up change to WP-09.
- Move and rename detection. Snapshots match items by path only; stable file
  identity is not recorded, because it exists today only in shared-aware
  allocated mode and would make identity-based behavior silently depend on the
  measurement mode.
- Truncated or summarized snapshots.
- Opening a snapshot as a browsable scan result.
- Automatic or scheduled background scans.

## Affected Projects

- `MacStorageAtlas.Core`
- `MacStorageAtlas.App`
- `MacStorageAtlas.*.Tests`

## Implementation Notes

Core owns the snapshot model, the gzip JSON writer and reader, the retention
policy, and the filesystem store, which takes its directory as a constructor
argument. The App resolves the Application Support location, classifies scan
completeness from the existing access guidance, starts capture off the UI thread
after a completed result is displayed, and cancels a capture in progress when a
new scan starts. Capture writes to a pending file, measures the finished
compressed size, applies retention, and only then publishes by move, so a
cancelled or failed capture never leaves a partial snapshot behind.

# 32. Exact Duplicate Detection

## Status

Delivered by WP-10 (`detect-exact-duplicates`).

## Purpose

Find regular files whose current contents are byte-identical, while avoiding
false-positive cleanup guidance and avoiding unnecessary file reads.

## Acceptance Criteria

- Duplicate analysis starts only after a scan result exists and is cancelled
  independently of scanning.
- Candidates are narrowed by current logical length before any content stream is
  opened.
- Zero-length files are ignored by default.
- Beginning and ending samples are compared before full-content hashing.
- Full-content hashing uses bounded buffers and remains cancellable.
- A final byte-for-byte comparison confirms equality before a group is reported.
- Known hardlinked paths are shown as linked paths instead of reclaimable
  duplicate copies.
- Known not-local cloud placeholders are skipped instead of downloaded.
- Changed, missing, replaced, unreadable, and not-local candidates are reported
  without stopping unrelated analysis.
- Reclaimable totals preserve one retained copy per group.
- Duplicate entries can be selected for Quick Look, Finder reveal, and
  cleanup-basket review through the existing selected-item commands.
- No duplicate copy is automatically selected for cleanup.

## Documented non-goals

- Fuzzy matching, perceptual image or audio matching, and near-duplicate
  detection.
- Proving APFS clone or shared-extent relationships from equal contents.
- Automatically choosing which copy to remove.
- Downloading cloud-only content to complete duplicate analysis.
- Persisting duplicate analysis results in scan history snapshots.

## Affected Projects

- `MacStorageAtlas.Core`
- `MacStorageAtlas.Platform.Mac`
- `MacStorageAtlas.App`
- `MacStorageAtlas.*.Tests`

## Implementation Notes

Core owns duplicate models, progress reporting, candidate grouping, sampling,
streaming hashing, final equality confirmation, hardlink classification, and
skip reporting. Platform.Mac provides current file length, file identity,
hardlink count, local-content availability, and read streams. The App composes
the analyzer after a completed scan, runs it off the UI thread, exposes progress
and cancellation, clears duplicate results when the scan changes, and presents
duplicate groups in a dedicated review tab that feeds the existing selected-item
and cleanup-basket commands.

## Direct-distribution updates

The implementation under `add-in-app-updates` uses Sparkle 2.9.6 for manual and
opt-in scheduled checks, authenticated stable releases, standard user-approved
installation, and App operation/persistence coordination. The existing Store
packaging path excludes the updater from both architectures and keeps updates
managed by the Mac App Store. Deployment status and remaining acceptance are
tracked in WP-01 of `IMPLEMENTATION_ROADMAP.md`; public support starts only after
the first updater-enabled release and signed feeds are published.

# 33. Item Context Menus

## Status

Delivered by WP-03 follow-up `add-item-context-menus`. Pointer and VoiceOver
behavior on macOS still needs manual verification.

## Purpose

Let users act on a scanned item where they see it, as they do in Finder,
instead of selecting it and moving to the toolbar.

## Acceptance Criteria

- Control-click, right-click, or two-finger click on an item in the folder
  tree, the treemap, the Largest files list, or the Duplicates list selects
  that item and opens an item context menu for it.
- Opening the menu over treemap space that shows no block opens no item menu
  and keeps the current selection.
- The menu offers Quick Look (Space), Show details (Command-I), Reveal in
  Finder, Copy path (Command-Option-C), Rescan this folder (Shift-Command-R,
  directories only), Add to cleanup basket, Remove from cleanup basket, and
  Move to Trash, in that order and grouped by separators.
- Each entry invokes the existing selected-item command and is enabled exactly
  when that command is enabled, including the update-restart gate.
- Move to Trash keeps the protected-path check and the Trash confirmation and
  only moves items to the macOS Trash.
- Copy path places the item's full path on the local clipboard as plain text.
  Command-Option-C does the same while no text field has focus and does
  nothing without a selected item.
- Starting a scan clears the selection, which disables every entry, and closes
  an open item menu.
- Every entry has an accessible name and a toolbar or keyboard equivalent.

## Documented non-goals

- Multi-selection or bulk actions.
- Context menus on scan errors, scan history entries, recent locations, the
  cleanup basket review, or file-type rows.
- Permanent deletion.

## Affected Projects

- `MacStorageAtlas.App`
- `MacStorageAtlas.App.Tests`

## Implementation Notes

The window defines one shared `ContextMenu` resource bound to the window's
`MainWindowViewModel` and attaches it through the tree and list item container
styles and to the treemap control. Menu entries bind to the existing commands
without parameters, so enablement, status messages, and safety checks stay
identical to the toolbar. The treemap selects a block on a secondary-button
press and suppresses the menu when no block lies under the pointer. The
`FolderActionsGroup` separator introduces folder-only entries. It holds
"Rescan this folder" from `rescan-folder-in-place` and is hidden with its
separator when the selection is a file.

# 34. Menu Bar Commands

## Status

Delivered by the cross-cutting accessibility follow-up `add-native-menu-bar`
under WP-03. Menus, enablement, and shortcuts in the direct-download and Mac
App Store builds, including VoiceOver reading of menu entries, still need
manual verification on macOS.

## Purpose

Make every main-window command discoverable in the macOS menu bar, where
assistive technologies and the system Help search also find it, and give the
common commands conventional keyboard shortcuts.

## Acceptance Criteria

- The main window adds File, Edit, Scan, Item, View, Window, and Help menus
  next to the application menu. Direct-download builds keep Check for Updates
  and Automatically Check for Updates in the application menu, and the Mac App
  Store build shows no update entry.
- File offers Select Folder… (Command-O), Open Recent with the recent scan
  locations in start-screen order and Clear Menu, Export as CSV… (Command-E),
  and Export as JSON… (Shift-Command-E).
- Edit offers Undo, Redo, Cut, Copy, Paste, and Select All for the focused
  text field.
- Scan offers Scan or Rescan (Command-R), Rescan Folder, Stop
  (Command-Period), and Find Duplicates.
- Item offers Quick Look (Command-Y), Show Details (Command-I), Reveal in
  Finder (Option-Command-R), Copy Path, Add to or Remove from Cleanup Basket,
  and Move to Trash….
- View offers Filter and Search (Command-F), Show Whole Scan, the three size
  measurement modes as radio entries, the application-bundle, hidden-file, and
  symbolic-link scan options as checkbox entries, and Scan History.
- Window offers Minimize (Command-M) and Zoom. Help opens the support page and
  the privacy policy.
- Every entry invokes the same command as its toolbar, context-menu, or panel
  counterpart and is enabled exactly when that command can execute. The Scan
  entry reads Rescan while a completed result for the selected location is
  displayed, and the basket entry names the action it will perform.
- Space (Quick Look), Command-B (add to or remove from the cleanup basket),
  Command-Delete (Move to Trash), Option-Command-C (Copy Path), and
  Shift-Command-R (Rescan Folder) are handled by the window and do nothing to
  scanned items while a text field has focus.
- Move to Trash from the menu or Command-Delete keeps the protected-path check
  and the Trash confirmation and only moves items to the macOS Trash.

## Documented non-goals

- Toolbar layout changes, which belong to `streamline-main-toolbar`.
- A settings window. Command-Comma stays reserved until one exists.
- Localized menu titles.

## Affected Projects

- `MacStorageAtlas.App`
- `MacStorageAtlas.App.Tests`

## Implementation Notes

`MainWindow.axaml` declares the menus as the window's `NativeMenu` with
compiled bindings to `MainWindowViewModel`, which Avalonia merges with the
application menu on macOS. `App.axaml.cs` keeps adding the update entries to
the application menu only when `DIRECT_UPDATES` is defined.

Avalonia turns `NativeMenuItem.Gesture` into the native key equivalent of the
menu item, and macOS dispatches key equivalents to the menu bar before a
focused text field receives the key. A menu item cannot show a shortcut
without registering it. Shortcuts that a text field uses, or that must not act
on an item while a text field has focus, are therefore not registered on the
menu: their entries show no shortcut, and the window's key handling applies
them with the text-field guard. The Edit entries leave the standard text
shortcuts to the text field itself. The item context menu shows Space,
Command-B, Command-Delete, Option-Command-C, and Shift-Command-R next to its
entries.

Native menu check marks do not toggle themselves, so the scan-option and
measurement entries use toggle and selection commands on the ViewModel. The
Open Recent submenu is rebuilt from `RecentLocations` when macOS asks to update
it.

# 35. Main Toolbar

## Status

Delivered by `streamline-main-toolbar` under WP-07. The toolbar layout at the
default, minimum, and a wide window size in light and dark appearance, the
tooltips, and the VoiceOver names still need manual verification on macOS.

## Purpose

Put the most frequent item actions one click away at the default window size
and remove toolbar controls that duplicate each other.

## Acceptance Criteria

- One primary scan control reads Scan while no completed result for the
  selected location is displayed, Rescan while one is, and Stop while a scan
  or folder rescan runs. It uses the accent style for Scan and Rescan and the
  danger style for Stop, and it invokes the command its label names.
- A second activation of the scan control within the same transition has no
  effect, so a double click starts exactly one scan and does not stop it.
- Quick Look, Reveal in Finder, Move to Trash, one cleanup basket toggle, and
  one Export menu are direct icon controls at the default and minimum window
  width of 1060 points.
- Every icon control has a tooltip with its action name and shortcut, shows
  that tooltip while disabled, and has an accessible name that states its
  action.
- The basket toggle adds the selected item or removes it, its icon, tooltip,
  and accessible name name the action it will perform, and it shows the number
  of items in the basket when the basket is not empty.
- The Export menu lists Export as CSV… and Export as JSON… and is enabled
  exactly when export is available.
- Select folder, Options, History, Filters, and Search stay in the single
  toolbar row with the existing group separators. The search field shrinks
  from 220 to 140 points before the row would overflow.
- No command changes when it is enabled or what it does.

## Documented non-goals

- New commands or a redesign of the Options, Filters, or History flyouts.
- Theme color or typography changes, or icons beyond the add-to-basket and
  remove-from-basket icons the toggle needs.
- Toolbar customization.

## Affected Projects

- `MacStorageAtlas.App`
- `MacStorageAtlas.App.Tests`

## Implementation Notes

`PrimaryScanActionCommand` on `MainWindowViewModel` dispatches to
`ScanFolderCommand` or `StopScanCommand` according to `IsScanning` and is
enabled exactly when that target is. It ignores an activation that follows the
previous accepted one within `PrimaryScanActionTransitionInterval` (500
milliseconds, measured with an injectable `TimeProvider`). Folder rescans set
`IsScanning` and share the scan cancellation source, so the same Stop state
cancels them. `RescanCommand` stays for the menu bar.

The basket toggle reuses `ToggleSelectedItemInCleanupBasketCommand` and
`CleanupBasketCommandLabel` from `add-native-menu-bar`. The `icon`,
`toolbar`, `primary-scan`, and `count-badge` styles live in
`Styles/Controls.axaml`.

`MainWindow.FullToolbarActionThreshold` is 1050 points. Measured through
Avalonia layout with the bundled Inter font, the widest single row needs
1041.5 points, with a five-digit basket count, the Rescan label, and the
search field at its 140-point minimum, on Apple Silicon and under Rosetta.
The compact Actions button therefore appears only below the supported minimum
width.

---

# 36. Cloud Availability

## Status

Delivered by `show-cloud-availability` as the local, provider-neutral first
step toward WP-14. The status on iCloud Drive and OneDrive folders in light
and dark appearance and with VoiceOver still needs manual verification on
macOS.

## Purpose

Show which files and folders exist only as cloud placeholders, how much logical
content lives only in the cloud, and which files actually occupy this Mac,
without downloading anything or contacting a provider.

## Acceptance Criteria

- Every scanned file is Local, Cloud only, or Unknown, read from the
  `SF_DATALESS` metadata flag in every measurement mode.
- A folder is Cloud only, Local, Partly local, or Unknown according to the
  files the scan included below it. An empty folder is Local.
- The folder tree and Largest files mark cloud-only and partly local items with
  a cloud icon. Hovering the icon shows the same text as the Availability row,
  including how many files and how many logical bytes are only in the cloud.
  VoiceOver reads the same text. Local items carry no marker.
- The Selected item tab has an Availability row. For a folder or file with
  cloud-only content it also states the cloud-only size.
- The status bar summarizes the whole result as "N files local · M files only
  in the cloud (X logical, not on this Mac)" when the result contains
  cloud-only files. A logical result adds that its total includes this size.
- The cloud-only size is never added to or shown as a measured byte count and
  is never presented as space that can be freed.
- The Filters panel offers local files only and cloud-only files only, and the
  criterion narrows Largest files and File types like the other criteria.
- A folder that itself carries `SF_DATALESS` is never listed, is reported in
  the status bar as a cloud folder that was not listed, and makes the scan
  partial in scan history.
- Snapshots and exports record each item's availability under schema
  version 3.

## Documented non-goals

- Downloading, evicting, pinning, or otherwise changing cloud state.
- Naming the provider or calling a provider API.
- A "free up space" or "remove download" action.
- Comparing two scans.

## Affected Projects

- `MacStorageAtlas.Core`
- `MacStorageAtlas.Platform.Mac`
- `MacStorageAtlas.App`
- Matching test projects

## Implementation Notes

`MacFileMetadataReader` implements `ICloudAvailabilityReader` with `stat(2)`
and adds `ATTR_CMN_FLAGS` and `ATTR_FILE_TOTALSIZE` to its `getattrlist(2)`
read, so allocated modes reuse the existing metadata call. In logical mode the
same `stat(2)` call supplies the logical length. `DiskItem.CloudTotals` keeps
local, cloud-only, and unknown file counts, the cloud-only logical size, and
unlisted cloud folders per directory. The scan adds them while it unwinds, and
folder rescans and Trash update them along the ancestor path. See
[Storage measurement semantics](STORAGE_MEASUREMENT.md#cloud-availability).
