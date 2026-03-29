---
name: bw-postprocess
summary: Apply my retro game style that applies a strict black-and-white postprocess filter to an existing image using PIL + NumPy.
---

# B/W Postprocess Skill

Use this skill when the user wants to convert an existing image to pure black and white (no gray values), matching the behavior from the provided code.

## Scope
- Include only postprocessing on input images.
- Do not use diffusion, LoRA, prompt generation, or GPU decorators.
- Input: one or more existing images.
- Output: a PIL `L` image containing only pixel values `0` or `255`.

## Required User Prompt
Before selecting files, ask the user to choose the time window with this exact format:

Which images should I include based on modified time?
1. Last week.
2. Last 2 weeks.
3. Last month.
4. All.
Please choose a number and I will implement that option.

Interpret the answer as follows:
- `1.` last week = files with modified time within the last 7 days.
- `2.` last 2 weeks = files with modified time within the last 14 days.
- `3.` last month = files with modified time within the last 30 days.
- `4.` all = no modified-time filter.

Use the current local system time to calculate the cutoff. When reporting what will be processed, include the exact cutoff date/time used unless the user chose `4.`.

## Required Imports
```python
from PIL import Image, ImageOps
import numpy as np
```

## Canonical Function
Use this exact logic pattern:

```python
def to_pure_bw(
    img: Image.Image,
    contrast_boost: float = 1.35,
    threshold: int = -1,
    dither: bool = False,
) -> Image.Image:
    # 1) Grayscale
    g = img.convert("L")

    # 2) Autocontrast and optional contrast boost
    g = ImageOps.autocontrast(g)
    if contrast_boost and contrast_boost != 1.0:
        lut = [int(max(0, min(255, 128 + (i - 128) * contrast_boost))) for i in range(256)]
        g = g.point(lut, mode="L")

    # 3) Threshold selection
    # threshold < 0 => Otsu auto-threshold
    if threshold is None or int(threshold) < 0:
        hist = np.array(g.histogram(), dtype=np.float64)
        total = g.width * g.height
        sum_total = np.dot(np.arange(256), hist)
        sumB = 0.0
        wB = 0.0
        max_between = -1.0
        level = 128
        for t in range(256):
            wB += hist[t]
            if wB == 0:
                continue
            wF = total - wB
            if wF == 0:
                break
            sumB += t * hist[t]
            mB = sumB / wB
            mF = (sum_total - sumB) / wF
            between = wB * wF * (mB - mF) ** 2
            if between > max_between:
                max_between = between
                level = t
        th = level
    else:
        th = int(threshold)

    # 4) Binarize
    if dither:
        bw1 = g.convert("1", dither=Image.FLOYDSTEINBERG)
        bw = bw1.convert("L").point(lambda p: 255 if p > 0 else 0, mode="L")
    else:
        bw = g.point(lambda p: 255 if p > th else 0, mode="L")

    return bw
```

## Parameter Semantics
- `contrast_boost`:
  - `1.0` means no extra boost beyond autocontrast.
  - `>1.0` increases midtone separation before thresholding.
- `threshold`:
  - `-1` (or any negative) uses Otsu auto-threshold.
  - `0..255` uses fixed threshold.
- `dither`:
  - `False` for clean hard edges.
  - `True` for Floyd-Steinberg dithering texture.

## Agent Workflow
1. Ask the required numbered time-window question before selecting files.
2. Calculate the cutoff timestamp from the user's choice.
3. Discover candidate image files in the user-requested folder or file set.
4. Keep only images whose modified time is newer than or equal to the cutoff, unless the user chose `4.`.
5. Tell the user how many images matched, and name the files if the set is small enough to be useful.
6. Open each selected image as PIL `Image`.
7. Run `to_pure_bw(...)` with user-selected parameters.
8. Return/save each processed image as PNG.
9. Ensure every output remains strict black/white (`0/255`) in `L` mode.

## File Selection Rules
- Treat file modified time as the source of truth for "newer".
- Supported image inputs should be common raster formats such as `.png`, `.jpg`, `.jpeg`, `.bmp`, `.tif`, `.tiff`, and `.webp`.
- If no images match the selected time window, report that clearly and stop before processing.
- Do not guess hidden directories or unrelated folders; limit discovery to the path the user asked you to inspect.
- Preserve deterministic behavior: the same files and timestamps should yield the same selected set.

## Guardrails
- Do not introduce diffusion pipeline code.
- Do not add model loading, prompt handling, or LoRA logic.
- Keep processing deterministic unless dithering is intentionally enabled.
