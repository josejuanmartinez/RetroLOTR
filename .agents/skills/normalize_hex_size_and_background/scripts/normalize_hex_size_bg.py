#!/usr/bin/env python3
"""
Normalize a hex tile's SIZE, BACKGROUND, and Unity import settings so it matches
the rest of the RetroLOTR tile set.

Takes a "raw" tile — typically AI/restyle output or a hand-exported image that has
a SOLID opaque background (white or black), the wrong canvas size/proportion, and
non-canonical sprite import settings — and produces a tile that matches the
canonical set:

  1. Background keying: if the border is mostly opaque, flood-fill the background
     color from the edges to alpha (interior black/white art is preserved because
     it is not connected to the border). Auto-detects white or black; override
     with --bg.
  2. Footprint detection: find the pointy-top hexagon from the alpha silhouette.
  3. Rescale uniformly so the footprint width == --target-width (default 773).
  4. Recompose onto a fixed --canvas (default 974x1314) with the footprint center
     at the canonical point (canvas center + CENTER_DROP_FRAC*width below), so
     Unity's center-pivot import keeps the terrain where the game expects it.
  5. Patch the sibling .meta to canonical sprite settings (spriteMode 1,
     alignment 0 = Center, spritePivot {0.5,0.5}, spritePixelsToUnits = --ppu).
     The GUID is never touched.

Reuses the detection + background-keying from the sibling `restyle_hex` skill.

Usage:
    # single tile, overwrite in place (patches its .meta too)
    python normalize_size_bg.py --image Assets/Art/Hexes/Tiles/shore_04.png

    # single tile to a new path
    python normalize_size_bg.py --image raw/shore_04.png --out Assets/Art/Hexes/Tiles/shore_04.png

    # whole folder, overwrite in place
    python normalize_size_bg.py --source Assets/Art/Hexes/Tiles/incoming

    # inspect without writing
    python normalize_size_bg.py --image foo.png --dry-run

Always eyeball the result: white/black keying can nibble into art of the same
color. If detection warns (slope-prior or inscribed fallback), check the output.
"""

from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

import numpy as np
from PIL import Image

# --- reuse the canonical detection + bg keying from the restyle_hex skill ------
_RESTYLE_SCRIPTS = Path(__file__).resolve().parent.parent.parent / "restyle_hex" / "scripts"
if not _RESTYLE_SCRIPTS.is_dir():
    sys.exit(f"Error: expected sibling skill scripts at {_RESTYLE_SCRIPTS} (restyle_hex). "
             "This skill depends on restyle_hex.")
sys.path.insert(0, str(_RESTYLE_SCRIPTS))

from trim_hex import detect_bg_color, build_bg_mask           # noqa: E402
from normalize_hex_tiles import detect, ALPHA_THRESHOLD, CENTER_DROP_FRAC  # noqa: E402

# --- canonical defaults (measured across the existing 208-tile set) -----------
DEFAULT_TARGET_WIDTH = 773
DEFAULT_CANVAS = (974, 1314)
DEFAULT_PPU = 750
DEFAULT_TOLERANCE = 30.0
BORDER_OPAQUE_FRAC = 0.5   # above this, the tile has a solid background to key out

def _meta_subs(ppu: int) -> list[tuple[str, str]]:
    # Replacements are literal (re.sub treats only backslashes specially); the
    # pivot value contains braces, so never run str.format over these.
    return [
        (r'(?m)^  spriteMode: .*$',          '  spriteMode: 1'),
        (r'(?m)^  alignment: .*$',           '  alignment: 0'),
        (r'(?m)^  spritePivot: .*$',         '  spritePivot: {x: 0.5, y: 0.5}'),
        (r'(?m)^  spritePixelsToUnits: .*$', f'  spritePixelsToUnits: {ppu}'),
    ]


def parse_canvas(s: str) -> tuple[int, int]:
    m = re.fullmatch(r'\s*(\d+)\s*[xX]\s*(\d+)\s*', s)
    if not m:
        raise argparse.ArgumentTypeError(f"--canvas must look like 974x1314, got {s!r}")
    return int(m.group(1)), int(m.group(2))


def parse_bg(s: str) -> str | np.ndarray:
    s = s.strip().lower()
    if s in ("auto", "white", "black"):
        return s
    m = re.fullmatch(r'(\d+)\s*,\s*(\d+)\s*,\s*(\d+)', s)
    if not m:
        raise argparse.ArgumentTypeError(f"--bg must be auto|white|black|R,G,B, got {s!r}")
    return np.array([int(x) for x in m.groups()], dtype=np.float32)


def border_opaque_frac(a: np.ndarray) -> float:
    al = a[:, :, 3]
    border = np.concatenate([al[0, :], al[-1, :], al[:, 0], al[:, -1]])
    return float((border > ALPHA_THRESHOLD).mean())


def patch_meta(meta_path: Path, ppu: int) -> bool:
    """Patch sprite import settings to canonical, preserving the GUID. Returns True if written."""
    if not meta_path.exists():
        print(f"  note: no meta at {meta_path.name} (Unity will generate one; PPU/pivot not set)")
        return False
    t = orig = meta_path.read_text(errors="ignore")
    guid_before = re.search(r'guid: \w+', orig)
    for pat, rep in _meta_subs(ppu):
        t, c = re.subn(pat, rep, t)
        if c != 1:
            print(f"  warn: meta field {pat!r} matched {c}x in {meta_path.name} (left as-is)")
    guid_after = re.search(r'guid: \w+', t)
    if guid_before and guid_after and guid_before.group() != guid_after.group():
        print(f"  ERROR: refusing to write {meta_path.name} — GUID changed", file=sys.stderr)
        return False
    if t != orig:
        meta_path.write_text(t)
    return True


def normalize_one(
    src: Path,
    out: Path,
    *,
    target_width: int,
    canvas: tuple[int, int],
    ppu: int,
    bg: str | np.ndarray,
    tolerance: float,
    patch_meta_file: bool,
    dry_run: bool,
    force: bool,
) -> int:
    if not src.exists():
        print(f"Error: not found: {src}", file=sys.stderr)
        return 1
    if out.exists() and not force and out.resolve() != src.resolve():
        print(f"Error: output exists: {out} (use --force)", file=sys.stderr)
        return 1

    img = Image.open(src).convert("RGBA")
    a = np.array(img)

    force_key = not isinstance(bg, str)
    keyed = False
    if force_key or border_opaque_frac(a) > BORDER_OPAQUE_FRAC:
        if isinstance(bg, str):
            if bg == "white":
                bg_color = np.array([255, 255, 255], dtype=np.float32)
            elif bg == "black":
                bg_color = np.array([0, 0, 0], dtype=np.float32)
            else:  # auto
                bg_color = detect_bg_color(a)
        else:
            bg_color = bg
        mask = build_bg_mask(a, bg_color, tolerance)
        a[mask, 3] = 0
        keyed = True
        bg_desc = ",".join(str(int(x)) for x in bg_color)
    else:
        bg_desc = "already-alpha (no keying)"

    amask = a[:, :, 3] > ALPHA_THRESHOLD
    try:
        r = detect(amask)
    except Exception as e:  # noqa: BLE001
        print(f"Error: footprint detection failed for {src.name}: {e}", file=sys.stderr)
        return 1

    s = target_width / r["width"]
    cx = (r["xl"] + r["xr"]) / 2
    cy = r["vertex_y"] - r["width"] / 2
    cw, ch = canvas
    drop = CENTER_DROP_FRAC * target_width

    print(f"  {src.name}: bg={bg_desc} keyed={keyed} detW={r['width']:.0f} "
          f"scale={s:.3f} warn={r['warnings'] or 'none'}")

    if dry_run:
        print(f"    -> would write {out}  ({cw}x{ch}, footprint {target_width}px, "
              f"center {cw//2},{ch//2 + drop:.0f}, ppu {ppu})")
        return 0

    scaled = Image.fromarray(a, "RGBA").resize(
        (max(1, round(img.width * s)), max(1, round(img.height * s))), Image.LANCZOS)
    out_canvas = Image.new("RGBA", (cw, ch), (0, 0, 0, 0))
    px = round(cw / 2 - cx * s)
    py = round(ch / 2 + drop - cy * s)
    out_canvas.alpha_composite(scaled, (px, py))
    out.parent.mkdir(parents=True, exist_ok=True)
    out_canvas.save(out, format="PNG")

    if patch_meta_file:
        patch_meta(out.with_name(out.name + ".meta"), ppu)

    print(f"    -> {out}  OK")
    return 0


def parse_args() -> argparse.Namespace:
    p = argparse.ArgumentParser(description="Normalize hex tile size + background + meta")
    mode = p.add_mutually_exclusive_group(required=True)
    mode.add_argument("--image", help="single input tile")
    mode.add_argument("--source", help="folder of tiles (batch)")
    p.add_argument("--out", help="output path (single; default: overwrite --image)")
    p.add_argument("--out-dir", help="output folder (batch; default: overwrite in place)")
    p.add_argument("--target-width", type=int, default=DEFAULT_TARGET_WIDTH,
                   help=f"footprint width in px (default {DEFAULT_TARGET_WIDTH})")
    p.add_argument("--canvas", type=parse_canvas, default=DEFAULT_CANVAS,
                   help="canvas WxH (default 974x1314)")
    p.add_argument("--ppu", type=int, default=DEFAULT_PPU,
                   help=f"spritePixelsToUnits for the meta (default {DEFAULT_PPU})")
    p.add_argument("--bg", type=parse_bg, default="auto",
                   help="background to key: auto|white|black|R,G,B (default auto). "
                        "A non-auto value forces keying even if the border looks transparent.")
    p.add_argument("--tolerance", type=float, default=DEFAULT_TOLERANCE,
                   help=f"bg color distance tolerance (default {DEFAULT_TOLERANCE})")
    p.add_argument("--no-meta", action="store_true", help="do not patch .meta files")
    p.add_argument("--dry-run", action="store_true", help="report only, write nothing")
    p.add_argument("--force", action="store_true", help="overwrite existing outputs")
    return p.parse_args()


def main() -> int:
    args = parse_args()
    common = dict(
        target_width=args.target_width, canvas=args.canvas, ppu=args.ppu,
        bg=args.bg, tolerance=args.tolerance, patch_meta_file=not args.no_meta,
        dry_run=args.dry_run, force=args.force,
    )

    if args.image:
        src = Path(args.image)
        out = Path(args.out) if args.out else src
        return normalize_one(src, out, **common)

    # batch
    source = Path(args.source)
    tiles = sorted(source.glob("*.png"))
    if not tiles:
        print(f"No PNGs in {source}", file=sys.stderr)
        return 1
    out_dir = Path(args.out_dir) if args.out_dir else None
    print(f"{len(tiles)} tiles in {source}" + (f" -> {out_dir}" if out_dir else " (in place)"))
    rc = 0
    for t in tiles:
        out = (out_dir / t.name) if out_dir else t
        rc |= normalize_one(t, out, **common)
    return rc


if __name__ == "__main__":
    raise SystemExit(main())
