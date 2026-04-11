---
name: new-image
description: Create new RetroLOTR card art images in a square format and in a retro ink/old-printer black-and-white style. Use when Codex needs to generate any new game image (especially action/spell images), using 2-3 lightweight style references from `Assets/Art/References/CardStyle`, then postprocess with the black&white (bw-postprocess) skill and save to the correct Assets/Art/Cards subfolder by image type.
---

# New Image

Create new game images that match the existing RetroLOTR card style.

## Workflow
1. Determine image type from the request using `CardTypeEnum` from `Assets/Scripts/Cards/CardTypeEnum.cs` (`Action`, `Event`, `Land`, `PC`, `Character`, `Army`, `Rest`, `Encounter`, `Spell`).
2. Pick 2 or 3 reference images from `Assets/Art/References/CardStyle` to anchor style.
3. Never use full-size production card assets as references when smaller curated previews are available.
4. Generate a square image (`1:1`, recommended `512x512`) using model `gpt-image-1.5`.
5. If the active image-generation path supports binary image references, send only the curated preview copies from `Assets/Art/References/CardStyle`.
6. If the active path does not support binary image references, use those same files only as prompt anchors and report that honestly.
7. Enforce style direction in the prompt: black and white, ink drawing, old printer texture, retro sword-and-sorcery, Conan / MERPG / classic D&D vibe.
8. Run the result through the `black&white` skill (`bw-postprocess`) to convert to strict pure black/white output (`0/255`).
9. Save the final image to the correct folder in `Assets/Art/Cards`.

## Random Reference Selection
Use this command to select 3 curated references:

```powershell
Get-ChildItem "Assets/Art/References/CardStyle" -File |
  Where-Object { $_.Extension -in ".png", ".jpg", ".jpeg" } |
  Get-Random -Count 3 |
  Select-Object -ExpandProperty FullName
```

## Model And Input Contract
- Always use model `gpt-image-1.5`.
- Use 2 or 3 references, not 5.
- Keep references in a dedicated lightweight source folder instead of the final asset folder.
- Never send full-size originals as references when resized preview copies exist.
- If the active image-generation path supports binary image references, send only the preview copies.
- If the active path does not support binary references, mention the curated reference files in the prompt/report instead of pretending they were uploaded.

## Prompt Requirements
Include all of the following constraints in the image-generation prompt:
- clearly describe what the image is about (subject, action, setting, mood, and key visual details)
- square composition, centered focal subject, card-art readability
- monochrome ink illustration
- high-contrast linework, cross-hatching, stipple/shading like vintage print
- gritty retro fantasy tone (Conan / MERPG / classic D&D module art)
- no modern UI elements, no text overlays, no logos, no color accents

If there is not enough information to write a good prompt, ask the user for missing details before generating the image.

## Card Type Enum
Use `CardTypeEnum` from `Assets/Scripts/Cards/CardTypeEnum.cs`:
- `Action`
- `Event`
- `Land`
- `PC`
- `Character`
- `Army`
- `Rest`
- `Encounter`
- `Spell`

## Save Location Rules
Save by image type:
- `Action`: `Assets/Art/Cards/Actions/<Name>.<ext>`
- `Event`: `Assets/Art/Cards/Actions/Events/<Name>.<ext>`
- `Spell`: `Assets/Art/Cards/Actions/Spells/<Name>.<ext>`
- `PC`: `Assets/Art/Cards/PC/<Name>.<ext>`
- `Land`: `Assets/Art/Cards/Lands/<Name>.<ext>`
- `Army`: `Assets/Art/Cards/Armies/<Name>.<ext>`
- `Character`: `Assets/Art/Cards/Characters/<Name>.<ext>`
- `Encounter`: `Assets/Art/Cards/Encounters/<Name>.<ext>`
- `Rest` or generic card art: `Assets/Art/Cards/Rest/<Name>.<ext>`

Always save all generated art under:
- `Assets\Art\Cards\XXXX\...`

If `XXXX` is unknown because the card type is unclear:
- Ask the user to confirm the card type/folder.
- If no answer is available, default to `Rest` (`Assets\Art\Cards\Rest\...`).

Prefer `.png` for new outputs unless the user requests another format.

## Final Checks
- Image is square (`width == height`).
- Final output is pure black and white after `bw-postprocess`.
- File path matches the intended card category.
- Reference images came from `Assets/Art/References/CardStyle`, not from the final asset folder.

## Completion Report (Mandatory)
After finishing image generation, always report:
- Final output file path.
- Model used (`gpt-image-1.5`).
- Exact reference images used (list full paths).
- Number of references used.
- Input format used for references in the generation call (for example base64 or bytes).
- The exact final prompt text used for generation.
