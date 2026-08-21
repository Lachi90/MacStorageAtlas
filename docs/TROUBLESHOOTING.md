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

macOS may prevent MacStorageAtlas from reading protected locations even when the
selected folder exists and ordinary files scan normally. If a completed scan has
permission-related inaccessible paths, the app shows a guidance banner with the
number of inaccessible paths and keeps the detailed scan errors available.

Use the banner's **Open Settings** action when it works. If System Settings does
not open to the right place, grant access manually:

1. Open **System Settings**.
2. Choose **Privacy & Security**.
3. Open **Full Disk Access**.
4. Add or enable **MacStorageAtlas**.
5. Restart MacStorageAtlas if macOS asks.
6. Rescan the same location.

MacStorageAtlas cannot grant Full Disk Access itself and never asks for an
administrator password. A readable test path does not prove every protected
location is available, so the app phrases access status conservatively.

Inaccessible paths are not purgeable space, free space, available space, or
cleanup recommendations. They are paths the scan could not read, and their exact
errors remain visible in the Scan errors tab.
