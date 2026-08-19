## 1. Quick Look Platform Service

- [x] 1.1 Add a platform-neutral Quick Look service abstraction that previews one path and can be substituted in App tests.
- [x] 1.2 Implement the macOS Quick Look service in `MacStorageAtlas.Platform.Mac` using the system Quick Look entry point, including path validation and friendly failure normalization.
- [x] 1.3 Add platform service tests for missing paths and launch failure behavior that remain deterministic on non-macOS test hosts where applicable.

## 2. App Command and Selection Behavior

- [x] 2.1 Inject the Quick Look service through App composition and ViewModel constructors while preserving existing test and design-time defaults.
- [x] 2.2 Add a Quick Look command that is enabled only when a selected item exists and uses the current selection from tree, treemap, or largest-files views.
- [x] 2.3 Add ViewModel tests for command disabled state, successful preview from each result view, missing-path status, platform-failure status, and preservation of completed scan result state.
- [x] 2.4 Add or update the main toolbar/action surface to expose Quick Look consistently with Reveal in Finder and Move to Trash.

## 3. Keyboard Inspection Shortcuts

- [x] 3.1 Bind Space to Quick Look for the current selected item without triggering while text input is being edited.
- [x] 3.2 Bind Command-I to show the existing selected-item details surface while preserving the current selected item and scan-time metadata display.
- [x] 3.3 Add tests or focused UI verification for Space preview routing, text-editing exclusion, and Command-I details navigation.

## 4. Safety, Privacy, and Documentation

- [x] 4.1 Verify Quick Look does not change scan measurement basis, treemap data, largest-files data, file-type summaries, metadata snapshots, Reveal in Finder behavior, or Trash confirmation behavior.
- [x] 4.2 Review `README.md`, relevant `docs/` files, and `docs/index.html`; update user-visible feature, shortcut, limitation, and roadmap text where needed.
- [x] 4.3 Update WP-03 roadmap status after Quick Look and shortcut behavior is complete.

## 5. Validation

- [x] 5.1 Manually verify Quick Look on macOS with representative images, videos, PDFs, archives, folders, removed paths, and privacy-restricted or unavailable paths where practical.
- [x] 5.2 Run `dotnet build MacStorageAtlas.slnx --no-restore`.
- [x] 5.3 Run `dotnet test MacStorageAtlas.slnx --no-build`.
- [x] 5.4 Run `dotnet format MacStorageAtlas.slnx analyzers --diagnostics IDE0005 --verify-no-changes`.
- [x] 5.5 Run `openspec validate --all --strict --no-interactive`.
- [x] 5.6 Run `git diff --check`.
