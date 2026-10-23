#!/usr/bin/env bash
# Prints the build version, the same rule as Assets/Scripts/Systems/BuildVersion.cs:
#   tag vX.Y.Z (or vX.Y.Z-rc.1)  ->  X.Y.Z (or X.Y.Z-rc.1)
#   anything else                ->  0.0.0-dev+<shortsha>
#
# Sources, first hit wins: SM_VERSION, GITHUB_REF (refs/tags/v...), `git describe --exact-match`.
# In GitHub Actions also writes version=, short_sha= and prerelease= to $GITHUB_OUTPUT.
set -euo pipefail

semver_re='^[0-9]+\.[0-9]+\.[0-9]+([-+][0-9A-Za-z.+-]*)?$'

strip_tag() {
  local t="${1#refs/tags/}"
  t="${t#v}"
  t="${t#V}"
  if [[ "$t" =~ $semver_re ]]; then
    printf '%s' "$t"
  fi
}

sha="${GITHUB_SHA:-}"
if [ -z "$sha" ]; then
  sha=$(git rev-parse HEAD 2>/dev/null || true)
fi
short_sha="${sha:0:7}"

version=""
if [ -n "${SM_VERSION:-}" ]; then
  version=$(strip_tag "$SM_VERSION")
  version="${version:-$SM_VERSION}"
fi
if [ -z "$version" ] && [[ "${GITHUB_REF:-}" == refs/tags/* ]]; then
  version=$(strip_tag "$GITHUB_REF")
fi
if [ -z "$version" ] && [ -z "${GITHUB_ACTIONS:-}" ]; then
  tag=$(git describe --tags --exact-match HEAD 2>/dev/null || true)
  if [ -n "$tag" ]; then
    version=$(strip_tag "$tag")
  fi
fi
if [ -z "$version" ]; then
  if [ -n "$short_sha" ]; then
    version="0.0.0-dev+$short_sha"
  else
    version="0.0.0-dev"
  fi
fi

prerelease=false
if [[ "$version" == *-* ]]; then
  prerelease=true
fi

if [ -n "${GITHUB_OUTPUT:-}" ]; then
  {
    echo "version=$version"
    echo "short_sha=$short_sha"
    echo "prerelease=$prerelease"
  } >>"$GITHUB_OUTPUT"
fi
echo "$version"
