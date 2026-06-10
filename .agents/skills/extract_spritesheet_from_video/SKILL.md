---
name: extract_spritesheet_from_video
description: Extract 64 evenly-spaced frames from a video file and arrange them into a spritesheet grid PNG. Use after spritesheet-generation to turn the output MP4 into a Unity-ready sprite atlas.
---

# Extract Spritesheet from Video

Extract N evenly-spaced frames from a video and tile them into a grid spritesheet PNG.

## Workflow

1. Take a source video path (e.g. from `Assets/Art/Characters/AnimationVideos/`).
2. Run `scripts/extract_spritesheet.py` with `--video` and `--out`.
3. The script samples 64 frames evenly across the full video duration.
4. Frames are arranged into an 8×8 grid and saved as a PNG.
5. Save the result to `Assets/Art/Characters/Animations/` alongside existing spritesheets.

## CLI Contract

Dry-run example:
```powershell
python .agents/skills/extract_spritesheet_from_video/scripts/extract_spritesheet.py `
  --video "Assets/Art/Characters/AnimationVideos/AlatarCharacter.mp4" `
  --out "Assets/Art/Characters/Animations/AlatarCharacter_spritesheet.png" `
  --dry-run
```

Live run example:
```powershell
python .agents/skills/extract_spritesheet_from_video/scripts/extract_spritesheet.py `
  --video "Assets/Art/Characters/AnimationVideos/AlatarCharacter.mp4" `
  --out "Assets/Art/Characters/Animations/AlatarCharacter_spritesheet.png"
```

Custom grid (e.g. 10×7 = 70 frames):
```powershell
python .agents/skills/extract_spritesheet_from_video/scripts/extract_spritesheet.py `
  --video "Assets/Art/Characters/AnimationVideos/AlatarCharacter.mp4" `
  --out "Assets/Art/Characters/Animations/AlatarCharacter_spritesheet.png" `
  --frames 70 --cols 10
```

## Parameters

| Flag | Default | Description |
|---|---|---|
| `--video` | required | Source video file path |
| `--out` | required | Output spritesheet PNG path |
| `--frames` | `256` | Number of frames to extract |
| `--cols` | `16` | Grid columns (rows computed automatically) |
| `--dry-run` | off | Print parameters, skip processing |

## Default Layout

256 frames → 16 columns × 16 rows. Each cell is the native video frame size.

For RetroLOTR character animations (6 phases × 2s @ 12s total) use `--frames 48 --cols 8` to get an 8×6 grid (8 frames per phase).

## Dependencies

```powershell
uv pip install opencv-python pillow numpy
```

## Completion Report

Always report:
- Source video path
- Output spritesheet path
- Number of frames extracted
- Grid dimensions (cols × rows)
- Final spritesheet pixel dimensions
