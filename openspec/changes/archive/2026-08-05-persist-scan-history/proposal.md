## Why

MacStorageAtlas can tell a user what their storage looks like right now, but it
forgets everything the moment the app closes. A user who cleans up 40 GB has no
way to confirm the cleanup held, and a user whose disk is filling up has no way
to see which folders are responsible for the growth. Answering "what changed?"
requires a record of what things looked like before, and today no such record
exists.

This change delivers the persistence half of WP-09 (Scan history and
comparison) in `docs/IMPLEMENTATION_ROADMAP.md`. It establishes the stored
snapshot format, the local history store, and the retention and privacy
controls around it. Comparison, the feature that consumes these snapshots, is a
separate follow-up change. Splitting the work this way front-loads the decision
that is hardest to reverse: once users have snapshot files on disk, changing
their shape means migrating real data.

## What Changes

- Add a local scan-history store that keeps completed scans as versioned,
  gzip-compressed snapshots under the application support directory, separate
  from user settings.
- Capture a snapshot from a completed scan, recording every scanned item at the
  same fidelity the JSON export produces, plus the scan options, the
  measurement mode, the clone-accounting coverage, the recoverable errors, and
  a completeness verdict describing whether the scan could read everything it
  walked.
- Make history capture opt-in. MacStorageAtlas captures nothing until the user
  turns history on, and the user can turn it off at any time.
- Add retention controls by snapshot count and by total store size, pruning the
  oldest snapshots first when a limit is exceeded.
- Show the user the stored history for a scan root: when each snapshot was
  taken, how many items it covers, how large it is on disk, and whether the
  scan behind it was complete.
- Let the user delete an individual snapshot and clear the entire history
  without affecting scan settings, filter presets, or recent locations.
- Let the user reveal the store in Finder, so that removing stored scan data by
  hand is discoverable rather than requiring the user to know how to reach a
  location Finder hides by default.
- Keep the store working when it is changed or removed outside the application,
  because deleting stored scan data must never depend on the app running.
- Keep the store readable across app versions through an explicit schema
  version, recovering from a corrupt or unreadable snapshot by reporting it and
  leaving the rest of the history usable.
- Document what history stores, where it stores it, and how to remove it.

## Non-goals

- Comparing two snapshots, classifying items as added, removed, grown, or
  shrunk, and any growth or shrinkage reporting. That is the follow-up change.
- Move and rename detection. Snapshots match items by path only. Stable file
  identity is not plumbed onto `DiskItem` by this change, because it exists
  today only in shared-aware allocated mode and would make any identity-based
  behavior silently measurement-mode-dependent.
- Truncating or summarizing a snapshot to fit a size budget. A snapshot is
  either captured at full fidelity or not captured at all, so that a later
  comparison can never mistake an omitted item for a deleted one.
- Importing a snapshot as a browsable scan result, or exporting one through the
  save-file picker. The existing export capability already covers user-directed
  export.
- Automatic or scheduled background scans.

## Capabilities

### New Capabilities

- `scan-history`: how MacStorageAtlas records a completed scan as a local
  snapshot, what a snapshot states about the scan that produced it, when
  capture happens and when it is refused, how the store is retained and pruned,
  how the user inspects and removes history, how the store survives corruption
  and schema change, and how stored scan data stays private and local.

### Modified Capabilities

No existing capability changes its requirements. `result-export` keeps its
user-directed export contract and its own schema version; scan history reuses
its row shape at the code level but versions its stored format independently.
`scan-access-guidance` and `storage-measurement` are read by history capture
without changing what they require.

## Impact

- `src/MacStorageAtlas.Core`: new snapshot model, snapshot writer and reader,
  store abstraction, retention policy, and completeness classification. No new
  package dependency; compression uses `System.IO.Compression` from the base
  class library.
- `src/MacStorageAtlas.App`: a history service implementing the store against
  the filesystem, history settings on `AppSettings`, capture wiring at scan
  completion in `MainWindowViewModel`, and history list and management UI in
  `MainWindow.axaml`.
- `tests/MacStorageAtlas.Core.Tests` and `tests/MacStorageAtlas.App.Tests`:
  round-trip, schema-version, retention, corruption, refusal, and
  opt-in-and-clear coverage.
- Documentation: `README.md`, `docs/FEATURES.md`, `docs/index.html`, and the
  WP-09 status row in `docs/IMPLEMENTATION_ROADMAP.md`.
- Privacy posture: this is the first feature that persists scanned paths
  without the user choosing a destination for each write, which is why capture
  is opt-in and the store is discoverable and clearable.

## Dependencies

- WP-03 (Quick Look and file metadata), complete, supplies the item metadata a
  snapshot records.
- WP-05 (CSV and JSON export), complete, supplies the row shape, the streaming
  write pattern, and the publish-only-when-whole write discipline this change
  reuses.
- WP-06 (Full Disk Access assistant), complete, supplies the access
  classification a snapshot's completeness verdict is derived from.

## Risks

- **Store size.** Full-fidelity snapshots of a large home directory are the
  largest artifact the app has ever written. Retention by total size, an opt-in
  gate, and refusing rather than truncating an oversized capture keep this
  bounded and honest.
- **Privacy.** A stored history is a durable index of every filename in the
  user's home directory. Opt-in capture, restrictive file permissions, a
  discoverable location, and one-action clearing are the mitigations.
- **Format lock-in.** Snapshots on disk outlive the code that wrote them. An
  explicit schema version and a documented refusal path for unreadable
  versions keep a future format change from silently misreading old data.
- **Capture cost.** Writing a snapshot after every completed scan adds time to
  a workflow users experience as finished. Capture runs off the UI thread, is
  cancellable, and reports failure without invalidating the displayed result.

## Estimate

WP-09 is estimated at 7-12 days for history and comparison together. This
change covers the persistence half: 4-6 days.
