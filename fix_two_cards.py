import json

PATH = r"c:\Users\jjmca\RetroLOTR\Assets\Resources\Cards\Modular\Sharkey.json"

with open(PATH, encoding="utf-8") as f:
    data = json.load(f)

for card in data["cards"]:
    if card["cardId"] == 9014:
        card["actionClassName"] = "LothosPurseAction"
        card["actionEffect"] = 'Gain 15 gold <sprite name="gold">. Leather and timber stores go to 0.'
        print(f"Fixed 9014 '{card['name']}'")
    if card["cardId"] == 9016:
        card["actionClassName"] = "GrimasKnifeAction"
        card["actionEffect"] = "Roll the dice: <25 caster is assassinated, 25-50 caster is wounded, 50-75 target is wounded, >75 target is killed."
        print(f"Fixed 9016 '{card['name']}'")

with open(PATH, "w", encoding="utf-8", newline="\n") as f:
    json.dump(data, f, indent=4, ensure_ascii=False)

# Verify immediately
with open(PATH, encoding="utf-8") as f:
    data2 = json.load(f)
for card in data2["cards"]:
    if card["cardId"] in (9014, 9016):
        print(f"  VERIFY {card['cardId']}: class='{card['actionClassName']}' effect='{card['actionEffect'][:60]}'")
