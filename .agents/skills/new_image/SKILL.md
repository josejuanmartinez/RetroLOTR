---
name: new-image
description: Create new RetroLOTR card art images in a square format and in a retro ink/old-printer black-and-white style. Use when Codex needs to generate any new game image (especially action/spell images), mandatorily use at least 5 style references from Assets/Art/Cards/Actions, then postprocess with the black&white (bw-postprocess) skill and save to the correct Assets/Art/Cards subfolder by image type.
---

# New Image

Create new game images that match the existing RetroLOTR card style.

## Workflow
1. Determine image type from the request (`action`, `spell-action`, `pc`, `region`, `army`, `character`, `rest`).
2. Pick at least 5 random reference images from `Assets/Art/Cards/Actions` to anchor style (never use fewer than 5).
3. Generate a square image (`1:1`, recommended `1024x1024`) using model `gpt-image-1.5` and include all selected references in the API call using the image payload format required by `openai-image-gen` (base64, bytes, or equivalent supported binary input).
4. Enforce style direction in the prompt: black and white, ink drawing, old printer texture, retro sword-and-sorcery, Conan / MERPG / classic D&D vibe.
5. Run the result through the `black&white` skill (`bw-postprocess`) to convert to strict pure black/white output (`0/255`).
6. Save the final image to the correct folder in `Assets/Art/Cards`.

## Random Reference Selection
Use this command to select 5 random references:

```powershell
Get-ChildItem "Assets/Art/Cards/Actions" -File |
  Where-Object { $_.Extension -in ".png", ".jpg", ".jpeg" } |
  Get-Random -Count 5 |
  Select-Object -ExpandProperty FullName
```

If a project uses `Assets/Cards/Actions` instead, use that path variant.

## Model And Input Contract
- Always use model `gpt-image-1.5`.
- Always use at least 5 reference images.
- Always include style reference images in the generation call using the image input encoding required by `openai-image-gen` (for example base64 or bytes; do not send only file paths).

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
Use this card type enum:
- `action`
- `spell-action`
- `pc`
- `region`
- `army`
- `character`
- `rest`

## Save Location Rules
Save by image type:
- `action` (Actions, Skills, Events in the world): `Assets/Art/Cards/Actions/<ActionName>.<ext>`
- `spell-action` (An Action that is specifically a spell): `Assets/Art/Cards/Actions/Spells/<SpellName>.<ext>`
- `pc` (Small land that is a Population Center): `Assets/Art/Cards/PC/<Name>.<ext>`
- `region` (Wild extense region / Land): `Assets/Art/Cards/Regions/<Name>.<ext>`
- `army`: `Assets/Art/Cards/Armies/<Name>.<ext>`
- `character`: `Assets/Art/Cards/Characters/<Name>.<ext>`

- `rest` or generic card art: `Assets/Art/Cards/Rest/<Name>.<ext>`

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

## Completion Report (Mandatory)
After finishing image generation, always report:
- Final output file path.
- Model used (`gpt-image-1.5`).
- Exact reference images used (list full paths).
- Number of references used (must be `5`).
- Input format used for references in the generation call (for example base64 or bytes).
- The exact final prompt text used for generation.
