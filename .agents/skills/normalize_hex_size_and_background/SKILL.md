---
name: normalize-hex-size-and-background
description: Normalize a RetroLOTR hex tile's size, background, and Unity import settings to match the rest of the tile set. Keys out a solid white/black background to alpha, rescales the hexagon to the canonical 773px footprint on a 974x1314 canvas with the standard center pivot, and patches the .meta (spriteMode 1, alignment Center, pivot 0.5/0.5, PPU 750). Use when a new/restyled/hand-exported tile has a black or white background, the wrong proportion/centering, or wrong import settings.
---

# Normalize Hex Size and Background

Make a "raw" hex tile match the existing set. Raw tiles — AI/restyle output or
hand-exported art — typically arrive with a **solid opaque background** (white or
black), the **wrong canvas size/proportion**, and **non-canonical sprite import
settings** (e.g. `spriteMode 2`, `PPU 100/250/700`, custom pivot). This skill
fixes all three in one pass.

## Canonical target (the rest of the tiles)
- Canvas: **974 x 1314**, transparent (alpha) background
- Footprint: pointy-top hexagon, **773 px** wide, center at **(487, 712)**
  (canvas center + 0.071·width drop — the baked-in sprite pivot)
- Meta: `spriteMode 1`, `alignment 0` (Center), `spritePivot {0.5, 0.5}`,
  `spritePixelsToUnits 750`

## What the script does
1. **Background → alpha**: if the border is mostly opaque, flood-fills the
   background color from the edges (auto-detects white or black). Interior
   black/white art survives because it is not connected to the border. Tiles that
   already have a transparent background skip this step.
2. **Footprint detect + rescale**: finds the hexagon from the alpha silhouette and
   uniformly rescales so the footprint width = 773.
3. **Recompose**: pastes onto a fresh 974x1314 canvas at the canonical center.
4. **Patch .meta**: sets the four sprite fields above. **The GUID is preserved**
   (never copy a whole .meta over another — it breaks references).

Depends on the sibling `restyle_hex` skill (reuses its footprint detector and
background-keying). Requires `pillow`, `numpy`, `scipy`.

## CLI

```powershell
# single tile, overwrite in place (also patches its .meta)
python .agents/skills/normalize_hex_size_and_background/scripts/normalize_hex_size_bg.py `
  --image "Assets/Art/Hexes/Tiles/shore_04.png"

# raw source -> destination in the tile set
python .agents/skills/normalize_hex_size_and_background/scripts/normalize_hex_size_bg.py `
  --image "raw/shore_04.png" --out "Assets/Art/Hexes/Tiles/shore_04.png"

# whole folder, in place
python .agents/skills/normalize_hex_size_and_background/scripts/normalize_hex_size_bg.py `
  --source "Assets/Art/Hexes/Tiles/incoming"

# inspect first, write nothing
python .agents/skills/normalize_hex_size_and_background/scripts/normalize_hex_size_bg.py `
  --image "raw/foo.png" --dry-run
```

### Options
- `--target-width` (default 773), `--canvas` (default `974x1314`), `--ppu` (default 750)
- `--bg auto|white|black|R,G,B` (default `auto`). A non-auto value **forces** keying
  even when the border looks transparent — useful for tiles with a filled interior bg.
- `--tolerance` (default 30) — bg color match distance. Lower it if keying nibbles
  into same-colored art; raise it if background remnants survive.
- `--no-meta` — leave .meta files untouched.
- `--dry-run`, `--force`.

## Rules
- **Always visually verify the output.** White keying near pale sand/water and
  black keying near dark ink can erode the art. The script prints a warning when
  detection used the slope-prior or inscribed-hexagon fallback — check those.
- Never alter the `.meta` GUID; only the four sprite fields are patched.
- This handles size/background/import only. To also change the art style, run the
  `restyle_hex` skill (`--style-only`) FIRST, then normalize the result.

## Completion report
Report, per tile: source size + detected background, footprint width + scale,
any detection warning, final size/center, and the meta fields set.
