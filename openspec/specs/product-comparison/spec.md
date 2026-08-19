## Purpose

Define how MacStorageAtlas presents public feature comparisons so claims remain
accurate, verifiable, and clearly separated from planned work.

## Requirements

### Requirement: Comparison claims use current primary evidence

Every competitor capability claim in the public comparison SHALL be supported
by a linked official product page, official documentation, or another primary
source, and the comparison SHALL display the date on which those claims were
last verified.

#### Scenario: Reviewer verifies a competitor capability

- **GIVEN** the README states that a competitor supports or lacks a capability
- **WHEN** a reviewer follows the source associated with that comparison
- **THEN** the source is an official or primary source that supports the claim
- **AND** the comparison states its last verification date

#### Scenario: A capability cannot be verified

- **GIVEN** a competitor capability cannot be confirmed from a current primary
  source
- **WHEN** the public comparison is updated
- **THEN** the comparison omits the claim or describes it as unverified rather
  than presenting it as fact

### Requirement: Storage measurement semantics are distinguished

The comparison SHALL distinguish logical file size, allocated file blocks, and
hardlink/clone-aware unique physical storage instead of grouping them under one
undifferentiated real-size label.

#### Scenario: MacStorageAtlas allocated-size behavior is described

- **GIVEN** MacStorageAtlas measures allocated blocks for scanned files
- **AND** it does not yet deduplicate all hardlink or APFS clone storage
- **WHEN** its storage measurement capability is compared publicly
- **THEN** the comparison describes allocated-block measurement as implemented
- **AND** does not claim unique physical-storage accounting

### Requirement: Implemented and planned capabilities remain separate

The public comparison MUST present only behavior available in the referenced
MacStorageAtlas release as implemented, while roadmap behavior MUST be labeled
as planned or omitted from the implemented-feature table.

#### Scenario: A roadmap feature has not shipped

- **GIVEN** a feature appears in `docs/IMPLEMENTATION_ROADMAP.md`
- **AND** the feature is not implemented in the referenced release
- **WHEN** the README comparison is rendered
- **THEN** the feature is not marked as currently available

### Requirement: Comparison scope remains maintainable

The README comparison SHALL remain a concise summary of differentiating
capabilities rather than a comprehensive competitor database.

#### Scenario: New market research contains extensive detail

- **GIVEN** research covers prices, versions, and many secondary capabilities
- **WHEN** the README comparison is updated
- **THEN** only durable, decision-relevant distinctions are added to the README
- **AND** volatile or extensive research remains in a separate document
