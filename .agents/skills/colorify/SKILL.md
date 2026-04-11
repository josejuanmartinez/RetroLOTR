---
name: colorify
description: Recolor an existing RetroLOTR black-and-white card image into a square painted fantasy illustration using OpenAI image editing, one card at a time. Use when Codex needs to replace a shipped monochrome card with a color version in a vintage 70s-80s fantasy style, following the project's existing OpenAI image-call workflow.
---

# Colorify

Convert an existing RetroLOTR black-and-white image into a new color version, one asset at a time.

This skill is now in production mode. The style has already been calibrated and approved.

## Workflow
1. Start with exactly one source image.
2. Run `scripts/colorify_card.py` in `edit` mode against exactly one source image.
3. Overwrite the original asset directly unless the user explicitly asks for a preview file instead.
4. Let the script upload a downscaled preview copy by default instead of the full original. The local default is `--upload-max-dim 512`.
5. Use this prompt verbatim unless the user explicitly asks for changes:

```text
Convert this existing black-and-white card illustration into a 1:1 square painted fantasy image. Keep the same subject, scene, and overall composition recognizable. Render it in a late-1970s hand-painted cel-animation fantasy style like vintage animated Lord of the Rings: simplified hand-drawn shapes, expressive slightly cartooned anatomy, bold dark ink outlines, flat-to-soft cel shading, painterly watercolor-like forest backgrounds, varied scene-appropriate colors, moody magical lighting, aged film texture, and a retro illustrated fantasy atmosphere. Make it feel like an old animated fantasy frame, not realistic modern concept art. Remove any card frame or white margin if present. Avoid AI-generated anatomy mistakes such as extra fingers, double hands, duplicate limbs, or distorted faces. Restyle everything to feel thematically at home in Lord of the Rings. Avoid a flat sepia or uniformly brown color cast; use richer greens, blues, reds, golds, and earth tones as appropriate to the card subject. If the source image does not clearly reflect the card name, reinforce the named idea more clearly in the final image while keeping it recognizable. NO TEXT ALLOWED IN THE IMAGES. No text, no logo, no card frame, no white border, no extra characters, no modern elements.
```

6. Keep processing directly against the original asset unless the user asks to pause, preview, or revise the prompt.
7. If OpenAI misreads the subject, rely on the card-name or asset-name reinforcement rules already built into the prompt.

## Interaction Rules
- Default mode is overwrite-first.
- Do not create a preview file unless the user explicitly asks for one.
- Overwrite the original asset directly when the user asks to colorify it.
- If the user later wants to iterate on style again, temporarily switch back to preview mode for that asset.
- Batch processing is still opt-in and must be explicitly requested by the user.

## CLI Contract
Use the bundled script instead of writing one-off OpenAI runners.

Dry-run example:

```powershell
python .agents/skills/colorify/scripts/colorify_card.py `
  --image Assets/Art/Cards/Actions/Hide.png `
  --out Assets/Art/Cards/Actions/Hide.png `
  --upload-max-dim 512 `
  --dry-run
```

Live run example:

```powershell
python .agents/skills/colorify/scripts/colorify_card.py `
  --image Assets/Art/Cards/Actions/Hide.png `
  --out Assets/Art/Cards/Actions/Hide.png `
  --upload-max-dim 512 `
  --force
```

## Rules
- Use model `gpt-image-1.5`.
- Use `images.edit`, not `images.generate`.
- Pass exactly one card image as the edit target unless the user explicitly wants a multi-image edit experiment.
- Do not upload the full-size original unless there is a specific reason; use the script's downscaled upload preview path.
- Default output format is `.png`.
- Default size is `1024x1024`.
- By default, skip images that no longer appear mostly black-and-white; use `--allow-nonbw` only when intentionally redoing an already colorized card.
- Always include the card name in the final prompt so resource/action cards communicate the correct subject.
- If OpenAI blocks a request on safety/moderation and the prompt includes the card name prefix, automatically retry once without the card-name prefix.
- Preserve the card's overall subject and composition while changing style and color treatment.
- Keep the output square.
- Overwrite the original asset directly by default.
- Use a sibling preview file only when the user explicitly asks for preview mode.

## Prompt
Default prompt text:

```text
Convert this existing black-and-white card illustration into a 1:1 square painted fantasy image. Keep the same subject, scene, and overall composition recognizable. Render it in a late-1970s hand-painted cel-animation fantasy style like vintage animated Lord of the Rings: simplified hand-drawn shapes, expressive slightly cartooned anatomy, bold dark ink outlines, flat-to-soft cel shading, painterly watercolor-like forest backgrounds, varied scene-appropriate colors, moody magical lighting, aged film texture, and a retro illustrated fantasy atmosphere. Make it feel like an old animated fantasy frame, not realistic modern concept art. Remove any card frame or white margin if present. Avoid AI-generated anatomy mistakes such as extra fingers, double hands, duplicate limbs, or distorted faces. Restyle everything to feel thematically at home in Lord of the Rings. Avoid a flat sepia or uniformly brown color cast; use richer greens, blues, reds, golds, and earth tones as appropriate to the card subject. If the source image does not clearly reflect the card name, reinforce the named idea more clearly in the final image while keeping it recognizable. NO TEXT ALLOWED IN THE IMAGES. No text, no logo, no card frame, no white border, no extra characters, no modern elements.
```

Append concise card-specific guardrails when useful:
- mention the subject that must remain recognizable
- mention any elements that must stay in frame
- say `no text, no logo, no card frame`

## Environment
- Requires `OPENAI_API_KEY` for live calls.
- Uses the same OpenAI Python SDK pattern as the repo's image generation tooling.
- If the key is missing, stop and tell the user what is needed instead of faking success.

## Completion Report
Always report:
- source image path
- output image path
- whether the original was overwritten or left untouched
- model used
- exact prompt text used
