## 1. Core date-criterion model

- [x] 1.1 Add `RelativeDateUnit` with `Days`, `Weeks`, `Months`, and `Years` to `MacStorageAtlas.Core`
- [x] 1.2 Add the abstract `DateCriterion` with `Resolve(DateTimeOffset referenceTime)` and a clock-free `Validate()`
- [x] 1.3 Add `AbsoluteDateCriterion(DateTimeOffset Instant)` returning its instant from `Resolve` and always validating
- [x] 1.4 Add `RelativeDateCriterion(int Count, RelativeDateUnit Unit)` resolving via `AddDays`, `AddDays` times seven, `AddMonths`, and `AddYears`, and reporting a non-positive count as invalid
- [x] 1.5 Add tests for resolution in every unit, for time-of-day preservation, for month-end clamping on a 31-day reference date, for a leap-day reference date, and for zero and negative counts

## 2. Filter model and validation

- [x] 2.1 Change the six `DiskItemFilter` date properties from `DateTimeOffset?` to `DateCriterion?` and update `HasDateCriteria`, `IsActive`, `Equals`, and `GetHashCode`
- [x] 2.2 Update `DiskItemFilter.Validate()` to check each criterion's clock-free validity and to compare two absolute bounds on the same dimension, keeping the existing size checks and messages
- [x] 2.3 Add `DiskItemFilter.Validate(DateTimeOffset referenceTime)` that runs the clock-free checks and then compares resolved bounds on each date dimension
- [x] 2.4 Add tests covering a non-positive relative count, two absolute bounds out of order, a relative bound resolving past an absolute bound, mixed bounds in valid order, and that the clock-free overload never fails on resolved ordering

## 3. Evaluation

- [x] 3.1 Add a `DateTimeOffset referenceTime` parameter to `DiskItemFilterEvaluator.Evaluate` and resolve all six bounds once in the private `Criteria` constructor before traversal
- [x] 3.2 Add the evaluated reference time to `FilterResult`
- [x] 3.3 Update existing evaluator tests to the new signature without changing their expected matches
- [x] 3.4 Add tests asserting that an absolute bound and a relative bound resolving to the same instant match the same files and report the same unknown-date exclusion count, and that the same filter evaluated at two reference times yields bounds relative to each

## 4. Built-in presets

- [x] 4.1 Remove the `referenceTime` parameter from `BuiltInFilterPresets.Create` and express "Not modified for one year" as a one-year relative bound
- [x] 4.2 Remove the now-unused day and year constants and update `ResultFilterViewModel.RefreshPresets` to the parameterless call
- [x] 4.3 Add tests asserting the built-in list is equal across two widely separated reference times and that the age preset resolves relative to whichever reference time it is evaluated at

## 5. Preset persistence

- [x] 5.1 Add `RelativeDateCriterionSettings` with a count and a unit to `MacStorageAtlas.App.Models`
- [x] 5.2 Add six nullable relative properties to `FilterPresetSettings` beside the existing absolute date properties, and raise `CurrentSchemaVersion` to 2
- [x] 5.3 Update `FilterPresetSettings.FromPreset` to write each bound as either its absolute instant or its relative offset, and to record schema version 2 only when at least one relative bound is present
- [x] 5.4 Update `TryCreatePreset` to build `AbsoluteDateCriterion` and `RelativeDateCriterion` values, to reject an undefined `RelativeDateUnit` with `Enum.IsDefined`, to reject a bound that supplies both forms, and to keep using the clock-free `Validate()`
- [x] 5.5 Add tests for a version 2 relative round-trip, a version 1 absolute file loading as absolute bounds, an absolute-only preset saved at version 1, a version 3 preset being skipped while its neighbours load, an undefined unit being rejected, and an unreadable entry being skipped by `TolerantFilterPresetListJsonConverter`

## 6. Filter view model

- [x] 6.1 Add a per-bound observable editing object exposing the absolute-or-relative mode, an absolute instant, a relative count and unit, the resolved instant caption, and a criterion projection, raising the existing criteria-changed notification
- [x] 6.2 Replace the six `[ObservableProperty]` date fields with the six editing objects and update `CurrentFilter` and `ApplyPreset` to read and write them under the existing `_isApplyingPreset` guard
- [x] 6.3 Record the applied preset name in `ApplyPreset`, clear it in `ClearFilter`, and clear it in `DeletePreset` when the deleted preset is the recorded one
- [x] 6.4 Add an `AppliedPreset` projection matching a preset in `Presets` by structural filter equality, and a `HasEditedCriteria` projection true when a recorded preset's filter no longer equals `CurrentFilter`
- [x] 6.5 Convert `RenamePreset` into a `[RelayCommand]` that rejects a built-in preset, a blank name, and leaves criteria untouched
- [x] 6.6 Add an `UpdatePreset` `[RelayCommand]` replacing the recorded user preset's filter with the current criteria, keeping its name, raising `UserPresetsChanged`, and disabled with no applied preset, with unedited criteria, or with a built-in applied preset
- [x] 6.7 Report each active relative bound as both its offset and its resolved instant in the active-filter description, using the view model's reference-time provider
- [x] 6.8 Add tests for applying a preset then identifying it, editing to an edited state, editing back to clear it, entering matching criteria identifying a preset with no edited state, renaming persisting the new name and criteria, updating a user preset persisting the new criteria under the same name, rejecting rename and update for a built-in preset, and a preset saved from built-in criteria matching that built-in at a later reference time

## 7. Result wiring

- [x] 7.1 Add an optional reference-time provider parameter to the `MainWindowViewModel` constructor defaulting to `DateTimeOffset.Now`
- [x] 7.2 Read the reference time once per filter application and pass that instant to `DiskItemFilterEvaluator.Evaluate` and to the match summary at both `CurrentFilter` call sites
- [x] 7.3 Add tests asserting one filter application uses a single reference time for every bound and that advancing the provider changes what a relative preset matches without editing the preset

## 8. Filter panel

- [x] 8.1 Add a mode selector to each of the six date rows in `MainWindow.axaml`, swapping the `DatePicker` for a count `NumericUpDown` and a unit `ComboBox` in relative mode
- [x] 8.2 Show the resolved instant as caption text beside each active relative row
- [x] 8.3 Add `AutomationProperties.Name` to every new control, matching the existing rows
- [x] 8.4 Add rename and update-preset affordances to the preset item template, visible only for user-created presets
- [x] 8.5 Indicate the applied preset and the edited state in the preset list, without relying on color alone
- [x] 8.6 Confirm the flyout still scrolls within its `MaxHeight` and that no row overflows the 420-pixel width

## 9. Documentation

- [x] 9.1 Update `README.md` to describe relative age criteria and presets that do not drift, and verify its existing rename claim is now true
- [x] 9.2 Update `docs/FEATURES.md` where it records advanced filters and presets as delivered
- [x] 9.3 Update the advanced filters entry on `docs/index.html`
- [x] 9.4 Record this follow-up in the WP-04 section of `docs/IMPLEMENTATION_ROADMAP.md`
- [x] 9.5 Review `docs/STORAGE_MEASUREMENT.md` and report that no update is needed, since measurement semantics are unchanged

## 10. Validation

- [x] 10.1 Run `dotnet build MacStorageAtlas.slnx --no-restore`
- [x] 10.2 Run `dotnet test MacStorageAtlas.slnx --no-build`
- [x] 10.3 Run `dotnet format MacStorageAtlas.slnx analyzers --diagnostics IDE0005 --verify-no-changes`
- [x] 10.4 Run `openspec validate --all --strict --no-interactive`
- [x] 10.5 Run `git diff --check`
