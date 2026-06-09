"""Add 20% transparent top padding to all PNGs in Assets/Art/Cards/Characters."""
import sys
from pathlib import Path
from PIL import Image

INPUT_DIR = Path(__file__).parent.parent / "Assets" / "Art" / "Characters"
PADDING_RATIO = 0.20

files = sorted(INPUT_DIR.glob("*.png"))
if not files:
    print("No PNG files found.")
    sys.exit(1)

for path in files:
    img = Image.open(path).convert("RGBA")
    w, h = img.size
    pad = round(w * PADDING_RATIO)
    new_img = Image.new("RGBA", (w + pad * 2, h), (0, 0, 0, 0))
    new_img.paste(img, (pad, 0))
    new_img.save(path)
    print(f"  {path.name}: {w}x{h} -> {w + pad * 2}x{h}")

print(f"\nDone — {len(files)} files updated.")
