# Hero narration (small local LLM + text-to-speech)

Optional and off by default. Heroes mutter short, in-character lines about what they're doing. A small language model on your machine writes the line, and a local text-to-speech model (Kokoro-82M) can also **speak it aloud**. Machines talk through a helmet-radio filter; the human Medic speaks clean. With no servers running, the template flavour lines stay and nothing else changes.

**The model voices decisions; it never makes them.** `SpecialistBrain` still decides everything. The narrator gets only the facts of the moment (class, level, dominant trait, health, purse, the bounty, the player's written orders) and returns one line.

## Quick start (one command)

```bash
# macOS / Linux
Tools/local_ai/start_narrator.sh              # LLM + voices, then launches Builds/macOS or Builds/Linux if present
Tools/local_ai/start_narrator.sh --no-game    # servers only; play in the Unity editor (see below)
Tools/local_ai/start_narrator.sh --no-speech  # text lines only, no spoken voices
Tools/local_ai/start_narrator.sh --laya       # also starts the Laya decision model (Apple Silicon)
```

```powershell
# Windows
powershell -ExecutionPolicy Bypass -File Tools\local_ai\start_narrator.ps1          # launches Builds\WindowsPlaytest or Builds\Windows
powershell -ExecutionPolicy Bypass -File Tools\local_ai\start_narrator.ps1 -NoGame    # servers only (Unity editor)
powershell -ExecutionPolicy Bypass -File Tools\local_ai\start_narrator.ps1 -NoSpeech  # text lines only
```

The launcher uses the first backend it finds: **llama.cpp** (`llama-server`), then **Ollama**, then **MLX** on Apple Silicon, then a self-contained **Python** fallback that installs `llama-cpp-python` into `Tools/local_ai/.venv` and downloads the model to `Tools/local_ai/models/` once. It waits until the server answers, starts the built game with `-narrator …`, and stops the server when you quit (Ctrl+C or close the window). Logs go to `Tools/local_ai/narrator.log`.

For voices, the launcher also installs `kokoro-onnx` into the same venv, downloads the Kokoro model (~340 MB, resumable) and starts `Tools/local_ai/voice_server.py` on port 8880. The game is launched with `-voice …` too.

In the Unity editor, command-line flags don't apply. Run with `--no-game`, then turn on **Settings → HERO LINES · LOCAL LLM** (text, `http://127.0.0.1:8080`) and **SPOKEN · LOCAL TTS** (voices, `http://127.0.0.1:8880`). SPOKEN also switches the text lines on, since it needs something to say. With Ollama (port 11434), set `SOLAR_NARRATOR_URL=http://127.0.0.1:11434` and `SOLAR_NARRATOR_MODEL=qwen3:1.7b` before starting Unity.

## Setup (manual)

Any **OpenAI-compatible** local server works. The game talks to `POST /v1/chat/completions` and checks the server is up via `GET /v1/models`.

| Server | Command | Game URL |
|---|---|---|
| llama.cpp | `llama-server -hf unsloth/Qwen3-1.7B-GGUF:Q4_K_M --port 8080 --jinja --reasoning-budget 0` | default (`http://127.0.0.1:8080`) |
| Ollama | `ollama run qwen3:1.7b` | `-narrator http://127.0.0.1:11434 -narrator-model qwen3:1.7b` |
| MLX (Apple Silicon) | `mlx_lm.server --model mlx-community/Qwen3-1.7B-4bit --port 8080` | default |
| LM Studio | load a model, start the server | `-narrator http://127.0.0.1:1234` |

Recommended model: **Qwen3-1.7B**, 4-bit (~1.1 GB), Apache-2.0. Any small instruct model works. The prompt asks Qwen3 not to think (`/no_think` plus `chat_template_kwargs.enable_thinking = false`), and llama.cpp's `--reasoning-budget 0` enforces it.

**Turn it on:** Settings → **HERO LINES · LOCAL LLM**, or `-narrator [url]`, or `SOLAR_NARRATOR_URL`. Set the model name with `-narrator-model` or `SOLAR_NARRATOR_MODEL` (default `local`; llama.cpp ignores it). The console prints `[Narrator] online at …` once the server answers.

## Spoken lines (TTS)

- **Server:** any server with the OpenAI-style `POST /v1/audio/speech` returning WAV. That's the bundled `Tools/local_ai/voice_server.py` (kokoro-onnx, CPU) or [Kokoro-FastAPI](https://github.com/remsky/Kokoro-FastAPI) (`docker run -p 8880:8880 ghcr.io/remsky/kokoro-fastapi-cpu`). Enable with the SPOKEN chip, `-voice [url]` or `SOLAR_VOICE_URL`.
- **Casting** (`HeroSpeech.VoiceFor`): two Kokoro voices per class, picked per hero, so the colony isn't one voice. Workaholics speak faster and lazy heroes drawl. Mechs get a heavy radio treatment, other bots a lighter one; the Medic is human and clean.
- **Playback** (`HeroSpeaker`): the WAV is decoded and filtered off the main thread, then played at the hero's position on the **Voice** bus (it follows your volume settings and mutes with them) and ducks music and ambience while the line plays. One line at a time; lines longer than 5 s are skipped.
- **Intelligibility check:** real Kokoro output passed through the game's robot filter was transcribed by Whisper exactly like the clean audio (e.g. *"450 for a den, I'll claim the rest."*). Listen: `Docs/ReviewEvidence/2026-09-25-hero-voice-mech.wav` (Defense Mech, radio voice) and `2026-09-25-hero-voice-medic.wav` (Medic, clean).
- **Latency:** Kokoro took ~0.8–1.6 s per line on a 4-core container CPU, on top of the LLM line (~1.5–4 s). Heroes speak a moment after they act, which reads as a mutter.

## When heroes speak

A laptop-sized model manages roughly one line every 1–4 s, so `NarrationScheduler` rations its time:

- One request in flight, with a 1.5 s gap between requests.
- **Big moments** (claiming a bounty, fleeing, levelling up, being refused by your written orders) are voiced for any hero, with a 6 s cooldown per hero.
- **Routine moments** (wandering, resting, workshop shifts) are voiced only for the **selected** hero, with a 7 s cooldown.
- Lines replace the hero's flavour line (roster and inspect panel). Level-ups, and big moments for the selected hero, are also posted to the overseer log.
- A line that arrives after the hero has already changed their mind is dropped.

## Safety of the output

`HeroNarration.Sanitize` keeps one printable line of at most 64 characters. It strips markup (the HUD uses IMGUI rich text), wrapping quotes and emoji. It rejects AI boilerplate and replies where the model reasoned about the prompt instead of speaking a line.

## Real sample (Qwen3-1.7B Q4_K_M, CPU, via the game's own request/parse code)

| Moment | Line |
|---|---|
| Greedy mech claims a 450-credit den | *450 for a den? I'll claim the rest.* |
| Cowardly scout claims a survey | *Survey done? I'd take a nap.* |
| Engineer refused by "stay away unless you are level 9 or higher" | *I've got a job to do. And I'm not leaving.* |
| Fearless sentinel levels up | *I'm six! I'll fight till the end—then take what's mine.* |
| Workaholic engineer on a workshop shift | *Welding while tired? I'll crank the torque.* |
| Lazy harvester resting | *Nap first. Then the inn.* |

Latency was 1.5–4 s per line on the container's CPU; Apple Silicon or a GPU is much faster.

## Code

- `Systems/Narration/HeroNarration.cs`: the moment facts, prompt with few-shot examples, request builder, reply parser/sanitiser and `NarrationScheduler`. Pure; tested by `NarrationTests`.
- `Runtime/Narration/HeroNarrator.cs`: the local server link (off the main thread, no proxy, probe while offline), plus `Report()` called from `SpecialistAgent` (decision changes, level-ups) and `GameLoop` (orders refusals).
- `Systems/LocalJson.cs`: the shared tiny JSON reader, also used by the Laya link.
