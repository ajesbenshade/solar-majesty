# Release pipeline

How CI, nightly builds and tagged releases work, and exactly what a person has to do to switch them
on. Nothing here needs a secret pasted into chat, an issue or a commit: every secret goes into
GitHub's encrypted store and the workflows read it as `${{ secrets.NAME }}`.

| Workflow | Runs on | Does |
|----------|---------|------|
| [`ci.yml`](../.github/workflows/ci.yml) | push to `main`, pull requests, nightly 06:00 UTC, manual | C# compile check (no secrets) · license preflight → EditMode + PlayMode tests → macOS / Windows / Linux builds, kept 14 days |
| [`release.yml`](../.github/workflows/release.yml) | pushing a `vX.Y.Z` tag | the same tests and builds with the tag's version → optional signing → GitHub Release → optional itch.io / Steam upload |

Both use [GameCI](https://game.ci) (`game-ci/unity-test-runner`, `game-ci/unity-builder`) on
`ubuntu-24.04`; macOS notarization alone runs on `macos-15`.

---

## 1. Turn CI on (required, once)

Until this is done every CI run fails in the **Unity license preflight** job with
`Unity license secrets missing`. That is deliberate: before the preflight existed, the Unity step
exited 1 without producing any results, which is why every earlier run is red.

### 1a. Check that Actions is enabled and has minutes

1. Open the repository on GitHub → **Actions** tab.
   - If it shows *"Workflows aren't being run on this repository"* or an **I understand my
     workflows, go ahead and enable them** button, click it.
   - If the **CI** workflow on the left says *disabled*, open it → **⋯** → **Enable workflow**.
     GitHub disables scheduled workflows on its own after 60 days without repository activity.
2. **Settings → Actions → General**: *Actions permissions* must allow actions (choose **Allow all
   actions and reusable workflows**, or allow at least `actions/*`, `game-ci/*` and
   `softprops/action-gh-release`). Under *Workflow permissions*, the default read-only token is
   enough; the workflows ask for the extra permissions they need.
3. Minutes: public repositories get standard GitHub-hosted runners free. For a private repository,
   check **Settings → Billing and plans** (personal account) for remaining minutes and any spending
   limit; macOS runner minutes count ten times.

As of this writing the repository's last CI run was on 2026-09-21, and pushes after that started
none. The workflow itself is listed as active, so check step 1 first: Actions may be turned off
at repository level, or blocked by an account or billing setting. Nothing in the repository can
change that.

### 1b. Get a Unity license for CI

GameCI activates Unity inside a Docker container, using your Unity account. Pick the case that
matches your license (GameCI's guide: <https://game.ci/docs/github/activation>).

**Unity Personal (free)**

1. Install [Unity Hub](https://unity.com/download) and sign in with the Unity account CI should use.
2. **Unity Hub → Preferences (Settings) → Licenses → Add → Get a free personal license.** Click
   *Add* even if a license is already listed: that step is what writes the license file.
3. Find the license file (hidden folders):
   - Windows: `C:\ProgramData\Unity\Unity_lic.ulf`
   - macOS: `/Library/Application Support/Unity/Unity_lic.ulf`
   - Linux: `~/.local/share/unity3d/Unity/Unity_lic.ulf`

   It is an XML document that starts `<?xml version="1.0" ...?><root><License id="Terms">`. The
   license is not tied to an operating system or Unity version: one made on a Mac works for the
   Ubuntu runners. If Hub offers no way to create a `.ulf`, see GameCI's Discord (linked from the
   activation guide); the workaround changes with Hub releases.

**Unity Plus / Pro**

Copy the serial key (`XX-XXXX-XXXX-XXXX-XXXX-XXXX`) from <https://id.unity.com/en/subscriptions>.
You do not need a `.ulf`.

### 1c. Add the secrets in GitHub

**Settings → Secrets and variables → Actions → Secrets → New repository secret.** Add them one at a
time. Never paste these into an issue, a pull request, a commit or a chat.

| Secret | Value |
|--------|-------|
| `UNITY_EMAIL` | the Unity account's email |
| `UNITY_PASSWORD` | the Unity account's password (the account must not need an interactive 2FA prompt for CI; GameCI's docs cover accounts with 2FA) |
| `UNITY_LICENSE` | **Personal:** the entire contents of `Unity_lic.ulf`, pasted as-is |
| `UNITY_SERIAL` | **Plus/Pro only:** the serial key. Use this *instead of* `UNITY_LICENSE` |

Then re-run CI: **Actions → CI → Run workflow** (on `main`), or push a commit. The preflight turns
green and the tests start. The preflight also warns if `UNITY_LICENSE` doesn't look like a `.ulf`.

Pull requests from forks never get repository secrets, so their Unity jobs are **skipped** (with a
notice), not failed. They run after merge.

### 1d. If the tests still fail

- **No image for the Unity version.** GameCI needs a `unityci/editor` image for exactly
  `6000.5.10f1` (set as `UNITY_VERSION` in both workflows, and it must match
  `ProjectSettings/ProjectVersion.txt`). Brand-new editor versions get images a few days after
  release; check <https://game.ci/docs/docker/versions>.
- **License activation errors** (`No valid Unity Editor license found`, `Machine bindings don't
  match`): regenerate the `.ulf` (step 1b) and replace `UNITY_LICENSE`.
- **PlayMode tests** run headless in GameCI's container with its default settings; if one needs a
  GPU it will fail there and not locally. The test modes are unchanged from before this pipeline.

### 1e. What each CI run tests

| Job | Push / pull request | Nightly 06:00 UTC and manual *Run workflow* |
|-----|---------------------|---------------------------------------------|
| **C# compile check** | yes (forks too, no secrets needed) | yes |
| **editmode tests** | all | all |
| **playmode tests** | all except `[Category("Soak")]` and `[Category("Perf")]` | everything, Soak and Perf included; perf JSON uploaded as `perf-<run number>` (30 days) |
| **Build** ×3 | after tests pass | after tests pass |

The tag release (`release.yml`) gates on the push / pull request suite. Soak and Perf run nightly.

How the pieces reach Unity:

- The exclusion is the Unity Test Framework argument `-testCategory !Soak;!Perf`, passed through
  `customParameters`. GameCI splits that string on spaces without handling quotes, so it must stay
  unquoted. If a run still executes Soak tests, look for the
  `Using custom parameters` line in the log.
- Unity runs in a container that **does not inherit the job's environment**. The workflow passes
  `CI=true`, `SM_SOAK_MINUTES=10` and
  `SM_PERF_OUT=/github/workspace/test-results/perf-<mode>.json` through
  `GAME_CI_DOCKER_ENV`, which GameCI's CLI turns into `--dockerEnv`. `unity-test-runner@v4`
  now wraps that CLI (`cliVersion` is pinned to `v0.1.72` for repeatable runs). Bump it on
  purpose, not by accident.
- **C# compile check** (`Tools/compile_check/check.sh`) builds every assembly against Unity
  reference assemblies with .NET 8. It catches compile errors in about a minute, without a
  license. The Unity jobs do not wait for it.

---

## 2. Nightly and `main` builds

Every push to `main` and every night at 06:00 UTC, CI builds all three platforms after the tests
pass.

To download one: **Actions → CI** → pick a run (filter by *Event: schedule* for nightlies) → scroll
to **Artifacts** at the bottom of the run summary → click
`SolarMajesty-0.0.0-dev+<sha>-Windows.zip` (or `-macOS` / `-Linux`). You must be signed in to
GitHub. Artifacts are deleted after **14 days**; test results are kept the same time.

The file is the game zip itself, not a zip in a zip. Executable bits are kept, so the Linux binary
and the macOS app run after unzipping. Nightly builds are **unsigned**: Windows SmartScreen shows
*More info → Run anyway*, and macOS needs right-click → **Open** the first time.

---

## 3. Versions

One rule, in [`BuildVersion.cs`](../Assets/Scripts/Systems/BuildVersion.cs) (with EditMode tests)
and [`Tools/release/version.sh`](../Tools/release/version.sh):

| Build | Version |
|-------|---------|
| tag `v1.2.3` | `1.2.3` |
| tag `v1.3.0-rc.1` | `1.3.0-rc.1` (published as a GitHub *pre-release*) |
| anything else | `0.0.0-dev+<7-char commit>` |

[`BuildCommands`](../Assets/Scripts/Editor/BuildCommands.cs) applies it for the build only (the
project's `ProjectSettings` stay unchanged): `PlayerSettings.bundleVersion` (`Application.version`;
Windows takes its file version from it) and `PlayerSettings.macOS.buildNumber` (`X.Y.Z` core,
because Apple wants digits only). It looks for, in order: `-smVersion <v>` on the Unity command
line, the `SM_VERSION` environment variable, GameCI's `-buildVersion`, a `vX.Y.Z` tag on `HEAD`,
and finally the dev fallback.

Every standalone player also gets `StreamingAssets/build-info.json`:

```json
{
  "version": "1.2.3",
  "commit": "c7014870…",
  "platform": "StandaloneWindows64",
  "builtUtc": "2026-10-11T08:30:00Z"
}
```

`BuildVersion.LoadLabel(Application.streamingAssetsPath, Application.version)` returns a short
label (`v1.2.3`, or `v0.0.0-dev+c701487`) for a title-screen or pause-menu corner. The label is
not wired into the UI yet.

Local builds from **Solar Majesty → Build → …** use the same code: they get the dev version of
your checkout, or the tag if `HEAD` is tagged.

---

## 4. Cutting a release

1. Make sure CI on `main` is green.
2. Tag the commit and push the tag:

   ```bash
   git checkout main && git pull
   git tag -a v0.1.0 -m "Solar Majesty 0.1.0"
   git push origin v0.1.0
   ```

3. Watch **Actions → Release**. It runs the tests, builds the three platforms with version
   `0.1.0`, signs whatever it has secrets for, and publishes **Releases → Solar Majesty 0.1.0**
   with:
   - `SolarMajesty-0.1.0-Windows.zip`, `SolarMajesty-0.1.0-macOS.zip`, `SolarMajesty-0.1.0-Linux.zip`
   - `SHA256SUMS.txt`
   - notes generated from the merged pull requests (edit them on the release page afterwards)
4. A tag with a suffix (`v0.2.0-rc.1`) is published as a pre-release.

A tag that is not `vX.Y.Z[-suffix]` stops the workflow in the preflight. If a release run fails
part-way, fix the problem, delete the tag (`git push --delete origin v0.1.0 && git tag -d v0.1.0`)
and the draft or partial release, and push the tag again.

---

## 5. Optional: signing and store uploads

Each stage runs only when **all** of its secrets are set; otherwise it is skipped and the release
still publishes unsigned zips. A preflight step turns the secrets into true/false flags, so the
secret values only reach the job that uses them. Values marked *variable* are not sensitive: add
them under **Settings → Secrets and variables → Actions → Variables** (a secret of the same name
also works).

### Windows: Authenticode

| Name | Kind | Value |
|------|------|-------|
| `WINDOWS_CERT_PFX_BASE64` | secret | `base64 -w0 cert.pfx` (macOS: `base64 -i cert.pfx`) |
| `WINDOWS_CERT_PASSWORD` | secret | the `.pfx` password |

[`sign-windows.sh`](../Tools/release/sign-windows.sh) signs `SolarMajesty.exe` with `osslsigncode`
on the Linux runner (SHA-256, RFC 3161 timestamp). `UnityPlayer.dll` and `UnityCrashHandler64.exe`
already carry Unity's signature.

**Read this before buying a certificate:** since June 2023, publicly trusted code-signing
certificates (OV and EV) are issued only on hardware tokens or cloud HSMs, and cannot be exported
as a `.pfx`. The `.pfx` path works for an older exportable certificate or an internal CA. For a new
certificate, use the CA's cloud signing service or Microsoft's Azure Trusted Signing (Artifact
Signing). Each has its own GitHub Action, which would replace the *Authenticode-sign* step in
`release.yml`.

### macOS: Developer ID signing and notarization

Needs a paid Apple Developer Program membership.

| Name | Kind | Value |
|------|------|-------|
| `APPLE_CERT_P12_BASE64` | secret | a **Developer ID Application** certificate with its private key, exported from Keychain Access as `.p12`, then `base64 -i cert.p12` |
| `APPLE_CERT_PASSWORD` | secret | the `.p12` export password |
| `APPLE_TEAM_ID` | secret | 10-character Team ID (developer.apple.com → Membership) |
| `APPLE_ID` | secret | the Apple ID email used to notarize |
| `APPLE_APP_PASSWORD` | secret | an app-specific password for that Apple ID (appleid.apple.com → Sign-In and Security) |

The `sign-macos` job runs on a **macOS runner**, because `codesign` and `notarytool` exist only on
macOS. [`sign-macos.sh`](../Tools/release/sign-macos.sh) imports the certificate into a throwaway
keychain, signs nested libraries and then the app with the hardened runtime and
[`SolarMajesty.entitlements`](../Tools/release/macos/SolarMajesty.entitlements) (the JIT and
library-validation exceptions a Unity Mono player needs), notarizes, staples, and re-zips. If
notarization is rejected, the job prints Apple's log and the release is not published. On a private
repository, macOS minutes count ten times against the allowance.

### itch.io (butler)

| Name | Kind | Value |
|------|------|-------|
| `BUTLER_API_KEY` | secret | <https://itch.io/user/settings/api-keys> → Generate new API key |
| `ITCH_TARGET` | variable | `<itch username>/<game slug>`, e.g. `yourname/solar-majesty` (create the game page on itch.io first) |

[`upload-itch.sh`](../Tools/release/upload-itch.sh) pushes the zips to the `windows`, `osx` and
`linux` channels with `--userversion <version>`, so itch tags platforms on its own.

### Steam (steamcmd)

Needs a Steamworks app with one depot per platform.

| Name | Kind | Value |
|------|------|-------|
| `STEAM_USERNAME` | secret | a dedicated build account with only *Edit App Metadata* / *Publish* rights for this app (not your personal account) |
| `STEAM_CONFIG_VDF` | secret | base64 of that account's `config/config.vdf` (see below) |
| `STEAM_APP_ID` | variable | the app id |
| `STEAM_DEPOT_WINDOWS`, `STEAM_DEPOT_MACOS`, `STEAM_DEPOT_LINUX` | variable | depot ids. Leave one empty to skip that platform |
| `STEAM_BRANCH` | variable, optional | a beta branch to set live, e.g. `beta`. Empty uploads without setting anything live. `default` is refused: promote to the public branch by hand in Steamworks |

`STEAM_CONFIG_VDF` lets CI log in without a Steam Guard prompt. On any machine, install
[steamcmd](https://developer.valvesoftware.com/wiki/SteamCMD), run `steamcmd +login <build account>`,
enter the password and the Steam Guard code once, `quit`, then base64 the `config/config.vdf` that
steamcmd wrote (inside the steamcmd folder, or `~/Steam/config/` on Linux):
`base64 -w0 config/config.vdf`. Steam can expire that session; if uploads start asking for a code,
repeat this.

[`upload-steam.sh`](../Tools/release/upload-steam.sh) unpacks the zips into one content folder
per platform, renders [`steam/app_build.vdf.template`](../Tools/release/steam/app_build.vdf.template)
and [`steam/depot.vdf.template`](../Tools/release/steam/depot.vdf.template), and runs
`steamcmd +run_app_build`.

---

## 6. App icons

[`Assets/Art/Icons/`](../Assets/Art/Icons/) holds `SM_AppIcon_<size>.png` at 1024, 512, 256, 128,
64, 48, 32 and 16 px, drawn per size by
[`Tools/release/icons/make_app_icons.py`](../Tools/release/icons/make_app_icons.py)
(`python3` + `numpy`). Every build assigns them as the default and Standalone icons. To store them
in `ProjectSettings` for builds made some other way, run **Solar Majesty → Build → Apply App
Icons** once and commit `ProjectSettings/ProjectSettings.asset`.
[`IconImportRules`](../Assets/Scripts/Editor/IconImportRules.cs) keeps them uncompressed and
without mipmaps, so the 16 and 32 px sizes stay crisp.

---

## 7. Files

| Path | Purpose |
|------|---------|
| `.github/workflows/ci.yml` | CI + nightly |
| `.github/workflows/release.yml` | tag → release |
| `Tools/release/check-unity-secrets.sh` | license preflight |
| `Tools/release/version.sh` | version rule for the workflows |
| `Tools/release/package.sh` | per-platform zip that keeps permissions; leaves out Unity's `DoNotShip` folders |
| `Tools/release/sign-windows.sh` / `sign-macos.sh` | signing |
| `Tools/release/upload-itch.sh` / `upload-steam.sh` | store uploads |
| `Assets/Scripts/Editor/BuildCommands.cs` | build entry points; `BuildFromCommandLine` is the CI one |
| `Assets/Scripts/Editor/ReleaseStamp.cs` | version, icons, `build-info.json` |
| `Assets/Scripts/Systems/BuildVersion.cs` | the version rule and build-info reader (tested) |

### Notes on the action versions

`actions/checkout@v7`, `actions/cache@v6`, `actions/upload-artifact@v7`,
`actions/download-artifact@v8`, `actions/setup-dotnet@v6`, `softprops/action-gh-release@v3` and
`game-ci/unity-test-runner@v4` all run on Node 24. `game-ci/unity-builder@v4` still declares
Node 20. `unity-builder@v6` moves to GameCI's CLI and drops several v4 inputs. The workflows set
`FORCE_JAVASCRIPT_ACTIONS_TO_NODE24`, so v4 already runs on Node 24. Move to v6 on its own
branch.
