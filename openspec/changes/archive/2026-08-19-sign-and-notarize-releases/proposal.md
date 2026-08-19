## Why

WP-01 calls for release artifacts that install without Gatekeeper bypasses. MacStorageAtlas can already build architecture-specific DMGs, but those artifacts are unsigned and un-notarized, which makes public distribution awkward and trains users to bypass macOS security prompts.

## What Changes

- Add a local release path that signs and notarizes macOS DMGs on a developer-controlled Mac.
- Preserve the existing unsigned local packaging path for development and ad hoc testing.
- Require Developer ID signing, hardened runtime, notarization, stapling, validation, and SHA-256 checksum generation for distributable release artifacts.
- Keep signing certificates, notarization credentials, and GitHub authentication local to the release machine.
- Document the local prerequisites, release commands, verification steps, artifact naming, and manual GitHub Release upload flow.
- Explicitly exclude CI/CD release automation and in-app update checking from this change.

## Capabilities

### New Capabilities

- `release-packaging`: Local macOS release packaging, signing, notarization, validation, checksums, and upload guidance.

### Modified Capabilities

- None.

## Impact

- Affects repository-root release scripts, release documentation, `README.md`, `docs/PACKAGING.md`, `docs/index.html`, and roadmap/backlog text that currently describes signing and notarization as future work.
- Requires macOS with Xcode command-line tools, a Developer ID Application certificate in the local keychain, and a local `notarytool` keychain profile.
- Does not add App Store distribution, CI/CD workflows, GitHub Actions secrets, package installers, silent updates, or in-app update checks.
- Risks include hardened runtime entitlement gaps for .NET/Avalonia output, incorrect nested signing order, local keychain setup variance, Apple notarization failures, and manual upload mistakes.
- Roadmap estimate remains WP-01 sized for signing/notarization work, but excludes the update-check portion that should move to a separate change.
