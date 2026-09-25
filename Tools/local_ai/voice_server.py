"""Local text-to-speech for Solar Majesty's hero voices (Kokoro-82M via kokoro-onnx).

Serves the OpenAI-style speech API the game uses, so Kokoro-FastAPI works too:

    POST /v1/audio/speech   {"input": "...", "voice": "am_michael", "speed": 1.0}  -> audio/wav
    GET  /v1/models         -> {"data": [{"id": "kokoro"}]}
    GET  /v1/audio/voices   -> {"voices": [...]}

    pip install kokoro-onnx
    python Tools/local_ai/voice_server.py --model kokoro-v1.0.onnx --voices voices-v1.0.bin

Binds to 127.0.0.1 only. Tools/local_ai/start_narrator.sh downloads the model and starts this.
"""

import argparse
import io
import json
import threading
import time
import wave
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

MAX_BODY = 16 * 1024
MAX_CHARS = 300


def to_wav(samples, rate):
    """Float samples (-1..1) → 16-bit mono PCM WAV bytes."""
    import numpy as np

    pcm = (np.clip(samples, -1.0, 1.0) * 32767.0).astype("<i2").tobytes()
    buf = io.BytesIO()
    with wave.open(buf, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(int(rate))
        w.writeframes(pcm)
    return buf.getvalue()


def build_handler(kokoro, voices, lock, verbose):
    default_voice = "am_michael" if "am_michael" in voices else sorted(voices)[0]

    class Handler(BaseHTTPRequestHandler):
        protocol_version = "HTTP/1.1"

        def log_message(self, fmt, *args):
            if verbose:
                super().log_message(fmt, *args)

        def _json(self, code, payload):
            body = json.dumps(payload).encode("utf-8")
            self.send_response(code)
            self.send_header("Content-Type", "application/json")
            self.send_header("Content-Length", str(len(body)))
            self.end_headers()
            self.wfile.write(body)

        def do_GET(self):
            if self.path in ("/v1/models", "/health"):
                self._json(200, {"object": "list", "data": [{"id": "kokoro", "object": "model"}]})
            elif self.path == "/v1/audio/voices":
                self._json(200, {"voices": sorted(voices)})
            else:
                self._json(404, {"detail": "not found"})

        def do_POST(self):
            if self.path != "/v1/audio/speech":
                self._json(404, {"detail": "not found"})
                return
            try:
                length = int(self.headers.get("Content-Length", "0"))
            except ValueError:
                length = 0
            if length <= 0 or length > MAX_BODY:
                self._json(413, {"detail": "bad body size"})
                return
            try:
                req = json.loads(self.rfile.read(length))
                text = str(req.get("input", "")).strip()[:MAX_CHARS]
                voice = str(req.get("voice") or default_voice)
                speed = float(req.get("speed", 1.0))
            except (ValueError, TypeError, AttributeError) as exc:
                self._json(400, {"detail": str(exc)})
                return
            if not text:
                self._json(400, {"detail": "empty input"})
                return
            if voice not in voices:
                voice = default_voice
            speed = min(2.0, max(0.5, speed))
            started = time.perf_counter()
            try:
                with lock:  # one synthesis at a time keeps latency predictable on CPU
                    samples, rate = kokoro.create(text, voice=voice, speed=speed, lang="en-us")
            except Exception:  # noqa: BLE001 - never crash the server on one bad line
                self._json(500, {"detail": "synthesis failed"})
                return
            body = to_wav(samples, rate)
            self.send_response(200)
            self.send_header("Content-Type", "audio/wav")
            self.send_header("Content-Length", str(len(body)))
            self.send_header("X-Synthesis-Ms", str(round((time.perf_counter() - started) * 1000)))
            self.end_headers()
            self.wfile.write(body)

    return Handler


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--model", default="kokoro-v1.0.onnx")
    ap.add_argument("--voices", default="voices-v1.0.bin")
    ap.add_argument("--port", type=int, default=8880)
    ap.add_argument("--verbose", action="store_true")
    args = ap.parse_args()

    from kokoro_onnx import Kokoro  # deferred so --help works without the package

    print(f"[voice] loading {args.model} ...", flush=True)
    kokoro = Kokoro(args.model, args.voices)
    voices = set(kokoro.get_voices())
    kokoro.create("Ready.", voice="am_michael" if "am_michael" in voices else sorted(voices)[0], lang="en-us")

    server = ThreadingHTTPServer(("127.0.0.1", args.port),
                                 build_handler(kokoro, voices, threading.Lock(), args.verbose))
    print(f"[voice] {len(voices)} voices; serving http://127.0.0.1:{args.port}/v1/audio/speech", flush=True)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass


if __name__ == "__main__":
    main()
