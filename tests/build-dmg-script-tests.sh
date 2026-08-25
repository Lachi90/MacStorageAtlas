#!/bin/bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SCRIPT="$ROOT_DIR/build-dmg.sh"
NATIVE_HOST_SOURCE="$ROOT_DIR/packaging/native-host.c"

assert_contains() {
  local haystack="$1"
  local needle="$2"

  if [[ "$haystack" != *"$needle"* ]]; then
    printf 'Expected output to contain: %s\n' "$needle" >&2
    printf 'Actual output:\n%s\n' "$haystack" >&2
    exit 1
  fi
}

assert_fails() {
  local output

  if output="$("$@" 2>&1)"; then
    printf 'Expected command to fail: %s\n' "$*" >&2
    printf 'Actual output:\n%s\n' "$output" >&2
    exit 1
  fi

  printf '%s\n' "$output"
}

bash -n "$SCRIPT"

help_output="$("$SCRIPT" --help)"
assert_contains "$help_output" "./build-dmg.sh [--dry-run] [arm64|x64|both]"
assert_contains "$help_output" "./build-dmg.sh release [--dry-run] <arm64|x64|both> <version> <signing-identity> <notary-profile>"
assert_contains "$help_output" "./build-dmg.sh appstore [--dry-run] <arm64|x64|both> <version> <app-signing-identity> <installer-signing-identity> [provisioning-profile]"

unsigned_output="$("$SCRIPT" --dry-run both)"
assert_contains "$unsigned_output" "mode=unsigned"
assert_contains "$unsigned_output" "runtime=osx-arm64 artifact=StorageAtlas-osx-arm64.dmg"
assert_contains "$unsigned_output" "runtime=osx-x64 artifact=StorageAtlas-osx-x64.dmg"

release_output="$("$SCRIPT" release --dry-run both 1.2.3 "Developer ID Application: Example (TEAMID)" "Example-notary")"
assert_contains "$release_output" "mode=release"
assert_contains "$release_output" "version=1.2.3"
assert_contains "$release_output" "runtime=osx-arm64 artifact=StorageAtlas-1.2.3-osx-arm64.dmg"
assert_contains "$release_output" "runtime=osx-x64 artifact=StorageAtlas-1.2.3-osx-x64.dmg"

appstore_output="$("$SCRIPT" appstore --dry-run both 1.2.3 "Apple Distribution: Example (TEAMID)" "3rd Party Mac Developer Installer: Example (TEAMID)" "/tmp/example.provisionprofile")"
assert_contains "$appstore_output" "mode=appstore"
assert_contains "$appstore_output" "version=1.2.3"
assert_contains "$appstore_output" "runtime=universal artifact=StorageAtlas-1.2.3-universal-appstore.pkg"

invalid_target_output="$(assert_fails "$SCRIPT" nope)"
assert_contains "$invalid_target_output" "Unknown target 'nope'"

missing_release_args_output="$(assert_fails "$SCRIPT" release arm64 1.2.3)"
assert_contains "$missing_release_args_output" "Usage:"

invalid_version_output="$(assert_fails "$SCRIPT" release --dry-run arm64 1/2 "Developer ID Application: Example (TEAMID)" Example-notary)"
assert_contains "$invalid_version_output" "Release version must not contain '/'"

invalid_appstore_version_output="$(assert_fails "$SCRIPT" appstore --dry-run arm64 1/2 "Apple Distribution: Example (TEAMID)" "3rd Party Mac Developer Installer: Example (TEAMID)")"
assert_contains "$invalid_appstore_version_output" "App Store version must not contain '/'"

if command -v security >/dev/null 2>&1 && command -v hdiutil >/dev/null 2>&1 && command -v dotnet >/dev/null 2>&1; then
  missing_identity_output="$(assert_fails "$SCRIPT" release arm64 1.2.3 "Developer ID Application: Missing Example (NOPE)" Example-notary)"
  assert_contains "$missing_identity_output" "Signing identity 'Developer ID Application: Missing Example (NOPE)' was not found in the local keychain."
fi

if ! grep -q "shasum -a 256" "$SCRIPT"; then
  printf 'Expected script to use shasum -a 256\n' >&2
  exit 1
fi

if ! grep -q -- '--entitlements "$entitlements_source"' "$SCRIPT"; then
  printf 'Expected release signing to use the entitlements file\n' >&2
  exit 1
fi

if ! grep -q "verify_app_launches" "$SCRIPT"; then
  printf 'Expected release verification to run an app launch smoke test\n' >&2
  exit 1
fi

if ! grep -q "com.apple.security.cs.allow-jit" "$ROOT_DIR/src/MacStorageAtlas.App/MacStorageAtlas.entitlements"; then
  printf 'Expected release entitlements to allow CoreCLR JIT\n' >&2
  exit 1
fi

if ! grep -q "com.apple.security.app-sandbox" "$ROOT_DIR/src/MacStorageAtlas.App/MacStorageAtlas.AppStore.entitlements"; then
  printf 'Expected App Store entitlements to enable app sandbox\n' >&2
  exit 1
fi

if ! grep -q "productbuild" "$SCRIPT"; then
  printf 'Expected App Store packaging to use productbuild\n' >&2
  exit 1
fi

if ! grep -q "validate_appstore_provisioning_profile" "$SCRIPT"; then
  printf 'Expected App Store packaging to validate the provisioning profile\n' >&2
  exit 1
fi

if ! grep -q "OSX|macOS" "$SCRIPT"; then
  printf 'Expected App Store provisioning profile validation to require macOS\n' >&2
  exit 1
fi

if ! grep -q "create_universal_app_bundle" "$SCRIPT"; then
  printf 'Expected App Store packaging to create universal app bundles\n' >&2
  exit 1
fi

if grep -q "temporary-exception" "$ROOT_DIR/src/MacStorageAtlas.App/MacStorageAtlas.AppStore.entitlements"; then
  printf 'Expected App Store entitlements to request no temporary exception\n' >&2
  exit 1
fi

if [ ! -f "$NATIVE_HOST_SOURCE" ]; then
  printf 'Expected the native host source at %s\n' "$NATIVE_HOST_SOURCE" >&2
  exit 1
fi

if ! grep -q -- "-framework CoreFoundation" "$SCRIPT"; then
  printf 'Expected the native host to link CoreFoundation so LaunchServices registration happens at launch\n' >&2
  exit 1
fi

if ! grep -q "MACSTORAGEATLAS_PAYLOAD_PATH" "$SCRIPT"; then
  printf 'Expected the x86_64 slice host to load the nested payload in process\n' >&2
  exit 1
fi

if grep -q "execv" "$NATIVE_HOST_SOURCE"; then
  printf 'Expected the native host to host the runtime in process instead of replacing it\n' >&2
  exit 1
fi

if ! grep -q -- "-verify_arch arm64 x86_64" "$SCRIPT"; then
  printf 'Expected App Store packaging to verify universal app architecture\n' >&2
  exit 1
fi

if ! grep -q "normalize_app_bundle_permissions" "$SCRIPT"; then
  printf 'Expected packaging to normalize app bundle file permissions\n' >&2
  exit 1
fi

if ! grep -q "clear_app_bundle_extended_attributes" "$SCRIPT"; then
  printf 'Expected packaging to clear extended attributes from app bundles\n' >&2
  exit 1
fi

if ! grep -q "xattr -cr" "$SCRIPT"; then
  printf 'Expected packaging to remove quarantine attributes recursively\n' >&2
  exit 1
fi

if ! grep -q "go+r" "$SCRIPT"; then
  printf 'Expected packaged files to be readable by non-root users\n' >&2
  exit 1
fi

if ! grep -q "create_appstore_signing_entitlements" "$SCRIPT"; then
  printf 'Expected App Store packaging to generate signing entitlements from the provisioning profile\n' >&2
  exit 1
fi

if ! grep -q "com.apple.application-identifier" "$SCRIPT"; then
  printf 'Expected App Store signing entitlements to include the application identifier\n' >&2
  exit 1
fi

if command -v clang >/dev/null 2>&1 && command -v otool >/dev/null 2>&1; then
  host_build_dir="$(mktemp -d "${TMPDIR:-/tmp}/macstorageatlas-host-tests.XXXXXX")"
  trap 'rm -rf "$host_build_dir"' EXIT

  for architecture in arm64 x86_64; do
    if ! clang -arch "$architecture" -mmacosx-version-min=11.0 -O2 \
      -DMACSTORAGEATLAS_MANAGED_ASSEMBLY='"MacStorageAtlas.App.dll"' \
      -framework CoreFoundation \
      -o "$host_build_dir/host-$architecture" "$NATIVE_HOST_SOURCE"; then
      printf 'Expected the native host to compile for %s\n' "$architecture" >&2
      exit 1
    fi

    if ! otool -L "$host_build_dir/host-$architecture" | grep -q "CoreFoundation.framework"; then
      printf 'Expected the %s native host to depend on CoreFoundation at load time\n' "$architecture" >&2
      exit 1
    fi
  done

  if ! clang -arch x86_64 -mmacosx-version-min=11.0 -O2 \
    -DMACSTORAGEATLAS_MANAGED_ASSEMBLY='"MacStorageAtlas.App.dll"' \
    -DMACSTORAGEATLAS_PAYLOAD_PATH='"/Contents/Helpers/StorageAtlas-x86_64.app/Contents/MacOS"' \
    -framework CoreFoundation \
    -o "$host_build_dir/host-slice" "$NATIVE_HOST_SOURCE"; then
    printf 'Expected the x86_64 slice host to compile with a nested payload path\n' >&2
    exit 1
  fi
fi

printf 'build-dmg.sh script tests passed\n'
