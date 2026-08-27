## ADDED Requirements

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
