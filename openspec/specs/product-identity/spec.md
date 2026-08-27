## Purpose

Define the user-visible product name and the identifiers that must survive a
rename, so the app carries no Apple trademark in its name and a renamed release
still reaches existing installations as an update.
## Requirements
### Requirement: The user-visible product name carries no Apple trademark

The product SHALL present itself as "Atlas Storage Analyzer" everywhere a user or a
reviewer can see its name. No user-visible name may use "Mac" as a prefix,
because the Guidelines for Using Apple's Trademarks and Copyrights do not permit
it and App Review rejects it under Guideline 5.2.5.

#### Scenario: The installed app identifies itself as Atlas Storage Analyzer

- **GIVEN** the packaged app bundle
- **THEN** `CFBundleName` and `CFBundleDisplayName` are "Atlas Storage Analyzer"
- **AND** the macOS menu bar title is "Atlas Storage Analyzer"
- **AND** the main window title is "Atlas Storage Analyzer"

#### Scenario: User-facing text uses the product name

- **GIVEN** guidance the app shows when macOS blocks access to a location
- **THEN** it refers to the app as "Atlas Storage Analyzer"

#### Scenario: Release artifacts carry the product name

- **WHEN** the release operator builds a DMG or an App Store package
- **THEN** the app bundle directory and the artifact file names start with "AtlasStorageAnalyzer"
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

### Requirement: Specification documents name the product as users see it

The main specs under `openspec/specs/` and the project context in
`openspec/config.yaml` SHALL refer to the product by the same user-visible name
the application presents, so a reader of the specifications and a user of the
app see one product. Renaming a specification document SHALL be editorial: the
requirements, scenarios, and normative keywords it contains MUST keep their
meaning.

#### Scenario: A main spec refers to the product

- **GIVEN** a specification file under `openspec/specs/`
- **WHEN** its prose names the product
- **THEN** it uses the current user-visible product name
- **AND** it does not use a name the application no longer presents

#### Scenario: The project context carries the product name

- **GIVEN** the project context in `openspec/config.yaml`
- **THEN** its product line names the product as the application presents it
- **AND** artifacts generated from that context inherit the current name

#### Scenario: A rename leaves specified behavior unchanged

- **GIVEN** a specification file whose product name is replaced
- **WHEN** the file is compared with its previous revision
- **THEN** every requirement heading, scenario heading, and normative keyword is unchanged apart from the product name

### Requirement: Code-level identifiers are exempt from the product rename

Renaming the product in specification documents SHALL NOT rename assembly,
namespace, project, or solution identifiers, because the code deliberately keeps
its existing names alongside the unchanged bundle identifier. Archived changes
SHALL NOT be rewritten, because they record the agreement that was in force when
they were implemented.

#### Scenario: A spec names a code identifier

- **GIVEN** a specification file that refers to an assembly, namespace, project, or solution
- **WHEN** the product name is replaced elsewhere in that file
- **THEN** identifiers such as `MacStorageAtlas.Core`, `MacStorageAtlas.App`, and `MacStorageAtlas.slnx` keep their existing spelling

#### Scenario: Archived changes stay as recorded

- **GIVEN** a change under `openspec/changes/archive/`
- **WHEN** the product is renamed in the main specs
- **THEN** the archived change is left unmodified
