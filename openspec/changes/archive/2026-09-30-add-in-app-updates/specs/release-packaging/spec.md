## ADDED Requirements

### Requirement: Direct releases contain a verified updater

Developer ID release packaging SHALL embed the updater and its native integration, preserve framework structure and executable permissions, sign nested updater helpers before their enclosing bundles, and verify the complete app before marking the DMG distributable. Both existing release architectures and the macOS 11.0 deployment target MUST remain supported.

#### Scenario: Both direct-release architectures are packaged

- **GIVEN** configured Developer ID credentials and verified updater dependencies
- **WHEN** the operator builds a release for both architectures
- **THEN** each architecture-specific app contains an updater compatible with its payload and macOS 11.0
- **AND** nested helpers and the outer bundle pass signature verification
- **AND** existing notarization, stapling, checksum, and launch checks still run

#### Scenario: An updater dependency cannot be verified

- **GIVEN** a missing or invalid required updater dependency
- **WHEN** direct-release packaging runs
- **THEN** packaging fails without marking an incomplete release distributable

### Requirement: Store packaging automatically excludes direct-update machinery

The existing Mac App Store packaging command SHALL compile and package every architecture payload without the direct updater, its native integration, feed keys, update-signing configuration, or updater-linked native dependencies. It MUST require no updater inputs, extra entitlements, manual removal, or later Store-specific adaptation. Existing Store signing, provisioning, sandbox permissions, bundle identifiers, native-host behavior, and universal architecture support MUST be preserved.

#### Scenario: The universal Store package is built without updater inputs

- **GIVEN** only the existing Store signing identities and provisioning profile are configured
- **WHEN** the operator runs the existing appstore command for both architectures
- **THEN** a signed universal Store package is produced without requiring updater tools or keys
- **AND** neither the outer bundle nor either architecture payload contains direct-update machinery or configuration
- **AND** the packaged entitlements and bundle identities retain the existing Store behavior

#### Scenario: A Store build follows a direct build

- **GIVEN** a direct-release build previously produced updater-enabled output
- **WHEN** the operator subsequently builds a Store package
- **THEN** direct-build output does not leak into the Store package
- **AND** inspection of native dependencies, plist keys, bridge files, and framework contents confirms the exclusion

#### Scenario: Both Store payloads start

- **GIVEN** the signed universal Store bundle
- **WHEN** it is launched on Apple Silicon and Intel
- **THEN** both payloads reach the main window in the existing sandbox
- **AND** neither initializes a direct updater nor needs additional entitlements

### Requirement: Development packaging requires no production updater configuration

Unsigned development packaging SHALL remain available without updater downloads, feed URLs, or signing keys and MUST NOT embed production update configuration.

#### Scenario: A contributor creates an unsigned build

- **GIVEN** no update-signing key, feed URL, or updater framework is configured
- **WHEN** the contributor runs the existing unsigned packaging command
- **THEN** an unsigned development artifact is created without production updater configuration

### Requirement: Release metadata references final authenticated compatible artifacts

The local release workflow SHALL generate architecture-specific stable-update metadata from final verified DMGs, bind each download to its version and minimum macOS compatibility, and authenticate the archive with an operator-controlled update-signing key. Private key material MUST remain outside repository files and distributable artifacts. An artifact whose signing or verification fails MUST NOT be advertised as an installable update.

#### Scenario: Verified artifacts become update candidates

- **GIVEN** both release DMGs have completed signing, notarization, stapling, checksum generation, and verification
- **WHEN** update metadata is generated locally
- **THEN** each architecture feed references its matching versioned artifact with the final archive's authentication signature and compatibility metadata
- **AND** only the public verification key is included in distributable apps

#### Scenario: Update signing fails

- **GIVEN** the required update-signing key is unavailable or signing fails
- **WHEN** metadata generation runs
- **THEN** the workflow reports a failure and does not produce a ready-to-publish update entry for that artifact
- **AND** no private key material is written to the repository or distribution output

### Requirement: Update publication remains ordered and locally controlled

Release documentation SHALL describe local metadata generation and operator-controlled HTTPS publication, with verified download assets available before feeds reference them. It SHALL cover first-release adoption, feed withdrawal, and signing-key recovery while preserving the existing manual or locally authenticated upload flow.

#### Scenario: The operator publishes an update

- **GIVEN** verified direct-release artifacts and generated update metadata
- **WHEN** the operator follows the release instructions
- **THEN** versioned assets are uploaded and checked before the corresponding feed entries are published
- **AND** no hosted signing or publishing workflow is required

#### Scenario: A user adopts the first updater-enabled release

- **GIVEN** an installed version without an updater
- **WHEN** the user reads the new update guidance
- **THEN** it explains the one-time manual installation and subsequent in-app update flow
- **AND** it distinguishes Store-managed updates from direct-release updates
