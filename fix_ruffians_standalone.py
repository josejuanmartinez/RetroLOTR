import json

PATH = r"c:\Users\jjmca\RetroLOTR\Assets\Resources\Cards\Modular\Sharkey.json"

with open(PATH, encoding="utf-8") as f:
    data = json.load(f)

for card in data["cards"]:
    if card["cardId"] == 9080:
        card["name"] = "Ruffians"
        card["spriteName"] = "RuffiansLightInfantry"
        card["quote"] = "Rough men, unasked-for, with clubs and cudgels, who take what they want and call it order."
        card["troopType"] = 2
        card["referenceDeckId"] = ""
        card["referenceCardId"] = 0
        card["commanderSkillRequired"] = 1
        card["leatherRequired"] = 1
        card["ironRequired"] = 2
        card["goldRequired"] = 3
        card["amount"] = 2
        print(f"Fixed: '{card['name']}' standalone (no reference)")

with open(PATH, "w", encoding="utf-8", newline="\n") as f:
    json.dump(data, f, indent=4, ensure_ascii=False)

print("Done.")
