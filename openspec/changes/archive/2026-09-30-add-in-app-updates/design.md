## Context

WP-01 already supplies locally signed, notarized Developer ID DMGs and a separate Mac App Store package. Direct releases contain one self-contained .NET payload per architecture; the universal Store bundle contains both payloads and starts them through the existing native host. `build-dmg.sh` produces Info.plist bundle versions independently of managed assembly version metadata.

There is no current updater or application update menu. App owns settings and operation state; Platform.Mac already provides native integrations. The user explicitly requires that adding the updater also completes the Store build separation, with no later Store-specific adaptation.

## Goals / Non-Goals

**Goals:**

- Provide stable-release checks and user-approved download, installation, and restart for Developer ID releases.
- Keep both architectures and the current macOS 11.0 deployment target.
- Preserve user control, local scan privacy, and recoverability before replacement.
- Produce Store-ready packages with the existing command and signing inputs, with updater exclusions applied automatically to both managed payloads and native packaging.

**Non-Goals:**

- Silent installation, automatic restart, beta channels, delta updates, mandatory updates, telemetry, or a custom update dialog.
- Changes to Store update behavior, entitlements, bundle identifiers, sandbox access, or universal packaging structure.
- Hosted release signing or publishing, cross-channel migration, and automatic scan-session restoration.

## Decisions

### 1. Use Sparkle 2 behind a narrow native bridge

Use Sparkle's standard updater UI and installation machinery. A small Objective-C bridge under `packaging` exposes a stable C ABI; Platform.Mac wraps it, and App supplies the service adapter, commands, and operation coordination. Native initialization and callbacks run on the main thread after Avalonia initialization. Retain the native controller and managed callback delegates for their required lifetimes.

Sparkle handles feed parsing, bundle-version comparison, download validation, and replacement. Do not implement a second parser, installer, or scheduler in C#. Use the installed bundle's `CFBundleVersion` as the current version, rather than the managed assembly version.

Alternatives considered:

- A GitHub Releases check opening a browser is smaller but does not fulfill the requested in-app installation flow.
- A custom .NET replacement helper would duplicate signature, permission, and restart handling.
- Direct Objective-C messaging throughout C# removes a native source file but makes delegate lifetimes and restart callbacks harder to audit.

### 2. Prove native integration before committing to an API or release version

The first implementation task is a bounded spike using the actual native host and Avalonia app lifecycle. Select and pin a maintained Sparkle 2 release compatible with macOS 11.0 and both architectures. Record framework acquisition, integrity verification, licensing, helper signing, and the exact delegate/API used to defer termination.

The spike must demonstrate manual checking, native UI, cancellation, successful signed bundle replacement and relaunch, and a restart veto while an App operation is active. Use isolated older/newer bundles and a test feed, never the developer's installed app. Test arm64 and x64. The user confirmed on 2026-09-30 that no physical Intel Mac is available; use Rosetta for the available x64 acceptance and explicitly retain the physical-hardware evidence gap without claiming Intel hardware was tested. If the integration cannot meet these requirements, revise this design before continuing; do not silently substitute a browser-only updater.

### 3. Select distribution at build time

Introduce a build input such as `DistributionChannel`, with `Development` as the safe default. The packaging script supplies `Direct` for Developer ID release publishes and `AppStore` for Store publishes. Pass the input consistently to both managed architecture payloads. Conditional compilation and build inclusion exclude native updater integration from Store and development builds. The Store composition root compiles without updater-service initialization or menu construction.

Embedding of Sparkle, bridge compilation/copying, updater plist keys, update-signing inputs, and update-metadata generation are conditional on release mode. The existing native host continues to serve all distribution modes; it must not acquire an unconditional Sparkle link dependency.

The existing `appstore` command, identities, and provisioning-profile arguments remain sufficient. It does not require a Sparkle download, update-signing key, feed URL, new entitlement, or manual cleanup. The universal Store bundle and every nested payload are checked for updater frameworks, bridge binaries, native references, and update configuration. No update actions or automatic-check preferences appear in the Store UI. The existing Store listing remains the place users obtain Store updates.

Runtime sandbox detection alone was rejected: it could disable execution while still leaving disallowed updater machinery in the submitted package. Build-time exclusion provides an inspectable distribution boundary.

### 4. Coordinate the standard installation flow with App operation state

Retain `SPUStandardUpdaterController`, as used by Rectangle and Transmission. Remind Me Later before download does not approve installation. Choosing Install Update authorizes downloading and preparing that installation; once prepared, Sparkle can finish it on ordinary quit. When offered at that stage, the postponement action is explicitly labelled Install on Quit. No custom user driver or extra installation confirmation is needed.

Expose an App-owned update service implemented by an App adapter calling Platform.Mac. Platform.Mac must not reference App or Avalonia. Core and Rendering need no new updater dependencies.

The bridge uses the standard updater delegate's relaunch-postponement hook to consult App. App checks scans, duplicate analysis, exports, cleanup, Trash, relocation, history capture, history operations, and pending persistence. Busy work defers an updater-triggered restart; the retained installation handler is invoked only after a user explicitly retries via the update menu. Becoming idle must not invoke it automatically.

An approved idle restart reserves shutdown, saves a settings snapshot off the UI thread, and prevents conflicting new work. A third bridge-gate outcome retains the installation continuation while that approved persistence finishes; success resumes that same approved transition without a second confirmation. Active-work deferral and persistence failure still require explicit user retry. Recheck the gate on Avalonia's shutdown request. On persistence failure leave the app running with no shutdown reservation. The native notice explains that current in-memory scan results close; this notice does not introduce a second installation-approval dialog. Disable automatic downloading/installing with `SUAllowsAutomaticUpdates = false` while preserving optional automatic checks.

Ordinary user-requested termination after an approved preparation is allowed to complete the installation. This is the familiar Sparkle workflow, not an unapproved background installation. Tests distinguish initial Remind Me Later, an approved prepared installation, and deferred updater-triggered restart.

### 5. Use architecture-specific HTTPS feeds and authenticated artifacts

Keep the existing per-architecture Developer ID DMGs. Each direct bundle embeds the feed URL for its packaged payload architecture and the public Ed25519 update key. This avoids choosing a native arm64 artifact for an x64 app running under Rosetta. Feed paths are `updates/arm64/appcast.xml` and `updates/x64/appcast.xml` on the existing GitHub Pages site; download URLs point to immutable, versioned GitHub Release assets.

Generate appcasts with Sparkle tooling after Developer ID signing, notarization, stapling, checksums, and verification. Sign the final update archives and appcast feeds with the separate Ed25519 private key held in the operator's keychain under `de.ltsoftware.macstorageatlas` by default. Enable both `SURequireSignedFeed` and its required `SUVerifyUpdateBeforeExtraction` companion. SHA-256 checksum files remain useful for manual downloads but are not the updater's authenticity mechanism. Release selection remains stable-only and respects minimum macOS versions and monotonic bundle versions. Sparkle treats an absent hardware requirement as Intel-compatible; the generator validates this convention. Because Sparkle evaluates the physical processor under Rosetta, `shouldProceedWithUpdate` additionally rejects a selected arm64-only entry for an x64 bridge and requires the HTTPS DMG name to match the installed runtime and advertised version. Sparkle retains version selection and OS checks.

The operator uploads and verifies assets before publishing feeds that reference them. Feed production is a local release step; publication remains manual or through the existing locally authenticated tooling. Do not introduce a signing CI workflow. Preserve framework symlinks and sign nested helper executables and bundles inside-out before signing the outer app. Do not apply the current flat `Contents/MacOS` file-signing loop to a framework hierarchy.

### 6. Keep preference ownership and networking explicit

Ask users whether to enable automatic checks before scheduling them. Keep checks disabled until consent; manual checking remains available. Use Sparkle's persisted preference as the single source of truth and expose it through the service adapter rather than maintaining a conflicting `AppSettings` flag. Schedule enabled checks no more frequently than once per 24 hours and retain manual checking at any time when no check is already active.

Network work must not block startup or the UI thread. Manual checks show up-to-date, unavailable, and failed outcomes; background failures remain quiet and retry through the normal schedule. Downloads can be cancelled before replacement. A missing production framework or invalid configuration disables updates with a useful manual-check error while leaving scanning available.

Disable optional system profiling and telemetry. Requests contain only update protocol metadata needed to select and download releases, without scan paths, results, cleanup selections, or stable installation identifiers. Document that GitHub can observe IP addresses and ordinary HTTP metadata when serving feeds and assets.

### 7. Assign validation to the owning layer

| Area | Responsibility and verification |
| --- | --- |
| App | Commands, distribution-dependent UI, consent/preference adapter, operation gate, settings flush; NUnit/NSubstitute tests with no public network access |
| Platform.Mac | Bridge lifetime, thread dispatch, availability, callbacks, and native cancellation; gated macOS tests against isolated fixtures |
| Packaging | Conditional inputs, framework embedding, helper signing, feed generation, and Store exclusions; packaging regression checks plus signed smoke tests |
| Core | Existing scan and filesystem behavior remains unchanged; existing suite detects regressions |
| Rendering | No update dependencies or behavior changes; existing suite remains green |

Use local feeds and test bundles for integration scenarios including old/equal/new versions, malformed feeds, timeouts, untrusted archives, minimum-OS incompatibility, and wrong architectures. Verify failure before replacement leaves the installed bundle runnable. Exercise read-only DMG launches and permission-denied installation without advising Gatekeeper bypasses.

## Risks / Trade-offs

- Native restart hooks might not compose with Avalonia termination → prove them with the packaged native host in the spike and gate further work on the result.
- App work can start between confirmation and termination → recheck eligibility on the UI dispatcher and reserve the final shutdown transition.
- Framework helpers introduce signing complexity → sign inside-out and verify the complete bundle, Gatekeeper acceptance, notarization, and relaunch.
- An architecture feed can point at the wrong asset → verify payload compatibility during feed generation and test both packaged architectures.
- A common publish directory can leak direct-build files into Store packaging → use clean mode-specific staging and inspect both universal payloads and native dependencies.
- Loss of the update key prevents ordinary updates → keep an operator-managed backup and document Sparkle's supported recovery/key-rotation path; never commit private material.
- Installation can fail or the new version can contain a defect → retain the old app on pre-replacement failure, use Sparkle's recovery behavior for replacement failures, and remove a defective release from feeds. A replacement failure after termination cannot report back to the exited App. Persist only the explicitly approved target version in the Direct updater preference domain before termination; on next launch consume it and show recovery guidance once if the installed version is still older. Successful or newer manual installations clear it silently. No scan data, settings file, or history snapshot is changed by this marker. Do not invent an automatic downgrade mechanism.

## Migration Plan

1. Complete the spike and finalize the pinned dependency, signing order, feed URLs, and restart hook.
2. Implement direct-update support and Store build exclusion together, then verify both distribution modes before public publication.
3. Prepare isolated old/new signed bundles and run the update and failure matrix. Verify Store sandbox startup and both universal architecture payloads without updater assets or new entitlements.
4. Publish the first updater-enabled Developer ID release and tell existing users to install it manually once. Announce update support only once that release and its feeds are live.
5. Upload subsequent verified release assets first, then publish their appcast entries. Existing Store submissions continue through the unchanged Store command and signing inputs.
6. If a feed entry is faulty, withdraw it and publish a higher fixed version. Users who installed a defective release can reinstall a known-good signed DMG manually; local settings/history are outside the app bundle and remain intact.

## Open Questions

- Clean-machine Gatekeeper and final production-feed acceptance remain pre-publication checks. A physical Intel Mac is unavailable; Rosetta is the available x64 test environment and the hardware gap is documented in the evidence record.
- The release operator must initialize and securely back up the dedicated keychain account before preparing a Direct release. Public feed defaults and HTTPS overrides are implemented; no private key is generated implicitly.
- Authenticated feeds and pre-extraction archive verification are selected. Release notes can be embedded in the signed appcast; external notes must follow Sparkle signing requirements.

## Dependency selection and initial spike findings

Verified on 2026-09-30 before application-code changes:

- Pin the binary distribution to Sparkle **2.9.6**, downloaded from the [official release archive](https://github.com/sparkle-project/Sparkle/releases/download/2.9.6/Sparkle-2.9.6.tar.xz).
- Expected SHA-256: `52bf9e88cdd972fc0c81501377a880e90d47031bd8ca5462488f843e2609e192`. The downloaded archive matches the GitHub release asset digest.
- The framework passes `codesign --verify --deep --strict`, contains `arm64` and `x86_64`, and declares `LSMinimumSystemVersion` 10.13. The app's existing macOS 11.0 target can remain intact. This establishes binary compatibility metadata, not runtime smoke-test evidence.
- Sparkle 2.10.0 raises its minimum to macOS 12.0 and is unsuitable for this scope. Sparkle 2.9.6 is the newest stable binary distribution retaining the earlier deployment target as of this verification date and includes the 2.9-series security fixes.
- Preserve Sparkle's complete bundled LICENSE in direct app resources, including its external-license notices. Sign framework helpers and enclosing bundles inside-out with the direct app identity; Store builds must not acquire these dependencies.
- Acquire archives only from the pinned official URL, verify their digest before extracting or using their tools, and preserve framework symlinks. Investigation downloads remain outside the repository; no binary dependencies or private keys have been committed.

The user approved the standard workflow on 2026-09-30 after comparing Rectangle and Transmission. The approved plan preserves Sparkle's ordinary-quit completion of an already-approved prepared installation, with an App gate for updater-triggered restart. The previously proposed custom user-driver adapter is rejected. Isolated signed arm64 and Rosetta x64 runs proved replacement, relaunch, and active-work deferral. Production-release acceptance and physical Intel evidence remain open. See [spike findings and reference apps](spike-findings.md).
