import json
import os

BASE = "Assets/Resources/Cards/Modular"

LEADERS = {
    # Base decks
    "GandalfBase.json": {"name": "Gandalf", "sprite": "Gandalf", "race": 4, "alignment": 0, "skills": {"commander": 3, "agent": 2, "emissary": 2, "mage": 3}},
    "SarumanBase.json": {"name": "Saruman", "sprite": "Saruman", "race": 4, "alignment": 2, "skills": {"commander": 3, "agent": 2, "emissary": 3, "mage": 3}},
    "SauronBase.json": {"name": "Sauron", "sprite": "Sauron", "race": 4, "alignment": 1, "skills": {"commander": 4, "agent": 2, "emissary": 2, "mage": 4}},
    # Gandalf subdecks
    "DisturberOfThePeace.json": {"name": "Disturber of the Peace", "sprite": "Gandalf-DisturberOfPeace", "race": 4, "alignment": 0, "skills": {"commander": 3, "agent": 1, "emissary": 1, "mage": 3}},
    "GandalfTheWhite.json": {"name": "Gandalf the White", "sprite": "Gandalf-TheWhite", "race": 4, "alignment": 0, "skills": {"commander": 3, "agent": 1, "emissary": 1, "mage": 3}},
    "Mithrandir.json": {"name": "Mithrandir", "sprite": "Gandalf-Mithrandir", "race": 4, "alignment": 0, "skills": {"commander": 3, "agent": 1, "emissary": 1, "mage": 3}},
    "Stormcrow.json": {"name": "Stormcrow", "sprite": "Gandalf-Stormcrow", "race": 4, "alignment": 0, "skills": {"commander": 3, "agent": 1, "emissary": 1, "mage": 3}},
    "Tharkun.json": {"name": "Tharkun", "sprite": "Gandalf-Tharkun", "race": 4, "alignment": 0, "skills": {"commander": 3, "agent": 1, "emissary": 1, "mage": 3}},
    # Saruman subdecks
    "OfManyColours.json": {"name": "Of Many Colours", "sprite": "Saruman-OfManyColours", "race": 4, "alignment": 2, "skills": {"commander": 3, "agent": 1, "emissary": 2, "mage": 3}},
    "SarumanTheWhite.json": {"name": "Saruman the White", "sprite": "Saruman-TheWhite", "race": 4, "alignment": 2, "skills": {"commander": 3, "agent": 1, "emissary": 2, "mage": 3}},
    "TheWhiteHand.json": {"name": "The White Hand", "sprite": "Saruman-TheWhiteHand", "race": 4, "alignment": 2, "skills": {"commander": 3, "agent": 1, "emissary": 2, "mage": 3}},
    # Sauron subdecks
    "ShadowOfTheEast.json": {"name": "Shadow of the East", "sprite": "Sauron-ShadowOfTheEast", "race": 4, "alignment": 1, "skills": {"commander": 3, "agent": 1, "emissary": 1, "mage": 3}},
    "TheDarkEye.json": {"name": "The Dark Eye", "sprite": "Sauron-TheDarkEye", "race": 4, "alignment": 1, "skills": {"commander": 3, "agent": 1, "emissary": 1, "mage": 3}},
    "TheDeceiver.json": {"name": "The Deceiver", "sprite": "Sauron-TheDeceiver", "race": 4, "alignment": 1, "skills": {"commander": 3, "agent": 1, "emissary": 1, "mage": 3}},
    "TheIronCrown.json": {"name": "The Iron Crown", "sprite": "Sauron-TheIronCrown", "race": 4, "alignment": 1, "skills": {"commander": 3, "agent": 1, "emissary": 1, "mage": 3}},
    "TheNecromancer.json": {"name": "The Necromancer", "sprite": "Sauron-TheNecromancer", "race": 4, "alignment": 1, "skills": {"commander": 3, "agent": 1, "emissary": 1, "mage": 3}},
}

def make_card(leader_info, deck_id, card_id):
    return {
        "cardId": card_id,
        "name": leader_info["name"],
        "quote": "",
        "actionEffect": "",
        "type": "Character",
        "tags": [],
        "deckId": deck_id,
        "alignment": leader_info["alignment"],
        "actionClassName": "",
        "action": "",
        "spriteName": leader_info["sprite"],
        "region": "",
        "requirementsText": "",
        "historyText": "",
        "statusEffect": "",
        "procChance": 0,
        "portraitName": "",
        "referenceDeckId": "",
        "referenceCardId": 0,
        "encounterOptions": [],
        "fleeOption": {
            "optionId": "",
            "label": "",
            "description": "",
            "outcomes": []
        },
        "commander": leader_info["skills"].get("commander", 0),
        "agent": leader_info["skills"].get("agent", 0),
        "emmissary": leader_info["skills"].get("emissary", 0),
        "mage": leader_info["skills"].get("mage", 0),
        "race": leader_info["race"],
        "artifacts": [],
        "troopType": 0,
        "specialAbilities": [],
        "commanderSkillRequired": 0,
        "agentSkillRequired": 0,
        "emissarySkillRequired": 0,
        "mageSkillRequired": 0,
        "difficulty": 0,
        "leatherRequired": 0,
        "mountsRequired": 0,
        "timberRequired": 0,
        "ironRequired": 0,
        "steelRequired": 0,
        "mithrilRequired": 0,
        "goldRequired": 0,
        "jokerRequired": 0,
        "leatherGranted": 0,
        "mountsGranted": 0,
        "timberGranted": 0,
        "ironGranted": 0,
        "steelGranted": 0,
        "mithrilGranted": 0,
        "goldGranted": 0,
    }

def main():
    # Find max card id
    max_id = 0
    for fname in os.listdir(BASE):
        if not fname.endswith(".json"): continue
        path = os.path.join(BASE, fname)
        with open(path, "r", encoding="utf-8-sig") as f:
            data = json.load(f)
        for card in data.get("cards", []):
            max_id = max(max_id, card.get("cardId", 0))
    next_id = max_id + 1
    print(f"Starting card IDs from {next_id}")

    for deck_file, leader_info in LEADERS.items():
        path = os.path.join(BASE, deck_file)
        if not os.path.exists(path):
            print(f"Skipping {deck_file} (not found)")
            continue
        
        with open(path, "r", encoding="utf-8-sig") as f:
            data = json.load(f)
        
        deck_id = data.get("deckId", "")
        
        # Check if leader already exists
        existing = [c for c in data.get("cards", []) if c.get("name") == leader_info["name"]]
        if existing:
            print(f"{deck_file}: already has '{leader_info['name']}'")
            continue
        
        card = make_card(leader_info, deck_id, next_id)
        data["cards"].append(card)
        print(f"Added {leader_info['name']} (id {next_id}) to {deck_file}")
        next_id += 1
        
        with open(path, "w", encoding="utf-8-sig") as f:
            json.dump(data, f, indent=4)

if __name__ == "__main__":
    main()
