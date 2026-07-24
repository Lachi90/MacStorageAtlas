# MacStorageAtlas Implementation Roadmap

This document turns the market analysis into an implementation plan. It is the
working roadmap for the features that move MacStorageAtlas from a capable disk
visualizer to a trustworthy, precise, and safe macOS storage-management tool.

Feature-level planning and implementation follows the
[`OPENSPEC_WORKFLOW.md`](OPENSPEC_WORKFLOW.md). The roadmap controls priority and
sequencing; each implementation unit receives a focused OpenSpec change before
code is written.

## Working branch

- Integration branch: `codex/storage-feature-roadmap`
- Implementation work is committed and pushed to this branch before it is
  merged into `main`.
- Every non-trivial feature starts as a focused change under
  `openspec/changes/`; roadmap work packages may be split into multiple changes.
- Each work package below should use one or more focused commits. Unrelated work
  must not be mixed into the same commit.
- The branch must remain buildable and testable after every completed work
  package.

## Product direction

MacStorageAtlas should be positioned as:

> A transparent, open-source storage inspector for macOS with precise numbers,
> understandable results, and safe, user-controlled cleanup.

The goal is not to copy every function of a broad cleaner suite. The app should
remain focused on storage analysis and deliberate cleanup. Automatic deletion,
malware scanning, memory optimization, and generic system-tuning features are
out of scope.

## Planning assumptions

- Estimates are person-days for one developer familiar with C#, Avalonia, and
  macOS.
- Estimates include implementation, automated tests, and basic documentation.
- Estimates do not include Apple review times, waiting for certificates, or
  legal review of third-party cloud integrations.
- Every macOS-specific capability must have a testable interface in Core or App
  and an implementation in `MacStorageAtlas.Platform.Mac`.
- Potentially destructive operations must be reversible where possible and
  require explicit confirmation.
- No scan result, file name, path, hash, or usage data is transmitted unless a
  future user explicitly configures a cloud provider.

## Current baseline

The repository already provides:

- Folder and volume selection.
- Asynchronous scanning with cancellation and throttled progress updates.
- A size-sorted folder tree and interactive treemap.
- File-type statistics and a largest-files list.
- Search by name or path.
- Scan-error collection.
- Finder reveal and move-to-Trash actions.
- Logical-size and allocated-size measurement.
- Options for hidden files, symbolic links, and application bundles.
- Recent scan locations and persisted settings.
- Self-contained arm64 and x64 DMG packaging.

The main gaps are trusted distribution, exact physical-size accounting,
metadata-based analysis, multi-item cleanup, Full Disk Access guidance, and
long-term comparison workflows.

## Priority and dependency overview

| ID | Work package | Priority | Estimate | Depends on | Target milestone |
| --- | --- | --- | ---: | --- | --- |
| WP-00 | Correct product and competitor documentation | P0 | <1 day | None | M0 |
| WP-01 | Signing, notarization, and update check | P0 | 3-6 days | Apple Developer account | M0 |
| WP-02 | Hardlink/APFS correctness and scan benchmarks | P0 | 10-18 days | None | M0-M1 |
| WP-03 | Quick Look and file metadata | P1 | 3-5 days | None | M1 |
| WP-04 | Advanced filters and saved filter presets | P1 | 4-7 days | WP-03 | M1 |
| WP-05 | CSV and JSON export | P1 | 1-2 days | WP-03 recommended | M1 |
| WP-06 | Full Disk Access assistant | P1 | 5-9 days | WP-01 recommended | M1 |
| WP-07 | Cleanup basket and multi-selection | P1 | 6-10 days | None | M2 |
| WP-08 | Move and copy to another location | P1 | 3-6 days | WP-07 | M2 |
| WP-09 | Scan history and comparison | P2 | 7-12 days | WP-03 | M3 |
| WP-10 | Exact duplicate detection | P2 | 8-14 days | WP-07 recommended | M3 |
| WP-11 | Developer-storage insights | P2 | 8-15 days | WP-03, WP-07 | M3 |
| WP-12 | Low-storage monitor and notifications | P2 | 4-7 days | WP-01 | M4 |
| WP-13 | APFS and Time Machine snapshot insights | P3 | 10-20 days | WP-06 | M4 |
| WP-14 | Direct cloud-storage scans | P3 | 15-30 days/provider | WP-02, WP-06 | M5 |

## Milestones

### M0 - Trustworthy distribution and claims

Deliver WP-00, WP-01, and the hardlink portion of WP-02.

Exit criteria:

- Public downloads open without a Gatekeeper bypass.
- Release artifacts are signed, notarized, and have verifiable checksums.
- Product documentation makes only verified comparison claims.
- Hardlinked files are not counted more than once in allocated-size mode.
- A repeatable scan benchmark exists.

### M1 - Better analysis

Deliver the remaining WP-02 work plus WP-03 through WP-06.

Exit criteria:

- Users can inspect relevant dates and preview files with Quick Look.
- Results can be filtered by size, age, type, path, and visibility.
- Results can be exported for offline analysis.
- Missing Full Disk Access is detected and clearly explained.
- Allocated-size limitations are documented and covered by tests.

### M2 - Safe cleanup workflow

Deliver WP-07 and WP-08.

Exit criteria:

- Multiple files can be collected from all result views.
- The app shows the total selected and expected reclaimable space.
- Protected or dangerous locations cannot be accidentally selected.
- Users can choose between moving to Trash and moving/copying elsewhere.
- Partial failures leave the scan model and filesystem state consistent.

### M3 - Differentiation

Deliver WP-09 through WP-11 in that order unless user feedback changes the
priority.

Exit criteria:

- Users can see what grew between two scans.
- Exact duplicates are found without hashing every file unnecessarily.
- Common developer storage consumers are explained and safely reviewable.

### M4 - Proactive macOS storage management

Deliver WP-12 and WP-13.

Exit criteria:

- Users can opt into a local low-storage warning.
- APFS/Time Machine snapshots are visible and understandable.
- Snapshot deletion, if implemented, uses explicit confirmation and documented
  macOS commands or APIs.

### M5 - Optional cloud expansion

Deliver WP-14 only after validating demand and selecting one provider.

Exit criteria:

- A provider can be connected and revoked safely.
- Remote metadata can be scanned without downloading file contents.
- Rate limits, pagination, retries, and token storage are handled.
- Local and remote sizes are clearly distinguished in the UI.

---

## Detailed work packages

## WP-00 - Correct product and competitor documentation

### Outcome

The README accurately describes current capabilities and avoids unsupported
claims about competitors.

### Scope

- Correct the current comparison row for physical/on-disk size.
- Separate `allocated file blocks` from `hardlink/clone-aware unique physical
  storage`.
- Add a `Last verified` date to market-comparison content.
- Prefer links to official competitor documentation.
- Keep the README comparison small; detailed research belongs in a separate
  market document if it is retained.

### Acceptance criteria

- No competitor is shown as missing a feature it officially documents.
- MacStorageAtlas limitations are stated in neutral language.
- Documentation distinguishes implemented features from roadmap items.

### Tests and verification

- Manual link check.
- Review all comparison statements against their linked primary source.

### Estimate

Less than 1 day.

---

## WP-01 - Signing, notarization, and update check

### Outcome

Users can install a trusted build without bypassing Gatekeeper and can discover
new releases.

### Scope

1. Add stable bundle identifiers and semantic versions to the app bundle.
2. Sign all nested binaries and the final `.app` with hardened runtime enabled.
3. Notarize the app or DMG with `notarytool`.
4. Staple and validate the notarization ticket.
5. Produce SHA-256 checksums for release artifacts.
6. Add a CI/release workflow with credentials supplied only as secrets.
7. Add a non-intrusive update check against a signed release feed or GitHub
   Releases.
8. Do not implement silent background installation in the first iteration.

### Architecture

- Packaging/release scripts remain at repository root.
- Update-check abstractions belong in App; macOS opening/install behavior belongs
  in Platform.Mac.
- The update feed must not receive file paths or scan information.

### Acceptance criteria

- `codesign --verify --deep --strict` succeeds.
- `spctl --assess` accepts the packaged application.
- `xcrun stapler validate` succeeds.
- A clean Mac can download and open the release normally.
- Update checks can be disabled.
- Network and parse failures do not block app startup.

### Tests and verification

- Unit-test version comparison and release-feed parsing.
- Test no-update, update-available, malformed-feed, timeout, and offline cases.
- Perform a manual clean-machine Gatekeeper test for both arm64 and x64 builds.

### Risks

- Requires a paid Apple Developer account and secure certificate handling.
- Avalonia/.NET single-file packaging may contain nested components that require
  an explicit signing order.

### Estimate

3-6 days, excluding account setup and Apple processing time.

---

## WP-02 - Hardlink/APFS correctness and scan benchmarks

### Outcome

The app reports defensible storage numbers and has measurable scan-performance
targets.

### Phase A: measurement specification and benchmark

- Define the exact meaning of:
  - Logical size.
  - Allocated file size.
  - Unique allocated size.
  - Volume free, used, available, and purgeable space.
- Add fixture generators for:
  - Normal files.
  - Sparse files.
  - Hardlinks.
  - Symbolic links.
  - Application packages.
  - Cloud placeholders where practical.
- Add a benchmark command that records duration, entry count, throughput,
  peak managed memory, and error count.
- Compare representative results with Finder, `du`, and `stat`.

### Phase B: hardlink deduplication

- Add a macOS file-identity reader based on device and inode.
- Track file identities only when unique allocated-size mode is active.
- Count the allocated bytes for a hardlinked file once.
- Retain every path in the result tree, but mark additional links as sharing
  storage.
- Make shared-size semantics visible in item details and exports.

### Phase C: APFS clone investigation and implementation

- Start with a time-boxed technical spike.
- Determine which supported public macOS API can reliably expose shared physical
  extents or clone identity.
- If exact clone accounting is not possible with stable public APIs:
  - Do not infer clone sharing from equal content.
  - Document the limitation.
  - Keep hardlink correctness and allocated-block measurement.
- If it is possible, introduce a platform capability rather than APFS logic in
  portable Core.

### Phase D: performance

- Establish a baseline before changing traversal.
- Investigate bounded parallel metadata reads without unbounded memory growth.
- Keep progress ordered enough to be understandable, but do not require strict
  filesystem order.
- Ensure cancellation stops new work and drains active work promptly.
- Avoid retaining duplicate full scan trees or path lists.

### Acceptance criteria

- Hardlinks are counted once in unique allocated-size mode.
- All hardlink paths remain browsable.
- Sparse files use allocated rather than logical size when configured.
- Cancellation still preserves a consistent partial result.
- A one-million-entry scan does not cause unbounded queues or UI updates.
- Benchmark results are documented and reproducible.
- APFS clone support is either tested or explicitly documented as unsupported.

### Tests and verification

- Core unit tests with injectable file identities and size readers.
- macOS integration tests using temporary hardlinks and sparse files.
- Cancellation and error-injection tests.
- Memory and throughput benchmark on SSD and one slower/external volume.

### Risks

- Exact APFS shared-block accounting may not be exposed through a stable public
  API.
- Parallel filesystem traversal can make network volumes slower and complicate
  cancellation.
- A global identity set consumes memory on very large scans.

### Estimate

10-18 days, including the APFS technical spike.

---

## WP-03 - Quick Look and file metadata

### Outcome

Users can determine what a file is and whether it is old before acting on it.

### Scope

- Extend `DiskItem` with an immutable metadata model:
  - Creation time where available.
  - Modification time.
  - Last-access time where reliable.
  - Logical and allocated size.
  - File identity/share status from WP-02.
- Read metadata during the existing filesystem visit to avoid a second full
  traversal.
- Add a Quick Look command using the macOS-native preview experience.
- Bind Space to Quick Look and Command-I to item details.
- Show unavailable or unreliable dates as unknown, not as a fabricated value.

### Acceptance criteria

- File and folder details display available metadata.
- Pressing Space previews the selected item from every result view.
- Missing or removed files produce a friendly status message.
- Metadata failures are recoverable scan errors where appropriate.
- The feature does not materialize cloud placeholders.

### Tests and verification

- Unit-test metadata mapping and formatting.
- Test files with known timestamps.
- Test Quick Look service command enablement and failure behavior.
- Manually verify common images, videos, PDFs, archives, and folders.

### Estimate

3-5 days.

---

## WP-04 - Advanced filters and saved presets

### Outcome

Users can reduce large scans to actionable questions instead of browsing the
entire tree.

### Scope

- Filter by:
  - Name and path.
  - Minimum and maximum size.
  - Creation, modification, and last-access age.
  - File extension or category.
  - File versus folder.
  - Hidden status.
  - Package membership.
  - Shared/hardlinked status.
- Combine filters with clear AND semantics in the first version.
- Add built-in presets:
  - Larger than 1 GB.
  - Not modified for one year.
  - Large downloads.
  - Large archives.
- Persist user-created presets in settings.
- Show match count and total matched size.

### Architecture

- Filtering remains a pure Core/App-view-model operation.
- Do not duplicate the scan tree to represent filtered results.
- Use a filter record that can be serialized independently of UI controls.

### Acceptance criteria

- Filters update the tree, largest-files list, and summary consistently.
- Clearing filters restores the full result without rescanning.
- Empty or contradictory filters produce a clear empty state.
- Selected items are cleared if they are no longer visible.
- Presets survive an app restart.

### Tests and verification

- Unit-test every predicate and common combinations.
- Test boundary values and unknown dates.
- Add view-model tests for totals, selection, and preset persistence.

### Estimate

4-7 days.

---

## WP-05 - CSV and JSON export

### Outcome

Users can analyze, archive, or share scan results outside the app.

### Scope

- Export the current full scan or current filtered result.
- CSV fields:
  - Path.
  - Name.
  - Item type.
  - Logical size.
  - Allocated/unique size.
  - Creation, modification, and access dates.
  - File category/extension.
  - Shared-storage indicator.
- JSON contains a versioned schema and scan metadata.
- Include root path, scan time, scan options, total counts, and errors.
- Use a native save-file picker.
- Stream large exports instead of building one huge string in memory.

### Acceptance criteria

- CSV opens correctly in common spreadsheet tools.
- Paths containing commas, quotes, and newlines are escaped correctly.
- JSON can round-trip through a schema/model test.
- Export cancellation and write errors are handled without a partial file being
  presented as complete.

### Tests and verification

- Golden-file tests for CSV escaping and JSON schema.
- Large-tree streaming test.
- Manual import into Numbers or another spreadsheet application.

### Estimate

1-2 days.

---

## WP-06 - Full Disk Access assistant

### Outcome

Users understand incomplete scans and can grant the correct permission without
guesswork.

### Scope

- Add a macOS access-status service.
- Detect likely missing Full Disk Access through documented checks and scan-error
  patterns.
- Show:
  - Whether the scan appears incomplete.
  - How many paths were inaccessible.
  - The difference between inaccessible and purgeable space.
- Add a button to open the relevant Privacy & Security settings.
- Explain that the user must grant access manually and may need to restart the
  app.
- Rescan after access is granted.
- Never request an administrator password inside the app.

### Acceptance criteria

- The assistant does not claim access is granted solely because one test path is
  readable.
- Permission errors remain visible in the normal error view.
- The app works normally when Full Disk Access is not granted.
- The settings deep-link has a documented fallback if macOS changes it.

### Tests and verification

- Unit-test classification of permission-related scan errors.
- Test granted, denied, indeterminate, and settings-open-failure states.
- Manual test on a clean macOS user account.

### Risks

- macOS does not provide a simple authoritative Full Disk Access status API.
- Settings URLs may change between macOS versions.

### Estimate

5-9 days.

---

## WP-07 - Cleanup basket and multi-selection

### Outcome

Users can build and review one safe cleanup operation across all result views.

### Scope

- Introduce a cleanup basket containing references to scanned items.
- Add/remove items from:
  - Folder tree.
  - Treemap.
  - Largest-files list.
  - Filtered results.
  - Future duplicate and developer-insight views.
- Show:
  - Item count.
  - Total logical size.
  - Expected uniquely reclaimable size.
  - Missing or changed items.
- Prevent selecting both a directory and its descendant twice.
- Add protected-path policy for macOS system locations and the current scan root.
- Add a final review dialog before filesystem changes.
- Revalidate existence, identity, and size immediately before acting.

### Architecture

- Cleanup planning belongs in Core and contains no UI types.
- Filesystem mutations go through platform service interfaces.
- The scan tree is updated only after the platform service confirms success.

### Acceptance criteria

- The same item cannot be added twice.
- Parent/child overlap does not overstate reclaimable space.
- Protected items clearly explain why they cannot be selected.
- Review shows the exact operation and destination.
- Cancellation before confirmation makes no changes.
- Partial failures identify each failed item and keep successful changes.

### Tests and verification

- Unit-test deduplication, parent/child overlap, totals, and protected paths.
- View-model tests for selections from each result view.
- Integration tests using temporary files and Trash where practical.

### Estimate

6-10 days.

---

## WP-08 - Move and copy to another location

### Outcome

Users can reclaim local space by archiving large data instead of deleting it.

### Scope

- Add Move and Copy actions to the cleanup basket.
- Use a native destination folder picker.
- Preflight:
  - Destination free space where available.
  - Name collisions.
  - Read-only destinations.
  - Moving into a source descendant.
- Offer explicit collision policies: skip, rename, or replace.
- Default to skip; replacing requires another confirmation.
- Show per-item progress and allow cancellation between files.
- Preserve metadata where supported.
- Update the scan model after successful moves.

### Acceptance criteria

- Cross-volume moves fall back to copy-then-verified-delete.
- A failed copy never deletes the source.
- Cancellation leaves completed items valid and reports remaining items.
- Moving a directory into itself or a descendant is blocked.
- The expected locally reclaimed size is shown before execution.

### Tests and verification

- Unit-test preflight and collision policies.
- Integration-test same-volume and cross-volume behavior.
- Test cancellation, insufficient space, read-only destination, and disappeared
  source.

### Estimate

3-6 days.

---

## WP-09 - Scan history and comparison

### Outcome

Users can answer which folders grew or shrank between two points in time.

### Scope

- Save a compact, versioned scan snapshot locally.
- Make history opt-in per location or globally configurable.
- Store paths, item types, sizes, relevant dates, scan options, and stable file
  identities where available.
- Add retention controls by count and total storage.
- Compare snapshots by stable identity first and normalized path second.
- Classify:
  - Added.
  - Removed.
  - Grown.
  - Shrunk.
  - Moved/renamed when identity supports it.
- Show top growth and shrinkage by file and directory.
- Do not silently treat different scan options as directly comparable.

### Architecture

- Add a versioned persistence interface separate from user settings.
- Use atomic writes and recover gracefully from corrupt history files.
- Do not deserialize arbitrary runtime types.

### Acceptance criteria

- Users can select any two compatible scans for the same root.
- Directory deltas equal the aggregate child change within defined semantics.
- Deleted and moved items are represented correctly.
- History can be cleared without affecting current scan settings.
- Old schema versions either migrate or fail with an actionable message.

### Tests and verification

- Snapshot round-trip and schema-version tests.
- Comparison tests for add, remove, move, grow, shrink, and path-case behavior.
- Corrupt-file and interrupted-write tests.
- Performance test on large synthetic snapshots.

### Estimate

7-12 days.

---

## WP-10 - Exact duplicate detection

### Outcome

Users can find byte-identical duplicate files with a low false-positive risk.

### Scope

1. Group regular files by logical size.
2. Ignore single-entry groups and zero-length files by default.
3. Compare a small beginning/end sample for remaining candidates.
4. Hash full contents only for candidates that still match.
5. Confirm equality before presenting a group as exact duplicates.
6. Keep hardlinks out of duplicate-waste totals.
7. Do not automatically download cloud placeholders.
8. Add a duplicate review view integrated with the cleanup basket.
9. Never auto-select a file for deletion in the first version.

### Acceptance criteria

- Files are labeled duplicates only after exact verification.
- Hardlinks are shown as links, not reclaimable duplicate copies.
- Hashing can be cancelled and reports progress.
- Changed-during-scan files are discarded or revalidated.
- The app explains why a cloud-only file was skipped.
- Reclaimable totals preserve at least one copy in every group.

### Tests and verification

- Known equal and same-size-different-content fixtures.
- Changed-while-hashing, read-error, cancellation, and hardlink tests.
- Large-file streaming test proving contents are not buffered in memory.

### Risks

- Reading all candidate contents can be slow and can wake external disks.
- Last-access timestamps may be changed by reading on some filesystems.

### Estimate

8-14 days.

---

## WP-11 - Developer-storage insights

### Outcome

MacStorageAtlas becomes especially useful to developers by explaining large,
regenerable build and tool data.

### Initial supported categories

- Xcode DerivedData and Archives.
- iOS/watchOS/tvOS simulator devices and runtimes.
- Docker Desktop images, volumes, and caches.
- Homebrew download caches.
- NuGet global packages and HTTP cache.
- npm, Yarn, and pnpm caches.
- Gradle caches.

### Scope

- Define every category as a versioned rule with:
  - Known paths.
  - Detection logic.
  - Human-readable origin.
  - Whether data is normally regenerable.
  - Risk level.
  - Preferred vendor-supported cleanup action.
- Show categories as insights, not automatic cleanup recommendations.
- Prefer vendor CLI dry-run/list commands when filesystem deletion could corrupt
  tool state.
- Integrate eligible paths with the cleanup basket.
- Make the rule catalog easy for contributors to extend and test.

### Acceptance criteria

- A rule never matches a broad parent such as the entire home or Library folder.
- Every rule has representative positive and negative path tests.
- The UI explains consequences before cleanup.
- Unsupported tool versions do not silently use unsafe commands.
- Users can disable individual insight categories.

### Tests and verification

- Fixture-based rule tests without requiring every developer tool to be
  installed.
- Manual validation against currently supported tool versions.
- Safety review for every cleanup operation.

### Risks

- Tool directory layouts and supported cleanup commands change over time.
- Docker and simulator data should often be managed through their own tools
  rather than raw file deletion.

### Estimate

8-15 days for the initial catalog.

---

## WP-12 - Low-storage monitor and notifications

### Outcome

Users can opt into a small local warning before low disk space becomes critical.

### Scope

- Add an optional menu-bar status item.
- Monitor only selected local volumes.
- Configure warning thresholds by percentage or absolute free space.
- Use native local notifications with cooldown and hysteresis.
- Notification actions open MacStorageAtlas at the affected volume.
- Do not perform a full scan in the background.
- Do not monitor network or cloud locations by default.

### Acceptance criteria

- Monitoring is off by default.
- The threshold is not repeatedly triggered while space remains unchanged.
- Removable-volume disconnects are handled cleanly.
- Startup behavior is explicit and user-controlled.
- Monitoring has negligible idle CPU usage.

### Tests and verification

- Unit-test threshold, cooldown, and hysteresis calculations.
- Test mount/unmount and unknown-capacity states.
- Manual notification and menu-bar verification.

### Estimate

4-7 days.

---

## WP-13 - APFS and Time Machine snapshot insights

### Outcome

Users can understand storage held by local snapshots and optionally remove
specific snapshots safely.

### Scope

### Phase A: read-only insight

- Detect whether the scanned volume uses APFS.
- Enumerate local snapshots using documented macOS facilities.
- Show snapshot identifiers, dates, and sizes where reliably available.
- Explain purgeable space and why snapshot totals may not equal ordinary file
  totals.
- Link snapshots to the volume rather than inserting them into the file tree.

### Phase B: controlled management

- Implement deletion only if a supported API or stable system command provides
  precise targeting and reliable result reporting.
- Require explicit selection and confirmation.
- Never delete all snapshots through a vague single action.
- Capture and display command output/errors.

### Acceptance criteria

- Read-only insight works without granting unnecessary privileges.
- Unsupported or restricted systems show an explanation instead of zero.
- Snapshot deletion cannot target a different volume than the one displayed.
- The UI distinguishes estimated, purgeable, and immediately reclaimable space.

### Tests and verification

- Parser tests using captured command/API fixtures.
- Manual APFS volume and Time Machine tests.
- Failure tests for insufficient permissions and snapshots that disappear.

### Risks

- Snapshot-size reporting is nuanced and may be estimated.
- macOS commands and permission requirements may change.
- This feature needs updated compatibility tests for each major macOS release.

### Estimate

10-20 days.

---

## WP-14 - Direct cloud-storage scans

### Outcome

Users can inspect remote cloud storage without first downloading every file.

### Decision gate

Do not begin implementation until:

- User demand identifies the first provider.
- OAuth application ownership and privacy policy are decided.
- Provider API terms permit the intended use.
- The local storage analyzer is already accurate and trusted.

### Provider-neutral architecture

- `IStorageSource` abstraction for local and remote hierarchies.
- Capabilities explicitly report:
  - Logical size availability.
  - Allocated/local size availability.
  - Modification dates.
  - Hash/checksum availability.
  - Delete, move, copy, and preview support.
- OAuth tokens stored in macOS Keychain.
- Provider SDK/API code isolated from Core domain rules.
- Bounded paging, retries, exponential backoff, and cancellation.

### First-provider scope

- Connect and disconnect account.
- Select a remote root.
- Metadata-only scan.
- Tree, summary, largest-files, filter, and export integration.
- Open the item in the provider's web UI.
- No remote deletion in the first iteration.

### Acceptance criteria

- Scanning does not download file content.
- Revoking access removes local credentials.
- Expired tokens are refreshed or produce an actionable reconnect state.
- Rate limiting does not lose already collected partial results.
- Remote logical size is never presented as local disk usage.
- Logs contain no access tokens or user file names by default.

### Tests and verification

- Provider client tests with recorded/synthetic responses.
- Pagination, retry, cancellation, token-expiry, and malformed-data tests.
- Security review of Keychain usage and log redaction.

### Risks

- OAuth verification, API pricing, and provider policy changes.
- Cloud-native documents may not report normal byte sizes.
- Each provider significantly increases maintenance cost.

### Estimate

15-30 days per provider.

---

## Cross-cutting engineering requirements

## Safety model

- Prefer Trash over permanent deletion.
- Revalidate path and identity immediately before any filesystem mutation.
- Block known system-critical roots and broad unsafe selections.
- Detect parent/child overlap.
- Never imply that an item is safe to delete solely because it is large or old.
- Show per-item results for batch operations.
- Preserve recoverable state after cancellation or partial failure.

## Performance model

- Keep filesystem work off the UI thread.
- Throttle progress updates.
- Use bounded concurrency only after benchmarking.
- Stream exports and hashing.
- Avoid a second full copy of the scan tree.
- Define large-scale test fixtures for at least one million entries.

## Compatibility

- Keep Apple Silicon and Intel release builds until the project explicitly
  changes its support policy.
- Test the oldest supported macOS version and the current macOS version.
- Isolate macOS APIs behind interfaces so Core tests remain portable.
- Treat network and removable filesystems as slower and less reliable than the
  internal APFS volume.

## Accessibility and interaction

- Every pointer action needs a keyboard equivalent.
- Use native shortcuts where appropriate:
  - Space: Quick Look.
  - Command-I: details.
  - Command-R: rescan.
  - Command-F: focus filters/search.
- Provide accessible names for treemap items and icon-only buttons.
- Do not encode file type or status by color alone.

## Localization

Before M2, move user-visible strings into resources and provide English and
German resources. Dates, numbers, and file sizes must respect the current
locale. Additional languages can be added by contributors after the resource
structure is stable.

## Definition of done for every work package

A work package is complete only when:

1. Acceptance criteria are met.
2. Core logic is separated from App and Platform.Mac responsibilities.
3. Relevant unit and integration tests exist.
4. `dotnet build` succeeds.
5. `dotnet test` succeeds.
6. User-facing strings and error states are reviewed.
7. Documentation and screenshots are updated when behavior changes.
8. No unrelated files are included in the commit.
9. The completed commits are pushed to `codex/storage-feature-roadmap`.
10. The roadmap checkbox/status is updated in the same or a follow-up commit.

## Recommended first execution sequence

1. WP-00: correct the comparison documentation.
2. WP-01: establish signed/notarized release infrastructure.
3. WP-02 Phase A/B: benchmark and implement hardlink correctness.
4. WP-05: add export as a low-risk quick win.
5. WP-03: add metadata and Quick Look.
6. WP-04: add advanced filters.
7. WP-06: add Full Disk Access guidance.
8. WP-07 and WP-08: build the reviewed multi-item cleanup workflow.
9. Reassess user feedback before choosing WP-09, WP-10, or WP-11.
10. Start WP-12 through WP-14 only after the core workflow is stable.

## Status tracking

Update this table when work starts or finishes.

| ID | Status | Owner/branch | Notes |
| --- | --- | --- | --- |
| WP-00 | Complete | `codex/storage-feature-roadmap` | Comparison corrected and verified 2026-07-24; OpenSpec change: `correct-market-comparison` |
| WP-01 | Planned | `codex/storage-feature-roadmap` | Requires Apple Developer account |
| WP-02 | Planned | `codex/storage-feature-roadmap` | Begin with measurement spec and benchmark |
| WP-03 | Planned | `codex/storage-feature-roadmap` |  |
| WP-04 | Planned | `codex/storage-feature-roadmap` | Depends on metadata |
| WP-05 | Planned | `codex/storage-feature-roadmap` | Low-risk quick win |
| WP-06 | Planned | `codex/storage-feature-roadmap` |  |
| WP-07 | Planned | `codex/storage-feature-roadmap` | Safety review required |
| WP-08 | Planned | `codex/storage-feature-roadmap` | Depends on cleanup basket |
| WP-09 | Planned | `codex/storage-feature-roadmap` |  |
| WP-10 | Planned | `codex/storage-feature-roadmap` | Exact matches only |
| WP-11 | Planned | `codex/storage-feature-roadmap` | Maintain versioned rule catalog |
| WP-12 | Planned | `codex/storage-feature-roadmap` | Opt-in only |
| WP-13 | Planned | `codex/storage-feature-roadmap` | Read-only phase first |
| WP-14 | Deferred | `codex/storage-feature-roadmap` | Requires provider decision |
