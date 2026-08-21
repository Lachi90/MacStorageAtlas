## 1. Universal Bundle Layout

- [x] 1.1 Publish both runtimes and place the arm64 payload in `Contents/MacOS` and the x64 payload in a nested `Contents/Helpers/MacStorageAtlas-x86_64.app` bundle.
- [x] 1.2 Give the nested slice its own `Info.plist` derived from the app's, with a distinct bundle identifier.
- [x] 1.3 Add `packaging/universal-launcher.c`, which resolves the bundle from its own executable path and replaces its process with the nested x64 app host.
- [x] 1.4 Build the main executable with `lipo` from the arm64 app host and the compiled x86_64 launcher.
- [x] 1.5 Remove the Mach-O merge that silently dropped the architecture-specific managed payload.

## 2. Signing

- [x] 2.1 Add `MacStorageAtlas.AppStore.Slice.entitlements` with App Sandbox inheritance and the JIT entitlement CoreCLR requires.
- [x] 2.2 Sign the nested slice bundle before the outer bundle, and sign its executable with the slice entitlements.
- [x] 2.3 Make the nested slice executable executable for all users during bundle permission normalization.

## 3. Verification

- [x] 3.1 Require `clang` and the launcher source when building the universal target.
- [x] 3.2 Verify the main executable carries both architectures and the nested slice carries x86_64.
- [ ] 3.3 Launch both slices of the built package on a Mac and confirm each reaches its main window.

## 4. Documentation

- [x] 4.1 Document the universal bundle layout, why a merged payload cannot work, and the slice entitlements in `docs/PACKAGING.md`.
- [x] 4.2 Document how to launch the Intel slice through Rosetta 2 for verification.
- [x] 4.3 Keep the App Store review information consistent with what the submitted build actually does.
