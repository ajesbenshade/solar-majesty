"""Local Laya-MLX decision server for Solar Majesty (Apple Silicon).

Serves the same ``POST /v1/systemone`` + ``GET /health`` wire protocol as upstream
``laya-serve``, so the Unity ``LayaBridge`` talks to either unchanged. Stdlib HTTP only;
the one dependency is ``laya-mlx``.

    pip install laya-mlx
    python Tools/laya_sidecar/laya_mlx_server.py            # English 421M checkpoint
    python Tools/laya_sidecar/laya_mlx_server.py --model aac6fef/laya-multilingual-mlx

Binds to 127.0.0.1 only: the game is the sole client.
"""

import argparse
import json
import threading
import time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

MAX_BODY_BYTES = 256 * 1024
MAX_QUESTIONS = 16


def build_handler(agent, lock, verbose):
    class Handler(BaseHTTPRequestHandler):
        protocol_version = "HTTP/1.1"

        def log_message(self, fmt, *args):  # quiet by default: the game asks many times a second
            if verbose:
                super().log_message(fmt, *args)

        def _send(self, code, payload):
            body = json.dumps(payload).encode("utf-8")
            self.send_response(code)
            self.send_header("Content-Type", "application/json")
            self.send_header("Content-Length", str(len(body)))
            self.end_headers()
            self.wfile.write(body)

        def do_GET(self):
            if self.path == "/health":
                self._send(200, {"status": "ok", "backend": "laya-mlx"})
            else:
                self._send(404, {"detail": "not found"})

        def do_POST(self):
            if self.path != "/v1/systemone":
                self._send(404, {"detail": "not found"})
                return
            try:
                length = int(self.headers.get("Content-Length", "0"))
            except ValueError:
                length = 0
            if length <= 0 or length > MAX_BODY_BYTES:
                self._send(413, {"detail": "bad body size"})
                return
            try:
                body = json.loads(self.rfile.read(length))
                state, questions = body.get("state"), body["questions"]
                if not isinstance(questions, dict) or not 0 < len(questions) <= MAX_QUESTIONS:
                    raise ValueError("questions must be an object with 1-%d entries" % MAX_QUESTIONS)
            except (ValueError, KeyError, AttributeError) as exc:
                self._send(400, {"detail": str(exc)})
                return
            started = time.perf_counter()
            try:
                # MLX shares one GPU stream; serialise inference, not the HTTP layer.
                with lock:
                    result = agent.predict(state, questions)
            except ValueError as exc:
                self._send(422, {"detail": str(exc)})
                return
            except Exception:  # noqa: BLE001 - never crash the server on one bad request
                self._send(500, {"detail": "inference failed"})
                return
            result["latency_ms"] = round((time.perf_counter() - started) * 1000, 2)
            self._send(200, result)

    return Handler


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--model", default="aac6fef/laya-mlx", help="Hugging Face id or local MLX export")
    ap.add_argument("--port", type=int, default=8765)
    ap.add_argument("--dtype", default="float16", choices=["float16", "float32", "bfloat16"])
    ap.add_argument("--optimize", action="store_true",
                    help="compile + 16-token buckets + prefix cache (faster for repeated prompts)")
    ap.add_argument("--verbose", action="store_true")
    args = ap.parse_args()

    import laya_mlx as laya  # deferred so --help works without MLX

    kwargs = {"dtype": args.dtype}
    if args.optimize:
        kwargs.update(compile=True, pad_to_multiple=16, cache_prompts=True)
    print(f"[laya] loading {args.model} ...", flush=True)
    agent = laya.load(args.model, **kwargs)

    # Warm-up so the game's first real question is not paying for compilation.
    agent.predict("warm up", {"q": {"type": "choice", "instructions": "Pick one.", "criteria": ["a", "b"]}})

    server = ThreadingHTTPServer(("127.0.0.1", args.port), build_handler(agent, threading.Lock(), args.verbose))
    print(f"[laya] serving http://127.0.0.1:{args.port}/v1/systemone  (Ctrl+C to stop)", flush=True)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass


if __name__ == "__main__":
    main()
