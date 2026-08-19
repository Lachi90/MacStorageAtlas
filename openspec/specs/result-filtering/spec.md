## Purpose

Define how MacStorageAtlas narrows a completed scan result by size, age, type,
text, and shared-storage status; how a date bound is expressed either as a fixed
instant or as an offset from the time the filter is evaluated; how filtered
results are presented across the result views; how match totals and exclusions
are reported; how filter presets are defined, applied, and persisted; and how the
applied preset and any edited criteria are reported, without rescanning or
changing the scan's measurement basis.

## Requirements

### Requirement: Filters narrow a completed scan result without rescanning

MacStorageAtlas SHALL apply result filters to the completed scan result already held in memory. Applying, changing, or clearing a filter MUST NOT start a new scan, revisit the filesystem, read file contents, or change the scan's measurement mode.

#### Scenario: Applying a filter does not rescan

- **GIVEN** a completed scan result is displayed
- **WHEN** the user applies a result filter
- **THEN** MacStorageAtlas narrows the displayed results from the existing scan result
- **AND** it does not start a new scan
- **AND** it does not read file contents

#### Scenario: Clearing a filter restores the full result

- **GIVEN** a completed scan result is displayed with an active filter
- **WHEN** the user clears the filter
- **THEN** the full completed scan result is displayed again
- **AND** no rescan occurs

#### Scenario: Filtering preserves the measurement basis

- **GIVEN** a completed scan result was produced with a selected measurement mode
- **WHEN** the user applies a result filter
- **THEN** every displayed byte value remains based on that completed scan's measurement mode
- **AND** the measurement basis label is unchanged

### Requirement: Filters match files and combine with AND semantics

MacStorageAtlas SHALL evaluate every filter criterion against files. A file MUST be treated as matching only when it satisfies every active criterion. Directories MUST NOT be evaluated against size, age, extension, category, or shared-storage criteria.

#### Scenario: All active criteria must hold

- **GIVEN** a filter specifies a minimum size and a file extension
- **WHEN** the filter is applied
- **THEN** only files satisfying both the minimum size and the extension are matched
- **AND** a file satisfying only one of the two criteria is not matched

#### Scenario: A directory is not matched by a size criterion

- **GIVEN** a directory whose aggregated size exceeds a filter's minimum size
- **AND** the directory contains no file that satisfies the filter
- **WHEN** the filter is applied
- **THEN** the directory is not reported as a match
- **AND** the match count does not include the directory

#### Scenario: Text matches name or path

- **GIVEN** a filter specifies a text term
- **WHEN** the filter is applied
- **THEN** a file whose name contains the term is matched
- **AND** a file whose path contains the term is matched
- **AND** matching ignores letter case

#### Scenario: An empty filter matches every file

- **GIVEN** a filter specifies no active criteria
- **WHEN** the filter is applied
- **THEN** every file in the completed scan result is matched
- **AND** the displayed results are equivalent to the unfiltered result

### Requirement: Directories appear only as ancestors of matching files

MacStorageAtlas SHALL display a directory in a filtered folder tree only when at least one of its descendant files matches the filter. Displayed ancestor directories MUST remain selectable so that existing per-item actions stay available.

#### Scenario: Ancestors of a match remain visible

- **GIVEN** a matching file is nested inside several directories
- **WHEN** the filter is applied
- **THEN** each ancestor directory of that file is displayed in the folder tree
- **AND** the matching file is reachable through those ancestors

#### Scenario: Directories without matching descendants are hidden

- **GIVEN** a directory contains no descendant file that matches the filter
- **WHEN** the filter is applied
- **THEN** that directory is not displayed in the filtered folder tree

#### Scenario: A displayed ancestor can still be selected

- **GIVEN** a filtered folder tree displays an ancestor directory
- **WHEN** the user selects that directory
- **THEN** the selected-item details show that directory
- **AND** existing per-item actions remain available for it

### Requirement: Size criteria use the size basis shown for results

MacStorageAtlas SHALL compare minimum and maximum size criteria against the same size value that the result views display for a file. Size bounds MUST be inclusive.

#### Scenario: Size bounds are inclusive

- **GIVEN** a filter specifies a minimum size equal to a file's displayed size
- **WHEN** the filter is applied
- **THEN** that file is matched

#### Scenario: A file whose counted size is zero is excluded by a minimum size

- **GIVEN** a completed scan counted a file's storage against another path that shares it
- **AND** the file's displayed counted size is zero
- **WHEN** a filter specifies a minimum size greater than zero
- **THEN** that file is not matched

### Requirement: Age criteria exclude results with unknown dates

MacStorageAtlas SHALL resolve every date criterion to an absolute instant before evaluating it, and SHALL evaluate creation, modification, and last-access criteria against the scan-time metadata retained for a file. When a required date is unavailable for a file, MacStorageAtlas MUST exclude that file from the matches rather than assume, infer, or substitute a date.

#### Scenario: A file with an unknown required date is excluded

- **GIVEN** a filter specifies a modification-date criterion
- **AND** a file's scan-time metadata has no modification date
- **WHEN** the filter is applied
- **THEN** that file is not matched
- **AND** no date is invented for that file

#### Scenario: Unknown-date exclusions are reported

- **GIVEN** a filter specifies a date criterion
- **AND** some files were excluded only because the required date was unknown
- **WHEN** the filter is applied
- **THEN** MacStorageAtlas reports how many files were excluded because a required date was unknown

#### Scenario: An unknown date does not exclude a file when no date criterion is active

- **GIVEN** a filter specifies no date criterion
- **AND** a file's scan-time metadata has no dates
- **WHEN** the filter is applied
- **THEN** that file is evaluated against the remaining criteria only
- **AND** the missing dates do not exclude it

#### Scenario: A resolved criterion is compared the same way regardless of its form

- **GIVEN** two filters specify the same modification instant, one as an absolute instant and one as an offset that resolves to that instant
- **WHEN** both filters are applied to the same completed scan result
- **THEN** both match the same files
- **AND** both report the same unknown-date exclusion count

### Requirement: Date criteria can be expressed relative to evaluation time

MacStorageAtlas SHALL allow each creation, modification, and last-access bound to be expressed either as an absolute instant or as an offset from the time the filter is evaluated. An offset MUST consist of a positive count and a unit of days, weeks, months, or years, and MUST be interpreted as that span before the reference time. Each bound MUST carry its own choice of absolute or relative form, independently of the other bounds. A relative bound MUST be stored as the offset itself and MUST NOT be stored as the instant it resolved to.

#### Scenario: A relative bound resolves against the reference time

- **GIVEN** a filter specifies that a file was modified more than 18 months before the reference time
- **WHEN** the filter is evaluated
- **THEN** the modification bound is the instant 18 months before the reference time
- **AND** files modified before that instant are matched

#### Scenario: The same relative bound resolves differently as time passes

- **GIVEN** a filter specifies a modification bound of one year before the reference time
- **WHEN** the filter is evaluated at one reference time and then again at a later reference time
- **THEN** each evaluation uses a bound one year before its own reference time
- **AND** the offset recorded in the filter is unchanged by either evaluation

#### Scenario: Absolute and relative bounds combine on the same date dimension

- **GIVEN** a filter specifies an absolute earliest modification instant
- **AND** the same filter specifies a latest modification bound of 30 days before the reference time
- **WHEN** the filter is evaluated
- **THEN** only files modified within the resulting span are matched

#### Scenario: A month or year offset that overruns the month length is clamped

- **GIVEN** a filter specifies a bound of one month before the reference time
- **AND** the reference time falls on a day that the earlier month does not have
- **WHEN** the filter is evaluated
- **THEN** the bound resolves to the last day of that earlier month
- **AND** the resolved instant keeps the reference time's time of day

#### Scenario: A non-positive offset is rejected

- **GIVEN** the user enters a relative bound whose count is zero or negative
- **WHEN** the filter is evaluated
- **THEN** MacStorageAtlas reports that the filter is invalid and explains why
- **AND** it does not present the state as a result with no matches

#### Scenario: A resolved relative bound is reported alongside its offset

- **GIVEN** a filter specifies a relative date bound
- **WHEN** the active filter is described to the user
- **THEN** the description states the offset
- **AND** it states the instant that offset resolved to

### Requirement: Results can be filtered by extension, category, and shared storage

MacStorageAtlas SHALL support filtering files by file extension, by a named file category derived from extension, and by whether a file's storage is counted against another path in the same scan.

#### Scenario: Extension criterion matches regardless of case

- **GIVEN** a filter specifies an extension
- **WHEN** the filter is applied
- **THEN** files with that extension are matched regardless of the letter case used in the file name or the criterion

#### Scenario: Category criterion matches every extension it covers

- **GIVEN** a filter specifies a file category
- **WHEN** the filter is applied
- **THEN** files whose extension belongs to that category are matched
- **AND** files whose extension belongs to no selected category are not matched

#### Scenario: A file without an extension is categorized explicitly

- **GIVEN** a file has no extension
- **WHEN** a filter specifies any file category
- **THEN** that file is not matched by a category that covers only known extensions

#### Scenario: Shared-storage criterion selects results counted elsewhere

- **GIVEN** a completed scan identified files whose storage is counted against another path
- **WHEN** a filter requests only results whose storage is shared
- **THEN** only those files are matched
- **AND** files whose storage is counted against themselves are not matched

### Requirement: Filtered results are presented consistently across result views

MacStorageAtlas SHALL apply the active filter to the folder tree, the largest-files list, and the file-type summary so that all three describe the same matched files. The treemap MUST retain its full unfiltered layout and MUST distinguish matching areas without removing non-matching areas.

#### Scenario: List views show matches only

- **GIVEN** a filter is active
- **WHEN** the user views the largest-files list and the file-type summary
- **THEN** both describe only matching files
- **AND** both are consistent with the filtered folder tree

#### Scenario: The treemap keeps true proportions

- **GIVEN** a filter is active
- **WHEN** the user views the treemap
- **THEN** the treemap still represents the full unfiltered contents of the displayed level
- **AND** areas remain proportional to the unfiltered sizes

#### Scenario: Matching treemap areas are distinguishable without relying on color alone

- **GIVEN** a filter is active
- **WHEN** the user views the treemap
- **THEN** matching areas are visually distinguished from non-matching areas
- **AND** the distinction does not rely on color alone
- **AND** the number of matches is also stated as text

### Requirement: Match totals and matched directory subtotals are reported

MacStorageAtlas SHALL report the number of matching files and their total matched size while a filter is active. When a filtered folder tree displays an ancestor directory, MacStorageAtlas MUST show the total matched size of that directory's descendants and MUST label it so that it is not read as the directory's full size.

#### Scenario: Match count and total are shown

- **GIVEN** a filter is active
- **WHEN** the filtered result is displayed
- **THEN** MacStorageAtlas shows the number of matching files
- **AND** it shows the total matched size

#### Scenario: A directory row shows its matched subtotal

- **GIVEN** a filtered folder tree displays a directory whose descendants include matching and non-matching files
- **WHEN** the user views that directory row
- **THEN** the row shows the total matched size of its descendants
- **AND** the displayed value is labelled as a matched size rather than the directory's full size

#### Scenario: Full size stays available for a filtered directory

- **GIVEN** a filtered folder tree displays a directory row showing a matched subtotal
- **WHEN** the user selects that directory
- **THEN** the selected-item details show the directory's full scanned size
- **AND** the full size remains based on the completed scan's measurement mode

#### Scenario: Match totals are absent without a filter

- **GIVEN** no filter is active
- **WHEN** the result is displayed
- **THEN** directory rows show their full scanned size
- **AND** no matched subtotal replaces it

### Requirement: Invalid filters are distinguished from empty results

MacStorageAtlas SHALL distinguish a filter that cannot be satisfied by construction from a valid filter that matches nothing. A filter's date bounds MUST be checked after they are resolved to absolute instants. An invalid filter MUST be reported as invalid and MUST NOT be presented as a zero-match result.

#### Scenario: Minimum size above maximum size is invalid

- **GIVEN** the user enters a minimum size greater than the maximum size
- **WHEN** the filter is evaluated
- **THEN** MacStorageAtlas reports that the filter is invalid and explains why
- **AND** it does not present the state as a result with no matches

#### Scenario: Resolved date bounds in the wrong order are invalid

- **GIVEN** the user enters an earliest and a latest bound on the same date dimension, at least one of them relative
- **AND** the resolved earliest bound is later than the resolved latest bound
- **WHEN** the filter is evaluated
- **THEN** MacStorageAtlas reports that the filter is invalid and explains why
- **AND** it does not present the state as a result with no matches

#### Scenario: A valid filter with no matches shows an empty state

- **GIVEN** a valid filter matches no file in the completed scan result
- **WHEN** the filter is applied
- **THEN** MacStorageAtlas shows an empty result state
- **AND** it reports a match count of zero
- **AND** the completed scan result remains available for clearing the filter

### Requirement: Selection stays consistent with the filtered result

MacStorageAtlas SHALL clear a selected item when a filter change makes that item invisible in the result view that owns the selection. A selection that remains visible MUST be preserved.

#### Scenario: A hidden selection is cleared

- **GIVEN** an item is selected in a result view
- **WHEN** a filter change makes that item invisible in that view
- **THEN** the selection is cleared
- **AND** the selected-item details no longer describe that item

#### Scenario: A visible selection is preserved

- **GIVEN** an item is selected in a result view
- **WHEN** a filter change leaves that item visible in that view
- **THEN** the item remains selected

### Requirement: Filter evaluation stays responsive and cancellable

MacStorageAtlas SHALL evaluate filters without blocking the user interface thread. When filter criteria change while an evaluation is in progress, MacStorageAtlas MUST abandon the superseded evaluation and MUST display only the result of the most recent criteria.

#### Scenario: Rapid criteria changes yield the latest result

- **GIVEN** a filter evaluation is in progress
- **WHEN** the user changes the filter criteria again before it completes
- **THEN** the superseded evaluation does not update the displayed result
- **AND** the displayed result reflects the most recent criteria

#### Scenario: The interface stays responsive during evaluation

- **GIVEN** a completed scan result contains a large number of files
- **WHEN** the user changes filter criteria
- **THEN** the user interface remains responsive
- **AND** the user can continue editing the criteria

### Requirement: Filter presets can be applied and persisted

MacStorageAtlas SHALL provide built-in filter presets and SHALL allow users to save, apply, rename, and delete their own presets through the filter panel. User-created presets MUST persist across application restarts, including any relative date bounds they contain. A built-in preset that constrains age MUST express that constraint as a relative bound so that it does not drift. A stored preset that records a schema version the running application does not support, or that cannot be read, MUST be skipped without preventing the remaining settings from loading. A stored preset whose date bounds were saved as absolute instants MUST continue to be read as absolute instants and MUST NOT be reinterpreted as relative bounds.

#### Scenario: A built-in preset populates the filter

- **GIVEN** built-in presets are available
- **WHEN** the user applies a built-in preset
- **THEN** the filter criteria are set to that preset's criteria
- **AND** the filtered result reflects those criteria

#### Scenario: A user preset survives a restart

- **GIVEN** the user saves the current filter criteria as a named preset
- **WHEN** the application is restarted
- **THEN** that preset is available
- **AND** applying it restores the saved criteria

#### Scenario: A saved relative bound does not drift

- **GIVEN** the user saves a preset whose modification bound is one year before the reference time
- **WHEN** the preset is applied at a later date
- **THEN** the bound resolves to one year before that later date
- **AND** the preset still describes the same span

#### Scenario: A preset saved from a built-in preset's criteria behaves like it

- **GIVEN** the user applies a built-in preset that constrains age
- **AND** saves the resulting criteria unchanged as a user preset
- **WHEN** both presets are applied at a later date
- **THEN** both match the same files

#### Scenario: A preset stored with an absolute bound keeps that bound

- **GIVEN** stored settings contain a preset whose modification bound is an absolute instant
- **WHEN** the preset is loaded and applied at a later date
- **THEN** the bound is that absolute instant
- **AND** it is not converted into a relative bound

#### Scenario: A user preset can be renamed from the filter panel

- **GIVEN** the user has a saved preset
- **WHEN** the user renames it from the filter panel
- **THEN** the preset is listed under the new name
- **AND** its criteria are unchanged
- **AND** the new name persists across an application restart

#### Scenario: A built-in preset cannot be renamed or deleted

- **GIVEN** built-in presets are listed
- **WHEN** the user views a built-in preset
- **THEN** renaming and deleting it are unavailable

#### Scenario: A deleted preset is not restored

- **GIVEN** the user deletes a saved preset
- **WHEN** the application is restarted
- **THEN** that preset is no longer available

#### Scenario: An unreadable stored preset is skipped

- **GIVEN** stored settings contain a preset that cannot be read
- **WHEN** settings are loaded
- **THEN** MacStorageAtlas skips that preset
- **AND** the remaining settings and presets load successfully

#### Scenario: A preset from a newer schema version is skipped

- **GIVEN** stored settings contain a preset recording a schema version the running application does not support
- **WHEN** settings are loaded
- **THEN** MacStorageAtlas skips that preset
- **AND** the remaining settings and presets load successfully

#### Scenario: A preset applies to any scan root

- **GIVEN** a preset defines criteria such as size, age, or category
- **WHEN** the user applies it to a completed scan of any folder
- **THEN** the criteria are evaluated against that scan's results
- **AND** applying the preset does not change the scan root

### Requirement: The applied preset and edited criteria are reported

MacStorageAtlas SHALL report which available preset the current filter criteria correspond to, and MUST report that the criteria have been edited when they no longer correspond to the preset that was applied. MacStorageAtlas SHALL allow the user to replace a user-created preset's criteria with the current criteria. A built-in preset MUST NOT be replaceable.

#### Scenario: An applied preset is identified

- **GIVEN** the user applies a preset
- **WHEN** the filter panel is displayed
- **THEN** MacStorageAtlas identifies that preset as the one the criteria correspond to

#### Scenario: Editing criteria reports an edited state

- **GIVEN** a preset was applied
- **WHEN** the user changes any filter criterion
- **THEN** MacStorageAtlas reports that the criteria have been edited
- **AND** it still identifies which preset they were edited from

#### Scenario: Returning to a preset's criteria clears the edited state

- **GIVEN** a preset was applied and its criteria were then edited
- **WHEN** the criteria are changed back to that preset's criteria
- **THEN** MacStorageAtlas no longer reports an edited state

#### Scenario: A user preset is updated from the edited criteria

- **GIVEN** a user-created preset was applied and its criteria were then edited
- **WHEN** the user chooses to update that preset
- **THEN** the preset's criteria become the current criteria
- **AND** the preset keeps its name
- **AND** the updated criteria persist across an application restart

#### Scenario: A built-in preset cannot be updated

- **GIVEN** a built-in preset was applied and its criteria were then edited
- **WHEN** the filter panel is displayed
- **THEN** updating that built-in preset is unavailable
- **AND** saving the edited criteria as a new user preset remains available

#### Scenario: No preset is identified for criteria that match none

- **GIVEN** the user has entered criteria that correspond to no available preset
- **WHEN** the filter panel is displayed
- **THEN** MacStorageAtlas identifies no applied preset
- **AND** it does not report an edited state

### Requirement: Filtering does not authorize destructive actions

MacStorageAtlas SHALL present filtered results as factual matches only. Filter names, preset names, applied-preset reporting, and match summaries MUST NOT describe matched items as safe to delete. A relative age bound MUST be described by the span it covers rather than by any implied consequence. Filtering MUST NOT bypass, weaken, or pre-authorize the existing confirmation required before moving an item to Trash.

#### Scenario: A preset name states a fact rather than a recommendation

- **GIVEN** a preset selects large or old files
- **WHEN** the user views the preset and the match summary
- **THEN** they describe the matching criteria factually
- **AND** they do not state that the matched items are safe to delete

#### Scenario: A relative bound is described by its span

- **GIVEN** a preset constrains results to files not modified for a span of time
- **WHEN** the user views the preset and the active filter description
- **THEN** they state the span and the instant it resolved to
- **AND** they do not state that the matched items are unused or unneeded

#### Scenario: Trash confirmation still applies to a filtered result

- **GIVEN** a filter is active and an item is selected
- **WHEN** the user chooses to move that item to Trash
- **THEN** the existing recoverable Trash workflow is used
- **AND** the existing confirmation is still required

#### Scenario: Filtering does not act on multiple items

- **GIVEN** a filter matches many files
- **WHEN** the filter is applied
- **THEN** MacStorageAtlas does not delete, move, or modify any matched file
- **AND** it does not select matched files for a bulk action

### Requirement: Filtering preserves privacy

MacStorageAtlas SHALL keep filter criteria, presets, and filtered results local. Filtering MUST NOT transmit paths, names, or match results, and MUST NOT materialize dataless cloud placeholders. Resolving a relative date bound MUST use a local clock and MUST NOT contact a network time source.

#### Scenario: Filtering transmits nothing

- **GIVEN** a filter is applied to a completed scan result
- **WHEN** matches are evaluated and displayed
- **THEN** no path, name, or match result is transmitted off the device

#### Scenario: Resolving a relative bound contacts nothing

- **GIVEN** a filter specifies a relative date bound
- **WHEN** the bound is resolved
- **THEN** MacStorageAtlas uses a local clock
- **AND** it does not contact a network time source

#### Scenario: Filtering does not materialize a cloud placeholder

- **GIVEN** a completed scan result contains a dataless cloud placeholder
- **WHEN** a filter is evaluated against it
- **THEN** MacStorageAtlas uses only the metadata retained from the scan
- **AND** it does not request that a cloud provider download the item
