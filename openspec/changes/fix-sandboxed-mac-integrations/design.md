## Context

The Mac App Store build is signed with `MacStorageAtlas.AppStore.entitlements`, which
enables `com.apple.security.app-sandbox` and grants only
`com.apple.security.files.user-selected.read-write`. The Developer ID build is not
sandboxed. Both builds share one codebase, so every macOS integration has to work under
the stricter of the two environments.

## Verification evidence

Behavior was measured with an ad-hoc signed probe bundle carrying the App Store
entitlement set, run on macOS 26.6.2, and compared against the same binary without the
sandbox:

| Operation | Unsandboxed | Sandboxed |
| --- | --- | --- |
| `osascript` Finder `delete POSIX file (item 1 of argv)` | fails, `-1728` | fails, `-600` |
| `NSFileManager trashItemAtURL:` | succeeds | succeeds |
| `/usr/bin/open -R <path>` | exit 0 | exit 0 |
| `NSWorkspace activateFileViewerSelectingURLs:` | succeeds | succeeds |
| `NSWorkspace openURL:` for `x-apple.systempreferences:` | succeeds | succeeds |
| Enumerate `~/Documents` without user selection | succeeds | `UnauthorizedAccessException` |

Two conclusions drive the design. First, the AppleScript Trash path is broken
everywhere, not only in the sandbox: passing the path through `argv` makes Finder
evaluate `POSIX file` itself, which it cannot resolve. Second, sandbox denials surface
as ordinary permission errors, so the existing access-guidance classifier already sees
them; only the recommended remedy is wrong.

## Goals

- Move to Trash and reveal in Finder work in both builds.
- Sandboxed builds recommend selecting the location instead of granting Full Disk
  Access.
- No new entitlements, and no Apple event automation.

## Decisions

### Trash uses NSFileManager instead of Finder automation

`NSFileManager trashItemAtURL:resultingItemURL:error:` is the API Apple documents for
sandboxed apps. It works for user-selected items, it is recoverable by definition, and
it returns an `NSError` that can be surfaced per item, which the cleanup basket already
expects. The alternative, keeping AppleScript and adding
`com.apple.security.automation.apple-events` plus `NSAppleEventsUsageDescription`, was
rejected: it requests a privilege the app does not need, it adds a consent prompt in the
middle of a destructive flow, and App Review scrutinizes automation entitlements.

The call is synchronous. `MoveToTrashAsync` keeps its signature and offloads the call so
that trashing a large folder never blocks the UI thread, and it observes the
cancellation token before starting an item.

### Reveal uses NSWorkspace instead of launching `/usr/bin/open`

`activateFileViewerSelectingURLs:` is the in-process equivalent of `open -R`. Measured
behavior of `open -R` in the sandbox was not a failure, but it depends on spawning a
process outside the app bundle, it cannot report whether Finder actually revealed the
item, and it is the kind of subprocess launch App Review flags. `NSWorkspace` keeps the
work in-process and matches the existing Quick Look integration style.

### Sandbox detection is environment-based

macOS sets `APP_SANDBOX_CONTAINER_ID` for sandboxed processes and redirects the home
directory into `~/Library/Containers/<bundle id>/Data`. Detection checks the environment
variable first and falls back to the container home layout, so it stays correct if the
variable is absent. Detection lives behind an interface in Core so ViewModels and tests
never read the environment directly, and it is resolved once per app run because the
sandbox state cannot change while the process lives.

### Full Disk Access status gains a sandboxed value

`FullDiskAccessStatus` gains `SandboxRestricted`. A sandboxed build never probes for
Full Disk Access, because granting it would not widen sandbox access, and reports
`SandboxRestricted` instead. The classifier maps that status plus inaccessible paths to
a new `AccessGuidanceStatus.SandboxedSelectionRequired`, which renders selection
guidance, hides the settings action and the manual settings fallback, and keeps the
rescan action. With no inaccessible paths, a sandboxed build shows no guidance at all
rather than an indeterminate warning.

### Platform tests fail instead of ignoring

The macOS Trash test currently catches `InvalidOperationException` and calls
`Assert.Ignore`, which hid this defect. macOS-gated tests keep `Assert.Ignore` only for
the non-macOS case. To keep unit coverage independent of the real Trash, the platform
services get internal seams (`ITrashItemMover`, `IFileRevealPresenter`) in the style of
the existing `IQuickLookPresenter`, so success, failure, and argument validation are
testable with NSubstitute, while one gated integration test exercises the real API.

## Risks and trade-offs

- Objective-C interop is untyped: a wrong selector or signature fails at runtime.
  Mitigated by the gated integration tests, which now fail instead of being ignored.
- `NSWorkspace` and Quick Look are AppKit UI APIs and are invoked from the existing
  UI-thread command path; nothing moves them to a background thread.
- `trashItemAtURL:` returns the resulting Trash URL, which the app deliberately ignores:
  the cleanup basket reports success per item and never records the user's Trash layout.
