"""One local URL in front of the two Qwen servers the game asks for.

The narrator and the hero chat both talk to a single OpenAI-style base URL and pick a model by
name. This forwards ambient lines to the small model's llama-server and conversation to the 4B
model's. If the 4B server is down, chat falls back to the small model so a reply still arrives.
Standard library only, so it runs on the bundled Python without any packages.

    python model_router.py --port 8080 --lines http://127.0.0.1:8081 --chat http://127.0.0.1:8082 \
        --lines-model qwen3:1.7b --chat-model qwen3:4b-instruct-2507-q4_K_M

Binds to 127.0.0.1 only.
"""

import argparse
import json
import urllib.error
import urllib.request
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

CFG = {}


def backend_ready(base):
    """llama-server answers /health with 200 once its model is loaded (503 while loading)."""
    try:
        with urllib.request.urlopen(base + "/health", timeout=1.0) as r:
            return r.status == 200
    except (urllib.error.URLError, OSError, ValueError):
        return False


def build_handler():
    class Handler(BaseHTTPRequestHandler):
        protocol_version = "HTTP/1.1"

        def log_message(self, fmt, *args):
            return

        def _send(self, code, body, ctype="application/json"):
            self.send_response(code)
            self.send_header("Content-Type", ctype)
            self.send_header("Content-Length", str(len(body)))
            self.end_headers()
            self.wfile.write(body)

        def do_GET(self):
            if self.path.startswith("/v1/models") or self.path == "/health":
                # Ready only once the small model has loaded; chat joins when its model has.
                if not backend_ready(CFG["lines"]):
                    self._send(503, b'{"error":"models are loading"}')
                    return
                names = [CFG["lines_model"]]
                if backend_ready(CFG["chat"]):
                    names.append(CFG["chat_model"])
                body = json.dumps({
                    "object": "list",
                    "data": [{"id": n, "object": "model"} for n in names],
                }).encode("utf-8")
                self._send(200, body)
            else:
                self._send(404, b'{"error":"not found"}')

        def do_POST(self):
            length = int(self.headers.get("Content-Length", "0") or 0)
            raw = self.rfile.read(length) if length else b"{}"
            try:
                body = json.loads(raw)
            except (ValueError, TypeError):
                body = {}
            if not isinstance(body, dict):
                body = {}
            wants_chat = body.get("model") == CFG["chat_model"]
            target = CFG["chat"] if wants_chat else CFG["lines"]
            body["model"] = CFG["chat_model"] if wants_chat else CFG["lines_model"]
            self._forward(target, body, can_fall_back=wants_chat)

        def _post(self, target, body):
            req = urllib.request.Request(
                target + self.path,
                data=json.dumps(body).encode("utf-8"),
                headers={"Content-Type": "application/json"},
                method="POST",
            )
            return urllib.request.urlopen(req, timeout=180)

        def _forward(self, target, body, can_fall_back):
            try:
                resp = self._post(target, body)
            except (urllib.error.HTTPError, urllib.error.URLError, OSError) as err:
                if can_fall_back:
                    body = dict(body)
                    body["model"] = CFG["lines_model"]
                    self._forward(CFG["lines"], body, can_fall_back=False)
                    return
                detail = err.read() if isinstance(err, urllib.error.HTTPError) else b""
                self._send(502, detail or b'{"error":"model server is not up"}')
                return
            with resp:
                self.send_response(resp.status)
                self.send_header("Content-Type", resp.headers.get("Content-Type", "application/json"))
                self.send_header("Cache-Control", "no-cache")
                self.send_header("Connection", "close")
                self.end_headers()
                while True:
                    chunk = resp.read(4096)  # streamed replies arrive word by word
                    if not chunk:
                        break
                    self.wfile.write(chunk)
                    self.wfile.flush()
            self.close_connection = True

    return Handler


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--port", type=int, default=8080)
    ap.add_argument("--lines", required=True, help="base URL of the small model's server")
    ap.add_argument("--chat", required=True, help="base URL of the chat model's server")
    ap.add_argument("--lines-model", default="qwen3:1.7b")
    ap.add_argument("--chat-model", default="qwen3:4b-instruct-2507-q4_K_M")
    args = ap.parse_args()
    CFG.update(
        lines=args.lines.rstrip("/"),
        chat=args.chat.rstrip("/"),
        lines_model=args.lines_model,
        chat_model=args.chat_model,
    )
    server = ThreadingHTTPServer(("127.0.0.1", args.port), build_handler())
    print(f"[router] http://127.0.0.1:{args.port} -> lines {CFG['lines']}, chat {CFG['chat']}", flush=True)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass


if __name__ == "__main__":
    main()
