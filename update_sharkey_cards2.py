import json

PATH = r"c:\Users\jjmca\RetroLOTR\Assets\Resources\Cards\Modular\Sharkey.json"

with open(PATH, encoding="utf-8") as f:
    data = json.load(f)

updates = {
    9076: {
        "name": "Destroy the Mill",
        "actionEffect": 'Gain 5 timber <sprite name="timber"> and 3 iron <sprite name="iron">. Reduce loyalty <sprite name="loyalty"> by 10 in the caster\'s hex.',
    },
    9078: {
        "actionEffect": "Kidnap a target enemy character in the caster's hex.",
    },
    9079: {
        "actionEffect": 'Gain 5 iron <sprite name="iron"> and 5 steel <sprite name="steel">. All PCs in radius 5 lose 15 loyalty <sprite name="loyalty">.',
    },
    9014: {
        "actionClassName": "LothosPurseAction",
        "actionEffect": 'Gain 15 gold <sprite name="gold">. Leather and timber stores go to 0.',
    },
    9011: {
        "actionClassName": "PipeweedMonopolyAction",
        "actionEffect": 'Gain 5 gold <sprite name="gold">. Draw Lured by Halflings\' Leaf and The Lure of the Senses if hand size allows.',
    },
}

for card in data["cards"]:
    cid = card["cardId"]
    if cid in updates:
        for k, v in updates[cid].items():
            card[k] = v
        print(f"Updated {cid} '{card['name']}': {list(updates[cid].keys())}")

with open(PATH, "w", encoding="utf-8", newline="\n") as f:
    json.dump(data, f, indent=4, ensure_ascii=False)

print("Done.")
