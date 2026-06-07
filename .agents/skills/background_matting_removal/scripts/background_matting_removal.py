#!/usr/bin/env python3
"""Remove background from an image using Gemini dual-background matting."""

from __future__ import annotations

import argparse
import os
import sys
import tempfile
import time
from io import BytesIO
from pathlib import Path


DEFAULT_MODEL = "gemini-2.5-flash-image"
DEFAULT_UPLOAD_MAX_DIM = 0
DEFAULT_IMAGE_SIZE = "4K"
MAX_IMAGE_BYTES = 50 * 1024 * 1024

BLACK_BG_PROMPT = (
    "Change only the background of this image to a perfectly flat, solid pure black (#000000). "
    "The foreground subject must remain completely unchanged — same colors, same position, same "
    "level of detail. Do not alter the subject at all. The background must be pure solid black "
    "with no gradients, no shadows, and no vignette. Keep hard edges wherever the subject meets "
    "the background."
)

WHITE_BG_PROMPT = (
    "Change only the background of this image to a perfectly flat, solid pure white (#ffffff). "
    "The foreground subject must remain completely unchanged — same colors, same position, same "
    "level of detail. Do not alter the subject at all. The background must be pure solid white "
    "with no gradients, no shadows, and no vignette. Keep hard edges wherever the subject meets "
    "the background."
)


def die(message: str, code: int = 1) -> None:
    print(f"Error: {message}", file=sys.stderr)
    raise SystemExit(code)


def ensure_api_key(dry_run: bool) -> None:
    if os.getenv("GEMINI_API_KEY"):
        return
    if dry_run:
        print("Warning: GEMINI_API_KEY is not set; dry-run only.", file=sys.stderr)
        return
    die("GEMINI_API_KEY is not set. Export it before running.")


def create_client():
    try:
        from google import genai
    except ImportError:
        die("google-genai SDK not installed. Install with `uv pip install --system google-genai`.")
    return genai.Client(api_key=os.environ["GEMINI_API_KEY"])


def parse_args() -> argparse.Namespace:
    p = argparse.ArgumentParser(
        description="Remove background via Gemini black/white matting",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=(
            "Algorithm:\n"
            "  1. Edit input image with black background prompt  -> black composite\n"
            "  2. Edit input image with white background prompt  -> white composite\n"
            "  3. For each pixel: alpha = 1 - (white - black)   (per-channel, averaged)\n"
            "     fg_color = black_composite / alpha\n"
            "  4. Output RGBA PNG with computed alpha matte\n"
        ),
    )
    p.add_argument("--image", required=True, help="Input image path")
    p.add_argument("--out", required=True, help="Output PNG path (will have transparency)")
    p.add_argument("--model", default=DEFAULT_MODEL, help=f"Gemini model (default: {DEFAULT_MODEL})")
    p.add_argument(
        "--upload-max-dim",
        type=int,
        default=DEFAULT_UPLOAD_MAX_DIM,
        help="Max pixel dimension when uploading to Gemini. Use 0 for original size (default).",
    )
    p.add_argument(
        "--image-size",
        default=DEFAULT_IMAGE_SIZE,
        choices=["1K", "2K", "4K"],
        help=f"Output resolution from Gemini (default: {DEFAULT_IMAGE_SIZE}).",
    )
    p.add_argument(
        "--feather",
        type=int,
        default=0,
        help="Gaussian blur radius for softening alpha edges (0 = disabled).",
    )
    p.add_argument(
        "--alpha-cutoff",
        type=float,
        default=0.15,
        help="Alpha values below this are clamped to 0 (removes background bleed noise). Default: 0.15.",
    )
    p.add_argument(
        "--keep-intermediates",
        action="store_true",
        help="Save the black-bg and white-bg composites alongside the output.",
    )
    p.add_argument("--dry-run", action="store_true")
    p.add_argument("--force", action="store_true", help="Overwrite output if it already exists.")
    return p.parse_args()


def prepare_upload_image(image_path: Path, max_dim: int) -> tuple[bytes, str]:
    """Return (image_bytes, mime_type), downscaling if needed."""
    try:
        from PIL import Image
    except ImportError:
        die("Pillow is required. Install with `uv pip install --system pillow`.")

    with Image.open(image_path) as img:
        mime = "image/png"
        if max_dim > 0 and max(img.size) > max_dim:
            working = img.convert("RGBA")
            working.thumbnail((max_dim, max_dim), Image.Resampling.LANCZOS)
        else:
            working = img.convert("RGBA")

        buf = BytesIO()
        working.save(buf, format="PNG")
        return buf.getvalue(), mime


def call_edit(client, image_bytes: bytes, mime: str, prompt: str, model: str, image_size: str, label: str) -> bytes:
    """Call Gemini image edit and return raw PNG bytes."""
    try:
        from google.genai import types
    except ImportError:
        die("google-genai SDK not installed.")

    print(f"  [{label}] Calling Gemini image edit (size={image_size})...", file=sys.stderr)
    started = time.time()

    response = client.models.generate_content(
        model=model,
        contents=[
            types.Part.from_bytes(data=image_bytes, mime_type=mime),
            types.Part.from_text(text=prompt),
        ],
        config=types.GenerateContentConfig(
            response_modalities=["IMAGE", "TEXT"],
            image_config=types.ImageConfig(image_size=image_size),
        ),
    )

    elapsed = time.time() - started
    print(f"  [{label}] Done in {elapsed:.1f}s.", file=sys.stderr)

    for part in response.candidates[0].content.parts:
        if part.inline_data is not None:
            return part.inline_data.data

    die(f"Gemini returned no image data for {label}.")


def compute_matte(black_bytes: bytes, white_bytes: bytes):
    """
    Compute foreground alpha matte from black-bg and white-bg composites.

    Derivation (per channel, values normalized 0-1):
        composite_black = alpha * fg
        composite_white = alpha * fg + (1 - alpha)
        => white - black = 1 - alpha
        => alpha = 1 - (white - black)
        => fg = composite_black / alpha
    """
    try:
        import numpy as np
        from PIL import Image
    except ImportError:
        die("Pillow and numpy are required. Install with `uv pip install --system pillow numpy`.")

    black_img = Image.open(BytesIO(black_bytes)).convert("RGB")
    white_img = Image.open(BytesIO(white_bytes)).convert("RGB")

    if black_img.size != white_img.size:
        white_img = white_img.resize(black_img.size, Image.Resampling.LANCZOS)

    black = np.array(black_img, dtype=np.float32) / 255.0
    white = np.array(white_img, dtype=np.float32) / 255.0

    alpha_per_channel = np.clip(1.0 - (white - black), 0.0, 1.0)
    alpha = np.mean(alpha_per_channel, axis=2)

    safe_alpha = np.where(alpha > 1e-4, alpha, 1.0)
    fg = np.clip(black / safe_alpha[..., np.newaxis], 0.0, 1.0)
    fg[alpha <= 1e-4] = 0.0

    out = np.zeros((*black.shape[:2], 4), dtype=np.uint8)
    out[..., :3] = (fg * 255).round().astype(np.uint8)
    out[..., 3] = (alpha * 255).round().astype(np.uint8)

    return Image.fromarray(out, mode="RGBA")


def apply_alpha_cutoff(image, cutoff: float):
    """Snap alpha values below cutoff to 0 to eliminate background bleed noise."""
    try:
        import numpy as np
        from PIL import Image
    except ImportError:
        die("Pillow and numpy are required.")
    arr = np.array(image, dtype=np.float32)
    alpha = arr[..., 3] / 255.0
    alpha[alpha < cutoff] = 0.0
    arr[..., 3] = (alpha * 255).round().astype(np.uint8)
    # Zero out fg color on fully transparent pixels to avoid artifacts
    mask = arr[..., 3] == 0
    arr[mask, :3] = 0
    return Image.fromarray(arr.astype(np.uint8), mode="RGBA")


def feather_alpha(image, radius: int):
    """Gaussian-blur the alpha channel to soften matte edges."""
    try:
        from PIL import Image, ImageFilter
    except ImportError:
        die("Pillow is required for feathering.")
    r, g, b, a = image.split()
    a = a.filter(ImageFilter.GaussianBlur(radius=radius))
    return Image.merge("RGBA", (r, g, b, a))


def main() -> int:
    args = parse_args()
    ensure_api_key(args.dry_run)

    image_path = Path(args.image)
    if not image_path.exists():
        die(f"Image file not found: {image_path}")
    if image_path.stat().st_size > MAX_IMAGE_BYTES:
        die(f"Image exceeds 50MB limit: {image_path}")

    out_path = Path(args.out)
    if out_path.suffix.lower() != ".png":
        die("Output must be a .png file — transparency requires PNG.")
    if out_path.exists() and not args.force:
        die(f"Output already exists: {out_path} (use --force to overwrite)")

    image_bytes, mime = prepare_upload_image(image_path, args.upload_max_dim)

    if args.dry_run:
        print("Dry-run: background matting removal (Gemini)")
        print(f"  image         = {image_path}")
        print(f"  upload_bytes  = {len(image_bytes)}")
        print(f"  model         = {args.model}")
        print(f"  image_size    = {args.image_size}")
        print(f"  out           = {out_path}")
        print(f"  feather       = {args.feather}")
        print(f"  keep_interm.  = {args.keep_intermediates}")
        return 0

    client = create_client()
    out_path.parent.mkdir(parents=True, exist_ok=True)

    black_bytes = call_edit(client, image_bytes, mime, BLACK_BG_PROMPT, args.model, args.image_size, "black-bg")
    white_bytes = call_edit(client, image_bytes, mime, WHITE_BG_PROMPT, args.model, args.image_size, "white-bg")

    if args.keep_intermediates:
        black_path = out_path.with_stem(out_path.stem + "_black_bg")
        white_path = out_path.with_stem(out_path.stem + "_white_bg")
        black_path.write_bytes(black_bytes)
        white_path.write_bytes(white_bytes)
        print(f"Saved intermediate: {black_path}")
        print(f"Saved intermediate: {white_path}")

    print("Computing alpha matte...", file=sys.stderr)
    result_img = compute_matte(black_bytes, white_bytes)

    if args.alpha_cutoff > 0:
        result_img = apply_alpha_cutoff(result_img, args.alpha_cutoff)

    if args.feather > 0:
        result_img = feather_alpha(result_img, args.feather)

    result_img.save(out_path, format="PNG", optimize=True)
    print(f"Wrote {out_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
