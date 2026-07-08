# Packaging MacStorageAtlas for macOS

This document describes how to publish the Avalonia desktop app for macOS. The
[`build-dmg.sh`](../build-dmg.sh) script automates publishing, `.app` bundling,
and DMG creation. Code signing and notarization remain deferred to future work.

## Quick start

From the repository root:

```shell
./build-dmg.sh            # Apple Silicon (default) → MacStorageAtlas.dmg
./build-dmg.sh arm64      # Apple Silicon (osx-arm64)
./build-dmg.sh x64        # Intel (osx-x64)
./build-dmg.sh both       # both architectures, one DMG each
```

The script publishes a self-contained Release build, wraps it in a
`MacStorageAtlas.app` bundle with the `.icns` icon, and produces a DMG with a
drag-to-`Applications` shortcut. The resulting DMG is **unsigned and
un-notarized** — see [Signing and notarization](#signing-and-notarization-future-work).

The sections below document the individual steps the script performs, for
reference and manual builds.

## Prerequisites

- macOS
- .NET 10 SDK

## Runtime identifiers

macOS ships on two CPU architectures, so publish a build per target:

- `osx-arm64` — Apple Silicon (M-series) Macs
- `osx-x64` — Intel Macs

## Publish commands

Publish a self-contained, single-file build for each runtime identifier from the
repository root:

```shell
# Apple Silicon
dotnet publish src/MacStorageAtlas.App \
  -c Release \
  -r osx-arm64 \
  --self-contained true \
  -p:PublishSingleFile=true

# Intel
dotnet publish src/MacStorageAtlas.App \
  -c Release \
  -r osx-x64 \
  --self-contained true \
  -p:PublishSingleFile=true
```

The published output is written to
`src/MacStorageAtlas.App/bin/Release/net10.0/<rid>/publish/`.

## Building a `.app` bundle

Avalonia produces a plain executable. For a distributable macOS application,
wrap the published output in a `MacStorageAtlas.app` bundle directory with the
standard layout:

```text
MacStorageAtlas.app/
  Contents/
    Info.plist          app metadata (bundle id, version, icon name)
    MacOS/              the published executable
    Resources/          AppIcon.icns and other assets
```

The bundled icon comes from `src/MacStorageAtlas.App/Assets/MacStorageAtlas.icns`;
`build-dmg.sh` copies it to `Contents/Resources/AppIcon.icns` and references it
via `CFBundleIconFile` in the generated `Info.plist`.

## Signing and notarization (future work)

Public distribution outside the App Store requires:

1. **Developer ID signing** — sign the `.app` bundle with a "Developer ID
   Application" certificate using `codesign`.
2. **Notarization** — submit the signed bundle to Apple with `notarytool` and
   staple the resulting ticket with `xcrun stapler`.

These steps need an Apple Developer account and credentials and are intentionally
**not** automated yet.

## DMG creation

`build-dmg.sh` assembles a staging folder containing the `.app` bundle and a
symlink to `/Applications`, then packages it into a compressed (`UDZO`) disk
image with `hdiutil`:

```shell
hdiutil create \
  -volname MacStorageAtlas \
  -srcfolder dmg-content \
  -ov \
  -format UDZO \
  MacStorageAtlas.dmg
```

Because the bundle is not yet signed or notarized, Gatekeeper blocks it on first
launch. Users can bypass this by right-clicking the app → **Open**, or by
removing the quarantine attribute:

```shell
xattr -dr com.apple.quarantine /Applications/MacStorageAtlas.app
```
