#!/bin/bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SCRIPT="$ROOT_DIR/build-dmg.sh"

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

unsigned_output="$("$SCRIPT" --dry-run both)"
assert_contains "$unsigned_output" "mode=unsigned"
assert_contains "$unsigned_output" "runtime=osx-arm64 artifact=MacStorageAtlas-osx-arm64.dmg"
assert_contains "$unsigned_output" "runtime=osx-x64 artifact=MacStorageAtlas-osx-x64.dmg"

release_output="$("$SCRIPT" release --dry-run both 1.2.3 "Developer ID Application: Example (TEAMID)" "Example-notary")"
assert_contains "$release_output" "mode=release"
assert_contains "$release_output" "version=1.2.3"
assert_contains "$release_output" "runtime=osx-arm64 artifact=MacStorageAtlas-1.2.3-osx-arm64.dmg"
assert_contains "$release_output" "runtime=osx-x64 artifact=MacStorageAtlas-1.2.3-osx-x64.dmg"

invalid_target_output="$(assert_fails "$SCRIPT" nope)"
assert_contains "$invalid_target_output" "Unknown target 'nope'"

missing_release_args_output="$(assert_fails "$SCRIPT" release arm64 1.2.3)"
assert_contains "$missing_release_args_output" "Usage:"

invalid_version_output="$(assert_fails "$SCRIPT" release --dry-run arm64 1/2 "Developer ID Application: Example (TEAMID)" Example-notary)"
assert_contains "$invalid_version_output" "Release version must not contain '/'"

if command -v security >/dev/null 2>&1 && command -v hdiutil >/dev/null 2>&1 && command -v dotnet >/dev/null 2>&1; then
  missing_identity_output="$(assert_fails "$SCRIPT" release arm64 1.2.3 "Developer ID Application: Missing Example (NOPE)" Example-notary)"
  assert_contains "$missing_identity_output" "Signing identity 'Developer ID Application: Missing Example (NOPE)' was not found in the local keychain."
fi

if ! grep -q "shasum -a 256" "$SCRIPT"; then
  printf 'Expected script to use shasum -a 256\n' >&2
  exit 1
fi

printf 'build-dmg.sh script tests passed\n'
