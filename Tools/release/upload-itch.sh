#!/usr/bin/env bash
# Pushes the release zips to itch.io with butler.
#
#   Tools/release/upload-itch.sh <dist_dir> <version>
#
# Environment:
#   BUTLER_API_KEY  itch.io API key (https://itch.io/user/settings/api-keys)
#   ITCH_TARGET     <user>/<game>, e.g. yourname/solar-majesty
#
# Channels are windows / osx / linux, which itch tags with the right platform automatically.
set -euo pipefail

if [ $# -ne 2 ]; then
  echo "usage: $0 <dist_dir> <version>" >&2
  exit 2
fi
dist_dir="$1"
version="$2"
: "${BUTLER_API_KEY:?BUTLER_API_KEY is not set}"
: "${ITCH_TARGET:?ITCH_TARGET is not set (user/game)}"
export BUTLER_API_KEY

work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT
if ! command -v butler >/dev/null 2>&1; then
  curl -fsSL -o "$work/butler.zip" "https://broth.itch.zone/butler/linux-amd64/LATEST/archive/default"
  unzip -q "$work/butler.zip" -d "$work/butler"
  chmod +x "$work/butler/butler"
  PATH="$work/butler:$PATH"
fi
butler -V

pushed=0
for pair in "Windows:windows" "macOS:osx" "Linux:linux"; do
  label="${pair%%:*}"
  channel="${pair##*:}"
  zip="$dist_dir/SolarMajesty-${version}-${label}.zip"
  if [ ! -f "$zip" ]; then
    echo "skip $channel: $zip not found"
    continue
  fi
  butler push "$zip" "${ITCH_TARGET}:${channel}" --userversion "$version"
  pushed=$((pushed + 1))
done

if [ "$pushed" -eq 0 ]; then
  echo "No zips found in $dist_dir for version $version." >&2
  exit 1
fi
echo "Pushed $pushed channel(s) to $ITCH_TARGET."
