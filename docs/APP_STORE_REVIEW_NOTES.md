# App Store review notes

This document is the source for the **App Review Information** notes and for the
reply to an App Review message in App Store Connect. Paste the sections below
into the Notes field, and keep this file updated whenever the submitted build
changes.

The first submission of version 1.0 was rejected under Guideline 2.1
(Information Needed / Performance: App Completeness) because the App Review
Information section did not contain this information.

## Before submitting

- [ ] Update [Tested configurations](#2-tested-configurations) if the build was
      tested on additional hardware or a newer macOS version.
- [ ] Record the screen recording described in
      [Screen recording](#1-screen-recording) on a physical Mac running the
      latest released macOS.
- [ ] Confirm the App Store screenshots show the app in use with a real scan
      result, not the empty start screen.
- [ ] Verify Trash, Finder reveal, and Quick Look in the signed sandboxed build
      as described in [docs/PACKAGING.md](PACKAGING.md).

## 1. Screen recording

Record on a physical Mac, running the latest released macOS, with the App Store
build. The recording must start with launching the app.

1. Launch MacStorageAtlas from the Dock or Launchpad.
2. Choose **Select folder** and pick `Downloads` in the open panel. Keep any
   macOS access prompt visible in the recording.
3. Start the scan and let the live progress run: current path, file and folder
   counts, and bytes scanned.
4. Cancel the running scan once to show that partial results stay visible, then
   scan again and let it finish.
5. Show the folder tree and the treemap side by side, then select a treemap
   block to show the item details.
6. Open the **File types** tab and the **Largest files** tab.
7. Start the opt-in duplicate analysis and show the result.
8. Select an item, press Space for Quick Look, then use **Reveal in Finder**.
9. Add an item to the cleanup basket, open the review, confirm, and show that the
   item is now in the macOS Trash and can be put back.
10. Open the settings surface to show the measurement mode, hidden files, and
    package expansion options.

MacStorageAtlas has no account registration, no login, no account deletion, no
in-app purchase, no subscription, and no user-generated content, so those flows
cannot be shown.

## 2. Tested configurations

MacStorageAtlas is developed and tested by a small independent developer with
one Mac. The submitted binary is a universal build for Apple Silicon
(`osx-arm64`) and Intel (`osx-x64`).

- MacBook Pro (Mac16,6), Apple M4 Max, 64 GB, macOS 26.6.2 (25G83) — full
  testing of the Apple Silicon slice, including scanning, duplicate analysis,
  Quick Look, Finder reveal, and moving items to the Trash from a sandboxed
  build signed with the App Store entitlements.
- The same Mac, running the Intel (`x86_64`) slice under Rosetta 2 — verified
  that the Intel slice launches sandboxed and that the macOS integrations
  behave identically.

No physical Intel Mac is available for testing. The Intel slice is therefore
verified through Rosetta 2 rather than on Intel hardware. If App Review
considers that insufficient, we can remove the `x86_64` slice and submit an
Apple Silicon only build instead.

## 3. What the app does and who it is for

MacStorageAtlas is a local disk space analyzer for macOS. It answers the
question "what is using my disk space, and what can I safely reclaim?"

The user selects a folder or a volume. MacStorageAtlas scans it and presents the
result as a folder tree sorted by size, a proportional treemap, a breakdown by
file type, and a list of the largest files. An opt-in analysis finds
byte-identical duplicate files. Items can be previewed with Quick Look, revealed
in Finder, collected in a cleanup basket, and then moved to the macOS Trash
after an explicit confirmation, or moved or copied to another volume instead.

Target audience: Mac users who are running out of storage, including
photographers, video editors, and developers whose caches, build outputs, and
media libraries grow silently. The value is that the app turns "the disk is
full" into a specific, reviewable list of large items, without deleting anything
on its own.

Files are never deleted permanently by the app. Every cleanup goes to the macOS
Trash and stays recoverable, protected and system locations are blocked from
in-app cleanup, and no item is preselected for deletion.

## 4. Setup and access instructions

No account, no login credentials, no demo user, no configuration, and no sample
files are required. The app is fully functional on first launch.

Because the app is sandboxed, it can only read locations the reviewer selects:

1. Launch the app. The window starts empty until a location is selected. This is
   expected, not an incomplete state.
2. Choose **Select folder** and pick a folder in the macOS open panel, for
   example `Downloads`, `Movies`, or an entire volume under `/Volumes`.
3. Choose **Scan** to analyze the selected location.

If a scan reports inaccessible paths, that is the macOS App Sandbox refusing
locations the reviewer has not selected. The app then shows guidance explaining
that only selected locations can be scanned and offers a rescan. Full Disk
Access is intentionally not requested and would not change this behavior for a
sandboxed app.

## 5. External services

None. MacStorageAtlas performs no network requests of any kind.

- No data providers, no authentication services, no payment processors, and no
  AI services.
- No analytics, telemetry, crash reporting, advertising, or tracking.
- No accounts and no server component.
- The app does not request the network entitlement.

All scanning, duplicate analysis, and cleanup happens locally on the user's Mac.
Scan results, paths, and file metadata never leave the device. File contents are
never uploaded, logged, or persisted; duplicate analysis reads file bytes only
locally and only to confirm that two candidates are identical. Cloud-only
placeholder files are skipped rather than downloaded. The privacy policy is
published at
<https://lachi90.github.io/MacStorageAtlas/privacy.html>.

## 6. Regional differences

The app functions identically in all regions. There are no region-specific
features, no region-locked content, and no regional pricing behavior. The user
interface is English only.

## 7. Regulated industries and third-party material

Not applicable. MacStorageAtlas does not operate in a regulated industry, does
not provide medical, financial, legal, gambling, or similar services, and does
not include protected third-party material. It is a general-purpose utility that
analyzes storage on the user's own Mac.

## 8. Permissions and purpose strings

The app requests no privacy-protected data classes: no location, contacts,
camera, microphone, photos, calendar, or App Tracking Transparency. File access
is granted exclusively by the user through the standard macOS open panel, which
is why the app declares `com.apple.security.files.user-selected.read-write` and
no other file entitlement. The app does not use Apple event automation and does
not launch external processes for Trash or Finder integration.
