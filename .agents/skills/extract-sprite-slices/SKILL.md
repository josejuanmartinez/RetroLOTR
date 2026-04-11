---
name: extract-sprite-slices
description: Extract sliced Unity sprite sub-assets from any multi-sprite texture into standalone PNG sprite files under a chosen Assets folder. Use when Codex needs to turn atlas slices such as banner, icon, token, or UI sprite sheets into independent sprite assets for easier Addressables loading or simpler runtime lookup.
---

# Extract Sprite Slices

Use the Unity editor tool in [Assets/Editor/ExtractSpriteSlices.cs](../../../../Assets/Editor/ExtractSpriteSlices.cs) when a multi-sprite texture should be converted into standalone sprite files.

## Workflow
1. In Unity, select one or more textures imported with `Sprite Mode = Multiple`.
2. Open `Tools > Sprites > Extract Sliced Sprites`.
3. Choose an output folder under `Assets/`.
4. Optionally set `Name Prefix Filter` to restrict exported slices, for example `banner_`.
5. Keep `Use Current Selection` enabled unless you intentionally adapt the tool for a broader workflow.
6. Click `Extract`.
7. If the extracted sprites should be loaded through Addressables by normal illustration lookup, run the repo's addressable sync flow after extraction.

## Notes
- The tool temporarily enables read/write on source textures only when needed and restores the previous non-readable state after extraction.
- Extracted PNGs are imported as `Sprite Mode = Single`, point filtered, uncompressed, and with alpha transparency enabled.
- The output filenames are the sprite slice names, so names should already be unique enough for the target folder.

## Good Uses
- Break banner sheets into independent files under `Assets/Art/UI/Alignment/Banners/`
- Extract token or UI atlas slices that need direct runtime lookup by sprite name
- Prepare individual sprite assets before assigning or syncing Addressables
