#!/usr/bin/env bash
# Codesigns, notarizes and staples the macOS player. Runs on a macOS runner only.
#
#   Tools/release/sign-macos.sh <in_zip> <out_zip>
#
#   in_zip   zip from package.sh holding SolarMajesty.app at its root
#   out_zip  signed, notarized, stapled zip (may be the same path)
#
# Environment (GitHub secrets):
#   APPLE_CERT_P12_BASE64  base64 of the "Developer ID Application" certificate + key (.p12)
#   APPLE_CERT_PASSWORD    its export password
#   APPLE_TEAM_ID          10-character team id
#   APPLE_ID               Apple ID email used for notarization
#   APPLE_APP_PASSWORD     app-specific password for that Apple ID (appleid.apple.com)
set -euo pipefail

if [ $# -ne 2 ]; then
  echo "usage: $0 <in_zip> <out_zip>" >&2
  exit 2
fi
if [ "$(uname -s)" != "Darwin" ]; then
  echo "sign-macos.sh needs macOS (codesign / notarytool)." >&2
  exit 2
fi

in_zip="$1"
out_zip="$2"
: "${APPLE_CERT_P12_BASE64:?APPLE_CERT_P12_BASE64 is not set}"
: "${APPLE_CERT_PASSWORD:?APPLE_CERT_PASSWORD is not set}"
: "${APPLE_TEAM_ID:?APPLE_TEAM_ID is not set}"
: "${APPLE_ID:?APPLE_ID is not set}"
: "${APPLE_APP_PASSWORD:?APPLE_APP_PASSWORD is not set}"

here="$(cd "$(dirname "$0")" && pwd)"
entitlements="$here/macos/SolarMajesty.entitlements"
work=$(mktemp -d)
keychain="$work/signing.keychain-db"
keychain_pass=$(uuidgen)

cleanup() {
  security delete-keychain "$keychain" >/dev/null 2>&1 || true
  rm -rf "$work"
}
trap cleanup EXIT

# --- Temporary keychain with the Developer ID identity --------------------------------------
printf '%s' "$APPLE_CERT_P12_BASE64" | base64 --decode >"$work/cert.p12"
security create-keychain -p "$keychain_pass" "$keychain"
security set-keychain-settings -lut 21600 "$keychain"
security unlock-keychain -p "$keychain_pass" "$keychain"
security import "$work/cert.p12" -k "$keychain" -P "$APPLE_CERT_PASSWORD" -T /usr/bin/codesign
security set-key-partition-list -S apple-tool:,apple:,codesign: -s -k "$keychain_pass" "$keychain" >/dev/null
# Put it on the search list so codesign finds the identity.
existing=$(security list-keychains -d user | sed -e 's/"//g' | xargs)
# shellcheck disable=SC2086 # word-splitting the existing keychain list is intended
security list-keychains -d user -s "$keychain" $existing

identity=$(security find-identity -v -p codesigning "$keychain" |
  awk -F'"' '/Developer ID Application/ {print $2; exit}')
if [ -z "$identity" ]; then
  echo "No 'Developer ID Application' identity in APPLE_CERT_P12_BASE64." >&2
  exit 1
fi
echo "Signing as: $identity"

# --- Unpack ------------------------------------------------------------------------------------
ditto -x -k "$in_zip" "$work/unpacked"
app=$(find "$work/unpacked" -maxdepth 1 -name "*.app" -type d | head -n 1)
if [ -z "$app" ]; then
  echo "no .app at the root of $in_zip" >&2
  exit 1
fi
xattr -cr "$app"
chmod +x "$app"/Contents/MacOS/*

# --- Sign inside-out: nested code first, then the bundle -------------------------------------
sign() {
  codesign --force --timestamp --options runtime \
    --entitlements "$entitlements" \
    --keychain "$keychain" \
    --sign "$identity" "$1"
}
# Deepest paths first, so a bundle is signed after everything inside it.
while IFS= read -r item; do
  sign "$item"
done < <(find "$app/Contents" \( -name "*.dylib" -o -name "*.so" -o -name "*.bundle" -o -name "*.framework" \) |
  awk '{ n = gsub("/", "/"); print n "\t" $0 }' | sort -rn | cut -f2-)
sign "$app"
codesign --verify --deep --strict --verbose=2 "$app"

# --- Notarize + staple ---------------------------------------------------------------------------
ditto -c -k --sequesterRsrc --keepParent "$app" "$work/notarize.zip"
result=$(xcrun notarytool submit "$work/notarize.zip" \
  --apple-id "$APPLE_ID" --team-id "$APPLE_TEAM_ID" --password "$APPLE_APP_PASSWORD" \
  --wait --output-format json) || true # an Invalid result exits non-zero; report it below
echo "$result"
status=$(printf '%s' "$result" | python3 -c 'import json, sys; print(json.load(sys.stdin).get("status", ""))' || true)
if [ "$status" != "Accepted" ]; then
  id=$(printf '%s' "$result" | python3 -c 'import json, sys; print(json.load(sys.stdin).get("id", ""))' || true)
  if [ -n "$id" ]; then
    xcrun notarytool log "$id" \
      --apple-id "$APPLE_ID" --team-id "$APPLE_TEAM_ID" --password "$APPLE_APP_PASSWORD" || true
  fi
  echo "Notarization status: ${status:-unknown}" >&2
  exit 1
fi
xcrun stapler staple "$app"
xcrun stapler validate "$app"
spctl --assess --type execute --verbose "$app" || true

rm -f "$out_zip"
ditto -c -k --sequesterRsrc --keepParent "$app" "$out_zip"
echo "Signed and notarized: $out_zip"
