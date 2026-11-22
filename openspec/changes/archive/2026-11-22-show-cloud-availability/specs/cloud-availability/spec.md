## ADDED Requirements

### Requirement: Each scanned file states whether its content is on this Mac

Atlas Storage Analyzer SHALL record, for every file included in a scan, whether its content is stored locally (Local), exists only with a cloud provider as a placeholder (Cloud only), or could not be determined (Unknown). The status MUST come from filesystem metadata, MUST be recorded in every measurement mode, and MUST NOT depend on the provider's name.

#### Scenario: A downloaded file is local

- **GIVEN** a file in a cloud-synced folder whose content is stored on this Mac
- **WHEN** a scan includes it
- **THEN** the file's availability is Local

#### Scenario: A placeholder is cloud only

- **GIVEN** a file whose content exists only with its cloud provider
- **WHEN** a scan includes it
- **THEN** the file's availability is Cloud only
- **AND** the scan's measured size for it is unchanged from today's measurement

#### Scenario: The status is recorded in logical mode

- **GIVEN** a scan measured in logical mode
- **WHEN** it includes a cloud-only placeholder
- **THEN** the file's availability is Cloud only

#### Scenario: An unreadable status is unknown

- **GIVEN** a file whose metadata cannot be read
- **WHEN** a scan includes it
- **THEN** its availability is Unknown
- **AND** it is not shown as Local or Cloud only

### Requirement: Folders summarize the availability of their files

Atlas Storage Analyzer SHALL derive a folder's availability from the files the scan included below it: Cloud only when every such file is cloud only, Local when none is, and Partly local when both kinds occur. A folder whose files include an Unknown status and no cloud-only file MUST be Unknown. A folder whose files include cloud-only files and files with an Unknown status MUST be Partly local. An empty folder MUST be Local and MUST NOT change the availability of its parent.

#### Scenario: A mixed folder is partly local

- **GIVEN** a folder containing one local file and one cloud-only file
- **WHEN** the scan completes
- **THEN** the folder's availability is Partly local

#### Scenario: A nested cloud-only folder makes its parent partly local

- **GIVEN** a folder with a local file and a subfolder whose files are all cloud only
- **WHEN** the scan completes
- **THEN** the subfolder is Cloud only
- **AND** the parent folder is Partly local

### Requirement: Availability is visible without relying on color

Atlas Storage Analyzer SHALL mark cloud-only and partly local items in the folder tree and in Largest files with an icon that has a tooltip and an accessible name, and SHALL show an Availability row in the selected-item details. Each marker's tooltip and accessible name MUST state the same availability text as the selected-item details, including the number of cloud-only files and the cloud-only size. Local items MUST NOT carry a marker in the tree or lists.

#### Scenario: A cloud-only file is marked in the tree

- **GIVEN** a completed result containing a cloud-only file
- **WHEN** the user expands its folder in the tree
- **THEN** the file shows the cloud marker
- **AND** VoiceOver announces it as only in the cloud

#### Scenario: Hovering the marker shows how much is only in the cloud

- **GIVEN** a partly local folder with 334 cloud-only files whose logical lengths add up to 2.4 GB
- **WHEN** the user hovers its cloud marker in the tree
- **THEN** the tooltip states that the folder is partly local
- **AND** it states 334 files only in the cloud and 2.4 GB logical, not on this Mac

#### Scenario: Details state the availability

- **GIVEN** the user selects a partly local folder
- **WHEN** the selected-item details are shown
- **THEN** they state that the folder is partly local

### Requirement: The result summarizes local and cloud-only files

Atlas Storage Analyzer SHALL show, for a completed result that contains cloud-only files, the number of local files, the number of cloud-only files, and the cloud-only size, which is the combined logical length of the cloud-only files. The selected-folder details MUST state the cloud-only size of that folder when it contains cloud-only files. Atlas Storage Analyzer MUST NOT show the summary when the result contains no cloud-only file.

#### Scenario: A cloud folder scan shows the summary

- **GIVEN** a completed result with 31 local files and 45 cloud-only files whose logical lengths add up to 12 GB
- **WHEN** the result is displayed
- **THEN** the summary states 31 local files and 45 files only in the cloud
- **AND** it states 12 GB as logical cloud content not stored on this Mac

#### Scenario: A local-only scan shows no summary

- **GIVEN** a completed result without cloud-only files
- **WHEN** the result is displayed
- **THEN** no cloud availability summary is shown

### Requirement: The cloud-only size stays a separately labeled quantity

Atlas Storage Analyzer SHALL label the cloud-only size as logical content that is not stored on this Mac. It MUST NOT add the cloud-only size to the scan's measured byte counts or show it as one of them, and MUST NOT present it as space that can be freed.

#### Scenario: The cloud-only size stays separate in an allocated scan

- **GIVEN** a completed result measured in an allocated mode that contains cloud-only files
- **WHEN** the result is displayed
- **THEN** the measured totals do not include the cloud-only size
- **AND** the cloud-only size is shown only in the labeled summary and details

#### Scenario: A logical scan states that its total includes cloud content

- **GIVEN** a completed result measured in logical mode that contains cloud-only files
- **WHEN** the result is displayed
- **THEN** the summary states that the scan total includes the cloud-only size

### Requirement: Reading availability never changes cloud state

Atlas Storage Analyzer SHALL determine availability from metadata only. It MUST NOT read file contents, request a download, evict local content, pin a file, or call a cloud provider while scanning or displaying availability. If listing a folder whose contents the provider has not fetched would cause the provider to fetch data, Atlas Storage Analyzer MUST NOT list that folder and MUST report it as a cloud folder that was not listed, distinct from an access error.

#### Scenario: Scanning a cloud folder downloads nothing

- **GIVEN** a cloud folder with cloud-only files
- **WHEN** the user scans it
- **THEN** every cloud-only file is still cloud only after the scan
- **AND** no file content was read

#### Scenario: An unfetched cloud folder is reported, not fetched

- **GIVEN** a cloud folder whose listing the provider has not fetched
- **AND** listing it would cause the provider to fetch data
- **WHEN** a scan reaches it
- **THEN** the scan does not list it
- **AND** the result reports it as a cloud folder that was not listed
- **AND** the scan is not reported as complete
