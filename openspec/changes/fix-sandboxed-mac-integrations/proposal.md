## Why

App Review rejected the first Mac App Store submission of MacStorageAtlas 1.0 under
Guideline 2.1 (Information Needed / Performance: App Completeness) and asked for a
screen recording plus written review information.

Verifying the App Store build against the App Sandbox surfaced defects that make the
rejection reproducible instead of purely administrative. Two macOS integrations rely on
Apple events and external process launches that do not work inside the sandbox, and the
Trash integration is additionally broken outside the sandbox on current macOS:

- The Trash integration runs `osascript` with a `tell application "Finder" to delete`
  script. Executed with the path passed through `argv`, Finder returns error `-1728`
  even in an unsandboxed process, so "Move to Trash" fails for every item. Inside the
  App Sandbox the same script fails with `-600`.
- Access guidance instructs users to grant Full Disk Access. Full Disk Access does not
  widen App Sandbox file access, so in the Mac App Store build the guidance sends users
  to a setting that cannot fix their scan.
- The existing macOS Trash test converts an integration failure into `Assert.Ignore`,
  so the broken Trash path passed the test suite unnoticed.

WP-01 owns distributable macOS packaging. This change keeps that packaging intact and
makes the sandboxed App Store build behave the way the product already promises.

## What Changes

- Replace the AppleScript/Finder Trash implementation with the sandbox-compatible
  `NSFileManager trashItemAtURL:resultingItemURL:error:` API, keeping cleanup
  recoverable and itemized.
- Replace the `/usr/bin/open -R` Finder reveal implementation with
  `NSWorkspace activateFileViewerSelectingURLs:`, removing the external process launch.
- Detect whether the running app is inside the macOS App Sandbox and report that state
  to the access-guidance surface.
- Adapt access guidance for sandboxed builds: explain that only selected folders and
  volumes can be scanned, drop the Full Disk Access instructions and the settings
  action, and keep the rescan action.
- Stop macOS platform integration tests from masking real integration failures.
- Document the App Store review information and the screen-recording flow that App
  Review requested, and align user-facing documentation with sandboxed behavior.

## Capabilities

### New Capabilities

- `sandboxed-platform-integration`: macOS Trash, Finder reveal, and sandbox detection
  behavior that works in both the Developer ID build and the sandboxed Mac App Store
  build.

### Modified Capabilities

- `scan-access-guidance`: access guidance distinguishes a sandboxed build, where
  selecting a location replaces Full Disk Access as the remedy.

## Impact

- Affects `src/MacStorageAtlas.Platform.Mac`, the access-guidance path in
  `src/MacStorageAtlas.App`, the access contracts in `src/MacStorageAtlas.Core/Access`,
  the Platform.Mac and App test projects, `README.md`, `docs/TROUBLESHOOTING.md`,
  `docs/PACKAGING.md`, and `docs/FEATURES.md`.
- Adds `docs/APP_STORE_REVIEW_NOTES.md` as the source of the App Review reply.
- Fixes a defect that also affects the current Developer ID build, so it warrants a
  release for both distribution channels.
- Does not add the `com.apple.security.automation.apple-events` entitlement, does not
  add new entitlements of any kind, does not change scan semantics or measurement
  modes, does not change packaging modes or supported architectures, and does not
  automate anything in App Store Connect.
- Risk: the Objective-C interop for Trash and reveal is only observable on macOS, so
  gated integration tests must fail loudly rather than be ignored when the platform
  API stops working.
