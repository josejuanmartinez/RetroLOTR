---
name: colorify-banner
description: Recolor RetroLOTR banner or crest art into vintage painted fantasy style while preserving the original banner silhouette and transparent background. Use when working on banner images, cutout emblems, or any transparent UI banner asset that should stay the same shape while gaining color.
---

# Colorify Banner

Use the existing `colorify_card.py` workflow for banner art, but add a banner-specific prompt guardrail.

## Workflow

1. Start with exactly one source banner image.
2. Run `.agents/skills/colorify/scripts/colorify_card.py` in `edit` mode against that image.
3. Overwrite the original asset unless the user explicitly asks for a preview.
4. Keep the default downscaled upload behavior unless there is a reason to change it.
5. Pass `--match-source-size` so the edit uses the source banner's aspect ratio.
6. Pass `--preserve-source-alpha` so the original transparent cutout is restored after the edit.
7. Use the base `colorify` prompt, plus this banner-specific instruction:

```text
Keep the banner shape, silhouette, and cutout edges exactly as in the original image. Preserve the transparent background outside the banner shape; do not fill in a rectangular background or add any new opaque border around the banner.
```

8. If the source already has transparency, treat that transparency as part of the asset and preserve it.
9. Preserve the original canvas dimensions and banner cutout shape; do not force a square crop.

## Prompt Guidance

- Keep the subject, heraldry, and composition recognizable.
- Reinforce the banner/emblem identity while recoloring it.
- Preserve transparency outside the banner silhouette.
- Avoid text, logos, extra framing, or a new background.
