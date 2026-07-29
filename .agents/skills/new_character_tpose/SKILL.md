---
name: new_character_tpose
description: Create a full-body T-pose character reference image for RetroLOTR, matching the same painted-fantasy art direction as the shipped character card art, from a character name and description, in a single gpt-image-2 edit call. Use when Codex needs a rigging/animation-ready character reference image (e.g. as input to the spritesheet-generation skill), not a card scene illustration.
---

# New Character T-Pose

Create a full-body T-pose reference illustration of a character on a solid chroma-fuchsia
background, matching RetroLOTR's painted-fantasy character-card art direction, in one API
call — no separate sketch/colorize round-trip.

## Required Clarifications
Ask only when missing or ambiguous:
1. Character name.
2. Character description (race/species, gear/clothing, build, notable visual traits, alignment/mood).

Both are required inputs to the generation script. Do not guess a description if the user
did not provide one — ask first.

## Why a Single Call (Not Generate-Then-Colorize)
`new_image` generates a rough sketch, converts it to strict black-and-white, then runs a
separate colorize edit pass — useful when there's no existing art to anchor style. This skill
doesn't need that: RetroLOTR already ships plenty of character card art, so 3 random images
from `Assets/Art/Cards/Characters` are strong enough style anchors to send straight into one
`gpt-image-2 images.edit` call alongside the prompt. `images.edit` isn't limited to literally
editing one of the input images — `restyle_hex` in this repo already relies on the same
endpoint accepting a primary image plus extra images purely as style references — so this
works as a single multi-image-conditioned generation, producing the final full-color,
solid-background result directly. That also means the style references actually
influence the final render, unlike a generate→colorize split where they'd only inform an
intermediate stage that gets discarded.

## Workflow
1. Confirm character name and description with the user if either is missing.
2. Use the bundled script `scripts/new_tpose_character.py` to select 3 random shipped
   character card images from `Assets/Art/Cards/Characters` on every run — scoped to that
   subfolder only (not the full `Assets/Art/Cards` tree) so the references stay
   character-appropriate.
3. Send those 3 references (downscaled for upload) plus one prompt to `gpt-image-2`'s
   `images.edit` in a single call. The prompt states the character name/description, the
   strict T-pose requirement, a flat chroma-key background requirement, and the RetroLOTR
   style direction, and tells the model the references are style/technique anchors only — not
   poses or subjects to copy.
4. Save the final image to `Assets/Art/Characters/Portraits/<Name>.png`.
5. Stop after saving. Keep the solid chroma-fuchsia background; do not remove it or convert it
   to transparency.

## Prompt Requirements
Include all of the following constraints in the image-generation prompt:
- character name and description, stated explicitly so the subject is unambiguous
- strict T-pose: upright, facing viewer, arms straight out to the sides at shoulder height,
  neutral expression
- the pose and body must be completely left-right symmetric: mirror-image arms, legs, and
  hands, head centered and not tilted — the vertical centerline is a perfect mirror axis
- both hands semi-closed (fingers gently curled), not flat/open with spread fingers and not a
  tight fist
- full body visible from head to feet, centered, not cropped, even margin above/below
- a flat, uniform chroma-key color background (magenta/pink, `#FF00FF`) — no scenery, no
  ground clutter, no props unless worn/attached to the body, no second character, no drop
  shadow, and the chroma-key color must not appear anywhere on the character itself
- late-1970s hand-painted cel-animation fantasy style like vintage animated Lord of the Rings
- bold dark ink outlines with flat-to-soft cel shading
- varied scene-appropriate colors; avoid a flat sepia or uniformly brown cast
- an explicit instruction that the reference images are style/palette/technique anchors only,
  not poses or subjects to copy
- no modern UI elements, no text overlays, no logos, no card frame, no white border

If there is not enough information to write a good prompt (missing name or description), ask
the user before generating the image.

## Background Requirement
The saved asset must retain a solid, opaque, uniform chroma-fuchsia (`#FF00FF`) background.
Do not run flood-fill keying, alpha conversion, transparency extraction, or any other
background-removal step. The chroma-fuchsia PNG is the final asset.

## Model And Input Contract
- Model: `gpt-image-2`, via `images.edit` (not `images.generate`/the Responses API tool —
  those only accept the standard size enum, not gpt-image-2's arbitrary small sizes).
- Use exactly 3 references, randomly selected from `Assets/Art/Cards/Characters`, downscaled
  to `--upload-max-dim` (default 512px) before upload to control cost — same convention as
  `restyle_hex`/`colorify`.
- **Keep this asset small.** It's a rigging/reference input, not a final display asset — it
  gets consumed by `spritesheet-generation` and re-rendered into a spritesheet anyway, so
  there's no reason to spend on a large, high-quality render. Defaults are the minimum
  practical for a full-body portrait: size `640x1024` (portrait, keeps the figure uncropped),
  quality `low`.
- gpt-image-2's `images.edit` accepts arbitrary `WIDTHxHEIGHT` (not just the standard enum),
  as long as both dimensions are divisible by 16, the aspect ratio is between 1:3 and 3:1, the
  longest side is at most 3840px, **and total pixels are between 655,360 and 8,294,400** —
  that lower bound is a hard API floor (confirmed by a live 400 error on `512x768` =
  393,216px), which is why the default is `640x1024` (exactly 655,360px) rather than something
  smaller. Only go above the `640x1024`/`low` defaults if the user explicitly asks for a
  higher-fidelity reference.

## Save Location
Always save to:
```
Assets/Art/Characters/Portraits/<Name>.png
```
This matches the input path expected by the `spritesheet-generation` skill, which turns a
static character reference into an animated spritesheet. If the user's next ask is animation,
point them at that skill with this output as `--image`.

## Unity Import Settings
After saving the final image, ensure the Unity TextureImporter is configured as a **single
sprite**:
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
- Character is in a strict T-pose, full body visible, not cropped.
- Background is solid, opaque, uniform chroma-fuchsia (`#FF00FF`), with no scenery.
- File path is `Assets/Art/Characters/Portraits/<Name>.png`.
- Reference images came from `Assets/Art/Cards/Characters`, not from the final asset folder.
- **TextureImporter is set to Sprite Mode = Single**

## Completion Report (Mandatory)
After finishing image generation, always report:
- Final output file path.
- Model/size/quality used (usually `gpt-image-2`, `640x1024`, `low`).
- Background result: solid chroma-fuchsia retained.
- Exact reference images used (list full paths).
- Number of references used.
- The exact final prompt text used for generation.

## CLI Contract
Use the bundled wrapper instead of writing one-off OpenAI runners.

Dry-run example:

```powershell
.\.agents\skills\new_character_tpose\scripts\new_tpose_character.ps1 `
  -Name "Elrian Duskwalker" `
  -Description "A tall grey-cloaked Dunedain ranger, weathered face, long dark hair tied back, leather and mail armor, longsword at hip" `
  -DryRun
```

Live run example:

```powershell
.\.agents\skills\new_character_tpose\scripts\new_tpose_character.ps1 `
  -Name "Elrian Duskwalker" `
  -Description "A tall grey-cloaked Dunedain ranger, weathered face, long dark hair tied back, leather and mail armor, longsword at hip" `
  -Force
```
