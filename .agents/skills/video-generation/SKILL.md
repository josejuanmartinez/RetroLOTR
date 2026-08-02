---
name: video-generation
description: Generate and download reference-guided MP4 videos with ByteDance Seedance 2.0 through fal.ai. Use when Codex needs to animate RetroLOTR card artwork, combine selected card references into a video, or run Seedance reference-to-video generation with configurable duration, resolution, aspect ratio, audio, bitrate, and seed.
---

# Video Generation

Generate a reference-guided video through fal.ai endpoint `bytedance/seedance-2.0/reference-to-video`.

## Gather Inputs

Before generating, inspect `Assets/Art/Cards` and ask the user which card images to use. Present likely matches as a numbered list with paths; do not choose card references without confirmation. Preserve the user's selected order because prompts address them as `@Image1`, `@Image2`, and so on.

Explain that the API accepts at most 12 reference files total, but no more than 9 may be images. Therefore, a cards-only request supports 1-9 card images, not 12. It may additionally contain up to 3 videos and 3 audio files while remaining within the 12-file total.

Ask for all generation choices before submitting a paid request:

1. Card reference images (1-9), in intended order.
2. Prompt or desired scene, motion, camera behavior, and audio/dialogue.
3. Duration: `auto` or 4-15 seconds.
4. Resolution: `480p`, `720p`, `1080p`, or `4k`.
5. Aspect ratio: `auto`, `21:9`, `16:9`, `4:3`, `1:1`, `3:4`, or `9:16`.
6. Generate synchronized audio: yes or no.
7. Bitrate: `standard` or `high`.
8. Optional seed for reproducibility; otherwise omit it.
9. Optional reference videos/audio, subject to the modality and 12-file limits.
10. Optional output filename; otherwise derive it from the request ID.

Offer the API defaults (`auto`, `720p`, `auto`, audio on, standard bitrate, random seed) as the recommended option. Use the repo-required numbered-choice format and end questions with: `Please choose a number and I will implement that option`.

## Workflow

1. Validate the selected local references and write a concrete prompt using `@Image1`, `@Image2`, `@Video1`, and `@Audio1` labels where relevant.
2. Run the CLI with `--dry-run` and show the user the ordered references, exact prompt, and parameters.
3. Submit the live request only after the user has asked to generate it; generation incurs fal.ai charges.
4. Poll the fal.ai queue, download the completed MP4, and report the result.

## CLI

```powershell
python .agents/skills/video-generation/scripts/generate_video.py `
  --prompt "The ranger from @Image1 crosses the landscape of @Image2 in a slow painted-fantasy tracking shot." `
  --image "Assets/Art/Cards/Characters/ranger.png" `
  --image "Assets/Art/Cards/Events/landscape.jpg" `
  --duration 10 `
  --resolution 720p `
  --aspect-ratio "16:9" `
  --generate-audio `
  --bitrate-mode standard `
  --dry-run
```

Remove `--dry-run` only for the confirmed live request. Use `--no-generate-audio` to disable generated audio. Repeat `--video` and `--audio` for optional non-image references.

Defaults:

- Output directory: `Assets/Art/Videos/Generated`
- Duration: `auto`
- Resolution: `720p`
- Aspect ratio: `auto`
- Generated audio: enabled
- Bitrate: `standard`
- Poll interval: 5 seconds
- Timeout: 30 minutes

The script embeds local references as base64 data URIs. Supported images are JPEG, PNG, and WebP; videos are MP4 and MOV; audio files are MP3 and WAV.

## Authentication

Read the API key only from `FAL_API_KEY`. Never print, persist, or place it on the command line. Send it to fal.ai using `Authorization: Key ...`.

## Constraints

- Require a non-empty prompt.
- Allow no more than 9 images, 3 videos, 3 audio files, and 12 total references.
- Require at least one image or video when audio references are supplied.
- Preserve reference ordering and prompt labels.
- Do not retry failed paid generations blindly; report the response without exposing secrets.
- Treat `1080p`, `4k`, `bitrate_mode`, and `seed` as schema-dependent options. If fal.ai rejects one, report the response and re-check the live endpoint schema before resubmitting.

## Completion Report

Always include the request ID, ordered reference paths, exact prompt, all generation parameters, returned seed, and saved MP4 path.
