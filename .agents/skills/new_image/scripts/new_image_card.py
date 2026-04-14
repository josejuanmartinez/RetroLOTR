#!/usr/bin/env python3
"""Generate a new RetroLOTR card image using random shipped card references."""

from __future__ import annotations

import argparse
import base64
import os
import random
import re
import sys
import tempfile
import time
from pathlib import Path

from bw_postprocess import to_pure_bw


DEFAULT_MODEL = "gpt-5"
DEFAULT_EDIT_MODEL = "gpt-image-1.5"
DEFAULT_SIZE = "1024x1024"
DEFAULT_REFERENCE_COUNT = 3
MAX_IMAGE_BYTES = 50 * 1024 * 1024
ALLOWED_EXTENSIONS = {".png", ".jpg", ".jpeg"}
EXCLUDED_PREFIXES = ("CardFrame", "CardFrameBlack")
REPO_ROOT = Path(__file__).resolve().parents[4]

SKETCH_PROMPT = (
    "Create a 1:1 square composition sketch for a RetroLOTR card.\n"
    "The named card subject must be the clear focal point.\n"
    "Keep the subject large, readable, and centered with a strong silhouette and "
    "clear card-art composition.\n"
    "Use the uploaded reference card images only as rough style, texture, and "
    "print-look guides. Do not copy their exact subjects, layouts, or symbols.\n"
    "Render it as a rough black-and-white fantasy illustration with bold contour "
    "lines, simple shadow masses, visible sketch texture, slightly flattened "
    "perspective, and a scanned old-print feel.\n"
    "Make it feel like a quick old fantasy card layout study, not a modern concept "
    "render, not glossy, not photoreal, not 3D, and not anime.\n"
    "No modern UI elements, no text overlays, no logos, no extra "
    "characters, no card frame, no white border, no watermarks."
)

COLORIFY_PROMPT = (
    "Convert this existing black-and-white card illustration into a 1:1 square "
    "painted fantasy image. Keep the same subject, scene, and overall composition "
    "recognizable. Render it in a late-1970s hand-painted cel-animation fantasy "
    "style like vintage animated Lord of the Rings: simplified hand-drawn shapes, "
    "expressive slightly cartooned anatomy, bold dark ink outlines, flat-to-soft "
    "cel shading, painterly watercolor-like forest backgrounds, varied "
    "scene-appropriate colors, moody magical lighting, aged film texture, and a "
    "retro illustrated fantasy atmosphere. Make it feel like an old animated fantasy "
    "frame, not realistic modern concept art. Remove any card frame or white margin "
    "if present. Avoid AI-generated anatomy mistakes such as extra fingers, double "
    "hands, duplicate limbs, or distorted faces. Restyle everything to feel "
    "thematically at home in Lord of the Rings. Avoid a flat sepia or uniformly "
    "brown color cast; use richer greens, blues, reds, golds, and earth tones as "
    "appropriate to the card subject. If the source image does not clearly reflect "
    "the card name, reinforce the named idea more clearly in the final image while "
    "keeping it recognizable. NO TEXT ALLOWED IN THE IMAGES. No text, no logo, no "
    "card frame, no white border, no extra characters, no modern elements."
)


def die(message: str, code: int = 1) -> None:
    print(f"Error: {message}", file=sys.stderr)
    raise SystemExit(code)


def ensure_api_key(dry_run: bool) -> None:
    if os.getenv("OPENAI_API_KEY"):
        return
    if dry_run:
        print("Warning: OPENAI_API_KEY is not set; dry-run only.", file=sys.stderr)
        return
    die("OPENAI_API_KEY is not set. Export it before running.")


def validate_size(size: str) -> None:
    if size not in {"1024x1024", "1536x1024", "1024x1536", "auto"}:
        die("size must be one of 1024x1024, 1536x1024, 1024x1536, or auto.")


def build_output_path(path: str) -> Path:
    out_path = Path(path)
    if not out_path.is_absolute():
        out_path = REPO_ROOT / out_path
    return out_path if out_path.suffix else out_path.with_suffix(".png")


def humanize_card_name(name: str) -> str:
    cleaned = re.sub(r"(?<!^)([A-Z])", r" \1", name).replace("_", " ").replace("-", " ").strip()
    return " ".join(token[:1].upper() + token[1:] if token else token for token in cleaned.split())


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Generate a new RetroLOTR card image from random shipped references"
    )
    parser.add_argument("--out", required=True, help="Path to write the generated image")
    parser.add_argument(
        "--prompt",
        required=True,
        help="Art brief describing the new image subject and desired scene",
    )
    parser.add_argument("--card-name", help="Optional card name to emphasize in the final prompt")
    parser.add_argument("--model", default=DEFAULT_MODEL)
    parser.add_argument("--edit-model", default=DEFAULT_EDIT_MODEL)
    parser.add_argument("--size", default=DEFAULT_SIZE)
    parser.add_argument(
        "--reference-root",
        default=str(REPO_ROOT / "Assets" / "Art" / "Cards"),
        help="Root folder to sample shipped card references from",
    )
    parser.add_argument(
        "--reference-count",
        type=int,
        default=DEFAULT_REFERENCE_COUNT,
        help="Number of random references to report and anchor in the prompt",
    )
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--force", action="store_true")
    parser.add_argument("--single-pass", action="store_true", help="Skip the sketch-then-colorify workflow and generate a single image pass.")
    return parser.parse_args()


def create_client():
    try:
        from openai import OpenAI
    except ImportError as exc:
        die("openai SDK not installed in the active environment. Install it with `uv pip install openai`.")  # noqa: TRY003
        raise exc
    return OpenAI()


def upload_reference_files(client, reference_paths: list[Path]) -> list[str]:
    file_ids: list[str] = []
    for path in reference_paths:
        with path.open("rb") as file_content:
            result = client.files.create(file=file_content, purpose="vision")
        file_ids.append(result.id)
    return file_ids


def list_card_reference_candidates(reference_root: Path, exclude_path: Path | None = None) -> list[Path]:
    if not reference_root.exists():
        die(f"Reference root not found: {reference_root}")

    excluded = exclude_path.resolve() if exclude_path is not None else None
    candidates: list[Path] = []
    for path in reference_root.rglob("*"):
        if not path.is_file():
            continue
        if path.suffix.lower() not in ALLOWED_EXTENSIONS:
            continue
        if any(path.name.startswith(prefix) for prefix in EXCLUDED_PREFIXES):
            continue
        if excluded is not None and path.resolve() == excluded:
            continue
        candidates.append(path)

    return candidates


def choose_references(reference_root: Path, count: int, exclude_path: Path | None = None) -> list[Path]:
    candidates = list_card_reference_candidates(reference_root, exclude_path=exclude_path)
    if len(candidates) < count:
        die(f"Need at least {count} shipped card images under {reference_root}; found {len(candidates)}.")
    return random.sample(candidates, count)


def build_sketch_prompt(user_prompt: str, card_name: str | None = None) -> str:
    brief = user_prompt.strip()
    name = humanize_card_name(card_name) if card_name else None
    header = ""
    if name:
        header = (
            f"Card name: {name}. Ensure the final image clearly matches the named "
            f"card concept and subject.\n"
        )
    return (
        f"{header}"
        f"{SKETCH_PROMPT}\n"
        f"Art brief: {brief}"
    )


def build_colorify_prompt(card_name: str | None = None) -> str:
    name = humanize_card_name(card_name) if card_name else None
    if not name:
        return COLORIFY_PROMPT
    return (
        f"Card name: {name}. Ensure the final image clearly matches the named "
        f"card concept and subject.\n{COLORIFY_PROMPT}"
    )


def is_moderation_block(exc: Exception) -> bool:
    message = str(exc).lower()
    return "moderation_blocked" in message or "safety system" in message


def save_generated_image(
    client,
    prompt: str,
    reference_file_ids: list[str],
    size: str,
    out_path: Path,
    model: str,
) -> None:
    response = client.responses.create(
        model=model,
        input=[
            {
                "role": "user",
                "content": [
                    {"type": "input_text", "text": prompt},
                    *[
                        {"type": "input_image", "file_id": file_id}
                        for file_id in reference_file_ids
                    ],
                ],
            }
        ],
        tools=[
            {
                "type": "image_generation",
                "action": "generate",
                "input_fidelity": "high",
                "size": size,
                "quality": "high",
                "background": "opaque",
            }
        ],
        tool_choice={"type": "image_generation"},
    )

    image_data = [
        output.result
        for output in response.output
        if getattr(output, "type", None) == "image_generation_call"
    ]
    if not image_data:
        die("OpenAI returned no image data.")

    out_path.write_bytes(base64.b64decode(image_data[0]))


def bw_postprocess_image(input_path: Path, out_path: Path) -> None:
    with input_path.open("rb") as input_file:
        from PIL import Image

        with Image.open(input_file) as img:
            bw = to_pure_bw(img, contrast_boost=1.35, threshold=-1, dither=False)
            bw.save(out_path, format="PNG")


def colorify_generated_image(
    client,
    image_path: Path,
    prompt: str,
    size: str,
    out_path: Path,
    model: str,
) -> None:
    with image_path.open("rb") as image_file:
        result = client.images.edit(
            model=model,
            image=image_file,
            prompt=prompt,
            size=size,
            quality="auto",
            output_format="png",
        )

    if not result.data:
        die("OpenAI returned no image data.")
    image_b64 = result.data[0].b64_json
    if not image_b64:
        die("OpenAI response did not include b64_json output.")
    out_path.write_bytes(base64.b64decode(image_b64))


def main() -> int:
    args = parse_args()
    ensure_api_key(args.dry_run)
    validate_size(args.size)

    out_path = build_output_path(args.out)
    if out_path.exists() and not args.force:
        die(f"Output already exists: {out_path} (use --force to overwrite)")

    reference_root = Path(args.reference_root)
    if not reference_root.is_absolute():
        reference_root = REPO_ROOT / reference_root
    reference_paths = choose_references(reference_root, args.reference_count, exclude_path=out_path)
    card_name = args.card_name or out_path.stem
    sketch_prompt = build_sketch_prompt(args.prompt, card_name)
    final_prompt = build_colorify_prompt(card_name)

    if args.dry_run:
        print("OpenAI three-step image-generation dry-run" if not args.single_pass else "OpenAI responses image-generation dry-run")
        print(f"reference_root={reference_root}")
        for index, reference_path in enumerate(reference_paths, start=1):
            print(f"reference_{index}={reference_path}")
        print(f"out={out_path}")
        print(f"model={args.model}")
        print(f"edit_model={args.edit_model}")
        print(f"size={args.size}")
        print(f"single_pass={args.single_pass}")
        print("tool=image_generation")
        print("action=generate")
        print("input_fidelity=high")
        print("stage1_prompt=")
        print(sketch_prompt)
        print("stage2_prompt=")
        print(final_prompt)
        return 0

    client = create_client()
    out_path.parent.mkdir(parents=True, exist_ok=True)
    reference_file_ids = upload_reference_files(client, reference_paths)

    print("Calling OpenAI responses image-generation API...", file=sys.stderr)
    started = time.time()
    sketch_path: Path | None = None
    bw_path: Path | None = None
    try:
        if args.single_pass:
            response = client.responses.create(
                model=args.model,
                input=[
                    {
                        "role": "user",
                        "content": [
                            {"type": "input_text", "text": sketch_prompt},
                            *[
                                {"type": "input_image", "file_id": file_id}
                                for file_id in reference_file_ids
                            ],
                        ],
                    }
                ],
                tools=[
                    {
                        "type": "image_generation",
                        "action": "generate",
                        "input_fidelity": "high",
                        "size": args.size,
                        "quality": "high",
                        "background": "opaque",
                    }
                ],
                tool_choice={"type": "image_generation"},
            )
            image_data = [
                output.result
                for output in response.output
                if getattr(output, "type", None) == "image_generation_call"
            ]
            if not image_data:
                die("OpenAI returned no image data.")
            out_path.write_bytes(base64.b64decode(image_data[0]))
        else:
            with tempfile.NamedTemporaryFile(delete=False, suffix=".png") as tmp:
                sketch_path = Path(tmp.name)
            with tempfile.NamedTemporaryFile(delete=False, suffix=".png") as tmp:
                bw_path = Path(tmp.name)
            save_generated_image(client, sketch_prompt, reference_file_ids, args.size, sketch_path, args.model)
            bw_postprocess_image(sketch_path, bw_path)
            colorify_generated_image(client, bw_path, final_prompt, args.size, out_path, args.edit_model)
    except Exception as exc:
        if not is_moderation_block(exc):
            raise
        die(f"OpenAI safety block hit: {exc}")
    finally:
        if sketch_path is not None and sketch_path.exists():
            sketch_path.unlink()
        if bw_path is not None and bw_path.exists():
            bw_path.unlink()

    elapsed = time.time() - started
    print(f"Generation completed in {elapsed:.1f}s.", file=sys.stderr)
    print(f"Wrote {out_path}")
    print("Reference images used:")
    for reference_path in reference_paths:
        print(f"- {reference_path}")
    print("Reference format: uploaded file IDs via Responses API.")
    if args.single_pass:
        print("Workflow: single-pass generate")
    else:
        print("Workflow: three-step generate-then-bw-postprocess-then-colorify")
    if args.card_name:
        print(f"Card name: {humanize_card_name(args.card_name)}")
    print("Stage 1 prompt used:")
    print(sketch_prompt)
    if not args.single_pass:
        print("Stage 2: strict black-and-white postprocess applied to stage 1 output.")
        print("Stage 2 prompt used:")
        print(final_prompt)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
