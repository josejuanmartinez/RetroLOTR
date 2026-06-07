#!/usr/bin/env python3
"""Generate a character animation video using Scenario + Grok Imagine Video 1.5."""

from __future__ import annotations

import argparse
import base64
import os
import sys
import time
from pathlib import Path


API_BASE = "https://api.cloud.scenario.com/v1"
MODEL_ID = "model_xai-grok-imagine-video-1-5"
DEFAULT_DURATION = 15
DEFAULT_RESOLUTION = "480p"
DEFAULT_NUM_OUTPUTS = 1
DEFAULT_UPLOAD_MAX_DIM = 1024  # resize before encoding to keep payload small

DEFAULT_PROMPT = (
    "Animate this character performing the following 6 distinct movement phases in order. "
    "The video is 15 seconds — allocate roughly 2-3 seconds per phase. "
    "CRITICAL: keep the entire character body fully within the camera frame at all times — "
    "no limbs, head, or body parts should ever leave the screen edges. Add internal padding so the character never touches the frame border. "
    "Do NOT zoom the camera in or out at any point. "
    "Phase 1 — IDLE (2s): the character stands in place with subtle breathing, weight shift, and gentle swaying. Return to neutral standing pose. "
    "Phase 2 — ACTION (2s): the character performs a clear combat attack or spell cast with full arm and body motion. Return to neutral standing pose. "
    "Phase 3 — WALK FORWARD (2-3s): the character walks directly toward the viewer in a full repeating walk cycle — legs and arms must swing continuously. "
    "The character must visibly travel forward for the ENTIRE duration of this phase, completing at least 3-4 full stride cycles. NOT a single step — sustained walking. Return to neutral standing pose. "
    "Phase 4 — WALK LEFT (2-3s): the character walks to the left in a full repeating walk cycle — legs and arms must swing continuously. "
    "The character must visibly travel leftward across the screen for the ENTIRE duration of this phase, completing at least 3-4 full stride cycles. NOT a single step or sway — sustained walking. Return to neutral standing pose. "
    "Phase 5 — WALK RIGHT (2-3s): the character walks to the right in a full repeating walk cycle — legs and arms must swing continuously. "
    "The character must visibly travel rightward across the screen for the ENTIRE duration of this phase, completing at least 3-4 full stride cycles. NOT a single step or sway — sustained walking. Return to neutral standing pose. "
    "Phase 6 — TURN AND EXIT (2s): the character turns around to face away from the viewer, then walks away in a continuous walk cycle until they are small in the distance. No camera zoom."
)


def die(message: str, code: int = 1) -> None:
    print(f"Error: {message}", file=sys.stderr)
    raise SystemExit(code)


def get_auth_header() -> dict:
    api_key = os.getenv("SCENARIO_API_KEY")
    api_secret = os.getenv("SCENARIO_API_KEY_SECRET")
    if not api_key or not api_secret:
        die("SCENARIO_API_KEY and SCENARIO_API_KEY_SECRET must be set.")
    token = base64.b64encode(f"{api_key}:{api_secret}".encode()).decode()
    return {"Authorization": f"Basic {token}", "Content-Type": "application/json"}


def resize_image(image_path: Path, max_dim: int) -> bytes:
    """Return PNG bytes, resized to max_dim if needed."""
    try:
        from PIL import Image
    except ImportError:
        die("Pillow is required. Install it with `uv pip install pillow`.")
    import io

    with Image.open(image_path) as img:
        if max_dim > 0 and max(img.size) > max_dim:
            img = img.copy()
            img.thumbnail((max_dim, max_dim), Image.Resampling.LANCZOS)
        buf = io.BytesIO()
        img.convert("RGBA").save(buf, format="PNG")
        return buf.getvalue()


def upload_asset(image_path: Path, headers: dict, max_dim: int) -> str:
    """Upload image to Scenario via the assets endpoint and return the asset ID."""
    try:
        import requests
    except ImportError:
        die("requests is required. Install it with `uv pip install requests`.")

    img_bytes = resize_image(image_path, max_dim)
    print(f"Uploading {image_path.name} ({len(img_bytes)//1024} KB) ...", flush=True)

    upload_headers = {k: v for k, v in headers.items() if k != "Content-Type"}

    resp = requests.post(
        f"{API_BASE}/assets",
        headers=upload_headers,
        files={"file": (image_path.name, img_bytes, "image/png")},
    )

    if resp.status_code not in (200, 201):
        # Fall back: return a base64 data URL so the caller can try passing it inline
        print(f"  Asset upload failed ({resp.status_code}): {resp.text}", file=sys.stderr)
        print("  Falling back to inline base64 data URL.", file=sys.stderr)
        b64 = base64.b64encode(img_bytes).decode()
        return f"data:image/png;base64,{b64}"

    data = resp.json()
    asset = data.get("asset") or data.get("data") or data
    asset_id = asset.get("id") if isinstance(asset, dict) else None
    if not asset_id:
        print(f"  Could not extract asset ID: {data}", file=sys.stderr)
        print("  Falling back to inline base64 data URL.", file=sys.stderr)
        b64 = base64.b64encode(img_bytes).decode()
        return f"data:image/png;base64,{b64}"

    print(f"  Asset ID: {asset_id}", flush=True)
    return asset_id


def start_generation(image_ref: str, prompt: str, duration: int, resolution: str,
                     num_outputs: int, headers: dict) -> str:
    """Start video generation and return the inference ID."""
    try:
        import requests
    except ImportError:
        die("requests is required.")

    payload = {
        "prompt": prompt,
        "image": image_ref,
        "numOutputs": num_outputs,
        "duration": duration,
        "resolution": resolution,
    }

    print(f"Starting generation (duration={duration}s, resolution={resolution}) ...", flush=True)

    resp = requests.post(
        f"{API_BASE}/generate/custom/{MODEL_ID}",
        headers=headers,
        json=payload,
    )

    if resp.status_code not in (200, 201):
        die(f"Generation start failed ({resp.status_code}): {resp.text}")

    data = resp.json()
    job = data.get("job") or data.get("inference") or data
    # Scenario uses jobId (not id)
    inference_id = (job.get("jobId") or job.get("id")) if isinstance(job, dict) else None
    if not inference_id:
        die(f"Could not extract inference ID from response: {data}")

    print(f"Inference started: {inference_id}", flush=True)
    return inference_id


def fetch_asset_url(asset_id: str, headers: dict) -> str | None:
    """Fetch the CDN download URL for a Scenario asset."""
    try:
        import requests
    except ImportError:
        die("requests is required.")
    resp = requests.get(f"{API_BASE}/assets/{asset_id}", headers=headers)
    if resp.status_code != 200:
        return None
    return (resp.json().get("asset") or {}).get("url")


def poll_inference(inference_id: str, headers: dict, poll_interval: int = 10,
                   timeout: int = 600) -> list[str]:
    """Poll until job is complete. Returns list of output video download URLs."""
    try:
        import requests
    except ImportError:
        die("requests is required.")

    deadline = time.time() + timeout
    print(f"Polling job {inference_id} ...", flush=True)

    while time.time() < deadline:
        # Scenario job status endpoint
        resp = requests.get(f"{API_BASE}/jobs/{inference_id}", headers=headers)
        if resp.status_code == 200:
            data = resp.json()
            job = data.get("job") or data
            status = job.get("status", "") if isinstance(job, dict) else ""
            print(f"  status: {status}", flush=True)

            if status == "success":
                # Output asset IDs live in job.metadata.assetIds
                asset_ids = (job.get("metadata") or {}).get("assetIds") or []
                urls = []
                for aid in asset_ids:
                    url = fetch_asset_url(aid, headers)
                    if url:
                        urls.append(url)
                if not urls:
                    die(f"Job succeeded but no output asset URLs found. job.metadata: {job.get('metadata')}")
                return urls

            if status in ("failed", "error", "cancelled"):
                die(f"Job {status}: {job}")

        time.sleep(poll_interval)

    die(f"Timed out waiting for job {inference_id} after {timeout}s")


def download_videos(urls: list[str], out_dir: Path, stem: str) -> list[Path]:
    """Download video URLs to out_dir. Returns saved paths."""
    try:
        import requests
    except ImportError:
        die("requests is required.")

    out_dir.mkdir(parents=True, exist_ok=True)
    saved = []
    for i, url in enumerate(urls):
        suffix = f"_{i}" if len(urls) > 1 else ""
        out_path = out_dir / f"{stem}{suffix}.mp4"
        if out_path.exists():
            out_path.unlink()
        print(f"Downloading -> {out_path}", flush=True)
        resp = requests.get(url, stream=True)
        if resp.status_code != 200:
            print(f"  Warning: download failed ({resp.status_code}) for {url}", file=sys.stderr)
            continue
        with out_path.open("wb") as f:
            for chunk in resp.iter_content(chunk_size=8192):
                f.write(chunk)
        saved.append(out_path)
        print(f"  Saved: {out_path}", flush=True)
    return saved


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Generate a character animation video via Scenario / Grok Imagine Video 1.5"
    )
    parser.add_argument("--image", required=True, help="Path to source character PNG")
    parser.add_argument("--out-dir", default="Assets/Art/Characters/AnimationVideos",
                        help="Output directory for generated video(s)")
    parser.add_argument("--prompt", default=DEFAULT_PROMPT, help="Animation prompt")
    parser.add_argument("--duration", type=int, default=DEFAULT_DURATION,
                        help="Video duration in seconds (1-15)")
    parser.add_argument("--resolution", default=DEFAULT_RESOLUTION,
                        choices=["480p", "720p"], help="Output resolution")
    parser.add_argument("--num-outputs", type=int, default=DEFAULT_NUM_OUTPUTS,
                        choices=range(1, 5), metavar="1-4",
                        help="Number of videos to generate concurrently")
    parser.add_argument("--upload-max-dim", type=int, default=DEFAULT_UPLOAD_MAX_DIM,
                        help="Resize image to this max dimension before upload (0 = no resize)")
    parser.add_argument("--poll-interval", type=int, default=10,
                        help="Seconds between status polls")
    parser.add_argument("--timeout", type=int, default=600,
                        help="Max seconds to wait for completion")
    parser.add_argument("--dry-run", action="store_true",
                        help="Print parameters without calling the API")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    image_path = Path(args.image)

    if not image_path.exists():
        die(f"Image not found: {image_path}")

    out_dir = Path(args.out_dir)
    stem = image_path.stem

    print("=== Spritesheet Video Generation ===")
    print(f"  image     : {image_path}")
    print(f"  out_dir   : {out_dir}")
    print(f"  prompt    : {args.prompt[:120]}{'...' if len(args.prompt) > 120 else ''}")
    print(f"  duration  : {args.duration}s")
    print(f"  resolution: {args.resolution}")
    print(f"  outputs   : {args.num_outputs}")

    if args.dry_run:
        print("\n[dry-run] No API calls made.")
        return 0

    headers = get_auth_header()
    image_ref = upload_asset(image_path, headers, args.upload_max_dim)
    inference_id = start_generation(
        image_ref, args.prompt, args.duration, args.resolution,
        args.num_outputs, headers
    )
    urls = poll_inference(inference_id, headers, args.poll_interval, args.timeout)
    saved = download_videos(urls, out_dir, stem)

    print("\n=== Completion Report ===")
    print(f"  Model    : {MODEL_ID}")
    print(f"  Image ref: {image_ref[:60]}{'...' if len(image_ref) > 60 else ''}")
    print(f"  Inference: {inference_id}")
    print(f"  Saved    : {len(saved)} video(s)")
    for p in saved:
        print(f"    {p}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
