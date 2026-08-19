## ADDED Requirements

### Requirement: Core source files are grouped by domain folder

`MacStorageAtlas.Core` SHALL organize its source files into folders that correspond to its domain responsibilities rather than keeping them in one flat directory. Each folder MUST correspond to a coherent Core responsibility, and every Core source file except assembly-level metadata MUST live in exactly one such folder.

#### Scenario: Domain folders exist for each Core responsibility

- **WHEN** a developer inspects `src/MacStorageAtlas.Core`
- **THEN** source files are grouped into folders covering scanning, disk-item modelling, filtering, storage insights, cleanup, relocation, export, scan history, access guidance, and host-integration abstractions
- **AND** no domain source file remains directly in the project root except assembly-level metadata under `Properties`

#### Scenario: A file's folder identifies its responsibility

- **GIVEN** a developer opens a Core source file
- **WHEN** they read its folder path
- **THEN** the folder names the Core responsibility that the type serves
- **AND** types belonging to the same responsibility are located in the same folder

#### Scenario: A new Core type has an unambiguous home

- **GIVEN** a developer adds a new type to Core
- **WHEN** the type serves an existing Core responsibility
- **THEN** it is placed in that responsibility's folder rather than in the project root
- **AND** a new folder is introduced only when the type serves a responsibility that no existing folder covers

### Requirement: Core namespaces match folder paths

`MacStorageAtlas.Core` SHALL declare each type in a namespace that matches its folder path relative to the project root, so a type's namespace and its location agree. The project MUST NOT rely on namespace aliases, global usings, or type forwarders to hide a mismatch between folder and namespace.

#### Scenario: Namespace follows folder

- **GIVEN** a Core source file located in a domain folder
- **WHEN** a developer reads its namespace declaration
- **THEN** the namespace is `MacStorageAtlas.Core.<Folder>` for that folder
- **AND** the declaration uses a file-scoped namespace

#### Scenario: Compilation reports no namespace-folder mismatch

- **WHEN** the solution is built and analyzers run
- **THEN** the build reports no namespace-folder mismatch diagnostic for Core
- **AND** no unused `using` directive remains in Core or in any project that consumes Core

#### Scenario: Consumers import the specific namespaces they use

- **GIVEN** a project that references `MacStorageAtlas.Core`
- **WHEN** a developer reads its `using` directives or an AXAML `xmlns` mapping for Core types
- **THEN** each import names the specific Core namespace that supplies the referenced types
- **AND** no import names a Core namespace whose types the file does not use

### Requirement: Host-integration abstractions have a defined home

`MacStorageAtlas.Core` SHALL place an abstraction of a host capability in the folder of the Core responsibility that consumes it, and MUST place host-integration abstractions that no single Core responsibility consumes in a dedicated host-integration folder. Core MUST NOT gain a dependency on Avalonia, `MacStorageAtlas.Platform.Mac`, or `MacStorageAtlas.App` as a result of this placement.

#### Scenario: A consumed abstraction sits with its consumer

- **GIVEN** a Core abstraction that exactly one Core responsibility consumes
- **WHEN** a developer looks for its declaration
- **THEN** it is declared in that responsibility's folder alongside the types that use it

#### Scenario: An unconsumed host abstraction sits in the host-integration folder

- **GIVEN** a Core abstraction that only the application layer invokes and no Core responsibility consumes
- **WHEN** a developer looks for its declaration
- **THEN** it is declared in the dedicated host-integration folder

#### Scenario: Core dependency direction is preserved

- **WHEN** a developer inspects the Core project file after the restructure
- **THEN** Core references no UI, Avalonia, or macOS platform assembly
- **AND** Core's package and project references are unchanged by the folder restructure

### Requirement: The restructure preserves Core behavior and persisted formats

The folder and namespace restructure SHALL be a move-and-rename operation only. It MUST NOT change any public type name, member signature, default value, or observable behavior, and it MUST NOT change the on-disk shape of scan snapshots, scan history entries, or exported CSV and JSON documents.

#### Scenario: Public surface is unchanged apart from namespace

- **WHEN** a developer compares Core's public types before and after the restructure
- **THEN** every type keeps its name, kind, accessibility, and members
- **AND** the only difference is the namespace each type is declared in

#### Scenario: Existing snapshots and exports remain readable

- **GIVEN** a scan snapshot or exported document produced before the restructure
- **WHEN** it is read back after the restructure
- **THEN** it is parsed with the same schema version, field names, and values as before
- **AND** no persisted field encodes a Core namespace or assembly-qualified type name

#### Scenario: Scan behavior is unchanged

- **WHEN** the Core test suite runs after the restructure
- **THEN** the same tests execute with the same assertions and outcomes as before
- **AND** streaming, cancellation, error resilience, hidden-file, package-expansion, symlink, and cycle-detection behavior is unchanged

### Requirement: Repository documentation reflects the Core layout

MacStorageAtlas SHALL keep repository documentation that describes project structure accurate after the Core restructure.

#### Scenario: Structure documentation describes the folder layout

- **WHEN** a developer reads repository guidance that describes what `MacStorageAtlas.Core` owns
- **THEN** the guidance describes the domain-folder layout and the folder-matching namespace convention
- **AND** it does not describe Core as a flat directory of source files

#### Scenario: Documented code references stay valid

- **GIVEN** documentation under `docs/` that names a Core type or namespace
- **WHEN** a developer follows that reference after the restructure
- **THEN** the named type or namespace exists as documented
