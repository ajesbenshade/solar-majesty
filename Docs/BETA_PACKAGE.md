# Beta package (game + bundled AI)

Testers unzip one file and double-click `SolarMajesty.exe`. The game finds the `ai` folder next to
it and starts the models itself. No Python, no Ollama, no installer, no internet.

## What is in `ai\`

| Folder | What | Used for |
|---|---|---|
| `llama\` | llama.cpp CPU build (`llama-server.exe` + ggml DLLs) | runs the two Qwen models |
| `models\` | `Qwen3-1.7B-Q4_K_M.gguf` (1.1 GB), `Qwen3-4B-Instruct-2507-Q4_K_M.gguf` (2.5 GB), `kokoro-v1.0.onnx` + `voices-v1.0.bin` (0.35 GB) | hero lines, hero chat, speech |
| `python\` | portable Python 3.12 with kokoro-onnx, onnxruntime, numpy, phonemizer | Kokoro speech server and the model router |
| `scripts\` | `voice_server.py`, `model_router.py` | the two Python services |
| `THIRD-PARTY-NOTICES.txt` | licenses (Apache-2.0, MIT, BSD, PSF, GPL-3.0 for phonemizer/espeak-ng) | |

Total about 3.9 GB. Laya is not included (it needs PyTorch and is slow on CPU; the game's own brain
makes the decisions). The docs for it are in [LAYA_LOCAL_AI.md](LAYA_LOCAL_AI.md).

## How the game uses it

`LocalAiHost` (Runtime/Narration) runs before the first scene loads:

1. Finds `ai\` next to the exe (or `SOLAR_AI_DIR`). Does nothing if it is missing.
2. `LocalAiPlan` (Systems/LocalAi, unit-tested) picks what to run from the files present and the PC's RAM:
   under 6 GB nothing; 6–11 GB lines + speech; 11 GB and up lines, chat and speech.
3. Starts each process hidden on a free loopback port, below normal priority, logging to
   `%USERPROFILE%\AppData\LocalLow\SolarMajesty\Solar Majesty\ai\*.log`.
4. Sets `SOLAR_NARRATOR_URL`, `SOLAR_NARRATOR_MODEL`, `SOLAR_CHAT_MODEL` and `SOLAR_VOICE_URL`, which the
   narrator, chat and speaker already read. They probe until the servers answer, so heroes use the written
   lines for the first moments and switch to live ones.
5. Ties the processes to the game with a Windows job object: if the game crashes or is killed, they die too.

Off with `-noai` or `SOLAR_NO_AI=1`. A developer's own servers (`-narrator`, `-voice`, `SOLAR_NARRATOR_URL`,
`SOLAR_VOICE_URL`, e.g. from `Tools\local_ai\start_narrator.ps1`) take precedence; the editor never starts the
pack unless `SOLAR_AI_DIR` is set.

## Building a package

```powershell
# 1. Game build (non-development)
Unity -batchmode -quit -projectPath . -executeMethod SolarMajesty.EditorTools.BuildCommands.BuildWindows -smBuildPath Builds/BetaGame

# 2. AI pack. Needs Tools\local_ai\.venv (python -m venv, pip install kokoro-onnx), Tools\local_ai\models,
#    and a llama.cpp release zip (llama-bNNNN-bin-win-cpu-x64.zip from github.com/ggml-org/llama.cpp/releases).
powershell -ExecutionPolicy Bypass -File Tools\local_ai\build_ai_pack.ps1 -OutDir Builds\Beta\stage -LlamaZip <zip>

# 3. Zips: Full (game + ai) and Lite (game only), with checksums
powershell -ExecutionPolicy Bypass -File Tools\package_beta.ps1 -GameDir Builds\BetaGame\Windows -AiDir Builds\Beta\stage\ai
```

`build_ai_pack.ps1` copies Python from the venv's base install, so build it on a machine whose Python matches
the packages in the venv. `-NoChat` drops the 4B model (saves 2.5 GB), `-NoVoice` drops Kokoro and Python.

## Sharing

Upload `SolarMajesty-Beta-<date>-Full.zip` (about 4 GB) and, for testers with slow connections, the Lite zip
(about 0.2 GB: baked lines only, no live AI). Paste the `.sha256.txt` contents next to the links.

OneDrive: a free personal account has 5 GB, so the two zips together will not fit. Microsoft 365 or a work
account has plenty.
