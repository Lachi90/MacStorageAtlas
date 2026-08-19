## 1. Release Signing Spike

- [x] 1.1 Inspect the current `dotnet publish` output for `osx-arm64` and `osx-x64` and record which nested files require explicit signing.
- [x] 1.2 Run a local signing/notarization spike against one RID with the Developer ID Application identity and `notarytool` profile available on the release machine.
- [x] 1.3 Determine the minimal hardened runtime entitlement set required for the signed .NET 10 Avalonia app to launch, and record the decision in `design.md`.
- [x] 1.4 Decide the release version input source and update `design.md` if the implementation evidence changes the initial recommendation.

## 2. Local Packaging Script Changes

- [x] 2.1 Refactor the repository-root packaging script so unsigned packaging remains available for `arm64`, `x64`, and `both` without Apple credentials.
- [x] 2.2 Add an explicit signed release mode that requires signing identity, notarization profile, and release version inputs before producing distributable artifacts.
- [x] 2.3 Add nested code signing and final app bundle signing with hardened runtime and secure timestamp enabled.
- [x] 2.4 Add release DMG creation after app signing while preserving architecture-specific artifact names for `both`.
- [x] 2.5 Add notarization submission, wait, log guidance on failure, stapling, and stapler validation for each signed release DMG.
- [x] 2.6 Add local verification steps for `codesign`, `spctl`, `hdiutil verify`, and stapled DMGs before reporting release readiness.
- [x] 2.7 Generate SHA-256 checksum files only after final signed and notarized DMGs pass release verification.
- [x] 2.8 Add script tests or shell-level checks that cover argument parsing, missing credential inputs, unsigned mode preservation, artifact naming, and checksum command selection without requiring real Apple credentials.

## 3. Credential and Upload Boundaries

- [x] 3.1 Ensure no signing certificate, notary password, keychain profile secret, GitHub token, or generated release artifact is committed or generated into tracked source files.
- [x] 3.2 Verify the implementation does not add GitHub Actions, CI/CD release workflows, hosted runner signing, repository secrets, or automated GitHub Release publishing.
- [x] 3.3 Document local `notarytool` keychain profile setup and local GitHub upload options using the web UI or authenticated `gh release upload`.

## 4. Documentation and Roadmap

- [x] 4.1 Update `docs/PACKAGING.md` with unsigned development packaging, signed release prerequisites, local release commands, verification steps, checksums, and upload checklist.
- [x] 4.2 Update `README.md` so public distribution no longer instructs users to bypass Gatekeeper for signed release artifacts while still explaining unsigned local builds.
- [x] 4.3 Update `docs/index.html` download notes to distinguish signed GitHub Release artifacts from unsigned development builds.
- [x] 4.4 Update `docs/IMPLEMENTATION_ROADMAP.md` and `docs/FEATURES.md` to show local signing/notarization as this change's scope and move CI/CD plus update checks out of the implemented scope.
- [x] 4.5 Review docs for any commands, behavior, limitations, or screenshots affected by the release-packaging change and update or explicitly record that no further documentation update is necessary.

## 5. Validation

- [x] 5.1 Run `dotnet build MacStorageAtlas.slnx --no-restore`.
- [x] 5.2 Run `dotnet test MacStorageAtlas.slnx --no-build`.
- [x] 5.3 Run `dotnet format MacStorageAtlas.slnx analyzers --diagnostics IDE0005 --verify-no-changes`.
- [x] 5.4 Run `shellcheck build-dmg.sh` if ShellCheck is available.
- [x] 5.5 Run `openspec validate --all --strict --no-interactive`.
- [x] 5.6 Run `git diff --check`.
- [x] 5.7 Manually verify a signed and notarized release artifact for both `osx-arm64` and `osx-x64` on a clean or quarantine-preserving Mac before publishing the GitHub Release.
