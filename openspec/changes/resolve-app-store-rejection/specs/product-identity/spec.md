## ADDED Requirements

### Requirement: The user-visible product name carries no Apple trademark

The product SHALL present itself as "Storage Atlas" everywhere a user or a
reviewer can see its name. No user-visible name may use "Mac" as a prefix,
because the Guidelines for Using Apple's Trademarks and Copyrights do not permit
it and App Review rejects it under Guideline 5.2.5.

#### Scenario: The installed app identifies itself as Storage Atlas

- **GIVEN** the packaged app bundle
- **THEN** `CFBundleName` and `CFBundleDisplayName` are "Storage Atlas"
- **AND** the macOS menu bar title is "Storage Atlas"
- **AND** the main window title is "Storage Atlas"

#### Scenario: User-facing text uses the product name

- **GIVEN** guidance the app shows when macOS blocks access to a location
- **THEN** it refers to the app as "Storage Atlas"

#### Scenario: Release artifacts carry the product name

- **WHEN** the release operator builds a DMG or an App Store package
- **THEN** the app bundle directory and the artifact file names start with "StorageAtlas"
- **AND** no artifact name contains "Mac" as a name prefix

### Requirement: The bundle identifier and user data locations are preserved

Renaming the product SHALL NOT change the bundle identifier or any location
where the app stores user data. The bundle identifier MUST stay
`de.ltsoftware.macstorageatlas`, so the Mac App Store treats an upload as an
update of the existing app rather than a new application.

#### Scenario: The bundle identifier is unchanged

- **GIVEN** the packaged app bundle
- **THEN** `CFBundleIdentifier` is `de.ltsoftware.macstorageatlas`

#### Scenario: Existing scan history and settings remain readable

- **GIVEN** a user who installed an earlier version
- **WHEN** the renamed version starts
- **THEN** it reads scan history and settings from the same application support directory
