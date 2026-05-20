import json

PATH = r"c:\Users\jjmca\RetroLOTR\Assets\Resources\Cards\Modular\Sharkey.json"

with open(PATH, "r", encoding="utf-8") as f:
    data = json.load(f)

existing_ids = {c["cardId"] for c in data["cards"]}
existing_names = {c["name"] for c in data["cards"] if c.get("name")}
print(f"Existing cards: {len(data['cards'])}, max ID: {max(existing_ids)}")

def base_card(card_id, name, card_type, quote, action_effect, sprite):
    return {
        "cardId": card_id, "name": name, "quote": quote,
        "actionEffect": action_effect, "type": card_type, "tags": [],
        "deckId": "sharkey", "alignment": 1,
        "actionClassName": "", "action": "", "spriteName": sprite,
        "region": "", "description": "", "requirementsText": "",
        "historyText": "", "statusEffect": "", "procChance": 0,
        "portraitName": "", "characterGroup": "", "referenceDeckId": "",
        "referenceCardId": 0, "encounterOptions": [],
        "fleeOption": {"optionId": "", "label": "", "description": "", "outcomes": []},
        "commander": 0, "agent": 0, "emmissary": 0, "mage": 0, "race": 0,
        "artifacts": [], "troopType": 0, "specialAbilities": [],
        "commanderSkillRequired": 0, "agentSkillRequired": 0,
        "emissarySkillRequired": 0, "mageSkillRequired": 0, "difficulty": 0,
        "leatherRequired": 0, "mountsRequired": 0, "timberRequired": 0,
        "ironRequired": 0, "steelRequired": 0, "mithrilRequired": 0,
        "goldRequired": 0, "jokerRequired": 0, "leatherGranted": 0,
        "mountsGranted": 0, "timberGranted": 0, "ironGranted": 0,
        "steelGranted": 0, "mithrilGranted": 0, "goldGranted": 0,
        "startingPC": "",
        "inspireEffectData": {"type": "", "statusEffect": "", "turns": 0,
            "amount": 0, "troopType": "", "resourceType": "", "skillType": "",
            "allCharacters": True, "nearest": True},
        "amount": 1, "deckSpriteName": ""
    }

next_id = max(existing_ids) + 1
print(f"Starting new IDs from: {next_id}")

# Skip Fredegar Bolger if already exists by name
new_cards = []

if "Fredegar Bolger" not in existing_names:
    c = base_card(next_id, "Fredegar Bolger", "Character",
        "Not all who stayed behind were idle. In the hidden lanes and quiet copses, a resistance kindled.",
        'Apply Hidden <sprite name="hidden"> (2 turns) to all allied characters in your hex.',
        "FredegaBolger")
    c.update({"commander": 1, "agent": 1, "race": 3, "startingPC": "The Lockholes",
        "inspireEffectData": {"type": "ApplyStatusEffect", "statusEffect": "Hidden",
            "turns": 2, "amount": 0, "troopType": "", "resourceType": "", "skillType": "",
            "allCharacters": True, "nearest": True}})
    new_cards.append(c); next_id += 1

if "Lotho Sackville-Baggins" not in existing_names:
    c = base_card(next_id, "Lotho Sackville-Baggins", "Character",
        "I am Chief now. The Chief. And if I say that you sell it, you sell it.",
        'Gain 4 gold <sprite name="gold"> from Shire taxation.',
        "LothoSackvilleBaggins")
    c.update({"agent": 1, "emmissary": 2, "race": 3, "startingPC": "The Lockholes",
        "emissarySkillRequired": 1, "goldRequired": 2,
        "inspireEffectData": {"type": "GainResource", "statusEffect": "", "turns": 0,
            "amount": 4, "troopType": "", "resourceType": "gold", "skillType": "",
            "allCharacters": True, "nearest": True}})
    new_cards.append(c); next_id += 1

if "Mat Heathertoes" not in existing_names:
    c = base_card(next_id, "Mat Heathertoes", "Character",
        "There's always work for men who ask the right questions and forget to hear the answers.",
        'Apply Fear <sprite name="fear"> (1 turn) to all enemy characters in the caster\'s hex.',
        "MatHeathertoes")
    c.update({"commander": 1, "agent": 1, "race": 0, "startingPC": "The Lockholes",
        "commanderSkillRequired": 1, "leatherRequired": 1, "ironRequired": 2, "goldRequired": 3,
        "inspireEffectData": {"type": "ApplyStatusEffect", "statusEffect": "Fear",
            "turns": 1, "amount": 0, "troopType": "", "resourceType": "", "skillType": "",
            "allCharacters": True, "nearest": False}})
    new_cards.append(c); next_id += 1

if "The Old Mill" not in existing_names:
    c = base_card(next_id, "The Old Mill", "Event",
        "Where once flour was ground for honest bread, now smoke billows at all hours and the river runs grey.",
        'Gain 3 iron <sprite name="iron"> and reduce loyalty <sprite name="loyalty"> by 10 in the caster\'s hex.',
        "TheOldMill")
    c.update({"tags": ["sharkey", "shire", "industry"],
        "agentSkillRequired": 1, "leatherRequired": 1, "timberRequired": 1, "goldRequired": 4,
        "amount": 2})
    new_cards.append(c); next_id += 1

if "The Battle of Bywater" not in existing_names:
    c = base_card(next_id, "The Battle of Bywater", "Event",
        "At the Battle of Bywater they fell upon the ruffians from hedge and ditch — and the Shire was its own again.",
        'Destroy all enemy light infantry <sprite name="li"> in a target hex in radius 3.',
        "TheBattleOfBywater")
    c.update({"tags": ["sharkey", "shire", "battle"],
        "commanderSkillRequired": 2, "mountsRequired": 2, "goldRequired": 6})
    new_cards.append(c); next_id += 1

if "Imprisonment" not in existing_names:
    c = base_card(next_id, "Imprisonment", "Event",
        "The Lockholes do not kill — they merely forget. You are there until the Chief decides otherwise.",
        "Remove a target enemy character from play for 2 turns.",
        "Imprisonment")
    c.update({"tags": ["sharkey", "control", "lockholes"],
        "agentSkillRequired": 2, "leatherRequired": 1, "goldRequired": 5})
    new_cards.append(c); next_id += 1

if "Industrialization" not in existing_names:
    c = base_card(next_id, "Industrialization", "Event",
        "Trees fell, water ran foul, and smoke lay low over the hills. The Shire was being improved, they said.",
        'Gain 2 iron <sprite name="iron"> and 2 timber <sprite name="timber">. All PCs in radius 2 lose 5 loyalty <sprite name="loyalty">.',
        "Industrialization")
    c.update({"tags": ["sharkey", "shire", "industry", "resource"],
        "emissarySkillRequired": 1, "leatherRequired": 1, "goldRequired": 3, "amount": 2})
    new_cards.append(c); next_id += 1

if "Ruffians" not in existing_names:
    c = base_card(next_id, "Ruffians", "Army",
        "Rough men, unasked-for, with clubs and cudgels, who take what they want and call it order.",
        "", "RuffiansLightInfantry")
    c.update({"troopType": 2, "referenceDeckId": "saruman_base", "referenceCardId": 716,
        "commanderSkillRequired": 1, "leatherRequired": 1, "ironRequired": 2,
        "goldRequired": 3, "amount": 2})
    new_cards.append(c); next_id += 1

if "Bywater" not in existing_names:
    c = base_card(next_id, "Bywater", "PC",
        "Bywater sits at the crossing of roads and the meeting of waters, and does not forget either.",
        "", "Bywater")
    c.update({"tags": ["TheShire"], "action": "PCAction", "region": "TheShire",
        "leatherGranted": 1, "mountsGranted": 1, "goldGranted": 1})
    new_cards.append(c); next_id += 1

# Fix empty sprite names for Cruel Winter and Caravans
for card in data["cards"]:
    if card["cardId"] == 9048 and not card.get("spriteName"):
        card["spriteName"] = "CruelWinter"
    if card["cardId"] == 9051 and not card.get("spriteName"):
        card["spriteName"] = "Caravans"

data["cards"].extend(new_cards)

with open(PATH, "w", encoding="utf-8", newline="\n") as f:
    json.dump(data, f, indent=4, ensure_ascii=False)

print(f"Done. Total cards: {len(data['cards'])}")
print("New cards added:")
for c in new_cards:
    print(f"  {c['cardId']} {c['name']} [{c['type']}]")
