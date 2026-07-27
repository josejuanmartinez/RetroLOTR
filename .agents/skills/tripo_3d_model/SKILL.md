---
name: tripo_3d_model
description: Turn a reference image into a textured 3D model using the Tripo3D API - a Smart Topology (clean low-poly) mesh at a fixed triangle budget, with a 2K texture derived from the source image. Use when the user wants a 3D model generated from concept art/a portrait image. Saves under Assets/Art/3D/<Name>/<Name>.fbx, alongside this repo's other 3D model folders.
---

# Tripo3D Image-to-3D-Model

Generate a textured 3D asset from a single reference image via the Tripo3D
API, in two chained API tasks:

1. **Image-to-model** - Tripo's `P1-20260311` model line ("Smart Mesh"/Smart Topology):
   generates a clean, structured low-poly mesh directly (no manual retopology
   needed) at a fixed triangle budget, textured from the input image.
2. **Convert** - exports the mesh to the target format, repacking the texture
   at 2K.

No rigging/skeleton step - this produces a static textured mesh only.

## Required Clarifications
Ask only when missing or ambiguous:
1. Asset name (used for the output folder and filename).
2. The reference image: a local file path, or an already-shipped path in the
   repo (e.g. a card portrait). Any single clear image works.

## Workflow
1. Confirm the asset name and reference image with the user if either is
   missing.
2. Run the bundled script, which chains both Tripo tasks and polls each to
   completion before starting the next (Tripo tasks are async).
3. Report the final model path and the intermediate task IDs (useful for
   re-running a later stage manually via Tripo's console if one part needs
   redoing without regenerating the mesh).

## Parameters Baked Into The Pipeline
- **Mesh**: `model=P1-20260311` - Tripo's dedicated Smart Mesh/Smart Topology
  clean-topology low-poly line, not a general-purpose high-fidelity model
  post-processed down. `smart_low_poly=true` is only added for non-P-series
  `--model-version` overrides — Tripo rejects that param outright on
  P-series models (confirmed live: `"smart_low_poly is not supported for
  P-series model"`), since clean low-poly topology is what the P-series
  already is by default.
- **Triangle budget**: `face_limit=5000` (matches the user's ask; override
  with `--face-limit` if a different budget is ever needed).
- **Texture**: generated from the input image during mesh generation
  (`texture=true`, `pbr=true`, `texture_alignment=original_image` so the bake
  aligns with the source photo, not a hallucinated re-texture), then
  repacked to `texture_size=2048` (2K) at the final export step so the
  shipped texture resolution doesn't depend on the model's default.
- **Export**: `format=FBX` by default (override with `--format` for GLB/OBJ/etc).

## Save Location
```
Assets/Art/3D/fbx/<Name>/<Name>.fbx
```
Matching the existing model folders under `Assets/Art/3D/fbx` (a 2026-07
reorg — animation clips for these characters now live separately under
`Assets/Art/3D/Animations/Humanoid`, shared across characters via Unity
Humanoid retargeting rather than per-character clips).

## API Contract Verification Status (Read Before First Live Run)
Tripo3D's own docs are inconsistent across pages about hostname and API
version (`api.tripo3d.ai/v2/openapi` with a `type`-tagged single `/task`
endpoint on some pages, `openapi.tripo3d.ai/v3` with one REST path per
operation on others). This script targets the **v3 REST shape**
(`https://openapi.tripo3d.ai/v3`, `Authorization: Bearer <key>`,
`POST /generation/image-to-model`, `POST /models/convert`,
`GET /tasks/{id}`), because that shape is corroborated by the official
`VAST-AI-Research/tripo-js-sdk` source (not just marketing pages) and by
developers.tripo3d.ai's quick start. It has **not been exercised against a
live Tripo API key** in this repo.
- If the first live run 400s on the host or a param name, check the account's
  own API-keys/docs page in the Tripo console - it will show the exact base
  URL and a working curl example for that account's plan.
- Override the host with `--base-url` / `-BaseUrl` (or `TRIPO_API_BASE_URL`)
  without editing the script.
- On any HTTP error the script prints the full raw response body to stderr -
  read that first; it will show the real param/field name Tripo expects if
  one of the guessed names above is stale.
- Response envelopes are normalized defensively (`{"data": {...}}` or a flat
  object both work), but if a stage returns `task_id`/`output` under a
  different key, the error message will show the raw parsed response so it's
  a one-line fix in `unwrap()`/`extract_model_url()`.

## Completion Report (Mandatory)
After a successful run, always report:
- Final model path.
- Triangle budget, texture size, and model line used.
- The two Tripo task IDs (image-to-model, convert).

## CLI Contract
Use the bundled wrapper instead of writing one-off API calls.

Dry-run example:
```powershell
.\.agents\skills\tripo_3d_model\scripts\tripo_image_to_3d_model.ps1 `
  -Name "Boromir" `
  -Image "Assets\Art\Characters\Portraits\Boromir.png" `
  -DryRun
```

Live run example:
```powershell
$env:TRIPO_API_KEY = "..."
.\.agents\skills\tripo_3d_model\scripts\tripo_image_to_3d_model.ps1 `
  -Name "Boromir" `
  -Image "Assets\Art\Characters\Portraits\Boromir.png"
```

Mesh-only (skip the convert/2K-repack stage, e.g. to sanity-check topology
first) saves a `.glb` instead of the target format:
```powershell
.\.agents\skills\tripo_3d_model\scripts\tripo_image_to_3d_model.ps1 `
  -Name "Boromir" -Image "..." -SkipConvert
```
