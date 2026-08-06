---
name: new-image
description: Create new RetroLOTR card art images in a square format and in a retro painted fantasy style. Use when Codex needs to generate any new game image (especially action/spell images), using 3 randomly chosen shipped card images as binary reference inputs to a single gpt-image-2 images.edit call, then saving to the correct Assets/Art/Cards subfolder by image type.
---

# New Image

Create new game images that match the existing RetroLOTR color card style.

## Workflow
1. Determine image type from the request using `CardTypeEnum` from `Assets/Scripts/Cards/CardTypeEnum.cs` (`Action`, `Event`, `Land`, `PC`, `Character`, `Army`, `Rest`, `Encounter`, `Spell`).
2. Use the bundled script `scripts/new_image_card.py` to select 3 random shipped card images from `Assets/Art/Cards` on every run.
3. Send those 3 references (downscaled for upload) plus the art brief and RetroLOTR style block to `gpt-image-2`'s `images.edit` in a **single call** — no separate generate-then-colorize pass. RetroLOTR already has plenty of shipped card art, so the references are strong enough style anchors on their own; a throwaway B&W sketch stage would only add cost/latency and (in the old pipeline) meant the references stopped influencing the image after the first pass. `restyle_hex` in this repo already relies on `images.edit` accepting a primary image plus extra images purely as style references, so this works the same way as a multi-image-conditioned generation.
4. Give the prompt a card-specific name when possible. The script can derive a name from the output filename if one is not passed explicitly.
5. Enforce style direction in the prompt: 1:1 square painted fantasy card illustration with a strong centered subject, clear silhouette, Bakshi-era Lord of the Rings mood, D&D cover art energy, MERP-style roleplaying-game illustration, rough hand-painted gouache/watercolor texture, visible brush strokes, heavy printed grain, jagged dark contour lines, earthy muted colors, strong shadows, and a real scanned fantasy-card look that matches the shipped RetroLOTR art.
6. The uploaded images are style, texture, and print-look guides only. Do not echo the reference files back into the prompt as a separate section or subject list, and tell the model explicitly not to copy their subjects/layouts/symbols.
7. Save the final image to the correct folder in `Assets/Art/Cards`.

## Random Reference Selection
The script handles random selection automatically, but this command matches its candidate pool:

```powershell
Get-ChildItem "Assets/Art/Cards" -Recurse -File |
  Where-Object { $_.Extension -in ".png", ".jpg", ".jpeg" -and $_.Name -notlike "CardFrame*" } |
  Get-Random -Count 3 |
  Select-Object -ExpandProperty FullName
```

## Model And Input Contract
- Model: `gpt-image-2`, via `images.edit` — this is the current standard image model for this
  project (newer than the `gpt-image-1.5` still used by the older `colorify` skill).
- Use exactly 3 references, randomly selected from `Assets/Art/Cards`, downscaled to
  `--upload-max-dim` (default 512px) before upload to control cost — same convention as
  `restyle_hex`/`colorify`.
- Default size is `1024x1024` (square card format); default quality is `high` — cards are
  final in-game display assets, unlike the throwaway `new_character_tpose` reference image, so
  don't drop quality below what the user asked for.
- gpt-image-2's `images.edit` accepts arbitrary `WIDTHxHEIGHT` (not just the standard size
  enum), as long as both dimensions are divisible by 16 and the aspect ratio is between 1:3
  and 3:1; quality is `low`, `medium`, `high`, or `auto`.
- Pass `--card-name` when you want the final prompt to emphasize the card name explicitly.
- The prompt text reads: card name first, then the art brief, then the style block, then an
  explicit "these references are style/texture guides only, don't copy their subjects" clause.

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
- an explicit instruction that the reference images are style/texture/print-look guides only,
  not subjects or layouts to copy

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
- `Object`: `Assets/Art/Cards/Objects/<Name>.<ext>`
- `Rest` or generic card art: `Assets/Art/Cards/Rest/<Name>.<ext>`

Always save all generated art under:
- `Assets\Art\Cards\XXXX\...`

If `XXXX` is unknown because the card type is unclear:
- Ask the user to confirm the card type/folder.
- If no answer is available, default to `Rest` (`Assets\Art\Cards\Rest\...`).

Prefer `.png` for new outputs unless the user requests another format.

## Unity Import Settings
After saving the final image to `Assets/Art/Cards/...`, ensure the Unity TextureImporter is configured as a **single sprite**:
- **Texture Type**: `Sprite (2D and UI)`
- **Sprite Mode**: `Single` (NOT Multiple)

If doing this programmatically from an Editor script:
```csharp
TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
importer.textureType = TextureImporterType.Sprite;
importer.spriteImportMode = SpriteImportMode.Single;
importer.SaveAndReimport();
```

Then run the Addressables sync to register the new asset:
```
Tools > Addressables > Sync Art Addresses
```

## Final Checks
- Image is square (`width == height`) unless the user explicitly asked for a different aspect ratio.
- File path matches the intended card category.
- Reference images came from `Assets/Art/Cards`, not from the final asset folder.
- **TextureImporter is set to Sprite Mode = Single**

## Completion Report (Mandatory)
After finishing image generation, always report:
- Final output file path.
- Model/size/quality used (usually `gpt-image-2`, `1024x1024`, `high`).
- Exact reference images used (list full paths).
- Number of references used.
- Input format used for references in the generation call (uploaded binary file handles via `images.edit`, downscaled for upload).
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
