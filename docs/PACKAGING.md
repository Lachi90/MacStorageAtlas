# Packaging MacStorageAtlas for macOS

This document describes how to publish the Avalonia desktop app for macOS. The
[`build-dmg.sh`](../build-dmg.sh) script automates publishing, `.app` bundling,
DMG creation, and the local Developer ID release path for public artifacts.

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
drag-to-`Applications` shortcut. These default builds are unsigned development
artifacts for local testing.

For a distributable release outside the Mac App Store, use the explicit release
mode:

```shell
./build-dmg.sh release arm64 1.2.3 \
  "Developer ID Application: Example Company (TEAMID)" \
  "MacStorageAtlas-notary"

./build-dmg.sh release both 1.2.3 \
  "Developer ID Application: Example Company (TEAMID)" \
  "MacStorageAtlas-notary"
```

Release DMGs are named `MacStorageAtlas-<version>-<runtime>.dmg`; each one gets a
matching `.sha256` file after signing, notarization, stapling, and verification
all pass.

The sections below document the individual steps the script performs, for
reference and manual builds.

## Prerequisites

- macOS
- .NET 10 SDK
- Xcode command-line tools for release signing and notarization
- A Developer ID Application certificate with private key in the local keychain
- A local `notarytool` keychain profile

## Runtime identifiers

macOS ships on two CPU architectures, so publish a build per target:

- `osx-arm64` — Apple Silicon (M-series) Macs
- `osx-x64` — Intel Macs

## Publish commands

Publish a self-contained build for each runtime identifier from the repository
root:

```shell
# Apple Silicon
dotnet publish src/MacStorageAtlas.App \
  -c Release \
  -r osx-arm64 \
  --self-contained true

# Intel
dotnet publish src/MacStorageAtlas.App \
  -c Release \
  -r osx-x64 \
  --self-contained true
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

## Unsigned development builds

The default commands do not require Apple Developer credentials and intentionally
produce unsigned DMGs:

```shell
./build-dmg.sh arm64
./build-dmg.sh x64
./build-dmg.sh both
```

Unsigned DMGs are suitable for local development or ad hoc testing only.
Gatekeeper blocks them on first launch. To run one you built yourself,
right-click the installed app in `/Applications`, choose **Open**, and confirm
the dialog, or remove quarantine:

```shell
xattr -dr com.apple.quarantine /Applications/MacStorageAtlas.app
```

## Signed release builds

Public distribution outside the App Store requires:

1. **Developer ID signing** — sign the `.app` bundle with a "Developer ID
   Application" certificate using `codesign`.
2. **Notarization** — submit the signed bundle to Apple with `notarytool` and
   staple the resulting ticket with `xcrun stapler`.

Create and install the Developer ID Application certificate through the Apple
Developer portal. Verify that the local keychain can see it:

```shell
security find-identity -v -p codesigning
```

Create the local notary profile once per release machine:

```shell
xcrun notarytool store-credentials "MacStorageAtlas-notary" \
  --apple-id "developer@example.com" \
  --team-id "TEAMID" \
  --password "app-specific-password"
```

The password value is an Apple app-specific password, not an Apple Developer
portal password. App Store Connect API key credentials can also be stored with
`notarytool` if that is the preferred local operator setup.

Then build a release artifact:

```shell
./build-dmg.sh release both 1.2.3 \
  "Developer ID Application: Example Company (TEAMID)" \
  "MacStorageAtlas-notary"
```

The release workflow signs every bundled runtime file except `.pdb` debug
symbols, signs the main executable, signs the `.app` bundle with hardened runtime
and secure timestamp enabled, creates and signs the DMG, submits that DMG for
notarization, staples the accepted ticket, validates the ticket, checks disk
image integrity, runs Gatekeeper assessment, and writes SHA-256 checksum
sidecars.

The signed release path keeps certificates, notary credentials, and GitHub
authentication local to the release machine. Do not commit certificates,
passwords, API keys, keychain exports, generated DMGs, or checksum files.

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

## Release verification

The release script performs these checks before writing checksum files:

```shell
codesign --verify --deep --strict --verbose=2 MacStorageAtlas.app
codesign --verify --verbose=2 MacStorageAtlas-1.2.3-osx-arm64.dmg
spctl --assess --type execute --verbose=2 MacStorageAtlas.app
hdiutil verify MacStorageAtlas-1.2.3-osx-arm64.dmg
xcrun stapler validate MacStorageAtlas-1.2.3-osx-arm64.dmg
spctl --assess --type open --context context:primary-signature --verbose=2 MacStorageAtlas-1.2.3-osx-arm64.dmg
shasum -a 256 MacStorageAtlas-1.2.3-osx-arm64.dmg
```

Before publishing, also download or copy the final DMGs through a
quarantine-preserving path on a clean Mac and launch both the Apple Silicon and
Intel artifacts on matching hardware or a trusted test setup.

## GitHub Release upload

Upload only final DMGs and matching `.sha256` files after release verification.
Use the GitHub web UI, or a locally authenticated GitHub CLI session:

```shell
gh release upload v1.2.3 \
  MacStorageAtlas-1.2.3-osx-arm64.dmg \
  MacStorageAtlas-1.2.3-osx-arm64.dmg.sha256 \
  MacStorageAtlas-1.2.3-osx-x64.dmg \
  MacStorageAtlas-1.2.3-osx-x64.dmg.sha256
```
