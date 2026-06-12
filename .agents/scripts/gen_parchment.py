#!/usr/bin/env python3
"""Generate a parchment 9-slice background sprite for the hex info panel."""

import base64, os, sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
OUT = REPO_ROOT / "Assets" / "Art" / "UI" / "HexInfoBackground.png"

PROMPT = (
    "A square UI background sprite for a 9-slice parchment panel in a fantasy strategy game. "
    "The image must read as aged, worn parchment or vellum — warm tan, ochre, and sepia tones. "
    "The border region (roughly 10% on each edge) should have slightly darker, more worn and "
    "fibrous edges, subtle ink bleed, and minor fraying, as if the paper was cut from an old "
    "medieval map or scroll. The center (roughly 80% of the canvas) should be a cleaner, "
    "lightly textured parchment surface that can tile or stretch without looking wrong — "
    "no strong directional gradients or centered decorations in the middle area. "
    "Overall mood: authentic aged paper from a Lord of the Rings style atlas or manuscript. "
    "No text, no characters, no symbols, no borders drawn as lines — only organic paper texture "
    "and edge wear. Muted earthy palette: warm tans, off-whites, burnt sienna, light brown. "
    "Flat lighting, no dramatic shadows, photographic paper texture quality."
)


def main() -> None:
    api_key = os.getenv("OPENAI_API_KEY")
    if not api_key:
        sys.exit("Error: OPENAI_API_KEY not set.")

    try:
        from openai import OpenAI
    except ImportError:
        sys.exit("Error: openai package not installed. Run: pip install openai")

    client = OpenAI(api_key=api_key)
    print("Generating parchment texture via gpt-image-1 (1024x1024)…")

    response = client.images.generate(
        model="gpt-image-1",
        prompt=PROMPT,
        size="1024x1024",
        quality="high",
        n=1,
        output_format="png",
    )

    b64 = response.data[0].b64_json
    if not b64:
        sys.exit("Error: no image data in response.")

    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_bytes(base64.b64decode(b64))
    print(f"Saved: {OUT}")


if __name__ == "__main__":
    main()
