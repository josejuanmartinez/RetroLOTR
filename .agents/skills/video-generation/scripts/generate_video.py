#!/usr/bin/env python3
"""Generate a reference-guided video with fal.ai Seedance 2.0."""

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


MODEL_ID = "bytedance/seedance-2.0/reference-to-video"
QUEUE_URL = f"https://queue.fal.run/{MODEL_ID}"
DEFAULT_OUT_DIR = Path("Assets/Art/Videos/Generated")
IMAGE_SUFFIXES = {".png", ".jpg", ".jpeg", ".webp"}
VIDEO_SUFFIXES = {".mp4", ".mov"}
AUDIO_SUFFIXES = {".mp3", ".wav"}
MIME_TYPES = {
    ".png": "image/png",
    ".jpg": "image/jpeg",
    ".jpeg": "image/jpeg",
    ".webp": "image/webp",
    ".mp4": "video/mp4",
    ".mov": "video/quicktime",
    ".mp3": "audio/mpeg",
    ".wav": "audio/wav",
}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--prompt", required=True)
    parser.add_argument("--image", action="append", type=Path, default=[])
    parser.add_argument("--video", action="append", type=Path, default=[])
    parser.add_argument("--audio", action="append", type=Path, default=[])
    parser.add_argument("--out-dir", type=Path, default=DEFAULT_OUT_DIR)
    parser.add_argument("--output-name", help="MP4 filename or stem")
    parser.add_argument("--duration", default="auto", choices=["auto"] + [str(n) for n in range(4, 16)])
    parser.add_argument("--resolution", default="720p", choices=("480p", "720p", "1080p", "4k"))
    parser.add_argument("--aspect-ratio", default="auto", choices=("auto", "21:9", "16:9", "4:3", "1:1", "3:4", "9:16"))
    parser.add_argument("--generate-audio", action=argparse.BooleanOptionalAction, default=True)
    parser.add_argument("--bitrate-mode", default="standard", choices=("standard", "high"))
    parser.add_argument("--seed", type=int)
    parser.add_argument("--end-user-id")
    parser.add_argument("--poll-interval", type=float, default=5.0)
    parser.add_argument("--timeout", type=float, default=1800.0)
    parser.add_argument("--dry-run", action="store_true")
    return parser.parse_args()


def validate_files(paths: list[Path], suffixes: set[str], label: str) -> list[Path]:
    resolved = [path.resolve() for path in paths]
    for path in resolved:
        if not path.is_file():
            raise ValueError(f"{label} does not exist: {path}")
        if path.suffix.lower() not in suffixes:
            raise ValueError(f"Unsupported {label.lower()} format: {path}")
    return resolved


def as_data_uri(path: Path) -> str:
    mime_type = MIME_TYPES.get(path.suffix.lower()) or mimetypes.guess_type(path.name)[0]
    if not mime_type:
        raise ValueError(f"Cannot determine MIME type for: {path}")
    encoded = base64.b64encode(path.read_bytes()).decode("ascii")
    return f"data:{mime_type};base64,{encoded}"


def request_json(method: str, url: str, api_key: str, payload: dict | None = None) -> dict:
    body = json.dumps(payload).encode("utf-8") if payload is not None else None
    headers = {"Authorization": f"Key {api_key}"}
    if body is not None:
        headers["Content-Type"] = "application/json"
    request = urllib.request.Request(url, data=body, headers=headers, method=method)
    try:
        with urllib.request.urlopen(request, timeout=120) as response:
            return json.loads(response.read().decode("utf-8"))
    except urllib.error.HTTPError as error:
        detail = error.read().decode("utf-8", errors="replace")
        raise RuntimeError(f"fal.ai returned HTTP {error.code}: {detail}") from error
    except urllib.error.URLError as error:
        raise RuntimeError(f"fal.ai request failed: {error.reason}") from error


def output_path(args: argparse.Namespace, request_id: str) -> Path:
    name = args.output_name or f"seedance-video-{request_id}"
    if not name.lower().endswith(".mp4"):
        name += ".mp4"
    args.out_dir.mkdir(parents=True, exist_ok=True)
    return (args.out_dir / name).resolve()


def main() -> int:
    args = parse_args()
    prompt = args.prompt.strip()
    if not prompt:
        raise ValueError("Prompt must not be empty")
    if args.poll_interval <= 0 or args.timeout <= 0:
        raise ValueError("Poll interval and timeout must be positive")

    images = validate_files(args.image, IMAGE_SUFFIXES, "Reference image")
    videos = validate_files(args.video, VIDEO_SUFFIXES, "Reference video")
    audios = validate_files(args.audio, AUDIO_SUFFIXES, "Reference audio")
    if len(images) > 9 or len(videos) > 3 or len(audios) > 3:
        raise ValueError("Seedance permits at most 9 images, 3 videos, and 3 audio files")
    if len(images) + len(videos) + len(audios) > 12:
        raise ValueError("Seedance permits at most 12 total reference files")
    if audios and not (images or videos):
        raise ValueError("Audio references require at least one image or video reference")

    payload: dict = {
        "prompt": prompt,
        "image_urls": [as_data_uri(path) for path in images],
        "video_urls": [as_data_uri(path) for path in videos],
        "audio_urls": [as_data_uri(path) for path in audios],
        "resolution": args.resolution,
        "duration": args.duration,
        "aspect_ratio": args.aspect_ratio,
        "generate_audio": args.generate_audio,
        "bitrate_mode": args.bitrate_mode,
    }
    if args.seed is not None:
        payload["seed"] = args.seed
    if args.end_user_id:
        payload["end_user_id"] = args.end_user_id

    summary = {
        "model": MODEL_ID,
        "prompt": prompt,
        "reference_images": [str(path) for path in images],
        "reference_videos": [str(path) for path in videos],
        "reference_audio": [str(path) for path in audios],
        **{key: value for key, value in payload.items() if not key.endswith("_urls") and key != "prompt"},
        "output_directory": str(args.out_dir.resolve()),
    }
    if args.dry_run:
        print(json.dumps(summary, indent=2))
        return 0

    api_key = os.environ.get("FAL_API_KEY")
    if not api_key:
        raise RuntimeError("FAL_API_KEY is not set")

    submitted = request_json("POST", QUEUE_URL, api_key, payload)
    request_id = submitted.get("request_id")
    status_url = submitted.get("status_url")
    response_url = submitted.get("response_url")
    if not request_id or not status_url or not response_url:
        raise RuntimeError(f"Unexpected fal.ai queue response: {submitted}")
    print(f"Request ID: {request_id}", flush=True)

    deadline = time.monotonic() + args.timeout
    while time.monotonic() < deadline:
        status_result = request_json("GET", status_url, api_key)
        status = status_result.get("status")
        print(f"Status: {status}", flush=True)
        if status == "COMPLETED":
            result = request_json("GET", response_url, api_key)
            data = result.get("data", result)
            video_url = data.get("video", {}).get("url")
            if not video_url:
                raise RuntimeError(f"Completed response has no video URL: {result}")
            destination = output_path(args, request_id)
            urllib.request.urlretrieve(video_url, destination)
            print(f"Seed: {data.get('seed')}")
            print(f"Saved: {destination}")
            return 0
        if status in {"FAILED", "CANCELLED"}:
            raise RuntimeError(f"Video generation {status.lower()}: {status_result}")
        time.sleep(args.poll_interval)

    raise TimeoutError(f"Timed out after {args.timeout:g}s; request ID {request_id} may still be running")


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, RuntimeError, TimeoutError, ValueError) as error:
        print(f"Error: {error}", file=sys.stderr)
        raise SystemExit(1)
