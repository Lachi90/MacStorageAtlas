## ADDED Requirements

### Requirement: The sandboxed build starts without a temporary entitlement exception

The Mac App Store build SHALL start inside the App Sandbox without requesting
any temporary entitlement exception. The bundle's main executable MUST have
CoreFoundation as a load-time dependency, so the process completes its
LaunchServices registration during launch instead of falling back to a
mach-lookup of `com.apple.coreservices.launchservicesd` by global name, which
the App Sandbox profile denies.

#### Scenario: The App Store entitlements request no exception

- **GIVEN** the App Store entitlements used to sign the submitted build
- **THEN** they enable App Sandbox, the CoreCLR JIT, and user-selected file access
- **AND** they declare no `com.apple.security.temporary-exception` key of any kind

#### Scenario: The sandboxed app reaches its main window

- **GIVEN** an app bundle signed with the App Store entitlements
- **WHEN** it is launched through LaunchServices
- **THEN** the app reaches its main window
- **AND** the system log records no denied lookup for `com.apple.coreservices.launchservicesd`

#### Scenario: The main executable links CoreFoundation

- **GIVEN** the packaged app bundle
- **WHEN** the release operator inspects the main executable's load commands
- **THEN** CoreFoundation is listed as a load-time dependency

### Requirement: The bundle's main executable is the native host

Packaging SHALL replace the published .NET app host with a native host compiled
from `packaging/native-host.c`. The native host MUST resolve its runtime payload
directory, load `libhostfxr.dylib` from it, and start the managed entry assembly
in process. Packaging MUST NOT leave the published app host in the bundle.

#### Scenario: Packaging installs the native host

- **WHEN** the release operator builds any app bundle
- **THEN** the bundle's `CFBundleExecutable` is the compiled native host
- **AND** the published .NET app host is not present in the bundle

#### Scenario: The native host fails loudly

- **GIVEN** a bundle whose runtime payload cannot be resolved
- **WHEN** the native host runs
- **THEN** it exits with a non-zero status instead of continuing

## MODIFIED Requirements

### Requirement: The universal App Store package runs on both architectures

Storage Atlas SHALL produce a universal Mac App Store package whose app bundle
starts on Apple Silicon and on Intel Macs. The bundle MUST carry a complete
self-contained payload for each supported architecture, because the precompiled
framework assemblies of a self-contained .NET publish cannot be shared between
architectures. Both architectures MUST run as the same application, with the
same bundle identifier, sandbox, and container. Each slice MUST load its payload
in its own process; packaging MUST NOT replace the process with a nested
executable, because that discards the LaunchServices registration the process
received at launch and makes the slice abort inside the App Sandbox.

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
