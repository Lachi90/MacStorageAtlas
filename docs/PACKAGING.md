# Packaging Atlas Storage Analyzer for macOS

This document describes how to publish the Avalonia desktop app for macOS. The
[`build-dmg.sh`](../build-dmg.sh) script automates publishing, `.app` bundling,
DMG creation, the local Developer ID release path for public artifacts, and the
Mac App Store package path for App Store Connect uploads.

## Quick start

From the repository root:

```shell
./build-dmg.sh            # Apple Silicon (default) → AtlasStorageAnalyzer.dmg
./build-dmg.sh arm64      # Apple Silicon (osx-arm64)
./build-dmg.sh x64        # Intel (osx-x64)
./build-dmg.sh both       # both architectures, one DMG each
```

The script publishes a self-contained Release build, wraps it in a
`AtlasStorageAnalyzer.app` bundle with the `.icns` icon, and produces a DMG with a
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

Release DMGs are named `AtlasStorageAnalyzer-<version>-<runtime>.dmg`; each one gets a
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
`AtlasStorageAnalyzer-<version>-<runtime>-appstore.pkg`. For App Store uploads,
prefer the `both` target, which publishes arm64 and x64 builds, combines them
into a single app bundle that runs on both architectures, and writes
`AtlasStorageAnalyzer-<version>-universal-appstore.pkg`.

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
wrap the published output in a `AtlasStorageAnalyzer.app` bundle directory with the
standard layout:

```text
AtlasStorageAnalyzer.app/
  Contents/
    Info.plist          app metadata (bundle id, version, icon name)
    MacOS/              the native host and the published runtime payload
    Resources/          AppIcon.icns and other assets
```

`CFBundleName` and `CFBundleDisplayName` are `Atlas Storage Analyzer`, while the bundle
directory and the release artifacts use `AtlasStorageAnalyzer` so that no path contains
a space. The bundle identifier stays `de.ltsoftware.macstorageatlas`, because
changing it would make the Mac App Store treat the upload as a new application
instead of an update.

### The native host

`Contents/MacOS/AtlasStorageAnalyzer` is not the .NET app host. `build-dmg.sh` compiles
it from `packaging/native-host.c` and it replaces the published
`MacStorageAtlas.App` app host in the bundle. The native host links
CoreFoundation, resolves the runtime payload directory next to itself, and
starts the managed entry assembly in process through
`hostfxr_main_startupinfo` in `libhostfxr.dylib`.

Linking CoreFoundation is what makes the sandboxed build work. The .NET app host
links only `libSystem` and `libc++`, so a .NET app loads AppKit later at runtime
through `dlopen`. By then the process has missed the LaunchServices registration
that happens during launch, and AppKit falls back to a mach-lookup of
`com.apple.coreservices.launchservicesd` by global name. The App Sandbox profile
in `/System/Library/Sandbox/Profiles/application.sb` does not allow that lookup,
so `_RegisterApplication` calls `abort()` before the first window appears. A
main executable that links CoreFoundation registers during launch and never
reaches that fallback. Loading CoreFoundation with `dlopen` at the start of
`main` is not sufficient; it has to be a load-time dependency of the main
executable.

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
xattr -dr com.apple.quarantine /Applications/AtlasStorageAnalyzer.app
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
   JIT support, and user-selected file read/write access. The build requests no
   temporary entitlement exception. See
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

### Universal app bundle layout

A .NET app bundle cannot be made universal by merging its files. The native
binaries can be combined with `lipo`, but the managed assemblies cannot: the
framework assemblies of a self-contained publish, `System.Private.CoreLib.dll`
above all, are precompiled per architecture. A bundle that keeps only the arm64
managed payload aborts on Intel Macs while CoreCLR starts:

```text
Failed to load System.Private.CoreLib.dll (error code 0x8007000B)
Failed to create CoreCLR, HRESULT: 0x8007000B
```

The `both` target therefore keeps one complete payload per architecture:

- `Contents/MacOS` holds the arm64 payload.
- `Contents/Helpers/AtlasStorageAnalyzer-x86_64.app` holds the complete x64 payload
  as a nested bundle with its own `Info.plist` and the bundle identifier
  `de.ltsoftware.macstorageatlas.x86-64`.
- `Contents/MacOS/AtlasStorageAnalyzer` is a universal binary created with `lipo` from
  two builds of `packaging/native-host.c`. The arm64 slice loads the payload
  next to itself. The x86_64 slice is compiled with `MACSTORAGEATLAS_PAYLOAD_PATH`
  and loads the nested x64 payload instead, in the same process.

The x86_64 slice must load the nested payload in process rather than replacing
itself with a nested executable through `execv`. An `execv` discards the
LaunchServices registration the process received at launch, so the new image
falls back to the denied mach-lookup and the Intel slice aborts inside the App
Sandbox even though its main executable links CoreFoundation.

The nested slice must be signed before the outer bundle, and its executable
needs `MacStorageAtlas.AppStore.Slice.entitlements`: App Sandbox inheritance
plus the JIT entitlement CoreCLR requires. Signing it with the plain inherited
entitlements instead makes the Intel slice abort in `InitThreadManager` with a
`KERN_PROTECTION_FAILURE`, because the .NET JIT cannot allocate executable
memory.

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
     AtlasStorageAnalyzer.app/Contents/MacOS/AtlasStorageAnalyzer
   codesign --force --timestamp --options runtime \
     --entitlements src/MacStorageAtlas.App/MacStorageAtlas.AppStore.entitlements \
     --sign "Developer ID Application: <team>" \
     AtlasStorageAnalyzer.app
   ```

   A Mac App Store provisioning profile does not authorize a local launch, so
   an Apple Distribution signature cannot be used for these checks. `open` then
   fails with `Launchd job spawn failed` before the app runs.

2. Launch the app and confirm that its window appears. A launch that aborts with
   `abort() called` in `_RegisterApplication` means the main executable no longer
   links CoreFoundation at load time, so the process missed its LaunchServices
   registration during launch. Confirm the dependency with
   `otool -L AtlasStorageAnalyzer.app/Contents/MacOS/AtlasStorageAnalyzer`, and confirm the
   system log records no denial with:

   ```shell
   log show --last 5m --style compact --predicate 'eventMessage CONTAINS[c] "launchservicesd"'
   ```

   Then select a folder in the open panel and scan it.
3. Move an item to the Trash from the cleanup basket and confirm that it arrives
   in the macOS Trash. Finder's Put Back command stays greyed out for it, because
   macOS records no Put Back entry for items moved with the system file API. That
   is expected and applies to the Developer ID build as well; the item is
   restored by dragging it out of the Trash.
4. Reveal an item in Finder and preview one with Quick Look.
5. Scan a location that contains a folder you did not select, and confirm the
   guidance asks you to select the missing location instead of offering Full
   Disk Access.

The app must not request Apple event automation permission at any point. If a
consent dialog for controlling Finder appears, a macOS integration regressed to
AppleScript and must be fixed before uploading.

Without an Intel Mac, verify the `osx-x64` slice of the universal build under
Rosetta 2 on an Apple Silicon Mac, so both submitted architectures have been
launched at least once. Launch it through LaunchServices rather than by running
the executable directly, because only that path applies the App Sandbox and the
LaunchServices registration the slice depends on:

```shell
open -n --arch x86_64 /Applications/AtlasStorageAnalyzer.app
```

Repeat the checks above for that slice, and confirm the running process is the
translated one, for example with Activity Monitor's Kind column. Confirm that it
loaded the nested payload rather than the arm64 one:

```shell
lsof -p "$(pgrep -n -f 'MacOS/AtlasStorageAnalyzer')" | grep libcoreclr.dylib
```

The reported path must be inside `Contents/Helpers/AtlasStorageAnalyzer-x86_64.app`.

The screen must be unlocked while verifying. On a locked screen every launch
fails with `Avalonia.Native was not able to start the RenderTimer`, which is an
artifact of the locked session rather than a defect of the build.

Record the review information for the submission in
[APP_STORE_REVIEW_NOTES.md](APP_STORE_REVIEW_NOTES.md) and paste it into the App
Review Information section of App Store Connect.

## DMG creation

`build-dmg.sh` assembles a staging folder containing the `.app` bundle and a
symlink to `/Applications`, then packages it into a compressed (`UDZO`) disk
image with `hdiutil`:

```shell
hdiutil create \
  -volname "Atlas Storage Analyzer" \
  -srcfolder dmg-content \
  -ov \
  -format UDZO \
  AtlasStorageAnalyzer.dmg
```

## Release verification

The release script performs these checks before writing checksum files:

```shell
codesign --verify --deep --strict --verbose=2 AtlasStorageAnalyzer.app
codesign --verify --verbose=2 AtlasStorageAnalyzer-1.2.3-osx-arm64.dmg
spctl --assess --type execute --verbose=2 AtlasStorageAnalyzer.app
AtlasStorageAnalyzer.app/Contents/MacOS/AtlasStorageAnalyzer
hdiutil verify AtlasStorageAnalyzer-1.2.3-osx-arm64.dmg
xcrun stapler validate AtlasStorageAnalyzer-1.2.3-osx-arm64.dmg
spctl --assess --type open --context context:primary-signature --verbose=2 AtlasStorageAnalyzer-1.2.3-osx-arm64.dmg
shasum -a 256 AtlasStorageAnalyzer-1.2.3-osx-arm64.dmg
```

Before publishing, also download or copy the final DMGs through a
quarantine-preserving path on a clean Mac and launch both the Apple Silicon and
Intel artifacts on matching hardware or a trusted test setup.

## GitHub Release upload

Upload only final DMGs and matching `.sha256` files after release verification.
Use the GitHub web UI, or a locally authenticated GitHub CLI session:

```shell
gh release upload v1.2.3 \
  AtlasStorageAnalyzer-1.2.3-osx-arm64.dmg \
  AtlasStorageAnalyzer-1.2.3-osx-arm64.dmg.sha256 \
  AtlasStorageAnalyzer-1.2.3-osx-x64.dmg \
  AtlasStorageAnalyzer-1.2.3-osx-x64.dmg.sha256
```

## Direct update release setup

Direct releases now require Sparkle 2.9.6. It retains the app's macOS 11.0
minimum and supports arm64 and x86_64. The pinned upstream archive SHA-256 is
`52bf9e88cdd972fc0c81501377a880e90d47031bd8ca5462488f843e2609e192`.
`packaging/prepare-sparkle.sh` downloads it from the official Sparkle release,
checks that digest, verifies its signatures and architectures, and extracts it
into ignored `packaging/.cache/sparkle/2.9.6`. The app includes the upstream
license in Resources. Developer ID release preparation additionally requires
Python 3 for metadata validation.

Set up the separate Ed25519 signing key once on the release machine:

```shell
packaging/prepare-sparkle.sh
packaging/.cache/sparkle/2.9.6/bin/generate_keys --account de.ltsoftware.macstorageatlas
packaging/.cache/sparkle/2.9.6/bin/generate_keys --account de.ltsoftware.macstorageatlas -p
```

The private key remains in the operator's login keychain. Record the public key
and store an encrypted private-key backup outside the repository. To export a
backup to a private location, use `generate_keys --account de.ltsoftware.macstorageatlas
-x <private-backup-path>`; restrict its permissions and transfer it only to
operator-controlled secure storage. Recover with the corresponding `-f
<private-backup-path>` option. Do not print private keys or add backups to source
control. If the old signing key cannot be recovered, existing signed-feed
clients need manual adoption of a new trusted release. Planned key rotation
requires retaining the old key and serving a bridge release and separate new
feed; follow [Sparkle's publishing guidance](https://sparkle-project.org/documentation/publishing/).

The existing `release` arguments are unchanged. The default keychain account is
`de.ltsoftware.macstorageatlas`; override it with `SPARKLE_KEY_ACCOUNT` when
using a different operator-managed account. The public key is read from that
account. An optional `SPARKLE_PUBLIC_KEY` must match it. Missing keys or a key
mismatch fail release preparation. No key is generated implicitly.

Default feeds are:

- `https://lachi90.github.io/MacStorageAtlas/updates/arm64/appcast.xml`
- `https://lachi90.github.io/MacStorageAtlas/updates/x64/appcast.xml`

`SPARKLE_FEED_BASE_URL` can select another HTTPS base. Download URLs default to
`https://github.com/Lachi90/MacStorageAtlas/releases/download/v<version>/`;
`SPARKLE_DOWNLOAD_BASE_URL` can override that versioned prefix. Releases require
three-part numeric stable versions. The bridge also checks that a chosen
archive's name and architecture match the installed variant, including x64
under Rosetta. Sparkle performs bundle-version and macOS compatibility checks.

Only Direct mode embeds the bridge and framework. The build signs Downloader.xpc,
Installer.xpc, Updater.app, Autoupdate, then Sparkle.framework before signing the
outer app. The bridge uses relocatable library paths. Framework symlinks and
executable permissions are preserved. Both feeds and final update archives are
signed; `SURequireSignedFeed` and `SUVerifyUpdateBeforeExtraction` are enabled.
Automatic checks default off, silent downloads/installations are disabled, and
optional profiling is disabled.

After the final DMG passes signing, notarization, stapling, Gatekeeper, launch,
and checksum verification, the release command generates a verified local feed
in ignored `update-output/arm64/appcast.xml` or `update-output/x64/appcast.xml`.
It retains previous entries from that local feed and refuses an equal or older
release version. Keep or restore the last published feed locally before preparing
later releases. No upload or feed publication happens automatically.

Publish in this order:

1. Finish the documented smoke tests on Apple Silicon and x64 under Rosetta.
   Record that physical Intel hardware is unavailable; Rosetta does not establish
   acceptance on physical Intel hardware.
2. Upload each verified DMG and its checksum to the matching `v<version>` GitHub
   Release and verify those immutable asset URLs.
3. Publish the generated feeds at the exact HTTPS URLs embedded in the bundles.
   Preserve signed XML bytes; editing a signed feed invalidates its signature.
4. Only then announce support. Existing installations need one manual download
   of the first updater-enabled release.

Withdraw a defective entry by restoring a previously signed feed without that
entry, or removing it and re-signing with the same account using `sign_update`.
Publish a higher fixed version; do not serve a different binary under an existing
version or attempt automatic downgrades. Users can reinstall a known-good signed
DMG while retaining settings/history outside the bundle.

## Distribution checks

The safe default is `DistributionChannel=Development`; the packaging command
passes `Direct`, `AppStore`, or `Development` to every managed publish. Publish
staging is fresh for each payload, so a prior Direct build cannot leak files into
Store packaging. `packaging/verify-no-updater.sh` inspects both Store payloads,
metadata, managed integration markers, frameworks, and native dependencies.

The existing `appstore` command, certificates, provisioning profile, and
entitlements remain sufficient. Neither Store nor unsigned builds need Sparkle,
Python metadata tooling, update-feed inputs, or an update-signing key. No updater
code or package cleanup is needed before an App Store submission.

Run repository checks from the root:

```shell
bash tests/build-dmg-script-tests.sh
python3 tests/update-metadata-tests.py
python3 tests/updater-fixtures-tests.py
bash tests/updater-bridge-tests.sh
dotnet test MacStorageAtlas.slnx -p:DistributionChannel=Direct
dotnet test MacStorageAtlas.slnx -p:DistributionChannel=AppStore
openspec validate --all --strict --no-interactive
```

Runtime and release evidence, including remaining hardware and publication
checks, is recorded in
[`add-in-app-updates/spike-findings.md`](../openspec/changes/archive/2026-09-30-add-in-app-updates/spike-findings.md).

For reproducible native UI checks, isolated signed fixtures, Rosetta coverage,
and the final release checklist, follow [manual update acceptance](UPDATE_TESTING.md).
