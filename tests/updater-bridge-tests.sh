#!/bin/bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
if [ "$(uname -s)" != Darwin ]; then
  printf 'Native updater tests require macOS.\n'
  exit 0
fi
"$ROOT_DIR/packaging/prepare-sparkle.sh"
SPARKLE_DIR="$ROOT_DIR/packaging/.cache/sparkle/2.9.6"
TEST_DIR="$(mktemp -d "${TMPDIR:-/tmp}/atlas-updater-bridge-tests.XXXXXX")"
trap 'rm -rf "$TEST_DIR"' EXIT
for architecture in arm64 x86_64; do
  clang -arch "$architecture" -mmacosx-version-min=11.0 -fobjc-arc \
    -F "$SPARKLE_DIR" -framework Cocoa -framework Sparkle \
    -Wl,-rpath,"$SPARKLE_DIR" \
    -o "$TEST_DIR/tests-$architecture" "$ROOT_DIR/tests/updater-bridge-tests.m"
  arch -"$architecture" "$TEST_DIR/tests-$architecture"
done
