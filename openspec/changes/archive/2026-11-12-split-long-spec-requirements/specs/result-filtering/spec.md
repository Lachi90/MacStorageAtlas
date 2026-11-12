# Spec Delta

## MODIFIED Requirements

### Requirement: Date criteria can be expressed relative to evaluation time

Atlas Storage Analyzer SHALL allow each creation, modification, and last-access bound to be expressed either as an absolute instant or as an offset from the time the filter is evaluated. An offset MUST consist of a positive count and a unit of days, weeks, months, or years, and MUST be interpreted as that span before the reference time.

#### Scenario: A relative bound resolves against the reference time

- **GIVEN** a filter specifies that a file was modified more than 18 months before the reference time
- **WHEN** the filter is evaluated
- **THEN** the modification bound is the instant 18 months before the reference time
- **AND** files modified before that instant are matched

#### Scenario: The same relative bound resolves differently as time passes

- **GIVEN** a filter specifies a modification bound of one year before the reference time
- **WHEN** the filter is evaluated at one reference time and then again at a later reference time
- **THEN** each evaluation uses a bound one year before its own reference time
- **AND** the offset recorded in the filter is unchanged by either evaluation

#### Scenario: Absolute and relative bounds combine on the same date dimension

- **GIVEN** a filter specifies an absolute earliest modification instant
- **AND** the same filter specifies a latest modification bound of 30 days before the reference time
- **WHEN** the filter is evaluated
- **THEN** only files modified within the resulting span are matched

#### Scenario: A month or year offset that overruns the month length is clamped

- **GIVEN** a filter specifies a bound of one month before the reference time
- **AND** the reference time falls on a day that the earlier month does not have
- **WHEN** the filter is evaluated
- **THEN** the bound resolves to the last day of that earlier month
- **AND** the resolved instant keeps the reference time's time of day

#### Scenario: A non-positive offset is rejected

- **GIVEN** the user enters a relative bound whose count is zero or negative
- **WHEN** the filter is evaluated
- **THEN** Atlas Storage Analyzer reports that the filter is invalid and explains why
- **AND** it does not present the state as a result with no matches

#### Scenario: A resolved relative bound is reported alongside its offset

- **GIVEN** a filter specifies a relative date bound
- **WHEN** the active filter is described to the user
- **THEN** the description states the offset
- **AND** it states the instant that offset resolved to

### Requirement: Filter presets can be applied and persisted

Atlas Storage Analyzer SHALL provide built-in filter presets and SHALL allow users to save, apply, rename, and delete their own presets through the filter panel. User-created presets MUST persist across application restarts, including any relative date bounds they contain.

#### Scenario: A built-in preset populates the filter

- **GIVEN** built-in presets are available
- **WHEN** the user applies a built-in preset
- **THEN** the filter criteria are set to that preset's criteria
- **AND** the filtered result reflects those criteria

#### Scenario: A user preset survives a restart

- **GIVEN** the user saves the current filter criteria as a named preset
- **WHEN** the application is restarted
- **THEN** that preset is available
- **AND** applying it restores the saved criteria

#### Scenario: A saved relative bound does not drift

- **GIVEN** the user saves a preset whose modification bound is one year before the reference time
- **WHEN** the preset is applied at a later date
- **THEN** the bound resolves to one year before that later date
- **AND** the preset still describes the same span

#### Scenario: A preset saved from a built-in preset's criteria behaves like it

- **GIVEN** the user applies a built-in preset that constrains age
- **AND** saves the resulting criteria unchanged as a user preset
- **WHEN** both presets are applied at a later date
- **THEN** both match the same files

#### Scenario: A preset stored with an absolute bound keeps that bound

- **GIVEN** stored settings contain a preset whose modification bound is an absolute instant
- **WHEN** the preset is loaded and applied at a later date
- **THEN** the bound is that absolute instant
- **AND** it is not converted into a relative bound

#### Scenario: A user preset can be renamed from the filter panel

- **GIVEN** the user has a saved preset
- **WHEN** the user renames it from the filter panel
- **THEN** the preset is listed under the new name
- **AND** its criteria are unchanged
- **AND** the new name persists across an application restart

#### Scenario: A built-in preset cannot be renamed or deleted

- **GIVEN** built-in presets are listed
- **WHEN** the user views a built-in preset
- **THEN** renaming and deleting it are unavailable

#### Scenario: A deleted preset is not restored

- **GIVEN** the user deletes a saved preset
- **WHEN** the application is restarted
- **THEN** that preset is no longer available

#### Scenario: An unreadable stored preset is skipped

- **GIVEN** stored settings contain a preset that cannot be read
- **WHEN** settings are loaded
- **THEN** Atlas Storage Analyzer skips that preset
- **AND** the remaining settings and presets load successfully

#### Scenario: A preset from a newer schema version is skipped

- **GIVEN** stored settings contain a preset recording a schema version the running application does not support
- **WHEN** settings are loaded
- **THEN** Atlas Storage Analyzer skips that preset
- **AND** the remaining settings and presets load successfully

#### Scenario: A preset applies to any scan root

- **GIVEN** a preset defines criteria such as size, age, or category
- **WHEN** the user applies it to a completed scan of any folder
- **THEN** the criteria are evaluated against that scan's results
- **AND** applying the preset does not change the scan root

## ADDED Requirements

### Requirement: Each date bound chooses its form independently

Each date bound MUST carry its own choice of absolute or relative form, independently of the other bounds.

#### Scenario: Different date dimensions use different forms

- **GIVEN** a filter specifies an absolute earliest creation instant
- **AND** the same filter specifies a latest modification bound of 30 days before the reference time
- **WHEN** the filter is evaluated
- **THEN** the creation bound is that absolute instant
- **AND** the modification bound is the instant 30 days before the reference time

#### Scenario: Switching one bound to relative leaves the other bounds unchanged

- **GIVEN** a filter specifies an absolute creation bound and an absolute modification bound
- **WHEN** the user switches the creation bound to a relative offset
- **THEN** the creation bound is that offset
- **AND** the modification bound keeps its absolute instant

### Requirement: A relative date bound is stored as its offset

A relative bound MUST be stored as the offset itself and MUST NOT be stored as the instant it resolved to.

#### Scenario: Saved relative bounds read back as offsets

- **GIVEN** the user saves filter criteria whose modification bound is 18 months and whose last-access bound is 3 weeks before the reference time
- **WHEN** the saved criteria are read back
- **THEN** each bound is the offset that was saved
- **AND** neither bound is an instant resolved at the time of saving

### Requirement: Built-in age presets use relative bounds

A built-in preset that constrains age MUST express that constraint as a relative bound so that it does not drift.

#### Scenario: The built-in age preset follows the reference time

- **GIVEN** the built-in preset that constrains how long ago files were modified
- **WHEN** it is evaluated at one reference time and again three years later
- **THEN** each evaluation uses a modification bound one year before its own reference time
- **AND** the list of built-in presets is the same at both times

#### Scenario: Applying the built-in age preset shows a relative bound

- **GIVEN** the user applies the built-in preset that constrains how long ago files were modified
- **WHEN** the filter panel shows the applied criteria
- **THEN** the modification bound is shown as a relative offset of one year
- **AND** no absolute instant is set for that bound

### Requirement: Unreadable stored presets are skipped

A stored preset that records a schema version the running application does not support, or that cannot be read, MUST be skipped without preventing the remaining settings from loading.

#### Scenario: A malformed preset list does not block other settings

- **GIVEN** stored settings contain other preferences and a preset list that cannot be read
- **WHEN** settings are loaded
- **THEN** no stored preset is restored
- **AND** the other preferences load successfully

#### Scenario: A readable preset beside a newer-version preset still loads

- **GIVEN** stored settings contain a readable preset and a preset recording a newer unsupported schema version
- **WHEN** settings are loaded
- **THEN** the readable preset is available
- **AND** the newer-version preset is not available

### Requirement: Stored absolute preset bounds stay absolute

A stored preset whose date bounds were saved as absolute instants MUST continue to be read as absolute instants and MUST NOT be reinterpreted as relative bounds.

#### Scenario: An absolute preset bound resolves to the same instant at any time

- **GIVEN** stored settings contain a preset whose modification bound is the absolute instant 1 January 2026
- **WHEN** the preset is loaded and evaluated at two different reference times
- **THEN** both evaluations use 1 January 2026 as the modification bound
