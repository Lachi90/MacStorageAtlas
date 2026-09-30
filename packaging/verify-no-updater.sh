#!/bin/bash
set -euo pipefail

[ "$#" -eq 1 ] || { printf 'Usage: verify-no-updater.sh <app-bundle>\n' >&2; exit 1; }
APP_BUNDLE="$1"
[ -d "$APP_BUNDLE/Contents" ] || exit 1
if find "$APP_BUNDLE/Contents" \( -iname '*sparkle*' -o -iname '*updater*' -o -name 'Autoupdate' \) -print | grep -q .; then
  printf 'Unexpected updater content in Store or Development bundle.\n' >&2
  exit 1
fi
while IFS= read -r plist; do
  if /usr/libexec/PlistBuddy -c Print "$plist" | grep -Eq 'SUFeedURL|SUPublicEDKey|SUEnable|SUAllow|SUScheduled|SUSend|SURequire|SUVerifyUpdate'; then
    printf 'Unexpected updater metadata in %s\n' "$plist" >&2
    exit 1
  fi
done < <(find "$APP_BUNDLE/Contents" -name Info.plist -type f)
while IFS= read -r binary; do
  if otool -L "$binary" 2>/dev/null | grep -q 'Sparkle.framework'; then
    printf 'Unexpected Sparkle linkage in %s\n' "$binary" >&2
    exit 1
  fi
done < <(find "$APP_BUNDLE/Contents" -type f \( -name '*.dylib' -o -name AtlasStorageAnalyzer \))
while IFS= read -r assembly; do
  if strings "$assembly" | grep -E 'MacAppUpdater|DirectAppUpdateService|msa_updater_start|libMacStorageAtlasUpdater' >/dev/null; then
    printf 'Unexpected managed updater integration in %s\n' "$assembly" >&2
    exit 1
  fi
done < <(find "$APP_BUNDLE/Contents" -type f \( -name MacStorageAtlas.App.dll -o -name MacStorageAtlas.Platform.Mac.dll \))
