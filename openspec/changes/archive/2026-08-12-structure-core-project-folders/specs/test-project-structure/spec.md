## ADDED Requirements

### Requirement: Test project layout mirrors the production project layout

MacStorageAtlas SHALL organize the internal folders of a test project to mirror the folder layout of the production project it tests, whenever that production project groups its source files into folders. Each test file MUST live in the folder that mirrors the production folder holding the code under test, and its namespace MUST match that mirrored folder path.

#### Scenario: Test folders mirror production folders

- **GIVEN** a production project whose source files are grouped into domain folders
- **WHEN** a developer inspects the corresponding test project
- **THEN** the test project contains a folder for each production folder that has tests
- **AND** each mirrored folder carries the same name as the production folder it covers

#### Scenario: A test file sits opposite the code it exercises

- **GIVEN** a test file that primarily exercises a production type
- **WHEN** a developer reads the test file's path
- **THEN** its folder mirrors the folder holding the type under test
- **AND** its namespace is the test assembly's root namespace followed by that mirrored folder path

#### Scenario: A production folder without tests needs no mirrored folder

- **GIVEN** a production folder that has no dedicated tests
- **WHEN** a developer inspects the corresponding test project
- **THEN** no empty mirrored folder is created for it
- **AND** the absence of the folder is not treated as a structural violation

#### Scenario: Mirroring preserves existing test behavior

- **WHEN** test files are moved into mirrored folders
- **THEN** test names, assertions, fixtures, platform gates, and temporary-directory handling are unchanged
- **AND** `dotnet test MacStorageAtlas.slnx --no-build` runs the same tests with the same outcomes
