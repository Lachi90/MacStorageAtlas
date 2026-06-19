# MacStorageAtlas

**MacStorageAtlas** is a macOS disk usage analyzer inspired by WinDirStat-style
tools. It helps you understand what consumes storage on your Mac by scanning
folders and volumes, then visualizing the results as a sortable folder tree, a
proportional treemap, file-type statistics, and a list of the largest files.

## Features

- Select and scan any folder or volume with live progress reporting.
- Browse results as a folder tree sorted by size, or as an interactive treemap.
- See storage broken down by file type and a list of the largest files.
- Search and filter scanned items by name or path.
- Reveal items in Finder or move them safely to the Trash.
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
