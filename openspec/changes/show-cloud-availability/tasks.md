## 1. Spike

- [x] 1.1 List a never-opened OneDrive folder and a never-opened iCloud Drive folder with the scanner's enumeration while monitoring network and download activity, and record in `design.md` whether listing fetches data.
- [x] 1.2 Decide the handling of directories that carry `SF_DATALESS` from the spike result, and update the spec if they are skipped.

## 2. Core and Platform.Mac

- [x] 2.1 Add `CloudAvailability` to `DiskItemMetadata` and read `SF_DATALESS` through the existing `stat` interop in every measurement mode.
- [x] 2.2 Aggregate folder availability while the scan unwinds, including Partly local and Unknown, and keep the logical length of each cloud-only file from its `stat` call.
- [x] 2.3 Add Core and Platform.Mac tests for local, cloud-only, unknown, empty, mixed, and nested folders, using a fake metadata reader. Gate any test that needs a real placeholder to macOS and ignore it with a reason when none is available.
- [x] 2.4 Run the scan benchmark in logical mode, record the cost of the extra `stat`, and apply the fallback from `design.md` if it exceeds 10 percent.

## 3. Filtering

- [x] 3.1 Add the cloud availability criterion to `DiskItemFilter`, the evaluator, validation, and presets, with Any as the default for stored presets.
- [x] 3.2 Add Core tests for each criterion value combined with other criteria, and for presets without the field.

## 4. History and export

- [x] 4.1 Write the availability per item in snapshots and JSON exports as schema version 3, add the CSV column, and read versions 1 and 2 as Unknown.
- [x] 4.2 Add Core round-trip and compatibility tests.

## 5. App

- [x] 5.1 Show the cloud icon with tooltip and accessible name in the tree and Largest files, the Availability row with the folder's cloud-only size in Selected item, and the result summary with file counts and the labeled cloud-only size.
- [x] 5.2 Add the filter control to the Filters flyout.
- [x] 5.3 Add App ViewModel tests for the summary, the details row, and the filter.
- [ ] 5.4 Manually verify the status on an iCloud Drive and a OneDrive folder in light and dark appearance and with VoiceOver.

## 6. Documentation

- [x] 6.1 Update `docs/STORAGE_MEASUREMENT.md` with the availability semantics and the cloud-only size as a labeled second basis, and name that exception in `AGENTS.md` and `CLAUDE.md`.
- [x] 6.2 Update `README.md`, `docs/FEATURES.md`, and `docs/index.html`, and add the WP-14 status note in `docs/IMPLEMENTATION_ROADMAP.md`.

## 7. Validation

- [x] 7.1 Run `dotnet build MacStorageAtlas.slnx --no-restore`.
- [x] 7.2 Run `dotnet test MacStorageAtlas.slnx --no-build`.
- [x] 7.3 Run `dotnet format MacStorageAtlas.slnx analyzers --diagnostics IDE0005 --verify-no-changes`.
- [x] 7.4 Run `openspec validate --all --strict --no-interactive`.
- [x] 7.5 Run `git diff --check`.
