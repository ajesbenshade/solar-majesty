#!/usr/bin/env bash
# Uploads the release zips to Steam with steamcmd, from Tools/release/steam/*.vdf.template.
#
#   Tools/release/upload-steam.sh <dist_dir> <version>
#
# Environment:
#   STEAM_USERNAME       build account (a dedicated Steamworks account with only build rights)
#   STEAM_CONFIG_VDF     base64 of that account's config/config.vdf after one interactive
#                        Steam Guard login (see Docs/RELEASE_PIPELINE.md)
#   STEAM_APP_ID         the app id
#   STEAM_DEPOT_WINDOWS  depot ids; a platform whose depot id is empty is not uploaded
#   STEAM_DEPOT_MACOS
#   STEAM_DEPOT_LINUX
#   STEAM_BRANCH         optional: branch to set live (never "default" from CI; e.g. "beta").
#                        Empty uploads the build without setting it live.
set -euo pipefail

if [ $# -ne 2 ]; then
  echo "usage: $0 <dist_dir> <version>" >&2
  exit 2
fi
dist_dir=$(cd "$1" && pwd)
version="$2"
: "${STEAM_USERNAME:?STEAM_USERNAME is not set}"
: "${STEAM_CONFIG_VDF:?STEAM_CONFIG_VDF is not set}"
: "${STEAM_APP_ID:?STEAM_APP_ID is not set}"

here="$(cd "$(dirname "$0")" && pwd)"
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT
content="$work/content"
output="$work/output"
mkdir -p "$content" "$output"

# Replaces @TOKEN@ in a template with values, no envsubst needed.
render() {
  # Bash 5.2+ would otherwise expand "&" in a replacement to the matched text.
  shopt -u patsub_replacement 2>/dev/null || true
  local text
  text=$(cat "$1")
  shift
  while [ $# -ge 2 ]; do
    text="${text//@$1@/$2}"
    shift 2
  done
  printf '%s\n' "$text"
}

depots=""
add_depot() {
  local depot_id="$1" label="$2" dir="$3"
  local zip="$dist_dir/SolarMajesty-${version}-${label}.zip"
  if [ -z "$depot_id" ]; then
    echo "skip $label: no depot id"
    return
  fi
  if [ ! -f "$zip" ]; then
    echo "skip $label: $zip not found"
    return
  fi
  mkdir -p "$content/$dir"
  unzip -q "$zip" -d "$content/$dir"
  # Windows / Linux zips wrap everything in one top folder; Steam wants the files at the root.
  local top="$content/$dir/SolarMajesty-${version}-${label}"
  if [ -d "$top" ]; then
    (shopt -s dotglob && mv "$top"/* "$content/$dir/")
    rmdir "$top"
  fi
  depots+="$(render "$here/steam/depot.vdf.template" DEPOT_ID "$depot_id" PLATFORM_DIR "$dir")"$'\n'
}

add_depot "${STEAM_DEPOT_WINDOWS:-}" Windows windows
add_depot "${STEAM_DEPOT_MACOS:-}" macOS macos
add_depot "${STEAM_DEPOT_LINUX:-}" Linux linux
if [ -z "$depots" ]; then
  echo "No depot ids set (STEAM_DEPOT_WINDOWS / _MACOS / _LINUX); nothing to upload." >&2
  exit 1
fi

setlive=""
if [ -n "${STEAM_BRANCH:-}" ]; then
  if [ "$STEAM_BRANCH" = "default" ]; then
    echo "Refusing to set the default branch live from CI; use a beta branch and promote in Steamworks." >&2
    exit 1
  fi
  setlive=$'\t'"\"SetLive\" \"${STEAM_BRANCH}\""
fi

vdf="$work/app_build.vdf"
render "$here/steam/app_build.vdf.template" \
  STEAM_APP_ID "$STEAM_APP_ID" \
  VERSION "$version" \
  CONTENT_ROOT "$content" \
  BUILD_OUTPUT "$output" \
  SETLIVE_LINE "$setlive" \
  DEPOTS "${depots%$'\n'}" >"$vdf"
echo "--- app_build.vdf"
cat "$vdf"

# --- steamcmd ------------------------------------------------------------------------------------
if ! dpkg -s lib32gcc-s1 >/dev/null 2>&1; then
  sudo apt-get update -qq
  sudo apt-get install -y -qq lib32gcc-s1
fi
steam_dir="$HOME/steamcmd"
mkdir -p "$steam_dir"
if [ ! -x "$steam_dir/steamcmd.sh" ]; then
  curl -fsSL "https://steamcdn-a.akamaihd.net/client/installer/steamcmd_linux.tar.gz" | tar -xz -C "$steam_dir"
fi

# Steam Guard: reuse the authorised session from config.vdf (both places steamcmd looks).
for cfg in "$steam_dir/config" "$HOME/Steam/config"; do
  mkdir -p "$cfg"
  printf '%s' "$STEAM_CONFIG_VDF" | base64 -d >"$cfg/config.vdf"
  chmod 600 "$cfg/config.vdf"
done

"$steam_dir/steamcmd.sh" \
  +@ShutdownOnFailedCommand 1 \
  +@NoPromptForPassword 1 \
  +login "$STEAM_USERNAME" \
  +run_app_build "$vdf" \
  +quit

echo "Steam build uploaded for app $STEAM_APP_ID ($version)."
