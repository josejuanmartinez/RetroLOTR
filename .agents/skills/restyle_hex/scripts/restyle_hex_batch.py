#!/usr/bin/env python3
"""Batch restyle hex tiles with live cost tracking. Ctrl+C stops cleanly."""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

from restyle_hex import (
    DEFAULT_PROMPT,
    DEFAULT_QUALITY,
    DEFAULT_SIZE,
    STYLE_ONLY_PROMPT,
    build_prompt,
    ensure_api_key,
    restyle_one,
)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Batch restyle all hex tiles — Ctrl+C to stop at any time"
    )
    parser.add_argument(
        "--source", default="Assets/Art/Hexes/Tiles/original",
        help="Folder containing source PNG tiles",
    )
    parser.add_argument(
        "--out", default="Assets/Art/Hexes/Tiles_Restyled",
        help="Folder to write restyled tiles",
    )
    parser.add_argument("--prompt", default=None,
                        help="Override the base prompt (defaults to the standard or style-only prompt)")
    parser.add_argument("--style-only", action="store_true",
                        help="Faithful restyle: change only the art style, never add or redesign features "
                             "(towers, rivers, bridges, etc.)")
    parser.add_argument("--quality", default=DEFAULT_QUALITY)
    parser.add_argument("--size", default=DEFAULT_SIZE)
    parser.add_argument("--force", action="store_true", help="Overwrite existing outputs")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    ensure_api_key()

    source_dir = Path(args.source)
    out_dir = Path(args.out)

    base_prompt = args.prompt
    if base_prompt is None:
        base_prompt = STYLE_ONLY_PROMPT if args.style_only else DEFAULT_PROMPT

    all_tiles = sorted(source_dir.glob("*.png"))
    if not all_tiles:
        print(f"No PNG files found in {source_dir}", file=sys.stderr)
        return 1

    if not args.force:
        tiles = [t for t in all_tiles if not (out_dir / t.name).exists()]
        already_done = len(all_tiles) - len(tiles)
        if already_done:
            print(f"Skipping {already_done} already-restyled tiles (use --force to redo).")
    else:
        tiles = all_tiles

    total = len(tiles)
    if total == 0:
        print("All tiles already restyled.")
        return 0

    print(f"{total} tiles to restyle  |  ~${total * 0.008:.2f} estimated total  (Ctrl+C to stop)")
    print()

    accumulated = 0.0
    done = 0
    skipped = 0

    try:
        for i, tile in enumerate(tiles, 1):
            out_path = out_dir / tile.name
            tile_prompt = build_prompt(base_prompt, tile.stem, style_only=args.style_only)
            code, cost = restyle_one(
                tile, out_path,
                prompt=tile_prompt,
                quality=args.quality,
                size=args.size,
                force=args.force,
            )
            if code == 0:
                done += 1
                accumulated += cost
            elif code == 2:
                skipped += 1

            print(f"  [{i}/{total}]  done={done}  skipped={skipped}  accumulated=${accumulated:.4f}", flush=True)

    except KeyboardInterrupt:
        print(f"\nStopped.  {done} done  |  {skipped} moderation skips  |  ${accumulated:.4f} spent")
        return 130

    print()
    print(f"Finished.  {done} restyled  |  {skipped} moderation skips  |  ${accumulated:.4f} total")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
