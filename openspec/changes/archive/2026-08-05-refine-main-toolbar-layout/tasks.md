## 1. Toolbar Layout

- [x] 1.1 Replace the stacked top toolbar structure in `MainWindow.axaml` with a single-row command surface at the default window size.
- [x] 1.2 Preserve scan, selected-item, cleanup basket, export, options, filters, and search command bindings while moving controls into the new layout.
- [x] 1.3 Add or retain themed visual separators between command groups and between the toolbar and the content/status area below it.
- [x] 1.4 Define compact or overflow behavior for controls that cannot fit at narrower supported widths without overlapping or unpredictable height growth.

## 2. Verification

- [x] 2.1 Add focused App test coverage where practical to verify toolbar command bindings and search behavior remain intact after the layout change.
- [x] 2.2 Perform visual verification at the default window size that the toolbar is one row high and separated from the content.
- [x] 2.3 Perform visual verification at the minimum supported width that toolbar controls remain reachable and do not overlap.
- [x] 2.4 Verify Options and Filters flyouts still open from the toolbar without clipping.

## 3. Documentation And Validation

- [x] 3.1 Review `README.md`, relevant files under `docs/`, and `docs/index.html`; update them only if the visible toolbar behavior or screenshots need documentation changes.
- [x] 3.2 Run `dotnet build MacStorageAtlas.slnx --no-restore`.
- [x] 3.3 Run `dotnet test MacStorageAtlas.slnx --no-build`.
- [x] 3.4 Run `dotnet format MacStorageAtlas.slnx analyzers --diagnostics IDE0005 --verify-no-changes`.
- [x] 3.5 Run `openspec validate --all --strict --no-interactive`.
- [x] 3.6 Run `git diff --check`.
