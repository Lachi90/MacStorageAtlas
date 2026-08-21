## ADDED Requirements

### Requirement: The universal App Store package runs on both architectures

MacStorageAtlas SHALL produce a universal Mac App Store package whose app bundle starts on Apple Silicon and on Intel Macs. The bundle MUST carry a complete self-contained payload for each supported architecture, because the precompiled framework assemblies of a self-contained .NET publish cannot be shared between architectures. Both architectures MUST run as the same application, with the same bundle identifier, sandbox, and container.

#### Scenario: The Apple Silicon slice starts

- **GIVEN** the universal App Store app bundle
- **WHEN** it is launched on an Apple Silicon Mac
- **THEN** the arm64 app host runs directly
- **AND** the app reaches its main window

#### Scenario: The Intel slice starts

- **GIVEN** the universal App Store app bundle
- **WHEN** it is launched on an Intel Mac, or as the x86_64 slice under Rosetta 2
- **THEN** the launcher replaces its own process with the x64 payload of the same bundle
- **AND** the app reaches its main window with the sandbox and container of the outer bundle

#### Scenario: Packaging verifies both architectures

- **WHEN** the release operator builds the universal App Store package
- **THEN** the workflow verifies that the bundle's main executable carries both architectures
- **AND** it verifies that the bundle contains an x86_64 payload
