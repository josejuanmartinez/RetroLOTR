import json, glob, os

# Load all decks
decks = {}
for path in glob.glob('Assets/Resources/Cards/Modular/*.json'):
    fname = os.path.basename(path)
    with open(path, 'r', encoding='utf-8') as f:
        data = json.load(f)
    decks[fname] = data

all_land_cards = {}
for fname, data in decks.items():
    for card in data.get('cards', []):
        if card.get('type') == 'Land':
            name = card.get('name', '')
            if name not in all_land_cards:
                all_land_cards[name] = []
            all_land_cards[name].append((fname, card))

# Re-analyze missing
pc_regions_by_deck = {}
land_names_by_deck = {}
for fname, data in decks.items():
    pc_regions_by_deck[fname] = set()
    land_names_by_deck[fname] = set()
    for card in data.get('cards', []):
        region = card.get('region', '')
        name = card.get('name', '')
        if card.get('type') == 'PC' and region:
            pc_regions_by_deck[fname].add(region)
        if card.get('type') == 'Land':
            land_names_by_deck[fname].add(name)

missing = {}
for fname, pc_regions in pc_regions_by_deck.items():
    if not pc_regions:
        continue
    land_names = land_names_by_deck.get(fname, set())
    missing_regions = pc_regions - land_names
    if missing_regions:
        missing[fname] = missing_regions

# For each missing region, find the original (non-reference) owner
print('=== Original Land card owners for missing regions ===')
for fname, regions in sorted(missing.items()):
    for region in sorted(regions):
        owners = all_land_cards.get(region, [])
        if not owners:
            print(f'{fname} missing {region}: NO LAND CARD EXISTS')
            continue
        # Find original (referenceDeckId empty)
        originals = [(f, c) for f, c in owners if not c.get('referenceDeckId')]
        if originals:
            orig = originals[0]
            print(f'{fname} missing {region}: original in {orig[0]} (cardId={orig[1].get("cardId")})')
        else:
            # All are references, pick the first one
            print(f'{fname} missing {region}: ALL REFERENCES, first in {owners[0][0]} (cardId={owners[0][1].get("cardId")})')
