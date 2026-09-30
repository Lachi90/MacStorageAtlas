# in-app-updates Specification

## Purpose

Define user-approved, authenticated updates for the Developer ID edition, operation-safe restart and recovery, update privacy, and exclusion from Store and development builds.

## Requirements

### Requirement: Update behavior follows the installed distribution channel

The application SHALL offer in-app update checking and installation only in Developer ID releases. The Mac App Store edition MUST use Store-managed updates and MUST NOT expose direct-update actions or preferences. Development builds MUST NOT contact production update services automatically.

#### Scenario: A direct-release user requests an update check

- **GIVEN** a packaged Developer ID release
- **WHEN** the user selects the application menu's update action
- **THEN** the app checks for compatible direct releases without opening a browser

#### Scenario: The Store edition starts

- **GIVEN** a Mac App Store build
- **WHEN** the application starts and its menu is displayed
- **THEN** no direct-update service is initialized or contacted
- **AND** direct-update actions and automatic-check preferences are absent

#### Scenario: A development build starts

- **GIVEN** an unsigned development build
- **WHEN** the application starts
- **THEN** no automatic request is made to a production update service

### Requirement: Users control automatic update checks

The direct edition SHALL keep automatic checking disabled until the user opts in, SHALL persist the preference, and SHALL allow it to be changed. Enabled automatic checks MUST occur no more frequently than once per 24 hours. Disabling automatic checks MUST retain manual checking and MUST NOT enable silent installation or restart.

#### Scenario: Automatic checks have not been approved

- **GIVEN** a direct installation without a stored automatic-check preference
- **WHEN** the application starts
- **THEN** no automatic update check occurs before the user opts in
- **AND** manual checking is available

#### Scenario: The user enables automatic checks

- **GIVEN** automatic checking is disabled
- **WHEN** the user enables it and later relaunches the app
- **THEN** the preference remains enabled
- **AND** scheduled checks respect the minimum 24-hour interval

#### Scenario: The user disables automatic checks

- **GIVEN** automatic checking was enabled
- **WHEN** the user disables it and later relaunches the app
- **THEN** no further scheduled check starts
- **AND** manual checking remains available

### Requirement: Release selection preserves version and platform compatibility

The updater SHALL select only stable releases newer than the installed bundle version and compatible with the packaged app's architecture and current macOS version. It MUST NOT offer a downgrade, an equal version, a prerelease, or an incompatible download as installable.

#### Scenario: A newer compatible release exists

- **GIVEN** an installed direct release and a newer stable release for its payload architecture and macOS version
- **WHEN** an update check succeeds
- **THEN** the updater offers that release with its version and release notes

#### Scenario: No newer stable release exists

- **GIVEN** available releases are equal, older, or prerelease versions
- **WHEN** a manual update check succeeds
- **THEN** the app reports that no newer stable update is available
- **AND** the installed app remains unchanged

#### Scenario: The app is running under Rosetta

- **GIVEN** an x64 direct build running on Apple Silicon under Rosetta
- **WHEN** a compatible update is selected
- **THEN** the offered artifact matches the x64 app payload

#### Scenario: Only incompatible releases exist

- **GIVEN** newer releases require another architecture or a newer macOS version
- **WHEN** a manual check completes
- **THEN** no incompatible release is offered for installation
- **AND** the result explains that no compatible update is available

### Requirement: Installation follows the standard user-approved update flow

The updater SHALL use the standard flow for in-app download and installation. Initial postponement MUST leave the installed bundle unchanged. Choosing installation SHALL authorize preparing the update, which can complete on ordinary user-requested quit. Prepared-installation actions MUST distinguish immediate restart from installation on quit. Automatic checks alone MUST NOT authorize downloading, replacing the bundle, or restarting. Before an updater-triggered restart the app SHALL explain that current in-memory scan results will close.

#### Scenario: The user approves installation while idle

- **GIVEN** a verified compatible update and no active operation
- **WHEN** the user chooses installation and immediate restart
- **THEN** the updater installs the release and relaunches from its installed location
- **AND** local settings and saved scan history remain available

#### Scenario: The user postpones before download

- **GIVEN** an available update that has not been approved for installation
- **WHEN** the user chooses Remind Me Later
- **THEN** the installed bundle and session remain unchanged
- **AND** no download, installation, or restart occurs

#### Scenario: The user quits after approving preparation

- **GIVEN** the user approved installation and the update has been prepared
- **WHEN** the user chooses ordinary quit or an explicitly labelled Install on Quit action
- **THEN** the prepared installation can complete after termination
- **AND** no automatic immediate restart is triggered merely by becoming idle

### Requirement: Restart does not interrupt active application work

The app MUST defer updater-triggered termination and restart while scanning, duplicate analysis, export, cleanup, Trash, relocation, or required persistence is active. It MUST revalidate eligibility at termination, prevent new conflicting work during final shutdown, and require the user to retry installation after active-work deferral or persistence failure. An approved final shutdown transition MAY continue once its own asynchronous settings flush completes.

#### Scenario: An operation is active when installation is requested

- **GIVEN** an application operation is active and an update is ready
- **WHEN** installation and restart are requested
- **THEN** updater-triggered termination and restart are deferred
- **AND** the operation continues without forced cancellation
- **AND** the user is told to retry when it finishes

#### Scenario: Work starts after initial approval

- **GIVEN** installation was approved while idle but an operation starts before final shutdown
- **WHEN** restart eligibility is checked at termination
- **THEN** updater-triggered termination and restart are deferred
- **AND** becoming idle later does not automatically restart the app

#### Scenario: Final shutdown has been reserved

- **GIVEN** an approved installation has entered its final idle shutdown transition
- **WHEN** a new conflicting operation is requested
- **THEN** that operation does not start before termination
- **AND** required settings persistence completes before the app exits

### Requirement: Update failures remain recoverable and do not block application use

Checking and downloading MUST remain responsive and MUST NOT block startup or scanning. Manual checks SHALL report failures and permit retry; background failures MUST NOT interrupt the user. Downloads SHALL be cancellable before replacement. A failure before replacement MUST leave the existing bundle runnable; replacement failures MUST report a recovery path without modifying user settings or saved history.

#### Scenario: A manual check fails

- **GIVEN** an offline connection, timeout, or malformed feed
- **WHEN** the user checks for updates
- **THEN** the app reports the failure and allows retry
- **AND** scanning and the installed bundle remain usable

#### Scenario: A background check fails

- **GIVEN** automatic checks are enabled and the update service is unavailable
- **WHEN** a scheduled check fails
- **THEN** startup and scanning continue without an error dialog
- **AND** future checks retain the normal schedule

#### Scenario: The user cancels a download

- **GIVEN** an update is downloading and bundle replacement has not started
- **WHEN** the user cancels
- **THEN** the download stops without installation or restart
- **AND** the installed app remains unchanged

#### Scenario: The app runs from a read-only location

- **GIVEN** the app is launched from a mounted read-only disk image
- **WHEN** the user attempts to install an update
- **THEN** replacement is refused with guidance to install the app in a writable location first
- **AND** the current session remains usable

#### Scenario: Replacement cannot complete

- **GIVEN** an approved update cannot replace the app because of permissions or an installation error
- **WHEN** installation fails
- **THEN** the user receives a failure and recovery message
- **AND** local settings and saved history are unchanged

#### Scenario: Replacement fails after application termination

- **GIVEN** an explicitly approved installation fails after the old app exits
- **WHEN** the old app is opened again and the approved version is still missing
- **THEN** the app reports once that installation did not complete and explains how to retry or manually install a signed DMG
- **AND** a successful update or newer manual installation clears the local recovery marker without a failure notice

#### Scenario: Updater initialization fails

- **GIVEN** a direct release with missing updater dependencies or invalid update configuration
- **WHEN** the app starts and the user requests a check
- **THEN** startup succeeds and the manual action explains that updates are unavailable
- **AND** scanning remains usable

### Requirement: Downloaded executable updates are authenticated

The updater MUST use HTTPS update endpoints and authenticate the final update archive against a trusted public key embedded in the installed app before replacement. It MUST also enforce the expected application identity and code-signing validation. Missing or invalid signatures and corrupted downloads MUST NOT cause installation or restart.

#### Scenario: A trusted update passes validation

- **GIVEN** a compatible update with a valid archive signature and expected signed app identity
- **WHEN** download validation completes
- **THEN** installation can be offered for explicit approval

#### Scenario: An update is untrusted or corrupted

- **GIVEN** an update with a missing or invalid archive signature, corrupted contents, or an unexpected app identity
- **WHEN** validation runs
- **THEN** the update is rejected before bundle replacement
- **AND** the existing app remains runnable

### Requirement: Update traffic preserves scan privacy

Update requests and updater logs MUST NOT include scanned names, paths, contents, metadata, hashes, results, cleanup selections, history snapshots, or stable installation identifiers. Optional system profiling and telemetry MUST remain disabled. Documentation SHALL disclose requests to the update host and its access to ordinary connection metadata.

#### Scenario: Checking after a scan

- **GIVEN** an app session containing scan results, duplicate results, and saved history
- **WHEN** any update check or download occurs
- **THEN** its requests and logs contain none of that user data
- **AND** no optional profiling or telemetry is submitted

#### Scenario: The user reads update privacy guidance

- **GIVEN** the updater-enabled edition is publicly released
- **WHEN** the user reads the privacy policy
- **THEN** it identifies the update host and ordinary connection metadata exposure
- **AND** it states that scan data remains local
