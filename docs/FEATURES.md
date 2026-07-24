# MacStorageAtlas Feature Backlog

MacStorageAtlas is a macOS disk usage analyzer inspired by WinDirStat-style tools.

The app helps users understand what consumes storage on their Mac by scanning folders, visualizing disk usage, and showing large files, folders, file types, and scan errors.

> The market-driven implementation sequence for future work is maintained in
> [`IMPLEMENTATION_ROADMAP.md`](IMPLEMENTATION_ROADMAP.md). This file documents
> the original feature specifications and remains the reference for already
> implemented baseline behavior.

## Architecture

```text
src/
  MacStorageAtlas.App              Avalonia UI and MVVM shell
  MacStorageAtlas.Core             disk scanning and domain logic
  MacStorageAtlas.Rendering        treemap layout logic
  MacStorageAtlas.Platform.Mac     macOS-specific integrations

tests/
  MacStorageAtlas.Tests            NUnit tests
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
Implement the Folder Selection feature for MacStorageAtlas.

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
- `MacStorageAtlas.Tests`

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
- `MacStorageAtlas.Tests`

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
- `MacStorageAtlas.Tests`

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
- `MacStorageAtlas.Tests`

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
- `MacStorageAtlas.Tests`

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
- `MacStorageAtlas.Tests`

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

## Purpose

Allow users to find files or folders by name/path.

## Acceptance Criteria

- Search input filters visible items.
- Matching items can be selected.
- Clearing search restores normal view.
- Search is case-insensitive.

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
- App asks for confirmation first.
- App does not permanently delete files.
- UI updates after successful trash operation.
- Errors are shown clearly.

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
- `MacStorageAtlas.Tests`

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
- `MacStorageAtlas.Tests`

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
- `MacStorageAtlas.Tests`

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

- Stop button cancels the active scan.
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

## Purpose

Allow the user to scan the same folder again.

## Acceptance Criteria

- Rescan button starts a new scan for the current folder.
- Existing scan is cancelled or blocked before starting another.
- UI state resets correctly.

## Affected Projects

- `MacStorageAtlas.App`

## Implementation Notes

Reuse the normal scan orchestration path so initial scans and rescans have identical lifecycle behavior.

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
- `MacStorageAtlas.Tests`

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

- `MacStorageAtlas.Tests`
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

- `MacStorageAtlas.Tests`
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
- Signing/notarization steps are documented as future work.

## Affected Projects

- repository root
- `MacStorageAtlas.App`
- `README.md`

## Implementation Notes

Document Apple Silicon and Intel runtime identifiers separately; defer credentials and release automation.

## Codex Prompt

```text
Prepare macOS Packaging Documentation.

Requirements:
- Add documentation for publishing the Avalonia app on macOS.
- Include dotnet publish command examples.
- Document that public distribution should use Developer ID signing and notarization.
- Do not implement signing automation yet.
- Add notes for future DMG creation.
```

---

# 27. App Icon and Branding

## Purpose

Give the app a recognizable product identity.

## Acceptance Criteria

- App has placeholder icon assets.
- App name is consistently MacStorageAtlas.
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
- Ensure the app display name is MacStorageAtlas.
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
- Explain what MacStorageAtlas is.
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

- Files can be measured by allocated size (`st_blocks × 512`) or logical length.
- Allocated (on-disk) measurement is the application default.
- Scanning never materializes/downloads dataless cloud files (stat only).
- Scanner respects `ScanOptions.MeasureAllocatedSize`.
- User can toggle the behavior in scan options.

## Affected Projects

- `MacStorageAtlas.Core`
- `MacStorageAtlas.App`
- `MacStorageAtlas.Tests`

## Implementation Notes

Read the allocated size via a native `stat(2)` P/Invoke on macOS (64-bit-inode
struct layout; `stat$INODE64` entry point on x86_64), falling back to the
logical length on other platforms or on failure. The core library keeps the
logical length as its portable default; the app opts into allocated size.

## Codex Prompt

```text
Implement On-Disk vs. Logical Size.

Requirements:
- Add ScanOptions.MeasureAllocatedSize.
- Measure allocated size via native stat (st_blocks * 512) on macOS.
- Fall back to logical length elsewhere; never download cloud placeholders.
- Default the app to allocated size; add a UI toggle.
- Add tests for allocated-size measurement.
```
