## Context

The main window top chrome currently uses a vertical container with a right-aligned options/filter/search row above a wrapping command row. At the default window width this creates a two-band toolbar area before the scan results, even though users expect the command surface to read as one toolbar separated from content by a divider.

The affected controls all live in `MacStorageAtlas.App`. The existing bindings, command enablement, flyouts, search focus handling, and status banners should remain intact.

Responsibilities:

- Core: no changes.
- Rendering: no changes.
- Platform.Mac: no changes.
- App: adjust the main window toolbar layout and any direct UI support needed for responsive presentation.
- Tests: verify command behavior remains wired and add focused layout or view tests where practical.

## Goals / Non-Goals

**Goals:**

- Present the main command surface as one toolbar row at the default supported window width.
- Keep a visible divider between the toolbar and the content/status area below it.
- Preserve recognizable command groups: scan commands, selected-item actions, cleanup/export actions, and utility controls.
- Keep the toolbar usable when the available width is too narrow for all full labels.
- Avoid changing command semantics, data flow, filesystem behavior, or storage measurement behavior.

**Non-Goals:**

- No toolbar customization or persisted user layout.
- No redesign of result panels, status banners, dialogs, or flyout contents.
- No new command behavior.
- No changes to minimum supported macOS architecture or platform APIs.

## Decisions

### Use one toolbar container instead of stacked rows

Use a single top toolbar layout that places command groups and utility controls in one horizontal structure. The toolbar container should own the bottom divider so the visual separator is independent of optional status banners below.

Alternative considered: keep the current vertical stack and tune spacing. That would reduce height but still encodes the wrong structure and can continue to render as two toolbar bands.

### Preserve command groups with lightweight separators

Keep scan commands, selected-item actions, cleanup/export actions, and options/filter/search visually grouped. Use divider resources already present in the theme rather than hardcoded colors.

Alternative considered: remove all group separators to save width. That would make the toolbar harder to scan because the current command set mixes scan, navigation, cleanup, filtering, and export actions.

### Prefer compact presentation for lower-priority actions before increasing toolbar height

At wider widths, selected-item, cleanup basket, and export actions should remain visible as direct toolbar buttons when they fit without overlap. At narrower widths, those same lower-priority actions can move into an overflow/menu affordance or otherwise reduce horizontal demand. The primary scan path and search/filter utilities should remain easy to reach.

Alternative considered: allow the toolbar to wrap. Wrapping is simple but recreates the screenshot problem: the toolbar consumes multiple rows and loses a clear relationship between the title bar, toolbar, and content.

### Keep the change App-local

The toolbar should continue binding to the existing `MainWindowViewModel` commands and properties. Any layout-specific state should stay in the App layer and should not leak into Core, Rendering, or Platform.Mac.

Alternative considered: add ViewModel properties for every layout state. That would make the ViewModel aware of presentation details without changing user-facing behavior.

## Risks / Trade-offs

- Full labels may not fit at all supported widths -> Show direct action buttons only once the window is wide enough and use compact overflow for lower-priority commands below that breakpoint.
- Overflow can hide useful commands -> Keep primary scan, options/filter, and search visible; only secondary actions should collapse first.
- Flyout alignment can regress when controls move -> Verify Options and Filters flyouts open from their new positions without clipping.
- Visual layout regressions are not fully covered by unit tests -> Add practical UI-level assertions where possible and perform manual or screenshot verification.

## Migration Plan

Implement the App layout change in one pass, preserving existing command bindings. Rollback is straightforward because no persisted data, APIs, scan results, or settings schemas are changed.

## Open Questions

- Should selected-item cleanup actions remain as labeled buttons at default width, or should basket-related actions collapse first if space is tight?
- Should export commands remain separate buttons, or should they share one export menu to reduce toolbar width?
