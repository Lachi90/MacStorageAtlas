## Purpose

Define how MacStorageAtlas organizes automated tests so test projects mirror production project ownership, keep references narrow, preserve platform gating, and remain runnable through the solution.

## Requirements

### Requirement: Test projects mirror production ownership

MacStorageAtlas SHALL organize automated test projects so each primary production project or tool with tests has a corresponding test project. The old umbrella test project MUST be removed after its tests are migrated.

#### Scenario: Assembly-aligned test projects exist

- **WHEN** a developer inspects the solution test projects
- **THEN** the solution includes test projects for Core, Rendering, Platform.Mac, App, and benchmark tooling
- **AND** each test project name identifies the production project or tool it tests
- **AND** the old `MacStorageAtlas.Tests` project is no longer included

#### Scenario: Test file ownership is visible

- **GIVEN** an existing test file has been moved
- **WHEN** a developer reads its path or namespace
- **THEN** both identify the production project or tool primarily under test

### Requirement: Test project references stay narrow

MacStorageAtlas SHALL keep test project references limited to the production assembly under test and the lower-level assemblies needed by that test project. A test project MUST NOT retain the old umbrella reference set unless every referenced assembly is needed by tests in that project.

#### Scenario: Core tests do not reference UI or platform projects

- **WHEN** a developer inspects the Core test project
- **THEN** it references the Core project
- **AND** it does not reference App, Rendering, Platform.Mac, or benchmark tooling unless a specific Core test requires that reference

#### Scenario: App tests use abstractions for platform behavior

- **WHEN** a developer inspects the App test project
- **THEN** it references App and the lower-level assemblies needed by App-facing tests
- **AND** it does not reference Platform.Mac for macOS adapter behavior

#### Scenario: Platform tests own macOS adapter coverage

- **WHEN** a developer inspects the Platform.Mac test project
- **THEN** it references Platform.Mac and Core
- **AND** tests for macOS Trash, Finder reveal, Quick Look, Full Disk Access, and native metadata live there

### Requirement: Internal access is scoped to owning and fixture-consuming assemblies

MacStorageAtlas SHALL grant internal visibility only to test assemblies that own tests for the granting production assembly or that require internals for lower-level domain fixture construction, plus existing non-test tooling that still requires internal access.

#### Scenario: Core internal access is narrow

- **WHEN** a developer inspects Core assembly attributes
- **THEN** Core grants internal visibility to Core tests
- **AND** Core grants internal visibility to App tests only for App-facing Core domain fixture construction
- **AND** it does not grant broad access to an obsolete umbrella test assembly
- **AND** existing benchmark-tool internal access remains only if still required

#### Scenario: App and Platform internal access is narrow

- **WHEN** a developer inspects App and Platform.Mac assembly attributes
- **THEN** App grants internal visibility to App tests
- **AND** Platform.Mac grants internal visibility to Platform.Mac tests
- **AND** neither grants internal visibility to the obsolete umbrella test assembly

### Requirement: Existing test behavior is preserved

MacStorageAtlas SHALL preserve existing test names, assertions, platform gates, and validation intent while moving tests into aligned projects. The restructure MUST NOT change product behavior.

#### Scenario: Full solution tests still pass

- **WHEN** the implementation is complete
- **THEN** `dotnet test MacStorageAtlas.slnx --no-build` runs the migrated test projects
- **AND** the same platform-gated tests remain skipped or executed according to their existing environment checks

#### Scenario: Product behavior is unchanged

- **WHEN** the test project restructure is implemented
- **THEN** production behavior remains unchanged
- **AND** production project dependencies are not broadened

### Requirement: Solution and documentation remain accurate

MacStorageAtlas SHALL keep solution membership, repository documentation, and validation guidance accurate after the test project split.

#### Scenario: Solution includes every migrated test project

- **WHEN** a developer opens or builds `MacStorageAtlas.slnx`
- **THEN** every new test project is included under the tests solution folder
- **AND** the removed umbrella test project is not referenced

#### Scenario: Documentation reflects the test layout

- **WHEN** a developer reads repository documentation that describes tests or validation
- **THEN** it describes the aligned test project structure or remains correct without mentioning the old umbrella project
