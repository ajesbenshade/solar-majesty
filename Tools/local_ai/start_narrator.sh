#!/usr/bin/env bash
# Starts a local LLM for Solar Majesty's hero voices, then (optionally) the game.
#
#   Tools/local_ai/start_narrator.sh            # server + launch the built game if found
#   Tools/local_ai/start_narrator.sh --no-game  # server only (play in the Unity editor)
#   Tools/local_ai/start_narrator.sh --laya     # also start the Laya decision model (Apple Silicon)
#   Tools/local_ai/start_narrator.sh --no-voices  # text only: skip the spoken-line TTS server
#
# Uses the first backend it finds: llama.cpp (llama-server) → Ollama → MLX (Apple Silicon)
# → a self-contained Python fallback (llama-cpp-python, CPU). Model: Qwen3-1.7B, 4-bit (~1.1 GB).
# If Tools/audio is set up (Tools/audio/setup.sh), also starts the Kokoro voice server on :8081
# so LLM lines are spoken aloud. Ctrl+C stops everything this script started.
# See Docs/HERO_NARRATION.md and Docs/AUDIO.md.
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$HERE/../.." && pwd)"
PORT="${NARRATOR_PORT:-8080}"
MODELS="$HERE/models"
VENV="$HERE/.venv"
GGUF_REPO="unsloth/Qwen3-1.7B-GGUF"
GGUF_FILE="Qwen3-1.7B-Q4_K_M.gguf"
MLX_MODEL="mlx-community/Qwen3-1.7B-4bit"
OLLAMA_MODEL="qwen3:1.7b"

LAUNCH_GAME=1
WITH_LAYA=0
WITH_VOICES=1
VOICE_PORT="${VOICE_PORT:-8081}"
for arg in "$@"; do
  case "$arg" in
    --no-game) LAUNCH_GAME=0 ;;
    --laya) WITH_LAYA=1 ;;
    --no-voices) WITH_VOICES=0 ;;
    -h|--help) sed -n '2,15p' "$0"; exit 0 ;;
    *) echo "unknown option: $arg" >&2; exit 2 ;;
  esac
done

PIDS=()
cleanup() { for p in "${PIDS[@]:-}"; do [ -n "$p" ] && kill "$p" 2>/dev/null || true; done; }
trap cleanup EXIT
trap 'cleanup; exit 130' INT TERM

# stderr, so helpers can return values on stdout.
say() { printf '\033[1;33m[narrator]\033[0m %s\n' "$*" >&2; }

venv_python() {
  if [ ! -x "$VENV/bin/python" ]; then
    say "creating Python environment in $VENV"
    python3 -m venv "$VENV"
    "$VENV/bin/python" -m pip install --quiet --upgrade pip
  fi
  echo "$VENV/bin/python"
}

fetch_gguf() {
  mkdir -p "$MODELS"
  local dest="$MODELS/$GGUF_FILE"
  if [ ! -s "$dest" ]; then
    say "downloading $GGUF_FILE (~1.1 GB, once)"
    curl -fL --progress-bar -o "$dest.part" "https://huggingface.co/$GGUF_REPO/resolve/main/$GGUF_FILE"
    mv "$dest.part" "$dest"
  fi
  echo "$dest"
}

URL="http://127.0.0.1:$PORT"
MODEL_NAME="local"

if command -v llama-server >/dev/null 2>&1; then
  say "backend: llama.cpp (llama-server) on :$PORT"
  llama-server -hf "$GGUF_REPO:Q4_K_M" --port "$PORT" --host 127.0.0.1 --jinja --reasoning-budget 0 >"$HERE/narrator.log" 2>&1 &
  PIDS+=($!)
elif command -v ollama >/dev/null 2>&1; then
  URL="http://127.0.0.1:11434"
  MODEL_NAME="$OLLAMA_MODEL"
  say "backend: Ollama ($OLLAMA_MODEL)"
  if ! curl -fs "$URL/v1/models" >/dev/null 2>&1; then
    ollama serve >"$HERE/narrator.log" 2>&1 &
    PIDS+=($!)
    sleep 2
  fi
  ollama pull "$OLLAMA_MODEL"
elif [ "$(uname -s)" = "Darwin" ] && [ "$(uname -m)" = "arm64" ] && command -v python3 >/dev/null 2>&1; then
  PY="$(venv_python)"
  "$PY" -c "import mlx_lm" 2>/dev/null || { say "installing mlx-lm"; "$PY" -m pip install --quiet mlx-lm; }
  say "backend: MLX ($MLX_MODEL) on :$PORT"
  "$PY" -m mlx_lm.server --model "$MLX_MODEL" --port "$PORT" --host 127.0.0.1 >"$HERE/narrator.log" 2>&1 &
  PIDS+=($!)
elif command -v python3 >/dev/null 2>&1; then
  PY="$(venv_python)"
  if ! "$PY" -c "import llama_cpp.server" 2>/dev/null; then
    say "installing llama-cpp-python (CPU)"
    "$PY" -m pip install --quiet --only-binary=:all: llama-cpp-python \
      --extra-index-url https://abetlen.github.io/llama-cpp-python/whl/cpu \
      || "$PY" -m pip install --quiet llama-cpp-python
    "$PY" -m pip install --quiet uvicorn fastapi sse-starlette starlette-context pydantic-settings
  fi
  GGUF="$(fetch_gguf)"
  say "backend: llama-cpp-python on :$PORT"
  "$PY" -m llama_cpp.server --model "$GGUF" --host 127.0.0.1 --port "$PORT" --n_ctx 2048 >"$HERE/narrator.log" 2>&1 &
  PIDS+=($!)
else
  echo "No backend found. Install one of: llama.cpp (brew install llama.cpp), Ollama, or Python 3." >&2
  exit 1
fi

say "waiting for $URL (first run loads the model)…"
for _ in $(seq 1 180); do
  if curl -fs "$URL/v1/models" >/dev/null 2>&1; then break; fi
  if [ ${#PIDS[@]} -gt 0 ] && ! kill -0 "${PIDS[0]}" 2>/dev/null; then
    echo "The model server exited. Last log lines:" >&2; tail -20 "$HERE/narrator.log" >&2 || true; exit 1
  fi
  sleep 2
done
curl -fs "$URL/v1/models" >/dev/null || { echo "Server did not come up; see $HERE/narrator.log" >&2; exit 1; }
say "hero voices online at $URL (model: $MODEL_NAME)"

if [ "$WITH_LAYA" = 1 ]; then
  if [ "$(uname -s)" = "Darwin" ] && [ "$(uname -m)" = "arm64" ]; then
    PY="$(venv_python)"
    "$PY" -c "import laya_mlx" 2>/dev/null || { say "installing laya-mlx"; "$PY" -m pip install --quiet laya-mlx; }
    say "starting Laya decisions on :8765"
    "$PY" "$ROOT/Tools/laya_sidecar/laya_mlx_server.py" --port 8765 >"$HERE/laya.log" 2>&1 &
    PIDS+=($!)
  else
    say "Laya MLX needs Apple Silicon; on this machine run: pip install \"laya[serve]\" && LAYA_HOST=127.0.0.1 LAYA_PORT=8765 laya-serve"
  fi
fi

GAME_ARGS=(-narrator "$URL" -narrator-model "$MODEL_NAME")
[ "$WITH_LAYA" = 1 ] && GAME_ARGS+=(-laya)

if [ "$WITH_VOICES" = 1 ]; then
  AUDIO="$ROOT/Tools/audio"
  VOICE_URL="http://127.0.0.1:$VOICE_PORT"
  if [ -x "$AUDIO/.venv/bin/python" ] && [ -f "$AUDIO/models/kokoro-v1.0.int8.onnx" ]; then
    say "starting spoken lines (Kokoro TTS) on :$VOICE_PORT"
    (cd "$AUDIO" && exec .venv/bin/python voice_server.py --port "$VOICE_PORT") >"$HERE/voice.log" 2>&1 &
    PIDS+=($!)
    for _ in $(seq 1 60); do
      curl -fs "$VOICE_URL/health" >/dev/null 2>&1 && break
      sleep 1
    done
    if curl -fs "$VOICE_URL/health" >/dev/null 2>&1; then
      say "spoken lines online at $VOICE_URL"
      GAME_ARGS+=(-voice-server "$VOICE_URL")
    else
      say "voice server did not come up; lines stay text (see $HERE/voice.log)"
    fi
  else
    say "spoken LLM lines: run Tools/audio/setup.sh once (baked barks work without it)"
  fi
fi

if [ "$LAUNCH_GAME" = 1 ]; then
  if [ -d "$ROOT/Builds/macOS/SolarMajesty.app" ]; then
    say "launching Builds/macOS/SolarMajesty.app"
    open -W "$ROOT/Builds/macOS/SolarMajesty.app" --args "${GAME_ARGS[@]}"
    exit 0
  elif [ -x "$ROOT/Builds/Linux/SolarMajesty.x86_64" ]; then
    say "launching Builds/Linux/SolarMajesty.x86_64"
    "$ROOT/Builds/Linux/SolarMajesty.x86_64" "${GAME_ARGS[@]}"
    exit 0
  fi
  say "no built game found (Solar Majesty → Build → macOS / Linux)."
fi

say "Playing in the Unity editor? Settings → HERO VOICES · LOCAL AI (it uses $URL)."
[ "$URL" != "http://127.0.0.1:8080" ] && say "Editor note: the chip uses :8080 — set SOLAR_NARRATOR_URL=$URL before starting Unity, or use llama.cpp / MLX."
say "Server running. Ctrl+C to stop."
if [ ${#PIDS[@]} -gt 0 ]; then
  wait "${PIDS[0]}" || true
  say "model server stopped."
else
  # Server was already running (e.g. Ollama): idle until Ctrl+C. Background sleep keeps signals prompt.
  while true; do sleep 3600 & wait $!; done
fi
