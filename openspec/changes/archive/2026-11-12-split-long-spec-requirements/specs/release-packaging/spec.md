# Spec Delta

## MODIFIED Requirements

### Requirement: The universal App Store package runs on both architectures

Atlas Storage Analyzer SHALL produce a universal Mac App Store package whose app
bundle starts on Apple Silicon and on Intel Macs.

#### Scenario: The Apple Silicon slice starts

- **GIVEN** the universal App Store app bundle
- **WHEN** it is launched on an Apple Silicon Mac
- **THEN** the arm64 slice of the native host loads the payload next to itself
- **AND** the app reaches its main window

#### Scenario: The Intel slice starts

- **GIVEN** the universal App Store app bundle
- **WHEN** it is launched on an Intel Mac, or as the x86_64 slice under Rosetta 2
- **THEN** the x86_64 slice of the native host loads the nested x64 payload of the same bundle in the same process
- **AND** the app reaches its main window with the sandbox and container of the outer bundle

#### Scenario: Packaging verifies both architectures

- **WHEN** the release operator builds the universal App Store package
- **THEN** the workflow verifies that the bundle's main executable carries both architectures
- **AND** it verifies that the bundle contains an x86_64 payload

## ADDED Requirements

### Requirement: Each architecture carries a complete self-contained payload

The universal App Store app bundle MUST carry a complete self-contained payload
for each supported architecture, because the precompiled framework assemblies of
a self-contained .NET publish cannot be shared between architectures.

#### Scenario: The bundle holds one payload per architecture

- **GIVEN** the universal App Store app bundle
- **WHEN** the release operator inspects its contents
- **THEN** it contains a complete self-contained arm64 payload
- **AND** it contains a separate complete self-contained x86_64 payload
- **AND** neither slice loads framework assemblies from the other architecture's payload

### Requirement: Both architectures run as the same sandboxed application

Both architectures of the universal App Store package MUST run as the same
application, with the same bundle identifier, sandbox, and container.

#### Scenario: Both slices share one application identity

- **GIVEN** the universal App Store app bundle
- **WHEN** it is launched once as the arm64 slice and once as the x86_64 slice under Rosetta 2
- **THEN** both launches run under the bundle identifier of the outer bundle
- **AND** both use the App Sandbox and container of the outer bundle

### Requirement: Each architecture slice loads its payload in its own process

Each slice of the universal App Store package MUST load its payload in its own
process; packaging MUST NOT replace the process with a nested executable,
because that discards the LaunchServices registration the process received at
launch and makes the slice abort inside the App Sandbox.

#### Scenario: The x86_64 slice keeps its launch registration

- **GIVEN** the universal App Store app bundle is launched through LaunchServices as the x86_64 slice
- **WHEN** the slice loads the nested x64 payload
- **THEN** the payload runs in the process that LaunchServices launched
- **AND** the system log records no denied lookup for `com.apple.coreservices.launchservicesd`
- **AND** the app reaches its main window
