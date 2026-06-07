#!/usr/bin/env python3
"""Extract N evenly-spaced frames from a video and arrange them into a spritesheet grid."""

from __future__ import annotations

import argparse
import math
import sys
from pathlib import Path


DEFAULT_FRAME_COUNT = 256
DEFAULT_COLS = 16  # 16x16 grid for 256 frames


def die(message: str, code: int = 1) -> None:
    print(f"Error: {message}", file=sys.stderr)
    raise SystemExit(code)


def extract_frames(video_path: Path, frame_count: int) -> list:
    try:
        import cv2
    except ImportError:
        die("opencv-python is required. Install with: uv pip install opencv-python")

    cap = cv2.VideoCapture(str(video_path))
    if not cap.isOpened():
        die(f"Could not open video: {video_path}")

    total_frames = int(cap.get(cv2.CAP_PROP_FRAME_COUNT))
    if total_frames == 0:
        die(f"Video has no frames: {video_path}")

    # Pick evenly-spaced frame indices across the full duration
    indices = [int(round(i * (total_frames - 1) / (frame_count - 1))) for i in range(frame_count)]
    indices = sorted(set(min(max(i, 0), total_frames - 1) for i in indices))

    frames = []
    for idx in indices:
        cap.set(cv2.CAP_PROP_POS_FRAMES, idx)
        ret, frame = cap.read()
        if not ret:
            print(f"  Warning: could not read frame {idx}, skipping", file=sys.stderr)
            continue
        # cv2 reads BGR; convert to RGB for Pillow
        frame_rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
        frames.append(frame_rgb)

    cap.release()

    if not frames:
        die("No frames could be extracted from the video.")

    print(f"  Extracted {len(frames)} frames from {total_frames} total (video: {video_path.name})", flush=True)
    return frames


def build_spritesheet(frames: list, cols: int, out_path: Path) -> None:
    try:
        from PIL import Image
        import numpy as np
    except ImportError:
        die("Pillow and numpy are required. Install with: uv pip install pillow numpy")

    rows = math.ceil(len(frames) / cols)
    frame_w, frame_h = frames[0].shape[1], frames[0].shape[0]

    sheet_w = cols * frame_w
    sheet_h = rows * frame_h

    sheet = Image.new("RGBA", (sheet_w, sheet_h), (0, 0, 0, 0))

    for i, frame_arr in enumerate(frames):
        col = i % cols
        row = i // cols
        x = col * frame_w
        y = row * frame_h
        frame_img = Image.fromarray(frame_arr, mode="RGB").convert("RGBA")
        sheet.paste(frame_img, (x, y))

    out_path.parent.mkdir(parents=True, exist_ok=True)
    if out_path.exists():
        out_path.unlink()
    sheet.save(out_path, format="PNG")

    print(f"  Spritesheet saved: {out_path}  ({sheet_w}x{sheet_h}px, {cols}col x {rows}row)", flush=True)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Extract frames from a video and arrange them into a spritesheet grid"
    )
    parser.add_argument("--video", required=True, help="Path to source video file")
    parser.add_argument("--out", required=True, help="Output spritesheet PNG path")
    parser.add_argument("--frames", type=int, default=DEFAULT_FRAME_COUNT,
                        help=f"Number of frames to extract (default: {DEFAULT_FRAME_COUNT})")
    parser.add_argument("--cols", type=int, default=DEFAULT_COLS,
                        help=f"Number of columns in the grid (default: {DEFAULT_COLS}; rows are computed automatically)")
    parser.add_argument("--dry-run", action="store_true",
                        help="Print parameters without processing")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    video_path = Path(args.video)

    if not video_path.exists():
        die(f"Video not found: {video_path}")

    out_path = Path(args.out)
    rows = math.ceil(args.frames / args.cols)

    print("=== Extract Spritesheet from Video ===")
    print(f"  video  : {video_path}")
    print(f"  out    : {out_path}")
    print(f"  frames : {args.frames}")
    print(f"  grid   : {args.cols} cols x {rows} rows")

    if args.dry_run:
        print("\n[dry-run] No processing performed.")
        return 0

    frames = extract_frames(video_path, args.frames)
    build_spritesheet(frames, args.cols, out_path)

    print("\n=== Done ===")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
