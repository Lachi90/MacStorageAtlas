#!/bin/bash
set -euo pipefail

[ "$#" -eq 3 ] || { printf 'Usage: generate-update-feed.sh <verified-dmg> <runtime> <version>\n' >&2; exit 1; }
DMG="$1"
RUNTIME="$2"
VERSION="$3"
case "$RUNTIME" in osx-arm64) ARCHITECTURE=arm64 ;; osx-x64) ARCHITECTURE=x64 ;; *) exit 1 ;; esac
PACKAGING_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TOOLS="$PACKAGING_DIR/.cache/sparkle/2.9.6/bin"
DESTINATION="$PACKAGING_DIR/../update-output/$ARCHITECTURE"
ACCOUNT="${SPARKLE_KEY_ACCOUNT:-de.ltsoftware.macstorageatlas}"
export UPDATE_VERSION="$VERSION"
python3 "$PACKAGING_DIR/update-metadata.py" config
[ -f "$DMG" ] && [ -f "$DMG.sha256" ] || exit 1
shasum -a 256 -c "$DMG.sha256"
WORK_DIR="$(mktemp -d "${TMPDIR:-/tmp}/macstorageatlas-feed.XXXXXX")"
trap 'rm -rf "$WORK_DIR"' EXIT
if [ -f "$DESTINATION/appcast.xml" ]; then
  python3 "$PACKAGING_DIR/update-metadata.py" previous "$DESTINATION/appcast.xml" "$RUNTIME" "$VERSION"
  cp "$DESTINATION/appcast.xml" "$WORK_DIR/"
fi
cp "$DMG" "$WORK_DIR/"
"$TOOLS/generate_appcast" --account "$ACCOUNT" --maximum-deltas 0 \
  --download-url-prefix "$SPARKLE_DOWNLOAD_BASE_URL/" --disable-signing-warning "$WORK_DIR"
python3 "$PACKAGING_DIR/update-metadata.py" feed "$WORK_DIR/appcast.xml" "$RUNTIME" "$VERSION"
"$TOOLS/sign_update" --account "$ACCOUNT" --verify "$WORK_DIR/appcast.xml"
mkdir -p "$DESTINATION"
cp "$WORK_DIR/appcast.xml" "$DESTINATION/appcast.xml"
printf 'Verified local feed: %s/appcast.xml\n' "$DESTINATION"
