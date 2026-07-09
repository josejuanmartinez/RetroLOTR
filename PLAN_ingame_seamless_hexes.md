# Plan: In-game seamless hex rendering (port of Scenario Creator preview)

**Goal:** make the in-game board (`Hex.cs` / `Board.cs`) render exactly like the Scenario
Creator's SeamlessBlend preview: cross-faded tile rims, invisible seams (alpha feather over
overdraw), and (optionally via a flag to be called from the game) the subtle rainbow neon grid.

**Status: APPLIED (2026-07-09).** Shader moved to `Assets/Shaders/HexSeamlessBlend.shader`
(`"RetroLOTR/HexSeamlessBlend"`, `_GammaOut` toggle; editor .mat sets 1), runtime material at
`Assets/Resources/Materials/HexSeamlessBlendGame.mat` (grid off), feeder
`Assets/Scripts/HexSeamlessTerrain.cs` + `Hex.cs` hooks (`ApplyHexTextureSprite`,
`UpdateVisibilityForFog` reveal tracking, `ApplyTerrainOverdraw` in `Initialize`), mipmaps +
trilinear enabled on `Assets/Art/Hexes/Tiles`. **Deviation from Step 2:** tile sprites are
trimmed to slightly different pixel rects per tile, so `_NeighborOffsetN`/`_AspectY` are per-hex
MPB values in units of each hex's own drawn width (from `board.hexSize`), not global material
values. In-game verification (Step 5) still pending.

---

## What already exists (reference implementation)

| Piece | Where |
|---|---|
| The shader (all the math) | `Assets/Shaders/HexPreview/HexSeamlessBlend.shader` |
| Editor material | `Assets/Editor/ScenarioCreator/Shaders/HexSeamlessBlend.mat` |
| Property feeding, per cell | `ScenarioCreatorWindow.ApplyNeighborBlendProperties` |
| View geometry (offsets/aspect), per repaint | `ScenarioCreatorWindow.ApplySeamlessBlendGeometry` |
| Overdraw + anchoring | `ScenarioCreatorWindow.DrawGrid` / `TileRect` (`TileOverdraw = 1.10`) |
| In-game terrain sprite assignment | `Hex.ApplyHexTextureSprite` (`Hex.cs` ~line 3068), renderer field `terrainTexture` (~line 75) |
| Board neighbor tables | `Board.cs` ~line 82 (`evenRowNeighbors` / `oddRowNeighbors`) |

### Shader math cheat-sheet (do not re-derive, it works)

- Tile-local frame: origin = hex center, +y = up on screen, 1 unit = drawn tile width.
  `s = (localUV.x - 0.5, (localUV.y - 0.5) * _AspectY)`.
- Per direction d: `_NeighborOffsetN` = neighbor center offset in that frame. `halfD = |offset|/2`
  is the seam. `t = dot(s, u) / halfD` → `t = 1` exactly at the seam.
- Neighbor color sample = reflection across the seam, which in neighbor-local coords collapses to
  `s - 2*dot(s,u)*u`. Both tiles sample identical colors at the seam → blend strength 0.5 makes
  the two sides mathematically equal there. **0.5 is the unique seamless value** — don't exceed.
- Neighbor taps are alpha-weighted (`Σrgb / Σa`) — this un-premultiplies bilinear/mip fringe and
  keeps transparent canvas margins from darkening rims. Never average raw RGB.
- Alpha feather ONLY beyond the seam (`t > 1`, inside the overdraw overhang where the other tile
  is fully opaque underneath). Feathering before the seam makes BOTH tiles translucent at the
  seam → background bleeds through as a grid. Been there.
- Grid glow: seam distance in screen px via `fwidth(t)`; hue from map-space position
  (`_CellCenter + s`) so both tiles compute identical color at a shared seam.

Current tuned values (editor .mat): `_BlendStrength 0.5`, `_BlendBand 0.38`, `_EdgeTrim 0.08`,
`_GridIntensity 0.2`, `_GridWidth 1.2`, `_GridGlowWidth ~0` (user-tuned), `_GridHueScale 0.12`.

---

## Step 1 — Make the shader usable at runtime (gamma toggle)

The editor GUI target stores raw values (we draw with `GL.sRGBWrite` off), so the shader ends with
`LinearToGammaSpace(result.rgb)`. The game camera pipeline does proper linear→sRGB conversion on
its own, so that line must be OFF in-game or everything will be washed out/bright.

- Add property `_GammaOut ("Editor gamma output", Float) = 0` to `HexSeamlessBlend.shader`.
- Replace the `#ifndef UNITY_COLORSPACE_GAMMA ... #endif` block body with:
  `if (_GammaOut > 0.5) result.rgb = LinearToGammaSpace(result.rgb);` (keep the `#ifndef` guard
  around it so a gamma-space project needs neither).
- Set `_GammaOut: 1` in the **editor** .mat (`Assets/Editor/ScenarioCreator/Shaders/HexSeamlessBlend.mat`).
- Consider moving/renaming shader to `Assets/Shaders/HexSeamlessBlend.shader` with name
  `"RetroLOTR/HexSeamlessBlend"` (it's no longer editor-only). Update the editor mat's shader ref
  if renamed. Keep the GUID (edit the name inside the file, don't duplicate) so the .mat keeps working.
- Create runtime material `Assets/Art/Materials/HexSeamlessBlendGame.mat` (or wherever runtime
  materials live): same defaults, `_GammaOut 0`. Decide `_GridOn` (see Open decisions).

Sprite-renderer compatibility is already in place: `[PerRendererData] _MainTex`, vertex color
multiplies the final color (so `Hex.SetHexSpriteAlpha` fog-of-war dimming keeps working),
premultiplied `Blend One OneMinusSrcAlpha` matches Sprites-Default behavior.

## Step 2 — Feed per-hex data via MaterialPropertyBlock

New component or a region in `Hex.cs` (suggest `HexSeamlessTerrain.cs` helper, static-friendly):

1. Assign the runtime material once to every `terrainTexture` renderer
   (`sharedMaterial = hexSeamlessBlendGame`).
2. Build a `MaterialPropertyBlock` per hex containing:
   - `_SpriteUV` — sprite atlas rect normalized (`sprite.rect / texture size`).
   - For each direction 0..5: `_NeighborTexN` (neighbor terrain sprite texture),
     `_NeighborUVN` (their normalized rect), `_NeighborValidN` (0/1), `_NeighborOffsetN`.
   - `_AspectY` = terrain quad world height / width.
   - `_CellCenter` = this hex's position in tile-width units (for grid hue):
     `(hexWorldPos - anyOriginConstant) / quadWorldWidth`, y as-is (world y is already "up").
   Apply with `terrainTexture.SetPropertyBlock(mpb)`.
3. **Compute `_NeighborOffsetN` from real transforms, not from axis conventions:**
   `offset = (neighborTerrainTexture.position - myTerrainTexture.position) / quadWorldWidth`
   (xy only). This sidesteps the fact that `Board.cs` labels its tables "flat-top" while the
   editor math assumed pointy-top odd-r — measured offsets are correct by construction.
   All hexes share the same 6 offsets, so compute once from any interior hex and cache statically.
   `quadWorldWidth` = `terrainTexture.bounds.size.x` **before** the overdraw scale of Step 3
   (or measure after and multiply consistently — the shader only cares that offsets and `s` use
   the same unit: the drawn tile width. In the editor: `colX = stepX / drawW`).
4. **Rebuild triggers:** at the end of `Hex.ApplyHexTextureSprite`, rebuild the MPB for this hex
   AND its 6 neighbors (their rim now blends toward the new art). Also run once for the whole
   board after initial generation/load, and on every reveal transition (see Step 2b). Never per frame.
5. Perf guardrail from the editor lesson (`reference_findobject_perf.md`): no per-hex
   `FindFirstObjectByType`, no LINQ in the rebuild path; cache the mapping/material statically.

## Step 2b — Fog of war (CRITICAL — hexes are not rendered until discovered)

Unrevealed hexes have their `terrainTexture` GameObject INACTIVE (`Board.cs` ~line 493,
`Hex.UpdateVisibilityForFog`). This changes the neighbor rules:

1. **`_NeighborValidN` is TRI-STATE (already implemented in the shader):**
   - `1` = rendered neighbor → normal blend + feather.
   - `0` = no neighbor at all (map border) → crisp edge, nothing happens.
   - `-1` = fog-of-war neighbor (hex exists but isn't rendered yet) → **fade our own alpha to
     zero at the seam** (`_FogFade` band, default 0.25 of center-to-edge), so revealed terrain
     dissolves softly into the fog instead of ending in a hard hex outline.
   The `-1` path never samples the hidden neighbor's art (**no info leak**), and fading into
   visible background is correct there — it IS the fog. Do NOT use `1` for unrevealed neighbors:
   that both previews unscouted terrain and relies on their (inactive) renderer to back the
   feather, punching see-through holes at the frontier.
2. **Reveal transitions rebuild MPBs:** when a hex flips to revealed, rebuild the MPB for that
   hex AND its 6 neighbors (they may now blend/feather toward it). Hook the single choke point
   where `Board`/`Hex` activates the terrain object on reveal rather than sprinkling calls.
3. **Revealed-but-unseen (dimmed 0.1 alpha) neighbors stay VALID:** their art is already
   player-known, so blending toward it leaks nothing. Expect a brightness edge at the seen/unseen
   boundary (our rim blends their art at OUR brightness while their tile renders dimmed) — that
   edge is the fog visual doing its job; do not compensate in the shader.
4. `Board.ToggleAllTerrainTextures()` (labels/debug view) deactivates ALL terrain — no shader
   implications, but MPB rebuilds must not touch `activeSelf` (leave activation to the fog code).

## Step 3 — Overdraw + draw order

In-game hex art touches exactly edge-to-edge → no overlap → the feather has nothing to fade and
seams (baked border pixels) stay visible. Mirror the editor's `TileOverdraw`:

- Scale each `terrainTexture` transform to **1.10** (localScale xy). Only the terrain sprite —
  not the hex root (would move gameplay positions), not pcTexture.
- IMPORTANT: after scaling, tile width changed; keep offset units consistent (offsets are
  "per drawn tile width", i.e. divide by the SCALED width — same as editor where
  `colX = stepX/drawW` with `drawW` already overdrawn, giving ~0.72, not 0.79).
- `_EdgeTrim` must complete inside the overhang: art edge lands at `t ≈ 1.10`, feather ends at
  `1 + _EdgeTrim = 1.08` ✓ with 0.08. If a different overdraw is chosen, re-check this inequality.
- Deterministic overlap resolution: set `terrainTexture.sortingOrder` from grid coords (e.g.
  `row * width + col` within the terrain layer, below PC/character/UI orders — check
  `Hex.cs` ~line 479 for the existing sortingOrder budget/clamping scheme first!).
  Any consistent order works; the feather makes top-vs-bottom invisible.

## Step 4 — Texture import settings (mipmaps)

Terrain tile textures (Assets/Art/Hexes/... — the ones `HexTextureMapping` serves): enable
**Generate Mip Maps** + **Trilinear** filtering. Without mips, a zoomed-out camera samples
~1000px sprites at heavy minification: aliasing + slow fetches (this is exactly what froze the
Scenario Creator; the editor now uses a downscaled cache, the game should just use mips).
Verify memory/quality after: mips add ~33% VRAM for those textures.

## Step 5 — Verify (in this order, each isolates one failure mode)

1. Enter play mode, load a map. **Brightness identical** to a hex with the default sprite
   material? (If washed out → `_GammaOut` accidentally 1 in the runtime mat.)
2. Rim **blending** visible between different terrains, tile centers crisp.
3. **No seams**: no dark hex outlines (tap contamination), no background-colored grid lines
   (feather starting before seam), no visible baked border rings (feather/overdraw mismatch).
4. **Fog of war** (Step 2b): unseen hexes dim to 0.1 alpha correctly (vertex color path);
   the fog FRONTIER fades softly into the fog (valid = -1 path) with NO hint of the hidden
   neighbors' art (info leak) and no hard hex outlines against the darkness.
   Scout a new hex: it AND its neighbors start blending together within one rebuild.
5. Map edges: border hexes keep crisp outer edges (no feather toward missing neighbors).
6. Zoom out fully: framerate OK? If draw calls hurt (each hex = own draw call because of 6
   MPB textures), add a zoom-threshold LOD: swap `terrainTexture` back to the default sprite
   material when zoomed far out (seams are sub-pixel there anyway).
7. Editor still works: open Scenario Creator, confirm SeamlessBlend preview unchanged
   (gamma toggle and shader rename are the risky bits).

## Open decisions (ask Juan / decide when testing)

- **Neon grid in game:** DECIDED — expose a flag callable from game code (set `_GridOn` on the
  runtime material, e.g. `HexSeamlessTerrain.SetGridEnabled(bool)`); default off unless testing
  says otherwise. Zero shader cost when off.
- Overdraw 1.10 vs smaller (1.06) in game — bigger overlap hides more but PC/army art sits on
  top anyway; 1.10 matches the editor, start there.
- Where the runtime material + helper class live (follow existing project conventions).

## Known traps (all hit once already — don't rediscover them)

- `_BlendStrength` > 0.5 breaks seam symmetry (sides stop matching).
- Feather before the seam → both tiles translucent at seam → background grid. Only `t > 1`.
- Averaging raw neighbor RGB (instead of `Σrgb/Σa`) → black-contaminated dark rims.
- `LinearToGammaSpace` in-game → overbright; missing in editor → too dark. `_GammaOut` picks.
- Serialized floats in a .mat silently override new shader defaults — set values in BOTH.
- Board's neighbor tables are labeled flat-top, editor assumed pointy-top odd-r: derive offsets
  from measured transform positions, not from either convention.
- Scene overrides: if the runtime material is wired on the Hex prefab, remember
  `reference_hextexturemapping_prefab_vs_scene.md` / `reference_boardgenerator_scene_overrides.md`
  — edit the prefab (`Board.prefab` / `Hex.prefab` parts), not scene instances.
- Fog of war: hexes are NOT rendered until discovered. An unrevealed neighbor must be
  `valid = -1` (soft fade into fog; never `1` — terrain preview leak + see-through feather holes
  at the frontier). Reveals must trigger MPB rebuilds for the hex + its 6 neighbors (Step 2b).
