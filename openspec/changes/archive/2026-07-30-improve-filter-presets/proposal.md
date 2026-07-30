## Why

A saved filter preset that contains an age criterion silently changes meaning as
time passes. `DiskItemFilter` can express only absolute instants, so saving the
criteria behind "not modified for one year" freezes a single date. Six months
later the same preset selects files not modified for eighteen months, and the
name it was saved under has become false. The built-in preset of the same name
does not drift, because it is recomputed against the current clock every time the
preset list is rebuilt, which makes the divergence between built-in and
user-saved presets invisible and confusing.

Two smaller defects compound the problem. The shipped `result-filtering` spec
requires that users can rename their presets, `ResultFilterViewModel` implements
`RenamePreset`, and `README.md` tells users the feature exists, but no view
exposes it. And nothing indicates which preset is currently applied or whether
the criteria have since been edited, so a preset list is a set of one-way buttons
rather than a state the user can see and return to.

This is follow-up work on WP-04 in
[`docs/IMPLEMENTATION_ROADMAP.md`](../../../docs/IMPLEMENTATION_ROADMAP.md),
whose preset scope was delivered by the archived `add-advanced-filters` change.
It matters before WP-07 builds a cleanup basket on top of filtered results: an
age preset that drifts always drifts toward matching more files, and those
matches are the input to a bulk workflow.

## What Changes

- Add relative age criteria to the filter model. A date criterion may be
  expressed as an offset from the time the filter is evaluated, as a count and a
  unit of days, weeks, months, or years, instead of as a fixed instant. Relative
  criteria are resolved against a reference time each time the filter is applied,
  so a preset saved as "modified more than 18 months ago" keeps meaning that.
- Let each of the six date criteria independently hold either an absolute instant
  or a relative offset, and surface that choice in the filter panel.
- Persist relative criteria in saved presets under a new preset schema version.
  Presets already stored with absolute dates keep those absolute dates, because
  that is what they were saved to mean. Nothing is silently reinterpreted.
- Redefine the built-in presets that use age in terms of relative criteria, so a
  built-in preset and a user preset saved from identical criteria behave
  identically from that point on.
- Expose preset renaming in the filter panel, satisfying the existing spec
  requirement and the existing `README.md` claim.
- Show which preset the current criteria match, and indicate when the criteria
  have been edited away from the applied preset. Offer updating that preset in
  place from the edited criteria.
- Report a relative criterion in the filter summary as both the offset and the
  instant it resolved to, so a user can see what a preset actually asked for.

## Non-goals

- Changing what an age criterion means once resolved. A relative criterion
  resolves to the same absolute comparison the evaluator already performs.
- Converting or migrating presets that are already stored with absolute dates,
  automatically or through a prompt.
- Reordering, grouping, duplicating, importing, or exporting presets.
- Confirmation before applying a preset over unsaved criteria. The active-preset
  indicator makes the overwrite visible, which was the concern behind the
  question the archived design left open.
- Boolean composition of criteria. Filters remain AND-only, as in WP-04.
- Recomputing an applied filter on a timer or when the system clock crosses a day
  boundary. Resolution happens when the filter is applied or reapplied.
- Any scan behavior change. Filtering still operates on a completed scan result.

## Capabilities

### New Capabilities

None. This change refines behavior already owned by `result-filtering`.

### Modified Capabilities

- `result-filtering`: The requirement covering filter presets gains relative age
  criteria, defines how they resolve at evaluation time, and states that
  previously stored absolute criteria are preserved rather than reinterpreted.
  The age-criteria requirement gains the resolution rule. A new requirement
  covers reporting the applied preset and the edited state, and the preset
  requirement's rename obligation gains a scenario that fixes it to a reachable
  affordance rather than an internal capability.

`file-metadata`, `storage-measurement`, `result-tree-browsing`, and
`scan-performance` are unchanged. Relative criteria alter only how a date bound
is derived, not which metadata is read or how bytes are measured.

## Impact

- `MacStorageAtlas.Core`: a relative-age representation and a date-criterion type
  that carries either an absolute instant or a relative offset; `DiskItemFilter`
  date properties adopt it; `DiskItemFilter.Validate` compares resolved bounds;
  a resolution step runs before evaluation; `BuiltInFilterPresets` expresses its
  age presets relatively and no longer needs a reference time to construct them.
- `MacStorageAtlas.App`: `FilterPresetSettings` gains schema version 2 and
  round-trips relative criteria while still reading version 1 absolute criteria;
  `ResultFilterViewModel` gains per-field absolute-or-relative editing state, an
  applied-preset and edited-state projection, a reachable rename command, and an
  update-in-place command; `MainWindow.axaml` gains the date-mode controls, the
  rename and update affordances, and the applied-preset indication.
- `MacStorageAtlas.Rendering` and `MacStorageAtlas.Platform.Mac`: unchanged.
- `MacStorageAtlas.Tests`: relative-offset resolution across units and boundaries
  including leap days and month-end clamping, validation of mixed absolute and
  relative bounds, version 1 and version 2 preset round-tripping, unreadable
  preset skipping, built-in preset stability across a simulated clock advance,
  rename and update-in-place behavior, and applied-preset and edited-state
  reporting.
- Documentation: `README.md` describes relative age presets and its rename claim
  becomes true; `docs/FEATURES.md` and `docs/index.html` describe presets that do
  not drift; the WP-04 roadmap section records this follow-up.

## Dependencies

- `add-advanced-filters` (WP-04), archived and complete. This change modifies the
  filter model, preset persistence, and filter panel it delivered.
- `add-file-metadata` (WP-03), archived and complete. Age criteria read the
  timestamps it captures, unchanged.

## Risks

- A date criterion type replacing six `DateTimeOffset?` properties touches every
  call site of the filter model, including the evaluator, the validator, the view
  model, and persistence. Mitigated by keeping resolution a single explicit step
  that produces the absolute bounds the evaluator already consumes, so evaluation
  logic and its tests are unaffected.
- Month and year offsets are not fixed durations. Subtracting one month from
  31 March and clamping is a decision users can be surprised by. Mitigated by
  specifying the clamping rule, testing month-end and leap-day boundaries, and
  reporting the resolved instant alongside the offset.
- Preset schema version 2 must load in a build that only understands version 1.
  The existing loader already skips a preset whose recorded version is newer than
  it supports, so an older build hides a version 2 preset rather than
  misinterpreting it. Mitigated by preserving that behavior and testing it.
- An applied-preset indicator invites treating a preset as a saved view of the
  scan. Mitigated by keeping the indicator a statement about criteria only, and
  keeping every preset name factual so no preset implies its matches are safe to
  delete.
- Relative criteria make it easier to keep a broad old-file filter around
  indefinitely. Mitigated by leaving all cleanup behind the existing per-item
  Trash confirmation until WP-07 delivers a reviewed multi-item workflow.

## Estimate

2-4 days.
