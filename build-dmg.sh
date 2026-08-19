#!/bin/bash
set -euo pipefail

APP_NAME="MacStorageAtlas"
BUNDLE_ID="de.ltsoftware.macstorageatlas"
DEFAULT_VERSION="0.0.2"
TARGET_FRAMEWORK="net10.0"
PROJECT="src/MacStorageAtlas.App"
EXECUTABLE_NAME="MacStorageAtlas.App"
ICON_SOURCE="$PROJECT/Assets/MacStorageAtlas.icns"
ENTITLEMENTS_SOURCE="$PROJECT/MacStorageAtlas.entitlements"
LAUNCH_SMOKE_TEST_SECONDS=5

MODE="unsigned"
DRY_RUN="false"
VERSION="$DEFAULT_VERSION"
SIGNING_IDENTITY=""
NOTARY_PROFILE=""

usage() {
  printf '%s\n' "Usage:"
  printf '%s\n' "  ./build-dmg.sh [--dry-run] [arm64|x64|both]"
  printf '%s\n' "  ./build-dmg.sh release [--dry-run] <arm64|x64|both> <version> <signing-identity> <notary-profile>"
}

fail() {
  printf 'Error: %s\n' "$1" >&2
  exit 1
}

require_command() {
  command -v "$1" >/dev/null 2>&1 || fail "Required command '$1' was not found."
}

parse_target() {
  case "$1" in
    arm64) RUNTIMES=("osx-arm64") ;;
    x64) RUNTIMES=("osx-x64") ;;
    both) RUNTIMES=("osx-arm64" "osx-x64") ;;
    *) fail "Unknown target '$1'. Use: arm64 | x64 | both" ;;
  esac
}

parse_args() {
  if [ "${1:-}" = "--help" ] || [ "${1:-}" = "-h" ]; then
    usage
    exit 0
  fi

  if [ "${1:-}" = "--dry-run" ]; then
    DRY_RUN="true"
    shift
  fi

  if [ "${1:-}" = "release" ]; then
    MODE="release"
    shift

    if [ "${1:-}" = "--dry-run" ]; then
      DRY_RUN="true"
      shift
    fi

    if [ "$#" -ne 4 ]; then
      usage >&2
      exit 1
    fi

    TARGET="$1"
    VERSION="$2"
    SIGNING_IDENTITY="$3"
    NOTARY_PROFILE="$4"

    [ -n "$VERSION" ] || fail "Release version is required."
    [ "${VERSION#*/}" = "$VERSION" ] || fail "Release version must not contain '/'."
    [ -n "$SIGNING_IDENTITY" ] || fail "Signing identity is required."
    [ -n "$NOTARY_PROFILE" ] || fail "Notary keychain profile is required."
    parse_target "$TARGET"
    return
  fi

  if [ "$#" -gt 1 ]; then
    usage >&2
    exit 1
  fi

  TARGET="${1:-arm64}"
  parse_target "$TARGET"
}

dmg_name_for() {
  local runtime="$1"

  if [ "$MODE" = "release" ]; then
    printf '%s-%s-%s.dmg\n' "$APP_NAME" "$VERSION" "$runtime"
  elif [ "${#RUNTIMES[@]}" -gt 1 ]; then
    printf '%s-%s.dmg\n' "$APP_NAME" "$runtime"
  else
    printf '%s.dmg\n' "$APP_NAME"
  fi
}

print_plan() {
  local runtime

  printf 'mode=%s\n' "$MODE"
  printf 'version=%s\n' "$VERSION"

  for runtime in "${RUNTIMES[@]}"; do
    printf 'runtime=%s artifact=%s\n' "$runtime" "$(dmg_name_for "$runtime")"
  done
}

check_release_prerequisites() {
  require_command codesign
  require_command security
  require_command shasum
  require_command xcrun

  security find-identity -v -p codesigning | grep -F -- "$SIGNING_IDENTITY" >/dev/null ||
    fail "Signing identity '$SIGNING_IDENTITY' was not found in the local keychain."

  xcrun notarytool history --keychain-profile "$NOTARY_PROFILE" >/dev/null ||
    fail "Notary keychain profile '$NOTARY_PROFILE' could not be used."

  [ -f "$ENTITLEMENTS_SOURCE" ] ||
    fail "Release entitlements file was not found at '$ENTITLEMENTS_SOURCE'."
}

create_info_plist() {
  local app_bundle="$1"

  cat > "$app_bundle/Contents/Info.plist" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "https://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key>
    <string>$APP_NAME</string>
    <key>CFBundleDisplayName</key>
    <string>$APP_NAME</string>
    <key>CFBundleIdentifier</key>
    <string>$BUNDLE_ID</string>
    <key>CFBundleVersion</key>
    <string>$VERSION</string>
    <key>CFBundleShortVersionString</key>
    <string>$VERSION</string>
    <key>CFBundleExecutable</key>
    <string>$EXECUTABLE_NAME</string>
    <key>CFBundleIconFile</key>
    <string>AppIcon</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>LSMinimumSystemVersion</key>
    <string>11.0</string>
    <key>NSHighResolutionCapable</key>
    <true/>
</dict>
</plist>
EOF
}

copy_publish_output() {
  local publish_dir="$1"
  local bundle_macos_dir="$2"

  if [ "$MODE" = "release" ]; then
    find "$publish_dir" -maxdepth 1 -type f ! -name "*.pdb" -exec cp {} "$bundle_macos_dir/" \;
  else
    cp -R "$publish_dir/"* "$bundle_macos_dir/"
  fi
}

create_app_bundle() {
  local runtime="$1"
  local app_bundle="$2"
  local publish_dir="$PROJECT/bin/Release/$TARGET_FRAMEWORK/$runtime/publish"

  printf 'Publishing app...\n'
  dotnet publish "$PROJECT" -c Release -r "$runtime" --self-contained true

  printf 'Creating .app bundle...\n'
  mkdir -p "$app_bundle/Contents/MacOS"
  mkdir -p "$app_bundle/Contents/Resources"
  copy_publish_output "$publish_dir" "$app_bundle/Contents/MacOS"

  if [ -f "$ICON_SOURCE" ]; then
    cp "$ICON_SOURCE" "$app_bundle/Contents/Resources/AppIcon.icns"
  else
    printf 'Warning: icon not found at %s, bundling without icon.\n' "$ICON_SOURCE"
  fi

  create_info_plist "$app_bundle"
  chmod +x "$app_bundle/Contents/MacOS/$EXECUTABLE_NAME"
}

sign_app_bundle() {
  local app_bundle="$1"
  local main_executable="$app_bundle/Contents/MacOS/$EXECUTABLE_NAME"

  printf 'Signing nested app content...\n'
  while IFS= read -r file; do
    codesign --force --timestamp --options runtime --sign "$SIGNING_IDENTITY" "$file"
  done < <(find "$app_bundle/Contents/MacOS" -type f ! -name "$EXECUTABLE_NAME" | sort)

  printf 'Signing app executable...\n'
  codesign --force --timestamp --options runtime --entitlements "$ENTITLEMENTS_SOURCE" --sign "$SIGNING_IDENTITY" "$main_executable"

  printf 'Signing app bundle...\n'
  codesign --force --timestamp --options runtime --entitlements "$ENTITLEMENTS_SOURCE" --sign "$SIGNING_IDENTITY" "$app_bundle"

  codesign --verify --deep --strict --verbose=2 "$app_bundle"
}

verify_app_launches() {
  local app_bundle="$1"
  local launch_log
  local pid
  local exit_code

  launch_log="$(mktemp "${TMPDIR:-/tmp}/macstorageatlas-launch.XXXXXX")"

  printf 'Running app launch smoke test...\n'
  set +e
  (
    cd "$app_bundle/Contents/MacOS" &&
      "./$EXECUTABLE_NAME"
  ) >"$launch_log" 2>&1 &
  pid=$!

  sleep "$LAUNCH_SMOKE_TEST_SECONDS"

  if kill -0 "$pid" >/dev/null 2>&1; then
    kill "$pid" >/dev/null 2>&1
    wait "$pid" >/dev/null 2>&1
    exit_code=0
  else
    wait "$pid"
    exit_code=$?
  fi
  set -e

  if [ "$exit_code" -eq 0 ]; then
    rm -f "$launch_log"
    return
  fi

  printf 'App launch smoke test failed with exit code %s.\n' "$exit_code" >&2
  if [ -s "$launch_log" ]; then
    sed 's/^/  /' "$launch_log" >&2
  fi
  rm -f "$launch_log"
  exit 1
}

create_dmg() {
  local app_bundle="$1"
  local dmg_dir="$2"
  local dmg_name="$3"

  printf 'Creating DMG content...\n'
  mkdir "$dmg_dir"
  cp -R "$app_bundle" "$dmg_dir/"
  ln -s /Applications "$dmg_dir/Applications"

  printf 'Creating DMG...\n'
  hdiutil create \
    -volname "$APP_NAME" \
    -srcfolder "$dmg_dir" \
    -ov \
    -format UDZO \
    "$dmg_name"
}

sign_dmg() {
  local dmg_name="$1"

  printf 'Signing DMG...\n'
  codesign --force --timestamp --sign "$SIGNING_IDENTITY" "$dmg_name"
  codesign --verify --verbose=2 "$dmg_name"
}

notarize_dmg() {
  local dmg_name="$1"

  printf 'Submitting DMG for notarization...\n'
  if ! xcrun notarytool submit "$dmg_name" --keychain-profile "$NOTARY_PROFILE" --wait; then
    printf 'Notarization failed. Run this command for Apple details:\n' >&2
    printf 'xcrun notarytool log <submission-id> --keychain-profile "%s"\n' "$NOTARY_PROFILE" >&2
    exit 1
  fi

  printf 'Stapling notarization ticket...\n'
  xcrun stapler staple "$dmg_name"
  xcrun stapler validate "$dmg_name"
}

verify_release_artifact() {
  local app_bundle="$1"
  local dmg_name="$2"

  printf 'Verifying signed release artifact...\n'
  codesign --verify --deep --strict --verbose=2 "$app_bundle"
  codesign --verify --verbose=2 "$dmg_name"
  spctl --assess --type execute --verbose=2 "$app_bundle"
  verify_app_launches "$app_bundle"
  hdiutil verify "$dmg_name"
  xcrun stapler validate "$dmg_name"
  spctl --assess --type open --context context:primary-signature --verbose=2 "$dmg_name"
}

write_checksum() {
  local dmg_name="$1"

  printf 'Writing SHA-256 checksum...\n'
  shasum -a 256 "$dmg_name" > "$dmg_name.sha256"
}

build_one() {
  local runtime="$1"
  local work_dir
  local app_bundle
  local dmg_dir
  local dmg_name

  work_dir="$(mktemp -d "${TMPDIR:-/tmp}/macstorageatlas.XXXXXX")"
  app_bundle="$work_dir/$APP_NAME.app"
  dmg_dir="$work_dir/dmg-content"
  dmg_name="$(dmg_name_for "$runtime")"

  printf '\n=== Building for %s ===\n' "$runtime"

  create_app_bundle "$runtime" "$app_bundle"

  if [ "$MODE" = "release" ]; then
    sign_app_bundle "$app_bundle"
  fi

  create_dmg "$app_bundle" "$dmg_dir" "$dmg_name"

  if [ "$MODE" = "release" ]; then
    sign_dmg "$dmg_name"
    notarize_dmg "$dmg_name"
    verify_release_artifact "$app_bundle" "$dmg_name"
    write_checksum "$dmg_name"
    printf 'Release artifact ready: %s\n' "$dmg_name"
    printf 'Checksum ready: %s.sha256\n' "$dmg_name"
  else
    printf 'Done: %s\n' "$dmg_name"
  fi
}

parse_args "$@"

if [ "$DRY_RUN" = "true" ]; then
  print_plan
  exit 0
fi

require_command dotnet
require_command hdiutil

if [ "$MODE" = "release" ]; then
  check_release_prerequisites
fi

for runtime in "${RUNTIMES[@]}"; do
  build_one "$runtime"
done
