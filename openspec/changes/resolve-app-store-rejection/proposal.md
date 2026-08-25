## Why

App Review rejected version 1.0 (1.0.2) on 2026-08-24 for two separate reasons,
and neither can be answered with metadata alone.

Guideline 5.2.5 rejects the app name. "MacStorageAtlas" uses the Apple
trademark "Mac" as a name prefix, which the Guidelines for Using Apple's
Trademarks and Copyrights do not permit.

Guideline 2.4.5(i) refuses the temporary entitlement exception
`com.apple.security.temporary-exception.mach-lookup.global-name` for
`com.apple.coreservices.launchservicesd`. The submitted build cannot start in
the App Sandbox without it, so the app is unreleasable until the underlying
cause is removed.

The exception was previously documented as an unavoidable requirement of the UI
framework. That was wrong. The App Sandbox profile in
`/System/Library/Sandbox/Profiles/application.sb` never allows that lookup, so
no correctly built sandboxed app can depend on it. The real cause is the load
order of the main executable: the .NET app host links only `libSystem` and
`libc++`, so AppKit is loaded later through `dlopen`. By then the process has
missed the LaunchServices registration that happens during launch, and AppKit
falls back to a global-name lookup that the sandbox denies, which makes
`_RegisterApplication` call `abort()` before the first window appears.

## What Changes

- Replace the published .NET app host with a native host compiled from
  `packaging/native-host.c`. It links CoreFoundation, so LaunchServices
  registration happens during launch, and it starts the managed entry assembly
  in process through `hostfxr_main_startupinfo`.
- Remove the temporary mach-lookup exception from the App Store entitlements.
- Load the nested x64 payload of the universal bundle in process instead of
  replacing the process with `execv`, and remove
  `packaging/universal-launcher.c`. An `execv` discards the LaunchServices
  registration, so the Intel slice would keep aborting even with a main
  executable that links CoreFoundation.
- Rename the product to "Storage Atlas" everywhere it is user visible: the
  bundle name and display name, the macOS menu bar title, the window title, the
  in-app guidance text, the export file name prefix, the release artifacts, and
  the documentation. The bundle identifier stays `de.ltsoftware.macstorageatlas`
  so the Mac App Store treats the upload as an update.
- Keep the repository name, namespaces, assembly names, and the application
  support directory unchanged, so existing scan history and settings remain
  readable.

## Capabilities

### Modified Capabilities

- `release-packaging`: the sandboxed build starts without any temporary
  entitlement exception, and the universal package runs both architectures in
  one process.

### Added Capabilities

- `product-identity`: the user-visible product name carries no Apple trademark.
