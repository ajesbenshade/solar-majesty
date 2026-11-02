# Bundled local AI (the `ai` folder)

The Windows game ships with its own small models in an `ai` folder next to `SolarMajesty.exe`.
When the player turns on **Settings → BOT LINES · LOCAL LLM** or **SPOKEN · LOCAL TTS** (both off by
default), the game starts them itself. Nothing to install: no Python, no Ollama, no internet.
Everything runs on 127.0.0.1 and nothing leaves the PC.

On Steam the folder is part of the base depot ([STEAM_RELEASE.md](STEAM_RELEASE.md)). For testers
outside Steam, [`package-windows.ps1`](../Tools/release/package-windows.ps1) zips it with the game.

## What is in `ai\`

| Folder | What | Used for |
|---|---|---|
| `llama\` | llama.cpp CPU build (`llama-server.exe` + ggml DLLs) | runs the two Qwen models |
| `models\` | `Qwen3-1.7B-Q4_K_M.gguf` (1.1 GB), `Qwen3-4B-Instruct-2507-Q4_K_M.gguf` (2.5 GB), `kokoro-v1.0.onnx` + `voices-v1.0.bin` (0.35 GB) | bot lines, conversation, speech |
| `python\` | portable Python 3.12 with kokoro-onnx, onnxruntime, numpy, phonemizer | Kokoro speech server and the model router |
| `scripts\` | [`voice_server.py`](../Tools/local_ai/voice_server.py), [`model_router.py`](../Tools/local_ai/model_router.py) | the two Python services |
| `THIRD-PARTY-NOTICES.txt`, `licenses\` | licenses (Apache-2.0, MIT, BSD, PSF, GPL-3.0 for phonemizer/espeak-ng) and the GPL-3.0 written offer | |

Total about 3.9 GB. Laya is not included (it needs PyTorch and is slow on CPU; the game's own brain
makes the decisions). Its docs are in [LAYA_LOCAL_AI.md](LAYA_LOCAL_AI.md).

## How the game uses it

`LocalAiHost` (Runtime/Narration) runs before the first scene loads, on Windows only:

1. Finds `ai\` next to the exe (or `SOLAR_AI_DIR`, the only way to use it in the editor). Does nothing if it is missing.
2. `LocalAiPlan` (Systems/LocalAi, tested by `LocalAiPlanTests`) picks what can run from the files present and the
   PC's RAM: under 6 GB nothing; 6–11 GB lines + speech; 11 GB and up lines, conversation and speech. With
   conversation on, `model_router.py` puts both Qwen servers behind one URL and falls back to the small model.
3. `HeroNarrator` and `HeroSpeaker` get the planned URLs and model names straight from `LocalAiHost` when no
   developer server is set. They stay behind the player's settings as before: the pack never turns them on.
4. While BOT LINES or SPOKEN is on, the host starts what the setting needs (text models for either, the voice
   server for SPOKEN), each hidden on a free loopback port, below normal priority, logging to
   `%USERPROFILE%\AppData\LocalLow\SolarMajesty\Solar Majesty\ai\*.log`. Bots use their written lines for the
   first minute while the models load, then switch to live ones; the chat panel says "Loading the local model".
5. Both settings off for 30 seconds stops the models again (several GB of RAM back).
6. A Windows job object ties the processes to the game: if the game crashes or is killed, they die too.

Off whatever the settings say with `-noai` or `SOLAR_NO_AI=1` (Steam launch option "Play without local AI";
the benchmark, autoplay and screenshot scripts pass `-noai`). A developer's own servers (`-narrator`, `-voice`,
`SOLAR_NARRATOR_URL`, `SOLAR_VOICE_URL`, e.g. from `Tools\local_ai\start_narrator.ps1`) take precedence, and the
host then starts nothing.

## Building the pack

```powershell
# 1. Game build (release)
& "C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe" -batchmode -quit -projectPath . `
  -executeMethod SolarMajesty.EditorTools.BuildCommands.BuildWindows -smBuildPath Builds\Release -smVersion 0.9.0

# 2. AI pack. Needs Tools\local_ai\.venv (python -m venv, pip install kokoro-onnx), Tools\local_ai\models,
#    and a llama.cpp release zip (llama-bNNNN-bin-win-cpu-x64.zip from github.com/ggml-org/llama.cpp/releases).
powershell -ExecutionPolicy Bypass -File Tools\local_ai\build_ai_pack.ps1 -OutDir Builds\AiPack -LlamaZip <zip> `
  -SourceOfferContact "<support email or URL>"

# 3a. Steam: stage the game with the pack beside the exe and upload
powershell -ExecutionPolicy Bypass -File Tools\steam\upload_to_steam.ps1 -GameDir Builds\Release\Windows `
  -AiDir Builds\AiPack\ai -AppId <APP_ID> -DepotId <DEPOT_ID> -SteamUser <build_account>

# 3b. Or zips for testers: with the pack, and (-NoAi) a small one without it, plus SHA256SUMS.txt
powershell -ExecutionPolicy Bypass -File Tools\release\package-windows.ps1 -GameDir Builds\Release\Windows `
  -AiDir Builds\AiPack\ai -NoAi
```

`build_ai_pack.ps1` copies Python from the venv's base install, so build it on a machine whose Python matches
the packages in the venv. `-NoChat` drops the 4B model (saves 2.5 GB), `-NoVoice` drops Kokoro and Python.
Set `-SourceOfferContact` to a real support address before a store build: it goes into the GPL-3.0 written offer.

The demo player ships without the pack (`upload_to_steam.ps1 -Demo` and `package-windows.ps1` both refuse it).

## Checking a build

1. Start the packaged game, turn on BOT LINES. Within about a minute `llama-server.exe` runs (Task Manager,
   below normal priority) and the chat panel stops saying "Loading the local model".
2. Turn on SPOKEN: a `python.exe` starts and bots speak their live lines.
3. Turn both off: after 30 seconds the processes are gone.
4. Kill `SolarMajesty.exe` from Task Manager with the models running: they go with it.
5. Start with `-noai`: the log says `[LocalAI] Local AI: off (-noai)` and nothing starts.
