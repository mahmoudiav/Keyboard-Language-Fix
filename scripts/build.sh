#!/usr/bin/env bash
# Package the extension for each store.
#
#   ./scripts/build.sh
#
# Produces dist/keyboard-language-fix-chrome.zip (Chrome, Edge, Brave, Opera)
# and dist/keyboard-language-fix-firefox.zip.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$root"

version="$(node -p "require('./manifest.json').version")"
dist="$root/dist"
rm -rf "$dist"
mkdir -p "$dist"

common=(icons _locales src)

package() {
  local target="$1" manifest="$2"
  local stage="$dist/stage-$target"
  mkdir -p "$stage"
  cp -r "${common[@]}" "$stage/"
  cp "$manifest" "$stage/manifest.json"
  (cd "$stage" && zip -qr "$dist/keyboard-language-fix-$target-$version.zip" .)
  rm -rf "$stage"
  echo "dist/keyboard-language-fix-$target-$version.zip"
}

package chrome manifest.json
package firefox manifest.firefox.json
