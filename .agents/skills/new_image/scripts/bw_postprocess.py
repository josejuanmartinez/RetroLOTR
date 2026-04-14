#!/usr/bin/env python3
"""Strict black-and-white postprocess helper for RetroLOTR card art."""

from __future__ import annotations

import argparse
from pathlib import Path

import numpy as np
from PIL import Image, ImageOps


DEFAULT_CONTRAST_BOOST = 1.35
DEFAULT_THRESHOLD = -1
DEFAULT_DITHER = False


def to_pure_bw(
    img: Image.Image,
    contrast_boost: float = DEFAULT_CONTRAST_BOOST,
    threshold: int = DEFAULT_THRESHOLD,
    dither: bool = DEFAULT_DITHER,
) -> Image.Image:
    """Convert an image to strict 0/255 black and white."""
    g = img.convert("L")
    g = ImageOps.autocontrast(g)
    if contrast_boost and contrast_boost != 1.0:
        lut = [int(max(0, min(255, 128 + (i - 128) * contrast_boost))) for i in range(256)]
        g = g.point(lut, mode="L")

    if threshold is None or int(threshold) < 0:
        hist = np.array(g.histogram(), dtype=np.float64)
        total = g.width * g.height
        sum_total = np.dot(np.arange(256), hist)
        sum_b = 0.0
        w_b = 0.0
        max_between = -1.0
        level = 128
        for t in range(256):
            w_b += hist[t]
            if w_b == 0:
                continue
            w_f = total - w_b
            if w_f == 0:
                break
            sum_b += t * hist[t]
            m_b = sum_b / w_b
            m_f = (sum_total - sum_b) / w_f
            between = w_b * w_f * (m_b - m_f) ** 2
            if between > max_between:
                max_between = between
                level = t
        th = level
    else:
        th = int(threshold)

    if dither:
        bw1 = g.convert("1", dither=Image.FLOYDSTEINBERG)
        bw = bw1.convert("L").point(lambda p: 255 if p > 0 else 0, mode="L")
    else:
        bw = g.point(lambda p: 255 if p > th else 0, mode="L")

    return bw


def build_output_path(path: str) -> Path:
    out_path = Path(path)
    if out_path.suffix == "":
        return out_path.with_suffix(".png")
    return out_path


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Convert a card image to strict black and white")
    parser.add_argument("--image", required=True, help="Path to the source image")
    parser.add_argument("--out", required=True, help="Path to write the B&W image")
    parser.add_argument("--contrast-boost", type=float, default=DEFAULT_CONTRAST_BOOST)
    parser.add_argument("--threshold", type=int, default=DEFAULT_THRESHOLD)
    parser.add_argument("--dither", action="store_true")
    parser.add_argument("--force", action="store_true")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    image_path = Path(args.image)
    if not image_path.exists():
        raise SystemExit(f"Error: image file not found: {image_path}")

    out_path = build_output_path(args.out)
    if out_path.exists() and not args.force:
        raise SystemExit(f"Error: output already exists: {out_path} (use --force to overwrite)")

    with Image.open(image_path) as img:
        bw = to_pure_bw(img, contrast_boost=args.contrast_boost, threshold=args.threshold, dither=args.dither)

    out_path.parent.mkdir(parents=True, exist_ok=True)
    bw.save(out_path, format="PNG")
    print(f"Wrote {out_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
