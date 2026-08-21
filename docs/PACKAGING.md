# Packaging MacStorageAtlas for macOS

This document describes how to publish the Avalonia desktop app for macOS. The
[`build-dmg.sh`](../build-dmg.sh) script automates publishing, `.app` bundling,
DMG creation, the local Developer ID release path for public artifacts, and the
Mac App Store package path for App Store Connect uploads.

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

For a Mac App Store package, use appstore mode:

```shell
./build-dmg.sh appstore arm64 1.2.3 \
  "Apple Distribution: Lachmann Thiem Software GbR (2G3HGCLNFN)" \
  "3rd Party Mac Developer Installer: Lachmann Thiem Software GbR (2G3HGCLNFN)" \
  "$HOME/.macstorageatlas-apple-certificates/MacStorageAtlas_Mac_App_Store.provisionprofile"

./build-dmg.sh appstore both 1.2.3 \
  "Apple Distribution: Lachmann Thiem Software GbR (2G3HGCLNFN)" \
  "3rd Party Mac Developer Installer: Lachmann Thiem Software GbR (2G3HGCLNFN)" \
  "$HOME/.macstorageatlas-apple-certificates/MacStorageAtlas_Mac_App_Store.provisionprofile"
```

App Store packages are named
`MacStorageAtlas-<version>-<runtime>-appstore.pkg`. For App Store uploads,
prefer the `both` target, which publishes arm64 and x64 builds, merges Mach-O
executables and libraries into a single universal app bundle with `lipo`, and
writes `MacStorageAtlas-<version>-universal-appstore.pkg`.

The sections below document the individual steps the script performs, for
reference and manual builds.

## Prerequisites

- macOS
- .NET 10 SDK
- Xcode command-line tools for release signing and notarization
- A Developer ID Application certificate with private key in the local keychain
- A local `notarytool` keychain profile
- For Mac App Store packages, an Apple Distribution certificate, a Mac Installer
  Distribution certificate, and a Mac App Store provisioning profile for
  `de.ltsoftware.macstorageatlas`. The provisioning profile must be created for
  macOS; an iOS, xrOS, or visionOS App Store profile is not valid for Mac App
  Store uploads even when it uses the same bundle identifier.

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
symbols, signs the main executable and `.app` bundle with hardened runtime,
secure timestamp, and the `MacStorageAtlas.entitlements` runtime exceptions
required by CoreCLR, creates and signs the DMG, submits that DMG for
notarization, staples the accepted ticket, validates the ticket, checks disk
image integrity, runs Gatekeeper assessment and an app launch smoke test, and
writes SHA-256 checksum sidecars.

The signed release path keeps certificates, notary credentials, and GitHub
authentication local to the release machine. Do not commit certificates,
passwords, API keys, keychain exports, generated DMGs, or checksum files.

## Mac App Store packages

Mac App Store distribution uses a separate signing path from Developer ID DMGs:

1. **Apple Distribution signing** — sign the `.app` bundle with the Apple
   Distribution certificate for the App Store Connect team.
2. **App Sandbox entitlements** — sign with
   `MacStorageAtlas.AppStore.entitlements`, which enables App Sandbox, CoreCLR
   JIT support, user-selected file read/write access, and a temporary mach-lookup
   exception for `com.apple.coreservices.launchservicesd`. Avalonia creates
   `NSApplication` during startup, and on current macOS AppKit registers the
   process with LaunchServices from that call. Without the exception the App
   Sandbox denies the lookup and the app aborts before its first window appears,
   so the exception is required for the sandboxed build to start at all. See
   [Avalonia macOS deployment](https://docs.avaloniaui.net/docs/deployment/macos).
3. **Provisioning profile embedding** — copy the Mac App Store provisioning
   profile to `Contents/embedded.provisionprofile` before signing.
4. **Profile-derived signing entitlements** — merge the profile's application
   identifier, team identifier, and keychain access groups into the app's
   signing entitlements so TestFlight can match the signature to the embedded
   profile.
5. **Bundle attribute cleanup** — remove extended attributes from the app bundle
   before signing so quarantine metadata cannot be carried into the package.
6. **Installer signing** — create the uploadable `.pkg` with `productbuild` and
   the Mac Installer Distribution certificate.

The local release machine currently stores private App Store signing inputs
outside the repository under:

```text
~/.macstorageatlas-apple-certificates/
```

Build a package from the repository root:

```shell
./build-dmg.sh appstore both 1.2.3 \
  "Apple Distribution: Lachmann Thiem Software GbR (2G3HGCLNFN)" \
  "3rd Party Mac Developer Installer: Lachmann Thiem Software GbR (2G3HGCLNFN)"
```

If the provisioning profile is stored somewhere other than the default local
path, pass it as the final argument:

```shell
./build-dmg.sh appstore both 1.2.3 \
  "Apple Distribution: Lachmann Thiem Software GbR (2G3HGCLNFN)" \
  "3rd Party Mac Developer Installer: Lachmann Thiem Software GbR (2G3HGCLNFN)" \
  "/path/to/MacStorageAtlas_Mac_App_Store.provisionprofile"
```

The script clears app bundle extended attributes and normalizes permissions
before signing so packaged files are not quarantined and remain readable by
non-root users. It then verifies the signed app bundle, the package signature,
and the universal app executable architecture for `both`. Upload the resulting
`.pkg` with Transporter or `xcrun altool` after App Store Connect has a matching
macOS app record for `de.ltsoftware.macstorageatlas`.

If Transporter reports an invalid provisioning profile signature, decode the
embedded profile with `security cms -D -i <profile>` and check that its
`Platform` entry contains `OSX` or `macOS`. If it lists `iOS`, `xrOS`, or
`visionOS`, recreate the profile in the Apple Developer portal as a macOS App
Store provisioning profile, replace the local profile, and rebuild the package.

## Sandbox verification before an App Store upload

The Mac App Store build runs in the macOS App Sandbox with only
`com.apple.security.files.user-selected.read-write`. Verify the sandboxed
behavior on a real Mac before uploading, because these paths cannot be observed
in an unsandboxed development build:

1. Install the signed `.app` from the App Store package, or re-sign a local
   build with the App Store entitlements for testing. The signature must come
   from a real signing identity; the Developer ID Application certificate works
   and needs no provisioning profile. An ad-hoc signature (`--sign -`) is not a
   valid substitute: macOS then initializes the sandbox container without a
   signer, and the app fails before any of the behavior below can be observed.

   ```shell
   codesign --force --timestamp --options runtime \
     --entitlements src/MacStorageAtlas.App/MacStorageAtlas.AppStore.entitlements \
     --sign "Developer ID Application: <team>" \
     MacStorageAtlas.app/Contents/MacOS/MacStorageAtlas.App
   codesign --force --timestamp --options runtime \
     --entitlements src/MacStorageAtlas.App/MacStorageAtlas.AppStore.entitlements \
     --sign "Developer ID Application: <team>" \
     MacStorageAtlas.app
   ```

2. Launch the app and confirm that its window appears. A launch that aborts with
   `abort() called` in `_RegisterApplication` means the mach-lookup exception for
   `com.apple.coreservices.launchservicesd` is missing from the signed
   entitlements. Then select a folder in the open panel and scan it.
3. Move an item to the Trash from the cleanup basket and confirm that it arrives
   in the macOS Trash and can be put back.
4. Reveal an item in Finder and preview one with Quick Look.
5. Scan a location that contains a folder you did not select, and confirm the
   guidance asks you to select the missing location instead of offering Full
   Disk Access.

The app must not request Apple event automation permission at any point. If a
consent dialog for controlling Finder appears, a macOS integration regressed to
AppleScript and must be fixed before uploading.

Without an Intel Mac, verify the `osx-x64` slice of the universal build under
Rosetta 2 on an Apple Silicon Mac, so both submitted architectures have been
launched at least once:

```shell
arch -x86_64 /Applications/MacStorageAtlas.app/Contents/MacOS/MacStorageAtlas
```

Record the review information for the submission in
[APP_STORE_REVIEW_NOTES.md](APP_STORE_REVIEW_NOTES.md) and paste it into the App
Review Information section of App Store Connect.

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
MacStorageAtlas.app/Contents/MacOS/MacStorageAtlas.App
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
