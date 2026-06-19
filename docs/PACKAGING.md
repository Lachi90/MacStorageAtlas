# Packaging MacStorageAtlas for macOS

This document describes how to publish the Avalonia desktop app for macOS. It
covers the `dotnet publish` workflow today and records the signing, notarization,
and DMG steps that are deferred to future work.

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

The app icon under `src/MacStorageAtlas.App/Assets/` is a **placeholder**; replace
it with final branding artwork (exported to `.icns`) before public distribution.

## Signing and notarization (future work)

Public distribution outside the App Store requires:

1. **Developer ID signing** — sign the `.app` bundle with a "Developer ID
   Application" certificate using `codesign`.
2. **Notarization** — submit the signed bundle to Apple with `notarytool` and
   staple the resulting ticket with `xcrun stapler`.

These steps need an Apple Developer account and credentials and are intentionally
**not** automated yet.

## DMG creation (future work)

A user-friendly download is typically a `.dmg` containing the signed `.app` and a
shortcut to `/Applications`. Tools such as `create-dmg` or `hdiutil` can build
the image. This is deferred until signing and notarization are in place.
