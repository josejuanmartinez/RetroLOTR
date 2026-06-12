#!/usr/bin/env python3
"""
Normalize hex tile sprites so every tile has the same hexagon footprint
size and position on a uniform canvas.

Canonical footprint (verified against clean tiles like hexOcean00 / 1027 / 069):
pointy-top hexagon, width W, total height W:
  - top cap        W/4  (slanted edges, dx/dy = 2)
  - vertical sides W/2
  - bottom cap     W/4  (slanted edges meeting at the bottom vertex)
Art may overhang above and sideways in the upper half; a dark "skirt" of
varying thickness hangs below the surface along the bottom edges, and the
hand-painted sides can wobble by ~20px, so detection leans on the bottom
cap, which is clean on every tile.

Detection per tile (alpha silhouette only):
  1. Robust line fits to the per-row left/right silhouette extents in a
     band above the bottommost row (inside the bottom cap).
  2. Bottom vertex = intersection of the two lines.
  3. Corners = walking up each line, the row where the silhouette departs
     inward from the line for good (the cap edge ended at the side).
  4. Footprint width W = distance between the lines at corner height.
  5. Sanity checks: vertex centered, cap height ~ W/4, plausible slopes.

Normalization: uniform Lanczos rescale so the footprint width equals the
target, then paste on a shared canvas with the footprint center exactly at
the canvas center (keeps Unity's center pivot / PPU import settings valid).

Usage:
    python normalize_hex_tiles.py --analyze --csv report.csv   # report only
    python normalize_hex_tiles.py --out-dir TileNormalization/output
    python normalize_hex_tiles.py --out-dir ... --target-width 760
"""

from __future__ import annotations

import argparse
import csv
import sys
from pathlib import Path

import numpy as np
from PIL import Image

ALPHA_THRESHOLD = 8
DEPART_TOLERANCE = 6      # px the silhouette may stray from the cap edge line
DEPART_RUN = 12           # consecutive departed rows that confirm the corner

# population medians measured across the clean tiles; used by the fallback
# detector when art hanging below the hexagon contaminates the silhouette
PRIOR_SLOPE_L = 1.906
PRIOR_SLOPE_R = -1.912
PRIOR_CAP_RATIO = 0.262

# special non-hex tiles (edge "curtains", unused in code) — copied unchanged
EXCLUDE = {"hexUnderOcean00.png", "hexUndercliff00.png"}

# in the original tiles the footprint center sits a median of +0.071*W below
# the canvas center (= the sprite pivot). Baking the same drop into the new
# canvas keeps the terrain exactly where the game was tuned for it.
CENTER_DROP_FRAC = 0.071


class DetectionError(Exception):
    pass


def row_extents(mask: np.ndarray):
    """Per-row leftmost/rightmost opaque x. Rows with no content get -1."""
    h, w = mask.shape
    any_row = mask.any(axis=1)
    left = np.where(any_row, mask.argmax(axis=1), -1)
    right = np.where(any_row, w - 1 - mask[:, ::-1].argmax(axis=1), -1)
    return left, right, any_row


def robust_line(ys: np.ndarray, xs: np.ndarray):
    """Least squares x = m*y + c with iterative outlier rejection."""
    m, c = np.polyfit(ys, xs, 1)
    for _ in range(3):
        resid = xs - (m * ys + c)
        keep = np.abs(resid - np.median(resid)) < 4.0
        if keep.sum() < 10:
            break
        m, c = np.polyfit(ys[keep], xs[keep], 1)
    return m, c


def find_corner(extent: np.ndarray, valid: np.ndarray, m: float, c: float,
                bottom_y: int, inward_sign: int) -> int:
    """
    Walk up from the bottom vertex along the fitted cap edge line; the corner
    is where the silhouette stops following the line (departs inward for
    DEPART_RUN consecutive rows). inward_sign: +1 for left edge, -1 for right.
    Rows where the silhouette pokes OUTSIDE the line are art occluding the
    edge — they neither confirm the edge nor count as departure.
    """
    run = 0
    corner = None
    for y in range(bottom_y, max(bottom_y - len(extent), 0), -1):
        if y < 0:
            break
        if not valid[y]:
            continue
        pred = m * y + c
        dev = (extent[y] - pred) * inward_sign  # positive = inside the line
        if dev > DEPART_TOLERANCE:
            if run == 0:
                corner = y + 1
            run += 1
            if run >= DEPART_RUN:
                return corner
        elif dev >= -DEPART_TOLERANCE:
            run = 0  # still on the edge
        # else: outside the line (occluding art) — skip the row
    raise DetectionError("cap edge never departs (no corner found)")


def detect_footprint(mask: np.ndarray) -> dict:
    h, w = mask.shape
    left, right, valid = row_extents(mask)
    if not valid.any():
        raise DetectionError("empty image")
    bottom_y = int(np.where(valid)[0].max())

    def fit(band_top):
        ys = np.arange(max(0, band_top), bottom_y + 1)
        ys = ys[valid[ys]]
        if len(ys) < 20:
            raise DetectionError("bottom cap band too short")
        ys = ys[: max(10, int(len(ys) * 0.92))]  # drop rows at the very tip
        ml, cl = robust_line(ys, left[ys].astype(float))
        mr, cr = robust_line(ys, right[ys].astype(float))
        return ml, cl, mr, cr

    # initial fit on a band safely inside the bottom cap (cap ~ 0.24 * canvas w)
    ml, cl, mr, cr = fit(bottom_y - int(0.16 * w))

    corner_l = find_corner(left, valid, ml, cl, bottom_y, +1)
    corner_r = find_corner(right, valid, mr, cr, bottom_y, -1)

    # refit using the full cap now that the corners are known, then redo corners
    ml, cl, mr, cr = fit(min(corner_l, corner_r))
    corner_l = find_corner(left, valid, ml, cl, bottom_y, +1)
    corner_r = find_corner(right, valid, mr, cr, bottom_y, -1)

    if abs(ml - mr) < 1e-6:
        raise DetectionError("bottom edges parallel")
    yv = (cr - cl) / (ml - mr)
    xv = ml * yv + cl

    xl = ml * corner_l + cl
    xr = mr * corner_r + cr
    width = xr - xl
    if width < 0.4 * w:
        raise DetectionError(f"footprint width {width:.0f} implausible vs canvas {w}")

    warnings = []
    if abs(corner_l - corner_r) > 0.05 * width:
        warnings.append(f"corner rows differ by {abs(corner_l - corner_r)}px")
    center_off = abs(xv - (xl + xr) / 2) / width
    if center_off > 0.05:
        warnings.append(f"vertex off-center by {center_off:.1%}")
    cap_ratio = (yv - (corner_l + corner_r) / 2) / width
    if not (0.18 <= cap_ratio <= 0.36):
        warnings.append(f"cap/width {cap_ratio:.3f} outside [0.18, 0.36]")
    if not (1.4 <= ml <= 2.7) or not (-2.7 <= mr <= -1.4):
        warnings.append(f"slopes {ml:.2f}/{mr:.2f} unusual")

    return {
        "xl": float(xl), "xr": float(xr), "width": float(width),
        "corner_y": (corner_l + corner_r) / 2,
        "vertex_x": float(xv), "vertex_y": float(yv),
        "slope_l": float(ml), "slope_r": float(mr),
        "cap_ratio": float(cap_ratio),
        "warnings": warnings,
    }


def detect_footprint_fallback(mask: np.ndarray) -> dict:
    """
    For tiles where art hangs below the hexagon and contaminates the
    silhouette: fix the cap edge slopes to the population medians and find
    each edge's intercept as the mode over rows in the bottom region — the
    true edge contributes the longest consistent run of intercepts, while
    dangling art scatters.
    """
    h, w = mask.shape
    left, right, valid = row_extents(mask)
    if not valid.any():
        raise DetectionError("empty image")
    rows = np.where(valid)[0]
    bottom_y, top_y = int(rows.max()), int(rows.min())
    band_top = bottom_y - int(0.38 * (bottom_y - top_y))
    ys = np.arange(max(0, band_top), bottom_y + 1)
    ys = ys[valid[ys]]
    if len(ys) < 30:
        raise DetectionError("fallback band too short")

    def mode_intercept(extent, m, inward_sign):
        """Strongest intercept line; among well-supported candidates prefer
        the most INWARD one — dangling art always lies outside the edge."""
        c = extent[ys].astype(float) - m * ys
        bins = np.round(c / 3.0)
        vals, counts = np.unique(bins, return_counts=True)
        strong = vals[counts >= max(20, int(counts.max() * 0.45))]
        if len(strong) == 0:
            raise DetectionError("no consistent edge intercept")
        best = strong.max() if inward_sign > 0 else strong.min()
        sel = np.abs(bins - best) <= 1
        if sel.sum() < 15:
            raise DetectionError("no consistent edge intercept")
        return float(np.median(c[sel]))

    ml, mr = PRIOR_SLOPE_L, PRIOR_SLOPE_R
    cl = mode_intercept(left, ml, +1)
    cr = mode_intercept(right, mr, -1)

    corner_l = find_corner(left, valid, ml, cl, bottom_y, +1)
    corner_r = find_corner(right, valid, mr, cr, bottom_y, -1)

    yv = (cr - cl) / (ml - mr)
    xv = ml * yv + cl
    xl = ml * corner_l + cl
    xr = mr * corner_r + cr
    width = xr - xl
    if width < 0.4 * w:
        raise DetectionError(f"fallback width {width:.0f} implausible vs canvas {w}")

    warnings = ["fallback detector (slope priors)"]
    center_off = abs(xv - (xl + xr) / 2) / width
    cap_ratio = (yv - (corner_l + corner_r) / 2) / width
    if center_off > 0.06:
        raise DetectionError(f"fallback vertex off-center by {center_off:.1%}")
    if not (0.18 <= cap_ratio <= 0.36):
        raise DetectionError(f"fallback cap/width {cap_ratio:.3f} implausible")
    if abs(corner_l - corner_r) > 0.08 * width:
        warnings.append(f"corner rows differ by {abs(corner_l - corner_r)}px")

    return {
        "xl": float(xl), "xr": float(xr), "width": float(width),
        "corner_y": (corner_l + corner_r) / 2,
        "vertex_x": float(xv), "vertex_y": float(yv),
        "slope_l": float(ml), "slope_r": float(mr),
        "cap_ratio": float(cap_ratio),
        "warnings": warnings,
    }


def hex_template(width: int) -> np.ndarray:
    """Boolean mask of the canonical footprint hexagon: caps W/4, sides W/2."""
    w = width
    h = w
    yy, xx = np.mgrid[0:h, 0:w]
    cap = w / 4.0
    cx = (w - 1) / 2.0
    half = np.minimum((yy + 1) * 2.0, w / 2.0)            # top cap opens at dx/dy=2
    half = np.minimum(half, (h - yy) * 2.0)               # bottom cap closes
    return np.abs(xx - cx) <= half


def detect_footprint_inscribed(mask: np.ndarray) -> dict:
    """
    Last resort for tiles whose bottom edges are fully covered by art:
    the largest canonical hexagon fully inscribed in the alpha mask.
    Art only ever adds pixels outside the footprint, so the largest
    inscribed hexagon approximates the footprint itself.
    """
    f = 4  # coarse factor
    h, w = mask.shape
    ch, cw = h // f, w // f
    coarse = mask[: ch * f, : cw * f].reshape(ch, f, cw, f).mean(axis=(1, 3)) > 0.5
    outside = (~coarse).astype(np.float32)

    best = None
    for s in range(int(cw * 0.96), int(cw * 0.45), -2):
        tpl = hex_template(s).astype(np.float32)
        # missing[y, x] = hexagon pixels not covered by alpha at placement (y, x)
        fh, fw = ch + s - 1, cw + s - 1
        F = np.fft.rfft2(outside, (fh, fw)) * np.fft.rfft2(tpl[::-1, ::-1], (fh, fw))
        missing = np.fft.irfft2(F, (fh, fw))[s - 1: ch, s - 1: cw]
        if missing.size == 0:
            continue
        allowed = 0.015 * tpl.sum()
        idx = np.unravel_index(np.argmin(missing), missing.shape)
        if missing[idx] <= allowed:
            best = (s, idx[0], idx[1])  # top-left corner of template in coarse px
            break
    if best is None:
        raise DetectionError("no inscribed hexagon found")

    s, ty, tx = best
    # refine at full resolution with a local brute-force search
    best_full = None
    for fs in range(s * f - f, s * f + f + 1, 2):
        tpl = hex_template(fs)
        area = tpl.sum()
        for oy in range(ty * f - f, ty * f + f + 1, 2):
            for ox in range(tx * f - f, tx * f + f + 1, 2):
                if oy < 0 or ox < 0 or oy + fs > h or ox + fs > w:
                    continue
                covered = mask[oy: oy + fs, ox: ox + fs][tpl].sum()
                score = covered - (area - covered) * 50  # heavy miss penalty
                if best_full is None or score > best_full[0]:
                    best_full = (score, fs, oy, ox)
    if best_full is None:
        raise DetectionError("inscribed refinement failed")

    _, fs, oy, ox = best_full
    width = float(fs)
    return {
        "xl": float(ox), "xr": float(ox + fs - 1), "width": width,
        "corner_y": oy + 0.75 * fs,
        "vertex_x": ox + (fs - 1) / 2.0, "vertex_y": float(oy + fs),
        "slope_l": 2.0, "slope_r": -2.0, "cap_ratio": 0.25,
        "warnings": ["inscribed-hexagon detector"],
    }


def detect(mask: np.ndarray) -> dict:
    """Primary detector; slope-prior fallback when the silhouette is
    contaminated; inscribed-hexagon search as the last resort."""
    primary = None
    try:
        primary = detect_footprint(mask)
        if not primary["warnings"]:
            return primary
    except DetectionError:
        pass
    try:
        return detect_footprint_fallback(mask)
    except DetectionError:
        pass
    try:
        return detect_footprint_inscribed(mask)
    except DetectionError:
        # keep a mildly-warned primary; severe geometry warnings mean garbage
        if primary is not None and not any(
            "off-center" in w or "outside" in w or "unusual" in w
            for w in primary["warnings"]
        ):
            return primary
        raise DetectionError("all detectors failed")


def load_mask(path: Path):
    img = Image.open(path).convert("RGBA")
    arr = np.array(img)
    return img, arr[:, :, 3] > ALPHA_THRESHOLD


def analyze(tiles: list[Path], csv_path: Path | None):
    results, failures = {}, {}
    for t in tiles:
        if t.name in EXCLUDE:
            continue
        try:
            _, mask = load_mask(t)
            results[t.name] = detect(mask)
        except DetectionError as e:
            failures[t.name] = str(e)

    widths = np.array([r["width"] for r in results.values()])
    caps = np.array([r["cap_ratio"] for r in results.values()])
    warned = {n: r["warnings"] for n, r in results.items() if r["warnings"]}

    print(f"detected: {len(results)}  failed: {len(failures)}  warned: {len(warned)}")
    if len(widths):
        print(f"footprint width: median={np.median(widths):.0f} "
              f"min={widths.min():.0f} max={widths.max():.0f}")
        print(f"cap/width: median={np.median(caps):.3f} "
              f"p5={np.percentile(caps, 5):.3f} p95={np.percentile(caps, 95):.3f}")
    for n, e in sorted(failures.items()):
        print(f"  FAIL {n}: {e}")
    for n, ws in sorted(warned.items()):
        print(f"  WARN {n}: {'; '.join(ws)}")

    if csv_path:
        with open(csv_path, "w", newline="") as f:
            wtr = csv.writer(f)
            wtr.writerow(["name", "width", "vertex_x", "vertex_y", "corner_y",
                          "cap_ratio", "slope_l", "slope_r", "warnings"])
            for n, r in sorted(results.items()):
                wtr.writerow([n, f"{r['width']:.1f}", f"{r['vertex_x']:.1f}",
                              f"{r['vertex_y']:.1f}", f"{r['corner_y']:.1f}",
                              f"{r['cap_ratio']:.3f}", f"{r['slope_l']:.3f}",
                              f"{r['slope_r']:.3f}", "; ".join(r["warnings"])])
        print(f"report -> {csv_path}")
    return results, failures


def normalize(tiles: list[Path], out_dir: Path, target_width: int | None):
    results, failures = analyze(tiles, None)
    if not results:
        print("nothing detected, aborting", file=sys.stderr)
        return 1

    widths = np.array([r["width"] for r in results.values()])
    tw = target_width or int(round(np.median(widths)))
    print(f"\ntarget footprint width: {tw}")

    # First pass: canvas size so every scaled tile fits with the footprint
    # center at (canvas_w/2, canvas_h/2 + drop).
    drop = CENTER_DROP_FRAC * tw
    half_w = half_h = 0.0
    placements = {}
    for t in tiles:
        r = results.get(t.name)
        if r is None:
            continue
        with Image.open(t) as img:
            w, h = img.size
        s = tw / r["width"]
        # footprint center in source px: between corners, W/2 above the vertex
        cx = (r["xl"] + r["xr"]) / 2
        cy = r["vertex_y"] - r["width"] / 2
        placements[t.name] = (s, cx, cy)
        half_w = max(half_w, s * cx, s * (w - cx))
        half_h = max(half_h, s * cy - drop, s * (h - cy) + drop)

    canvas_w = 2 * int(np.ceil(half_w)) + 2
    canvas_h = 2 * int(np.ceil(half_h)) + 2
    print(f"canvas: {canvas_w} x {canvas_h}  (footprint center {drop:.0f}px below canvas center)")

    out_dir.mkdir(parents=True, exist_ok=True)
    done = 0
    for t in tiles:
        if t.name not in placements:
            if t.name in EXCLUDE:
                Image.open(t).save(out_dir / t.name, format="PNG")
                print(f"  copy unchanged (excluded): {t.name}")
            else:
                print(f"  skip (detection failed): {t.name}")
            continue
        s, cx, cy = placements[t.name]
        img = Image.open(t).convert("RGBA")
        new_size = (max(1, round(img.width * s)), max(1, round(img.height * s)))
        scaled = img.resize(new_size, Image.LANCZOS)
        canvas = Image.new("RGBA", (canvas_w, canvas_h), (0, 0, 0, 0))
        px = round(canvas_w / 2 - cx * s)
        py = round(canvas_h / 2 + drop - cy * s)
        canvas.alpha_composite(scaled, (px, py))
        canvas.save(out_dir / t.name, format="PNG")
        done += 1

    print(f"\nDone. {done}/{len(tiles)} tiles -> {out_dir}")
    if failures:
        print(f"NOT normalized ({len(failures)}): {', '.join(failures)}")
    return 0


def main() -> int:
    p = argparse.ArgumentParser(description="Normalize hex tile footprints")
    p.add_argument("--source", default="Assets/Art/Hexes/Tiles")
    p.add_argument("--analyze", action="store_true", help="report only")
    p.add_argument("--csv", help="write analysis CSV here")
    p.add_argument("--out-dir", help="output directory for normalized tiles")
    p.add_argument("--target-width", type=int,
                   help="footprint width in px (default: median of detected)")
    args = p.parse_args()

    tiles = sorted(Path(args.source).glob("*.png"))
    if not tiles:
        print(f"no PNGs in {args.source}", file=sys.stderr)
        return 1
    print(f"{len(tiles)} tiles in {args.source}")

    if args.analyze or not args.out_dir:
        analyze(tiles, Path(args.csv) if args.csv else None)
        return 0
    return normalize(tiles, Path(args.out_dir), args.target_width)


if __name__ == "__main__":
    raise SystemExit(main())
