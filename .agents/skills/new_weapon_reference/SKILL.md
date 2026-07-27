---
name: new_weapon_reference
description: Create a front-facing, perfectly vertical, full-length weapon reference image for RetroLOTR, matching the same painted-fantasy art direction as the shipped character card art, from a weapon name and description, in a single gpt-image-2 edit call. Use when Codex needs a weapon prop reference image (e.g. as input to the tripo_3d_model skill for a 3D conversion), not a card scene illustration or a character holding the weapon.
---

# New Weapon Reference

Create a front-facing, vertical, full-length reference illustration of a
single weapon with a transparent background, matching RetroLOTR's
painted-fantasy character-card art direction, in one API call — no separate
sketch/colorize round-trip. This is a copy of the `new_character_tpose` skill
adapted for weapons/props instead of characters: same single-call pipeline
and background-keying technique, different orientation requirements (no
T-pose/limb symmetry — a rigid object shot straight-on and upright instead).

## Required Clarifications
Ask only when missing or ambiguous:
1. Weapon name.
2. Weapon description (type — sword/axe/bow/staff/etc, material, era/culture,
   ornamentation, notable visual traits).

Both are required inputs to the generation script. Do not guess a description
if the user did not provide one — ask first.

## Why A Single Call (Not Generate-Then-Colorize)
Same reasoning as `new_character_tpose`: RetroLOTR already ships plenty of
character card art, so 3 random images from `Assets/Art/Cards/Characters` are
strong enough style anchors to send straight into one `gpt-image-2
images.edit` call alongside the prompt, producing the final full-color,
transparent-background result directly. The references only inform paint
style/technique/palette, never the weapon's actual pose, subject, or
composition.

## Workflow
1. Confirm weapon name and description with the user if either is missing.
2. Use the bundled script `scripts/new_weapon_reference.py` to select 3
   random shipped character card images from `Assets/Art/Cards/Characters` on
   every run (same style-anchor pool as `new_character_tpose` — this repo has
   no dedicated weapon art to sample from yet).
3. Send those 3 references (downscaled for upload) plus one prompt to
   `gpt-image-2`'s `images.edit` in a single call. The prompt states the
   weapon name/description, the strict front-facing/vertical/full-length
   framing requirement, a flat chroma-key background requirement, and the
   RetroLOTR style direction, and tells the model the references are
   style/technique anchors only — not a subject or pose to copy.
4. Save the final image to `Assets/Art/Weapons/References/<Name>.png`.
5. The script always runs a flood-fill + spot-color alpha-keying pass
   afterward to convert the chroma-key background to real transparency — see
   Background Requirement below.

## Prompt Requirements
Include all of the following constraints in the image-generation prompt:
- weapon name and description, stated explicitly so the subject is
  unambiguous
- the weapon alone: no character, hand, arm, or grip holding it, no stand or
  mount, no scenery or other props
- perfectly **vertical/upright**: the weapon's long axis runs straight up and
  down the center of the frame (blade/haft/shaft/stave pointing up, hilt or
  base at the bottom), with zero lean or tilt
- strict **front-facing** camera: a direct elevation view with no perspective
  distortion or three-quarter angle, showing the weapon's flattest,
  most-identifying silhouette (e.g. a sword's blade flat-on, a bow with the
  string facing the viewer)
- **full length visible**: the entire weapon from top to bottom fully in
  frame, not cropped, centered, occupying as much of the frame's vertical
  extent as possible without touching the edges
- a flat, uniform chroma-key color background (magenta/pink, `#FF00FF`) — no
  scenery, no ground clutter, no drop shadow, and the chroma-key color must
  not appear anywhere on the weapon itself
- late-1970s hand-painted cel-animation fantasy style like vintage animated
  Lord of the Rings
- bold dark ink outlines with flat-to-soft cel shading
- varied, material-appropriate colors (steel, bronze, aged wood, leather,
  gems); avoid a flat sepia or uniformly brown cast
- an explicit instruction that the reference images are style/palette/
  technique anchors only, not a subject or pose to copy
- no modern UI elements, no text overlays, no logos, no card frame, no white
  border

If there is not enough information to write a good prompt (missing name or
description), ask the user before generating the image.

## Background Requirement
Identical mechanism to `new_character_tpose`: `gpt-image-2`'s `images.edit`
rejects `background="transparent"` outright (confirmed live 400 error), so
the prompt asks for a flat magenta/pink (`#FF00FF`) chroma-key background and
the script always runs a flood-fill + spot-color-match + edge-dilation
keying pass afterward (same code, copied verbatim from
`new_character_tpose/scripts/new_tpose_character.py`). This is not
pixel-perfect at heavily anti-aliased edges — a known, minor limit of
hard-threshold chroma keying, not worth chasing further unless the user asks.
If keying would erase almost the entire image, the script leaves the file
untouched and prints a warning instead of shipping a blank asset.

## Model And Input Contract
- Model: `gpt-image-2`, via `images.edit`.
- Use exactly 3 references, randomly selected from
  `Assets/Art/Cards/Characters`, downscaled to `--upload-max-dim` (default
  512px) before upload.
- **Keep this asset small.** It's a prop/reference input, not a final display
  asset. Defaults: size `576x1536` (a taller portrait than
  `new_character_tpose`'s `640x1024` — weapons are usually long and thin, so
  the extra vertical extent avoids wasting canvas on padding), quality `low`.
- gpt-image-2's `images.edit` accepts arbitrary `WIDTHxHEIGHT` as long as both
  dimensions are divisible by 16, the aspect ratio is between 1:3 and 3:1, the
  longest side is at most 3840px, and total pixels are between 655,360 and
  8,294,400. `576x1536` sits comfortably inside all four constraints
  (ratio ≈ 0.375, ≈884,736px). Only go above the defaults if the user
  explicitly asks for a higher-fidelity reference, or use an even more
  elongated ratio (down toward 1:3) for unusually long weapons like pikes.

## Save Location
Always save to:
```
Assets/Art/Weapons/References/<Name>.png
```
If the next ask is a 3D conversion, point the user at the `tripo_3d_model`
skill with this output as `--image`.

## Unity Import Settings
After saving the final image, ensure the Unity TextureImporter is configured
as a **single sprite**:
- **Texture Type**: `Sprite (2D and UI)`
- **Sprite Mode**: `Single` (NOT Multiple)

Then run the Addressables sync to register the new asset:
```
Tools > Addressables > Sync Art Addresses
```

## Final Checks
- Weapon is alone, no character/hand/grip holding it.
- Weapon is perfectly vertical (no lean/tilt) and shown in strict front view.
- Entire weapon length is visible, not cropped.
- Background is transparent (native or flood-fill-keyed), not a solid color
  or scene.
- File path is `Assets/Art/Weapons/References/<Name>.png`.
- Reference images came from `Assets/Art/Cards/Characters`.
- **TextureImporter is set to Sprite Mode = Single**

## Completion Report (Mandatory)
After finishing image generation, always report:
- Final output file path.
- Model/size/quality used (usually `gpt-image-2`, `576x1536`, `low`).
- Background result: native transparency, flood-fill fallback applied, or
  failed/needs review.
- Exact reference images used (list full paths).
- Number of references used.
- The exact final prompt text used for generation.

## CLI Contract
Use the bundled wrapper instead of writing one-off OpenAI runners.

Dry-run example:

```powershell
.\.agents\skills\new_weapon_reference\scripts\new_weapon_reference.ps1 `
  -Name "Orcrist" `
  -Description "An ancient Elvish longsword with a leaf-shaped blade that glows faint blue near orcs, an ornate gold-inlaid crossguard, and a dark leather-wrapped grip" `
  -DryRun
```

Live run example:

```powershell
.\.agents\skills\new_weapon_reference\scripts\new_weapon_reference.ps1 `
  -Name "Orcrist" `
  -Description "An ancient Elvish longsword with a leaf-shaped blade that glows faint blue near orcs, an ornate gold-inlaid crossguard, and a dark leather-wrapped grip" `
  -Force
```
