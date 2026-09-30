#!/bin/bash
set -euo pipefail

[ "$#" -eq 2 ] || { printf 'Usage: embed-sparkle.sh <app-bundle> <arm64|x86_64>\n' >&2; exit 1; }
APP_BUNDLE="$1"
ARCHITECTURE="$2"
case "$ARCHITECTURE" in arm64|x86_64) ;; *) exit 1 ;; esac
PACKAGING_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SPARKLE_DIR="$PACKAGING_DIR/.cache/sparkle/2.9.6"
[ -d "$APP_BUNDLE/Contents/MacOS" ] || exit 1
"$PACKAGING_DIR/prepare-sparkle.sh"
mkdir -p "$APP_BUNDLE/Contents/Frameworks" "$APP_BUNDLE/Contents/Resources"
cp -R "$SPARKLE_DIR/Sparkle.framework" "$APP_BUNDLE/Contents/Frameworks/"
cp "$SPARKLE_DIR/LICENSE" "$APP_BUNDLE/Contents/Resources/Sparkle-LICENSE.txt"
clang -arch "$ARCHITECTURE" -mmacosx-version-min=11.0 -fobjc-arc -dynamiclib \
  -F "$SPARKLE_DIR" -framework Cocoa -framework Sparkle \
  -Wl,-rpath,@loader_path/../Frameworks \
  -install_name @rpath/libMacStorageAtlasUpdater.dylib \
  -o "$APP_BUNDLE/Contents/MacOS/libMacStorageAtlasUpdater.dylib" \
  "$PACKAGING_DIR/updater-bridge.m"
