## MODIFIED Requirements

### Requirement: Filtered results are presented consistently across result views

Atlas Storage Analyzer SHALL apply the active filter to the folder tree, the largest-files list, and the file-type summary so that all three describe the same matched files. The largest-files list and the file-type summary MUST describe the matched files that lie inside the insight scope, and the folder tree MUST continue to describe every matched file of the result. The treemap MUST retain its full unfiltered layout and MUST distinguish matching areas without removing non-matching areas.

#### Scenario: List views show matches only

- **GIVEN** a filter is active
- **WHEN** the user views the largest-files list and the file-type summary
- **THEN** both describe only matching files
- **AND** both are consistent with the filtered folder tree

#### Scenario: List views show matches inside the insight scope

- **GIVEN** a filter is active
- **AND** the insight scope is a folder narrower than the scan root
- **WHEN** the user views the largest-files list and the file-type summary
- **THEN** both describe only matching files inside that folder or its
  subfolders
- **AND** every file they list is also shown as a match in the folder tree

#### Scenario: The treemap keeps true proportions

- **GIVEN** a filter is active
- **WHEN** the user views the treemap
- **THEN** the treemap still represents the full unfiltered contents of the displayed level
- **AND** areas remain proportional to the unfiltered sizes

#### Scenario: Matching treemap areas are distinguishable without relying on color alone

- **GIVEN** a filter is active
- **WHEN** the user views the treemap
- **THEN** matching areas are visually distinguished from non-matching areas
- **AND** the distinction does not rely on color alone
- **AND** the number of matches is also stated as text
