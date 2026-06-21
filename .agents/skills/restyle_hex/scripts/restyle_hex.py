#!/usr/bin/env python3
"""Restyle a RetroLOTR hex tile using gpt-image-2 image editing."""

from __future__ import annotations

import argparse
import base64
import os
import re
import sys
import time
from pathlib import Path


DEFAULT_MODEL = "gpt-image-2"
DEFAULT_SIZE = "1024x1024"
DEFAULT_QUALITY = "low"
DEFAULT_OUTPUT_FORMAT = "png"
DEFAULT_UPLOAD_MAX_DIM = 512
DEFAULT_BOTTOM_PADDING_FRACTION = 0.20  # add 20% transparent height at bottom before upload
MAX_IMAGE_BYTES = 50 * 1024 * 1024

# Bundled style-anchor tiles shipped with the skill. Sent to images.edit after the
# source tile so the model copies their muted, hand-painted look instead of drifting
# bright/cartoonish. Override with --style-ref / --no-style-ref.
DEFAULT_STYLE_REFS_DIR = Path(__file__).resolve().parent.parent / "style_refs"
MAX_STYLE_REFS = 3  # cap to keep upload size and cost down

# Generic tile names: pure digits/spaces (001, "002 3") or any hex-prefixed name (hexDirtCastle00)
_GENERIC_NAME_RE = re.compile(r'^hex|^[\d\s]+$', re.IGNORECASE)

# $8/M image input, $8/M text input, $30/M output  (gpt-image-2 rates)
COST_RATES = {
    "image_input": 8.0 / 1_000_000,
    "text_input":  8.0 / 1_000_000,
    "output":     30.0 / 1_000_000,
}

DEFAULT_PROMPT = (
    "Restyle this hex tile in the aesthetic of classic fantasy illustration: "
    "d&d, Bakshi, Conan, LOTR, MERPG, MECCG. "
    "The art style must be isometric 2D — flat illustrated elements viewed from a fixed isometric angle, "
    "like classic tabletop hex map tiles. "
    "MANDATORY: preserve the exact hexagon shape of the tile with the point at the BOTTOM (pointy-top orientation) — never rotate, distort, or change the hex to flat-bottom. "
    "Keep the hex shape and overall composition, but you are free to redesign the interior elements "
    "— terrain, structures, iconography, colors — to better evoke a Middle-earth feeling. "
    "CRITICAL — 2.5D HEIGHT RULE: mountains, peaks, towers, spires, castles, and tall buildings MUST visibly rise upward "
    "and their tops MUST clearly protrude above the top edge of the hex — not just touching it, but genuinely extending beyond it. "
    "Think of classic MECCG/MERPG hex art where mountain peaks tower well above the hex frame. "
    "NEVER draw these elements flat or compressed inside the hex boundary — they must have real, imposing vertical presence. "
    "Only flat terrain (rivers, fields, roads, low forests) stays clipped within the hex boundary. "
    "PALETTE — CRITICAL: use a muted, desaturated, earth-toned palette (mossy greens, ochres, browns, slate greys, faded blues). "
    "The look must be a matte, hand-painted printed tabletop tile — NOT bright, NOT neon, NOT glossy, NOT cartoonish, NOT a video-game render. "
    "Avoid vivid primary colors and high-saturation lighting; keep tones subdued and slightly aged. "
    "Do not add any text, labels, or lettering anywhere in the image. "
    "Do not draw blood anywhere — if the scene calls for it, substitute water, mud, dark stone, or another thematically fitting element instead. "
    "CRITICAL: the area outside the hex shape must be SOLID BLACK and nothing else — no gradients, no textures, no scenery, no sky, no ground. Solid black only. "
    "Leave visible background space below the hex shape — the hex must not touch or bleed into the bottom edge."
)


# Faithful restyle: change ONLY the art style, never add or redesign elements.
STYLE_ONLY_PROMPT = (
    "Aggressively restyle this hex tile into the aesthetic of classic hand-painted fantasy illustration: "
    "d&d, Bakshi, Conan, LOTR, MERPG, MECCG. "
    "The style change must be BOLD and clearly visible — strong inked outlines, hand-painted brush textures, "
    "richer earthy hand-mixed tones, and the printed look of a classic tabletop hex map tile. "
    "Do not produce a near-copy of the source; the source is only a reference for shapes and layout — the rendering must look distinctly re-illustrated. "
    "The art style must be isometric 2D — flat illustrated elements viewed from a fixed isometric angle, "
    "like classic tabletop hex map tiles. "
    "MANDATORY — FAITHFUL RESTYLE ONLY: keep every element exactly where it already is. "
    "Do NOT add any new features — no extra towers, fortresses, houses, standing stones, rivers, bridges, "
    "structures, figures, vegetation, or iconography that are not already present in the source image. "
    "Do NOT remove, relocate, or redesign any existing element. "
    "Preserve the exact shapes, positions, count, and composition of everything in the tile — only the visual style changes. "
    "MANDATORY: preserve the exact hexagon shape of the tile with the point at the BOTTOM (pointy-top orientation) — never rotate, distort, or change the hex to flat-bottom. "
    "If a tall element (mountain, tower, spire, castle) is already present and already protrudes above the hex, keep it protruding; "
    "do not change which elements rise above the hex edge. "
    "PALETTE — CRITICAL: use a muted, desaturated, earth-toned palette (mossy greens, ochres, browns, slate greys, faded blues). "
    "The look must be a matte, hand-painted printed tabletop tile — NOT bright, NOT neon, NOT glossy, NOT cartoonish, NOT a video-game render. "
    "Avoid vivid primary colors and high-saturation lighting; keep tones subdued and slightly aged. "
    "Do not add any text, labels, or lettering anywhere in the image. "
    "Do not draw blood anywhere — if the scene calls for it, substitute water, mud, dark stone, or another thematically fitting element instead. "
    "CRITICAL: the area outside the hex shape must be SOLID BLACK and nothing else — no gradients, no textures, no scenery, no sky, no ground. Solid black only. "
    "Leave visible background space below the hex shape — the hex must not touch or bleed into the bottom edge."
)


# Prepended when one or more style-reference images are supplied. The reference
# images are appended after the source tile in the images.edit array.
STYLE_REF_PROMPT_PREFIX = (
    "The FIRST image is the tile to restyle. The IMAGE(S) AFTER IT are STYLE REFERENCES only: "
    "match their art style exactly — the same muted desaturated palette, inked outlines, matte hand-painted "
    "brushwork, and printed-tile rendering. "
    "Do NOT copy the shapes, terrain, structures, or layout of the reference images; copy ONLY their look and color treatment. "
    "Apply that reference style to the content of the first image. "
)


# Content-based thematic overrides keyed by regex -> theme instruction
_CONTENT_THEMES: list[tuple[re.Pattern, str]] = [
    (
        re.compile(r'lava', re.IGNORECASE),
        "This is a lava tile — render it in a Mordor style: molten rock, dark ashen terrain, "
        "rivers of glowing lava, smoke and fire, oppressive red-orange light, "
        "volcanic crags and dark obsidian formations typical of the land of shadow.",
    ),
    (
        re.compile(r'pyramid', re.IGNORECASE),
        "This tile features pyramids — render them as Khand/Easterling pyramids: "
        "stepped or tiered desert stone structures with Eastern/Middle-earth Khand architectural motifs, "
        "warm sandstone tones, ornate carved surfaces, scorched desert surroundings.",
    ),
    (
        re.compile(r'desert|karagmir', re.IGNORECASE),
        "This is a desert or Khand/Easterling region tile — render it with Khand and Easterling thematic elements: "
        "arid sandy terrain, Eastern-style banners or totems, caravanserai ruins, "
        "scorched earth, sparse dry vegetation, warm ochre and terracotta palette.",
    ),
    (
        re.compile(r'snow|ice|arctic|tundra', re.IGNORECASE),
        "This is a snow or ice tile — render it with igloo camps or snowmen camps: "
        "small rounded igloo domes, bundled figures or snowmen, frost-covered ground, "
        "pale blue-white palette, cold wintry Middle-earth atmosphere.",
    ),
    (
        re.compile(r"eagle.?s?.?eyrie|eyrie", re.IGNORECASE),
        "This tile is the Eagle's Eyrie — a dramatic mountain peak hosting the realm of the Great Eagles of Middle-earth. "
        "Render it as a high rocky mountain summit with massive eagle nests built from thick branches and bones on the crags, "
        "one or more giant eagles perched or soaring with enormous wingspans, sharp cliff faces, strong winds suggested by "
        "swirling clouds or mist at the peak. Convey a sense of wild, ancient, unreachable height.",
    ),
    (
        re.compile(r'angmar|carn.?dum|gundabad|mt.?gram|gram', re.IGNORECASE),
        "This tile belongs to Angmar, realm of the Witch-king — render it in the dark, dread aesthetic of Angmar/Carn Dûm: "
        "jagged black iron towers and battlements, frozen tundra and glacial rock, perpetual grey overcast sky within the hex, "
        "sickly pale light, crow-black banners, bone and iron iconography, blighted landscape with dead trees and frost. "
        "Evoke the cold malice and ancient evil of the Witch-king's stronghold and the nearby peaks of Gundabad and Gram.",
    ),
    (
        re.compile(r'mountain|mt\.?|peak|summit|hill|tower|spire|castle|fortress|citadel|keep|city|town|stronghold', re.IGNORECASE),
        "This tile contains tall structures or elevated terrain — mountains, towers, spires, or similar. "
        "The mountain or tall structure MUST dramatically protrude above the top edge of the hex — its peak or top "
        "should sit well above the hex boundary, not just graze it. "
        "Imagine the hex as a frame the mountain bursts out of from the top. "
        "The base of the mountain sits inside the hex; the bulk and peak extend clearly beyond the top edge. "
        "Do NOT compress the mountain to fit inside the hex. Do NOT draw it as flat terrain.",
    ),
]


def content_theme_hint(stem: str) -> str | None:
    """Return a thematic instruction if the stem matches a known content keyword, else None."""
    key = stem.replace("_", " ").replace("-", " ")
    for pattern, hint in _CONTENT_THEMES:
        if pattern.search(key):
            return hint
    return None


def extract_place_name(stem: str) -> str | None:
    """Return a human-readable place name if the filename looks meaningful, else None.

    Generic names like '001', 'hex0A3F' return None.
    Names like 'Barad_Dur', 'Minas_Tirith', 'The_Shire' return the cleaned string.
    """
    if _GENERIC_NAME_RE.match(stem):
        return None
    cleaned = stem.replace("_", " ").replace("-", " ").strip()
    # Still generic if nothing alphabetic remains after cleaning
    if not re.search(r'[a-zA-Z]', cleaned):
        return None
    return cleaned


def terrain_label(stem: str) -> str | None:
    """Return a plain terrain/subject label from the filename, or None if generic.

    Strips a trailing index ('grass_17' -> 'grass'), splits camelCase
    ('deepWater' -> 'deep water'), and lowercases. Used in style-only mode to tell
    the model WHAT the tile depicts without granting it license to add/redesign
    elements. Returns None for purely numeric/hex names that carry no meaning.
    """
    # Drop trailing index like "_17", " 3", "01"
    base = re.sub(r'[\s_-]*\d+$', '', stem)
    # Split camelCase: deepWater -> deep Water
    base = re.sub(r'(?<=[a-z])(?=[A-Z])', ' ', base)
    base = base.replace("_", " ").replace("-", " ").strip().lower()
    if not base or not re.search(r'[a-z]', base):
        return None
    if base.startswith("hex"):  # generic hexXXXX names carry no subject
        return None
    return base


def build_prompt(base_prompt: str, stem: str, style_only: bool = False) -> str:
    """Return a tile-specific prompt, injecting content themes and/or place name.

    When ``style_only`` is True, no content-theme or place-name text is injected
    (those add or redesign features). Instead we inject only a neutral terrain
    label so the model knows WHAT it is restyling — without permission to change
    the elements.
    """
    if style_only:
        label = terrain_label(stem)
        if label:
            return (
                base_prompt
                + f" For context, this tile depicts '{label}' terrain — render that subject "
                f"convincingly and recognizably in the new art style, but you still must NOT add, "
                f"remove, move, or redesign any element; only the visual style changes."
            )
        return base_prompt

    prompt = base_prompt

    theme = content_theme_hint(stem)
    if theme:
        prompt += " " + theme

    place = extract_place_name(stem)
    if place:
        prompt += (
            f" This tile represents '{place}' from Middle-earth (Tolkien lore or fan lore). "
            "You have full creative freedom to reimagine the interior elements of the hex — "
            "replace or redesign the terrain, structures, symbols, and colors to authentically evoke the character, "
            "history, and atmosphere of this specific location. "
            "Draw from its lore: its peoples, architecture, landscape, and iconic imagery. "
        )

    if theme or place:
        prompt += (
            "All elements must remain isometric 2D — flat illustrated, fixed isometric viewpoint, no perspective distortion. "
            "The hex outline/silhouette must remain, and nothing may be added outside it. "
            "The background outside the hex must remain solid black."
        )

    return prompt


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


def prepare_upload_image(
    image_path: Path,
    max_dim: int,
    bottom_padding_fraction: float = DEFAULT_BOTTOM_PADDING_FRACTION,
) -> tuple[Path, bool]:
    try:
        from PIL import Image
    except ImportError:
        die("Pillow is required. Install it with `uv pip install pillow`.")

    import tempfile

    with Image.open(image_path) as img:
        working = img.convert("RGBA") if img.mode not in {"RGB", "RGBA"} else img.copy()

    needs_resize = max_dim > 0 and max(working.size) > max_dim
    if needs_resize:
        working.thumbnail((max_dim, max_dim))

    if bottom_padding_fraction > 0:
        w, h = working.size
        pad_h = max(1, int(h * bottom_padding_fraction))
        padded = Image.new("RGBA", (w, h + pad_h), (0, 0, 0, 255))
        padded.paste(working, (0, 0))
        working = padded

    needs_temp = needs_resize or bottom_padding_fraction > 0
    if not needs_temp:
        return image_path, False

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


def resolve_style_refs(
    refs: list[str] | None,
    use_default: bool,
    *,
    exclude: Path | None = None,
) -> list[Path]:
    """Return the style-reference image paths to send to images.edit.

    Explicit ``refs`` win. Otherwise, when ``use_default`` is True, fall back to
    every PNG in DEFAULT_STYLE_REFS_DIR. ``exclude`` drops a path whose resolved
    location matches the source tile (avoids feeding a tile its own output as a
    reference). Capped at MAX_STYLE_REFS.
    """
    paths: list[Path] = []
    if refs:
        paths = [Path(r) for r in refs]
    elif use_default and DEFAULT_STYLE_REFS_DIR.is_dir():
        paths = sorted(DEFAULT_STYLE_REFS_DIR.glob("*.png"))

    resolved: list[Path] = []
    exclude_r = exclude.resolve() if exclude else None
    for p in paths:
        if not p.exists():
            print(f"  Warning: style ref not found, skipping: {p}", file=sys.stderr)
            continue
        if exclude_r and p.resolve() == exclude_r:
            continue
        resolved.append(p)
    return resolved[:MAX_STYLE_REFS]


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
    bottom_padding_fraction: float = DEFAULT_BOTTOM_PADDING_FRACTION,
    style_refs: list[Path] | None = None,
    force: bool = False,
) -> tuple[int, float]:
    """Restyle one tile. Returns (exit_code, cost_usd). exit_code 2 = moderation skip.

    ``style_refs`` are extra images sent after the source tile in the images.edit
    array; the model copies their art style (palette, brushwork) without copying
    their content. The prompt should already include STYLE_REF_PROMPT_PREFIX.
    """
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

    upload_path, upload_is_temp = prepare_upload_image(image_path, upload_max_dim, bottom_padding_fraction)

    # Style refs: downscale for upload but no bottom padding (they are anchors, not the tile).
    ref_temps: list[tuple[Path, bool]] = []
    for ref in (style_refs or []):
        ref_temps.append(prepare_upload_image(ref, upload_max_dim, bottom_padding_fraction=0.0))

    def cleanup_temps() -> None:
        if upload_is_temp and upload_path.exists():
            upload_path.unlink()
        for rp, is_tmp in ref_temps:
            if is_tmp and rp.exists():
                rp.unlink()

    client = create_client()
    out_path.parent.mkdir(parents=True, exist_ok=True)

    started = time.time()
    last_exc = None
    for attempt in range(1, 4):  # up to 3 attempts
        handles = []
        try:
            handles.append(upload_path.open("rb"))
            handles.extend(rp.open("rb") for rp, _ in ref_temps)
            # Single image -> pass the file directly; multiple -> pass the list.
            image_arg = handles[0] if len(handles) == 1 else handles
            result = client.images.edit(
                model=model,
                image=image_arg,
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
            cleanup_temps()
            raise
        finally:
            for h in handles:
                h.close()
    else:
        cleanup_temps()
        print(f"  SKIPPED after 3 moderation blocks: {image_path.name}", file=sys.stderr)
        return 2, 0.0

    cleanup_temps()

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
    parser.add_argument("--prompt", default=None,
                        help="Override the base prompt (defaults to the standard or style-only prompt)")
    parser.add_argument("--style-only", action="store_true",
                        help="Faithful restyle: change only the art style, never add or redesign features "
                             "(towers, rivers, bridges, etc.)")
    parser.add_argument("--model", default=DEFAULT_MODEL)
    parser.add_argument("--size", default=DEFAULT_SIZE)
    parser.add_argument("--quality", default=DEFAULT_QUALITY)
    parser.add_argument("--output-format", default=DEFAULT_OUTPUT_FORMAT)
    parser.add_argument("--upload-max-dim", type=int, default=DEFAULT_UPLOAD_MAX_DIM)
    parser.add_argument("--bottom-padding-fraction", type=float, default=DEFAULT_BOTTOM_PADDING_FRACTION,
                        help="Fraction of image height to add as transparent bottom padding (default 0.20)")
    parser.add_argument("--style-ref", action="append", default=None, metavar="PATH",
                        help="Style-reference image sent after the source tile so the model copies its look "
                             "(repeatable). Overrides the bundled default refs.")
    parser.add_argument("--no-style-ref", action="store_true",
                        help="Disable the bundled default style references in style_refs/.")
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--force", action="store_true")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    ensure_api_key(args.dry_run)

    image_path = Path(args.image)
    out_path = Path(args.out)

    base_prompt = args.prompt
    if base_prompt is None:
        base_prompt = STYLE_ONLY_PROMPT if args.style_only else DEFAULT_PROMPT
    effective_prompt = build_prompt(base_prompt, image_path.stem, style_only=args.style_only)

    style_refs = resolve_style_refs(args.style_ref, use_default=not args.no_style_ref, exclude=image_path)
    if style_refs:
        effective_prompt = STYLE_REF_PROMPT_PREFIX + effective_prompt

    if args.dry_run:
        upload_path, upload_is_temp = prepare_upload_image(image_path, args.upload_max_dim, args.bottom_padding_fraction)
        print("restyle_hex dry-run")
        print(f"image={image_path}")
        print(f"upload_image={upload_path}")
        print(f"out={out_path}")
        print(f"model={args.model}  quality={args.quality}  size={args.size}")
        print(f"bottom_padding_fraction={args.bottom_padding_fraction}")
        print(f"style_refs={[str(p) for p in style_refs] or 'none'}")
        print(f"prompt={effective_prompt}")
        if upload_is_temp and upload_path.exists():
            upload_path.unlink()
        return 0

    code, _ = restyle_one(
        image_path, out_path,
        prompt=effective_prompt,
        model=args.model,
        size=args.size,
        quality=args.quality,
        output_format=args.output_format,
        upload_max_dim=args.upload_max_dim,
        bottom_padding_fraction=args.bottom_padding_fraction,
        style_refs=style_refs,
        force=args.force,
    )
    return code


if __name__ == "__main__":
    raise SystemExit(main())
