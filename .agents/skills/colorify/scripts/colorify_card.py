#!/usr/bin/env python3
"""Colorify a single RetroLOTR card image using OpenAI image editing."""

from __future__ import annotations

import argparse
import base64
import os
import re
import sys
import time
from io import BytesIO
from pathlib import Path


DEFAULT_MODEL = "gpt-image-1.5"
DEFAULT_SIZE = "1024x1024"
DEFAULT_OUTPUT_FORMAT = "png"
DEFAULT_QUALITY = "auto"
DEFAULT_PROMPT = (
    "Convert this existing black-and-white card illustration into a 1:1 square "
    "painted fantasy image. Keep the same subject, scene, and overall "
    "composition recognizable. Render it in a late-1970s hand-painted "
    "cel-animation fantasy style like vintage animated Lord of the Rings: "
    "simplified hand-drawn shapes, expressive slightly cartooned anatomy, bold "
    "dark ink outlines, flat-to-soft cel shading, painterly watercolor-like "
    "forest backgrounds, varied scene-appropriate colors, moody magical "
    "lighting, aged film texture, and a retro illustrated fantasy atmosphere. "
    "Make it feel like an old animated fantasy frame, not realistic modern "
    "concept art. Remove any card frame or white margin if present. Avoid "
    "AI-generated anatomy mistakes such as extra fingers, double hands, "
    "duplicate limbs, or distorted faces. Restyle everything to feel "
    "thematically at home in Lord of the Rings. Avoid a flat sepia or "
    "uniformly brown color cast; use richer greens, blues, reds, golds, and "
    "earth tones as appropriate to the card subject. If the source image does "
    "not clearly reflect the card name, reinforce the named idea more clearly "
    "in the final image while keeping it recognizable. NO TEXT ALLOWED IN THE "
    "IMAGES. No text, no logo, no card frame, no white border, no extra "
    "characters, no modern elements."
)
MAX_IMAGE_BYTES = 50 * 1024 * 1024
DEFAULT_GRAYSCALE_THRESHOLD = 0.85
DEFAULT_CHANNEL_TOLERANCE = 12


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


def normalize_output_format(fmt: str) -> str:
    lowered = (fmt or DEFAULT_OUTPUT_FORMAT).lower()
    if lowered == "jpg":
        lowered = "jpeg"
    if lowered not in {"png", "jpeg", "webp"}:
        die("output-format must be png, jpeg, jpg, or webp.")
    return lowered


def validate_size(size: str) -> None:
    if size not in {"1024x1024", "1536x1024", "1024x1536", "auto"}:
        die("size must be one of 1024x1024, 1536x1024, 1024x1536, or auto.")


def build_output_path(path: str, output_format: str) -> Path:
    out_path = Path(path)
    if out_path.suffix == "":
        return out_path.with_suffix("." + output_format)
    return out_path


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Colorify a single card image with OpenAI image edits")
    parser.add_argument("--image", required=True, help="Path to the black-and-white card image")
    parser.add_argument("--out", required=True, help="Path to write the new color image")
    parser.add_argument("--prompt", default=DEFAULT_PROMPT, help="Prompt override")
    parser.add_argument("--card-name", help="Override the derived card name used in the final prompt")
    parser.add_argument("--model", default=DEFAULT_MODEL)
    parser.add_argument("--size", default=DEFAULT_SIZE)
    parser.add_argument("--quality", default=DEFAULT_QUALITY)
    parser.add_argument("--output-format", default=DEFAULT_OUTPUT_FORMAT)
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--force", action="store_true")
    parser.add_argument("--allow-nonbw", action="store_true", help="Process even if the image already appears colorized")
    return parser.parse_args()


def humanize_card_name(image_path: Path) -> str:
    stem = image_path.stem
    return re.sub(r"(?<!^)([A-Z])", r" \1", stem).replace("_", " ").strip()


def build_prompt(base_prompt: str, image_path: Path, card_name_override: str | None = None) -> str:
    card_name = (card_name_override or humanize_card_name(image_path)).strip()
    return (
        f"Card name: {card_name}. Ensure the final image clearly matches the named "
        f"card concept and subject.\n{base_prompt.strip()}"
    )


def is_moderation_block(exc: Exception) -> bool:
    message = str(exc).lower()
    return "moderation_blocked" in message or "safety system" in message


def create_client():
    try:
        from openai import OpenAI
    except ImportError as exc:
        die("openai SDK not installed in the active environment. Install it with `uv pip install openai`.")  # noqa: TRY003
        raise exc
    return OpenAI()


def analyze_grayscale_ratio(image_path: Path, tolerance: int = DEFAULT_CHANNEL_TOLERANCE) -> float:
    try:
        from PIL import Image
    except ImportError:
        die("Pillow is required for grayscale detection. Install it with `uv pip install pillow`.")

    with Image.open(image_path) as img:
        rgb = img.convert("RGB")
        rgb.thumbnail((256, 256))
        pixels = rgb.load()
        width, height = rgb.size
        total = 0
        grayscale_like = 0
        for y in range(height):
            for x in range(width):
                r, g, b = pixels[x, y]
                total += 1
                if max(r, g, b) - min(r, g, b) <= tolerance:
                    grayscale_like += 1

    if total == 0:
        return 1.0
    return grayscale_like / total


def main() -> int:
    args = parse_args()
    ensure_api_key(args.dry_run)
    validate_size(args.size)
    output_format = normalize_output_format(args.output_format)

    image_path = Path(args.image)
    if not image_path.exists():
        die(f"Image file not found: {image_path}")
    if image_path.stat().st_size > MAX_IMAGE_BYTES:
        die(f"Image exceeds 50MB limit: {image_path}")

    grayscale_ratio = analyze_grayscale_ratio(image_path)
    if not args.allow_nonbw and grayscale_ratio < DEFAULT_GRAYSCALE_THRESHOLD:
        print(
            (
                f"Skipping {image_path} because it already appears colorized "
                f"(grayscale_ratio={grayscale_ratio:.3f} < {DEFAULT_GRAYSCALE_THRESHOLD:.2f}). "
                "Use --allow-nonbw to force recolor."
            ),
            file=sys.stderr,
        )
        return 0

    out_path = build_output_path(args.out, output_format)
    if out_path.exists() and not args.force:
        die(f"Output already exists: {out_path} (use --force to overwrite)")

    final_prompt = build_prompt(args.prompt, image_path, args.card_name)

    payload = {
        "model": args.model,
        "prompt": final_prompt,
        "size": args.size,
        "quality": args.quality,
        "output_format": output_format,
    }

    if args.dry_run:
        print("OpenAI image edit dry-run")
        print(f"image={image_path}")
        print(f"out={out_path}")
        print(f"grayscale_ratio={grayscale_ratio:.3f}")
        for key, value in payload.items():
            print(f"{key}={value}")
        return 0

    client = create_client()
    out_path.parent.mkdir(parents=True, exist_ok=True)

    print("Calling OpenAI image edit API...", file=sys.stderr)
    started = time.time()
    try:
        with image_path.open("rb") as image_file:
            result = client.images.edit(
                image=image_file,
                **payload,
            )
    except Exception as exc:
        if not is_moderation_block(exc):
            raise

        print(
            "OpenAI safety block hit. Retrying once without the card-name prefix.",
            file=sys.stderr,
        )
        payload["prompt"] = args.prompt.strip()
        started = time.time()
        with image_path.open("rb") as image_file:
            result = client.images.edit(
                image=image_file,
                **payload,
            )
    elapsed = time.time() - started
    print(f"Edit completed in {elapsed:.1f}s.", file=sys.stderr)

    if not result.data:
        die("OpenAI returned no image data.")

    image_b64 = result.data[0].b64_json
    if not image_b64:
        die("OpenAI response did not include b64_json output.")

    out_path.write_bytes(base64.b64decode(image_b64))
    print(f"Wrote {out_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
