#!/usr/bin/env python3
"""Generate a new RetroLOTR card image in a single gpt-image-2 edit call using random shipped card references."""

from __future__ import annotations

import argparse
import base64
import os
import random
import re
import sys
import time
from pathlib import Path


DEFAULT_MODEL = "gpt-image-2"
DEFAULT_SIZE = "1024x1024"
DEFAULT_QUALITY = "high"
DEFAULT_REFERENCE_COUNT = 3
DEFAULT_UPLOAD_MAX_DIM = 512
# gpt-image-2 hard limits: total pixels in [655360, 8294400], longest side <= 3840.
MIN_PIXEL_BUDGET = 655_360
MAX_PIXEL_BUDGET = 8_294_400
MAX_SIDE = 3840
MAX_IMAGE_BYTES = 50 * 1024 * 1024
ALLOWED_EXTENSIONS = {".png", ".jpg", ".jpeg"}
EXCLUDED_PREFIXES = ("CardFrame", "CardFrameBlack")
REPO_ROOT = Path(__file__).resolve().parents[4]

STYLE_BLOCK = (
    "1:1 square painted fantasy card illustration with a strong centered "
    "subject, clear silhouette, card-art readability. Bakshi-era Lord of the "
    "Rings mood, D&D cover art energy, MERP-style roleplaying-game "
    "illustration. Late-1970s hand-painted cel-animation fantasy style: "
    "simplified hand-drawn shapes with expressive slightly cartooned "
    "anatomy, bold dark ink outlines, flat-to-soft cel shading, painterly "
    "watercolor-like backgrounds, moody magical lighting, heavy printed "
    "grain, jagged dark contour lines, strong shadows, and a real scanned "
    "fantasy-card look that matches the shipped RetroLOTR art. Use varied "
    "scene-appropriate colors — avoid a flat sepia or uniformly brown cast. "
    "No modern UI elements, no text overlays, no logos, no extra characters, "
    "no card frame, no white border, no watermarks."
)

REFERENCE_USAGE = (
    "The uploaded images are existing RetroLOTR card art, provided only as "
    "style, texture, and print-look guides — match their hand-painted look, "
    "ink linework, and paint texture. Do NOT copy their subjects, layouts, "
    "or symbols; render the new subject described in the art brief above."
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
    if size == "auto":
        return
    match = re.fullmatch(r"(\d+)x(\d+)", size)
    if not match:
        die("size must be WIDTHxHEIGHT (e.g. 1024x1024) or auto.")
    width, height = int(match.group(1)), int(match.group(2))
    if width % 16 != 0 or height % 16 != 0:
        die("gpt-image-2 requires width and height to each be divisible by 16.")
    ratio = width / height
    if not (1 / 3 <= ratio <= 3):
        die("gpt-image-2 requires an aspect ratio between 1:3 and 3:1.")
    if max(width, height) > MAX_SIDE:
        die(f"gpt-image-2 requires the longest side to be at most {MAX_SIDE}px.")
    pixels = width * height
    if not (MIN_PIXEL_BUDGET <= pixels <= MAX_PIXEL_BUDGET):
        die(
            f"gpt-image-2 requires total pixels between {MIN_PIXEL_BUDGET} and "
            f"{MAX_PIXEL_BUDGET} (got {width}x{height} = {pixels})."
        )


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
        description="Generate a new RetroLOTR card image from random shipped references in one gpt-image-2 edit call"
    )
    parser.add_argument("--out", required=True, help="Path to write the generated image")
    parser.add_argument(
        "--prompt",
        required=True,
        help="Art brief describing the new image subject and desired scene",
    )
    parser.add_argument("--card-name", help="Optional card name to emphasize in the final prompt")
    parser.add_argument("--model", default=DEFAULT_MODEL)
    parser.add_argument("--size", default=DEFAULT_SIZE)
    parser.add_argument("--quality", default=DEFAULT_QUALITY, help="gpt-image-2 quality: low, medium, high, or auto")
    parser.add_argument(
        "--reference-root",
        default=str(REPO_ROOT / "Assets" / "Art" / "Cards"),
        help="Root folder to sample shipped card references from",
    )
    parser.add_argument(
        "--reference-count",
        type=int,
        default=DEFAULT_REFERENCE_COUNT,
        help="Number of random references to send as style anchors",
    )
    parser.add_argument(
        "--upload-max-dim",
        type=int,
        default=DEFAULT_UPLOAD_MAX_DIM,
        help="Maximum width/height for reference images uploaded to OpenAI. Use 0 to disable downscaling.",
    )
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--force", action="store_true")
    return parser.parse_args()


def create_client():
    try:
        from openai import OpenAI
    except ImportError as exc:
        die("openai SDK not installed in the active environment. Install it with `uv pip install openai`.")  # noqa: TRY003
        raise exc
    return OpenAI()


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


def build_prompt(user_prompt: str, card_name: str | None = None) -> str:
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
        f"Art brief: {brief}\n"
        f"{STYLE_BLOCK}\n"
        f"{REFERENCE_USAGE}"
    )


def is_moderation_block(exc: Exception) -> bool:
    message = str(exc).lower()
    return "moderation_blocked" in message or "safety system" in message


def prepare_upload_image(image_path: Path, max_dim: int) -> tuple[Path, bool]:
    if max_dim <= 0:
        return image_path, False

    try:
        from PIL import Image
    except ImportError:
        die("Pillow is required for reference downscaling. Install it with `uv pip install pillow`.")

    import tempfile

    with Image.open(image_path) as img:
        if max(img.size) <= max_dim:
            return image_path, False
        working = img.convert("RGBA") if img.mode not in {"RGB", "RGBA"} else img.copy()
        working.thumbnail((max_dim, max_dim), Image.Resampling.LANCZOS)

        tmp = tempfile.NamedTemporaryFile(delete=False, suffix=".png")
        tmp_path = Path(tmp.name)
        try:
            working.save(tmp, format="PNG", optimize=True)
        finally:
            tmp.close()
    return tmp_path, True


def generate_card_image(
    client,
    prompt: str,
    reference_paths: list[Path],
    size: str,
    quality: str,
    out_path: Path,
    model: str,
    upload_max_dim: int,
) -> None:
    upload_entries = [prepare_upload_image(p, upload_max_dim) for p in reference_paths]
    handles = []
    try:
        handles = [entry[0].open("rb") for entry in upload_entries]
        image_arg = handles[0] if len(handles) == 1 else handles
        result = client.images.edit(
            model=model,
            image=image_arg,
            prompt=prompt,
            size=size,
            quality=quality,
            output_format="png",
        )
    finally:
        for h in handles:
            h.close()
        for upload_path, is_temp in upload_entries:
            if is_temp and upload_path.exists():
                upload_path.unlink()

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
    prompt = build_prompt(args.prompt, card_name)

    if args.dry_run:
        print("gpt-image-2 single-call card generation dry-run")
        print(f"reference_root={reference_root}")
        for index, reference_path in enumerate(reference_paths, start=1):
            print(f"reference_{index}={reference_path}")
        print(f"out={out_path}")
        print(f"model={args.model}")
        print(f"size={args.size}  quality={args.quality}")
        print(f"upload_max_dim={args.upload_max_dim}")
        print("prompt=")
        print(prompt)
        return 0

    client = create_client()
    out_path.parent.mkdir(parents=True, exist_ok=True)

    print("Calling OpenAI gpt-image-2 images.edit API...", file=sys.stderr)
    started = time.time()
    try:
        generate_card_image(
            client, prompt, reference_paths, args.size, args.quality, out_path, args.model, args.upload_max_dim
        )
    except Exception as exc:
        if not is_moderation_block(exc):
            raise
        die(f"OpenAI safety block hit: {exc}")

    elapsed = time.time() - started
    print(f"Generation completed in {elapsed:.1f}s.", file=sys.stderr)
    print(f"Wrote {out_path}")
    print("Reference images used:")
    for reference_path in reference_paths:
        print(f"- {reference_path}")
    print("Reference format: uploaded as binary file handles via images.edit (downscaled for upload).")
    if args.card_name:
        print(f"Card name: {humanize_card_name(args.card_name)}")
    print("Prompt used:")
    print(prompt)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
