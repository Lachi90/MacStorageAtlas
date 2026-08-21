## ADDED Requirements

### Requirement: Moving items to Trash uses a sandbox-compatible file API

MacStorageAtlas SHALL move items to the macOS Trash through a file-management API that works inside the macOS App Sandbox. The Trash integration MUST NOT depend on Apple events, Finder automation, or launching an external process, and it MUST NOT permanently delete an item. Failures MUST be reported with the reason macOS gave for that item.

#### Scenario: An eligible item is moved to Trash

- **GIVEN** MacStorageAtlas runs on macOS
- **AND** an existing item the user approved for cleanup
- **WHEN** MacStorageAtlas moves that item to Trash
- **THEN** the item is no longer at its original path
- **AND** the item is recoverable from the macOS Trash

#### Scenario: The sandboxed build moves an item to Trash

- **GIVEN** MacStorageAtlas runs as a sandboxed Mac App Store build
- **AND** the user selected the item's location through the system open panel
- **WHEN** MacStorageAtlas moves that item to Trash
- **THEN** the Trash operation succeeds without requesting an Apple event or automation permission

#### Scenario: macOS refuses the Trash operation

- **GIVEN** macOS refuses to move an item to Trash
- **WHEN** MacStorageAtlas performs the Trash operation for that item
- **THEN** MacStorageAtlas reports a failure for that item
- **AND** the reported failure carries the reason macOS returned
- **AND** the item is not permanently deleted

#### Scenario: The item no longer exists

- **GIVEN** a path that no longer exists on disk
- **WHEN** MacStorageAtlas is asked to move that path to Trash
- **THEN** it reports that the item no longer exists
- **AND** it does not attempt a filesystem change

### Requirement: Revealing an item in Finder uses a sandbox-compatible workspace API

MacStorageAtlas SHALL reveal a selected item in Finder through an in-process macOS workspace API that works inside the macOS App Sandbox. Revealing MUST NOT launch an external process and MUST NOT modify the revealed item.

#### Scenario: An existing item is revealed

- **GIVEN** MacStorageAtlas runs on macOS
- **AND** the selected item still exists on disk
- **WHEN** the user reveals that item in Finder
- **THEN** MacStorageAtlas asks macOS to select that item in Finder
- **AND** it reports the reveal as performed

#### Scenario: The selected item no longer exists

- **GIVEN** the selected item no longer exists on disk
- **WHEN** the user reveals that item in Finder
- **THEN** MacStorageAtlas reports that the item could not be revealed
- **AND** the completed scan result remains visible

### Requirement: The app determines whether it runs inside the App Sandbox

MacStorageAtlas SHALL determine whether the running process is inside the macOS App Sandbox and SHALL make that state available to the access-guidance surface. The determination MUST NOT read file contents and MUST NOT depend on a build-time flag, so one binary behaves correctly in both distribution channels.

#### Scenario: The process runs inside the App Sandbox

- **GIVEN** macOS started MacStorageAtlas with an App Sandbox container
- **WHEN** MacStorageAtlas determines its sandbox state
- **THEN** it reports that it runs inside the App Sandbox

#### Scenario: The process runs outside the App Sandbox

- **GIVEN** macOS started MacStorageAtlas without an App Sandbox container
- **WHEN** MacStorageAtlas determines its sandbox state
- **THEN** it reports that it does not run inside the App Sandbox

### Requirement: macOS platform integration coverage fails on regression

The macOS platform integration tests SHALL fail when a macOS integration they cover stops working on macOS. A test MAY be ignored only because the host platform is not macOS or because a required platform capability is unavailable on the host; it MUST NOT be ignored because the integration returned an error.

#### Scenario: A covered integration breaks on macOS

- **GIVEN** the test suite runs on macOS
- **AND** a covered macOS integration returns an error
- **WHEN** the platform integration tests execute
- **THEN** the affected test fails
- **AND** it is not reported as ignored

#### Scenario: The test host is not macOS

- **GIVEN** the test suite runs on a platform other than macOS
- **WHEN** the macOS platform integration tests execute
- **THEN** they are ignored with a reason that names the unsupported platform
