# Compile check (no Unity needed)

`check.sh` compiles the whole game outside Unity, so a broken build shows up in seconds:

- on any machine with the .NET 8 SDK;
- in CI before the Unity licence secrets exist;
- on every PR, as a fast first gate ahead of the full Unity test run.

```bash
Tools/compile_check/check.sh
```

It checks three groups:

| Project | Compiles |
|---|---|
| `systems/` | `Assets/Scripts/**` outside Runtime, Editor and Tests (the `SolarMajesty.Systems` assembly) |
| `runtime_tests/` | Runtime and every EditMode and PlayMode test |
| `runtime_editor/` | Runtime and the Editor tooling (build commands, content builders) |

## What it compiles against

- **Unity reference assemblies, restored from NuGet as compile-only references** (never shipped or committed):
  - UnityEngine modules 2021.3, the newest published on NuGet;
  - UnityEditor 2020.2.
- **Small hand-written stubs in `stubs/`** for the URP, AI Navigation and Test Framework types the code uses. These only need the members the game calls.

## Limits

- **It doesn't run anything.** Tests still need Unity, via `.github/workflows/ci.yml`.
- **The references are older than Unity 6**, so calls to Unity-6-only APIs fail to compile here. Examples: `FindObjectsByType()` with no arguments, `EntityId`, `NamedBuildTarget`. Each one is listed once in `baseline.txt` and ignored.
- **Adding a new Unity-6-only call** means rerunning with `--update`, reviewing the `baseline.txt` diff, and noting it in the PR.
- **A stub missing a member** shows up as an error in `compile_check/stubs/...`. Add the member to the stub; don't change game code to suit the stub.
