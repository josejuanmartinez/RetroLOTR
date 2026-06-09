#!/usr/bin/env python3
"""Extract a spritesheet PNG from a 12s / 6-phase animation video."""

from __future__ import annotations

import argparse
import sys
from pathlib import Path


PHASE_NAMES = ["idle", "action", "walk_forward", "walk_left", "turn_back", "exit_back"]
PHASE_DURATION_S = 2
NUM_PHASES = 6


def die(msg: str, code: int = 1) -> None:
    print(f"Error: {msg}", file=sys.stderr)
    raise SystemExit(code)


def extract(video_path: Path, out_path: Path, frames_per_phase: int,
            frame_w: int, frame_h: int) -> None:
    try:
        import cv2
        from PIL import Image
    except ImportError:
        die("OpenCV and Pillow are required. Install with: pip install opencv-python pillow")

    cap = cv2.VideoCapture(str(video_path))
    if not cap.isOpened():
        die(f"Cannot open video: {video_path}")

    fps = cap.get(cv2.CAP_PROP_FPS)
    total_frames = int(cap.get(cv2.CAP_PROP_FRAME_COUNT))
    src_w = int(cap.get(cv2.CAP_PROP_FRAME_WIDTH))
    src_h = int(cap.get(cv2.CAP_PROP_FRAME_HEIGHT))

    frames_per_phase_in_video = int(round(fps * PHASE_DURATION_S))
    print(f"Video: {src_w}x{src_h} @ {fps}fps, {total_frames} frames, {total_frames/fps:.1f}s")
    print(f"Phases: {NUM_PHASES} x {PHASE_DURATION_S}s = {frames_per_phase_in_video} frames each")
    print(f"Extracting: {frames_per_phase} frames per phase -> {frames_per_phase * NUM_PHASES} total")
    print(f"Output size per frame: {frame_w}x{frame_h}")

    # Build list of frame indices to sample (evenly spaced within each phase)
    sample_indices: list[tuple[int, int]] = []  # (phase_idx, frame_idx)
    for phase in range(NUM_PHASES):
        phase_start = phase * frames_per_phase_in_video
        phase_end = min(phase_start + frames_per_phase_in_video, total_frames)
        count = min(frames_per_phase, phase_end - phase_start)
        step = (phase_end - phase_start) / count
        for i in range(count):
            fi = int(phase_start + i * step)
            sample_indices.append((phase, fi))

    # Extract frames
    cells: dict[tuple[int, int], Image.Image] = {}
    prev_fi = -1
    for col, (phase, fi) in enumerate(sample_indices):
        if fi != prev_fi + 1:
            cap.set(cv2.CAP_PROP_POS_FRAMES, fi)
        ret, bgr = cap.read()
        if not ret:
            print(f"  Warning: could not read frame {fi}", file=sys.stderr)
            continue
        rgb = cv2.cvtColor(bgr, cv2.COLOR_BGR2RGB)
        img = Image.fromarray(rgb).resize((frame_w, frame_h), Image.Resampling.LANCZOS)
        col_in_phase = col % frames_per_phase
        cells[(phase, col_in_phase)] = img
        prev_fi = fi

    cap.release()

    # Compose spritesheet
    sheet_w = frames_per_phase * frame_w
    sheet_h = NUM_PHASES * frame_h
    sheet = Image.new("RGB", (sheet_w, sheet_h), (0, 0, 0))

    for (row, col), img in cells.items():
        sheet.paste(img, (col * frame_w, row * frame_h))

    out_path.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(str(out_path), "PNG")
    print(f"\nSaved: {out_path}  ({sheet_w}x{sheet_h}px, {out_path.stat().st_size // 1024} KB)")
    print(f"Layout: {frames_per_phase} cols x {NUM_PHASES} rows")
    for i, name in enumerate(PHASE_NAMES):
        print(f"  Row {i}: {name}")


def parse_args() -> argparse.Namespace:
    p = argparse.ArgumentParser(description="Extract spritesheet from animation video")
    p.add_argument("--video", required=True, help="Source MP4 path")
    p.add_argument("--out", default=None, help="Output PNG path (default: <video_dir>/<stem>_spritesheet.png)")
    p.add_argument("--frames-per-phase", type=int, default=8,
                   help="Frames to sample per 2s animation phase (default: 8)")
    p.add_argument("--frame-width", type=int, default=160, help="Output frame width px (default: 160)")
    p.add_argument("--frame-height", type=int, default=120, help="Output frame height px (default: 120)")
    return p.parse_args()


def main() -> int:
    args = parse_args()
    video_path = Path(args.video)
    if not video_path.exists():
        die(f"Video not found: {video_path}")
    out_path = Path(args.out) if args.out else video_path.parent / f"{video_path.stem}_spritesheet.png"
    extract(video_path, out_path, args.frames_per_phase, args.frame_width, args.frame_height)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
