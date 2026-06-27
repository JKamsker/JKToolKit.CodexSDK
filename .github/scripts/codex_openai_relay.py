#!/usr/bin/env python3
"""Relay OpenAI-compatible traffic to the secret Codex endpoint.

The workflow points gh-aw at a runner-local URL so the real upstream host can
stay in CODEX_LB_BASE_URL. This relay deliberately avoids logging upstream URLs.
"""

from __future__ import annotations

import http.client
import os
import signal
import sys
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from typing import Final
from urllib.parse import urlsplit


HOP_BY_HOP_HEADERS: Final[set[str]] = {
    "connection",
    "keep-alive",
    "proxy-authenticate",
    "proxy-authorization",
    "te",
    "trailer",
    "transfer-encoding",
    "upgrade",
}


def parse_upstream() -> tuple[str, str, int, str]:
    raw_endpoint = os.environ.get("CODEX_LB_BASE_URL", "").strip()
    if not raw_endpoint:
        raise RuntimeError("CODEX_LB_BASE_URL is required")

    parsed = urlsplit(raw_endpoint.rstrip("/"))
    if parsed.scheme not in {"http", "https"} or not parsed.hostname:
        raise RuntimeError("CODEX_LB_BASE_URL must be an absolute HTTP(S) URL")
    if parsed.username or parsed.password:
        raise RuntimeError("CODEX_LB_BASE_URL must not contain credentials")

    default_port = 443 if parsed.scheme == "https" else 80
    base_path = parsed.path.rstrip("/")
    return parsed.scheme, parsed.hostname, parsed.port or default_port, base_path


UPSTREAM_SCHEME, UPSTREAM_HOST, UPSTREAM_PORT, UPSTREAM_BASE_PATH = parse_upstream()


def join_upstream_path(request_target: str) -> str:
    incoming = urlsplit(request_target)
    path = incoming.path or "/"
    if UPSTREAM_BASE_PATH:
        path = f"{UPSTREAM_BASE_PATH}{path}"
    if incoming.query:
        path = f"{path}?{incoming.query}"
    return path


class RelayHandler(BaseHTTPRequestHandler):
    protocol_version = "HTTP/1.1"

    def log_message(self, _format: str, *_args: object) -> None:
        return

    def do_DELETE(self) -> None:
        self.proxy()

    def do_GET(self) -> None:
        if self.path == "/__codex_relay_health":
            self.send_response(204)
            self.send_header("Connection", "close")
            self.end_headers()
            return
        self.proxy()

    def do_HEAD(self) -> None:
        self.proxy()

    def do_OPTIONS(self) -> None:
        self.proxy()

    def do_PATCH(self) -> None:
        self.proxy()

    def do_POST(self) -> None:
        self.proxy()

    def do_PUT(self) -> None:
        self.proxy()

    def proxy(self) -> None:
        if self.command == "CONNECT":
            self.send_error(405)
            return

        body = self.read_request_body()
        headers = self.copy_request_headers(body)

        connection_cls = (
            http.client.HTTPSConnection
            if UPSTREAM_SCHEME == "https"
            else http.client.HTTPConnection
        )
        connection = connection_cls(UPSTREAM_HOST, UPSTREAM_PORT, timeout=300)

        try:
            connection.request(
                self.command,
                join_upstream_path(self.path),
                body=body,
                headers=headers,
            )
            upstream = connection.getresponse()
            self.send_response(upstream.status, upstream.reason)
            self.copy_response_headers(upstream)
            self.end_headers()
            self.stream_response(upstream)
        except BrokenPipeError:
            return
        except Exception:
            self.send_error(502, "Upstream request failed")
        finally:
            connection.close()
            self.close_connection = True

    def read_request_body(self) -> bytes:
        content_length = self.headers.get("Content-Length")
        if not content_length:
            return b""

        try:
            byte_count = int(content_length)
        except ValueError:
            return b""
        if byte_count <= 0:
            return b""
        return self.rfile.read(byte_count)

    def copy_request_headers(self, body: bytes) -> dict[str, str]:
        headers: dict[str, str] = {}
        for key, value in self.headers.items():
            lower_key = key.lower()
            if lower_key in HOP_BY_HOP_HEADERS or lower_key == "host":
                continue
            headers[key] = value

        host_header = UPSTREAM_HOST
        if (UPSTREAM_SCHEME == "https" and UPSTREAM_PORT != 443) or (
            UPSTREAM_SCHEME == "http" and UPSTREAM_PORT != 80
        ):
            host_header = f"{UPSTREAM_HOST}:{UPSTREAM_PORT}"
        headers["Host"] = host_header
        headers["Connection"] = "close"
        headers["Content-Length"] = str(len(body))
        return headers

    def copy_response_headers(self, upstream: http.client.HTTPResponse) -> None:
        for key, value in upstream.getheaders():
            lower_key = key.lower()
            if lower_key in HOP_BY_HOP_HEADERS or lower_key == "connection":
                continue
            self.send_header(key, value)
        self.send_header("Connection", "close")

    def stream_response(self, upstream: http.client.HTTPResponse) -> None:
        while True:
            chunk = upstream.read(64 * 1024)
            if not chunk:
                break
            self.wfile.write(chunk)
            self.wfile.flush()


def main() -> int:
    host = os.environ.get("CODEX_OPENAI_RELAY_HOST", "0.0.0.0")
    port = int(os.environ.get("CODEX_OPENAI_RELAY_PORT", "80"))
    server = ThreadingHTTPServer((host, port), RelayHandler)

    def stop(_signum: int, _frame: object) -> None:
        server.shutdown()

    signal.signal(signal.SIGTERM, stop)
    signal.signal(signal.SIGINT, stop)
    print(f"Codex endpoint relay listening on {host}:{port}", flush=True)
    server.serve_forever()
    return 0


if __name__ == "__main__":
    sys.exit(main())
