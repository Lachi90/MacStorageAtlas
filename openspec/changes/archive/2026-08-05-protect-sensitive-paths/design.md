## Context

MacStorageAtlas currently has two cleanup entry points. Cleanup basket operations use `CleanupProtectedPathPolicy` before items are accepted into the basket and again during preflight before Trash execution. The single selected-item Move to Trash command does not use that policy; it asks for confirmation, calls `ITrashService`, and reconciles the scan result after success.

That leaves an inconsistent safety boundary. The basket blocks the active scan root, macOS system paths, Trash locations, and paths outside the completed scan result, while single-item Trash can still attempt those paths if selected. The archived `add-cleanup-basket` design also left two open questions that this change answers: which additional paths should be blocked, and whether single-item Trash should share the same protected-path behavior.

## Goals / Non-Goals

**Goals:**

- Make protected-path classification the shared safety contract for all in-app Trash cleanup.
- Keep the classification in Core with no Avalonia or macOS UI dependency.
- Distinguish broad sensitive containers from ordinary descendant files where that distinction keeps cleanup useful.
- Surface blocked cleanup with a clear user-visible reason before confirmation or platform mutation.
- Preserve existing scan, filter, export, Reveal in Finder, Quick Look, Full Disk Access guidance, and recoverable Trash behavior.
- Keep platform Trash execution itemized and unchanged for allowed cleanup paths.

**Non-Goals:**

- Permanent deletion or a force override.
- Hiding protected paths from scan results.
- Preventing users from using Finder, Terminal, or another app to modify their files.
- Reading file contents, hashing files, or probing sensitive data to classify cleanup paths.
- Adding a user-editable allow-list or block-list.
- Changing Rendering or storage measurement semantics.

## Decisions

### Use one Core protected-path classifier for all cleanup entry points

`CleanupProtectedPathPolicy` remains the central classifier. App code will use the policy before selected-item Trash confirmation, cleanup basket addition, and cleanup basket preflight. The classifier will keep returning a reason and message so App can show the same blocked status across workflows.

Alternative considered: add a separate selected-item guard in `MainWindowViewModel`. That would fix the immediate scan-root issue, but it would duplicate path rules in App and make future cleanup workflows harder to keep consistent.

### Expand protection reasons instead of overloading system path

The current reasons are enough for system, Trash, scan-root, and outside-result blocking. This change should add a distinct sensitive-location reason so tests and user messages can distinguish user data containers from macOS system paths.

Alternative considered: classify user home and standard folders as system paths. That would avoid a new enum value, but the message would be inaccurate and would make the policy harder to reason about.

### Treat sensitive paths as tiered rules

The policy should use normalized full paths and metadata already present in the scan tree. It should not enumerate or read the filesystem to discover special directories during classification.

Initial tiers:

- Always block the active scan root exactly.
- Always block paths outside the completed scan result.
- Always block any Trash location path containing `.Trash` or `.Trashes`.
- Always block macOS system subtrees such as `/System`, `/Library`, `/bin`, `/sbin`, `/usr`, `/private`, `/etc`, and `/var`.
- Block the current user's home directory exactly when it appears in the completed scan result.
- Block broad standard user containers exactly when selected as folders: `Desktop`, `Documents`, `Downloads`, `Library`, `Movies`, `Music`, and `Pictures`.
- Block sensitive user Library subtrees such as `Mail`, `Messages`, `Safari`, `Containers`, `Group Containers`, and `Application Support`.

The distinction is intentional: selecting `~/Documents` as a whole is a broad destructive cleanup action and should be blocked in app, while selecting `~/Documents/old.dmg` can remain allowed after normal confirmation. Sensitive Library subtrees are different because moving descendants can corrupt application state, so those subtrees should be protected as prefixes.

Alternative considered: block every descendant under Desktop, Documents, Downloads, Movies, Music, and Pictures. That would protect more aggressively but would make MacStorageAtlas much less useful for normal storage cleanup, where large files often live under those folders.

### Block selected-item Trash before confirmation

The selected-item Move to Trash command should classify the selected item first. If protected, it should set the existing Trash status surface with the policy reason and not show the confirmation dialog. If not protected, it should continue through the current confirmation and `ITrashService` flow.

Alternative considered: show confirmation first and then block. Blocking first is clearer because the app already knows the action cannot proceed, and it avoids asking the user to confirm an operation that will not run.

### Keep platform services unchanged

`MacTrashService` should keep moving an allowed path to macOS Trash through the existing platform boundary. Protection is based on scan context, so it belongs above the platform service rather than inside `MacTrashService`, which only receives a path.

Alternative considered: put sensitive path checks into `MacTrashService`. That would miss the outside-scan-result and active-scan-root checks unless platform code gained scan context, which would violate project boundaries.

## Risks / Trade-offs

- Conservative blocking rejects intentional broad cleanup -> Users can still reveal the item in Finder and act outside MacStorageAtlas.
- Path rules can become stale across macOS releases -> Keep the initial list small, explicit, and covered by tests; extend through focused changes when needed.
- Prefix blocking for Library subtrees may block some recoverable cache cleanup -> Defer cache-specific exceptions until developer-storage insight rules can explain risk and use vendor-supported cleanup paths.
- Environment-specific home paths can vary -> Derive user-container candidates from the scanned path shape and the current user profile path when available, and fall back to normalized path comparisons without failing classification.
- Policy checks could become expensive on large scans -> Reuse the existing scanned-path set and simple normalized string comparisons.

## Migration Plan

1. Extend Core protection reasons and classifier rules with focused tests.
2. Route selected-item Trash through the same policy before confirmation.
3. Update or remove tests that currently expect direct scan-root Trash to clear the scan result.
4. Keep cleanup basket behavior passing while expanding its protected-path coverage.
5. Review and update user-facing documentation for the stricter cleanup boundary.
6. Run the repository validation commands and strict OpenSpec validation.

Rollback can remove the new sensitive-location rules and selected-item preflight call while leaving the existing cleanup basket policy in place.

## Open Questions

- Should `~/Library/Caches` be protected as part of `~/Library`, or remain eligible for future developer-insight cleanup flows with stronger context?
- Should `/Applications` be protected as a broad system-level app container, while still allowing user-selected `.app` bundles outside `/Applications`?
- Should the selected-item button be disabled for protected selections, or remain enabled so pressing it explains why the item is blocked?
