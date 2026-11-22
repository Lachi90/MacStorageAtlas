## Why

After a scan that read every location without a single permission error, Atlas
Storage Analyzer can still show a banner titled "Access status is unclear". It
appears whenever the Full Disk Access probe finds fewer than two readable
protected folders, for example on a Mac where Mail or Messages were never used.
The banner then warns about missing access although nothing was blocked. It
reads like a generic disclaimer and erodes trust in the guidance that matters.

## What Changes

- Show access guidance only when the scan actually hit permission-related
  inaccessible paths. A scan without such paths shows no access guidance,
  whatever the Full Disk Access probe concluded.
- Remove the "Access status is unclear" guidance state.
- Make the Full Disk Access probe conclusive on every Mac by also probing
  `/Library/Application Support/com.apple.TCC`. That system directory exists on
  every macOS installation and can only be listed with Full Disk Access.
- A scan without blocked paths is recorded in scan history as complete.

Non-goals:

- Do not change guidance for scans with inaccessible paths, the sandboxed
  build's guidance, or the Open Settings and Rescan actions.
- Do not read any file contents. The probe only lists directory entries, as
  before.
- Do not change the stored `ScanCompleteness` values. `Undetermined` remains
  readable for existing snapshots.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `scan-access-guidance`: Guidance appears only for scans with
  permission-related inaccessible paths.

## Impact

Affected areas:

- `src/MacStorageAtlas.App/ViewModels`: `AccessGuidanceClassifier`,
  `AccessGuidanceStatus`, `ScanCompletenessClassifier`, and the guidance texts
  in `MainWindowViewModel`.
- `src/MacStorageAtlas.Platform.Mac/MacFullDiskAccessService.cs`: An
  additional probe path.
- Tests in App and Platform.Mac.

Dependencies:

- Builds on the archived `guide-full-disk-access` behavior.

Risks:

- A protected folder that macOS hides silently, rather than refusing with a
  permission error, would no longer produce a banner. macOS reports TCC
  refusals as "Operation not permitted", which the scan already records as an
  inaccessible path, so this case is not known to occur.

Roadmap reference:

- WP-06 (Full Disk Access assistant).

Estimate:

- Less than 1 day.
