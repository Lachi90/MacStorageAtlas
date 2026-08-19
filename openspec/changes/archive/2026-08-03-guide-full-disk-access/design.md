## Context

MacStorageAtlas already records recoverable scan failures as `ScanError` values and continues scanning after inaccessible paths. The current UI exposes those errors in a raw tab, but it does not help users understand whether a scan is incomplete because macOS protected locations were inaccessible or how to grant Full Disk Access before rescanning.

WP-06 asks for a Full Disk Access assistant. The difficult part is that macOS does not expose a simple authoritative status API suitable for saying "Full Disk Access is granted" in all launch and distribution contexts. Apple documents Full Disk Access as a user-managed Privacy & Security setting, and settings deep links have changed across macOS releases. The design therefore treats access status as conservative guidance based on scan errors and narrow platform checks, not as a privileged permission manager.

Responsibilities:

- `MacStorageAtlas.Core`: keep scan traversal, measurement, cancellation, and recoverable-error semantics unchanged.
- `MacStorageAtlas.Rendering`: unchanged.
- `MacStorageAtlas.Platform.Mac`: own macOS-specific access probes and settings launch behavior.
- `MacStorageAtlas.App`: classify scan errors for presentation, expose guidance state and commands, and render the guidance UI.
- `MacStorageAtlas.Tests`: cover classifiers, ViewModel states, platform-service outcomes through abstractions, and scan-state preservation.

## Goals / Non-Goals

**Goals:**

- Explain likely incomplete scans in language users can act on.
- Keep raw scan errors available and copyable.
- Guide users to the correct macOS Privacy & Security area and provide manual fallback instructions.
- Reuse the existing rescan lifecycle after a user changes access.
- Stay conservative about access status and avoid false certainty.
- Preserve privacy by inspecting metadata/accessibility only and never reading file contents for permission checks.

**Non-Goals:**

- Granting Full Disk Access automatically.
- Adding a privileged helper, administrator-password prompt, MDM profile flow, or App Sandbox entitlement work.
- Changing scanner inclusion, measurement, clone accounting, or error collection behavior.
- Estimating sizes for inaccessible paths.
- Treating inaccessible paths as purgeable, safe to delete, or equivalent to volume free space.
- Adding a first-run onboarding wizard before any scan exists.

## Decisions

### Keep scanner errors neutral

`DiskScanner` should continue emitting `ScanError` records for recoverable exceptions and should not know about Full Disk Access. A scan can fail to read a path for many reasons: TCC privacy protection, Unix permissions, vanished paths, removable media, network failures, or ordinary IO errors. Encoding Full Disk Access into Core would make portable scan semantics depend on macOS policy.

Alternative considered: add a scanner-level `PermissionCategory` to every scan error. This would make the scan tree appear more semantically rich, but it would either be too macOS-specific for Core or too vague to be trustworthy. The chosen approach keeps the domain result factual and layers interpretation in App.

### Use a conservative guidance model

The App layer should derive a guidance state from the completed scan result and platform access service:

- no guidance needed;
- scan incomplete with permission-related inaccessible paths;
- likely missing Full Disk Access;
- access status indeterminate;
- settings could not be opened.

The exact UI labels can be simpler than the internal state, but tests should verify that the app does not claim Full Disk Access is granted solely because one probe path is readable.

Alternative considered: binary granted/denied status. That is easier to display, but macOS behavior does not support that certainty reliably for an unsandboxed storage analyzer. The conservative model is less tidy but more honest.

### Show guidance after scan progress resolves into user-facing evidence

The guidance surface should be driven primarily by scan errors from the current scan. Post-scan guidance is tied to the user's selected root and actual inaccessible paths, while pre-scan checks can be misleading because a readable probe does not prove broad access.

Alternative considered: first-run "Check Full Disk Access" onboarding. It would create extra friction before the user has evidence of a problem and risks teaching users to grant broad access unnecessarily. A later iteration can add a help entry or manual check if user feedback shows post-scan guidance is insufficient.

### Put macOS actions behind a small service

Add an application-facing access service with test doubles. The macOS implementation should own:

- narrow, documented access probes that do not read file contents;
- opening Privacy & Security or Full Disk Access settings;
- reporting settings-open failure without throwing into the ViewModel.

The service should avoid private APIs and avoid querying TCC databases. Before implementation, run a short compatibility spike for the exact settings URL on supported macOS versions and record the fallback path in documentation.

Alternative considered: shelling out to inspect the TCC database or using private frameworks. That would be brittle, privacy-sensitive, and inappropriate for a user-facing open-source app.

The implementation uses `x-apple.systempreferences:com.apple.preference.security?Privacy_AllFiles` as the direct settings URL and falls back to `x-apple.systempreferences:com.apple.preference.security` when the direct destination cannot be opened. The user-facing fallback path is System Settings > Privacy & Security > Full Disk Access.

### Keep rescan as the recovery action

After the user changes access, the app should offer to rescan the same root using the same scan options. Existing scan cancellation and one-scan-at-a-time behavior should remain the source of truth.

Alternative considered: automatically watching System Settings and rescanning when the app returns to focus. That would be surprising for long-running scans and could rescan before macOS applies a permission change or before the app has restarted.

## Risks / Trade-offs

- Settings URL changes across macOS releases -> Use a platform service that reports failure, document manual navigation, and test the failure path.
- User grants access but the running process still cannot read protected paths -> Explain that the app may need restart, and make rescan explicit rather than automatic.
- Inaccessible paths are not caused by Full Disk Access -> Phrase guidance as likely or possible, keep raw errors visible, and avoid overwriting diagnostic detail.
- Guidance increases UI noise for minor permission errors -> Gate prominent Full Disk Access messaging behind permission-related patterns and keep lower-certainty cases subdued.
- Permission checks could accidentally read sensitive data -> Restrict probes to existence/enumeration or metadata checks and never read file contents.
- Scan errors can be numerous on large scans -> Summarize counts in guidance and leave the detailed list in the existing virtualized result surface.

## Migration Plan

This is an additive UI and platform-service change. Existing scan results, settings, exports, and documentation remain compatible. No persisted data migration is required.

Implementation can be rolled back by removing the access-guidance service, ViewModel state, commands, UI surface, and documentation updates. The existing scan errors tab and scanner behavior should continue to work unchanged.

## Open Questions

- Which settings URL works most reliably across the supported macOS range, and which fallback should the service try first when the Full Disk Access pane URL fails?
- Should guidance appear only after completed scans, or also during long-running scans once permission errors are observed? The initial design favors completed scans unless implementation evidence shows a strong reason to surface it earlier.
- Should the app include a persistent Help menu action for Full Disk Access even when no scan has shown errors? This is useful for troubleshooting but not required for WP-06.
