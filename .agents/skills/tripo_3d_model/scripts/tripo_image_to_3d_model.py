#!/usr/bin/env python3
"""Turn a reference image into a textured 3D model via the Tripo3D API.

Pipeline (each stage is a Tripo task; the script polls each to completion
before starting the next):

  1. POST /files                      - upload the local image (skipped for a URL input)
  2. POST /generation/image-to-model  - Smart Topology mesh (model=P1, smart_low_poly=true)
                                         at a fixed triangle budget, textured from the image
  3. POST /models/convert             - export to the target format with the texture
                                         repacked at 2K

Output goes to `Assets/Art/3D/fbx/<Name>/<Name>.fbx` (or whatever --format
resolves to), alongside this repo's other Assets/Art/3D/fbx model folders.

Tripo's API surface is not in-repo prior art, and public docs for it are
inconsistent about hostnames/versions (v2 vs v3). This script targets the v3
REST API documented at https://developers.tripo3d.ai (openapi.tripo3d.ai/v3,
Bearer auth, POST /generation/image-to-model, POST /models/convert,
GET /tasks/{id}), cross-checked against the official
VAST-AI-Research/tripo-js-sdk source. If your Tripo account's docs show a
different host or response envelope, override with --base-url and inspect the
printed raw response on failure.
"""

from __future__ import annotations

import argparse
import json
import mimetypes
import os
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path

DEFAULT_BASE_URL = "https://openapi.tripo3d.ai/v3"
DEFAULT_MODEL_VERSION = "P1-20260311"  # Tripo's Smart Mesh / Smart Topology low-poly line
DEFAULT_FACE_LIMIT = 5000
DEFAULT_TEXTURE_QUALITY = "detailed"
DEFAULT_TEXTURE_SIZE = 2048  # 2K, applied at the final export/repack step
DEFAULT_FORMAT = "FBX"
DEFAULT_POLL_INTERVAL = 3.0
DEFAULT_TIMEOUT = 600.0
TERMINAL_STATUSES = {"success", "failed", "cancelled", "banned", "expired"}
REPO_ROOT = Path(__file__).resolve().parents[4]
DEFAULT_OUT_DIR = REPO_ROOT / "Assets" / "Art" / "3D" / "fbx"


def die(message: str, code: int = 1) -> None:
    print(f"Error: {message}", file=sys.stderr)
    raise SystemExit(code)


def api_key(dry_run: bool) -> str:
    key = os.getenv("TRIPO_API_KEY")
    if key:
        return key
    if dry_run:
        print("Warning: TRIPO_API_KEY is not set; dry-run only.", file=sys.stderr)
        return ""
    die("TRIPO_API_KEY is not set. Export it before running.")


def unwrap(payload: dict):
    """Tripo wraps most responses as {"code": 0, "data": {...}}; normalize both shapes."""
    if isinstance(payload, dict) and "data" in payload and isinstance(payload["data"], dict):
        return payload["data"]
    return payload


class TripoClient:
    def __init__(self, base_url: str, key: str):
        self.base_url = base_url.rstrip("/")
        self.key = key

    def _request(self, method: str, path: str, *, json_body: dict | None = None, form: tuple[bytes, str] | None = None) -> dict:
        url = path if path.startswith("http") else f"{self.base_url}{path}"
        headers = {}
        if self.key:
            headers["Authorization"] = f"Bearer {self.key}"

        if form is not None:
            body, content_type = form
            headers["Content-Type"] = content_type
        elif json_body is not None:
            body = json.dumps(json_body).encode("utf-8")
            headers["Content-Type"] = "application/json"
        else:
            body = None

        req = urllib.request.Request(url, data=body, headers=headers, method=method)
        try:
            with urllib.request.urlopen(req, timeout=120) as resp:
                raw = resp.read()
        except urllib.error.HTTPError as exc:
            raw = exc.read()
            die(
                f"{method} {path} failed: HTTP {exc.code}\n"
                f"Response body: {raw.decode('utf-8', errors='replace')}"
            )
        if not raw:
            return {}
        try:
            return json.loads(raw.decode("utf-8"))
        except json.JSONDecodeError:
            die(f"{method} {path} returned non-JSON: {raw[:500]!r}")

    def upload_file(self, image_path: Path) -> str:
        boundary = "----tripoUpload"
        content_type = mimetypes.guess_type(str(image_path))[0] or "application/octet-stream"
        data = image_path.read_bytes()
        parts = [
            f"--{boundary}\r\n"
            f'Content-Disposition: form-data; name="file"; filename="{image_path.name}"\r\n'
            f"Content-Type: {content_type}\r\n\r\n".encode("utf-8"),
            data,
            f"\r\n--{boundary}--\r\n".encode("utf-8"),
        ]
        body = b"".join(p if isinstance(p, bytes) else p.encode("utf-8") for p in parts)
        result = unwrap(self._request("POST", "/files", form=(body, f"multipart/form-data; boundary={boundary}")))
        token = result.get("file_token") or result.get("image_token")
        if not token:
            die(f"Upload did not return a file_token: {result}")
        return token

    def create_task(self, path: str, payload: dict) -> str:
        result = unwrap(self._request("POST", path, json_body=payload))
        task_id = result.get("task_id")
        if not task_id:
            die(f"{path} did not return a task_id: {result}")
        return task_id

    def get_task(self, task_id: str) -> dict:
        return unwrap(self._request("GET", f"/tasks/{task_id}"))

    def wait_for_task(self, task_id: str, label: str, poll_interval: float, timeout: float) -> dict:
        started = time.time()
        last_status = None
        while True:
            task = self.get_task(task_id)
            status = task.get("status", "unknown")
            if status != last_status:
                progress = task.get("progress", 0)
                print(f"  [{label}] {status} ({progress}%)", file=sys.stderr)
                last_status = status
            if status in TERMINAL_STATUSES:
                if status != "success":
                    die(f"{label} task {task_id} ended with status={status}: {task}")
                return task
            if time.time() - started > timeout:
                die(f"{label} task {task_id} timed out after {timeout}s (last status={status}).")
            time.sleep(poll_interval)


def extract_model_url(task: dict) -> str:
    output = task.get("output") or {}
    for key in ("model_url", "pbr_model", "base_model"):
        if output.get(key):
            return output[key]
    urls = output.get("model_urls")
    if urls:
        return urls[0]
    die(f"Could not find a model URL in task output: {task}")


def download(url: str, out_path: Path) -> None:
    req = urllib.request.Request(url, method="GET")
    try:
        with urllib.request.urlopen(req, timeout=120) as resp:
            data = resp.read()
    except urllib.error.HTTPError as exc:
        die(f"Downloading final model failed: HTTP {exc.code} {exc.reason}")
    out_path.write_bytes(data)


FORMAT_EXTENSIONS = {
    "FBX": ".fbx",
    "GLB": ".glb",
    "GLTF": ".gltf",
    "OBJ": ".obj",
    "USDZ": ".usdz",
    "STL": ".stl",
    "3MF": ".3mf",
}


def build_output_path(name: str, out: str | None, fmt: str) -> Path:
    ext = FORMAT_EXTENSIONS[fmt]
    if out:
        out_path = Path(out)
        if not out_path.is_absolute():
            out_path = REPO_ROOT / out_path
        return out_path if out_path.suffix else out_path.with_suffix(ext)
    return DEFAULT_OUT_DIR / name / f"{name}{ext}"


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--name", required=True, help="Asset name, used for the output folder and filename")
    parser.add_argument("--image", required=True, help="Local image path or an http(s) URL")
    parser.add_argument("--out", help="Output model path (default: Assets/Art/3D/<Name>/<Name>.<ext>)")
    parser.add_argument("--model-version", default=DEFAULT_MODEL_VERSION, help="Tripo model line (default: P1-20260311, the Smart Mesh/Smart Topology low-poly line)")
    parser.add_argument("--face-limit", type=int, default=DEFAULT_FACE_LIMIT)
    parser.add_argument("--texture-quality", default=DEFAULT_TEXTURE_QUALITY, choices=["standard", "detailed"])
    parser.add_argument("--texture-size", type=int, default=DEFAULT_TEXTURE_SIZE, help="Texture resolution baked into the final export, e.g. 2048 for 2K")
    parser.add_argument("--format", default=DEFAULT_FORMAT, choices=list(FORMAT_EXTENSIONS))
    parser.add_argument("--skip-convert", action="store_true", help="Stop after mesh+texture generation; download the raw generated GLB instead of running the convert/2K-repack stage")
    parser.add_argument("--base-url", default=os.getenv("TRIPO_API_BASE_URL", DEFAULT_BASE_URL))
    parser.add_argument("--poll-interval", type=float, default=DEFAULT_POLL_INTERVAL)
    parser.add_argument("--timeout", type=float, default=DEFAULT_TIMEOUT, help="Per-stage timeout in seconds")
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--force", action="store_true")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    key = api_key(args.dry_run)

    out_path = build_output_path(args.name, args.out, args.format)
    if out_path.exists() and not args.force:
        die(f"Output already exists: {out_path} (use --force to overwrite)")

    is_url = args.image.startswith("http://") or args.image.startswith("https://")
    image_path = None if is_url else Path(args.image)
    if not is_url and not image_path.exists():
        die(f"Image not found: {image_path}")

    is_p_series = args.model_version.upper().startswith("P")

    image_to_model_payload = {
        "model": args.model_version,
        "face_limit": args.face_limit,
        "texture": True,
        "pbr": True,
        "texture_quality": args.texture_quality,
        "texture_alignment": "original_image",
    }
    if not is_p_series:
        # smart_low_poly requests Smart-Mesh-style topology on a non-P-series
        # model; Tripo rejects it outright on P-series models (confirmed via
        # a live 400: "smart_low_poly is not supported for P-series model"),
        # because clean low-poly topology is what the P-series already is.
        image_to_model_payload["smart_low_poly"] = True

    if args.dry_run:
        print("Tripo3D image-to-3D-model pipeline dry-run")
        print(f"base_url={args.base_url}")
        print(f"image={'URL: ' + args.image if is_url else image_path}")
        print(f"out={out_path}")
        print("stage 1 (image-to-model) payload:")
        print(json.dumps({**image_to_model_payload, "file": "<uploaded file_token or url>"}, indent=2))
        if not args.skip_convert:
            print(f"stage 2 (convert, format={args.format}, texture_size={args.texture_size})")
        return 0

    client = TripoClient(args.base_url, key)

    file_ref = {"url": args.image} if is_url else {"file_token": client.upload_file(image_path)}
    if not is_url:
        print("Uploaded image, file_token acquired.", file=sys.stderr)

    model_task_id = client.create_task("/generation/image-to-model", {"file": file_ref, **image_to_model_payload})
    model_task = client.wait_for_task(model_task_id, "image-to-model", args.poll_interval, args.timeout)

    if args.skip_convert:
        out_path.parent.mkdir(parents=True, exist_ok=True)
        glb_path = out_path.with_suffix(".glb")
        download(extract_model_url(model_task), glb_path)
        print(f"Wrote {glb_path} (mesh + texture as generated, no 2K repack/format convert).")
        return 0

    convert_task_id = client.create_task(
        "/models/convert",
        {"input": model_task_id, "format": args.format, "texture_size": args.texture_size},
    )
    convert_task = client.wait_for_task(convert_task_id, "convert", args.poll_interval, args.timeout)

    out_path.parent.mkdir(parents=True, exist_ok=True)
    download(extract_model_url(convert_task), out_path)

    print(f"Wrote {out_path}")
    print(f"model_version={args.model_version} face_limit={args.face_limit} "
          f"texture_quality={args.texture_quality} texture_size={args.texture_size} "
          f"format={args.format}")
    print(f"Task IDs: image-to-model={model_task_id} convert={convert_task_id}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
