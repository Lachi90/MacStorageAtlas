## 1. Native Host

- [x] 1.1 Add `packaging/native-host.c`, which links CoreFoundation, resolves its runtime payload directory, and starts the managed entry assembly through `hostfxr_main_startupinfo`.
- [x] 1.2 Compile the native host per architecture in `build-dmg.sh` and install it as the bundle's `CFBundleExecutable`.
- [x] 1.3 Remove the published .NET app host from the bundle.
- [x] 1.4 Require `clang` and the native host source for every packaging mode.

## 2. Universal Bundle

- [x] 2.1 Compile the x86_64 slice with `MACSTORAGEATLAS_PAYLOAD_PATH` so it loads the nested x64 payload in process.
- [x] 2.2 Build the universal main executable with `lipo` from the arm64 and x86_64 native hosts.
- [x] 2.3 Remove `packaging/universal-launcher.c` and the `execv` handoff that discarded the LaunchServices registration.

## 3. Entitlements

- [x] 3.1 Remove `com.apple.security.temporary-exception.mach-lookup.global-name` from `MacStorageAtlas.AppStore.entitlements`.

## 4. Product Name

- [x] 4.1 Set `CFBundleName` and `CFBundleDisplayName` to "Atlas Storage Analyzer" and keep the bundle identifier unchanged.
- [x] 4.2 Name the app bundle directory and the release artifacts `AtlasStorageAnalyzer`.
- [x] 4.3 Rename the macOS menu bar title, the window title, the in-app guidance text, and the export file name prefix.
- [x] 4.4 Leave namespaces, assembly names, and the application support directory unchanged so existing scan history and settings stay readable.

## 5. Tests

- [x] 5.1 Assert that the App Store entitlements request no temporary exception.
- [x] 5.2 Compile the native host for both architectures and assert CoreFoundation is a load-time dependency.
- [x] 5.3 Assert that packaging loads the nested payload in process rather than replacing it.
- [x] 5.4 Update the name assertions for the menu application, the window title, the export file names, and the release artifacts.
- [x] 5.5 Repair the stale packaging assertion that still referenced the removed `merge_universal_publish_output`.

## 6. Verification

- [x] 6.1 Launch the sandboxed arm64 build signed with the App Store entitlements and confirm it reaches its main window with no denied lookup.
- [x] 6.2 Launch the x86_64 slice under Rosetta 2 and confirm it reaches its main window and loads the nested x64 payload.
- [x] 6.3 Build and upload the signed universal App Store package, and submit it with the updated review notes.

## 7. Documentation

- [x] 7.1 Document the native host, the load-order cause, and why `dlopen` is not sufficient in `docs/PACKAGING.md`.
- [x] 7.2 Replace the sandbox verification steps that described the removed exception.
- [x] 7.3 Update `docs/APP_STORE_REVIEW_NOTES.md` with the new name, the removed exception, and the resolution of both review issues.
- [x] 7.4 Update `README.md`, the documentation under `docs/`, and the landing page for the product name and the artifact names.
