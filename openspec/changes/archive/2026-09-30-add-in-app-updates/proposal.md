## Why

Users of the GitHub edition currently have to discover releases and replace the app manually. This WP-01 change adds an in-app update flow with download, explicit installation approval, and restart, while preserving Mac App Store updates for the Store edition.

## What Changes

- Add a manual update check in the application menu and optional automatic checks with a persisted user preference.
- Show available stable releases and release notes, and let users download and install a compatible update without visiting a browser or dragging a new app into Applications.
- Use Sparkle's standard approval flow: Remind Me Later before download does not approve installation; a user-approved prepared update can install on ordinary quit. Defer updater-triggered restart while scans, exports, duplicate analysis, or file operations are active.
- Authenticate update downloads, preserve Apple Silicon and Intel support, and keep update failures independent of scanning and startup.
- Extend local release packaging to include the updater only in Developer ID releases and produce architecture-specific update metadata for publication alongside verified GitHub Release artifacts.
- Keep the Mac App Store edition on Store-managed updates and leave unsigned development builds without production update checks.
- Make the existing `appstore` command automatically compile and package both Store architecture payloads without the updater, its native bridge, feed configuration, or additional entitlements; no follow-up Store adaptation or manual removal is required.
- Document update preferences, privacy, release publication, and the one-time manual installation needed to adopt the first updater-enabled release.

## Capabilities

### New Capabilities

- `in-app-updates`: Distribution-aware update discovery, user preferences, compatible release selection, authenticated downloads, approved installation and restart, and recoverable failures.

### Modified Capabilities

- `release-packaging`: Package and sign updater dependencies for Developer ID releases only, generate authenticated update metadata, and preserve a ready-to-submit Store package through build-time exclusion and verification.

## Impact

- App services, ViewModels, the composition root, and native application menu integration gain update controls and operation coordination.
- Platform.Mac gains the macOS updater integration; native bridge sources belong under `packaging`. Core scan logic and Rendering are unaffected.
- `build-dmg.sh` gains updater embedding, nested signing, feed configuration, and local update-metadata generation. The proposed dependency is Sparkle 2; its exact pinned version and bridge API must be proven in a technical spike first.
- Documentation work covers `README.md`, `docs/PACKAGING.md`, `docs/TROUBLESHOOTING.md`, `docs/privacy.html`, `docs/index.html`, and WP-01 in `docs/IMPLEMENTATION_ROADMAP.md` and `docs/OPENSPEC_WORKFLOW.md`.

## Non-goals

- Silent installation, automatic restart, beta channels, mandatory updates, delta updates, and a custom updater window.
- A self-updater for the Mac App Store edition or migration between distribution channels.
- Hosted signing, CI/CD publishing, a new backend, telemetry, or scan-data uploads.
- Universal Developer ID packaging, changes to scanning semantics, or automatic restoration of an in-memory scan after restart.

## Dependencies

- The completed `sign-and-notarize-releases` change and current `release-packaging` specification.
- Existing Mac App Store packaging and the native host introduced by `resolve-app-store-rejection`; retaining their signing, sandbox, bundle identity, and universal launch behavior is an acceptance condition of this change.
- Local Developer ID credentials, notarization access, and a separate update-signing key held by the release operator.
- A public HTTPS feed location, proposed on the existing GitHub Pages site, with downloads hosted on GitHub Releases.
- A successful Avalonia/.NET integration spike, including signed bundle replacement, restart coordination, and both architectures.

## Risks

- Native lifetime, main-thread callbacks, and application termination may require careful bridging between Avalonia and Sparkle.
- Framework helper signing, read-only launch locations, interrupted downloads, and incompatible artifacts can prevent installation.
- Incorrect feed publication or loss of an update-signing key can block future updates.
- Update networking adds third-party requests that must be disclosed without exposing scanned paths or results.

## Roadmap estimate

WP-01 currently estimates 3–6 days excluding account setup and Apple processing time. That estimate includes already completed signing work and a smaller update-check scope; this proposal expands the follow-up to confirmed installation and restart. Re-estimate the remaining work after the native integration spike, rather than treating the original estimate as a commitment for this expanded scope.
