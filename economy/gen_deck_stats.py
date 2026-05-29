import json
import os
from pathlib import Path

MODULAR_DIR = Path(__file__).parent.parent / "Assets/Resources/Cards/Modular"
MANIFEST_PATH = MODULAR_DIR / "manifest.json"
OUTPUT_PATH = Path(__file__).parent / "deck_stats.json"

TROOP_STRENGTH = {0: 1, 1: 2, 2: 2, 3: 3, 4: 3, 5: 4, 6: 2, 7: 0}
TROOP_DEFENSE  = {0: 1, 1: 1, 2: 2, 3: 3, 4: 2, 5: 3, 6: 1, 7: 0}
TROOP_NAMES    = {0: "ma", 1: "ar", 2: "li", 3: "hi", 4: "lc", 5: "hc", 6: "ca", 7: "ws"}

RESOURCES = ["leather", "mounts", "timber", "iron", "steel", "mithril", "gold"]
CHAR_CLASSES = ["commander", "agent", "emmissary", "mage"]


def empty_required():
    return {r: 0 for r in RESOURCES + ["joker"]}

def empty_produced():
    return {r: 0 for r in RESOURCES}

def empty_char_levels():
    return {c: 0 for c in CHAR_CLASSES}

def empty_army_stats():
    return {
        "totalAttack": 0,
        "totalDefense": 0,
        "count": 0,
        "byTroopType": {
            name: {"count": 0, "attack": 0, "defense": 0}
            for name in TROOP_NAMES.values()
        }
    }


def process_deck(deck_path: Path) -> dict:
    with open(deck_path, encoding="utf-8-sig") as f:
        data = json.load(f)

    cards = data.get("cards", [])
    required = empty_required()
    produced = empty_produced()
    char_levels = empty_char_levels()
    army = empty_army_stats()
    card_type_counts: dict[str, int] = {}

    for card in cards:
        ctype = card.get("type", "Unknown")
        card_type_counts[ctype] = card_type_counts.get(ctype, 0) + 1

        # Resource cost — every card type
        for r in RESOURCES:
            required[r] += card.get(f"{r}Required", 0)
        required["joker"] += card.get("jokerRequired", 0)

        # Resource production — PC and Land cards only
        if ctype in ("PC", "Land"):
            for r in RESOURCES:
                produced[r] += card.get(f"{r}Granted", 0)

        # Character levels — Character cards only
        if ctype == "Character":
            for c in CHAR_CLASSES:
                char_levels[c] += card.get(c, 0)

        # Army stats — Army cards only
        if ctype == "Army":
            troop_int = card.get("troopType", 0)
            troop_name = TROOP_NAMES.get(troop_int, "ma")
            atk = TROOP_STRENGTH.get(troop_int, 0)
            dfn = TROOP_DEFENSE.get(troop_int, 0)

            army["count"] += 1
            army["totalAttack"] += atk
            army["totalDefense"] += dfn
            army["byTroopType"][troop_name]["count"] += 1
            army["byTroopType"][troop_name]["attack"] += atk
            army["byTroopType"][troop_name]["defense"] += dfn

    # Remove troop types that never appear
    army["byTroopType"] = {
        k: v for k, v in army["byTroopType"].items() if v["count"] > 0
    }

    return {
        "cardCount": len(cards),
        "cardTypeBreakdown": card_type_counts,
        "resources": {
            "required": required,
            "produced": produced,
        },
        "characterLevels": char_levels,
        "armyStats": army,
    }


def main():
    with open(MANIFEST_PATH, encoding="utf-8") as f:
        manifest = json.load(f)

    result = {}

    for entry in manifest["decks"]:
        deck_id = entry["id"]
        deck_path = Path(entry["path"])

        if not deck_path.exists():
            print(f"  SKIP (file missing): {deck_path}")
            continue

        stats = process_deck(deck_path)
        result[deck_id] = {
            "deckId": deck_id,
            **stats,
        }
        print(f"  OK  {deck_id:30s}  {stats['cardCount']} cards")

    with open(OUTPUT_PATH, "w", encoding="utf-8") as f:
        json.dump(result, f, indent=2)

    print(f"\nWrote {OUTPUT_PATH}")


if __name__ == "__main__":
    main()
