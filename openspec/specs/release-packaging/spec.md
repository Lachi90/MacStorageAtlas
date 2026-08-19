# Release Packaging Specification

## Purpose

Define the local macOS packaging flow for unsigned development builds and signed,
notarized public release artifacts.

## Requirements

### Requirement: Local release packaging supports both macOS architectures

MacStorageAtlas SHALL provide a local release packaging workflow that can produce separate distributable DMG artifacts for Apple Silicon and Intel Macs.

#### Scenario: Build both release artifacts locally

- **WHEN** the release operator runs the local release packaging workflow for both supported architectures
- **THEN** the workflow produces one Apple Silicon DMG and one Intel DMG with architecture-specific filenames

#### Scenario: Build one release artifact locally

- **WHEN** the release operator runs the local release packaging workflow for a single supported architecture
- **THEN** the workflow produces a DMG for only that requested architecture

### Requirement: Unsigned packaging remains available

MacStorageAtlas SHALL preserve an unsigned packaging workflow that does not require Apple Developer credentials.

#### Scenario: Build unsigned development DMG

- **WHEN** a contributor runs the unsigned packaging workflow without a Developer ID certificate or notarization profile
- **THEN** the workflow produces an unsigned DMG suitable for local development or ad hoc testing

### Requirement: Release artifacts are signed before distribution

MacStorageAtlas SHALL sign all distributable release app bundles with a Developer ID Application identity, hardened runtime enabled, a secure timestamp, and the runtime entitlements required for CoreCLR startup before creating final release artifacts.

#### Scenario: Signing identity is configured

- **WHEN** the release operator runs the signed release workflow with a configured Developer ID Application identity
- **THEN** the workflow signs nested executable content and the final app bundle with the release entitlements before packaging the release DMG

#### Scenario: Signing identity is missing

- **WHEN** the release operator runs the signed release workflow without the required signing identity
- **THEN** the workflow fails without producing a distributable signed release artifact

### Requirement: Release artifacts are notarized and stapled

MacStorageAtlas SHALL submit each signed release DMG for Apple notarization, staple the accepted notarization ticket, and validate the stapled artifact before it is treated as distributable.

#### Scenario: Notarization succeeds

- **WHEN** Apple accepts a signed release DMG for notarization
- **THEN** the workflow staples the notarization ticket to that DMG and validates the stapled artifact

#### Scenario: Notarization fails

- **WHEN** Apple rejects a signed release DMG during notarization
- **THEN** the workflow reports the failure and does not mark that DMG as a distributable release artifact

### Requirement: Release verification is explicit

MacStorageAtlas SHALL verify signed release artifacts with local macOS signing, Gatekeeper, app launch, stapling, and disk image integrity checks before publication.

#### Scenario: Verification succeeds

- **WHEN** all release verification checks pass for a DMG
- **THEN** the workflow reports that the DMG is ready for upload

#### Scenario: Verification fails

- **WHEN** any release verification check fails for a DMG
- **THEN** the workflow fails the release for that artifact and reports which verification step failed

### Requirement: Release checksums are generated

MacStorageAtlas SHALL generate SHA-256 checksum files for each final signed and notarized DMG.

#### Scenario: Checksums generated after final artifact validation

- **WHEN** a signed and notarized DMG passes release verification
- **THEN** the workflow writes a SHA-256 checksum file for that final DMG

### Requirement: Release credentials stay local

MacStorageAtlas SHALL keep signing certificates, notarization credentials, and GitHub authentication out of repository files, generated artifacts, and CI/CD configuration.

#### Scenario: Local keychain credentials are used

- **WHEN** the release operator runs the signed release workflow
- **THEN** the workflow uses credentials configured on the local release machine without writing secret values to the repository

#### Scenario: CI/CD configuration is absent

- **WHEN** the release packaging change is implemented
- **THEN** the repository does not gain a CI/CD workflow for signing, notarization, or GitHub Release publishing

### Requirement: GitHub release upload remains local or manual

MacStorageAtlas SHALL document GitHub Release upload as a local operator action after artifacts have been signed, notarized, stapled, verified, and checksummed.

#### Scenario: Release operator uploads artifacts

- **WHEN** final DMGs and checksum files are ready
- **THEN** the release documentation instructs the operator to upload those files to GitHub Releases manually or with a locally authenticated GitHub CLI

### Requirement: User-facing release documentation reflects trusted artifacts

MacStorageAtlas SHALL update user-facing packaging and download documentation to distinguish unsigned development builds from signed and notarized release artifacts.

#### Scenario: Documentation describes release artifacts

- **WHEN** a user reads the package or download documentation after this change
- **THEN** the documentation identifies signed and notarized release artifacts as the normal public distribution path

#### Scenario: Documentation describes development artifacts

- **WHEN** a contributor reads the package documentation after this change
- **THEN** the documentation explains that unsigned packaging is still available for local development and ad hoc testing
