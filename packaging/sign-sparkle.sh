#!/bin/bash
set -euo pipefail

[ "$#" -eq 2 ] || { printf 'Usage: sign-sparkle.sh <app-bundle> <identity>\n' >&2; exit 1; }
FRAMEWORK="$1/Contents/Frameworks/Sparkle.framework"
SIGNING_IDENTITY="$2"
for nested in XPCServices/Downloader.xpc XPCServices/Installer.xpc Updater.app Autoupdate; do
  codesign --force --timestamp --options runtime --sign "$SIGNING_IDENTITY" "$FRAMEWORK/Versions/B/$nested"
done
codesign --force --timestamp --options runtime --sign "$SIGNING_IDENTITY" "$FRAMEWORK"
codesign --verify --deep --strict "$FRAMEWORK"
