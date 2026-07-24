#!/bin/bash
set -e


APP_NAME="MacStorageAtlas"
BUNDLE_ID="de.ltsoftware.macstorageatlas"
VERSION="0.0.2"
TARGET_FRAMEWORK="net10.0"

PROJECT="src/MacStorageAtlas.App"
EXECUTABLE_NAME="MacStorageAtlas.App"
ICON_SOURCE="$PROJECT/Assets/MacStorageAtlas.icns"

case "${1:-arm64}" in
  arm64) RUNTIMES=("osx-arm64") ;;
  x64)   RUNTIMES=("osx-x64") ;;
  both)  RUNTIMES=("osx-arm64" "osx-x64") ;;
  *)
    echo "Unknown target '$1'. Use: arm64 | x64 | both"
    exit 1
    ;;
esac

build_one() {
  local runtime="$1"
  local publish_dir="$PROJECT/bin/Release/$TARGET_FRAMEWORK/$runtime/publish"
  local app_bundle="$APP_NAME.app"
  local dmg_dir="dmg-content"

  local dmg_name
  if [ "${#RUNTIMES[@]}" -gt 1 ]; then
    dmg_name="$APP_NAME-$runtime.dmg"
  else
    dmg_name="$APP_NAME.dmg"
  fi

  echo ""
  echo "=== Building for $runtime ==="

  echo "Publishing app..."
  dotnet publish "$PROJECT" -c Release -r "$runtime" --self-contained true

  echo "Creating .app bundle..."
  rm -rf "$app_bundle"
  mkdir -p "$app_bundle/Contents/MacOS"
  mkdir -p "$app_bundle/Contents/Resources"

  cp -R "$publish_dir/"* "$app_bundle/Contents/MacOS/"

  if [ -f "$ICON_SOURCE" ]; then
    cp "$ICON_SOURCE" "$app_bundle/Contents/Resources/AppIcon.icns"
  else
    echo "Warning: icon not found at $ICON_SOURCE, bundling without icon."
  fi

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

  chmod +x "$app_bundle/Contents/MacOS/$EXECUTABLE_NAME"

  echo "Creating DMG content..."
  rm -rf "$dmg_dir"
  mkdir "$dmg_dir"

  cp -R "$app_bundle" "$dmg_dir/"
  ln -s /Applications "$dmg_dir/Applications"

  echo "Creating DMG..."
  rm -f "$dmg_name"

  hdiutil create \
    -volname "$APP_NAME" \
    -srcfolder "$dmg_dir" \
    -ov \
    -format UDZO \
    "$dmg_name"

  rm -rf "$app_bundle" "$dmg_dir"

  echo "Done: $dmg_name"
}

for runtime in "${RUNTIMES[@]}"; do
  build_one "$runtime"
done
