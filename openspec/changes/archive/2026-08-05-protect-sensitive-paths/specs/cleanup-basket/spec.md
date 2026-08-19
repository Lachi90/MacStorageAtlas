## MODIFIED Requirements

### Requirement: Protected paths are blocked from basket cleanup

MacStorageAtlas SHALL block protected paths from being added to an executable cleanup basket. Protected-path decisions MUST include a user-visible reason and MUST include the current scan root, macOS system locations, Trash locations, paths outside the completed scan result, sensitive user Library subtrees, the current user home directory, and broad user data or media containers when those containers are selected directly.

#### Scenario: Current scan root is protected

- **GIVEN** a completed scan result is displayed
- **WHEN** the user tries to add the current scan root to the cleanup basket
- **THEN** MacStorageAtlas does not add the scan root as an executable cleanup item
- **AND** it explains that the scan root is protected from cleanup

#### Scenario: macOS system path is protected

- **GIVEN** a completed scan result includes a macOS system location
- **WHEN** the user tries to add that location to the cleanup basket
- **THEN** MacStorageAtlas does not add the path as an executable cleanup item
- **AND** it explains that the path is protected

#### Scenario: Trash location is protected

- **GIVEN** a completed scan result includes a Trash location
- **WHEN** the user tries to add that location to the cleanup basket
- **THEN** MacStorageAtlas does not add the path as an executable cleanup item
- **AND** it explains that Trash locations are protected from cleanup

#### Scenario: Outside path is protected

- **GIVEN** a path is not part of the completed scan result
- **WHEN** that path is evaluated for cleanup basket inclusion
- **THEN** MacStorageAtlas blocks the path from basket cleanup
- **AND** it explains that the path is outside the scanned result

#### Scenario: User home container is protected

- **GIVEN** a completed scan result includes the current user's home directory
- **WHEN** the user tries to add the home directory to the cleanup basket
- **THEN** MacStorageAtlas does not add the home directory as an executable cleanup item
- **AND** it explains that broad user data containers are protected from cleanup

#### Scenario: Standard user folder container is protected

- **GIVEN** a completed scan result includes a standard user folder such as Documents, Desktop, Downloads, Movies, Music, or Pictures
- **WHEN** the user tries to add that folder itself to the cleanup basket
- **THEN** MacStorageAtlas does not add the folder as an executable cleanup item
- **AND** it explains that broad user data containers are protected from cleanup

#### Scenario: Ordinary descendant of standard user folder can remain eligible

- **GIVEN** a completed scan result includes an ordinary file inside a standard user folder
- **WHEN** the user tries to add that file to the cleanup basket
- **THEN** MacStorageAtlas does not block the file merely because it is inside that standard user folder

#### Scenario: Sensitive user Library subtree is protected

- **GIVEN** a completed scan result includes a sensitive user Library subtree such as Mail, Messages, Safari, Containers, Group Containers, or Application Support
- **WHEN** a path inside that subtree is evaluated for cleanup basket inclusion
- **THEN** MacStorageAtlas blocks the path from basket cleanup
- **AND** it explains that sensitive application data locations are protected from cleanup

## ADDED Requirements

### Requirement: Selected-item Trash observes protected paths

MacStorageAtlas SHALL apply the same protected-path classification to the single selected-item Move to Trash command before asking for confirmation or invoking platform Trash services. A protected selected item MUST NOT be moved to Trash by MacStorageAtlas and MUST produce a user-visible reason.

#### Scenario: Selected scan root is blocked before confirmation

- **GIVEN** a completed scan result is displayed
- **AND** the current scan root is selected
- **WHEN** the user chooses Move to Trash for the selected item
- **THEN** MacStorageAtlas does not ask for Trash confirmation
- **AND** it does not invoke the platform Trash service
- **AND** it explains that the scan root is protected from cleanup

#### Scenario: Selected sensitive path is blocked before Trash execution

- **GIVEN** a completed scan result includes a protected sensitive path
- **AND** that path is selected
- **WHEN** the user chooses Move to Trash for the selected item
- **THEN** MacStorageAtlas does not move the path to Trash
- **AND** it explains why the path is protected

#### Scenario: Selected eligible file keeps existing confirmation

- **GIVEN** a completed scan result includes an eligible ordinary file
- **AND** that file is selected
- **WHEN** the user chooses Move to Trash for the selected item
- **THEN** MacStorageAtlas asks for Trash confirmation
- **AND** it moves the file to macOS Trash only after confirmation succeeds

### Requirement: Cleanup protection preserves inspection and privacy boundaries

MacStorageAtlas SHALL keep protected-path classification local to scan metadata and normalized paths. Protected-path classification MUST NOT read file contents, hash files, send paths or metadata externally, intentionally materialize cloud placeholders, alter scan results, or block non-mutating inspection actions.

#### Scenario: Protected path remains visible for inspection

- **GIVEN** a completed scan result includes a protected path
- **WHEN** MacStorageAtlas displays the scan result
- **THEN** the protected path remains visible according to the normal scan and filter behavior
- **AND** Reveal in Finder and Quick Look remain governed by their existing non-mutating workflows

#### Scenario: Protection uses metadata only

- **GIVEN** MacStorageAtlas classifies a selected path for cleanup protection
- **WHEN** the classification is evaluated
- **THEN** it uses normalized paths and scan metadata
- **AND** it does not read or persist file contents
- **AND** it does not send paths or metadata externally

#### Scenario: Cloud placeholder remains unmaterialized during blocked cleanup

- **GIVEN** a protected cleanup candidate is a cloud-backed placeholder
- **WHEN** MacStorageAtlas blocks cleanup for that path
- **THEN** it does not intentionally download the placeholder contents
- **AND** the completed scan result remains based on the original scan metadata
