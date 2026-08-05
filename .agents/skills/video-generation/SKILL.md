---
name: video-generation
description: Generate and download RetroLOTR text-to-video MP4 clips with Black Forest Labs FLUX 3 through the official BFL API. Use when Codex needs to turn a written scene into video, write a FLUX 3 prompt in the project's dark late-1970s hand-painted and rotoscoped fantasy-animation style, or run configurable video generation with duration, resolution, aspect ratio, audio, and safety controls.
---

# Video Generation

Generate text-to-video through the official Black Forest Labs endpoint `POST https://api.bfl.ai/v1/flux-3-video` with `mode: "t2v"`. Do not send image, video, or audio references. Use the nine files in `Assets/Art/Videos/Inspiration` only as a local visual brief.

## Write the Prompt

Read [references/inspiration-style.md](references/inspiration-style.md) in full before drafting every generation prompt. Inspect the inspiration images again when the files have changed or the requested scene needs a feature not covered by the written analysis.

Build one coherent prompt in this order:

1. Subject, setting, and the single main action.
2. Shot size, composition, camera motion, and temporal continuity.
3. Character acting, cloth/hair/environmental motion, and physical cause and effect.
4. Lighting, palette, atmosphere, and depth treatment.
5. The canonical visual-style block from the reference, adapted without weakening its defining traits.
6. Audio direction: ambience, physical sounds, score character, and dialogue only when requested.
7. Negative constraints phrased as positive direction where possible: one continuous readable shot, stable anatomy, no captions, no logos, no modern objects, no glossy CGI.

Prefer one legible action over a montage. Describe concrete visible motion rather than abstract mood alone. Keep the scene request distinct from the style description.

## Confirm Paid Inputs

Before submitting a paid request, show the exact prompt and ask the user to confirm:

1. Duration: `auto` or 5-20 seconds.
2. Resolution: `hd` or `fhd`.
3. Aspect ratio: `auto`, `21:9`, `2:1`, `16:9`, `4:3`, `1:1`, `3:4`, or `9:16`.
4. Native audio: yes or no.
5. Safety tolerance: 0-4; recommend 2.
6. Optional output filename.

Recommend `10 seconds`, `hd`, `16:9`, native audio on, and safety tolerance 2 unless the deliverable suggests otherwise. Use the repository's numbered-choice format and end a question with: `Please choose a number and I will implement that option`.

## Workflow

1. Draft the prompt from the requested content and the inspiration style reference.
2. Run the CLI with `--dry-run` and review the exact payload with the user.
3. Submit only after the user asks to generate; FLUX 3 generation incurs Black Forest Labs API charges.
4. Poll the queue, download the completed MP4, and report the result.

## CLI

```powershell
python .agents/skills/video-generation/scripts/generate_video.py `
  --prompt "A lone grey pilgrim crosses a blasted ridge... [complete scene and style prompt]" `
  --duration 10 `
  --resolution hd `
  --aspect-ratio "16:9" `
  --generate-audio `
  --safety-tolerance 2 `
  --dry-run
```

Remove `--dry-run` only for a confirmed live request. Use `--no-generate-audio` for a silent clip.

Defaults:

- Output directory: `Assets/Art/Videos/Generated`
- Duration: `auto`
- Resolution: `hd`
- Aspect ratio: `auto`
- Native audio: enabled
- Safety tolerance: 2
- Poll interval: 5 seconds
- Timeout: 30 minutes

## Authentication

Read the API key only from `FLUX_API_KEY`. Never print, persist, log, or place it on the command line. Send it only to `api.bfl.ai` in the official `x-key` header.

## Constraints

- Require a non-empty prompt.
- Use text-to-video only; do not accept reference media arguments.
- Do not retry failed paid generations blindly. Report the response without exposing secrets.
- Treat the live endpoint schema as authoritative because FLUX 3 is a new model. Re-check it before changing supported options.
- Never claim the inspiration images were supplied to FLUX 3; their characteristics are translated into text.

## Completion Report

Always include the request ID, exact prompt, duration, resolution, aspect ratio, audio setting, safety tolerance, returned seed, and saved MP4 path.
