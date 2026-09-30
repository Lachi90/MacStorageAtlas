# Updater investigation and implementation evidence

## Evidence

Date: 2026-09-30. The initial dependency and source investigation preceded application-code changes. The runtime and delivery evidence below records the subsequent implementation.

Sparkle 2.9.6's official binary archive matches its published SHA-256 digest, passes deep strict signature verification, includes both supported architectures, and declares a macOS 10.13 minimum. Version 2.10.0 requires macOS 12.0, so the compatible binary dependency is pinned to 2.9.6 in the design.

The investigation read the pinned `SPUUserDriver.h`, `SPUUpdaterDelegate.h`, `SPUStandardUserDriver.m`, and `SPUUIBasedUpdateDriver.m`. These show:

- A prepared installation can run when the application terminates normally.
- Dismissing a prepared installation does not cancel that behavior.
- Returning `SPUUserUpdateChoiceSkip` at the ready-to-install stage cancels the prepared installation without permanently skipping the release at that stage.
- The relaunch-postponement callback does not cover every termination path.

These initial findings came from static source inspection. Runtime verification subsequently exercised manual UI, signed replacement, relaunch, busy-operation gating, and signed Store packaging as recorded below.

## Approved outcome

On 2026-09-30 the user approved following the standard workflow used by other Mac apps. Proposal, design, requirements, and tasks now distinguish initial Remind Me Later from an approved prepared installation that can complete on ordinary quit. The custom user-driver proposal is not selected. App still protects active work from updater-triggered restart and requires an explicit retry after deferral.

## Reference apps and clarification

Source inspection on 2026-09-30 confirms that Rectangle initializes `SPUStandardUpdaterController` and uses standard-user-driver delegates to make scheduled notifications less intrusive. Transmission also uses `SPUStandardUpdaterController`, disables automatic updates with `SUAllowsAutomaticUpdates = false`, and configures a 24-hour check interval.

Inspection of Sparkle 2.9.6's `SUUpdateAlert.m` clarifies an important distinction: before download, the action is Remind Me Later; once installation is prepared, the corresponding action is explicitly labelled Install on Quit. The initial findings describe internal protocol semantics and must not be read as evidence that Sparkle silently treats the initial Remind Me Later action as installation approval.

The standard workflow is therefore a practical reference for this app. The plan allows completion on ordinary quit after installation has already been approved. The isolated runtime tests below provide evidence for this app's Avalonia lifecycle and active-operation protection; the reference apps alone do not establish that evidence.

Approved approach after this comparison: retain the standard updater UI and clarify the specification around initial postponement, approved preparation, and explicitly labelled installation on quit. Do not add a custom user driver solely because of the internal Dismiss semantics. No custom user driver is planned.

- [Rectangle updater initialization](https://github.com/rxhanson/Rectangle/blob/main/Rectangle/AppDelegate.swift).
- [Transmission updater integration](https://github.com/transmission/transmission/blob/main/macosx/Controller.mm).
- [Transmission update policy](https://github.com/transmission/transmission/blob/main/macosx/Info.plist).
- [Sparkle's stage-dependent button labels](https://github.com/sparkle-project/Sparkle/blob/2.9.6/Sparkle/SUUpdateAlert.m).

## Implemented behavior

Direct builds compose `SPUStandardUpdaterController` through a retained Objective-C bridge, Platform.Mac wrapper, and App service. Development is the default channel. AppStore and Development compile out the concrete updater service and native wrapper; their packaging does not acquire, embed, sign, or initialize Sparkle.

The native menu offers manual checking and an optional automatic-check preference. Sparkle owns the persisted preference. Scheduling starts disabled, uses a minimum 24-hour interval, and disables automatic installation and optional profiling. Production metadata requires signed appcasts and pre-extraction archive verification. Production archive selection additionally requires a stable version, HTTPS, and the exact runtime-specific DMG name.

The restart delegate is `updater:shouldPostponeRelaunchForUpdate:untilInvokingBlock:`. App returns busy, ready, or approved persistence in progress. Busy work retains the installation handler without retrying when the app becomes idle. The user explicitly retries through Finish Installing Update. An already-approved idle transition saves a settings snapshot off the UI thread, then resumes its retained handler. Avalonia's `ShutdownRequested` rechecks eligibility. Settings use an atomic replacement so failed writes preserve the existing file. History operations and pending scan finalization participate in the gate.

## Isolated signed runtime tests

Tests ran on Apple Silicon with macOS 27.0.1 using the actual native host, self-contained .NET payloads, Avalonia, Sparkle 2.9.6, and separate test bundle identifiers. x64 tests ran under Rosetta; a process sample confirmed `X86-64 (translated)`. This is preliminary x64 evidence, not a physical Intel test.

Only fixtures under `/tmp/msa-runtime-spike` and `/tmp/msa-runtime-spike-x64` were scanned or updated. Test composition redirected settings and history into the fixture directory. A gated fixture scanner exercised the actual App scan pipeline while waiting for an explicit fixture marker to be released. Test feeds used loopback HTTP and temporary file-based Ed25519 keys. The production bridge does not enable those test URL exceptions. Neither production signing keys nor the user's installed application were replaced.

Bundles and nested Sparkle helpers were signed with the available Developer ID identity and passed `codesign --verify --deep --strict`. Isolated signing used no timestamp and update archives were ZIP files, so this evidence does not establish acceptance of the final notarized release DMGs.

| Scenario | Observed result |
| --- | --- |
| arm64 1.0.0 → 1.0.1 | Standard manual UI, approved download, authenticated replacement, and relaunch from the original installed path succeeded. |
| arm64 1.0.2 → 1.0.3 during a scan | Restart was deferred; completing the scan did not install automatically. Explicit Finish Installing Update completed installation and relaunch. |
| x64 1.0.0 → 1.0.1 under Rosetta | Standard checking, download, replacement, and installed-location relaunch succeeded. |
| x64 1.0.2 → 1.0.3 under Rosetta | Signed feed and pre-extraction verification were enabled. Active scanning deferred restart; the old process and version remained after the scan completed and the app became idle. Explicit retry performed asynchronous settings persistence and relaunched 1.0.3. |
| Settings/history during x64 replacement | Fixture user-data digest remained `6b2d2529283ead4ca18e6e05b9f0d92b9d185631548b7554256e3050432bad85`; the replacement bundle passed deep strict signature verification. |
| Equal, older, and beta-channel versions | No eligible update was offered, with the standard manual no-update UI. |
| Minimum macOS 99.0 | Standard incompatible-system outcome appeared; no replacement occurred. |
| arm64-only entry selected under Rosetta | Sparkle initially exposed the physical CPU behavior. The additional bridge guard now rejects it before download for an installed x64 payload. |
| Invalid or missing archive Ed25519 signature | Download authentication failed; the installed application and user data remained intact. |
| Authenticated corrupted ZIP | Extraction failed without replacing the installed application. |
| Authenticated, signed bundle with unexpected identifier | Application identity verification rejected replacement. |
| Manual error recovery | Cancelling each tested failed update allowed another manual check. |
| Automatic-check preference | Enabling persisted `SUEnableAutomaticChecks = 1` across relaunch; disabling persisted `0`; manual checking remained available. |
| Initial offer dismissal | The standard updater offer could be closed without approving installation. |
| Read-only DMG launch | The app started from the mounted test DMG. Manual checking reported that it must first be copied to Applications and relaunched; no replacement occurred. |

Fixture HTTP logs showed only appcast/archive GET requests with no scan paths or query identifiers. The bridge supplies no system-profile keys and metadata disables profiling. This observation does not claim a complete capture of all production request headers.

## Store and automated verification

The unchanged `./build-dmg.sh appstore both` interface built signed universal packages 1.0.901 and 1.0.902 using the existing Apple Distribution identity, installer identity, provisioning profile, and entitlements. A Direct architecture publish preceded the Store run. Both managed payloads, nested bundle metadata, native dependency lists, and filesystem contents passed updater-exclusion inspection. The universal launcher contains arm64 and x86_64 slices; the nested Intel launcher contains x86_64. Deep strict bundle verification and trusted installer-signature verification passed. No updater inputs or new entitlements were needed.

Final solution validation passed in Development, Direct, and AppStore channels: 821 tests passed and one existing platform integration test was skipped per channel. The Direct tests also exercise unavailable native initialization. Five metadata tests and the packaging regression suite passed. Both production bridge architectures compiled. Unused-import analyzer verification and whitespace checks passed. ShellCheck is unavailable on this host.

README, landing page, feature list, troubleshooting, packaging, privacy, Store review notes, roadmap, and workflow were reviewed and updated. Documentation describes first-release adoption and does not announce live feeds prematurely. Storage screenshots require no replacement for these menu and status changes.

## Follow-up acceptance on 2026-09-30

The user confirmed that no physical Intel Mac is available and requested all other tests plus manual instructions. The available x64 acceptance uses Rosetta; physical Intel remains an explicitly untested hardware configuration rather than a claimed result.

`tests/updater-fixtures.py` now creates reproducible, isolated signed 1.0.0/1.0.1 bundles, per-architecture loopback feeds, temporary file-based keys, a gated scanner, and separate settings/history. `reset` uses `ditto` to preserve code-signature extended attributes and rejects a running test app. The initial Python copy lost signatures on some managed files; it was corrected, deep verification was added, and the download cancellation test was rerun against the corrected bundle. Six fixture-safety tests pass, including rejection of a symlinked installation directory.

Additional observed results:

- ARM active download cancellation stopped the loopback transfer after 2,097,152 bytes of a 44,409,704-byte ZIP. The application remained on 1.0.0 and passed deep strict signature verification afterward. A subsequent manual check succeeded.
- A feed modified after signing was rejected with the standard improperly-signed-feed error. A correctly signed malformed feed produced the standard parsing error. Cancelling each error restored the current scan view.
- A genuinely due scheduled check returned HTTP 503 while an App scan was active, without a dialog. The scan completed after its gate marker was released. The correctly signed malformed feed also failed quietly during a genuinely due scheduled check, and its scan completed. Fixture defaults moved SULastCheckTime from 2000 to the current time; the production 24-hour interval was not changed.
- A normal signed 1.0.0 to 1.0.1 update succeeded even with the owned installation parent set to mode 0555. Sparkle can handle this owner-permission situation. It is not evidence of a denied replacement. The stronger fixture now combines mode 0555 with the user immutable directory flag; its interactive installation failure was exercised in the final acceptance below.
- Recording was enabled for a fixture scan, but a later audit found that its history directory was still empty. Final preservation acceptance therefore used an explicitly seeded, readable Core history snapshot created from the isolated scan fixture; it does not claim that the earlier UI scan recorded a snapshot.
- Sparkle generated and signed DMG appcasts for both arm64 and x64 fixture bundles with the exact versioned HTTPS DMG naming convention. Appcast authentication and `update-metadata.py` checks for runtime, minimum macOS, version, and immutable URL all passed. These are locally signed fixture DMGs, not notarized production DMGs.
- The current signed universal production Store package 1.0.903 built with unchanged identities, profile, arguments, and entitlements, and passed updater exclusion and signatures. A separately rebuilt isolated Apple-Distribution copy failed local launch with AMFI code -413, No matching profile found; taskgated reported that the production profile cannot be installed as a development profile. Its profile expires in 2027, so expiration was not the cause. No security policy was bypassed.
- A separate Developer-ID-signed sandbox copy retains the Store payloads and the existing sandbox/JIT/file-selection rights, uses a dedicated test identifier, and removes production provisioning bindings only from that isolated copy. The production package is unchanged. Process samples establish startup in Avalonia's AppKit run loop; the samples identify native ARM and translated x64 separately. This does not establish successful local Apple-Distribution authorization or visual menu acceptance.

The Mac locked again before the stronger denial and Store menu checks. An asynchronous unlock request was made; these UI-dependent checks are not marked complete. Both fixture servers and prepared bundles are suitable for the [manual acceptance guide](../../../../docs/UPDATE_TESTING.md). The guide also distinguishes local sandbox payload testing from the final TestFlight/App Store distribution test.

The default build and all 821 passing tests remain green, with one existing integration skip. Metadata tests, packaging regression tests, fixture-safety tests, analyzer verification, and strict OpenSpec validation are included in the follow-up checks. README and packaging documentation now link the manual guide. The landing page and screenshots need no additional change for this developer testing workflow.

## Final local acceptance and archive on 2026-09-30

The user requested the remaining tests, a new branch, archival, commit, and push. The final negative installation test exposed a recovery gap: a permission failure during final replacement occurs after the old app terminates, so an App-side Sparkle callback can no longer show it. The Direct bridge now persists only the approved target version immediately before final installation. On the next launch it consumes that marker and reports recovery once if the installed bundle is still older. Successful replacement and newer manual installation clear it silently. Sparkle's pinned installation delegate calls were inspected against the official source; ordinary approved install-on-quit uses the same installation hook.

Seven native regression cases passed on ARM and Rosetta x64: fresh install, failed replacement, one-time reporting, successful replacement, newer manual replacement, numeric version ordering, and absent bundle version. The durable runner is `tests/updater-bridge-tests.sh`.

An isolated Core console scan captured a readable `update-acceptance.msascan.gz` snapshot (501 bytes) from the owned ARM scan fixture. Its SHA-256 is `2c9d42852d6a233f394b3a01232f58b58be080d5a4cbd188023b3f9c254c3c67`; the ARM settings SHA-256 is `ad7fcfba39e66fd7f8c4b1fd052d03c9fb7bfb88786a79478bc209a023097ea5`. The x64 test uses a copy of this valid fixture snapshot and its own settings SHA-256 `8c5dd300508e8b44f16242d6c83e3f3ef1cab05f2ce853f6a17d4b1736821e68`. These are fixture metadata and do not describe any real user scan.

Observed final results:

- ARM: set mode 0555 and `uchg` only after preparation; actual final replacement failed with Operation not permitted. The old 1.0.0 bundle passed deep strict verification, and settings/history were byte-identical. Opening it displayed The update installation did not complete and writable/unlocked-folder/retry guidance. After removing only the fixture lock, checking again and installing succeeded; version 1.0.1 relaunched from the same installed location, passed deep strict verification, and preserved settings/history. The recovery marker was consumed.
- Rosetta x64: the same actual final replacement failure retained the signed 1.0.0 bundle and byte-identical settings/history. The approved-version marker persisted as 1.0.1. The Mac locked before visual recovery/retry; those additional x64 UI observations are not claimed as passing. Earlier signed x64 replacement/relaunch and busy deferral remain proven, and the final recovery logic passes the native x64 regression suite.
- Store sandbox surrogate: both ARM and x64 reached a visibly inspected main window, and neither native application menu contains Check for Updates or Automatically Check for Updates. Process samples `/tmp/atlas-final-store-arm.sample` and `/tmp/atlas-final-store-x64.sample` identify ARM64 and X86-64 (translated), respectively. Deep signatures, existing sandbox rights, and updater exclusion remain verified. This does not make the separately rejected Apple-Distribution profile locally authorized.
- A macOS authorization prompt during an earlier negative preparation attempt could not be controlled: the computer-use tool rejects access to SecurityAgent. The owned waiting test process was terminated, and the final replacement test instead locked the directory after preparation. No security policy was bypassed and no password was entered by the agent.
- Final build, 821 passing .NET tests with one existing platform skip, analyzer verification, five metadata tests, six fixture-safety tests, packaging regressions, and seven native tests per architecture passed. ShellCheck is unavailable. Strict OpenSpec and whitespace checks are repeated around archival.

The implementation is archived at the user's explicit request with 28/29 tasks complete and one open production release gate (5.5). Archive status does not assert production release readiness. The current branch is `codex/add-in-app-updates`.

## Remaining release acceptance

- Physical Intel hardware is unavailable; Rosetta is the available x64 test environment.
- The read-only `generate_keys --account de.ltsoftware.macstorageatlas -p` probe reported No existing signing key found. The example notarization profile is absent. The operator must initialize and back up release inputs before final Direct packaging.
- Final notarized production DMGs, clean-machine Gatekeeper acceptance, actual production-key/feed authentication, and assets-before-feeds publication remain open. No production keys or public feeds were created and nothing was released.
- The actual Apple-Distribution Store copy needs TestFlight/App Store acceptance or a suitable local development profile. Local surrogate GUI/menu acceptance passed for both Store payloads; no later Store feature adaptation is planned.
- Visual x64 recovery/retry is still available in the [manual guide](../../../../docs/UPDATE_TESTING.md) if the Mac remains locked through delivery.

README, feature documentation, packaging, troubleshooting, manual acceptance, roadmap, and OpenSpec were reviewed for the final recovery notice. The landing page already describes the prepared future update channel; its prose and screenshots require no further change for this recovery detail.

## Primary sources

- [Sparkle 2.9.6 release and security fixes](https://github.com/sparkle-project/Sparkle/releases/tag/2.9.6).
- [Sparkle 2.10.0 deployment-target change](https://github.com/sparkle-project/Sparkle/releases/tag/2.10.0).
- [Pinned user-driver protocol](https://github.com/sparkle-project/Sparkle/blob/2.9.6/Sparkle/SPUUserDriver.h).
- [Pinned standard user-driver implementation](https://github.com/sparkle-project/Sparkle/blob/2.9.6/Sparkle/SPUStandardUserDriver.m).
- [User-driver protocol reference](https://sparkle-project.org/documentation/api-reference/Protocols/SPUUserDriver.html).

- [Pinned installation callback order](https://github.com/sparkle-project/Sparkle/blob/2.9.6/Sparkle/SPUCoreBasedUpdateDriver.m).
