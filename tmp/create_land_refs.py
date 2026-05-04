import json, glob, os, copy

# Load all decks
decks = {}
deck_paths = {}
for path in glob.glob('Assets/Resources/Cards/Modular/*.json'):
    fname = os.path.basename(path)
    with open(path, 'r', encoding='utf-8') as f:
        data = json.load(f)
    decks[fname] = data
    deck_paths[fname] = path

all_land_cards = {}
for fname, data in decks.items():
    for card in data.get('cards', []):
        if card.get('type') == 'Land':
            name = card.get('name', '')
            if name not in all_land_cards:
                all_land_cards[name] = []
            all_land_cards[name].append((fname, card))

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

# Find max cardId globally
max_card_id = 0
for fname, data in decks.items():
    for card in data.get('cards', []):
        max_card_id = max(max_card_id, card.get('cardId', 0))

print(f'Current max cardId: {max_card_id}')

# Deck ID mapping from manifest
manifest_id_to_file = {
    'gandalf_base': 'GandalfBase.json',
    'mithrandir': 'Mithrandir.json',
    'stormcrow': 'Stormcrow.json',
    'disturber_of_the_peace': 'DisturberOfThePeace.json',
    'tharkun': 'Tharkun.json',
    'gandalf_the_white': 'GandalfTheWhite.json',
    'saruman_base': 'SarumanBase.json',
    'saruman_the_white': 'SarumanTheWhite.json',
    'the_white_hand': 'TheWhiteHand.json',
    'of_many_colours': 'OfManyColours.json',
    'sauron_base': 'SauronBase.json',
    'the_dark_eye': 'TheDarkEye.json',
    'the_deceiver': 'TheDeceiver.json',
    'the_necromancer': 'TheNecromancer.json',
    'shadow_of_the_east': 'ShadowOfTheEast.json',
    'the_iron_crown': 'TheIronCrown.json',
}

# Reverse mapping
file_to_deck_id = {}
for fname, data in decks.items():
    if data.get('cards'):
        file_to_deck_id[fname] = data['cards'][0].get('deckId', '')

# Mapping from file to manifest-style id
file_to_manifest_id = {}
for manifest_id, manifest_file in manifest_id_to_file.items():
    file_to_manifest_id[manifest_file] = manifest_id

changes = []

for fname, regions in sorted(missing.items()):
    target_deck_id = file_to_deck_id.get(fname, '')
    for region in sorted(regions):
        owners = all_land_cards.get(region, [])
        if not owners:
            print(f'SKIP: {fname} missing {region} - no Land card exists anywhere')
            continue
        
        # Find original (referenceDeckId empty)
        originals = [(f, c) for f, c in owners if not c.get('referenceDeckId')]
        if originals:
            source_file, source_card = originals[0]
        else:
            source_file, source_card = owners[0]
        
        max_card_id += 1
        new_card = copy.deepcopy(source_card)
        new_card['cardId'] = max_card_id
        new_card['deckId'] = target_deck_id
        new_card['referenceDeckId'] = file_to_deck_id.get(source_file, '')
        new_card['referenceCardId'] = source_card['cardId']
        
        # Insert into target deck
        decks[fname]['cards'].append(new_card)
        
        changes.append({
            'target': fname,
            'region': region,
            'new_card_id': max_card_id,
            'source': source_file,
            'source_card_id': source_card['cardId'],
        })
        print(f'CREATED: {fname} <- {region} (newId={max_card_id}, from {source_file} id={source_card["cardId"]})')

print(f'\nTotal new reference cards created: {len(changes)}')

# Save all modified decks
for fname, data in decks.items():
    if any(c['target'] == fname for c in changes):
        with open(deck_paths[fname], 'w', encoding='utf-8') as f:
            json.dump(data, f, indent=4, ensure_ascii=False)
        print(f'SAVED: {fname}')
