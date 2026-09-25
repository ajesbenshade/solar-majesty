"""Local text-to-speech server for Solar Majesty's live character lines.

    Tools/audio/.venv/bin/python Tools/audio/voice_server.py [--port 8081]

Speaks lines that cannot be baked ahead of time: hero lines written by the local LLM narrator
(Docs/HERO_NARRATION.md) and Overseer lines with numbers in them. Same Kokoro-82M model, cast and
robot processing as the baked bank (render_voices.py), so live and baked lines sound like the
same character.

API (OpenAI-shaped, so the game's request is familiar):
    GET  /health              -> {"ok": true, "speakers": [...]}
    GET  /v1/models           -> {"data": [{"id": "kokoro"}]}
    POST /v1/audio/speech     {"input": "...", "voice": "hero.EngineerBot"} -> audio/wav (PCM16 mono)

`voice` is a cast.json key. Binds to 127.0.0.1 only; nothing here is meant to face a network.
"""

from __future__ import annotations

import argparse
import io
import json
import sys
import threading
import time
from collections import OrderedDict
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

import numpy as np
import soundfile as sf

from voice_common import load_cast, load_kokoro, speakable
from voice_fx import SAMPLE_RATE, process, synth_speed

MAX_BODY = 4096
MAX_CHARS = 240
CACHE_SIZE = 128


class Voices:
    def __init__(self) -> None:
        self.cast = load_cast()
        self.kokoro = load_kokoro()
        self.lock = threading.Lock()
        self.cache: OrderedDict[tuple[str, str], bytes] = OrderedDict()
        # First synthesis pays for graph warm-up; do it now so the first in-game line isn't late.
        self.speak("overseer", "Online.")

    def speak(self, speaker: str, text: str) -> bytes:
        key = (speaker, text)
        with self.lock:
            if key in self.cache:
                self.cache.move_to_end(key)
                return self.cache[key]
            entry = self.cast[speaker]
            samples, sr = self.kokoro.create(speakable(text), voice=entry["voice"],
                                             speed=synth_speed(entry), lang="en-us")
            audio = process(samples, sr, entry)
            buf = io.BytesIO()
            sf.write(buf, np.clip(audio, -1.0, 1.0), SAMPLE_RATE, format="WAV", subtype="PCM_16")
            wav = buf.getvalue()
            self.cache[key] = wav
            if len(self.cache) > CACHE_SIZE:
                self.cache.popitem(last=False)
            return wav


def make_handler(voices: Voices):
    class Handler(BaseHTTPRequestHandler):
        server_version = "SolarMajestyVoice/1"

        def log_message(self, fmt, *args):  # quieter than the default per-request stderr line
            pass

        def _json(self, code: int, payload: dict) -> None:
            body = json.dumps(payload).encode("utf-8")
            self.send_response(code)
            self.send_header("Content-Type", "application/json")
            self.send_header("Content-Length", str(len(body)))
            self.end_headers()
            self.wfile.write(body)

        def do_GET(self):
            if self.path == "/health":
                self._json(200, {"ok": True, "speakers": sorted(voices.cast)})
            elif self.path == "/v1/models":
                self._json(200, {"object": "list", "data": [{"id": "kokoro", "object": "model"}]})
            else:
                self._json(404, {"error": "not found"})

        def do_POST(self):
            if self.path != "/v1/audio/speech":
                return self._json(404, {"error": "not found"})
            length = int(self.headers.get("Content-Length") or 0)
            if length <= 0 or length > MAX_BODY:
                return self._json(413, {"error": "body too large"})
            try:
                req = json.loads(self.rfile.read(length).decode("utf-8"))
            except (ValueError, UnicodeDecodeError):
                return self._json(400, {"error": "bad json"})

            text = str(req.get("input") or "").strip()[:MAX_CHARS]
            speaker = str(req.get("voice") or "overseer")
            if not text:
                return self._json(400, {"error": "empty input"})
            if speaker not in voices.cast:
                return self._json(400, {"error": f"unknown voice '{speaker}'"})

            started = time.time()
            try:
                wav = voices.speak(speaker, text)
            except Exception as e:  # keep serving; one bad line shouldn't kill the game's voice
                print(f"[voice] synthesis failed for {speaker}: {e}", file=sys.stderr)
                return self._json(500, {"error": "synthesis failed"})
            self.send_response(200)
            self.send_header("Content-Type", "audio/wav")
            self.send_header("Content-Length", str(len(wav)))
            self.end_headers()
            self.wfile.write(wav)
            print(f"[voice] {speaker:<20} {time.time() - started:4.2f}s  {text[:70]}", flush=True)

    return Handler


def main() -> int:
    ap = argparse.ArgumentParser(description="Local Kokoro TTS for Solar Majesty")
    ap.add_argument("--port", type=int, default=8081)
    args = ap.parse_args()

    voices = Voices()
    server = ThreadingHTTPServer(("127.0.0.1", args.port), make_handler(voices))
    print(f"[voice] ready on http://127.0.0.1:{args.port} ({len(voices.cast)} speakers)", flush=True)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        server.server_close()
    return 0


if __name__ == "__main__":
    sys.exit(main())
