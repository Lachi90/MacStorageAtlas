## Context

The sandboxed build aborted during startup with:

```text
_RegisterApplication(), unable to get application ASN from launchservicesd, and
this application requires an ASN, so aborting.
```

preceded by:

```text
denied lookup: name = com.apple.coreservices.launchservicesd, flags = 0x8,
requestor = MacStorageAtlas, error = 159: Sandbox restriction
```

## Investigation

The cause was isolated by building the same bundle five ways and launching each
one signed with a Developer ID Application certificate and the App Store
entitlements.

| Main executable | AppKit reached by | Result |
| --- | --- | --- |
| Objective-C, linked against Cocoa | load time | starts |
| The same Objective-C binary inside the real app bundle | load time | starts |
| C, `dlopen` of AppKit as the first statement of `main` | runtime | aborts |
| .NET, self-contained, calling AppKit through P/Invoke | runtime | aborts |
| C, linked against CoreFoundation only, `dlopen` of AppKit | load time | starts |

The bundle, the Info.plist, the entitlements, the signing identity, and the
install location were identical across the runs, so none of them is the cause.
The dividing line is whether the main executable has CoreFoundation as a
load-time dependency. `dlopen` of CoreFoundation as the first statement of
`main` does not help, which rules out "load CoreFoundation earlier" as an
in-process fix and rules out any managed-code workaround.

`otool -L` on the published .NET app host lists only `libSystem.B.dylib` and
`libc++.1.dylib`. That is why .NET GUI apps hit this and Objective-C and Swift
apps do not.

## Decision

Make the bundle's main executable a native host that links CoreFoundation and
hosts the runtime in process.

`packaging/native-host.c` resolves its own path, derives the runtime payload
directory, loads `libhostfxr.dylib` from it, and calls
`hostfxr_main_startupinfo`, which is the documented hosting entry point the .NET
app host itself uses. `build-dmg.sh` compiles it per architecture and replaces
the published app host in the bundle.

### Why not the alternatives

- **Keep the exception.** Not available. App Review refused it under Guideline
  2.4.5(i).
- **`DYLD_INSERT_LIBRARIES` through `LSEnvironment`.** Requires
  `com.apple.security.cs.allow-dyld-environment-variables`, trades one
  questionable entitlement for another, and is fragile under the hardened
  runtime.
- **Patch an `LC_LOAD_DYLIB` into the published app host.** No supported tool
  adds a load command, and a hand-written Mach-O patcher in the release path is
  not reviewable.
- **Native AOT.** Removes the app host entirely, but it is a large change to the
  runtime model of the whole application for a startup-only defect.

### The universal bundle

The previous universal launcher replaced its process with the nested x64 app
host through `execv`. That still aborts: an `execv` discards the LaunchServices
registration the process received at launch, so the new image falls back to the
denied lookup even though it links CoreFoundation.

The x86_64 slice of the main executable is therefore compiled with
`MACSTORAGEATLAS_PAYLOAD_PATH` and loads the nested x64 payload in the same
process. The bundle keeps one complete payload per architecture, and
`packaging/universal-launcher.c` is removed.

## Risks

- The native host takes over app-host responsibilities. It resolves paths,
  loads `libhostfxr.dylib`, and returns its exit code; it adds no behavior of
  its own, and a failure to resolve any of them exits non-zero rather than
  continuing.
- The nested slice bundle keeps its own executable so it remains a valid, signable
  bundle, even though the outer x86_64 slice no longer runs it.
- Renaming the bundle directory means a Developer ID user who installed
  `MacStorageAtlas.app` keeps it alongside `AtlasStorageAnalyzer.app` until they remove
  it. The application support directory is deliberately unchanged, so scan
  history and settings carry over.
