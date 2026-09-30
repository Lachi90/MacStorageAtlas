#!/bin/bash
set -euo pipefail

SPARKLE_VERSION="2.9.6"
SPARKLE_SHA256="52bf9e88cdd972fc0c81501377a880e90d47031bd8ca5462488f843e2609e192"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DESTINATION="${1:-$SCRIPT_DIR/.cache/sparkle/$SPARKLE_VERSION}"

[ "$#" -le 1 ] || { printf 'Usage: %s [destination]\n' "$0" >&2; exit 1; }
mkdir -p "$DESTINATION"
ARCHIVE="$DESTINATION/Sparkle-$SPARKLE_VERSION.tar.xz"
if [ ! -f "$ARCHIVE" ]; then
  curl --fail --location --retry 2 --output "$ARCHIVE" \
    "https://github.com/sparkle-project/Sparkle/releases/download/$SPARKLE_VERSION/Sparkle-$SPARKLE_VERSION.tar.xz"
fi
ACTUAL_SHA256="$(shasum -a 256 "$ARCHIVE" | cut -d ' ' -f 1)"
[ "$ACTUAL_SHA256" = "$SPARKLE_SHA256" ] || { printf 'Sparkle archive checksum mismatch.\n' >&2; exit 1; }
tar -xf "$ARCHIVE" -C "$DESTINATION"
codesign --verify --deep --strict "$DESTINATION/Sparkle.framework"
ARCHITECTURES="$(lipo -archs "$DESTINATION/Sparkle.framework/Sparkle")"
for architecture in arm64 x86_64; do
  printf '%s\n' "$ARCHITECTURES" | grep -qw "$architecture" || { printf 'Missing Sparkle architecture: %s\n' "$architecture" >&2; exit 1; }
done
printf 'Verified Sparkle %s at %s\n' "$SPARKLE_VERSION" "$DESTINATION"
