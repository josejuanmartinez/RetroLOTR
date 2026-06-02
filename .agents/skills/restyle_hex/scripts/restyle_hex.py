#!/usr/bin/env python3
"""Restyle a RetroLOTR hex tile using gpt-image-2 image editing."""

from __future__ import annotations

import argparse
import base64
import os
import sys
import time
from pathlib import Path


DEFAULT_MODEL = "gpt-image-2"
DEFAULT_SIZE = "1024x1024"
DEFAULT_QUALITY = "low"
DEFAULT_OUTPUT_FORMAT = "png"
DEFAULT_UPLOAD_MAX_DIM = 512
MAX_IMAGE_BYTES = 50 * 1024 * 1024

# $8/M image input, $8/M text input, $30/M output  (gpt-image-2 rates)
COST_RATES = {
    "image_input": 8.0 / 1_000_000,
    "text_input":  8.0 / 1_000_000,
    "output":     30.0 / 1_000_000,
}

DEFAULT_PROMPT = (
    "Can you change the style of this image, keeping exactly as it is, but with "
    "another style: d&d random bakshi conan lotr merpg meccg style without changing "
    "at all the shape, elements, or anything else - just style"
)


def die(message: str, code: int = 1) -> None:
    print(f"Error: {message}", file=sys.stderr)
    raise SystemExit(code)


def ensure_api_key(dry_run: bool = False) -> None:
    if os.getenv("OPENAI_API_KEY"):
        return
    if dry_run:
        print("Warning: OPENAI_API_KEY is not set; dry-run only.", file=sys.stderr)
        return
    die("OPENAI_API_KEY is not set. Export it before running.")


def calc_cost(usage) -> float:
    details = usage.input_tokens_details
    return (
        details.image_tokens * COST_RATES["image_input"]
        + details.text_tokens * COST_RATES["text_input"]
        + usage.output_tokens * COST_RATES["output"]
    )


def prepare_upload_image(image_path: Path, max_dim: int) -> tuple[Path, bool]:
    if max_dim <= 0:
        return image_path, False
    try:
        from PIL import Image
    except ImportError:
        die("Pillow is required. Install it with `uv pip install pillow`.")

    with Image.open(image_path) as img:
        if max(img.size) <= max_dim:
            return image_path, False
        import tempfile
        working = img.convert("RGBA") if img.mode not in {"RGB", "RGBA"} else img.copy()
        working.thumbnail((max_dim, max_dim))
        tmp = tempfile.NamedTemporaryFile(delete=False, suffix=".png")
        tmp_path = Path(tmp.name)
        try:
            working.save(tmp, format="PNG")
        finally:
            tmp.close()
    return tmp_path, True


def restore_alpha(source_path: Path, out_path: Path) -> None:
    from PIL import Image
    with Image.open(source_path) as src:
        alpha = src.convert("RGBA").getchannel("A")
    with Image.open(out_path) as out_img:
        rgba = out_img.convert("RGBA")
        if rgba.size != alpha.size:
            alpha = alpha.resize(rgba.size, Image.Resampling.LANCZOS)
        rgba.putalpha(alpha)
        rgba.save(out_path, format="PNG")


def create_client():
    try:
        from openai import OpenAI
    except ImportError:
        die("openai SDK not installed. Install it with `uv pip install openai`.")
    return OpenAI()


def restyle_one(
    image_path: Path,
    out_path: Path,
    *,
    prompt: str = DEFAULT_PROMPT,
    model: str = DEFAULT_MODEL,
    size: str = DEFAULT_SIZE,
    quality: str = DEFAULT_QUALITY,
    output_format: str = DEFAULT_OUTPUT_FORMAT,
    upload_max_dim: int = DEFAULT_UPLOAD_MAX_DIM,
    force: bool = False,
) -> tuple[int, float]:
    """Restyle one tile. Returns (exit_code, cost_usd). exit_code 2 = moderation skip."""
    if not image_path.exists():
        print(f"Error: Image not found: {image_path}", file=sys.stderr)
        return 1, 0.0
    if image_path.stat().st_size > MAX_IMAGE_BYTES:
        print(f"Error: Image exceeds 50MB: {image_path}", file=sys.stderr)
        return 1, 0.0
    if out_path.suffix == "":
        out_path = out_path.with_suffix("." + output_format)
    if out_path.exists() and not force:
        print(f"Error: Output exists: {out_path} (use force=True to overwrite)", file=sys.stderr)
        return 1, 0.0

    upload_path, upload_is_temp = prepare_upload_image(image_path, upload_max_dim)
    client = create_client()
    out_path.parent.mkdir(parents=True, exist_ok=True)

    started = time.time()
    last_exc = None
    for attempt in range(1, 4):  # up to 3 attempts
        try:
            with upload_path.open("rb") as f:
                result = client.images.edit(
                    model=model,
                    image=f,
                    prompt=prompt,
                    size=size,
                    quality=quality,
                    output_format=output_format,
                    extra_body={"moderation": "low"},
                )
            last_exc = None
            break
        except Exception as exc:
            msg = str(exc).lower()
            if "moderation_blocked" in msg or "safety system" in msg:
                last_exc = exc
                print(f"  Moderation block (attempt {attempt}/3): {image_path.name}", file=sys.stderr)
                time.sleep(2)
                continue
            if upload_is_temp and upload_path.exists():
                upload_path.unlink()
            raise
    else:
        if upload_is_temp and upload_path.exists():
            upload_path.unlink()
        print(f"  SKIPPED after 3 moderation blocks: {image_path.name}", file=sys.stderr)
        return 2, 0.0

    if upload_is_temp and upload_path.exists():
        upload_path.unlink()

    elapsed = time.time() - started
    cost = calc_cost(result.usage) if hasattr(result, "usage") and result.usage else 0.0

    if not result.data:
        print("Error: OpenAI returned no image data.", file=sys.stderr)
        return 1, cost
    image_b64 = result.data[0].b64_json
    if not image_b64:
        print("Error: No b64_json in response.", file=sys.stderr)
        return 1, cost

    out_path.write_bytes(base64.b64decode(image_b64))

    print(f"  {image_path.name} -> {out_path.name}  ({elapsed:.1f}s)  ${cost:.4f}", flush=True)
    return 0, cost


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Restyle a hex tile with gpt-image-2")
    parser.add_argument("--image", required=True)
    parser.add_argument("--out", required=True)
    parser.add_argument("--prompt", default=DEFAULT_PROMPT)
    parser.add_argument("--model", default=DEFAULT_MODEL)
    parser.add_argument("--size", default=DEFAULT_SIZE)
    parser.add_argument("--quality", default=DEFAULT_QUALITY)
    parser.add_argument("--output-format", default=DEFAULT_OUTPUT_FORMAT)
    parser.add_argument("--upload-max-dim", type=int, default=DEFAULT_UPLOAD_MAX_DIM)
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--force", action="store_true")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    ensure_api_key(args.dry_run)

    image_path = Path(args.image)
    out_path = Path(args.out)

    if args.dry_run:
        upload_path, upload_is_temp = prepare_upload_image(image_path, args.upload_max_dim)
        print("restyle_hex dry-run")
        print(f"image={image_path}")
        print(f"upload_image={upload_path}")
        print(f"out={out_path}")
        print(f"model={args.model}  quality={args.quality}  size={args.size}")
        print(f"prompt={args.prompt}")
        if upload_is_temp and upload_path.exists():
            upload_path.unlink()
        return 0

    code, _ = restyle_one(
        image_path, out_path,
        prompt=args.prompt,
        model=args.model,
        size=args.size,
        quality=args.quality,
        output_format=args.output_format,
        upload_max_dim=args.upload_max_dim,
        force=args.force,
    )
    return code


if __name__ == "__main__":
    raise SystemExit(main())
