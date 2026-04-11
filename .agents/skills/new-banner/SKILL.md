---
name: new-banner
description: Create new RetroLOTR banner sprite art that matches the existing heraldic banner set in `Assets/Art/UI/Alignment/Banners`. Use when Codex needs to add a new nation, leader, variant, or faction banner image, generate a fresh banner from a textual description, or remake a banner so it fits the current in-game alignment banner style and standalone sprite workflow.
---

# New Banner

Create new banner sprite art that matches the existing RetroLOTR heraldic banner library.

## Workflow
1. Determine the requested banner name, symbolism, palette, and faction identity from the user request or adjacent game data.
2. Pick 2 or 3 lightweight reference banner images from `Assets/Art/References/BannerStyle`.
3. Generate a new portrait banner sprite using model `gpt-image-1.5`.
4. If the active image-generation path supports binary image references, send only the curated preview copies from `Assets/Art/References/BannerStyle`.
5. If the active path is the CLI fallback, do not claim binary references were sent. The CLI `generate` path does not accept them, so use the curated preview files only as prompt anchors and report that honestly.
6. Match the existing banner family: heraldic fantasy emblem, strong silhouette, readable at small size, limited palette, aged fabric or painted-standard feel, transparent background if supported by the workflow.
7. Save the final banner as a standalone PNG under `Assets/Art/UI/Alignment/Banners/<banner_name>.png`.
8. If the repo expects normal illustration lookup or Addressables registration for new UI art, run the usual import/addressables sync flow after saving.

## Reference Selection
Always use 2 or 3 existing banner references from `Assets/Art/References/BannerStyle`.

Use this command to choose 3 random references:

```powershell
Get-ChildItem "Assets/Art/References/BannerStyle" -File |
  Where-Object { $_.Extension -in ".png", ".jpg", ".jpeg" } |
  Get-Random -Count 3 |
  Select-Object -ExpandProperty FullName
```

Prefer choosing references that are symbolically close to the requested banner when possible:
- cavalry or horse factions: horse, rider, sun, wind, or standard-bearing banners
- dwarven factions: hammer, key, mountain, axe, forge, or geometric heraldry
- dark factions: eye, iron, demon, flame, skull, beast, or harsh angular insignia
- elven or noble factions: tree, leaf, star, moon, harp, swan, or elegant heraldry

## Style Requirements
Match the shipped banner set:
- vertical banner / standard composition
- clean central emblem with strong contrast
- readable at small UI size
- restrained fantasy heraldry, not photorealistic painting
- textured cloth or printed-banner feel is welcome, but keep the silhouette crisp
- no text, no borders from modern UI, no logos, no watermarks

Target the existing banner proportions. Current extracted banner sprites are typically portrait-oriented around `162x240`, so preserve a similar aspect ratio unless the user explicitly asks otherwise.

## Cost Rules
- Keep banner references in `Assets/Art/References/BannerStyle`, not in the final banner output folder.
- Never send full-size production banners as references.
- Use resized preview copies around `256px` to `512px` maximum.
- For the CLI fallback workflow, report references as prompt anchors only, because no binary reference images were uploaded.

## Prompt Requirements
Include all of the following in the image-generation prompt:
- the faction or leader identity
- the central heraldic symbol
- the intended color palette
- portrait banner sprite composition
- clean transparent or isolated background
- readable emblem for game UI usage
- retro fantasy heraldry matching the existing `Assets/Art/UI/Alignment/Banners` set

If the request does not provide enough detail for a distinctive banner, infer from nearby game data when possible. Only ask the user if the missing design choice would materially change the result.

## Naming And Save Rules
- Save as `Assets/Art/UI/Alignment/Banners/<banner_name>.png`
- Prefer lowercase snake-case names that follow the existing convention, for example `banner_white_horse`
- Keep the saved filename aligned with the JSON / gameplay banner id whenever possible

## Final Checks
- Output is a standalone PNG sprite in `Assets/Art/UI/Alignment/Banners`
- Composition matches the existing banner family
- Emblem remains legible when reduced to small UI size
- Filename matches the intended in-game banner id

## Completion Report
After generating a banner, always report:
- final output file path
- model used
- exact reference images used
- number of references used
- input format used for references in the image call
- exact final prompt text used for generation
