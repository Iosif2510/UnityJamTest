"""Local web editor for portrait eyes.

    python Tools/IrisPainter/server.py        then open http://localhost:5178

The eye colour in game is the mask layer, tinted and drawn behind the body image, seen through the see-through iris
of the body image. This tool paints that cut-out (body image) and the mask, with a live colour preview.
Pixels travel as raw RGBA so nothing but the painted pixels changes in the saved PNGs.
"""
import json
import os
import shutil
import struct
import sys
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

import numpy as np
from PIL import Image

HERE = Path(__file__).resolve().parent
PROJECT = HERE.parent.parent
PORTRAITS = PROJECT / "Assets" / "Arts" / "Portraits"
BACKUP = PROJECT / "Library" / "IrisPainter"   # first-seen copy of each body image, used by the "restore" brush
PORT = int(os.environ.get("PORT", "5178"))


def body_path(name): return PORTRAITS / name / f"POR_{name}_BASE.png"
def mask_path(name): return PORTRAITS / name / f"POR_{name}_IRIS_SOLID_MASK.png"


def characters():
    return sorted(d.name for d in PORTRAITS.iterdir() if d.is_dir() and body_path(d.name).exists() and mask_path(d.name).exists())


def original_path(name):
    BACKUP.mkdir(parents=True, exist_ok=True)
    kept = BACKUP / f"POR_{name}_BASE.png"
    if not kept.exists():
        shutil.copy2(body_path(name), kept)
    return kept


def raw(path):
    image = Image.open(path).convert("RGBA")
    return struct.pack("<II", image.width, image.height) + image.tobytes()


class Handler(BaseHTTPRequestHandler):
    def log_message(self, *args):
        pass

    def send(self, status, body, kind="application/json"):
        if isinstance(body, (dict, list)):
            body = json.dumps(body, ensure_ascii=False).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", kind)
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Cache-Control", "no-store")
        self.end_headers()
        self.wfile.write(body)

    def name_from(self, part):
        if part not in characters():
            raise KeyError(part)
        return part

    def do_GET(self):
        parts = [p for p in self.path.split("?")[0].split("/") if p]
        try:
            if not parts:
                return self.send(200, (HERE / "index.html").read_bytes(), "text/html; charset=utf-8")
            if parts == ["api", "characters"]:
                return self.send(200, characters())
            if len(parts) == 4 and parts[:2] == ["api", "raw"]:
                name = self.name_from(parts[2])
                path = {"body": body_path, "mask": mask_path, "original": original_path}[parts[3]](name)
                return self.send(200, raw(path), "application/octet-stream")
            self.send(404, {"error": "not found"})
        except KeyError:
            self.send(404, {"error": "unknown character or layer"})

    def do_POST(self):
        parts = [p for p in self.path.split("?")[0].split("/") if p]
        try:
            if len(parts) == 3 and parts[:2] == ["api", "save"]:
                name = self.name_from(parts[2])
                data = self.rfile.read(int(self.headers["Content-Length"]))
                width, height = struct.unpack("<II", data[:8])
                size = width * height * 4
                if len(data) != 8 + size * 2:
                    return self.send(400, {"error": "unexpected size"})
                with Image.open(body_path(name)) as current:
                    if current.size != (width, height):
                        return self.send(400, {"error": "image size changed on disk"})
                original_path(name)
                for path, chunk in ((body_path(name), data[8:8 + size]), (mask_path(name), data[8 + size:])):
                    pixels = np.frombuffer(chunk, dtype=np.uint8).reshape(height, width, 4)
                    Image.fromarray(pixels, "RGBA").save(path)
                return self.send(200, {"saved": name})
            self.send(404, {"error": "not found"})
        except KeyError:
            self.send(404, {"error": "unknown character"})


if __name__ == "__main__":
    if not PORTRAITS.exists():
        sys.exit(f"Portraits folder not found: {PORTRAITS}")
    print(f"Iris painter: http://localhost:{PORT}  ({len(characters())} characters)")
    ThreadingHTTPServer(("127.0.0.1", PORT), Handler).serve_forever()
