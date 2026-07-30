## Why

Every completed scan materializes a view model for every node in the result
tree, and every change to the search text rebuilds that tree synchronously on
the UI thread. On a home folder or volume scan this means hundreds of thousands
of allocations at scan completion and again on each keystroke, which makes the
result view feel slow exactly when the scan was largest.

This is a prerequisite for WP-04 in
[`docs/IMPLEMENTATION_ROADMAP.md`](../../../docs/IMPLEMENTATION_ROADMAP.md).
Advanced filters replace one search box with many criteria, multiplying the
number of triggers that rebuild the tree. Fixing the rebuild cost first keeps
that change reviewable on its own terms and avoids compounding an existing
performance problem.

It also brings the folder tree in line with two standing engineering
conventions: keep expensive work off the UI thread, and avoid holding duplicate
full copies of the scan tree.

## What Changes

- Materialize folder-tree view models lazily. A node's children are created when
  the node is first expanded rather than when the tree is built.
- Rebuild the folder tree off the UI thread with cancellation, and marshal the
  resulting state back through the existing UI-dispatcher abstraction.
- Debounce search-text input so that a burst of keystrokes produces one rebuild
  rather than one per character.
- Abandon a superseded rebuild so that only the most recent search text
  determines the displayed tree.
- Preserve all observable folder-tree behavior: the same nodes match, the same
  ancestors are shown, expansion state behaves as before, and selection
  continues to clear on a search change.

## Non-goals

- Changing which items match the search text. Matching stays a case-insensitive
  substring test over name and path.
- Adding filter criteria. That is `add-advanced-filters`.
- Changing scanning, measurement, metadata capture, or any result view other
  than the folder tree.
- Introducing UI virtualization in the AXAML layer beyond what the existing
  `TreeView` already provides.
- Building a flat file index shared across result views.

## Capabilities

### New Capabilities

- `result-tree-browsing`: How the folder tree presents a completed scan result
  as the user searches and expands it, including which items are matched, that
  the displayed tree reflects the most recent search text, and that browsing a
  large result stays responsive.

### Modified Capabilities

<!-- No existing capability's requirements change. Measurement, metadata, and
     scan behavior are untouched. -->

Lazy child materialization is deliberately not specified. It is an
implementation choice serving the responsiveness requirement, and pinning it in
a spec would constrain future work without describing anything the user can
observe.

## Impact

- `MacStorageAtlas.App`: `DiskItemTreeNodeViewModel` gains lazy child
  materialization; `DiskItemTreeFilter` returns a lazily expandable tree;
  `MainWindowViewModel` gains debounced, cancellable, off-thread tree rebuilds
  in place of the synchronous `ApplySearch` path.
- `MacStorageAtlas.Core`, `MacStorageAtlas.Rendering`,
  `MacStorageAtlas.Platform.Mac`: unchanged.
- `MacStorageAtlas.Tests`: `DiskItemTreeFilterTests` and
  `MainWindowViewModelTests` gain coverage for lazy materialization, debouncing,
  superseded rebuilds, and cancellation, alongside the existing behavioral
  assertions which must continue to pass unmodified where they describe
  observable behavior.
- No user-visible behavior change, so no documentation update is expected beyond
  the roadmap note.

## Dependencies

None. This change stands alone and can land before, after, or without
`add-advanced-filters`, though landing it first is the intent.

## Risks

- Lazy materialization can change expansion or selection behavior subtly.
  Mitigated by keeping the existing folder-tree tests unmodified where they
  assert observable behavior, so a regression fails the build.
- Moving rebuilds off the UI thread introduces the possibility of a stale
  rebuild overwriting a newer one. Mitigated by cancelling the in-flight rebuild
  on each new input and discarding superseded results.
- Debouncing adds latency between typing and results. Mitigated by keeping the
  interval short enough to feel immediate.

## Estimate

1-2 days. This is not a separately estimated roadmap work package; it is
preparatory work carved out of the WP-04 estimate.
