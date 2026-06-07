#!/usr/bin/env python3
"""Batch-generate animation videos + spritesheets for all character PNGs."""

from __future__ import annotations

import subprocess
import sys
from pathlib import Path

SKIP = {"races_atlas_23.png"}
CHARS_DIR = Path("Assets/Art/Characters")
VIDEOS_DIR = Path("Assets/Art/Characters/AnimationVideos")
ANIM_DIR = Path("Assets/Art/Characters/Animations")

GEN_SCRIPT = Path(".agents/skills/spritesheet-generation/scripts/generate_spritesheet_video.py")
SHEET_SCRIPT = Path(".agents/skills/extract_spritesheet_from_video/scripts/extract_spritesheet.py")


def run(cmd: list[str], label: str) -> bool:
    print(f"\n>>> {label}", flush=True)
    result = subprocess.run([sys.executable] + cmd, capture_output=False)
    if result.returncode != 0:
        print(f"    FAILED (exit {result.returncode})", flush=True)
        return False
    return True


def main() -> None:
    images = sorted(
        p for p in CHARS_DIR.glob("*.png")
        if p.name not in SKIP
    )

    done, failed = [], []

    for img in images:
        stem = img.stem
        video_out = VIDEOS_DIR / f"{stem}.mp4"
        sheet_out = ANIM_DIR / f"{stem}_spritesheet.png"

        # Step 1: generate video (skip if already exists)
        if video_out.exists():
            print(f"\n--- Skipping video generation for {stem} (already exists)")
        else:
            ok = run(
                [str(GEN_SCRIPT), "--image", str(img)],
                f"[{stem}] Generating animation video"
            )
            if not ok:
                failed.append(stem)
                continue

        # Step 2: extract spritesheet (always regenerate)
        ok = run(
            [str(SHEET_SCRIPT), "--video", str(video_out), "--out", str(sheet_out)],
            f"[{stem}] Extracting spritesheet"
        )
        if ok:
            done.append(stem)
        else:
            failed.append(stem)

    print("\n" + "=" * 50)
    print(f"Done: {len(done)}  Failed: {len(failed)}")
    if failed:
        print("Failed:", ", ".join(failed))


if __name__ == "__main__":
    main()
