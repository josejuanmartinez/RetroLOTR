---
name: spritesheet-generation
description: Generate a character animation video from a static sprite image using the Scenario API with Grok Imagine Video 1.5. Animates the character through idle, action, forward walk, left walk, right walk, and turn-and-exit sequences. Output is an MP4 saved to Assets/Art/Characters/AnimationVideos/.
---

# Spritesheet Generation

Generate a 5-second character animation video from a static sprite PNG using Grok Imagine Video 1.5 via the Scenario API.

## Animation Sequence

The prompt instructs the model to animate through 6 phases, returning to neutral pose after each (except exit). The entire character must stay fully inside the camera frame — no limbs or body parts leave the screen edges. No camera zoom at any point.

1. **Idle** — stands in place, subtle breathing, weight shift, gentle sway → return to neutral pose
2. **Action** — clear combat attack or spell cast with full arm/body motion → return to neutral pose
3. **Walk Forward** — multiple continuous walking steps toward the viewer, full leg+arm walk cycle, visibly advancing for the entire phase → return to neutral pose
4. **Walk Left** — multiple continuous walking steps to the left, full walk cycle, visibly traveling left across the screen for the entire phase → return to neutral pose
5. **Walk Right** — multiple continuous walking steps to the right, full walk cycle, visibly traveling right for the entire phase → return to neutral pose
6. **Turn and Exit** — turns to face away, then walks away in a continuous walk cycle until small in the distance (no camera zoom)

## Workflow

1. Identify the source character PNG (a flat sprite or portrait image).
2. Run `scripts/generate_spritesheet_video.py` with `--image` pointing at the sprite.
3. The script uploads the image to Scenario to get an asset ID.
4. It submits the generation job to Grok Imagine Video 1.5.
5. It polls until the job completes, then downloads the output MP4.
6. The video is saved to `Assets/Art/Characters/AnimationVideos/<stem>.mp4`.

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
| `--duration` | `15` | Video length in seconds (1–15) |
| `--resolution` | `480p` | `480p` or `720p` |
| `--num-outputs` | `1` | Concurrent videos to generate (1–4) |
| `--dry-run` | off | Print parameters, skip API calls |

## Default Prompt

```
Animate this character performing the following distinct movement phases in order. CRITICAL: keep the entire character body fully within the camera frame at all times — no limbs, head, or body parts should ever leave the screen edges. Add internal padding so the character never touches the frame border. Do NOT zoom the camera in or out at any point. Phase 1 — IDLE: the character stands in place with subtle breathing, weight shift, and gentle swaying. Return to neutral standing pose. Phase 2 — ACTION: the character performs a clear combat attack or spell cast with full arm and body motion. Return to neutral standing pose. Phase 3 — WALK FORWARD: the character takes multiple continuous walking steps directly toward the viewer, legs and arms swinging in a full walk cycle, visibly advancing forward for the entire phase. Return to neutral standing pose. Phase 4 — WALK LEFT: the character takes multiple continuous walking steps to the left, legs and arms swinging in a full walk cycle, visibly traveling left across the screen for the entire phase. Return to neutral standing pose. Phase 5 — WALK RIGHT: the character takes multiple continuous walking steps to the right, legs and arms swinging in a full walk cycle, visibly traveling right across the screen for the entire phase. Return to neutral standing pose. Phase 6 — TURN AND EXIT: the character turns around to face away from the viewer, then walks away in a continuous walk cycle until they are small in the distance. No camera zoom.
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

## Output Location

```
Assets/Art/Characters/AnimationVideos/<character-name>.mp4
```

If multiple outputs were requested: `<character-name>_0.mp4`, `<character-name>_1.mp4`, etc.

## Completion Report

After finishing, always report:
- Source image path
- Asset ID returned by the upload
- Inference ID from the generation job
- Output file path(s)
- Prompt used
- Duration and resolution used
