---
name: new-image
description: Create new RetroLOTR card art images in a square format and in a retro painted fantasy style. Use when Codex needs to generate any new game image (especially action/spell images), using 3 randomly chosen shipped card images as uploaded binary reference inputs through the Responses API, then saving to the correct Assets/Art/Cards subfolder by image type.
---

# New Image

Create new game images that match the existing RetroLOTR color card style.

## Workflow
1. Determine image type from the request using `CardTypeEnum` from `Assets/Scripts/Cards/CardTypeEnum.cs` (`Action`, `Event`, `Land`, `PC`, `Character`, `Army`, `Rest`, `Encounter`, `Spell`).
2. Use the bundled script `scripts/new_image_card.py` to select 3 random shipped card images from `Assets/Art/Cards` on every run.
3. The script uploads those selected card images as binary file inputs through the Responses API and reports them explicitly.
4. Give the prompt a card-specific name when possible. The script can derive a name from the output filename if one is not passed explicitly.
5. By default, generate in three steps: first create the image, then run the old strict black-and-white postprocess helper (`scripts/bw_postprocess.py`), then send that B&W result into the `colorify`-style edit pass. This is the preferred workflow and should be treated as the standard way to make new RetroLOTR art.
6. Enforce style direction in the prompt: 1:1 square painted fantasy card illustration with a strong centered subject, clear silhouette, Bakshi-era Lord of the Rings mood, D&D cover art energy, MERP-style roleplaying-game illustration, rough hand-painted gouache/watercolor texture, visible brush strokes, heavy printed grain, jagged dark contour lines, earthy muted colors, strong shadows, and a real scanned fantasy-card look that matches the shipped RetroLOTR art.
7. The uploaded images are style, texture, and print-look guides only. Do not echo the reference files back into the prompt as a separate section or subject list.
8. Save the final image to the correct folder in `Assets/Art/Cards`.

## Random Reference Selection
The script handles random selection automatically, but this command matches its candidate pool:

```powershell
Get-ChildItem "Assets/Art/Cards" -Recurse -File |
  Where-Object { $_.Extension -in ".png", ".jpg", ".jpeg" -and $_.Name -notlike "CardFrame*" } |
  Get-Random -Count 3 |
  Select-Object -ExpandProperty FullName
```

## Model And Input Contract
- Use the Responses API helper, which defaults to `gpt-5` in this workspace.
- Use exactly 3 references.
- Keep references limited to 3 randomly selected shipped card images.
- The script uploads the chosen card images as binary file inputs through the Responses API.
- The default workflow is generate, then strict B&W postprocess, then colorify using `gpt-image-1.5`.
- The default workflow is generate, then strict B&W postprocess, then colorify using `gpt-image-1.5`; this is the standard process for matching the shipped RetroLOTR art.
- Pass `--card-name` when you want the final prompt to emphasize the card name explicitly.
- The prompt text should read like `colorify`: card name first, then the art brief, then the style block, with a tighter card-art composition lock.
- Use `--single-pass` only when you intentionally want to bypass the two-pass process.

## Prompt Requirements
Include all of the following constraints in the image-generation prompt:
- clearly describe what the image is about (subject, action, setting, mood, and key visual details)
- square composition, centered focal subject, card-art readability
- late-1970s hand-painted cel-animation fantasy style like vintage animated Lord of the Rings
- simplified hand-drawn shapes with expressive slightly cartooned anatomy
- bold dark ink outlines with flat-to-soft cel shading
- painterly watercolor-like backgrounds and moody magical lighting
- varied scene-appropriate colors; avoid a flat sepia or uniformly brown cast
- no modern UI elements, no text overlays, no logos, no extra characters

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
- File path matches the intended card category.
- Reference images came from `Assets/Art/Cards`, not from the final asset folder.
- The image handed to `colorify` is the strict black-and-white output of `scripts/bw_postprocess.py`, not the raw generation output.

## Completion Report (Mandatory)
After finishing image generation, always report:
- Final output file path.
- Model used (the helper's current default model, usually `gpt-5`).
- Exact reference images used (list full paths).
- Number of references used.
- Input format used for references in the generation call (uploaded file IDs via Responses API).
- The exact final prompt text used for generation.

## CLI Contract
Use the bundled wrapper instead of writing one-off OpenAI runners.

Dry-run example:

```powershell
.\.agents\skills\new_image\scripts\new_image_card.ps1 `
  -Out Assets/Art/Cards/Actions/MyNewCard.png `
  -Prompt "A ranger crossing a stormy ridge with a glowing sword" `
  -DryRun
```

Live run example:

```powershell
.\.agents\skills\new_image\scripts\new_image_card.ps1 `
  -Out Assets/Art/Cards/Actions/MyNewCard.png `
  -Prompt "A ranger crossing a stormy ridge with a glowing sword" `
  -Force
```
