# Spec Delta

## MODIFIED Requirements

### Requirement: Installation follows the standard user-approved update flow

The updater SHALL use the standard flow for in-app download and installation. Initial postponement MUST leave the installed bundle unchanged. Choosing installation SHALL authorize preparing the update, which can complete on ordinary user-requested quit. Prepared-installation actions MUST distinguish immediate restart from installation on quit.

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

## ADDED Requirements

### Requirement: Automatic update checks do not authorize installation

Automatic checks alone MUST NOT authorize downloading, replacing the bundle, or restarting.

#### Scenario: A scheduled check finds a compatible update

- **GIVEN** a direct installation with automatic checking enabled
- **AND** a newer compatible stable release is available
- **WHEN** a scheduled check finds that release
- **THEN** no download, bundle replacement, or restart occurs before the user approves installation
- **AND** the installed bundle remains unchanged

### Requirement: An update restart is announced as closing the current scan

Before an updater-triggered restart the app SHALL explain that current in-memory scan results will close.

#### Scenario: An update is presented while a scan result is open

- **GIVEN** a completed scan result is displayed
- **WHEN** the updater presents an available update
- **THEN** the app explains that installing the update closes the current scan
- **AND** it states that settings and saved scan history remain available

#### Scenario: A deferred restart repeats the explanation

- **GIVEN** an updater-triggered restart was deferred because an operation was active
- **WHEN** the app tells the user how to finish installing the update
- **THEN** it explains that restarting closes in-memory scan results
