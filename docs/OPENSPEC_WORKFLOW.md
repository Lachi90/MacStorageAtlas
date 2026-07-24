# OpenSpec Workflow

MacStorageAtlas uses [OpenSpec](https://openspec.dev/docs) for feature-level
spec-driven development. The market roadmap remains the portfolio plan;
OpenSpec is the reviewable contract for one concrete change at a time.

## Repository setup

- OpenSpec schema: `spec-driven`
- OpenSpec CLI version used for initialization: `1.6.0`
- AI integration: Codex
- Project configuration: `openspec/config.yaml`
- Current behavior specs: `openspec/specs/`
- Active changes: `openspec/changes/`
- Generated Codex skills: `.codex/skills/`
- Integration branch: `codex/storage-feature-roadmap`

Install the CLI if it is not already available:

```shell
npm install -g @fission-ai/openspec@latest
openspec --version
```

Run `openspec update` after upgrading the global OpenSpec CLI so the generated
Codex skills stay in sync.

## Why the roadmap is not imported wholesale

OpenSpec is delta-first. For an existing application, its recommended workflow
is to specify the behavior being changed now instead of backfilling the entire
codebase or creating every future change in advance.

Therefore:

- `docs/IMPLEMENTATION_ROADMAP.md` owns priority, estimates, dependencies, and
  milestone planning.
- One focused folder under `openspec/changes/` owns the agreement for the next
  implementation unit.
- `openspec/specs/` grows as completed changes are archived.
- A roadmap work package may be split into multiple OpenSpec changes when the
  package contains independently shippable behavior.

## Daily workflow

The `/opsx:*` commands below are typed in the Codex chat. The `openspec ...`
commands are typed in a terminal.

1. Update and verify the integration branch:

   ```shell
   git switch codex/storage-feature-roadmap
   git pull --ff-only
   openspec doctor
   ```

2. Explore the relevant code and roadmap item:

   ```text
   /opsx:explore WP-03 from docs/IMPLEMENTATION_ROADMAP.md
   ```

3. Create one focused change:

   ```text
   /opsx:propose add-file-metadata
   ```

4. Review the generated artifacts in this order:

   1. `proposal.md`: correct problem, scope, dependencies, and non-goals.
   2. Delta spec: testable behavior and edge cases.
   3. `design.md`: safe architecture and explicit trade-offs.
   4. `tasks.md`: small, ordered, verifiable steps.

5. Validate before implementation:

   ```shell
   openspec status --change add-file-metadata
   openspec validate add-file-metadata --strict --no-interactive
   ```

6. Implement from the approved tasks:

   ```text
   /opsx:apply add-file-metadata
   ```

7. Validate code and artifacts:

   ```shell
   dotnet build
   dotnet test
   openspec validate --all --strict --no-interactive
   ```

8. Update the roadmap status table, commit the change artifacts together with
   the code, and push to `codex/storage-feature-roadmap`.

9. Archive a change after its implementation has been accepted on the
   integration branch:

   ```text
   /opsx:archive add-file-metadata
   ```

   MacStorageAtlas uses the "archive inside the integration branch" convention.
   This keeps the resulting current-behavior spec and the implementation
   together when the integration branch is eventually merged to `main`.

## Change naming plan

These are recommended change boundaries, not folders that must all exist now.
Names may be refined during exploration, but the WP identifier must be retained
in the proposal.

| Roadmap item | Suggested OpenSpec changes, in order |
| --- | --- |
| WP-00 | `correct-market-comparison` |
| WP-01 | `sign-and-notarize-releases`, `add-release-update-check` |
| WP-02 | `define-storage-measurement`, `deduplicate-hardlinks`, `investigate-apfs-clone-accounting`, `benchmark-and-optimize-scans` |
| WP-03 | `add-file-metadata`, `add-quick-look` |
| WP-04 | `add-advanced-filters`, `add-filter-presets` |
| WP-05 | `export-scan-results` |
| WP-06 | `guide-full-disk-access` |
| WP-07 | `add-cleanup-basket`, `protect-sensitive-paths` |
| WP-08 | `move-and-copy-items` |
| WP-09 | `persist-scan-history`, `compare-scan-results` |
| WP-10 | `detect-exact-duplicates` |
| WP-11 | `add-developer-storage-insights`, followed by focused catalog additions |
| WP-12 | `add-low-storage-monitor` |
| WP-13 | `show-apfs-snapshots`, then `manage-apfs-snapshots` |
| WP-14 | `add-cloud-storage-source`, then one `add-<provider>-cloud-scan` change per provider |

## Review checklist

Before approving a proposal:

- The change has one intent and can be implemented independently.
- The proposal references exactly one primary roadmap WP.
- Non-goals prevent adjacent roadmap work from leaking into scope.
- Every requirement is observable and testable.
- Scenarios cover the most important failure or safety case.
- The design respects project boundaries.
- Destructive operations remain reversible where possible.
- Privacy and cloud-placeholder behavior are explicit.
- Tasks include tests and documentation with the corresponding behavior.
- Dependencies on earlier OpenSpec changes are named.

## Useful terminal commands

```shell
openspec list
openspec list --specs
openspec show <change-name>
openspec status --change <change-name>
openspec validate <change-name> --strict --no-interactive
openspec validate --all --strict --no-interactive
openspec doctor
openspec view
openspec update
```

The interactive dashboard is opened with `openspec view`. Slash commands such
as `/opsx:propose` and `/opsx:apply` belong in the Codex chat, not the terminal.
