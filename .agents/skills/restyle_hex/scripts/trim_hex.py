#!/usr/bin/env python3
"""
Remove background from hex tiles and crop to content.

Steps:
  1. Detect background color by sampling the image edges
  2. Flood-fill from all four corners to remove connected background → alpha
  3. Crop to the bounding box of remaining opaque content

Usage (single file):
    python trim_hex.py --image path/to/tile.png --out path/to/out.png

Usage (batch directory):
    python trim_hex.py --source Assets/Art/Hexes/Tiles_Restyled --out-dir Assets/Art/Hexes/Tiles_Trimmed
"""

from __future__ import annotations

import argparse
import sys
from collections import Counter, deque
from pathlib import Path

import numpy as np
from PIL import Image


def detect_bg_color(arr: np.ndarray) -> np.ndarray:
    """Return the most common RGB color found along the image edges."""
    h, w = arr.shape[:2]
    samples: list[tuple] = []

    # dense corner sampling
    corner_r = min(5, h // 4, w // 4)
    for y in range(corner_r):
        for x in range(corner_r):
            for py, px in [(y, x), (y, w-1-x), (h-1-y, x), (h-1-y, w-1-x)]:
                samples.append(tuple(arr[py, px, :3].tolist()))

    # sparse edge sampling
    step_x = max(1, w // 40)
    step_y = max(1, h // 40)
    for x in range(0, w, step_x):
        samples.append(tuple(arr[0, x, :3].tolist()))
        samples.append(tuple(arr[h - 1, x, :3].tolist()))
    for y in range(0, h, step_y):
        samples.append(tuple(arr[y, 0, :3].tolist()))
        samples.append(tuple(arr[y, w - 1, :3].tolist()))

    bg = Counter(samples).most_common(1)[0][0]
    return np.array(bg, dtype=np.float32)


def build_bg_mask(arr: np.ndarray, bg_color: np.ndarray, tolerance: float) -> np.ndarray:
    """
    BFS flood-fill from all four corners.
    Returns a bool mask of pixels that are (a) within `tolerance` of bg_color
    and (b) reachable from the image border without crossing non-background pixels.
    """
    h, w = arr.shape[:2]
    rgb = arr[:, :, :3].astype(np.float32)

    dist = np.sqrt(np.sum((rgb - bg_color) ** 2, axis=-1))
    candidate = dist <= tolerance

    visited = np.zeros((h, w), dtype=bool)
    queue: deque[tuple[int, int]] = deque()

    for sy, sx in [(0, 0), (0, w - 1), (h - 1, 0), (h - 1, w - 1)]:
        if candidate[sy, sx] and not visited[sy, sx]:
            visited[sy, sx] = True
            queue.append((sy, sx))

    while queue:
        y, x = queue.popleft()
        for dy, dx in ((-1, 0), (1, 0), (0, -1), (0, 1)):
            ny, nx = y + dy, x + dx
            if 0 <= ny < h and 0 <= nx < w and not visited[ny, nx] and candidate[ny, nx]:
                visited[ny, nx] = True
                queue.append((ny, nx))

    return visited


def trim_tile(
    input_path: Path,
    output_path: Path,
    *,
    tolerance: float = 30.0,
    padding: int = 0,
    force: bool = False,
) -> bool:
    if output_path.exists() and not force:
        print(f"  skip (exists): {output_path.name}")
        return False

    img = Image.open(input_path).convert("RGBA")
    arr = np.array(img)

    bg_color = detect_bg_color(arr)
    bg_mask = build_bg_mask(arr, bg_color, tolerance)

    arr[bg_mask, 3] = 0

    result = Image.fromarray(arr, "RGBA")

    bbox = result.getbbox()
    if bbox is None:
        print(f"  warning: entirely transparent after removal — skipping: {input_path.name}", file=sys.stderr)
        return False

    if padding > 0:
        x1, y1, x2, y2 = bbox
        bw, bh = result.size
        bbox = (max(0, x1 - padding), max(0, y1 - padding),
                min(bw, x2 + padding), min(bh, y2 + padding))

    result = result.crop(bbox)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    result.save(output_path, format="PNG")
    print(f"  {input_path.name} -> {output_path.name}  {img.size} -> {result.size}")
    return True


def parse_args() -> argparse.Namespace:
    p = argparse.ArgumentParser(description="Remove background and crop hex tiles")
    mode = p.add_mutually_exclusive_group(required=True)
    mode.add_argument("--image", help="Single input image")
    mode.add_argument("--source", help="Source directory (batch mode)")

    p.add_argument("--out", help="Output path (single mode)")
    p.add_argument("--out-dir", help="Output directory (batch mode)")
    p.add_argument("--tolerance", type=float, default=30.0,
                   help="Color distance tolerance for background detection (default 30)")
    p.add_argument("--padding", type=int, default=0,
                   help="Extra transparent pixels to leave around content after crop (default 0)")
    p.add_argument("--force", action="store_true", help="Overwrite existing outputs")
    p.add_argument("--dry-run", action="store_true", help="Print what would be done without writing files")
    return p.parse_args()


def main() -> int:
    args = parse_args()

    try:
        import numpy  # noqa: F401
        from PIL import Image  # noqa: F401
    except ImportError as e:
        print(f"Missing dependency: {e}. Install with: uv pip install pillow numpy", file=sys.stderr)
        return 1

    if args.image:
        if not args.out:
            print("Error: --out is required with --image", file=sys.stderr)
            return 1
        input_path = Path(args.image)
        output_path = Path(args.out)
        if args.dry_run:
            print(f"dry-run: {input_path} -> {output_path}  tolerance={args.tolerance}  padding={args.padding}")
            return 0
        trim_tile(input_path, output_path, tolerance=args.tolerance, padding=args.padding, force=args.force)
        return 0

    # batch mode
    source_dir = Path(args.source)
    out_dir = Path(args.out_dir) if args.out_dir else source_dir.parent / "alpha"
    tiles = sorted(source_dir.glob("*.png"))

    if not tiles:
        print(f"No PNG files found in {source_dir}", file=sys.stderr)
        return 1

    print(f"{len(tiles)} tiles  |  source: {source_dir}  ->  out: {out_dir}")
    if args.dry_run:
        for t in tiles:
            print(f"  would trim: {t.name}")
        return 0

    done = 0
    for tile in tiles:
        out_path = out_dir / tile.name
        if trim_tile(tile, out_path, tolerance=args.tolerance, padding=args.padding, force=args.force):
            done += 1

    print(f"\nDone. {done}/{len(tiles)} tiles trimmed -> {out_dir}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
