---
name: video-generation
description: Generate a Grok Imagine MP4 through xAI's direct API in reference-to-video mode with up to seven images or image-to-video mode with one starting image. Use when Codex needs to create a new reference-guided video, animate a starting frame, or run and download a Grok video generation.
---

# Video Generation

Generate a reference-guided or starting-image video with an xAI Grok Imagine video model.

## Workflow

1. Inspect `Assets/Art/Videos/Inspiration` and identify the relevant reference images.
2. If the directory contains more than seven images, select at most seven that best support the requested video. Never choose more than seven.
3. Write a concrete motion-and-camera prompt. Refer to inputs as `<IMAGE_1>`, `<IMAGE_2>`, and so on when assigning distinct subjects, props, settings, or styles.
4. Run a dry run first to validate image count, formats, parameters, and output path.
5. Run the live request only when the user asked to generate the video. Video generation incurs xAI API charges.
6. Report the request ID, reference image paths, exact prompt, generation parameters, and saved MP4 path.

For image-to-video, inspect any additional inspiration images yourself and express their shared visual traits in the prompt. Send only the selected starting frame to the API.

## CLI

Use every supported image in the inspiration directory when it contains no more than seven:

```powershell
python .agents/skills/video-generation/scripts/generate_video.py `
  --prompt "A slow cinematic tracking shot through the painted fantasy setting shown in <IMAGE_1>." `
  --dry-run
```

Select references explicitly when more than seven exist or only some are relevant:

```powershell
python .agents/skills/video-generation/scripts/generate_video.py `
  --prompt "The warrior from <IMAGE_1> crosses the landscape from <IMAGE_2>, matching the painted mood of <IMAGE_3>." `
  --image "Assets/Art/Videos/Inspiration/warrior.png" `
  --image "Assets/Art/Videos/Inspiration/landscape.jpg" `
  --image "Assets/Art/Videos/Inspiration/style.webp" `
  --duration 10 `
  --aspect-ratio "16:9" `
  --resolution 720p
```

Animate one starting frame with Grok Imagine Video 1.5:

```powershell
python .agents/skills/video-generation/scripts/generate_video.py `
  --mode image-to-video `
  --model grok-imagine-video-1.5 `
  --image "Assets/Art/Videos/Inspiration/1.jpg" `
  --prompt "Animate the starting frame with restrained hand-drawn cel motion..." `
  --duration 10 `
  --aspect-ratio "16:9" `
  --resolution 1080p
```

Defaults:

- References: all supported images in `Assets/Art/Videos/Inspiration`
- Output directory: `Assets/Art/Videos/Generated`
- Model: `grok-imagine-video`
- Duration: 10 seconds (reference-to-video maximum)
- Aspect ratio: `16:9`
- Resolution: `720p`
- Poll interval: 5 seconds
- Timeout: 15 minutes

Supported local reference formats are PNG, JPEG, WebP, GIF, AVIF, and BMP. The script embeds them as base64 data URIs; it does not upload them elsewhere first.

## Authentication

Read the API key only from `GROK_API_KEY`. Never print, persist, or place it on the command line.

```powershell
$env:GROK_API_KEY = "your-xai-api-key"
```

The script sends it as a bearer token to `https://api.x.ai/v1`.

## Constraints

- Require a non-empty prompt and 1–7 reference images.
- Do not combine `reference_images` with image-to-video, video editing, or video extension fields.
- Keep reference-to-video duration at 10 seconds or less.
- Preserve input ordering because prompt labels such as `<IMAGE_1>` correspond to that order.
- In image-to-video mode, supply exactly one `--image`; it becomes the first frame.
- Do not retry a failed generation blindly. Report the API response without exposing secrets.

## Completion Report

Always include:

- Request ID
- Ordered reference image paths
- Exact prompt
- Model, duration, aspect ratio, and resolution
- Saved MP4 path
