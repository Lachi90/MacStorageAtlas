# Tasks

## 1. Core Equivalent-Path Contract

- [x] 1.1 Run `openspec validate --all --strict --no-interactive` before implementation and resolve any artifact errors without changing approved scope; verified by a clean validation run.
- [x] 1.2 Add the `EquivalentPath` record and `IEquivalentPathProvider` abstraction in `MacStorageAtlas.Core.Scanning` without macOS-specific types; verified by a successful Core build.
- [x] 1.3 Add optional provider injection to the `DiskScanner` constructors while keeping the provider-less behavior unchanged; verified by the existing Core scanner tests passing unmodified.
- [x] 1.4 Add `OmittedEquivalentPaths` to `ScanProgress` with an empty default; verified by a unit test that a scan without a provider reports an empty list.

## 2. Scope Selection and Traversal Omission

- [x] 2.1 Select provider pairs whose visible and alternate paths are both equal to or below the normalized scan root using the platform path comparer; verified by unit tests for roots `/`, `/System`, `/Users`, a home folder, and `/System/Volumes/Data` with a fake provider.
- [x] 2.2 Skip directory entries whose full path is an alternate in the omission set before measuring, adding, or enumerating them, and record each skipped pair; verified by temporary-directory tests where the alternate subtree contributes no items, bytes, or errors.
- [x] 2.3 Verify attribution to the visible path regardless of enumeration order; verified by a test with an injected enumerator that returns the alternate before the visible path in shared-aware mode, asserting counted bytes under the visible path and an unchanged counted root total.
- [x] 2.4 Verify logical and per-path allocated modes count equivalent content once; verified by unit tests asserting root totals equal the visible subtree plus non-equivalent content.
- [x] 2.5 Verify non-equivalent siblings of an alternate remain traversed and aggregated; verified by a test with an extra directory beside the alternate inside the simulated Data-volume root.
- [x] 2.6 Verify cancellation after an omission yields no completion, no bytes from the alternate, and a partial `OmittedEquivalentPaths` list containing only encountered omissions; verified by a cancellation unit test.
- [x] 2.7 Verify an empty provider result, a throwing provider, and a provider returning pairs outside the scope leave traversal unchanged and add no scan errors; verified by unit tests.

## 3. macOS Firmlink Provider

- [x] 3.1 Implement firmlink definition parsing in Platform.Mac that maps tab-separated entries to `/System/Volumes/Data/<relative path>` and ignores blank, malformed, relative, or duplicate lines; verified by parsing unit tests with in-memory definitions.
- [x] 3.2 Return a pair only when both paths are directories with equal device and inode using the existing Apple Silicon and Intel `stat` entry points; verified by unit tests with an injected identity reader covering equal, different, missing, and failing identities.
- [x] 3.3 Return an empty list without throwing when the definition file is missing or unreadable; verified by a unit test with an injected file reader.
- [x] 3.4 Add a macOS-gated integration test confirming that `/Users` is returned as equivalent to `/System/Volumes/Data/Users` on the test host, ignored with a clear reason on unsupported platforms; verified by the test run output.

## 4. App Wiring and Presentation

- [x] 4.1 Wire the macOS provider into `App.axaml.cs` and the default `MainWindowViewModel` constructors without adding Platform.Mac or UI dependencies to Core; verified by a successful solution build.
- [x] 4.2 Expose the omitted equivalent paths of the current result from `MainWindowViewModel` and clear them when a new scan starts; verified by ViewModel tests for completed, cancelled, and new scans.
- [x] 4.3 Show a result-summary note naming each omitted path and where it is shown, hidden when nothing was omitted, using existing styles and resources; verified by ViewModel tests and a real-system `/` scan through the production scanner and firmlink provider that reported 18 omitted alternates, without a separate interactive check in the running app.

## 5. Documentation and WP-02 Tracking

- [x] 5.1 Update `docs/STORAGE_MEASUREMENT.md` with firmlink-equivalent omission, its verification and fallback, and the effect on `/` totals; verified by review against the delta specification.
- [x] 5.2 Review `README.md`, `docs/FEATURES.md`, and `docs/index.html` and update them where whole-disk scan behavior or limitations are described, or record that no change was needed; verified by the review notes in the change summary.
- [x] 5.3 Add this change to the WP-02 notes in `docs/IMPLEMENTATION_ROADMAP.md`; verified by the updated roadmap section.
- [x] 5.4 Reconcile the final implementation with this change's proposal, design, delta specification, and completed task checkboxes; verified by a final artifact review.

## 6. Validation

- [x] 6.1 Run `dotnet build MacStorageAtlas.slnx --no-restore` with no errors.
- [x] 6.2 Run `dotnet test MacStorageAtlas.slnx --no-build` with all tests passing.
- [x] 6.3 Run `dotnet format MacStorageAtlas.slnx analyzers --diagnostics IDE0005 --verify-no-changes` with no changes reported.
- [x] 6.4 Run `openspec validate --all --strict --no-interactive` with no errors.
- [x] 6.5 Run `git diff --check` and confirm no generated output or unrelated working-tree changes were introduced.
