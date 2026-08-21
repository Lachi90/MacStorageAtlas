## Why

The universal Mac App Store package cannot start on Intel Macs. `build-dmg.sh`
built the universal app bundle by merging the arm64 and x64 publish output and
combining Mach-O files with `lipo`. Managed .NET assemblies are not Mach-O
files, so the merge skipped them and the bundle kept only the arm64 managed
payload. Running the x86_64 slice under Rosetta 2 aborts while CoreCLR starts:

```text
Failed to load System.Private.CoreLib.dll (error code 0x8007000B)
Failed to create CoreCLR, HRESULT: 0x8007000B
```

The framework assemblies of a self-contained publish are precompiled per
architecture, so a single merged payload cannot serve both. Every universal
package built so far carries this defect, and the App Store review information
claims an Intel verification that the build could not have passed.

## What Changes

- Build the universal App Store bundle from one complete payload per
  architecture: the arm64 payload in `Contents/MacOS` and the x64 payload in a
  nested `Contents/Helpers/MacStorageAtlas-x86_64.app` bundle.
- Make the bundle's main executable a universal binary built with `lipo` from
  the arm64 app host and a small x86_64 launcher that replaces its own process
  with the nested x64 app host, so the app keeps the process, the sandbox, and
  the container of the outer bundle.
- Sign the nested slice before the outer bundle, with App Sandbox inheritance
  plus the JIT entitlement CoreCLR needs.
- Verify both architectures of the built package as part of packaging
  verification.
- Document the layout, the reason a merged payload cannot work, and how to
  verify the Intel slice through Rosetta 2.

## Capabilities

### Modified Capabilities

- `release-packaging`: the universal Mac App Store package runs on both
  supported architectures instead of only Apple Silicon.

## Impact

- Affects `build-dmg.sh`, adds `packaging/universal-launcher.c` and
  `src/MacStorageAtlas.App/MacStorageAtlas.AppStore.Slice.entitlements`, and
  updates `docs/PACKAGING.md` and `docs/APP_STORE_REVIEW_NOTES.md`.
- The universal package roughly doubles in size, because it carries two
  complete runtime payloads.
- Does not change the per-architecture DMG artifacts, the Developer ID signing
  path, scan behavior, or any user-visible application behavior.
