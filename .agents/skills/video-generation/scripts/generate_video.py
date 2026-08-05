#!/usr/bin/env python3
"""Generate and download a text-to-video clip with the official BFL FLUX API."""

from __future__ import annotations

import argparse
import json
import os
from pathlib import Path
import sys
import time
import urllib.error
import urllib.request


MODEL_ID = "flux-3-video"
SUBMIT_URL = "https://api.bfl.ai/v1/flux-3-video"
DEFAULT_OUT_DIR = Path("Assets/Art/Videos/Generated")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--prompt", required=True)
    parser.add_argument("--out-dir", type=Path, default=DEFAULT_OUT_DIR)
    parser.add_argument("--output-name", help="MP4 filename or stem")
    parser.add_argument("--duration", default="auto", choices=["auto"] + [str(n) for n in range(5, 21)])
    parser.add_argument("--resolution", default="hd", choices=("hd", "fhd"))
    parser.add_argument(
        "--aspect-ratio",
        default="auto",
        choices=("auto", "21:9", "2:1", "16:9", "4:3", "1:1", "3:4", "9:16"),
    )
    parser.add_argument("--generate-audio", action=argparse.BooleanOptionalAction, default=True)
    parser.add_argument("--safety-tolerance", type=int, default=2, choices=range(0, 5))
    parser.add_argument("--poll-interval", type=float, default=5.0)
    parser.add_argument("--timeout", type=float, default=1800.0)
    parser.add_argument("--dry-run", action="store_true")
    return parser.parse_args()


def request_json(method: str, url: str, api_key: str, payload: dict | None = None) -> dict:
    body = json.dumps(payload).encode("utf-8") if payload is not None else None
    headers = {"x-key": api_key}
    if body is not None:
        headers["Content-Type"] = "application/json"
    request = urllib.request.Request(url, data=body, headers=headers, method=method)
    try:
        with urllib.request.urlopen(request, timeout=120) as response:
            return json.loads(response.read().decode("utf-8"))
    except urllib.error.HTTPError as error:
        detail = error.read().decode("utf-8", errors="replace")
        raise RuntimeError(f"Black Forest Labs returned HTTP {error.code}: {detail}") from error
    except urllib.error.URLError as error:
        raise RuntimeError(f"Black Forest Labs request failed: {error.reason}") from error


def output_path(args: argparse.Namespace, request_id: str) -> Path:
    name = args.output_name or f"flux3-video-{request_id}"
    if not name.lower().endswith(".mp4"):
        name += ".mp4"
    args.out_dir.mkdir(parents=True, exist_ok=True)
    return (args.out_dir / name).resolve()


def download(url: str, destination: Path) -> None:
    try:
        with urllib.request.urlopen(url, timeout=300) as response, destination.open("wb") as output:
            while chunk := response.read(1024 * 1024):
                output.write(chunk)
    except (OSError, urllib.error.URLError) as error:
        if destination.exists():
            destination.unlink()
        raise RuntimeError(f"Could not download generated video: {error}") from error


def get_api_key() -> str | None:
    api_key = os.environ.get("FLUX_API_KEY")
    if api_key or os.name != "nt":
        return api_key

    # Codex or an IDE may have started before a newly-created Windows environment
    # variable was broadcast. Read the user/machine environment registry without
    # printing the value so the configured key works without restarting the editor.
    import winreg

    locations = (
        (winreg.HKEY_CURRENT_USER, r"Environment"),
        (winreg.HKEY_LOCAL_MACHINE, r"SYSTEM\CurrentControlSet\Control\Session Manager\Environment"),
    )
    for hive, subkey in locations:
        try:
            with winreg.OpenKey(hive, subkey) as key:
                value, _ = winreg.QueryValueEx(key, "FLUX_API_KEY")
                if isinstance(value, str) and value.strip():
                    return value
        except FileNotFoundError:
            continue
    return None


def main() -> int:
    args = parse_args()
    prompt = args.prompt.strip()
    if not prompt:
        raise ValueError("Prompt must not be empty")
    if args.poll_interval <= 0 or args.timeout <= 0:
        raise ValueError("Poll interval and timeout must be positive")

    duration: str | int = args.duration if args.duration == "auto" else int(args.duration)
    payload = {
        "mode": "t2v",
        "prompt": prompt,
        "aspect_ratio": args.aspect_ratio,
        "resolution": args.resolution,
        "duration": duration,
        "version": "latest",
        "generate_audio": args.generate_audio,
        "safety_tolerance": args.safety_tolerance,
        "draft": False,
    }
    summary = {
        "model": MODEL_ID,
        **payload,
        "output_directory": str(args.out_dir.resolve()),
        "output_name": args.output_name,
    }
    if args.dry_run:
        print(json.dumps(summary, indent=2))
        return 0

    api_key = get_api_key()
    if not api_key:
        raise RuntimeError("FLUX_API_KEY is not set")

    submitted = request_json("POST", SUBMIT_URL, api_key, payload)
    request_id = submitted.get("id")
    polling_url = submitted.get("polling_url")
    if not request_id or not polling_url:
        raise RuntimeError(f"Unexpected Black Forest Labs response: {submitted}")
    print(f"Request ID: {request_id}", flush=True)
    print(f"Cost: {submitted.get('cost')}", flush=True)

    deadline = time.monotonic() + args.timeout
    while time.monotonic() < deadline:
        status_result = request_json("GET", polling_url, api_key)
        status = status_result.get("status")
        print(f"Status: {status}", flush=True)
        if status == "Ready":
            data = status_result.get("result") or {}
            video_url = data.get("sample")
            if not video_url:
                raise RuntimeError(f"Ready response has no video URL: {status_result}")
            destination = output_path(args, request_id)
            download(video_url, destination)
            print(f"Seed: {data.get('seed')}")
            print(f"Saved: {destination}")
            return 0
        if status in {"Error", "Request Moderated", "Content Moderated", "Task not found"}:
            raise RuntimeError(f"Video generation ended with {status}: {status_result}")
        time.sleep(args.poll_interval)

    raise TimeoutError(f"Timed out after {args.timeout:g}s; request ID {request_id} may still be running")


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, RuntimeError, TimeoutError, ValueError) as error:
        print(f"Error: {error}", file=sys.stderr)
        raise SystemExit(1)
