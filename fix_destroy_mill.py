import json

PATH = r"c:\Users\jjmca\RetroLOTR\Assets\Resources\Cards\Modular\Sharkey.json"

with open(PATH, encoding="utf-8") as f:
    data = json.load(f)

for card in data["cards"]:
    if card["cardId"] == 9076:
        card["name"] = "Destroy the Mill"
        card["actionClassName"] = "TheOldMillAction"
        card["actionEffect"] = 'Reduce loyalty <sprite name="loyalty"> by 10. Enemy PC owner loses up to 3 of each resource.'
        print(f"Updated: {card['actionEffect']}")

with open(PATH, "w", encoding="utf-8", newline="\n") as f:
    json.dump(data, f, indent=4, ensure_ascii=False)

print("Done.")
