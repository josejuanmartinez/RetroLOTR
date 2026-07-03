"""Compose one or more images into a grid PNG in the OS temp folder and open it.

Used by the show-images-inline skill to display images to the user, since the
Claude Code transcript cannot render images or ANSI art directly.
"""

import argparse
import math
import os
import sys
import time
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

LABEL_HEIGHT = 38
PADDING = 10
BACKGROUND = (24, 24, 28)
LABEL_COLOR = (220, 220, 220)
MARKER_COLOR = (255, 200, 80)


def load_font(size: int = 20):
    try:
        return ImageFont.truetype("arialbd.ttf", size)
    except OSError:
        return ImageFont.load_default()


def marker(index: int, style: str) -> str:
    if style == "numbers":
        return f"{index + 1})"
    # letters: A) .. Z) AA) AB) ...
    label = ""
    index += 1
    while index:
        index, rem = divmod(index - 1, 26)
        label = chr(ord("A") + rem) + label
    return f"{label})"


def main() -> int:
    parser = argparse.ArgumentParser(description="Tile images into a grid PNG and open it in the default viewer.")
    parser.add_argument("images", nargs="+", help="Image file paths")
    parser.add_argument("--cols", type=int, default=0, help="Grid columns (0 = auto, ~square grid)")
    parser.add_argument("--cell", type=int, default=512, help="Max cell width/height in pixels")
    parser.add_argument("--no-labels", action="store_true", help="Skip filename labels under each cell")
    parser.add_argument("--numbering", choices=["letters", "numbers", "none"], default="letters",
                        help="Selection marker style prefixed to each label (default: letters)")
    parser.add_argument("--out", default="", help="Output path (default: temp file in %%TEMP%%)")
    parser.add_argument("--no-open", action="store_true", help="Only write the file, do not open a viewer")
    args = parser.parse_args()

    paths = []
    for raw in args.images:
        path = Path(raw)
        if not path.is_file():
            print(f"ERROR: not a file: {raw}", file=sys.stderr)
            return 1
        paths.append(path)

    cols = args.cols if args.cols > 0 else math.ceil(math.sqrt(len(paths)))
    rows = math.ceil(len(paths) / cols)
    label_h = 0 if args.no_labels else LABEL_HEIGHT

    cell_w = args.cell
    cell_h = args.cell + label_h
    canvas = Image.new(
        "RGB",
        (PADDING + cols * (cell_w + PADDING), PADDING + rows * (cell_h + PADDING)),
        BACKGROUND,
    )
    draw = ImageDraw.Draw(canvas)
    font = load_font()

    for i, path in enumerate(paths):
        col, row = i % cols, i // cols
        x0 = PADDING + col * (cell_w + PADDING)
        y0 = PADDING + row * (cell_h + PADDING)

        with Image.open(path) as img:
            img = img.convert("RGB")
            img.thumbnail((args.cell, args.cell), Image.LANCZOS)
            canvas.paste(img, (x0 + (cell_w - img.width) // 2, y0 + (args.cell - img.height) // 2))

        if not args.no_labels:
            prefix = "" if args.numbering == "none" else marker(i, args.numbering) + " "
            label = prefix + path.stem
            text_w = draw.textlength(label, font=font)
            text_x = x0 + (cell_w - text_w) / 2
            text_y = y0 + args.cell + 8
            if prefix:
                draw.text((text_x, text_y), prefix, fill=MARKER_COLOR, font=font)
                draw.text((text_x + draw.textlength(prefix, font=font), text_y), path.stem,
                          fill=LABEL_COLOR, font=font)
            else:
                draw.text((text_x, text_y), label, fill=LABEL_COLOR, font=font)

    if args.out:
        out_path = Path(args.out)
    else:
        out_path = Path(os.environ.get("TEMP", "/tmp")) / f"show_images_{time.strftime('%Y%m%d_%H%M%S')}.png"
    out_path.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(out_path)

    print(f"Grid: {cols}x{rows}, {len(paths)} image(s), {canvas.width}x{canvas.height}px")
    if args.numbering != "none" and not args.no_labels:
        for i, path in enumerate(paths):
            print(f"  {marker(i, args.numbering)} {path}")
    print(f"Written: {out_path}")

    if not args.no_open:
        os.startfile(str(out_path))  # noqa: S606 - opening in the user's default viewer is the point
        print("Opened in default viewer.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
