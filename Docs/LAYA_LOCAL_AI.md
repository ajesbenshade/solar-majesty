# Local Laya model for heroes and mobs

Optional. Off by default. When it's off, or the server is unreachable, the game behaves exactly as before.

[Laya](https://github.com/NandhaKishorM/laya) is an open-weight "typed decision" model (Apache-2.0, Convai Innovations). It doesn't generate text. You give it a state and a question with named options, and in one forward pass it returns a probability for each option. [laya-mlx](https://github.com/mizorewww/laya-mlx) is an independent port to Apple Silicon: about 7–13 ms per question, under 1 GB of memory, fully local.

## How it plugs in

Laya picks between options that the utility brain has already allowed. It never adds options. This is the split the laya-mlx Snake demo uses: the model proposes, and rules guard. The design pillars still hold: no direct control, and `SpecialistBrain` is the only source of decisions.

```
SpecialistBrain.CollectOptions  ──► legal options (Evaluate's pick is always #0)
        │                              │
        │ forced? (panic flee,         ▼
        │  exhausted) → utility    LayaHeroPolicy → POST /v1/systemone (async, off-thread)
        ▼                              │
  LayaHeroDriver ◄──── answer next think tick (~0.5 s) ─┘
        │  pick still legal and p ≥ minProbability → use it (held ≤ 4 s)
        └─ else → utility pick
```

| Piece | Where | Role |
|---|---|---|
| `SpecialistBrain.CollectOptions` | Systems | Every option the gates allow: bounties that pass the greed gate (top 3), hunt, repair, rest, wander. Reuses the frozen scorers. `Evaluate` is unchanged. |
| `LayaProtocol` | Systems/Laya | Builds request JSON and parses responses. Pure C#. |
| `LayaHeroPolicy` / `LayaMobPolicy` | Systems/Laya | State text, option labels, and mapping answers back to decisions. |
| `LayaBridge` | Runtime/Laya | `HttpClient` against localhost, probes `/health`, caps requests in flight, tracks latency and override counts. |
| `LayaHeroDriver` | Runtime/Laya | Per-specialist glue called from `SpecialistAgent.TickThink`. |
| `DustStalkerAgent.TickLaya` | Runtime/Threat | Mobs ask for a stance every ~2 s: `raid` (the scripted role), `ambush` (collectors only) or `prowl` (no raids). Each stance is a subset of the scripted role, so Laya can make a mob warier but never stronger. |

## Run it

**macOS on Apple Silicon (laya-mlx):**

```bash
pip install laya-mlx
python Tools/laya_sidecar/laya_mlx_server.py            # add --optimize for compile + prefix cache
```

**Windows / Linux (upstream Laya, PyTorch; CUDA if available):**

```bash
pip install "laya[serve]"
LAYA_HOST=127.0.0.1 LAYA_PORT=8765 LAYA_MODELS=english laya-serve
# PowerShell: $env:LAYA_HOST="127.0.0.1"; $env:LAYA_PORT="8765"; $env:LAYA_MODELS="english"; laya-serve
```

Both servers use the same `POST /v1/systemone` / `GET /health` protocol, so the game doesn't care which one is running. The first run downloads the checkpoint from Hugging Face. After that, everything runs offline.

**Turn it on in the game** (any one of these):

- Command line: `SolarMajesty.exe -laya`, or `-laya http://127.0.0.1:9000`
- Environment variable: `SOLAR_LAYA_URL=http://127.0.0.1:8765`
- PlayerPref: `SM_Set_LayaAI = 1` (uses the default URL)

The console prints `[Laya] online at …` once the server answers. Decision logs from Laya picks end in `[laya]`, and `SpecialistAgent.LastDecisionFromLaya` exposes the same thing to the HUD. `LayaBridge` has inspector fields for the confidence floor (`minProbability`, 0.35), requests in flight (6), timeout (1 s) and the probe interval.

## Budget

At 20 heroes thinking every 0.4–0.6 s, plus mobs every ~2 s, that's about 40–50 questions per second. The M3 Max figures in the laya-mlx README are 75 decisions/s on Snake and 395 questions/s batched on the multilingual checkpoint, so there's headroom. If the server falls behind, the in-flight cap drops the extra requests, and those agents simply keep using the utility brain.

## Limits

- MLX runs on Apple Silicon only. Windows playtest builds need the PyTorch server, which is slower on CPU. Sentis/ONNX in-engine inference (`com.unity.ai.inference` is already in the manifest, and upstream ships an ONNX agent) would remove the sidecar entirely, but it needs a C# ModernBERT tokenizer. That's a follow-up.
- The checkpoints are general typed-decision models, not trained on this game. Picks follow the personality text, so treat them as flavour, not balance. The utility gates keep the outcome within tuned limits.
- Answers apply one think tick late, by design.
