#!/usr/bin/env bash
# Compile-checks the whole game without a Unity install or licence.
#
# Builds Systems, Runtime + every EditMode/PlayMode test, and Runtime + Editor tooling against
# Unity reference assemblies from NuGet (UnityEngine 2021.3 modules, UnityEditor 2020.2) plus small
# stubs for the URP / AI Navigation packages. Those references are older than the project's Unity 6,
# so the handful of Unity-6-only calls the game makes are listed in baseline.txt and ignored.
# Anything else is a real compile error. Needs the .NET 8 SDK.
#
#   Tools/compile_check/check.sh            # check this checkout
#   Tools/compile_check/check.sh --update   # rewrite baseline.txt (only for a new Unity-6-only API)
set -uo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../.." && pwd)"
UPDATE=0
[ "${1:-}" = "--update" ] && UPDATE=1

# One line per distinct error: file (relative to Assets/Scripts), code and message; no line numbers,
# so unrelated edits don't churn the baseline.
norm() {
  grep -E ": error " | sed -E 's# \[[^]]*\]$##; s#.*Assets/Scripts/##; s#.*compile_check/#compile_check/#; s#\([0-9]+,[0-9]+\)##' | sort -u
}

tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT

build() {
  dotnet build "$HERE/$1" -nologo -v q -p:RepoRoot="$ROOT/" 2>&1 | norm > "$tmp/$2"
}

echo "[compile-check] restoring + building (first run downloads ~20 MB of reference assemblies)..."
build systems/Systems.csproj systems.txt
if [ -s "$tmp/systems.txt" ]; then
  # Runtime, tests and editor reference Systems, so they can't be checked until it compiles.
  echo "[compile-check] FAILED — Systems assembly (fix these first; Runtime/Editor/tests were not checked):"
  sed 's/^/  /' "$tmp/systems.txt"
  exit 1
fi
build runtime_tests/RuntimeTests.csproj tests.txt
build runtime_editor/RuntimeEditor.csproj editor.txt
cat "$tmp/systems.txt" "$tmp/tests.txt" "$tmp/editor.txt" | sort -u > "$tmp/all.txt"

if [ "$UPDATE" = 1 ]; then
  cp "$tmp/all.txt" "$HERE/baseline.txt"
  echo "[compile-check] baseline.txt rewritten ($(wc -l < "$HERE/baseline.txt") lines). Review the diff before committing."
  exit 0
fi

comm -23 "$tmp/all.txt" "$HERE/baseline.txt" > "$tmp/new.txt"
comm -13 "$tmp/all.txt" "$HERE/baseline.txt" > "$tmp/gone.txt"
if [ -s "$tmp/gone.txt" ]; then
  echo "[compile-check] note: baseline lines no longer reported (safe to remove from baseline.txt):"
  sed 's/^/  /' "$tmp/gone.txt"
fi
if [ -s "$tmp/new.txt" ]; then
  echo "[compile-check] FAILED — compile errors:"
  sed 's/^/  /' "$tmp/new.txt"
  echo "If one of these is a genuine Unity 6 API the 2021 references lack, add it with --update and say so in the PR."
  exit 1
fi
echo "[compile-check] OK — no compile errors ($(wc -l < "$HERE/baseline.txt") known Unity-6-only API differences ignored)."
