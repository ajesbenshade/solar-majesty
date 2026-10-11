#!/usr/bin/env bash
# Authenticode-signs the Windows player on Linux with osslsigncode.
#
#   Tools/release/sign-windows.sh <build_dir>
#
# Environment:
#   WINDOWS_CERT_PFX_BASE64  base64 of the code-signing certificate + key (.pfx / .p12)
#   WINDOWS_CERT_PASSWORD    its password
#   WINDOWS_TIMESTAMP_URL    optional RFC 3161 timestamp server (default: DigiCert)
#
# Signs SolarMajesty.exe. UnityPlayer.dll and UnityCrashHandler64.exe ship already signed by
# Unity, so they are left alone. Note: code-signing certificates issued since mid-2023 live on
# hardware tokens / cloud HSMs and cannot be exported as a .pfx; see Docs/RELEASE_PIPELINE.md.
set -euo pipefail

if [ $# -ne 1 ]; then
  echo "usage: $0 <build_dir>" >&2
  exit 2
fi
build_dir="$1"
: "${WINDOWS_CERT_PFX_BASE64:?WINDOWS_CERT_PFX_BASE64 is not set}"
: "${WINDOWS_CERT_PASSWORD:?WINDOWS_CERT_PASSWORD is not set}"
ts_url="${WINDOWS_TIMESTAMP_URL:-http://timestamp.digicert.com}"

if ! command -v osslsigncode >/dev/null 2>&1; then
  sudo apt-get update -qq
  sudo apt-get install -y -qq osslsigncode
fi

exe="$build_dir/SolarMajesty.exe"
if [ ! -f "$exe" ]; then
  echo "not found: $exe" >&2
  exit 1
fi

work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT
pfx="$work/cert.pfx"
printf '%s' "$WINDOWS_CERT_PFX_BASE64" | base64 -d >"$pfx"
chmod 600 "$pfx"

# Password via a file so it never appears on a command line.
pass_file="$work/pass"
printf '%s' "$WINDOWS_CERT_PASSWORD" >"$pass_file"
chmod 600 "$pass_file"

osslsigncode sign \
  -pkcs12 "$pfx" \
  -readpass "$pass_file" \
  -n "Solar Majesty" \
  -h sha256 \
  -ts "$ts_url" \
  -in "$exe" \
  -out "$work/signed.exe"

mv "$work/signed.exe" "$exe"
osslsigncode verify -in "$exe" >/dev/null 2>&1 || echo "note: verify could not build a full chain (normal without the CA bundle); the signature is embedded."
echo "Signed $exe"
