## Context

MacStorageAtlas currently ships a repository-root `build-dmg.sh` script that publishes the Avalonia app for `osx-arm64` and `osx-x64`, wraps each publish output in a hand-built `.app` bundle, and creates compressed DMGs with `hdiutil`. The README, packaging documentation, and GitHub Pages site tell users that the DMGs are unsigned and un-notarized.

WP-01 asks for trusted release artifacts, checksums, CI release automation, and update checks. The decision for this change narrows WP-01 to local signing and notarization only: releases are created on a trusted local Mac, then uploaded to GitHub manually or with a locally authenticated `gh` CLI session. CI/CD and update checks remain separate concerns.

Apple's current notarization workflow requires Developer ID signing, hardened runtime, secure timestamps, `notarytool`, stapling, and verification. The implementation must preserve both Apple Silicon and Intel artifacts.

## Goals / Non-Goals

**Goals:**

- Produce distributable DMGs for `osx-arm64` and `osx-x64` that pass local code-signing, Gatekeeper, stapling, and checksum verification.
- Keep the existing unsigned packaging path available for local development and testing.
- Keep Apple signing certificates and notarization credentials in the release operator's local keychain.
- Document local prerequisite setup, artifact generation, verification, and GitHub Release upload steps.
- Keep release scripts at the repository root.

**Non-Goals:**

- No GitHub Actions workflow, hosted runner signing, CI/CD release automation, or repository secrets.
- No in-app update check, release feed parser, updater UI, or silent installer.
- No Mac App Store, TestFlight, installer `.pkg`, Sparkle integration, or package manager distribution.
- No App Sandbox enablement or Full Disk Access entitlement redesign.
- No change to scan, cleanup, export, rendering, or storage-measurement behavior.

## Decisions

### Use a local release mode instead of CI/CD

The release script will support an explicit signed/notarized mode that runs on a local Mac with the required Apple identity and notary profile already configured. GitHub upload remains a separate local/manual step.

Alternative considered: GitHub Actions signing and notarization. That would satisfy the original roadmap bullet, but it would move certificate import, keychain lifecycle, and App Store Connect credentials into CI secrets. The user explicitly rejected that trust model for this project.

### Preserve unsigned packaging as the default development path

Unsigned DMG creation remains available so contributors without an Apple Developer account can build and test packaging. The signed release path must be opt-in and fail clearly when required local signing inputs are missing.

Alternative considered: replace the existing script with release-only signing. That would make routine local packaging depend on paid-account credentials and would slow development.

### Keep release orchestration in repository-root shell scripts

The existing packaging entry point is `build-dmg.sh`, and repository instructions already expect packaging scripts at the root. The implementation can either extend that script with release flags or add a small release wrapper that delegates to shared packaging functions. In either case, release scripts stay at the root and must preserve `arm64`, `x64`, and `both` targets.

Alternative considered: use MSBuild targets for macOS signing. That would hide important notarization and stapling steps behind project configuration and make local operator prerequisites harder to audit. The current bundle is manually assembled after `dotnet publish`, so shell orchestration is more transparent for this workflow.

### Sign nested code before signing the final app bundle

The release path will sign executable files and nested runtime components inside `MacStorageAtlas.app/Contents/MacOS` before signing the final `.app` bundle with hardened runtime and secure timestamp enabled. The task plan includes a signing-order spike because .NET/Avalonia publish output can change by SDK and runtime identifier.

Alternative considered: rely on `codesign --deep --force` for the final bundle only. That is simpler, but Apple notarization commonly exposes nested signing issues. An explicit signing pass produces more understandable failures.

### Notarize and staple the distributed DMG

The release path will sign the final DMG, notarize it, wait for completion, staple the ticket to the DMG, and validate the stapled artifact. Since the DMG is the user-facing download, this gives Gatekeeper a signed container plus an offline ticket for the distributed artifact while preserving tickets for nested content returned by notarization.

Alternative considered: notarize only a zipped `.app` and then create a DMG afterward. That risks changing the distributed container after notarization and adds an extra artifact format the project does not otherwise need.

### Generate checksums beside final artifacts

Each release DMG will have a corresponding SHA-256 checksum file generated after stapling and validation. Checksums are part of the final upload set and documentation must tell the release operator to upload them with the DMGs.

Alternative considered: publish checksums only in release notes. Sidecar files are easier to verify locally and harder to mistype.

### Use explicit release version input

Signed release artifacts use a required version argument instead of the existing hardcoded development version. Release DMGs are named `MacStorageAtlas-<version>-<runtime>.dmg`, which keeps architecture and version visible in the uploaded assets while preserving the old unsigned artifact names.

### Sign all bundled runtime files except debug symbols

The .NET 10 Avalonia publish output contains `MacStorageAtlas.App`, `createdump`, native `.dylib` runtime components, managed `.dll` assemblies, JSON host metadata, and `.pdb` debug symbols. Local signing evidence showed that `codesign --verify --deep --strict` treats sibling runtime files in `Contents/MacOS` as nested code requirements when the main executable is signed. The release workflow therefore excludes `.pdb` files from signed release bundles and signs every remaining file in `Contents/MacOS` before signing the main executable, final `.app` bundle, and final DMG.

### Do not add entitlements for the initial Developer ID release

The signed Apple Silicon bundle launched locally with hardened runtime enabled and no entitlements file. The implementation therefore does not add release entitlements. If a future SDK, Avalonia, Skia, or runtime update introduces a hardened-runtime failure, that should be handled as a focused follow-up with the smallest entitlement set supported by evidence.

## Risks / Trade-offs

- Hardened runtime may require entitlements for the .NET/Avalonia app to launch after signing -> Run an early local signing spike, inspect notarization logs, and add the smallest entitlements file needed for release builds.
- Nested signing order may vary by .NET SDK output -> Discover executable and dynamic-library files from the actual publish directory and verify both RIDs independently.
- Local keychain setup can differ across machines -> Document required certificate names, keychain profile setup, and failure messages without storing secrets in the repository.
- Manual GitHub upload can miss an artifact -> Generate predictable filenames and checksums, then document a release checklist including both DMGs and checksum files.
- Notarization depends on Apple services and can be slow or fail externally -> Make notarization an explicit release step with clear log retrieval guidance and no partial-success claim.
- Stapling or Gatekeeper validation may pass locally but fail after download quarantine is applied -> Include a manual clean-machine or quarantine-preserving verification step before publishing the release as final.

## Migration Plan

1. Add local release signing/notarization support while leaving current unsigned commands valid.
2. Verify unsigned packaging still creates development DMGs.
3. Verify signed release packaging on Apple Silicon and Intel RIDs from a local Mac with configured Developer ID credentials.
4. Update README, packaging docs, GitHub Pages, roadmap, and backlog text so users no longer see signed release work as deferred once the flow is implemented.
5. Upload final DMGs and checksum files to GitHub Releases manually or with locally authenticated `gh release upload`.

Rollback is straightforward: use the existing unsigned packaging path and do not publish the failed signed release artifacts. No app data, scan history, or user settings migrate as part of this change.

## Open Questions

Resolved during implementation:

- No hardened runtime entitlements are required for the initial Developer ID release based on the local Apple Silicon launch spike.
- Release version is a required script argument for signed release mode.
- Final GitHub upload guidance documents both the web UI and locally authenticated `gh release upload`.
