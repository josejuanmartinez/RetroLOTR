import json

PATH = r"c:\Users\jjmca\RetroLOTR\Assets\Resources\Cards\Modular\Sharkey.json"

FIXES = {
    9011: {"actionClassName": "PipeweedMonopolyAction",
           "actionEffect": 'Gain 5 gold <sprite name="gold">. Draw Lured by Halflings\' Leaf and The Lure of the Senses if hand size allows.'},
    9014: {"actionClassName": "LothosPurseAction",
           "actionEffect": 'Gain 15 gold <sprite name="gold">. Leather and timber stores go to 0.'},
    9016: {"actionClassName": "GrimasKnifeAction",
           "actionEffect": "Roll the dice: <25 caster is assassinated, 25-50 caster is wounded, 50-75 target is wounded, >75 target is killed."},
    9076: {"name": "Destroy the Mill", "actionClassName": "TheOldMillAction",
           "actionEffect": 'Gain 5 timber <sprite name="timber"> and 3 iron <sprite name="iron">. Reduce loyalty <sprite name="loyalty"> by 10 in the caster\'s hex.'},
    9077: {"actionClassName": "TheBattleOfBywaterAction",
           "actionEffect": 'Destroy all enemy light infantry <sprite name="li"> forces in radius 3.'},
    9078: {"actionClassName": "ImprisonmentAction",
           "actionEffect": "Kidnap a target enemy character in the caster's hex."},
    9079: {"actionClassName": "IndustrializationAction",
           "actionEffect": 'Gain 5 iron <sprite name="iron"> and 5 steel <sprite name="steel">. All PCs in radius 5 lose 15 loyalty <sprite name="loyalty">.'},
}

with open(PATH, encoding="utf-8") as f:
    data = json.load(f)

for card in data["cards"]:
    cid = card["cardId"]
    if cid in FIXES:
        for k, v in FIXES[cid].items():
            card[k] = v

with open(PATH, "w", encoding="utf-8", newline="\n") as f:
    json.dump(data, f, indent=4, ensure_ascii=False)

# Verify
with open(PATH, encoding="utf-8") as f:
    data2 = json.load(f)
for card in data2["cards"]:
    if card["cardId"] in FIXES:
        print(f"{card['cardId']} '{card['name']}': class='{card['actionClassName']}' effect='{card['actionEffect'][:70]}'")
