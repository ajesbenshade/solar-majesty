# Hero narration (small local LLM)

Optional and off by default. Heroes mutter short, in-character lines about what they're doing, voiced by a small language model running on your machine. With no server running, the template flavour lines stay and nothing else changes.

**The model voices decisions; it never makes them.** `SpecialistBrain` still decides everything. The narrator gets only the facts of the moment (class, level, dominant trait, health, purse, the bounty, the player's written orders) and returns one line.

## Setup

Any **OpenAI-compatible** local server works. The game talks to `POST /v1/chat/completions` and checks the server is up via `GET /v1/models`.

| Server | Command | Game URL |
|---|---|---|
| llama.cpp | `llama-server -hf unsloth/Qwen3-1.7B-GGUF:Q4_K_M --port 8080 --jinja --reasoning-budget 0` | default (`http://127.0.0.1:8080`) |
| Ollama | `ollama run qwen3:1.7b` | `-narrator http://127.0.0.1:11434 -narrator-model qwen3:1.7b` |
| MLX (Apple Silicon) | `mlx_lm.server --model mlx-community/Qwen3-1.7B-4bit --port 8080` | default |
| LM Studio | load a model, start the server | `-narrator http://127.0.0.1:1234` |

Recommended model: **Qwen3-1.7B**, 4-bit (~1.1 GB), Apache-2.0. Any small instruct model works. The prompt asks Qwen3 not to think (`/no_think` plus `chat_template_kwargs.enable_thinking = false`), and llama.cpp's `--reasoning-budget 0` enforces it.

**Turn it on:** Settings → **HERO VOICES · LOCAL AI**, or `-narrator [url]`, or `SOLAR_NARRATOR_URL`. Set the model name with `-narrator-model` or `SOLAR_NARRATOR_MODEL` (default `local`; llama.cpp ignores it). The console prints `[Narrator] online at …` once the server answers.

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
