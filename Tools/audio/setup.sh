#!/usr/bin/env bash
# One-time setup for Tools/audio: Python env + Kokoro-82M TTS weights (Apache-2.0, ~120 MB).
# Needed only to re-bake audio or run the live voice server; the game ships the baked files.
set -euo pipefail
cd "$(dirname "$0")"

if [ ! -x .venv/bin/python ]; then
  if command -v uv >/dev/null 2>&1; then
    uv venv --python 3.12 .venv
  else
    python3 -m venv .venv
  fi
fi
if command -v uv >/dev/null 2>&1; then
  uv pip install --python .venv/bin/python -r requirements.txt
else
  .venv/bin/python -m pip install -r requirements.txt
fi

mkdir -p models
fetch() {
  local url=$1 file=$2 sha=$3
  if [ -f "models/$file" ] && echo "$sha  models/$file" | shasum -a 256 -c --status; then
    return
  fi
  echo "Downloading $file …"
  curl -fL --progress-bar -o "models/$file.part" "$url"
  echo "$sha  models/$file.part" | shasum -a 256 -c --status || { echo "Checksum mismatch for $file" >&2; rm -f "models/$file.part"; exit 1; }
  mv "models/$file.part" "models/$file"
}
base=https://github.com/thewh1teagle/kokoro-onnx/releases/download/model-files-v1.0
fetch "$base/kokoro-v1.0.int8.onnx" kokoro-v1.0.int8.onnx 6e742170d309016e5891a994e1ce1559c702a2ccd0075e67ef7157974f6406cb
fetch "$base/voices-v1.0.bin" voices-v1.0.bin bca610b8308e8d99f32e6fe4197e7ec01679264efed0cac9140fe9c29f1fbf7d
echo "Tools/audio ready."
