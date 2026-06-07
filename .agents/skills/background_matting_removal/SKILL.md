---
name: background_matting_removal
description: Remove the background from an image by sending it to Gemini twice (once requesting a pure black background, once a pure white background), then computing a pixel-accurate alpha matte from the two composites using the dual-background matting formula. Outputs a PNG with transparency.
---

# Background Matting Removal

Extract a clean foreground from an image using GPT Image dual-background matting.

## How it works

1. Send the source image to GPT Image with a prompt to change ONLY the background to pure black (#000000). Subject is unchanged.
2. Send the same image again asking for a pure white (#ffffff) background.
3. For each pixel, apply the matting formula:

```
alpha = 1 - (white_composite - black_composite)   [per channel, averaged]
fg_color = black_composite / alpha
```

Pixels that changed between the two composites are background (alpha ≈ 0).  
Pixels that stayed the same are foreground (alpha ≈ 255).  
Edges get a smooth, physically-correct partial alpha.

## CLI Contract

Use the bundled script directly. Do not write one-off OpenAI runners.

Dry-run example:

```powershell
python .agents/skills/background_matting_removal/scripts/background_matting_removal.py `
  --image Assets/Art/Cards/Characters/Aragorn.png `
  --out Assets/Art/Cards/Characters/Aragorn_transparent.png `
  --dry-run
```

Live run example:

```powershell
python .agents/skills/background_matting_removal/scripts/background_matting_removal.py `
  --image Assets/Art/Cards/Characters/Aragorn.png `
  --out Assets/Art/Cards/Characters/Aragorn_transparent.png `
  --force
```

With edge feathering and intermediate debug saves:

```powershell
python .agents/skills/background_matting_removal/scripts/background_matting_removal.py `
  --image Assets/Art/Cards/Characters/Aragorn.png `
  --out Assets/Art/Cards/Characters/Aragorn_transparent.png `
  --feather 2 `
  --keep-intermediates `
  --force
```

## CLI Arguments

| Flag | Default | Purpose |
|---|---|---|
| `--image` | required | Source image path |
| `--out` | required | Output `.png` path (must be `.png`) |
| `--model` | `gpt-image-1` | OpenAI image model to use |
| `--size` | `1024x1024` | Edit output resolution |
| `--quality` | `auto` | OpenAI quality setting |
| `--upload-max-dim` | `512` | Downscale before upload (0 = full size) |
| `--feather` | `0` | Gaussian blur radius on alpha channel for softer edges |
| `--keep-intermediates` | off | Save `_black_bg.png` and `_white_bg.png` alongside output |
| `--force` | off | Overwrite if output already exists |
| `--dry-run` | off | Print parameters without calling the API |

## Rules

- Output must always be `.png` (transparency requires PNG).
- Makes exactly 2 API calls per run (one per background color).
- Uses `images.edit`, not `images.generate`.
- Uploads a downscaled preview (default 512px max dim) to keep costs low.
- The matting formula works best when the model faithfully preserves the foreground. If quality is poor, try a higher `--upload-max-dim` or `--quality high`.
- `--feather 1` or `--feather 2` is recommended for card art to smooth jagged matte edges.
- `--keep-intermediates` is useful for debugging when the matte looks wrong.

## Environment

- Requires `GEMINI_API_KEY`.
- Requires Python packages: `google-genai`, `pillow`, `numpy`. Install with:
  ```
  uv pip install --system google-genai pillow numpy
  ```

## Quality notes

The accuracy of the matte depends on how well GPT Image preserves the foreground between the two edits. Card art with strong silhouettes and clean backgrounds (sky, solid fill) produces the best results. Busy or blended backgrounds may leave residual halos — use `--feather` to soften them.

## Completion Report

Always report:
- Source image path
- Output image path
- Model used
- Whether intermediates were saved
- Whether feathering was applied
