import json
import os

MODULAR_DIR = "Assets/Resources/Cards/Modular"

for filename in sorted(os.listdir(MODULAR_DIR)):
    if not filename.endswith(".json"):
        continue
    with open(os.path.join(MODULAR_DIR, filename), "r", encoding="utf-8") as f:
        data = json.load(f)
    for card in data.get("cards", []):
        if card.get("type") == "Character" and card.get("actionEffect"):
            print(f"{card['name']}: {card['actionEffect']}")
