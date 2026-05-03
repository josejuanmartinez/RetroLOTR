import json
import os
import shutil

# Base paths
BASE = "Assets/Resources/Cards/Modular"
ART_BASE = "Assets/Art/Cards/Characters"
ADDR = "Assets/AddressableAssetsData/AssetGroups/Default Local Group.asset"

# Deck file mapping
DECK_FILES = {
    "Mithrandir": "Mithrandir.json",
    "GandalfTheWhite": "GandalfTheWhite.json",
    "DisturberOfThePeace": "DisturberOfThePeace.json",
    "Stormcrow": "Stormcrow.json",
    "SauronBase": "SauronBase.json",
    "OfManyColours": "OfManyColours.json",
    "TheDeceiver": "TheDeceiver.json",
    "TheWhiteHand": "TheWhiteHand.json",
    "TheIronCrown": "TheIronCrown.json",
    "TheNecromancer": "TheNecromancer.json",
    "ShadowOfTheEast": "ShadowOfTheEast.json",
    "Tharkun": "Tharkun.json",
}

# Character -> (decks, race, skills dict)
# skills: {commander, agent, emissary, mage}
CHARACTERS = [
    ("Arwen", ["Mithrandir", "GandalfTheWhite"], 1, {"emissary": 1}),
    ("Bilbo", ["DisturberOfThePeace"], 3, {"agent": 1}),
    ("Boromir", ["GandalfTheWhite"], 0, {"commander": 1}),
    ("Celeborn", ["Mithrandir"], 1, {"commander": 1}),
    ("Elladan", ["Mithrandir"], 1, {"commander": 1}),
    ("Elrohir", ["Mithrandir"], 1, {"commander": 1}),
    ("Eomer", ["Stormcrow"], 0, {"commander": 1}),
    ("Eowyn", ["Stormcrow", "GandalfTheWhite"], 0, {"commander": 1}),
    ("Erestor", ["Mithrandir"], 1, {"emissary": 1}),
    ("Galdor", ["Mithrandir"], 1, {"emissary": 1}),
    ("Gimli", ["Tharkun"], 2, {"commander": 1}),
    ("Glorfindel", ["Mithrandir", "DisturberOfThePeace"], 1, {"commander": 1}),
    ("Gollum", ["SauronBase"], 0, {"agent": 1}),
    ("Halbarad", ["DisturberOfThePeace"], 12, {"commander": 1}),
    ("Haldir", ["Mithrandir"], 1, {"commander": 1}),
    ("Legolas", ["Mithrandir"], 1, {"commander": 1}),
    ("Sam", ["DisturberOfThePeace"], 3, {"emissary": 1}),
    ("Angulion", ["OfManyColours", "TheDeceiver"], 0, {"commander": 1}),
    ("Aonghas", ["TheWhiteHand"], 0, {"commander": 1}),
    ("Broggha", ["TheIronCrown"], 5, {"commander": 1}),
    ("Bugrug", ["SauronBase"], 5, {"commander": 1}),
    ("Celedhring", ["TheNecromancer"], 1, {"mage": 1}),
    ("Enion", ["TheWhiteHand"], 0, {"commander": 1}),
    ("HarryGoatleaf", ["DisturberOfThePeace"], 0, {"agent": 1}),
    ("Lomelinde", ["ShadowOfTheEast"], 0, {"emissary": 1}),
    ("Mauhir", ["TheIronCrown"], 5, {"commander": 1}),
    ("Din Ohtar", ["ShadowOfTheEast"], 0, {"commander": 1}),
]

def make_card(name, deck_id, card_id, race, skills):
    sprite = name.replace(" ", "")
    return {
        "cardId": card_id,
        "name": name,
        "quote": "",
        "actionEffect": "",
        "type": "Character",
        "tags": [],
        "deckId": deck_id,
        "alignment": 0,
        "actionClassName": "",
        "action": "",
        "spriteName": sprite,
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
        "commander": skills.get("commander", 0),
        "agent": skills.get("agent", 0),
        "emmissary": skills.get("emissary", 0),
        "mage": skills.get("mage", 0),
        "race": race,
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
    # Rename Leardinoth -> Din Ohtar
    old_art = os.path.join(ART_BASE, "Leardinoth.png")
    new_art = os.path.join(ART_BASE, "DinOhtar.png")
    old_meta = old_art + ".meta"
    new_meta = new_art + ".meta"
    if os.path.exists(old_art):
        shutil.move(old_art, new_art)
        print(f"Renamed art: {old_art} -> {new_art}")
    if os.path.exists(old_meta):
        shutil.move(old_meta, new_meta)
        print(f"Renamed meta: {old_meta} -> {new_meta}")
    # Update addressables
    if os.path.exists(ADDR):
        with open(ADDR, "r", encoding="utf-8-sig") as f:
            addr_content = f.read()
        addr_content = addr_content.replace(
            "Assets/Art/Cards/Characters/Leardinoth.png",
            "Assets/Art/Cards/Characters/DinOhtar.png"
        )
        with open(ADDR, "w", encoding="utf-8-sig") as f:
            f.write(addr_content)
        print("Updated addressables for DinOhtar")

    # Find max card id across all decks
    max_id = 0
    for fname in os.listdir(BASE):
        if not fname.endswith(".json"):
            continue
        path = os.path.join(BASE, fname)
        with open(path, "r", encoding="utf-8-sig") as f:
            data = json.load(f)
        for card in data.get("cards", []):
            max_id = max(max_id, card.get("cardId", 0))
    next_id = max_id + 1
    print(f"Starting card IDs from {next_id}")

    # Group characters by deck
    deck_additions = {}
    for char_name, decks, race, skills in CHARACTERS:
        for deck in decks:
            deck_file = DECK_FILES[deck]
            deck_id = os.path.splitext(deck_file)[0].lower().replace("ofmanycolours", "of_many_colours")
            # Fix deck_id mapping
            deck_id_map = {
                "ofmanycolours": "of_many_colours",
                "thedeceiver": "the_deceiver",
                "thewhitehand": "the_white_hand",
                "theironcrown": "the_iron_crown",
                "thenecromancer": "the_necromancer",
                "shadowoftheeast": "shadow_of_the_east",
                "disturberofthepeace": "disturber_of_the_peace",
                "gandalfthewhite": "gandalf_the_white",
                "sauronbase": "sauron_base",
            }
            # Actually deckId in JSON is already known from the file, let's read it
            if deck_file not in deck_additions:
                deck_additions[deck_file] = []
            deck_additions[deck_file].append((char_name, race, skills))

    # Process each deck
    for deck_file, additions in deck_additions.items():
        path = os.path.join(BASE, deck_file)
        with open(path, "r", encoding="utf-8-sig") as f:
            data = json.load(f)
        deck_id = data.get("deckId", "")
        for char_name, race, skills in additions:
            card = make_card(char_name, deck_id, next_id, race, skills)
            data["cards"].append(card)
            print(f"Added {char_name} (id {next_id}) to {deck_file}")
            next_id += 1
        with open(path, "w", encoding="utf-8-sig") as f:
            json.dump(data, f, indent=4)
        print(f"Saved {deck_file}")

if __name__ == "__main__":
    main()
