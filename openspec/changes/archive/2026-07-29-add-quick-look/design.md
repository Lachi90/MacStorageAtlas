## Context

MacStorageAtlas already retains scan-time metadata on `DiskItem` results and exposes one current `SelectedItem` across the folder tree, treemap, and largest-files views. The App project owns selection state, command enablement, status messages, and Avalonia bindings. The Platform.Mac project already wraps Finder reveal and Trash behavior behind interfaces, using macOS command-line/process integrations rather than bringing UI framework dependencies into Core.

WP-03's metadata details are complete. The remaining inspection gap is native Quick Look preview plus keyboard access that matches macOS conventions. The application should let users preview the selected item before acting, while preserving local privacy, scan semantics, and recoverable cleanup behavior.

## Goals / Non-Goals

**Goals:**

- Add Quick Look preview for the selected result item from every result view that can select an item.
- Add Space as the native keyboard shortcut for Quick Look.
- Add Command-I as a keyboard path to the existing selected-item details surface.
- Keep Core independent of Avalonia, AppKit, and macOS process details.
- Keep Quick Look startup off the scan pipeline and avoid changing `DiskItem` measurement or metadata capture.
- Provide friendly, testable failure handling for missing paths and platform launch failures.
- Preserve support for Apple Silicon and Intel Macs.

**Non-Goals:**

- Embedding `QLPreviewPanel` or another live preview surface inside the Avalonia window.
- Opening files in their default application.
- Adding new metadata fields, content inspection, hashing, duplicate detection, or safety recommendations.
- Changing Trash confirmation, Reveal in Finder behavior, or post-Trash result refresh.
- Adding multi-item Quick Look or cleanup basket behavior.

## Decisions

### Add a platform-neutral Quick Look service

Core will define an interface for requesting Quick Look preview of one path. App will depend on the interface for command wiring and tests. Platform.Mac will provide the macOS implementation.

Alternative considered: call Platform.Mac directly from the ViewModel without a Core interface. That would match the current fallback constructors but would make command behavior harder to test consistently and would weaken the existing pattern where platform operations are abstracted for App logic.

### Use QuickLookUI instead of the qlmanage debug tool

Platform.Mac should present Quick Look through the macOS QuickLookUI framework behind the Quick Look service. This avoids the `[DEBUG]` title shown by the developer-oriented `/usr/bin/qlmanage -p` preview window while keeping App and Core free of platform UI details.

Alternative considered: use `/usr/bin/qlmanage -p`. That launches a Quick Look preview, but its window title is marked `[DEBUG]`, so it is not acceptable as product UI.

Alternative considered: use `/usr/bin/open`. That opens the default application rather than Quick Look, so it does not satisfy the roadmap outcome.

Alternative considered: send Finder a reveal-and-Space automation sequence. That is brittle, steals focus, and can require Accessibility permissions.

### Keep selection as the source of truth

Quick Look and Command-I will operate on the existing `SelectedItem` abstraction. The selection rules that keep tree, treemap, and largest-files selections mutually exclusive should remain unchanged. Command enablement should follow selection presence and avoid previewing when no item is selected.

Alternative considered: query focused controls directly from the view to determine the target item. That creates multiple sources of truth and makes keyboard behavior harder to unit test.

### Keep shortcut handling in the App layer

Avalonia key bindings or view-level input handling should route Space to the ViewModel Quick Look command and Command-I to details presentation. The implementation must avoid stealing Space while the user is typing in text input and should avoid requiring code-behind for business decisions.

Alternative considered: handle all key events in code-behind. That can be necessary for focus edge cases, but the command and target selection should still remain in the ViewModel to preserve testability.

### Represent Command-I as details navigation, not new metadata behavior

Command-I should show the existing selected-item details surface and keep the current selection intact. It should not trigger a filesystem refresh, add a separate info window, or change scan-time metadata semantics.

Alternative considered: create a dedicated item-info window. That duplicates the existing selected-item panel and increases UI surface area without adding inspection value for this change.

## Responsibilities

- Core: Own the Quick Look service interface if the App command needs a portable dependency boundary.
- Rendering: No changes.
- Platform.Mac: Own path validation, QuickLookUI presentation, and platform failure normalization.
- App: Own command state, status messages, keyboard shortcut routing, details-tab selection/focus behavior, and dependency injection.
- Tests: Cover ViewModel command behavior with substitutes, platform service failure behavior with missing paths, and App interaction behavior where practical.

## Risks / Trade-offs

- Quick Look can fail for missing, unsupported, privacy-restricted, removable, or network-backed paths -> Revalidate path existence immediately before launch and report a friendly status message without changing the scan result.
- QuickLookUI interop depends on macOS runtime APIs -> Keep it isolated in Platform.Mac and fail closed with the same friendly status path as other platform failures.
- Keyboard shortcuts can interfere with text input -> Scope shortcuts so Space does not trigger Quick Look while editing search text or other text controls.
- Cloud providers may materialize placeholders in response to system Quick Look -> MacStorageAtlas must not read file contents or request downloads itself, and documentation/specs should describe this as a system-level preview action.
- A richer embedded preview may become desirable later -> The service interface allows a later implementation swap without changing ViewModel behavior.

## Migration Plan

This is an additive UI/platform integration change. There is no persisted data format, scan-result schema, or user setting to migrate.

Rollback can remove the Quick Look service, command, shortcut bindings, and documentation updates while leaving scan metadata, Reveal in Finder, Trash, and storage measurement behavior intact.

## Open Questions

- Which macOS versions should be manually verified for Quick Look behavior before marking WP-03 complete?
- Should a later change add an embedded preview panel after a dedicated AppKit/Avalonia integration spike?
