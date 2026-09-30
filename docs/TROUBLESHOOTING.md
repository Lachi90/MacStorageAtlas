# Troubleshooting

## Incomplete scans

macOS can stop a scan from reading a location. The remedy depends on which build
you use, because the Mac App Store edition runs in the macOS App Sandbox and the
GitHub Release edition does not. The in-app guidance banner already shows the
guidance that matches your build.

## Mac App Store edition: select the location

A sandboxed app can only read folders and volumes you select yourself. If a scan
reports inaccessible paths:

1. Choose **Select folder**.
2. Select the location that was skipped, or a parent that contains it.
3. Rescan.

Full Disk Access does not widen what a sandboxed app can read, so the Mac App
Store edition neither requests it nor offers the settings action.

## GitHub Release edition: Full Disk Access

macOS may prevent Atlas Storage Analyzer from reading protected locations even when the
selected folder exists and ordinary files scan normally. If a completed scan has
permission-related inaccessible paths, the app shows a guidance banner with the
number of inaccessible paths and keeps the detailed scan errors available.

Use the banner's **Open Settings** action when it works. If System Settings does
not open to the right place, grant access manually:

1. Open **System Settings**.
2. Choose **Privacy & Security**.
3. Open **Full Disk Access**.
4. Add or enable **Atlas Storage Analyzer**.
5. Restart Atlas Storage Analyzer if macOS asks.
6. Rescan the same location.

Atlas Storage Analyzer cannot grant Full Disk Access itself and never asks for an
administrator password. A readable test path does not prove every protected
location is available, so the app phrases access status conservatively.

Inaccessible paths are not purgeable space, free space, available space, or
cleanup recommendations. They are paths the scan could not read, and their exact
errors remain visible in the Scan errors tab.

## Software updates

The next updater-enabled Developer ID release uses Sparkle. Existing public
DMGs need one manual installation of that first release; the update rollout is
not live until its signed feeds and release assets are published. Updates for
Mac App Store installations remain managed by the Store. Unsigned development
builds have no update controls or production requests.

In a Direct release, choose **Check for Updates…** in the application menu.
**Automatically Check for Updates** is off initially, remembers your choice,
and uses a minimum 24-hour schedule. Disabling it leaves manual checks available.
Automatic checks never approve downloads or installation. Background check
failures stay quiet; a manual check shows a failure and can be retried.

Export any current results before installing. In-memory scans close on restart;
settings and saved history stay outside the application bundle. Choosing
installation approves its preparation. A prepared update may finish when you
quit normally. Closing or postponing the initial offer before downloading does
not approve installation.

A running scan, duplicate analysis, export, cleanup, Trash, or relocation task
postpones an updater-triggered restart. Finish that task, then choose **Finish
Installing Update…**. Becoming idle does not restart automatically. Failed
settings persistence also postpones the restart; check that the app's settings
location remains writable and retry.

If replacement fails after the app has quit, reopen the old app. It reports
**The update installation did not complete** once when the approved version is
still missing. Check that the app and its containing folder are writable and
unlocked, then retry **Check for Updates…** or install the latest signed DMG.
This recovery notice does not change settings or saved scan history.

Install the app in a writable location such as Applications before updating.
An app opened from a mounted read-only DMG cannot replace itself. If a check
fails, verify the connection and retry. If startup reports that the updater is
unavailable, reinstall the latest signed DMG. Signature, extraction, identity,
or installation errors require a fresh verified release download; never bypass
signature or Gatekeeper verification. Settings and saved history can be retained
when reinstalling the application bundle.
