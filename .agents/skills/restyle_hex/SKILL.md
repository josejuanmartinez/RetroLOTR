---
name: restyle-hex
description: Restyle RetroLOTR hex tile images using gpt-image-2. Keeps exact shapes, elements, and composition — changes only the art style to a D&D/Bakshi/Conan/LOTR/MERP/MECCG painted fantasy look. Outputs go to Assets/Art/Hexes/Tiles_Restyled/ by default to preserve originals.
---

# Restyle Hex

Restyle an existing hex tile image using `gpt-image-2 images.edit`. Preserves all shapes and elements exactly; changes only the visual style.

## Workflow
1. Take one or more source tile paths from `Assets/Art/Hexes/Tiles/`.
2. Run `scripts/restyle_hex.py` for each tile.
3. Save outputs to `Assets/Art/Hexes/Tiles_Restyled/` unless the user asks to overwrite originals.

## Default Prompt
```text
Can you change the style of this image, keeping exactly as it is, but with another style: d&d random bakshi conan lotr merpg meccg style without changing at all the shape, elements, or anything else - just style
```

## Style-Only Mode (`--style-only`)

By default the prompt grants the model creative freedom to redesign/add interior
elements (towers, fortresses, houses, standing stones, rivers, bridges, etc.) and
injects content-theme + place-name instructions that add structures. Pass
`--style-only` for a **faithful restyle**: it swaps in `STYLE_ONLY_PROMPT`, which
forbids adding/removing/redesigning any element, and skips all theme/place
injection. Only the art style changes.

```powershell
python .agents/skills/restyle_hex/scripts/restyle_hex.py `
  --image "Assets/Art/Hexes/Tiles/001 1.png" `
  --out "Assets/Art/Hexes/Tiles_Restyled/001 1.png" `
  --style-only --force
```

The same flag exists on `scripts/restyle_hex_batch.py` to restyle a whole folder
without adding features.

## CLI Contract

Dry-run example:
```powershell
python .agents/skills/restyle_hex/scripts/restyle_hex.py `
  --image "Assets/Art/Hexes/Tiles/001 1.png" `
  --out "Assets/Art/Hexes/Tiles_Restyled/001 1.png" `
  --dry-run
```

Live run example:
```powershell
python .agents/skills/restyle_hex/scripts/restyle_hex.py `
  --image "Assets/Art/Hexes/Tiles/001 1.png" `
  --out "Assets/Art/Hexes/Tiles_Restyled/001 1.png" `
  --force
```

## Rules
- Model: `gpt-image-2`
- Quality: `low`
- Size: `1024x1024`
- Upload max dim: `512` (downscales for upload, output is still full quality)
- Never overwrite originals unless the user explicitly asks
- No grayscale check — hex tiles are already in color
- No card-name prefix — hex tiles don't have names

## Completion Report
Always report:
- Source image path
- Output image path
- Model and quality used
- Prompt used

## Footprint Normalization (`scripts/normalize_hex_tiles.py`)

Aligns all hex tiles to one canonical footprint: pointy-top hexagon, caps W/4,
vertical sides W/2 (total height = W). Detects each tile's hexagon from the
alpha silhouette (bottom-cap line fits → slope-prior fallback → inscribed-
hexagon search), rescales uniformly to the target width, and composites onto a
shared canvas with the footprint center at a fixed point, so Unity's
center-pivot import settings keep working unchanged.

```powershell
# report only
python .agents/skills/restyle_hex/scripts/normalize_hex_tiles.py --analyze --csv report.csv
# normalize to a review folder, then copy over Assets/Art/Hexes/Tiles
python .agents/skills/restyle_hex/scripts/normalize_hex_tiles.py --out-dir TileNormalization/output
```

Notes:
- `hexUnderOcean00.png` / `hexUndercliff00.png` are special curtain tiles,
  excluded and copied through unchanged.
- New tiles dropped into `Tiles/` or `PCs/` later can be normalized the same
  way; use `--target-width 773` to match the existing normalized set. Canvas
  size may differ between batches — alignment only depends on the target
  width and the baked-in center drop.
