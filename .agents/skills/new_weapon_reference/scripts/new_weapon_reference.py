#!/usr/bin/env python3
"""Generate a front-facing, vertical, full-length weapon reference image in a single gpt-image-2 edit call.

Same single-call approach as new_character_tpose (RetroLOTR already ships
enough character card art to anchor style directly, so there's no need for a
separate sketch/colorize round-trip): 3 randomly-selected character card
references plus a full-color prompt go straight into one `images.edit` call.

gpt-image-2's `images.edit` rejects `background="transparent"` outright (a
live 400 error, not a soft-ignore), so this asks the model for a flat
chroma-key color background instead and always converts that to real alpha
via flood-fill keying after generation (see `ensure_transparent_background`,
copied verbatim from new_character_tpose - same technique as
restyle_hex/trim_hex.py).
"""

from __future__ import annotations

import argparse
import base64
import os
import random
import re
import sys
import time
from collections import Counter, deque
from pathlib import Path


DEFAULT_MODEL = "gpt-image-2"
DEFAULT_SIZE = "576x1536"
DEFAULT_QUALITY = "low"
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
DEFAULT_OUT_DIR = REPO_ROOT / "Assets" / "Art" / "Weapons" / "References"
DEFAULT_REFERENCE_ROOT = REPO_ROOT / "Assets" / "Art" / "Cards" / "Characters"
BACKGROUND_KEY_TOLERANCE = 30.0
# Anti-aliased edge pixels blend between the weapon's ink outline and the
# flat chroma-key color, landing just outside the color-match tolerance —
# left alone they survive as a visible magenta fringe around the silhouette.
# Growing the background mask by a couple of pixels before keying removes
# that ring along with the flat background.
EDGE_DILATE_PIXELS = 2

WEAPON_ORIENTATION = (
    "The weapon is shown completely alone, with no character, hand, arm, or "
    "gloved grip holding it, and no stand, mount, or scenery. "
    "It is oriented perfectly VERTICAL and UPRIGHT: its long axis runs "
    "straight up and down the center of the frame — a blade, haft, shaft, or "
    "stave points straight up, with the hilt, grip, pommel, or base at the "
    "bottom. It must not lean, tilt, or angle even slightly; the weapon's "
    "centerline is a perfectly vertical line down the middle of the image. "
    "The camera view is a strict FRONT-FACING elevation view, like a "
    "product/reference-sheet shot: viewed directly from the front with zero "
    "perspective distortion or three-quarter angle, so the flattest, most "
    "identifying silhouette of the weapon reads clearly (e.g. a sword's "
    "blade shown flat-on, not edge-on; a bow shown with the string facing "
    "the viewer). If the weapon has an asymmetric guard, crossguard, or "
    "off-axis part, keep that orientation consistent with a true front view "
    "rather than rotating it to a more flattering angle. "
    "The ENTIRE weapon, from its topmost point to its bottommost point, must "
    "be fully visible within the frame, not cropped, centered horizontally, "
    "with even empty margin on both sides and a small margin above and "
    "below — this is a full-length reference, so the weapon should occupy "
    "as much of the vertical extent of the frame as possible without "
    "touching the edges."
)

CHROMA_KEY_COLOR = "magenta/pink (#FF00FF)"

BACKGROUND_REQUIREMENT = (
    f"CRITICAL: the background MUST be a completely flat, uniform {CHROMA_KEY_COLOR} "
    "chroma-key color — solid and even across the entire background area, with no "
    "gradient, no texture, no vignette, no scenery, and no drop shadow behind the "
    "weapon. This exact color must not appear anywhere on the weapon itself "
    "(blade, metal, wood, leather, gems, or any other material), only in the "
    "background, because it will be programmatically removed and replaced with "
    "transparency after generation. Only the weapon should be rendered in "
    "natural materials and colors; everything else in frame must be the flat "
    "chroma-key color."
)

STYLE_DIRECTION = (
    "Render it in a late-1970s hand-painted cel-animation fantasy style like "
    "vintage animated Lord of the Rings: simplified hand-drawn shapes, "
    "bold dark ink outlines, flat-to-soft cel shading, painterly "
    "watercolor-like texture, moody magical lighting, and a retro "
    "illustrated fantasy atmosphere. Make it feel like an old animated "
    "fantasy prop reference/model sheet, not realistic modern concept art, "
    "not glossy, not photoreal, not 3D, and not anime. Avoid AI-generated "
    "mistakes such as warped proportions, duplicated parts, or melted "
    "detail. Avoid a flat sepia or uniformly brown color cast; use rich, "
    "material-appropriate colors (steel, bronze, aged wood, leather, gems, "
    "etc) as appropriate to the weapon."
)

REFERENCE_USAGE = (
    "The uploaded images are existing RetroLOTR character cards, provided "
    "only as style, palette, and paint-technique references — match their "
    "hand-painted look, ink linework, and color treatment. Do NOT copy their "
    "subjects, characters, poses, or compositions; this is a single isolated "
    "weapon rendered in the strict front-facing vertical layout described "
    "below, not a character illustration."
)

GENERATE_PROMPT = (
    "Create a tall portrait-format weapon reference illustration for a "
    "RetroLOTR game weapon.\n"
    f"{WEAPON_ORIENTATION}\n"
    f"{BACKGROUND_REQUIREMENT}\n"
    f"{STYLE_DIRECTION}\n"
    f"{REFERENCE_USAGE}\n"
    "No modern UI elements, no text overlays, no logos, no card frame, no "
    "white border, no watermarks, no character or hand holding the weapon, "
    "no scenery or props in the background — only the flat chroma-key color."
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
        die("size must be WIDTHxHEIGHT (e.g. 576x1536) or auto.")
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


def humanize_name(name: str) -> str:
    cleaned = re.sub(r"(?<!^)([A-Z])", r" \1", name).replace("_", " ").replace("-", " ").strip()
    return " ".join(token[:1].upper() + token[1:] if token else token for token in cleaned.split())


def build_output_path(name: str, out: str | None) -> Path:
    if out:
        out_path = Path(out)
        if not out_path.is_absolute():
            out_path = REPO_ROOT / out_path
        return out_path if out_path.suffix else out_path.with_suffix(".png")
    safe_name = re.sub(r"[^A-Za-z0-9_]+", "", name.replace(" ", ""))
    return DEFAULT_OUT_DIR / f"{safe_name}.png"


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Generate a front-facing, vertical, full-length weapon reference image in one gpt-image-2 edit call"
    )
    parser.add_argument("--name", required=True, help="Weapon name")
    parser.add_argument(
        "--description",
        required=True,
        help="Weapon description: type, material, era/culture, ornamentation, notable visual traits",
    )
    parser.add_argument("--out", help="Path to write the generated image (default: Assets/Art/Weapons/References/<Name>.png)")
    parser.add_argument("--model", default=DEFAULT_MODEL)
    parser.add_argument("--size", default=DEFAULT_SIZE)
    parser.add_argument("--quality", default=DEFAULT_QUALITY, help="gpt-image-2 quality: low, medium, high, or auto")
    parser.add_argument(
        "--reference-root",
        default=str(DEFAULT_REFERENCE_ROOT),
        help="Root folder to sample shipped character card references from (style/art-direction anchors)",
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


def list_card_reference_candidates(reference_root: Path) -> list[Path]:
    if not reference_root.exists():
        die(f"Reference root not found: {reference_root}")

    candidates: list[Path] = []
    for path in reference_root.rglob("*"):
        if not path.is_file():
            continue
        if path.suffix.lower() not in ALLOWED_EXTENSIONS:
            continue
        if any(path.name.startswith(prefix) for prefix in EXCLUDED_PREFIXES):
            continue
        candidates.append(path)

    return candidates


def choose_references(reference_root: Path, count: int) -> list[Path]:
    candidates = list_card_reference_candidates(reference_root)
    if len(candidates) < count:
        die(f"Need at least {count} shipped character card images under {reference_root}; found {len(candidates)}.")
    return random.sample(candidates, count)


def build_prompt(name: str, description: str) -> str:
    header = (
        f"Weapon name: {humanize_name(name)}.\n"
        f"Weapon description: {description.strip()}\n"
        "Ensure the final image clearly matches this named weapon and description.\n"
    )
    return f"{header}{GENERATE_PROMPT}"


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


def generate_weapon_image(
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


def detect_bg_color(arr):
    """Return the most common RGB color found along the image edges.

    Same technique as `restyle_hex/scripts/trim_hex.py` — dense corner sampling
    plus sparse edge sampling, then majority vote.
    """
    import numpy as np

    h, w = arr.shape[:2]
    samples: list[tuple] = []

    corner_r = min(5, h // 4, w // 4)
    for y in range(corner_r):
        for x in range(corner_r):
            for py, px in [(y, x), (y, w - 1 - x), (h - 1 - y, x), (h - 1 - y, w - 1 - x)]:
                samples.append(tuple(arr[py, px, :3].tolist()))

    step_x = max(1, w // 40)
    step_y = max(1, h // 40)
    for x in range(0, w, step_x):
        samples.append(tuple(arr[0, x, :3].tolist()))
        samples.append(tuple(arr[h - 1, x, :3].tolist()))
    for y in range(0, h, step_y):
        samples.append(tuple(arr[y, 0, :3].tolist()))
        samples.append(tuple(arr[y, w - 1, :3].tolist()))

    bg = Counter(samples).most_common(1)[0][0]
    return np.array(bg, dtype=np.float32)


def build_bg_mask(arr, bg_color, tolerance: float):
    """BFS flood-fill from all four corners.

    Returns a bool mask of pixels within `tolerance` of bg_color that are
    reachable from the image border without crossing non-background pixels —
    so background-colored patches inside the weapon silhouette survive.
    """
    import numpy as np

    h, w = arr.shape[:2]
    rgb = arr[:, :, :3].astype(np.float32)

    dist = np.sqrt(np.sum((rgb - bg_color) ** 2, axis=-1))
    candidate = dist <= tolerance

    visited = np.zeros((h, w), dtype=bool)
    queue: deque[tuple[int, int]] = deque()

    for sy, sx in [(0, 0), (0, w - 1), (h - 1, 0), (h - 1, w - 1)]:
        if candidate[sy, sx] and not visited[sy, sx]:
            visited[sy, sx] = True
            queue.append((sy, sx))

    while queue:
        y, x = queue.popleft()
        for dy, dx in ((-1, 0), (1, 0), (0, -1), (0, 1)):
            ny, nx = y + dy, x + dx
            if 0 <= ny < h and 0 <= nx < w and not visited[ny, nx] and candidate[ny, nx]:
                visited[ny, nx] = True
                queue.append((ny, nx))

    return visited


def dilate_mask(mask, iterations: int):
    """Grow a boolean mask outward by `iterations` pixels (4-neighbor dilation).

    Applied to the background mask before keying so the thin ring of
    anti-aliased edge pixels (blended between the weapon's ink outline and
    the flat chroma-key color — too far from pure chroma to match the color
    tolerance, but still visibly tinted) gets removed along with the flat
    background, instead of surviving as a magenta fringe around the figure.
    """
    import numpy as np

    grown = mask
    for _ in range(iterations):
        shifted = np.zeros_like(grown)
        shifted[1:, :] |= grown[:-1, :]
        shifted[:-1, :] |= grown[1:, :]
        shifted[:, 1:] |= grown[:, :-1]
        shifted[:, :-1] |= grown[:, 1:]
        grown = grown | shifted
    return grown


def border_alpha_stats(arr) -> tuple[float, float]:
    """Return (min_alpha, mean_alpha) sampled along the image border."""
    import numpy as np

    h, w = arr.shape[:2]
    alpha = arr[:, :, 3]
    border = np.concatenate([alpha[0, :], alpha[-1, :], alpha[:, 0], alpha[:, -1]])
    return float(border.min()), float(border.mean())


def ensure_transparent_background(image_path: Path, tolerance: float = BACKGROUND_KEY_TOLERANCE) -> str:
    """Guarantee the saved image has a transparent background.

    gpt-image-2's images.edit rejects `background="transparent"` outright, so
    the model is instead asked (via the prompt) to render a flat chroma-key
    color background. This function keys that color out to real alpha via
    the same flood-fill technique used by `restyle_hex/scripts/trim_hex.py`
    (no cropping — canvas size stays untouched so framing stays predictable).
    This is expected to run on every image, not just as a rare fallback.

    Returns one of: "native" (border was already transparent — only possible
    if a future API version honors transparency directly), "keyed" (chroma-key
    background removed), "failed" (removal would have erased the whole image;
    left untouched and the caller should warn the user).
    """
    from PIL import Image
    import numpy as np

    with Image.open(image_path) as img:
        rgba = img.convert("RGBA")
        arr = np.array(rgba)

    min_alpha, mean_alpha = border_alpha_stats(arr)
    if min_alpha < 250 or mean_alpha < 250:
        return "native"

    bg_color = detect_bg_color(arr)
    bg_mask = build_bg_mask(arr, bg_color, tolerance)

    if not bg_mask.any():
        return "failed"

    # Pockets of chroma-key color enclosed by the weapon's own silhouette
    # (e.g. inside a closed hilt loop or an ornamental cutout) aren't
    # reachable from the border by the flood-fill above, so key them out
    # directly by color match too — safe here because the RetroLOTR palette
    # never legitimately uses the chroma-key color.
    import numpy as np

    rgb = arr[:, :, :3].astype(np.float32)
    spot_mask = np.sqrt(np.sum((rgb - bg_color) ** 2, axis=-1)) <= tolerance
    bg_mask = bg_mask | spot_mask

    bg_mask = dilate_mask(bg_mask, EDGE_DILATE_PIXELS)

    candidate_arr = arr.copy()
    candidate_arr[bg_mask, 3] = 0

    opaque_fraction = float((candidate_arr[:, :, 3] > 0).mean())
    if opaque_fraction < 0.02:
        # Keying nuked almost the entire image — the color-distance guess was
        # wrong. Leave the file untouched rather than ship a blank sprite.
        return "failed"

    Image.fromarray(candidate_arr, "RGBA").save(image_path, format="PNG")
    return "keyed"


def main() -> int:
    args = parse_args()
    ensure_api_key(args.dry_run)
    validate_size(args.size)

    out_path = build_output_path(args.name, args.out)
    if out_path.exists() and not args.force:
        die(f"Output already exists: {out_path} (use --force to overwrite)")

    reference_root = Path(args.reference_root)
    if not reference_root.is_absolute():
        reference_root = REPO_ROOT / reference_root
    reference_paths = choose_references(reference_root, args.reference_count)
    prompt = build_prompt(args.name, args.description)

    if args.dry_run:
        print("gpt-image-2 single-call weapon reference generation dry-run")
        print(f"reference_root={reference_root}")
        for index, reference_path in enumerate(reference_paths, start=1):
            print(f"reference_{index}={reference_path}")
        print(f"out={out_path}")
        print(f"model={args.model}")
        print(f"size={args.size}  quality={args.quality}")
        print(f"background: model renders flat chroma-key ({CHROMA_KEY_COLOR}), then flood-fill keyed to alpha")
        print(f"upload_max_dim={args.upload_max_dim}")
        print("prompt=")
        print(prompt)
        return 0

    client = create_client()
    out_path.parent.mkdir(parents=True, exist_ok=True)

    print("Calling OpenAI gpt-image-2 images.edit API...", file=sys.stderr)
    started = time.time()
    try:
        generate_weapon_image(
            client, prompt, reference_paths, args.size, args.quality, out_path, args.model, args.upload_max_dim
        )
    except Exception as exc:
        if not is_moderation_block(exc):
            raise
        die(f"OpenAI safety block hit: {exc}")

    elapsed = time.time() - started
    print(f"Generation completed in {elapsed:.1f}s.", file=sys.stderr)
    print(f"Wrote {out_path}")

    bg_status = ensure_transparent_background(out_path)
    if bg_status == "native":
        print("Background: transparent as returned by the API (unexpected but fine).")
    elif bg_status == "keyed":
        print(
            "Background: chroma-key color removed via flood-fill alpha keying "
            "(same technique as restyle_hex/trim_hex.py) — gpt-image-2's images.edit "
            "does not support requesting transparency directly."
        )
    else:
        print(
            "WARNING: API returned an opaque background and automatic "
            "flood-fill keying was not confident enough to apply (would have "
            "erased most of the image). Background removal was skipped — "
            "review the image manually.",
            file=sys.stderr,
        )

    print("Reference images used:")
    for reference_path in reference_paths:
        print(f"- {reference_path}")
    print("Reference format: uploaded as binary file handles via images.edit (downscaled for upload).")
    print(f"Weapon name: {humanize_name(args.name)}")
    print(f"Weapon description: {args.description}")
    print("Prompt used:")
    print(prompt)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
