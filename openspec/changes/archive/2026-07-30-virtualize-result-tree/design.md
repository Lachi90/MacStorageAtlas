## Context

`DiskItemTreeNodeViewModel`'s public constructor recursively projects every
descendant of a `DiskItem` into a view model, so a completed scan builds a full
mirror of the scan tree even before any search is applied.
`DiskItemTreeFilter.Filter` then rebuilds that structure for each search, and
`MainWindowViewModel.OnSearchTextChanged` calls it synchronously on the UI
thread for every keystroke.

Two standing conventions are in tension with this: keep expensive work off the
UI thread, and avoid duplicate full copies of the scan tree. The cost also grows
precisely with scan size, so it is worst where the tool is most useful.

`add-advanced-filters` replaces one search box with many criteria. Every
criterion becomes a rebuild trigger, so this cost should be removed before that
change lands rather than alongside it.

## Goals / Non-Goals

**Goals:**

- Stop materializing view models for nodes the user has not expanded.
- Move tree preparation off the UI thread with cancellation.
- Guarantee the displayed tree corresponds to the latest search text.
- Preserve all observable folder-tree behavior.

**Non-Goals:**

- Changing what the search text matches.
- Adding filter criteria.
- Changing any result view other than the folder tree.
- A flat file index shared across result views.

## Decisions

### Decision 1: Lazy children on `DiskItemTreeNodeViewModel`

`Children` becomes lazily produced, materializing child view models on first
access rather than in the constructor. Avalonia's `TreeView` requests children
when a node is expanded, so an unexpanded subtree costs one view model instead
of one per descendant.

The existing internal constructor that accepts a pre-built child list is
retained, because filtered results already know their children and should not
re-derive them.

*Alternative considered:* keeping eager construction and relying on UI
virtualization alone. Rejected because `TreeView` virtualization limits how many
containers are realized, not how many view models are allocated. The allocation
happens before the control is ever consulted.

### Decision 2: Filtered subtrees stay eager, unfiltered subtrees go lazy

When search text is present, the matching set must be known to decide which
ancestors to display, so a filtered rebuild necessarily visits every node and
can build its (much smaller) result eagerly. When search text is absent, no
traversal is needed at all and the whole tree can be lazy from the root.

This split means the expensive case at scan completion, the unfiltered tree, is
the one that becomes free, while filtered results stay small enough that eager
construction is not a concern.

### Decision 3: Debounced, cancellable preparation with last-input-wins

Search text changes are debounced, then preparation runs through `Task.Run`
with a `CancellationToken`. Each new input cancels the in-flight preparation
before starting the next, and a superseded preparation never writes to
`TreeItems`. Results are marshalled back through `IUiDispatcher`.

The debounce interval should be short enough that typing feels immediate. The
intended range is 150-250 ms, and the implementation uses 200 ms.

The scan benchmark tooling cannot inform this choice, or measure tree
preparation at all: `tools/MacStorageAtlas.Benchmarks` references only Core and
Platform.Mac, while the tree view models live in the Avalonia App assembly.
Giving that tool a UI dependency to reach them would invert its dependency
direction for little gain. Tree preparation is instead baselined in the tests
project by counting materialized `DiskItemTreeNodeViewModel` instances, which is
deterministic and machine-independent where wall-clock timing is neither.

*Alternative considered:* incremental or chunked preparation that streams
partial trees into the view. Rejected as unnecessary complexity once preparation
is off the UI thread and cancellable, and because a partially-populated tree is
a confusing intermediate state.

### Decision 4: Selection behavior is preserved exactly

`ApplySearch` clears the tree selection on every search change today. That
behavior is retained rather than improved here, so this change stays a pure
performance change with no behavioral surface. `add-advanced-filters` revisits
selection deliberately, with per-view reconciliation.

## Risks / Trade-offs

- **Lazy materialization changes expansion or selection behavior subtly** →
  Keep the existing `DiskItemTreeFilterTests` and `MainWindowViewModelTests`
  assertions unmodified where they describe observable behavior, so any
  regression fails the build.
- **A stale preparation overwrites a newer result** → Cancel the in-flight
  preparation on each new input and discard superseded results before assigning
  to `TreeItems`.
- **Debouncing adds perceptible latency** → Keep the interval short, and verify
  against the benchmark fixtures rather than choosing a value by feel.
- **Lazy children could be requested from a non-UI thread** → Materialize
  children only in response to view interaction, and assign prepared results
  through `IUiDispatcher`.

## Recorded Result

`DiskItemTreeMaterializationTests` uses a synthetic in-memory tree of depth 5
and branching factor 8, which is 37,449 `DiskItem` nodes.

| Preparation | View models materialized |
| --- | ---: |
| Unfiltered, before | 37,449 |
| Unfiltered, after | 1 |
| Unfiltered, after, fully expanded | 37,449 |
| Filtered to a single leaf, after | 6 |

The before figure is the tree's total node count, because the previous
constructor recursed over every descendant and produced exactly one view model
per `DiskItem`. `ExpandingEveryNodeMaterializesTheWholeTree` pins that same total
independently, so the fully-expanded case still reaches it and no node is lost.

## Migration Plan

No data or settings migration. The change is internal to the App layer and has
no persisted state. Rollback is a straight revert; no other change depends on it,
and `add-advanced-filters` remains functionally correct without it.

## Open Questions

None outstanding. The debounce interval is settled in Decision 3.
