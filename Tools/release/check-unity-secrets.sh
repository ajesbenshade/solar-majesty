#!/usr/bin/env bash
# Unity license preflight for GitHub Actions.
#
# game-ci needs UNITY_EMAIL + UNITY_PASSWORD, plus UNITY_LICENSE (Personal: the .ulf file
# contents) or UNITY_SERIAL (Plus/Pro). Without them the Unity step exits 1 with no test results,
# which is what every CI run did before this check existed. Fail here instead, in one line a
# human can act on.
#
# Inputs (environment): UNITY_LICENSE, UNITY_SERIAL, UNITY_EMAIL, UNITY_PASSWORD, and
# IS_FORK_PR=true when the run is a pull request from a fork (GitHub withholds secrets there, so
# the Unity jobs are skipped rather than failed).
# Output: ok=true|false appended to $GITHUB_OUTPUT when it is set.
set -euo pipefail

DOC="Docs/RELEASE_PIPELINE.md"
missing=()
[ -n "${UNITY_EMAIL:-}" ] || missing+=("UNITY_EMAIL")
[ -n "${UNITY_PASSWORD:-}" ] || missing+=("UNITY_PASSWORD")
if [ -z "${UNITY_LICENSE:-}" ] && [ -z "${UNITY_SERIAL:-}" ]; then
  missing+=("UNITY_LICENSE (or UNITY_SERIAL for Plus/Pro)")
fi

emit() {
  if [ -n "${GITHUB_OUTPUT:-}" ]; then
    echo "ok=$1" >>"$GITHUB_OUTPUT"
  fi
}

summary() {
  if [ -n "${GITHUB_STEP_SUMMARY:-}" ]; then
    printf '%s\n' "$@" >>"$GITHUB_STEP_SUMMARY"
  fi
}

if [ ${#missing[@]} -eq 0 ]; then
  if [ -n "${UNITY_LICENSE:-}" ] && ! printf '%s' "$UNITY_LICENSE" | grep -q "<License"; then
    echo "::warning title=UNITY_LICENSE looks wrong::UNITY_LICENSE should be the full contents of the Unity_lic.ulf file (an XML document starting with <?xml ...><root><License ...>). See $DOC."
  fi
  echo "Unity license secrets are present."
  emit true
  exit 0
fi

list=$(printf '%s, ' "${missing[@]}")
list=${list%, }

if [ "${IS_FORK_PR:-false}" = "true" ]; then
  echo "::notice title=Unity jobs skipped::Pull requests from forks get no repository secrets, so the Unity test and build jobs are skipped. They run after merge."
  summary "Unity jobs skipped: fork pull requests have no access to repository secrets."
  emit false
  exit 0
fi

echo "::error title=Unity license secrets missing::Missing GitHub Actions secret(s): $list. Add them under the repository's Settings > Secrets and variables > Actions (never paste them into an issue or chat). Step-by-step: $DOC."
summary "## Unity license secrets missing" \
  "" \
  "Missing: **$list**" \
  "" \
  "Add them under **Settings > Secrets and variables > Actions > New repository secret**." \
  "How to get a Unity license file: see \`$DOC\`."
emit false
exit 1
