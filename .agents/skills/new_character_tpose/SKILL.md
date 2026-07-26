---
name: new_character_tpose
description: Create a full-body T-pose character reference image for RetroLOTR, matching the same painted-fantasy art direction as the shipped character card art, from a character name and description, in a single gpt-image-2 edit call. Use when Codex needs a rigging/animation-ready character reference image (e.g. as input to the spritesheet-generation skill), not a card scene illustration.
---

# New Character T-Pose

Create a full-body T-pose reference illustration of a character with a transparent
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
transparent-background result directly. That also means the style references actually
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
5. The script always runs a flood-fill + spot-color alpha-keying pass afterward to convert the
   chroma-key background to real transparency — see Background Requirement below.

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
The saved asset must have a transparent background, not a solid color — it is a sprite, not a
card illustration.
- **`gpt-image-2`'s `images.edit` rejects `background="transparent"` outright** — confirmed by
  a live 400 error: `"Transparent background is not supported for this model."` (param
  `background`, code `invalid_value`). This is a hard rejection, not a soft ignore, so the
  parameter is never sent.
- Instead, the prompt asks the model for a flat, uniform magenta/pink (`#FF00FF`) chroma-key
  background — a color that should never legitimately appear on a RetroLOTR character. The
  script then always runs an alpha-keying pass on the output (this is the primary mechanism,
  not a rare fallback):
  1. **Flood-fill** from all four corners — same BFS technique as `restyle_hex/scripts/trim_hex.py` —
     removes the connected flat background.
  2. **Spot color-match** — any remaining pixel within color-distance tolerance of the detected
     chroma-key color also gets keyed out, even if not reachable from the border. This catches
     pockets enclosed by the character's own silhouette (e.g. between splayed T-pose fingers)
     that the flood-fill can't reach.
  3. **Edge dilation** (2px) — anti-aliased pixels blended between the character's ink outline
     and the chroma-key color sit just outside the color-match tolerance and would otherwise
     survive as a visible magenta fringe around the silhouette; growing the background mask by
     a couple of pixels before applying it removes that ring too.
  None of this crops the canvas, so framing stays predictable for `spritesheet-generation`.
- This is not perfect at the single-pixel level — a handful of heavily anti-aliased pixels
  (e.g. where a beard strand or hat brim curve blends into the background over 2-3px) can
  survive with a faint tint even after all three passes. This is a known, minor limit of
  hard-threshold chroma keying (true removal would need alpha-decontamination/unmixing, which
  is disproportionate effort for a rigging reference image) — do not chase it further unless
  the user specifically asks for pixel-perfect edges.
- If keying would erase almost the entire image (a bad color-distance guess), the script
  leaves the file untouched and prints a warning instead of shipping a blank sprite — in that
  case, inspect the image manually and consider re-running.
- Report the keying result in the completion report.

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
- Background is transparent (native or flood-fill-keyed), not a solid color or scene.
- File path is `Assets/Art/Characters/Portraits/<Name>.png`.
- Reference images came from `Assets/Art/Cards/Characters`, not from the final asset folder.
- **TextureImporter is set to Sprite Mode = Single**

## Completion Report (Mandatory)
After finishing image generation, always report:
- Final output file path.
- Model/size/quality used (usually `gpt-image-2`, `640x1024`, `low`).
- Background result: native transparency, flood-fill fallback applied, or failed/needs review.
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
