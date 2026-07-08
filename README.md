# MacStorageAtlas

**MacStorageAtlas** is a macOS disk usage analyzer inspired by WinDirStat-style
tools. It helps you understand what consumes storage on your Mac by scanning
folders and volumes, then visualizing the results as a sortable folder tree, a
proportional treemap, file-type statistics, and a list of the largest files.

## Features

- Select and scan any folder or volume with live progress reporting, and
  cancel a running scan at any time.
- Browse results as a folder tree sorted by size, or as an interactive treemap.
- See storage broken down by file type and a list of the largest files.
- Search and filter scanned items by name or path.
- Reveal items in Finder or move them safely to the Trash.
- Inspect files and folders that couldn't be scanned, and copy their paths to
  the clipboard.
- Configurable scanning: hidden files, symbolic links, and `.app` package
  expansion.
- Remembers your scanner preferences and recent scan locations between runs.
- Modern, native-feeling UI that follows the system light/dark appearance, with
  a responsive treemap and a live scan-progress overlay.

> Branding artwork lives under `src/MacStorageAtlas.App/Assets/`: `app.ico` (window
> icon), `icon.png` (1024×1024 master), and `MacStorageAtlas.icns` for macOS app
> bundling.

## Prerequisites

- macOS
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Avalonia templates are **not** required to build or run; all dependencies are
  restored from NuGet.

## Build

```shell
dotnet restore
dotnet build --no-restore
```

## Test

```shell
dotnet test --no-build
```

## Run

```shell
dotnet run --project src/MacStorageAtlas.App
```

## Package (DMG)

Run the packaging script from the repository root. It publishes a self-contained
Release build, wraps it in a `MacStorageAtlas.app` bundle (with the `.icns` app
icon), and creates a DMG with a drag-to-`Applications` shortcut.

```shell
./build-dmg.sh            # Apple Silicon (default) → MacStorageAtlas.dmg
./build-dmg.sh arm64      # Apple Silicon (osx-arm64)
./build-dmg.sh x64        # Intel (osx-x64)
./build-dmg.sh both       # both architectures, one DMG each
```

When building `both`, the DMGs are named per architecture
(`MacStorageAtlas-osx-arm64.dmg` and `MacStorageAtlas-osx-x64.dmg`). Each build
is self-contained and does **not** run under Rosetta on the other architecture —
pick the DMG that matches the target Mac.

> ⚠️ **Unsigned & un-notarized build**
>
> This DMG is **not code-signed or notarized**, because the project has no paid
> Apple Developer account. macOS Gatekeeper will therefore block the app on
> first launch ("MacStorageAtlas is damaged and can't be opened" / "cannot be
> opened because Apple cannot check it for malicious software").
>
> To run it anyway, either:
>
> - Right-click the app in `/Applications` → **Open** → confirm **Open** in the
>   dialog, or
> - Remove the quarantine attribute from a terminal:
>
>   ```shell
>   xattr -dr com.apple.quarantine /Applications/MacStorageAtlas.app
>   ```
>
> Only do this for builds you trust and compiled yourself.

## Project structure

```text
src/
  MacStorageAtlas.App              Avalonia UI and MVVM shell
  MacStorageAtlas.Core             disk scanning and domain logic
  MacStorageAtlas.Rendering        treemap layout logic
  MacStorageAtlas.Platform.Mac     macOS-specific integrations (reveal, trash, dock icon)

tests/
  MacStorageAtlas.Tests            NUnit tests
```

## Documentation

- Product backlog and feature specifications: [`docs/FEATURES.md`](docs/FEATURES.md)
- macOS packaging and distribution: [`docs/PACKAGING.md`](docs/PACKAGING.md)
