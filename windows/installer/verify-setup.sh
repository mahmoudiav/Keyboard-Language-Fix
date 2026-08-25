#!/usr/bin/env bash
# Checks a built Setup.exe before it goes anywhere near a person.
#
#   ./windows/installer/verify-setup.sh windows/dist/KeyboardLanguageFix-1.0.0-x64-Setup.exe
#
# build-setup.sh runs this automatically. It needs 7z to look inside the
# installer; without it the payload checks are skipped and the script says so.
set -uo pipefail

setup="${1:-}"
if [[ -z "$setup" || ! -f "$setup" ]]; then
  echo "Usage: $0 <path to Setup.exe>" >&2
  exit 2
fi

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
project="$root/windows/src/KeyboardLanguageFix.App/KeyboardLanguageFix.App.csproj"
expected_version="$(grep -oP '(?<=<Version>)[^<]+' "$project" | head -1)"

failures=0
check() {
  if [[ "$1" == "ok" ]]; then
    printf '  PASS  %s\n' "$2"
  else
    printf '  FAIL  %s\n' "$2"
    failures=$((failures + 1))
  fi
}

echo "Verifying $(basename "$setup")"

# --- the installer itself ----------------------------------------------------

description="$(file -b "$setup")"
[[ "$description" == *"PE32"* ]] && check ok "is a Windows executable" \
                                 || check no "is a Windows executable (got: $description)"
[[ "$description" == *"Nullsoft Installer"* ]] && check ok "is an NSIS installer" \
                                               || check no "is an NSIS installer"

# The version resource is what Windows shows in the file's Properties dialog and
# in the SmartScreen prompt, so it must match what was actually built.
version_fields="$(python3 - "$setup" <<'PY'
import sys
data = open(sys.argv[1], 'rb').read()
for key in ('ProductName', 'FileVersion', 'CompanyName'):
    encoded = key.encode('utf-16-le')
    index = data.find(encoded)
    if index < 0:
        print(f'{key}=')
        continue
    tail = data[index + len(encoded): index + len(encoded) + 200]
    print(f"{key}={tail.decode('utf-16-le', 'ignore').strip(chr(0)).split(chr(0))[0]}")
PY
)"

grep -q "^ProductName=Keyboard Language Fix$" <<<"$version_fields" \
  && check ok "product name in the version resource" \
  || check no "product name in the version resource"
grep -q "^FileVersion=$expected_version$" <<<"$version_fields" \
  && check ok "version resource says $expected_version" \
  || check no "version resource says $expected_version (got: $(grep '^FileVersion=' <<<"$version_fields"))"
grep -q "^CompanyName=Mahmoud SATALEH$" <<<"$version_fields" \
  && check ok "author in the version resource" \
  || check no "author in the version resource"

# --- the payload -------------------------------------------------------------

if ! command -v 7z >/dev/null 2>&1; then
  echo "  SKIP  payload checks (7z not installed)"
else
  listing="$(7z l "$setup" 2>/dev/null)"

  for entry in KeyboardLanguageFix.exe LICENSE.txt README.txt; do
    grep -qE "[ /]$entry\$" <<<"$listing" \
      && check ok "payload contains $entry" \
      || check no "payload contains $entry"
  done

  extracted="$(mktemp -d)"
  trap 'rm -rf "$extracted"' EXIT

  if 7z x -o"$extracted" "$setup" >/dev/null 2>&1; then
    app="$extracted/KeyboardLanguageFix.exe"

    app_description="$(file -b "$app" 2>/dev/null || echo missing)"
    [[ "$app_description" == *"PE32+"* && "$app_description" == *"x86-64"* || "$app_description" == *"Aarch64"* ]] \
      && check ok "the packaged app is a Windows binary" \
      || check no "the packaged app is a Windows binary (got: $app_description)"

    # Nine icon sizes are embedded by scripts/make-icons.py; losing them means
    # a blank icon in the tray and the taskbar.
    icons="$(python3 -c "print(open('$app','rb').read().count(b'\x89PNG\r\n\x1a\n'))" 2>/dev/null || echo 0)"
    [[ "$icons" -ge 9 ]] && check ok "the app keeps its icons ($icons found)" \
                         || check no "the app keeps its icons (found $icons, expected 9)"

    grep -q "Mahmoud SATALEH" "$extracted/README.txt" \
      && check ok "README.txt credits the author" \
      || check no "README.txt credits the author"
    grep -q "mahmoudiav@icloud.com" "$extracted/README.txt" \
      && check ok "README.txt carries the contact address" \
      || check no "README.txt carries the contact address"
    grep -qi "free" "$extracted/README.txt" \
      && check ok "README.txt says the app is free" \
      || check no "README.txt says the app is free"
    grep -q "MIT License" "$extracted/LICENSE.txt" \
      && check ok "LICENSE.txt is the MIT licence" \
      || check no "LICENSE.txt is the MIT licence"
  else
    check no "the payload could be extracted"
  fi
fi

echo
if [[ "$failures" -gt 0 ]]; then
  echo "$failures check(s) failed."
  exit 1
fi
echo "All installer checks passed."
