"""
Renames the TMP sprites in environment_terrain_features_spritesheet.asset so that
TextMeshPro <sprite name="..."> resolves a TerrainEnum / HexFeatureEnum / Environmental-card
name.

Source of truth: mapping.json  (suffix -> FINAL m_Name, written verbatim).
Confident sprites use the normalized atlas name (lowercase, alphanumeric, matching
CardNameUtility.Normalize). Uncertain sprites use FIXME_<suffix>_<candidates> so they
can be found and corrected by hand in the TMP Sprite Asset.

Only m_SpriteCharacterTable[].m_Name is edited (that is what TMP looks up); the .png.meta
slice names are left untouched.

Run:  python tools/sprite_mapping/apply_sprite_names.py            # dry-run, prints diff
      python tools/sprite_mapping/apply_sprite_names.py --write    # writes the .asset
"""
import json, os, re, sys
from collections import Counter

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
ASSET = os.path.join(ROOT, "Assets", "Art", "Fonts", "Spritesheets",
                     "environment_terrain_features_spritesheet.asset")
MAPPING = os.path.join(os.path.dirname(os.path.abspath(__file__)), "mapping.json")


def main():
    write = "--write" in sys.argv
    raw = json.load(open(MAPPING, encoding="utf-8"))
    targets = {int(k): v for k, v in raw.items() if not k.startswith("_")}

    dups = {n: c for n, c in Counter(targets.values()).items() if c > 1}
    if dups:
        print("!! duplicate names (would collide in TMP):", dups, "\n")

    text = open(ASSET, encoding="utf-8").read()
    changes = []

    def repl(m):
        suffix = int(m.group(1))
        new = targets.get(suffix)
        if new is None:
            return m.group(0)
        changes.append((suffix, new))
        return f"m_Name: {new}"

    new_text = re.sub(r"m_Name: environment_terrain_features_(\d+)", repl, text)

    for suffix, new in sorted(changes):
        tag = "  <-- FIXME" if new.startswith("FIXME") else ""
        print(f"#{suffix:2d} -> {new}{tag}")
    fixmes = sum(1 for _, n in changes if n.startswith("FIXME"))
    print(f"\n{len(changes)} names set ({len(changes) - fixmes} final, {fixmes} FIXME).")

    if write:
        open(ASSET, "w", encoding="utf-8", newline="\n").write(new_text)
        print(f"\nWROTE {ASSET}")
    else:
        print("\n(dry-run; pass --write to apply)")


if __name__ == "__main__":
    main()
