#!/usr/bin/env bash
# Builds KeyboardLanguageFix-Setup.exe — the single file to share with people.
#
#   ./windows/build/build-setup.sh            # x64
#   ./windows/build/build-setup.sh arm64      # Windows on ARM
#
# Needs the .NET 8 SDK and NSIS (makensis). Both cross-compile, so this runs on
# Linux and macOS as well as Windows:
#
#   Windows   winget install NSIS.NSIS
#   Debian    apt install nsis
#   macOS     brew install makensis
set -euo pipefail

architecture="${1:-x64}"
case "$architecture" in
  x64|arm64) ;;
  *) echo "Unknown architecture '$architecture'. Use x64 or arm64." >&2; exit 1 ;;
esac

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
project="$root/windows/src/KeyboardLanguageFix.App/KeyboardLanguageFix.App.csproj"
staging="$root/windows/dist/setup-stage-$architecture"
output_dir="$root/windows/dist"

for tool in dotnet makensis; do
  if ! command -v "$tool" >/dev/null 2>&1; then
    echo "'$tool' was not found. See the comment at the top of this script." >&2
    exit 1
  fi
done

# The version in the csproj is the single source of truth for the installer,
# the About box and the executable's file properties.
version="$(grep -oP '(?<=<Version>)[^<]+' "$project" | head -1)"
if [[ ! "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
  echo "Could not read a three-part <Version> from $project (found '$version')." >&2
  exit 1
fi

echo "Keyboard Language Fix $version — building the installer for win-$architecture"
echo

rm -rf "$staging"
mkdir -p "$staging" "$output_dir"

echo "Publishing the app..."
# A folder build, not a single file: NSIS compresses loose .NET assemblies far
# better, and the installed app starts faster with nothing to unpack at run time.
dotnet publish "$project" \
  -c Release \
  -r "win-$architecture" \
  --self-contained true \
  -p:DebugType=none \
  -p:PublishDocumentationFiles=false \
  -p:PublishReferencesDocumentationFiles=false \
  -o "$staging" \
  --nologo \
  -v quiet

# Files the installer places next to the app.
cp "$root/LICENSE" "$staging/LICENSE.txt"
cp "$root/windows/installer/README.txt" "$staging/README.txt"
cp "$root/windows/src/KeyboardLanguageFix.App/Assets/app.ico" "$staging/setup.ico"

output="$output_dir/KeyboardLanguageFix-$version-$architecture-Setup.exe"

echo "Packing the installer..."
makensis -V2 \
  "-DAPP_VERSION=$version" \
  "-DSOURCE_DIR=$staging" \
  "-DOUTPUT_FILE=$output" \
  "$root/windows/installer/KeyboardLanguageFix.nsi"

rm -rf "$staging"

echo
"$root/windows/installer/verify-setup.sh" "$output"

size="$(du -h "$output" | cut -f1)"
echo
echo "Built $output ($size)"
echo
echo "This is the file to share. People download it, double-click it, and the app"
echo "installs for their user account only — no administrator rights needed."
