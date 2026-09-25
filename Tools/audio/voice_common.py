"""Shared bits for the voice bake: paths, cast, Kokoro, line keys."""

from __future__ import annotations

import json
import os
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPO = HERE.parent.parent
MODELS = HERE / "models"
MODEL_FILE = MODELS / "kokoro-v1.0.int8.onnx"
VOICES_FILE = MODELS / "voices-v1.0.bin"
MODEL_URLS = {
    MODEL_FILE: "https://github.com/thewh1teagle/kokoro-onnx/releases/download/model-files-v1.0/kokoro-v1.0.int8.onnx",
    VOICES_FILE: "https://github.com/thewh1teagle/kokoro-onnx/releases/download/model-files-v1.0/voices-v1.0.bin",
}
BANK_DIR = REPO / "Assets" / "Resources" / "Audio" / "Voices"
MANIFEST = BANK_DIR / "voices.json"

SPEAKER_PREFIXES = ("Grok — ", "Grok - ", "Grok: ")


def load_cast() -> dict:
    with open(HERE / "cast.json", encoding="utf-8") as f:
        return {k: v for k, v in json.load(f).items() if not k.startswith("_")}


def load_kokoro():
    missing = [p for p in (MODEL_FILE, VOICES_FILE) if not p.exists()]
    if missing:
        names = ", ".join(p.name for p in missing)
        raise SystemExit(f"Missing Kokoro model files ({names}). Run Tools/audio/setup.sh first.")
    # Keep onnxruntime from grabbing every core; the game is running beside it.
    os.environ.setdefault("OMP_NUM_THREADS", "4")
    from kokoro_onnx import Kokoro
    return Kokoro(str(MODEL_FILE), str(VOICES_FILE))


def normalize_line(text: str) -> str:
    """Mirror of VoiceBank.NormalizeLine in C#. Keep the two in lockstep."""
    t = (text or "").strip()
    for prefix in SPEAKER_PREFIXES:
        if t.startswith(prefix):
            t = t[len(prefix):]
            break
    out = []
    pending_space = False
    for ch in t.lower():
        if ("a" <= ch <= "z") or ("0" <= ch <= "9"):
            if pending_space and out:
                out.append(" ")
            out.append(ch)
            pending_space = False
        else:
            pending_space = True
    return "".join(out)


def line_key(text: str) -> str:
    """FNV-1a 32 of the normalised line, as 8 hex chars. Mirror of VoiceBank.LineKey."""
    h = 0x811C9DC5
    for b in normalize_line(text).encode("ascii"):
        h ^= b
        h = (h * 0x01000193) & 0xFFFFFFFF
    return f"{h:08x}"


def speakable(text: str) -> str:
    """Text as Kokoro should read it: no speaker prefix, and a few in-game abbreviations spelt out."""
    t = (text or "").strip()
    for prefix in SPEAKER_PREFIXES:
        if t.startswith(prefix):
            t = t[len(prefix):]
            break
    replacements = {
        " EU": " E U", "HAB": "hab", "ICE ": "ice ", "REG": "reg", " MET": " met",
        "Re-fab": "Ree-fab", "—": ", ", "Fobot": "Foe-bot",
    }
    for a, b in replacements.items():
        t = t.replace(a, b)
    return t
