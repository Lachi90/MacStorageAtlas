## ADDED Requirements

### Requirement: Public download surfaces present only live distribution channels as available

Atlas Storage Analyzer SHALL describe a distribution channel as available on its
public surfaces only while a user can install the app from that channel, and it
SHALL link every channel it presents as available to the page that installs it.
A channel that is prepared but not yet released MUST be described as planned or
omitted, and MUST NOT be presented as a download. The machine-readable
description of the landing page MUST state the same availability as its prose.

#### Scenario: A distribution channel is live

- **GIVEN** a distribution channel from which a user can install the app
- **WHEN** the landing page and the README describe how to obtain the app
- **THEN** they present that channel as available
- **AND** they link it to the page from which the app installs

#### Scenario: A distribution channel is prepared but not released

- **GIVEN** a distribution channel that has been prepared but not released
- **WHEN** the public surfaces describe how to obtain the app
- **THEN** they describe that channel as planned or omit it
- **AND** they do not present it as a download

#### Scenario: Structured data states the same availability as the prose

- **GIVEN** the structured data of the landing page
- **WHEN** the page presents a distribution channel as available
- **THEN** the offer for that channel states in-stock availability and carries its URL
- **AND** no offer describes a released channel as a pre-order

#### Scenario: A visitor outside the operator's country follows the store link

- **GIVEN** a visitor whose store account belongs to another country
- **WHEN** the visitor follows the store link on a public surface
- **THEN** the link resolves in that visitor's own storefront rather than a fixed one
