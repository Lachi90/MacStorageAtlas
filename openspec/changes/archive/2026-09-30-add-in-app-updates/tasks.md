## 1. Validate scope and prove native integration

- [x] 1.1 Validate the approved artifacts with `openspec validate --all --strict --no-interactive` before application-code changes and record WP-01 implementation status.
- [x] 1.2 Select and pin a verified Sparkle 2 distribution compatible with macOS 11.0, arm64, and x64; record acquisition, integrity verification, licensing, and helper-signing requirements in the design.
- [x] 1.3 Prove manual checking, standard updater UI, and cancellation through a minimal native bridge with the packaged native host and Avalonia lifecycle on arm64 and x64.
- [x] 1.4 Use isolated old/new signed test bundles and a test feed to prove authenticated replacement, installed-location relaunch, and a termination veto during active App work; record the exact restart hook and results before continuing.
- [x] 1.5 Resolve feed URLs, release-key configuration, and optional signed-feed support in the design, and re-estimate remaining WP-01 work from the spike evidence.

## 2. Establish distribution separation

- [x] 2.1 Add a safe-default build channel and pass Direct, AppStore, or Development consistently from packaging to every managed architecture publish; keep the existing appstore arguments unchanged.
- [x] 2.2 Make bridge compilation, native updater integration, framework embedding, production feed keys, and updater service initialization conditional on Direct releases; preserve the shared native host without unconditional updater linkage.
- [x] 2.3 Add packaging regression checks for arm64, x64, universal App Store, unsigned builds, and Direct-to-AppStore build sequences, proving both Store payloads exclude updater machinery and need no updater inputs or new entitlements.

## 3. Add direct-update controls and preferences

- [x] 3.1 Add the Platform.Mac bridge wrapper and App-owned service adapter with main-thread dispatch, retained callback lifetimes, unavailable-update behavior, and no new Core or Rendering dependencies; test initialization failures and distribution selection.
- [x] 3.2 Add the native application-menu update action and automatic-check preference to Direct builds only, using compiled bindings where applicable; test command availability and absence of update controls in Store builds.
- [x] 3.3 Expose persisted automatic-check consent through the updater's single preference store, retain manual checking, enforce the 24-hour minimum schedule, and disable silent installation and optional profiling; test opt-in, opt-out, and relaunch behavior.
- [x] 3.4 Verify newer, equal, older, prerelease, incompatible-OS, wrong-architecture, and Rosetta cases against isolated feed fixtures, including clear manual no-update outcomes.
- [x] 3.5 Verify manual error reporting and retry, quiet background failures, nonblocking startup, download cancellation, missing dependencies, and read-only launch behavior using deterministic fixtures and gated macOS tests.

## 4. Coordinate approved installation and restart

- [x] 4.1 Add an App restart gate for scan, duplicate analysis, export, cleanup, Trash, relocation, and required persistence; test each busy state and the idle case with NUnit/NSubstitute.
- [x] 4.2 Connect the native termination hook to final eligibility revalidation, deferral without automatic retry, and prevention of new conflicting work during final shutdown; test work starting between confirmation and termination.
- [x] 4.3 Retain the standard approved-installation flow, explain loss of in-memory results, finish settings persistence, and distinguish initial postponement from approved install-on-quit; verify automatic checks cannot enable unapproved replacement or restart.
- [x] 4.4 Exercise invalid/missing signatures, corrupted archives, unexpected app identity, and permission-denied replacement against isolated test apps, proving recovery and preservation of settings/history without touching real user files.

## 5. Extend local release packaging and metadata

- [x] 5.1 Embed the pinned framework and bridge into Direct bundles with preserved symlinks, permissions, and relocatable library paths; add inside-out helper signing and verify both release architectures.
- [x] 5.2 Add architecture-specific feed URLs and the public verification key to Direct bundle metadata, source installed versions from bundle metadata, and keep private key material in operator-managed local storage.
- [x] 5.3 Generate stable appcasts and archive signatures only after final DMG verification, keep existing checksums, and validate versioned asset URLs, minimum macOS versions, monotonic versions, and matching architectures with packaging checks.
- [x] 5.4 Make missing dependencies, bad configuration, failed verification, and unavailable update-signing keys fail release preparation without publishing usable entries; verify Store and unsigned commands remain independent of these inputs.
- [ ] 5.5 Run signed old-to-new update smoke tests on Apple Silicon and x64 under Rosetta, verifying signature checks, Gatekeeper acceptance, cancellation, active-work deferral, installation, relaunch, and persistence; document the unavailable physical Intel evidence before public release.
- [x] 5.6 Build and inspect the signed universal Store package with existing arguments, identities, profile, and entitlements; prove updater exclusion and sandbox startup on both architectures so no follow-up Store change remains.

## 6. Document behavior and validate delivery

- [x] 6.1 Update `README.md`, `docs/index.html`, and `docs/TROUBLESHOOTING.md` for direct updates, preferences, failures, read-only launches, scan-result closure, and one-time manual adoption; describe Store-managed updates accurately and announce support only when live.
- [x] 6.2 Update `docs/PACKAGING.md` for dependency acquisition, nested signing, local key backup/recovery, metadata generation, assets-before-feeds publication, defective-release withdrawal, and unchanged Store commands; review `docs/APP_STORE_REVIEW_NOTES.md` and record whether an update is needed.
- [x] 6.3 Update `docs/privacy.html` to disclose feed/download requests and ordinary GitHub connection metadata; verify fixture traffic and updater logs contain no scanned data, stable identifiers, or optional profiling.
- [x] 6.4 Update WP-01 scope, estimate, and status in `docs/IMPLEMENTATION_ROADMAP.md`, align `docs/OPENSPEC_WORKFLOW.md` with `add-in-app-updates`, and review relevant `docs/FEATURES.md` entries.
- [x] 6.5 Run `dotnet restore` if dependencies or SDK inputs changed, then `dotnet build MacStorageAtlas.slnx --no-restore`, `dotnet test MacStorageAtlas.slnx --no-build`, and `dotnet format MacStorageAtlas.slnx analyzers --diagnostics IDE0005 --verify-no-changes`.
- [x] 6.6 Run `shellcheck build-dmg.sh` when available, `openspec validate --all --strict --no-interactive`, and `git diff --check`; record direct-update and Store acceptance evidence before marking implementation complete.

## Acceptance status on 2026-09-30

28 of 29 tasks are complete. The implementation and available local acceptance are complete. Task 5.5 retains production release acceptance as an explicit open gate. The user requested archiving, committing, and pushing this implementation on 2026-09-30; archival does not approve public release or turn incomplete production checks into passing results. See [implementation evidence](spike-findings.md).

- 3.5: Manual error/retry, unavailable initialization, nonblocking startup, read-only launch, active download cancellation, signed malformed/tampered feeds, and quiet scheduled HTTP/parse failure during scanning were verified.
- 4.4: Invalid/missing archive signatures, corrupted archives, and unexpected app identity were rejected. Immutable-directory replacement denial preserved the signed old app and byte-identical settings/history. ARM reopening showed recovery guidance once, and a subsequent retry installed 1.0.1 successfully. The same actual x64 denial preserved its signed app and settings/history; after the Mac was kept unlocked, its recovery dialog and successful 1.0.1 retry/relaunch under Rosetta also passed. Seven native recovery regression tests passed on each architecture.
- 5.5: Signed arm64 and Rosetta x64 replacement/relaunch and active-work deferral passed. Physical Intel hardware is unavailable by user confirmation. Fixture DMG appcast signing and validation passed on both architectures. Final notarized DMGs, clean-machine Gatekeeper acceptance, and production-key/feed verification remain open. The configured production update-key account contains no key, and the example notarization profile is absent; these operator inputs are not created implicitly.
- 5.6: Signed universal Store packaging retains existing arguments, profile, entitlements, identities, and both payloads. A separate Developer-ID sandbox surrogate reached the main window on ARM and translated x64; both native menus exclude update controls. This verifies local Store-payload behavior, not Apple-Distribution authorization on this Mac. The production copy requires TestFlight/App Store acceptance or a suitable development provisioning setup.

No later Store feature adaptation is planned. No production key was initialized, and no feeds, release assets, or Store submissions were published. ShellCheck is unavailable. The [manual acceptance guide](../../../../docs/UPDATE_TESTING.md) retains the production release checklist and remaining environmental limits.
