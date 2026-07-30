#!/usr/bin/env python3
"""Generate a reference-guided video with xAI's Grok Imagine API."""

from __future__ import annotations

import argparse
import base64
import json
import mimetypes
import os
from pathlib import Path
import sys
import time
import urllib.error
import urllib.request


API_BASE = "https://api.x.ai/v1"
DEFAULT_IMAGE_DIR = Path("Assets/Art/Videos/Inspiration")
DEFAULT_OUT_DIR = Path("Assets/Art/Videos/Generated")
SUPPORTED_SUFFIXES = {".png", ".jpg", ".jpeg", ".webp", ".gif", ".avif", ".bmp"}
IMAGE_MIME_TYPES = {
    ".png": "image/png",
    ".jpg": "image/jpeg",
    ".jpeg": "image/jpeg",
    ".webp": "image/webp",
    ".gif": "image/gif",
    ".avif": "image/avif",
    ".bmp": "image/bmp",
}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--prompt", required=True, help="Required video generation prompt")
    parser.add_argument(
        "--image",
        action="append",
        type=Path,
        help="Reference image path; repeat to preserve an explicit order",
    )
    parser.add_argument("--image-dir", type=Path, default=DEFAULT_IMAGE_DIR)
    parser.add_argument("--out-dir", type=Path, default=DEFAULT_OUT_DIR)
    parser.add_argument("--output-name", help="MP4 filename or stem")
    parser.add_argument(
        "--mode",
        choices=("reference-to-video", "image-to-video"),
        default="reference-to-video",
        help="Use all selected images as references or one image as the starting frame",
    )
    parser.add_argument("--model", default="grok-imagine-video")
    parser.add_argument("--duration", type=int, default=10)
    parser.add_argument("--aspect-ratio", default="16:9")
    parser.add_argument("--resolution", default="720p")
    parser.add_argument("--poll-interval", type=float, default=5.0)
    parser.add_argument("--timeout", type=float, default=900.0)
    parser.add_argument("--dry-run", action="store_true")
    return parser.parse_args()


def resolve_images(args: argparse.Namespace) -> list[Path]:
    if args.image:
        images = args.image
    else:
        if not args.image_dir.is_dir():
            raise ValueError(f"Reference directory does not exist: {args.image_dir}")
        images = sorted(
            (
                path
                for path in args.image_dir.iterdir()
                if path.is_file() and path.suffix.lower() in SUPPORTED_SUFFIXES
            ),
            key=lambda path: path.name.lower(),
        )

    if not images:
        raise ValueError("Provide 1-7 reference images; none were found")
    if args.mode == "image-to-video" and len(images) != 1:
        raise ValueError("Image-to-video mode requires exactly one --image")
    if len(images) > 7:
        raise ValueError(
            f"Found {len(images)} reference images, but xAI allows at most 7. "
            "Repeat --image to select and order up to 7."
        )

    resolved = [path.resolve() for path in images]
    for path in resolved:
        if not path.is_file():
            raise ValueError(f"Reference image does not exist: {path}")
        if path.suffix.lower() not in SUPPORTED_SUFFIXES:
            raise ValueError(f"Unsupported reference image format: {path}")
    return resolved


def as_data_uri(path: Path) -> str:
    mime_type = IMAGE_MIME_TYPES.get(path.suffix.lower())
    if mime_type is None:
        mime_type = mimetypes.guess_type(path.name)[0]
    if not mime_type or not mime_type.startswith("image/"):
        raise ValueError(f"Cannot determine an image MIME type for: {path}")
    encoded = base64.b64encode(path.read_bytes()).decode("ascii")
    return f"data:{mime_type};base64,{encoded}"


def request_json(
    method: str, url: str, api_key: str, payload: dict | None = None
) -> dict:
    body = json.dumps(payload).encode("utf-8") if payload is not None else None
    headers = {"Authorization": f"Bearer {api_key}"}
    if body is not None:
        headers["Content-Type"] = "application/json"
    request = urllib.request.Request(url, data=body, headers=headers, method=method)
    try:
        with urllib.request.urlopen(request, timeout=120) as response:
            return json.loads(response.read().decode("utf-8"))
    except urllib.error.HTTPError as error:
        detail = error.read().decode("utf-8", errors="replace")
        raise RuntimeError(f"xAI API returned HTTP {error.code}: {detail}") from error
    except urllib.error.URLError as error:
        raise RuntimeError(f"xAI API request failed: {error.reason}") from error


def output_path(args: argparse.Namespace, request_id: str) -> Path:
    name = args.output_name or f"grok-video-{request_id}"
    if not name.lower().endswith(".mp4"):
        name += ".mp4"
    args.out_dir.mkdir(parents=True, exist_ok=True)
    return (args.out_dir / name).resolve()


def main() -> int:
    args = parse_args()
    prompt = args.prompt.strip()
    if not prompt:
        raise ValueError("Prompt must not be empty")
    if not 1 <= args.duration <= 10:
        raise ValueError("Reference-to-video duration must be between 1 and 10 seconds")
    if args.poll_interval <= 0 or args.timeout <= 0:
        raise ValueError("Poll interval and timeout must be positive")

    images = resolve_images(args)
    summary = {
        "model": args.model,
        "mode": args.mode,
        "prompt": prompt,
        "reference_images": [str(path) for path in images],
        "duration": args.duration,
        "aspect_ratio": args.aspect_ratio,
        "resolution": args.resolution,
        "output_directory": str(args.out_dir.resolve()),
    }
    if args.dry_run:
        print(json.dumps(summary, indent=2))
        return 0

    api_key = os.environ.get("GROK_API_KEY")
    if not api_key:
        raise RuntimeError("GROK_API_KEY is not set")

    payload = {
        "model": args.model,
        "prompt": prompt,
        "duration": args.duration,
        "aspect_ratio": args.aspect_ratio,
        "resolution": args.resolution,
    }
    if args.mode == "image-to-video":
        payload["image"] = {"url": as_data_uri(images[0])}
    else:
        payload["reference_images"] = [{"url": as_data_uri(path)} for path in images]
    submitted = request_json(
        "POST", f"{API_BASE}/videos/generations", api_key, payload
    )
    request_id = submitted.get("request_id")
    if not request_id:
        raise RuntimeError(f"xAI response did not include request_id: {submitted}")
    print(f"Request ID: {request_id}", flush=True)

    deadline = time.monotonic() + args.timeout
    while time.monotonic() < deadline:
        result = request_json("GET", f"{API_BASE}/videos/{request_id}", api_key)
        status = result.get("status")
        progress = result.get("progress")
        print(
            f"Status: {status}" + (f" ({progress}%)" if progress is not None else ""),
            flush=True,
        )
        if status == "done":
            video_url = result.get("video", {}).get("url")
            if not video_url:
                raise RuntimeError(f"Completed response has no video URL: {result}")
            destination = output_path(args, request_id)
            try:
                urllib.request.urlretrieve(video_url, destination)
            except urllib.error.URLError as error:
                raise RuntimeError(f"Video download failed: {error.reason}") from error
            print(f"Saved: {destination}")
            return 0
        if status in {"failed", "expired", "cancelled"}:
            raise RuntimeError(f"Video generation {status}: {result}")
        time.sleep(args.poll_interval)

    raise TimeoutError(
        f"Timed out after {args.timeout:g}s; request ID {request_id} may still be running"
    )


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, RuntimeError, TimeoutError, ValueError) as error:
        print(f"Error: {error}", file=sys.stderr)
        raise SystemExit(1)
