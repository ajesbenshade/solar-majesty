#!/usr/bin/env bash
# Starts Solar Majesty's local AI for hero voices — a small LLM that writes each hero's line and a
# Kokoro text-to-speech server that speaks it — then (optionally) the game.
#
#   Tools/local_ai/start_narrator.sh              # LLM + voices + launch the built game if found
#   Tools/local_ai/start_narrator.sh --no-game    # servers only (play in the Unity editor)
#   Tools/local_ai/start_narrator.sh --no-speech  # text lines only, no spoken voices
#   Tools/local_ai/start_narrator.sh --laya       # also start the Laya decision model (Apple Silicon)
#   Tools/local_ai/start_narrator.sh --chat4b     # Ollama: also fetch Qwen3-4B-Instruct (~2.5 GB) for conversations
#
# LLM: first backend found of llama.cpp (llama-server) → Ollama → MLX (Apple Silicon) → a
# self-contained Python fallback (llama-cpp-python, CPU); model Qwen3-1.7B 4-bit (~1.1 GB).
# Voices: Kokoro-82M via kokoro-onnx in a local venv (~340 MB, downloaded once), port 8880.
# Ctrl+C stops everything this script started. See Docs/HERO_NARRATION.md.
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
# Conversation model; the game uses it for chat when the server lists it. Not the plain qwen3:4b tag (thinking-only).
OLLAMA_CHAT_MODEL="qwen3:4b-instruct-2507-q4_K_M"

VOICE_PORT="${VOICE_PORT:-8880}"
KOKORO_BASE="https://github.com/thewh1teagle/kokoro-onnx/releases/download/model-files-v1.0"

LAUNCH_GAME=1
WITH_LAYA=0
WITH_SPEECH=1
WITH_CHAT4B=0
for arg in "$@"; do
  case "$arg" in
    --no-game) LAUNCH_GAME=0 ;;
    --laya) WITH_LAYA=1 ;;
    --no-speech) WITH_SPEECH=0 ;;
    --chat4b) WITH_CHAT4B=1 ;;
    -h|--help) sed -n "2,$(( $(grep -n '^set -euo' "$0" | cut -d: -f1) - 1 ))p" "$0"; exit 0 ;;
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

# Resumable download with retries: big model files over flaky links should not restart from zero.
download() {
  local url="$1" dest="$2" label="$3"
  [ -s "$dest" ] && return 0
  mkdir -p "$(dirname "$dest")"
  say "downloading $label (once)"
  for attempt in 1 2 3 4 5; do
    if curl -fL -C - --progress-bar -o "$dest.part" "$url"; then
      mv "$dest.part" "$dest"
      return 0
    fi
    say "download interrupted, resuming ($attempt/5)…"
    sleep 3
  done
  echo "Could not download $url" >&2
  return 1
}

fetch_gguf() {
  local dest="$MODELS/$GGUF_FILE"
  download "https://huggingface.co/$GGUF_REPO/resolve/main/$GGUF_FILE" "$dest" "$GGUF_FILE (~1.1 GB)"
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
    # Keep the model loaded between lines: a reload costs seconds on the next reply.
    OLLAMA_KEEP_ALIVE=30m ollama serve >"$HERE/narrator.log" 2>&1 &
    PIDS+=($!)
    sleep 2
  fi
  ollama pull "$OLLAMA_MODEL"
  [ "$WITH_CHAT4B" = 1 ] && ollama pull "$OLLAMA_CHAT_MODEL"
  # Load them now so the first hero line is not the one that pays for loading.
  for m in "$OLLAMA_MODEL" $([ "$WITH_CHAT4B" = 1 ] && echo "$OLLAMA_CHAT_MODEL"); do
    curl -fs "$URL/api/generate" -d "{\"model\":\"$m\",\"keep_alive\":\"30m\"}" >/dev/null 2>&1 || true
  done
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

VOICE_URL="http://127.0.0.1:$VOICE_PORT"
if [ "$WITH_SPEECH" = 1 ]; then
  if curl -fs "$VOICE_URL/v1/models" >/dev/null 2>&1; then
    say "voices already running at $VOICE_URL"
  elif command -v python3 >/dev/null 2>&1; then
    PY="$(venv_python)"
    "$PY" -c "import kokoro_onnx" 2>/dev/null || { say "installing kokoro-onnx (text-to-speech)"; "$PY" -m pip install --quiet kokoro-onnx; }
    download "$KOKORO_BASE/kokoro-v1.0.onnx" "$MODELS/kokoro-v1.0.onnx" "Kokoro voice model (~310 MB)"
    download "$KOKORO_BASE/voices-v1.0.bin" "$MODELS/voices-v1.0.bin" "Kokoro voices (~27 MB)"
    say "starting voices on :$VOICE_PORT"
    "$PY" "$HERE/voice_server.py" --model "$MODELS/kokoro-v1.0.onnx" --voices "$MODELS/voices-v1.0.bin" \
      --port "$VOICE_PORT" >"$HERE/voice.log" 2>&1 &
    PIDS+=($!)
    for _ in $(seq 1 60); do curl -fs "$VOICE_URL/v1/models" >/dev/null 2>&1 && break; sleep 2; done
    if curl -fs "$VOICE_URL/v1/models" >/dev/null 2>&1; then
      say "voices online at $VOICE_URL"
    else
      say "voices did not start (see $HERE/voice.log) — continuing with text lines only"
      WITH_SPEECH=0
    fi
  else
    say "voices need Python 3 — continuing with text lines only"
    WITH_SPEECH=0
  fi
fi

GAME_ARGS=(-narrator "$URL" -narrator-model "$MODEL_NAME")
[ "$WITH_SPEECH" = 1 ] && GAME_ARGS+=(-voice "$VOICE_URL")
[ "$WITH_LAYA" = 1 ] && GAME_ARGS+=(-laya)

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

if [ "$WITH_SPEECH" = 1 ]; then
  say "Playing in the Unity editor? Settings → HERO LINES · LOCAL LLM and SPOKEN · LOCAL TTS (uses $URL and $VOICE_URL)."
else
  say "Playing in the Unity editor? Settings → HERO LINES · LOCAL LLM (uses $URL)."
fi
[ "$URL" != "http://127.0.0.1:8080" ] && say "Editor note: the chip uses :8080 — set SOLAR_NARRATOR_URL=$URL before starting Unity, or use llama.cpp / MLX."
say "Server running. Ctrl+C to stop."
if [ ${#PIDS[@]} -gt 0 ]; then
  wait "${PIDS[0]}" || true
  say "model server stopped."
else
  # Server was already running (e.g. Ollama): idle until Ctrl+C. Background sleep keeps signals prompt.
  while true; do sleep 3600 & wait $!; done
fi
