---
name: spritesheet-generation
description: Generate a character animation video from a static sprite image using the Scenario API with Grok Imagine Video 1.5, then extract frames into a spritesheet PNG. Animates the character through idle, action, walk forward, walk left in place, turn back, and exit sequences. Output is an MP4 + spritesheet PNG.
---

# Spritesheet Generation

Generate a 12-second character animation video from a static sprite PNG using Grok Imagine Video 1.5 via the Scenario API.

## Animation Sequence

6 phases × 2 seconds each = 12 seconds total. Each phase is self-contained: starts from idle stance, executes the movement, comes to a full stop, returns to idle stance. Phases must NOT bleed into each other. Camera is completely static — no zoom, no pan.

**Idle stance** (reference for all phases): upright, facing directly toward viewer, feet shoulder-width apart, arms relaxed at sides.

1. **Idle** (2s) — holds idle stance with subtle chest breathing, gentle weight shift and sway. Defines the idle stance.
2. **Action** (2s) — decisive combat attack or spell cast with full arm/body motion → full stop → return to idle stance
3. **Walk Forward** (2s) — walks straight toward viewer (full walk cycle, 3+ strides, no sideways drift) → full stop → return to idle stance
4. **Walk Left Side View** (2s) — turns 90° left (left side faces camera, pure side profile) → walks left in side-view walk cycle (3+ strides) → full stop → turns 90° back to face camera → return to idle stance
5. **Turn Back** (2s) — rotates 180° to face completely away from viewer → back-facing idle stance (upright, still, back to camera)
6. **Exit Turned Back** (2s) — from back-facing idle, walks straight away from viewer, growing smaller in distance

## Workflow

1. Identify the source character PNG (a flat sprite or portrait image).
2. Run `scripts/generate_spritesheet_video.py` with `--image` pointing at the sprite.
3. The script uploads the image to Scenario to get an asset ID.
4. It submits the generation job to Grok Imagine Video 1.5.
5. It polls until the job completes, then downloads the output MP4.
6. The video is saved to `Assets/Art/Characters/AnimationVideos/<stem>.mp4`.
7. Run the extract spritesheet script on the output MP4 to produce a spritesheet PNG in `Assets/Art/Characters/Animations/`.

## CLI Contract

Dry-run example:
```powershell
python .agents/skills/spritesheet-generation/scripts/generate_spritesheet_video.py `
  --image "Assets/Art/Characters/Portraits/Gandalf.png" `
  --dry-run
```

Live run example:
```powershell
python .agents/skills/spritesheet-generation/scripts/generate_spritesheet_video.py `
  --image "Assets/Art/Characters/Portraits/Gandalf.png" `
  --out-dir "Assets/Art/Characters/AnimationVideos"
```

Custom prompt example:
```powershell
python .agents/skills/spritesheet-generation/scripts/generate_spritesheet_video.py `
  --image "Assets/Art/Characters/Portraits/Balrog.png" `
  --prompt "Animate this character: idle with fire flickering, then a dramatic wing-spread attack, then stomp forward, then strafe left, then strafe right, then turn and exit. No zoom out." `
  --duration 5 `
  --resolution 480p
```

## Parameters

| Flag | Default | Description |
|---|---|---|
| `--image` | required | Source character PNG path |
| `--out-dir` | `Assets/Art/Characters/AnimationVideos` | Output directory |
| `--prompt` | see below | Animation prompt text |
| `--duration` | `12` | Video length in seconds (1–15) |
| `--resolution` | `480p` | `480p` or `720p` |
| `--num-outputs` | `1` | Concurrent videos to generate (1–4) |
| `--dry-run` | off | Print parameters, skip API calls |

## Default Prompt

```
Animate this character performing 6 completely isolated movement phases. Each phase is self-contained: it starts from idle stance, executes its movement, comes to a FULL STOP, then returns to idle stance before the next phase begins. CRITICAL: phases must NOT bleed into each other — no drifting, turning, or transitioning while a phase is still running. Keep the entire character body fully within the camera frame at all times. The camera is completely static — no zoom, no pan. The idle stance (used as start and end of every phase) is: upright, facing directly toward the viewer, feet shoulder-width apart, arms relaxed at sides. Phase 1 — IDLE (2s): the character holds the idle stance and breathes with subtle chest rise, a gentle weight shift, and a small natural sway. This defines the idle stance. Phase 2 — ACTION (2s): from idle stance, the character performs a single decisive combat attack or spell cast with full arm and body motion, then comes to a complete stop and returns to idle stance. No movement before or after the action. Phase 3 — WALK FORWARD (2s): from idle stance, the character walks directly and straight toward the viewer — legs and arms in a full repeating walk cycle, at least 3 full stride cycles, advancing forward the entire time. Then comes to a complete stop and returns to idle stance. The character must NOT begin turning or drifting sideways at any point during this phase. Phase 4 — WALK LEFT SIDE VIEW (2s): from idle stance, the character turns exactly 90 degrees to the left so their LEFT side faces the camera (pure side profile). Then walks to the left in a clean side-view walk cycle — legs and arms swinging in profile, at least 3 full stride cycles. Then comes to a complete stop, turns exactly 90 degrees back to face the camera, and returns to idle stance. Phase 5 — TURN BACK (2s): from idle stance, the character smoothly rotates exactly 180 degrees to face completely away from the viewer, ending in a neutral back-facing idle stance — upright, still, back fully toward the camera. Phase 6 — EXIT TURNED BACK (2s): from the back-facing idle stance, the character walks straight away from the viewer in a continuous walk cycle, growing smaller in the distance. No camera movement.
```

## Authentication

Requires two environment variables:

```powershell
$env:SCENARIO_API_KEY        = "your-api-key"
$env:SCENARIO_API_KEY_SECRET = "your-api-secret"
```

The script encodes them as `Basic base64(KEY:SECRET)` per the Scenario API spec.

## Model Details

- **Model ID**: `model_xai-grok-imagine-video-1-5`
- **Endpoint**: `https://api.cloud.scenario.com/v1/generate/custom/model_xai-grok-imagine-video-1-5`
- **Image input**: uploaded asset ID (not raw binary in the generation call)
- **Type**: Third-Party (Grok image-to-video)

## Step 2: Extract Spritesheet

After the video is downloaded, run the extract spritesheet script:

```powershell
python .agents/skills/extract_spritesheet_from_video/scripts/extract_spritesheet.py `
  --video "Assets/Art/Characters/AnimationVideos/<character-name>.mp4" `
  --out "Assets/Art/Characters/Animations/<character-name>_spritesheet.png"
```

This samples 256 frames evenly across the full video and tiles them into a 16×16 grid PNG saved to `Assets/Art/Characters/Animations/`.

## Output Location

```
Assets/Art/Characters/AnimationVideos/<character-name>.mp4
Assets/Art/Characters/Animations/<character-name>_spritesheet.png
```

If multiple videos were requested: `<character-name>_0.mp4`, `<character-name>_1.mp4`, etc. — run the extract step for each.

## Completion Report

After finishing, always report:
- Source image path
- Asset ID returned by the upload
- Inference ID from the generation job
- Output video path(s)
- Output spritesheet path(s)
- Prompt used
- Duration and resolution used
- Spritesheet grid dimensions (cols × rows) and frame count
