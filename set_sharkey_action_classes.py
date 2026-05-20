import json

PATH = r"c:\Users\jjmca\RetroLOTR\Assets\Resources\Cards\Modular\Sharkey.json"

ACTION_CLASSES = {
    9076: "TheOldMillAction",
    9077: "TheBattleOfBywaterAction",
    9078: "ImprisonmentAction",
    9079: "IndustrializationAction",
}

with open(PATH, encoding="utf-8") as f:
    data = json.load(f)

for card in data["cards"]:
    if card["cardId"] in ACTION_CLASSES:
        card["actionClassName"] = ACTION_CLASSES[card["cardId"]]
        print(f"Set {card['cardId']} {card['name']} actionClassName = {card['actionClassName']}")

with open(PATH, "w", encoding="utf-8", newline="\n") as f:
    json.dump(data, f, indent=4, ensure_ascii=False)

print("Done.")
