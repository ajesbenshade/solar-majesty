#!/usr/bin/env bash
# Zips one platform's player for distribution.
#
#   Tools/release/package.sh <targetPlatform> <build_dir> <version> <out_dir>
#
#   targetPlatform  StandaloneWindows64 | StandaloneLinux64 | StandaloneOSX (game-ci names)
#   build_dir       folder holding the player (game-ci: build/<targetPlatform>)
#   version         e.g. 1.2.0 or 0.0.0-dev+abc1234
#   out_dir         where SolarMajesty-<version>-<Windows|Linux|macOS>.zip is written
#
# Windows / Linux zips hold one top folder; the macOS zip holds SolarMajesty.app at its root.
# Unix permissions and symlinks are kept (zip -y; ditto on macOS), which upload-artifact alone
# would lose. Unity's "DoNotShip" / "DontShipItWithYourGame" debug folders are left out.
# Prints the zip path.
set -euo pipefail

if [ $# -ne 4 ]; then
  echo "usage: $0 <targetPlatform> <build_dir> <version> <out_dir>" >&2
  exit 2
fi

platform="$1"
build_dir="$2"
version="$3"
out_dir="$4"

case "$platform" in
  StandaloneWindows64) label="Windows" ;;
  StandaloneLinux64) label="Linux" ;;
  StandaloneOSX) label="macOS" ;;
  *)
    echo "unknown platform: $platform" >&2
    exit 2
    ;;
esac

if [ ! -d "$build_dir" ]; then
  echo "build folder not found: $build_dir" >&2
  exit 1
fi

mkdir -p "$out_dir"
out_dir=$(cd "$out_dir" && pwd)
zip_name="SolarMajesty-${version}-${label}.zip"
zip_path="$out_dir/$zip_name"
rm -f "$zip_path"

if [ "$platform" = "StandaloneOSX" ]; then
  app=$(find "$build_dir" -maxdepth 1 -name "*.app" -type d | head -n 1)
  if [ -z "$app" ]; then
    echo "no .app bundle in $build_dir" >&2
    exit 1
  fi
  # Unity's Linux mac-target builds can leave the executable without +x.
  find "$app/Contents/MacOS" -type f -exec chmod +x {} + ||
    echo "warning: could not chmod $app/Contents/MacOS (game-ci already sets +x there)" >&2
  if command -v ditto >/dev/null 2>&1; then
    ditto -c -k --sequesterRsrc --keepParent "$app" "$zip_path"
  else
    (cd "$(dirname "$app")" && zip -q -r -y "$zip_path" "$(basename "$app")")
  fi
else
  stage=$(mktemp -d)
  trap 'rm -rf "$stage"' EXIT
  top="SolarMajesty-${version}-${label}"
  mkdir -p "$stage/$top"
  # Copy without Unity's debug-symbol side folders.
  (cd "$build_dir" && tar -cf - \
    --exclude='*_BurstDebugInformation_DoNotShip' \
    --exclude='*_BackUpThisFolder_ButDontShipItWithYourGame' \
    .) | (cd "$stage/$top" && tar -xf -)
  if [ "$platform" = "StandaloneLinux64" ]; then
    find "$stage/$top" -maxdepth 1 -type f ! -name "*.*" -exec chmod +x {} +
    find "$stage/$top" -maxdepth 1 -type f -name "*.x86_64" -exec chmod +x {} +
  fi
  (cd "$stage" && zip -q -r -y "$zip_path" "$top")
fi

echo "$zip_path"
