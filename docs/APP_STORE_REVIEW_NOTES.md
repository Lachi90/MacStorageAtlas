# App Store review notes

Everything below the separator is the exact text for the App Review Information
notes field in App Store Connect, and for the reply to the Guideline 2.1 review
message. Copy it as is. Keep it updated whenever the submitted build changes.

Before submitting: record the screen recording described in section 1, confirm
the App Store screenshots show a real scan result rather than the empty start
screen, and run the sandbox checks in docs/PACKAGING.md.

---

MacStorageAtlas 1.0.2 - App Review Information

1. SCREEN RECORDING

The attached screen recording was captured on a physical Mac running macOS
26.6.2. It starts with launching the app from the Applications folder and shows
the typical user flow: selecting a folder in the macOS open panel, scanning it,
browsing the folder tree and the treemap, inspecting item details, the file type
breakdown, the largest files, the duplicate analysis with its live progress,
Quick Look preview, Reveal in Finder, and moving an item to the Trash through
the cleanup basket review, which reports the itemized result.

The scan itself completes in about a second for the 122,000 files and 8.2 GB in
the demo folder, so the scan progress display is only briefly visible. The
recording uses a prepared demo folder rather than personal files.

MacStorageAtlas has no account registration, no login, no account deletion, no
in-app purchase, no subscription, and no user-generated content, so none of
those flows appear in the recording. The app requests no privacy-protected data
classes, so no location, contacts, camera, photos, or App Tracking Transparency
prompt appears either. The only access mechanism is the standard macOS open
panel, which is shown in the recording.

2. DEVICES AND OPERATING SYSTEMS TESTED

MacStorageAtlas is developed by a small independent developer with one Mac. The
submitted binary is a universal build for Apple Silicon (arm64) and Intel
(x86_64).

- MacBook Pro (Mac16,6), Apple M4 Max, 64 GB RAM, macOS 26.6.2 (build 25G83).
  Full testing of the Apple Silicon slice: scanning, duplicate analysis, Quick
  Look, Reveal in Finder, and moving items to the Trash. The macOS Trash,
  Finder, and file access integrations were additionally verified while running
  inside the App Sandbox with the entitlements of the submitted build.
- The same Mac running the Intel (x86_64) slice under Rosetta 2. Verified that
  the Intel slice starts inside the App Sandbox and that its macOS integrations
  behave identically to the Apple Silicon slice.

No physical Intel Mac is available for testing, so the Intel slice was verified
through Rosetta 2 rather than on Intel hardware. If App Review considers that
insufficient, we will gladly remove the x86_64 slice and submit an Apple
Silicon only build.

3. WHAT THE APP DOES, AND FOR WHOM

MacStorageAtlas is a local disk space analyzer for macOS. It answers the
question "what is using my disk space, and what can I safely reclaim?"

The user selects a folder or a volume. MacStorageAtlas scans it and presents the
result as a folder tree sorted by size, a proportional treemap, a breakdown by
file type, and a list of the largest files. An opt-in analysis finds
byte-identical duplicate files. Items can be previewed with Quick Look, revealed
in Finder, collected in a cleanup basket, and then moved to the macOS Trash
after an explicit confirmation, or moved or copied to another volume instead.

The target audience is Mac users who are running out of storage, including
photographers, video editors, and developers whose caches, build outputs, and
media libraries grow silently. The value is that the app turns "the disk is
full" into a specific, reviewable list of large items without deleting anything
on its own.

The app never deletes a file permanently. Every cleanup goes to the macOS Trash
and stays recoverable, protected and system locations are blocked from in-app
cleanup, and no item is ever preselected for deletion.

4. SETUP AND HOW TO REACH THE MAIN FEATURES

No account, no login credentials, no demo user, no configuration, and no sample
files are required. The app is fully functional on first launch.

Because the app is sandboxed, it can only read locations the reviewer selects:

1. Launch the app. The window is empty until a location is selected. This is
   the expected initial state, not an incomplete screen.
2. Choose "Select folder" and pick a folder in the macOS open panel, for
   example Downloads or Movies, or an entire volume under /Volumes.
3. Choose "Scan" to analyze the selected location.
4. After the scan completes, the tabs for file types, largest files, and
   duplicate analysis become available, and any listed item can be previewed,
   revealed in Finder, or added to the cleanup basket. Items in the cleanup
   basket are moved to the macOS Trash only after the review sheet is confirmed,
   and they stay there until the Trash is emptied. The app moves items with the
   system file API instead of automating Finder, and macOS records no Put Back
   entry for items moved that way, so a trashed item is restored by dragging it
   out of the Trash rather than through Finder's Put Back command.

If a scan reports inaccessible paths, that is the macOS App Sandbox refusing
locations the reviewer has not selected. The app then explains that only
selected locations can be scanned and offers a rescan. Full Disk Access is
deliberately not requested, because it would not widen what a sandboxed app can
read.

5. EXTERNAL SERVICES USED TO DELIVER CORE FUNCTIONALITY

None. MacStorageAtlas performs no network requests of any kind.

There are no data providers, no authentication services, no payment processors,
and no AI services. There is no analytics, telemetry, crash reporting,
advertising, or tracking, no user accounts, and no server component. The app
does not request the network entitlement.

All scanning, duplicate analysis, and cleanup happens locally on the user's Mac.
Scan results, paths, and file metadata never leave the device. File contents are
never uploaded, logged, or persisted; duplicate analysis reads file bytes only
locally and only to confirm that two candidate files are identical. Cloud-only
placeholder files are skipped rather than downloaded.

Privacy policy: https://lachi90.github.io/MacStorageAtlas/privacy.html
Support: https://lachi90.github.io/MacStorageAtlas/support.html

6. REGIONAL DIFFERENCES

The app functions identically in all regions. There are no region-specific
features, no region-locked content, and no regional differences in behavior.
The user interface is English only.

7. REGULATED INDUSTRIES AND PROTECTED THIRD-PARTY MATERIAL

Not applicable. MacStorageAtlas does not operate in a regulated industry and
does not provide medical, financial, legal, gambling, or similar services. It
includes no protected third-party material. It is a general-purpose utility that
analyzes storage on the user's own Mac.

8. ENTITLEMENTS AND PERMISSIONS

The app is sandboxed and declares only
com.apple.security.files.user-selected.read-write for file access, plus the JIT
entitlement required by the .NET runtime. File access is granted exclusively by
the user through the standard macOS open panel. The app uses no Apple event
automation and launches no external processes for its Trash and Finder
integrations, so no automation consent prompt can appear.

The build additionally declares
com.apple.security.temporary-exception.mach-lookup.global-name for the single
name com.apple.coreservices.launchservicesd. This is a startup requirement of
the cross-platform UI framework the app is built with, not a feature of the app.
The framework creates NSApplication during startup, AppKit registers the process
with LaunchServices from that call, and without the exception the App Sandbox
denies that lookup and the process aborts before its first window appears. The
exception grants nothing beyond that one system service, and the app makes no
LaunchServices calls of its own. It requests no other temporary exception, no
network access, and no additional file access.
