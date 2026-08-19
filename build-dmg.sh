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
APPSTORE_ENTITLEMENTS_SOURCE="$PROJECT/MacStorageAtlas.AppStore.entitlements"
APPSTORE_INHERIT_ENTITLEMENTS_SOURCE="$PROJECT/MacStorageAtlas.AppStore.Inherit.entitlements"
APPSTORE_DEFAULT_PROFILE="$HOME/.macstorageatlas-apple-certificates/MacStorageAtlas_Mac_App_Store.provisionprofile"
LAUNCH_SMOKE_TEST_SECONDS=5

MODE="unsigned"
DRY_RUN="false"
APPSTORE_UNIVERSAL="false"
VERSION="$DEFAULT_VERSION"
SIGNING_IDENTITY=""
INSTALLER_SIGNING_IDENTITY=""
NOTARY_PROFILE=""
PROVISIONING_PROFILE=""

usage() {
  printf '%s\n' "Usage:"
  printf '%s\n' "  ./build-dmg.sh [--dry-run] [arm64|x64|both]"
  printf '%s\n' "  ./build-dmg.sh release [--dry-run] <arm64|x64|both> <version> <signing-identity> <notary-profile>"
  printf '%s\n' "  ./build-dmg.sh appstore [--dry-run] <arm64|x64|both> <version> <app-signing-identity> <installer-signing-identity> [provisioning-profile]"
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

  if [ "${1:-}" = "appstore" ]; then
    MODE="appstore"
    shift

    if [ "${1:-}" = "--dry-run" ]; then
      DRY_RUN="true"
      shift
    fi

    if [ "$#" -lt 4 ] || [ "$#" -gt 5 ]; then
      usage >&2
      exit 1
    fi

    TARGET="$1"
    VERSION="$2"
    SIGNING_IDENTITY="$3"
    INSTALLER_SIGNING_IDENTITY="$4"
    PROVISIONING_PROFILE="${5:-$APPSTORE_DEFAULT_PROFILE}"

    [ -n "$VERSION" ] || fail "App Store version is required."
    [ "${VERSION#*/}" = "$VERSION" ] || fail "App Store version must not contain '/'."
    [ -n "$SIGNING_IDENTITY" ] || fail "App Store signing identity is required."
    [ -n "$INSTALLER_SIGNING_IDENTITY" ] || fail "App Store installer signing identity is required."
    [ -n "$PROVISIONING_PROFILE" ] || fail "App Store provisioning profile is required."
    parse_target "$TARGET"
    if [ "$TARGET" = "both" ]; then
      APPSTORE_UNIVERSAL="true"
    fi
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

pkg_name_for() {
  local runtime="$1"

  printf '%s-%s-%s-appstore.pkg\n' "$APP_NAME" "$VERSION" "$runtime"
}

print_plan() {
  local runtime

  printf 'mode=%s\n' "$MODE"
  printf 'version=%s\n' "$VERSION"

  for runtime in "${RUNTIMES[@]}"; do
    if [ "$MODE" = "appstore" ]; then
      if [ "$APPSTORE_UNIVERSAL" = "true" ]; then
        printf 'runtime=universal artifact=%s\n' "$(pkg_name_for "universal")"
        break
      else
        printf 'runtime=%s artifact=%s\n' "$runtime" "$(pkg_name_for "$runtime")"
      fi
    else
      printf 'runtime=%s artifact=%s\n' "$runtime" "$(dmg_name_for "$runtime")"
    fi
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

check_appstore_prerequisites() {
  require_command codesign
  require_command productbuild
  require_command pkgutil
  require_command security
  require_command xattr
  if [ "$APPSTORE_UNIVERSAL" = "true" ]; then
    require_command lipo
  fi

  security find-identity -v -p codesigning | grep -F -- "$SIGNING_IDENTITY" >/dev/null ||
    fail "App Store signing identity '$SIGNING_IDENTITY' was not found in the local keychain."

  security find-certificate -a -c "$INSTALLER_SIGNING_IDENTITY" >/dev/null ||
    fail "App Store installer signing identity '$INSTALLER_SIGNING_IDENTITY' was not found in the local keychain."

  [ -f "$PROVISIONING_PROFILE" ] ||
    fail "App Store provisioning profile was not found at '$PROVISIONING_PROFILE'."

  [ -f "$APPSTORE_ENTITLEMENTS_SOURCE" ] ||
    fail "App Store entitlements file was not found at '$APPSTORE_ENTITLEMENTS_SOURCE'."

  [ -f "$APPSTORE_INHERIT_ENTITLEMENTS_SOURCE" ] ||
    fail "App Store inherited entitlements file was not found at '$APPSTORE_INHERIT_ENTITLEMENTS_SOURCE'."

  validate_appstore_provisioning_profile
}

validate_appstore_provisioning_profile() {
  local profile_plist
  local profile_platforms
  local team_identifier
  local expected_app_identifier
  local app_identifier

  profile_plist="$(mktemp "${TMPDIR:-/tmp}/macstorageatlas-profile.XXXXXX")"

  if ! security cms -D -i "$PROVISIONING_PROFILE" > "$profile_plist"; then
    fail "App Store provisioning profile could not be decoded. Download a fresh macOS App Store provisioning profile from the Apple Developer portal."
  fi

  profile_platforms="$(/usr/libexec/PlistBuddy -c "Print :Platform" "$profile_plist" 2>/dev/null || true)"
  if ! printf '%s\n' "$profile_platforms" | grep -Eq 'OSX|macOS'; then
    fail "App Store provisioning profile '$PROVISIONING_PROFILE' is not for macOS. Create a macOS App Store provisioning profile for '$BUNDLE_ID'."
  fi

  team_identifier="$(/usr/libexec/PlistBuddy -c "Print :TeamIdentifier:0" "$profile_plist" 2>/dev/null || true)"
  expected_app_identifier="$team_identifier.$BUNDLE_ID"
  app_identifier="$(/usr/libexec/PlistBuddy -c "Print :Entitlements:application-identifier" "$profile_plist" 2>/dev/null || true)"

  if [ -z "$app_identifier" ]; then
    app_identifier="$(/usr/libexec/PlistBuddy -c "Print :Entitlements:com.apple.application-identifier" "$profile_plist" 2>/dev/null || true)"
  fi

  if [ "$app_identifier" != "$expected_app_identifier" ]; then
    fail "App Store provisioning profile '$PROVISIONING_PROFILE' is for '$app_identifier', expected '$expected_app_identifier'."
  fi

  rm -f "$profile_plist"
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
    <key>LSApplicationCategoryType</key>
    <string>public.app-category.utilities</string>
    <key>NSHighResolutionCapable</key>
    <true/>
</dict>
</plist>
EOF
}

copy_publish_output() {
  local publish_dir="$1"
  local bundle_macos_dir="$2"

  if [ "$MODE" != "unsigned" ]; then
    find "$publish_dir" -maxdepth 1 -type f ! -name "*.pdb" -exec cp {} "$bundle_macos_dir/" \;
  else
    cp -R "$publish_dir/"* "$bundle_macos_dir/"
  fi
}

is_macho_file() {
  file -b "$1" | grep -q "Mach-O"
}

is_universal_macho_file() {
  lipo "$1" -verify_arch arm64 x86_64 >/dev/null 2>&1
}

merge_universal_publish_output() {
  local arm64_publish_dir="$1"
  local x64_publish_dir="$2"
  local bundle_macos_dir="$3"
  local x64_file
  local relative_path
  local arm64_file
  local target_file

  while IFS= read -r x64_file; do
    relative_path="${x64_file#"$x64_publish_dir/"}"
    arm64_file="$arm64_publish_dir/$relative_path"
    target_file="$bundle_macos_dir/$relative_path"

    if [ -f "$arm64_file" ] && is_macho_file "$arm64_file" && is_macho_file "$x64_file"; then
      if is_universal_macho_file "$arm64_file"; then
        cp "$arm64_file" "$target_file"
      elif is_universal_macho_file "$x64_file"; then
        cp "$x64_file" "$target_file"
      else
        lipo -create "$arm64_file" "$x64_file" -output "$target_file"
      fi
    elif [ ! -f "$arm64_file" ] && [ "${relative_path##*.}" != "pdb" ]; then
      cp "$x64_file" "$target_file"
    fi
  done < <(find "$x64_publish_dir" -maxdepth 1 -type f | sort)
}

normalize_app_bundle_permissions() {
  local app_bundle="$1"

  find "$app_bundle" -type d -exec chmod 755 {} \;
  find "$app_bundle" -type f -exec chmod u+rw,go+r {} \;
  chmod +x "$app_bundle/Contents/MacOS/$EXECUTABLE_NAME"
  find "$app_bundle/Contents/MacOS" -type f -perm -111 -exec chmod a+rx {} \;
}

clear_app_bundle_extended_attributes() {
  local app_bundle="$1"

  xattr -cr "$app_bundle"
}

create_appstore_signing_entitlements() {
  local output_path="$1"
  local profile_plist
  local app_identifier
  local team_identifier
  local keychain_group
  local index

  profile_plist="$(mktemp "${TMPDIR:-/tmp}/macstorageatlas-profile.XXXXXX")"
  security cms -D -i "$PROVISIONING_PROFILE" > "$profile_plist"
  cp "$APPSTORE_ENTITLEMENTS_SOURCE" "$output_path"

  app_identifier="$(/usr/libexec/PlistBuddy -c "Print :Entitlements:com.apple.application-identifier" "$profile_plist" 2>/dev/null || true)"
  if [ -z "$app_identifier" ]; then
    app_identifier="$(/usr/libexec/PlistBuddy -c "Print :Entitlements:application-identifier" "$profile_plist" 2>/dev/null || true)"
  fi
  team_identifier="$(/usr/libexec/PlistBuddy -c "Print :Entitlements:com.apple.developer.team-identifier" "$profile_plist" 2>/dev/null || true)"

  /usr/libexec/PlistBuddy -c "Delete :com.apple.application-identifier" "$output_path" 2>/dev/null || true
  /usr/libexec/PlistBuddy -c "Add :com.apple.application-identifier string $app_identifier" "$output_path"
  /usr/libexec/PlistBuddy -c "Delete :com.apple.developer.team-identifier" "$output_path" 2>/dev/null || true
  /usr/libexec/PlistBuddy -c "Add :com.apple.developer.team-identifier string $team_identifier" "$output_path"
  /usr/libexec/PlistBuddy -c "Delete :keychain-access-groups" "$output_path" 2>/dev/null || true
  /usr/libexec/PlistBuddy -c "Add :keychain-access-groups array" "$output_path"

  index=0
  while keychain_group="$(/usr/libexec/PlistBuddy -c "Print :Entitlements:keychain-access-groups:$index" "$profile_plist" 2>/dev/null)"; do
    /usr/libexec/PlistBuddy -c "Add :keychain-access-groups:$index string $keychain_group" "$output_path"
    index=$((index + 1))
  done

  rm -f "$profile_plist"
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
  if [ "$MODE" = "appstore" ]; then
    cp "$PROVISIONING_PROFILE" "$app_bundle/Contents/embedded.provisionprofile"
  fi
  clear_app_bundle_extended_attributes "$app_bundle"
  normalize_app_bundle_permissions "$app_bundle"
}

create_universal_app_bundle() {
  local app_bundle="$1"
  local arm64_publish_dir="$PROJECT/bin/Release/$TARGET_FRAMEWORK/osx-arm64/publish"
  local x64_publish_dir="$PROJECT/bin/Release/$TARGET_FRAMEWORK/osx-x64/publish"

  printf 'Publishing arm64 app...\n'
  dotnet publish "$PROJECT" -c Release -r osx-arm64 --self-contained true

  printf 'Publishing x64 app...\n'
  dotnet publish "$PROJECT" -c Release -r osx-x64 --self-contained true

  printf 'Creating universal .app bundle...\n'
  mkdir -p "$app_bundle/Contents/MacOS"
  mkdir -p "$app_bundle/Contents/Resources"
  copy_publish_output "$arm64_publish_dir" "$app_bundle/Contents/MacOS"
  merge_universal_publish_output "$arm64_publish_dir" "$x64_publish_dir" "$app_bundle/Contents/MacOS"

  if [ -f "$ICON_SOURCE" ]; then
    cp "$ICON_SOURCE" "$app_bundle/Contents/Resources/AppIcon.icns"
  else
    printf 'Warning: icon not found at %s, bundling without icon.\n' "$ICON_SOURCE"
  fi

  create_info_plist "$app_bundle"
  cp "$PROVISIONING_PROFILE" "$app_bundle/Contents/embedded.provisionprofile"
  clear_app_bundle_extended_attributes "$app_bundle"
  normalize_app_bundle_permissions "$app_bundle"
  lipo "$app_bundle/Contents/MacOS/$EXECUTABLE_NAME" -verify_arch arm64 x86_64
}

sign_app_bundle() {
  local app_bundle="$1"
  local main_executable="$app_bundle/Contents/MacOS/$EXECUTABLE_NAME"
  local entitlements_source="$ENTITLEMENTS_SOURCE"

  if [ "$MODE" = "appstore" ]; then
    entitlements_source="$(mktemp "${TMPDIR:-/tmp}/macstorageatlas-entitlements.XXXXXX")"
    create_appstore_signing_entitlements "$entitlements_source"
  fi

  printf 'Signing nested app content...\n'
  while IFS= read -r file; do
    if [ "$MODE" = "appstore" ]; then
      codesign --force --timestamp --options runtime --entitlements "$APPSTORE_INHERIT_ENTITLEMENTS_SOURCE" --sign "$SIGNING_IDENTITY" "$file"
    else
      codesign --force --timestamp --options runtime --sign "$SIGNING_IDENTITY" "$file"
    fi
  done < <(find "$app_bundle/Contents/MacOS" -type f ! -name "$EXECUTABLE_NAME" | sort)

  printf 'Signing app executable...\n'
  codesign --force --timestamp --options runtime --entitlements "$entitlements_source" --sign "$SIGNING_IDENTITY" "$main_executable"

  printf 'Signing app bundle...\n'
  codesign --force --timestamp --options runtime --entitlements "$entitlements_source" --sign "$SIGNING_IDENTITY" "$app_bundle"

  codesign --verify --deep --strict --verbose=2 "$app_bundle"

  if [ "$MODE" = "appstore" ]; then
    rm -f "$entitlements_source"
  fi
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

create_appstore_pkg() {
  local app_bundle="$1"
  local pkg_name="$2"

  printf 'Creating App Store package...\n'
  productbuild \
    --sign "$INSTALLER_SIGNING_IDENTITY" \
    --component "$app_bundle" /Applications \
    --product "$app_bundle/Contents/Info.plist" \
    "$pkg_name"
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

verify_appstore_artifact() {
  local app_bundle="$1"
  local pkg_name="$2"

  printf 'Verifying App Store artifact...\n'
  codesign --verify --deep --strict --verbose=2 "$app_bundle"
  codesign --display --entitlements :- "$app_bundle" >/dev/null
  if [ "$APPSTORE_UNIVERSAL" = "true" ]; then
    lipo "$app_bundle/Contents/MacOS/$EXECUTABLE_NAME" -verify_arch arm64 x86_64
  fi
  pkgutil --check-signature "$pkg_name"
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
  local pkg_name

  work_dir="$(mktemp -d "${TMPDIR:-/tmp}/macstorageatlas.XXXXXX")"
  app_bundle="$work_dir/$APP_NAME.app"
  dmg_dir="$work_dir/dmg-content"
  dmg_name="$(dmg_name_for "$runtime")"
  pkg_name="$(pkg_name_for "$runtime")"

  printf '\n=== Building for %s ===\n' "$runtime"

  if [ "$MODE" = "appstore" ] && [ "$APPSTORE_UNIVERSAL" = "true" ]; then
    create_universal_app_bundle "$app_bundle"
  else
    create_app_bundle "$runtime" "$app_bundle"
  fi

  if [ "$MODE" = "release" ] || [ "$MODE" = "appstore" ]; then
    sign_app_bundle "$app_bundle"
  fi

  if [ "$MODE" = "appstore" ]; then
    create_appstore_pkg "$app_bundle" "$pkg_name"
    verify_appstore_artifact "$app_bundle" "$pkg_name"
    printf 'App Store artifact ready: %s\n' "$pkg_name"
    return
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
elif [ "$MODE" = "appstore" ]; then
  check_appstore_prerequisites
fi

for runtime in "${RUNTIMES[@]}"; do
  if [ "$MODE" = "appstore" ] && [ "$APPSTORE_UNIVERSAL" = "true" ]; then
    build_one "universal"
    break
  fi
  build_one "$runtime"
done
