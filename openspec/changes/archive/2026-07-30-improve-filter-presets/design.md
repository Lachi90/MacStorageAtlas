## Context

`add-advanced-filters` delivered WP-04. `DiskItemFilter` holds six nullable
`DateTimeOffset` bounds, `DiskItemFilterEvaluator` compares them against
`DiskItemMetadata` timestamps, and `FilterPresetSettings` persists them verbatim
at schema version 1.

Three consequences of that shape motivate this change.

`BuiltInFilterPresets.Create(DateTimeOffset referenceTime)` computes
`referenceTime.AddDays(-365)` every time `ResultFilterViewModel.RefreshPresets`
runs, so the built-in age preset tracks the clock. A user preset saved from the
same criteria stores the instant that computation produced, so it does not. Two
presets that look identical diverge from the moment the second is saved.

`ResultFilterViewModel.RenamePreset` is public and tested, but the preset item
template in `MainWindow.axaml` binds only `ApplyPresetCommand` and
`DeletePresetCommand`. `RenamePreset` is not a `[RelayCommand]`, so there is no
command to bind. `README.md` already tells users renaming works.

Nothing records which preset was applied. `ApplyPreset` sets the observable
criteria properties under an `_isApplyingPreset` guard and returns. Because
built-in presets are rebuilt against a fresh clock on every `RefreshPresets`,
their `DiskItemFilter` values are not stable over time, so comparing current
criteria against a built-in preset could not have been a reliable identity test
even if something had tried.

Constraints carried in from the existing design: filtering stays a Core plus
App-view-model operation, `DiskItemFilter` stays serializable independently of UI
controls, evaluation stays off the UI thread and cancellable, and no preset name
or summary may imply that a matched file is safe to delete.

## Goals / Non-Goals

**Goals:**

- A saved age preset keeps meaning the span it was saved to mean.
- A built-in preset and a user preset saved from identical criteria stay
  identical.
- All bounds in one evaluation resolve against one instant.
- Presets already on disk load with their absolute bounds intact, and remain
  loadable by a build that predates this change when they contain nothing new.
- Renaming becomes reachable, satisfying the shipped spec and the shipped README.
- The user can see which preset the criteria correspond to and whether they have
  been edited since.

**Non-Goals:**

- Changing how a resolved bound is compared against file metadata.
- Migrating, converting, or prompting about absolute bounds already persisted.
- Re-resolving an applied filter on a timer or a clock-change notification.
- Preset ordering, grouping, duplication, import, or export.
- Boolean composition beyond AND.

## Decisions

### Decision 1: A date criterion is a two-case record hierarchy in Core

Add to `MacStorageAtlas.Core`:

```text
abstract record DateCriterion
    abstract DateTimeOffset Resolve(DateTimeOffset referenceTime)
    abstract DiskItemFilterValidation Validate()

sealed record AbsoluteDateCriterion(DateTimeOffset Instant) : DateCriterion
sealed record RelativeDateCriterion(int Count, RelativeDateUnit Unit) : DateCriterion

enum RelativeDateUnit { Days, Weeks, Months, Years }
```

`DiskItemFilter`'s six date properties change from `DateTimeOffset?` to
`DateCriterion?`.

Resolution lives on the criterion, so no call site switches on the case. Record
equality gives the applied-preset comparison for free: the synthesized
`EqualityContract` check means an `AbsoluteDateCriterion` never equals a
`RelativeDateCriterion`, and `DiskItemFilter.Equals` keeps working unchanged
because it already compares these properties with `==`.

Considered and rejected: **a single flat record** with a nullable instant and a
nullable count-and-unit pair, exactly one of which is set. Fewer types, but the
invariant is unenforceable and every reader has to re-check it. **Keeping
`DateTimeOffset?` and adding a parallel set of offset properties** to
`DiskItemFilter` was rejected for the same reason, plus it doubles the
validation surface. **`TimeSpan` instead of count-and-unit** was rejected because
a `TimeSpan` cannot express a calendar month or year, which is what users
actually mean by "a year ago".

### Decision 2: Resolution happens once per evaluation, in the evaluator

`DiskItemFilterEvaluator.Evaluate` gains a `DateTimeOffset referenceTime`
parameter. Its private `Criteria` constructor resolves all six bounds to
`DateTimeOffset?` once, before the traversal starts, and the per-file comparison
code is untouched.

This is the property that matters most: six bounds must not straddle a clock
tick. A single reference time threaded into the evaluator makes that structural
rather than a convention. It also keeps resolution off the UI thread, since
`Evaluate` already runs on a background thread from
`MainWindowViewModel.PrepareTreeAsync`.

`FilterResult` gains the reference time it was evaluated at, so the filter
summary can report what a relative bound resolved to without reading the clock a
second time and reporting a different answer.

Considered and rejected: **resolving inside `DiskItemFilter` and passing an
absolute-only `DiskItemFilter` to an unchanged evaluator.** Smallest diff, but
nothing distinguishes a resolved filter from an unresolved one at the type level,
and `FilterResult.Filter` would then hold the resolved copy rather than what the
user actually asked for, which is exactly what the summary needs. **Reading the
clock lazily per bound** was rejected because it reintroduces the straddling
problem.

### Decision 3: Validation splits into a clock-free check and a resolved check

`DiskItemFilter.Validate()` keeps its current signature and checks what does not
depend on the clock: negative sizes, minimum above maximum, a non-positive
relative count, and ordering between two absolute bounds on the same dimension.

A new `DiskItemFilter.Validate(DateTimeOffset referenceTime)` runs those checks
and then compares resolved bounds, catching an earliest bound that resolves later
than its latest bound when at least one of the two is relative.

The split exists because `FilterPresetSettings.TryCreatePreset` validates at
settings-load time and must not reject a stored preset for a resolved-ordering
problem. Whether an offset currently resolves past an absolute bound depends on
when the settings happen to be loaded, and a preset must not vanish from the list
because of the calendar. The view model, which does have a clock, uses the
reference-time overload.

### Decision 4: Month and year offsets use BCL calendar arithmetic and its clamping

`RelativeDateCriterion.Resolve` maps to `AddDays(-Count)`,
`AddDays(-Count * 7)`, `AddMonths(-Count)`, and `AddYears(-Count)`.
`DateTimeOffset.AddMonths` and `AddYears` already clamp an overrunning day to the
last day of the target month, which is the behavior the spec requires, so no
custom arithmetic is introduced. Time of day is preserved.

The consequence to accept: one month before 31 March is 28 or 29 February, so two
consecutive one-month offsets do not compose into a two-month offset. This is
standard calendar behavior and reporting the resolved instant next to the offset
makes it observable rather than mysterious.

### Decision 5: Persistence adds parallel relative properties and versions per preset

`FilterPresetSettings` keeps its six `DateTimeOffset?` properties for absolute
bounds and gains six nullable `RelativeDateCriterionSettings` properties, one per
bound, each holding a count and a unit. At most one of each pair is written.

The JSON shape stays additive. A version 1 file writes
`"ModifiedBefore": "2025-07-30T00:00:00+00:00"` and this code still reads it as an
`AbsoluteDateCriterion`, which is what satisfies the requirement that stored
absolute bounds are not reinterpreted. Replacing `ModifiedBefore` with a nested
object would have turned every version 1 preset into a deserialization failure
that `TolerantFilterPresetListJsonConverter` silently swallows, destroying the
user's presets on first launch.

`CurrentSchemaVersion` becomes 2, but a preset records the lowest version it
needs: 2 only when it carries at least one relative bound, otherwise 1. An
absolute-only preset saved by this build therefore still loads in a build that
predates it. `TryCreatePreset` already returns null when
`SchemaVersion > CurrentSchemaVersion`, so an older build skips a version 2 preset
instead of misreading it, and the tolerant converter keeps the rest of the list.
Unknown `RelativeDateUnit` values are rejected with `Enum.IsDefined`, matching how
`FileCategory` values are already filtered.

Considered and rejected: **always writing version 2.** Simpler, but it silently
breaks downgrade for presets that gained nothing from the new schema.

### Decision 6: Built-in presets become clock-free values

`BuiltInFilterPresets.Create` loses its `referenceTime` parameter.
"Not modified for one year" becomes
`ModifiedBefore = new RelativeDateCriterion(1, RelativeDateUnit.Years)`.

This removes the divergence at its source rather than papering over it, and it
makes the built-in list a stable value, which Decision 7 depends on.
`ResultFilterViewModel` keeps its `_referenceTimeProvider`, now used for
validation, resolution reporting, and nothing else.

### Decision 7: Applied-preset state is name plus structural comparison

`ResultFilterViewModel` records the name of the last applied preset.
`AppliedPreset` is the preset in `Presets` whose `Filter` equals `CurrentFilter`,
found by the existing structural `DiskItemFilter.Equals`. `HasEditedCriteria` is
true when a preset name was recorded and that preset's filter no longer equals
`CurrentFilter`.

Deriving identity from the criteria rather than from a stored selection means
typing criteria that happen to match a preset identifies that preset, and editing
back to a preset's criteria clears the edited state, both of which the spec
requires. The recorded name is only needed to answer "edited from what", and it is
cleared by `ClearFilter` and by `DeletePreset` when the deleted preset is the one
recorded.

`UpdatePresetCommand` replaces the recorded user preset's filter with
`CurrentFilter`, keeps the name, and raises `UserPresetsChanged`, which
`MainWindowViewModel.OnUserPresetsChanged` already turns into a settings save. It
is disabled when no preset is applied, when the criteria are unedited, or when the
applied preset is built in.

Considered and rejected: **a `SelectedPreset` observable set only by
`ApplyPreset`.** It cannot satisfy the return-to-preset-criteria scenario without
a comparison anyway, so the comparison is the primitive and the stored name is the
addendum.

### Decision 8: Each date row carries its own mode toggle

The filter flyout already groups dates into Modified, Created, and Last accessed,
each with an After and a Before row bound to a `DatePicker`. Each row gains a mode
selector; picking Relative swaps the `DatePicker` for a numeric count and a unit
`ComboBox`, and shows the resolved instant as caption text.

Twelve new controls in a 420-pixel flyout is the cost. It is contained because the
rows already exist and only their editor changes, and the flyout is already inside
a `ScrollViewer` with `MaxHeight="560"`. Each new control gets an
`AutomationProperties.Name`, matching the existing rows, and the resolved-instant
caption is text rather than a tooltip so it is available to assistive technology.

`ResultFilterViewModel` exposes one small observable object per bound rather than
three flat properties per bound, so the AXAML binds a row to a single object and
the mode-swap logic lives in one place instead of six.

Considered and rejected: **a single global absolute-or-relative mode for all six
bounds.** Far less UI, but it makes "created after a fixed release date and
modified in the last 30 days" unexpressible, which is a natural question.
**Relative-only, dropping absolute bounds.** Rejected because it would break every
stored preset and remove a working capability.

### Responsibility map

- `MacStorageAtlas.Core`: `DateCriterion`, `AbsoluteDateCriterion`,
  `RelativeDateCriterion`, `RelativeDateUnit`; `DiskItemFilter` date property
  types, `Validate` overload, `Equals` and `GetHashCode` adjustments;
  `DiskItemFilterEvaluator.Evaluate` reference-time parameter and resolution in
  `Criteria`; `FilterResult` reference time; `BuiltInFilterPresets` clock removal.
- `MacStorageAtlas.App`: `FilterPresetSettings` version 2 and
  `RelativeDateCriterionSettings`; `ResultFilterViewModel` per-bound editing
  objects, applied-preset and edited state, `RenamePresetCommand`,
  `UpdatePresetCommand`, resolved-instant reporting; `MainWindowViewModel`
  reference-time snapshot passed to the evaluator; `MainWindow.axaml` date rows,
  rename and update affordances, applied-preset indication.
- `MacStorageAtlas.Rendering`: unchanged. Treemap highlighting consumes
  `FilterResult` matches, which are unaffected.
- `MacStorageAtlas.Platform.Mac`: unchanged. No platform integration is involved.
- `MacStorageAtlas.Tests`: unit tests for resolution per unit, month-end and leap
  day clamping, non-positive counts, clock-free and resolved validation, version 1
  and version 2 round-tripping, newer-version and unreadable preset skipping,
  built-in stability across a simulated clock advance, rename and update-in-place,
  and applied-preset and edited-state transitions.

`MainWindowViewModel` reads the clock once per filter application and passes that
instant to both `Evaluate` and the summary. Its constructor gains an optional
reference-time provider defaulting to `DateTimeOffset.Now`, matching the pattern
`ResultFilterViewModel` already uses, so tests can advance the clock without
waiting.

### macOS, performance, and privacy

Nothing here is architecture-specific, so Apple Silicon and Intel behave
identically. Resolution is six arithmetic operations per evaluation against a
traversal that already visits every node, so it is not measurable. `DateTimeOffset`
arithmetic uses the local offset captured in the value, so a preset resolves
against the user's current local time. Resolution reads a local clock only; no
network time source is contacted, and no criterion, preset, or resolved instant
leaves the device.

## Risks / Trade-offs

- **Changing six `DiskItemFilter` property types touches the evaluator,
  validator, view model, persistence, and their tests at once** → Keep resolution
  a single explicit step producing the absolute bounds the per-file comparison
  already consumes, so match semantics and their tests are unchanged, and land
  Core before App so the compiler enumerates the call sites.
- **A month or year offset is not a fixed duration and does not compose** → Use
  BCL clamping rather than custom arithmetic, specify the rule, test month-end and
  leap-day boundaries, and display the resolved instant next to the offset.
- **A version 2 preset is invisible to an older build** → Record the lowest schema
  version a preset needs, so only presets that actually use relative bounds are
  affected, and rely on the existing skip-and-continue loader so the rest of the
  list and the remaining settings still load.
- **Twelve new controls crowd the filter flyout** → Reuse the existing row
  structure, swap only the editor per row, keep the flyout scrollable, and bind
  each row to one view-model object so the AXAML does not grow six copies of the
  same logic.
- **An applied-preset indicator invites reading a preset as a saved view of the
  scan** → Keep the indicator a statement about criteria only, and keep every
  preset name and every relative-span description factual, so nothing implies a
  matched file is safe to delete.
- **Relative bounds make a standing broad old-file filter easy to keep** → Leave
  all cleanup behind the existing per-item Trash confirmation until WP-07 delivers
  a reviewed multi-item workflow.
- **A user who wants a frozen date loses it by choosing Relative** → Absolute
  bounds remain first-class and per-row, and the mode is visible in the panel
  rather than inferred.

## Migration Plan

No data migration runs. Settings written before this change load unchanged:
every date bound is read as an `AbsoluteDateCriterion`, and every preset keeps
schema version 1 until the user edits it into using a relative bound.

Rollback is reverting the change. Presets written afterwards that use relative
bounds record version 2 and are skipped by the reverted build, which leaves the
preset list shorter but every other setting intact; absolute-only presets written
afterwards still record version 1 and load normally.

Landing order that keeps the solution buildable: Core types and resolution, then
evaluator and validation, then persistence, then view model, then AXAML, then
documentation.

## Open Questions

- Should the unit vocabulary include weeks at all, given that a week is exactly
  seven days and the days unit already covers it? Weeks are specified because
  "not accessed in three weeks" reads better than "in 21 days", but dropping the
  unit would remove a case from every resolution and serialization test. This does
  not block implementation.
- Should the resolved instant appear next to every relative row, or only in the
  active-filter summary? The specified behavior requires it in the description of
  the active filter; showing it per row as well is a presentation choice that can
  be reduced if the flyout feels crowded.
