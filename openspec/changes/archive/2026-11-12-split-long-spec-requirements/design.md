# Design

## Context

See `proposal.md` for the motivation. OpenSpec CLI 1.14.3 reports any
requirement description longer than 500 characters as a warning, and
`--strict` turns that warning into a failure. The 14 affected requirements
live in seven main specs that were produced by earlier archived changes. The
CLI's spec instructions describe one supported way to shorten an existing
requirement: in a change made for that purpose, keep the requirement's header
and every scenario under `## MODIFIED Requirements`, cut its description down
to one behavior, and add each removed behavior as its own requirement under
`## ADDED Requirements`.

## Goals / Non-Goals

**Goals:**

- Make `openspec validate --all --strict --no-interactive` pass for every
  main spec once this change is archived.
- Keep the specified behavior identical: every normative sentence survives,
  either in the shortened description or as a new requirement.

**Non-Goals:**

- Touch Core, Rendering, Platform.Mac, App, Tests, packaging, or user
  documentation. No code responsibility moves, so there is no macOS
  compatibility, performance, or privacy impact.

## Decisions

### Split through an OpenSpec change instead of editing main specs

Main specs are the archive output of reviewed changes. Editing them directly
would bypass the proposal, delta review, and strict change validation that
`docs/OPENSPEC_WORKFLOW.md` requires, and would leave no record of why the
requirement structure changed. A dedicated change keeps the edit reviewable
and reproducible through `openspec archive`.

### Keep strict validation instead of dropping `--strict`

Dropping `--strict` from the project's validation commands would make the
suite green while also hiding every future warning, including real problems
such as missing scenarios in other changes. Splitting fixes the 14 findings
and keeps the stronger gate.

### Split by behavior and keep wording

Each modified requirement keeps the behavior its name states. Each other
normative sentence moves into a new requirement with its SHALL or MUST
wording intact. A moved sentence is reworded only where it would otherwise
not stand on its own, for example by naming the subject ("the universal App
Store app bundle" instead of "the bundle"). New requirement names do not
collide with existing names in the same spec or with names added by the
in-flight `attribute-firmlinked-storage` change.

### Ground every new scenario in existing behavior

Each new scenario restates behavior that existing scenarios, tests, packaging
scripts, or documents already establish, such as
`tests/MacStorageAtlas.Core.Tests/Export/`,
`tests/MacStorageAtlas.App.Tests/FilterPresetPersistenceTests.cs`,
`tests/MacStorageAtlas.App.Tests/MainWindowViewModelScanHistoryTests.cs`,
`packaging/updater-bridge.m`, `docs/PACKAGING.md`, and
`docs/STORAGE_MEASUREMENT.md`. No scenario introduces new behavior.

## Risks / Trade-offs

- [A split changes a requirement's meaning] → Shortened descriptions are
  verified to contain only sentences from the original description, and each
  original scenario is kept verbatim.
- [A new scenario describes behavior that does not exist] → Each scenario was
  checked against the cited sources before it was written.
- [Archive conflicts with in-flight changes] → The archive is simulated on a
  copy of `openspec/`, followed by strict validation of all specs and of the
  remaining in-flight changes.
- Rollback: before archive, delete the change folder; after archive, revert
  the archive commit. Neither affects the application.
