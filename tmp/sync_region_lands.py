import json, glob, os, copy

DECK_DIR = 'Assets/Resources/Cards/Modular'
files = sorted(glob.glob(f'{DECK_DIR}/*.json'))

exclude = {'SharedBase.json', 'ActionsDeck.json', 'SpellsDeck.json', 'manifest.json'}
decks = {}
for f in files:
    basename = os.path.basename(f)
    if basename in exclude:
        continue
    with open(f, 'r', encoding='utf-8') as fh:
        decks[f] = json.load(fh)

# Build land map: normalized name -> list of (src_deck_id, src_card_id, card_data)
land_map = {}
for f, data in decks.items():
    for c in data.get('cards', []):
        if c.get('type') == 'Land':
            name = c.get('name', '')
            if name:
                norm = name.replace(' ', '')
                land_map.setdefault(name, []).append((data['deckId'], c['cardId'], c))
                if norm != name:
                    land_map.setdefault(norm, []).append((data['deckId'], c['cardId'], c))

def deck_has_land(data, region):
    norm_region = region.replace(' ', '')
    for c in data.get('cards', []):
        if c.get('type') == 'Land':
            name = c.get('name', '')
            if name == region or name.replace(' ', '') == norm_region:
                return True
    return False

max_ids = {}
for f, data in decks.items():
    max_ids[f] = max((c.get('cardId', 0) for c in data.get('cards', [])), default=0)

copies = []
seen = set()
unresolved = []
for f, data in decks.items():
    deck_id = data['deckId']
    for c in data.get('cards', []):
        if c.get('type') == 'PC':
            region = c.get('region', '')
            if not region:
                continue
            if deck_has_land(data, region):
                continue
            norm_region = region.replace(' ', '')
            candidates = land_map.get(region, []) or land_map.get(norm_region, [])
            if candidates:
                src_deck_id, src_card_id, src_card = candidates[0]
                key = (deck_id, region)
                if key not in seen:
                    seen.add(key)
                    copies.append((f, deck_id, region, src_deck_id, src_card_id, src_card))
            else:
                unresolved.append((deck_id, c.get('name',''), region))

print(f'Copies to create: {len(copies)}')
print(f'Unresolved: {len(unresolved)}')
for u in unresolved:
    print('  ', u)

# Apply copies
modified_files = set()
for target_file, target_deck_id, region, src_deck_id, src_card_id, src_card in copies:
    data = decks[target_file]
    new_card = copy.deepcopy(src_card)
    max_ids[target_file] += 1
    new_card['cardId'] = max_ids[target_file]
    new_card['deckId'] = target_deck_id
    new_card['referenceDeckId'] = src_deck_id
    new_card['referenceCardId'] = src_card_id
    data['cards'].append(new_card)
    modified_files.add(target_file)
    print('Added', new_card['name'], '(id', new_card['cardId'], ') to', target_deck_id, 'referencing', src_deck_id, '/', src_card_id)

# Write modified decks
for f in modified_files:
    with open(f, 'w', encoding='utf-8') as fh:
        json.dump(decks[f], fh, indent=4, ensure_ascii=False)
        fh.write('\n')
    print('Wrote', f)

# Update manifest
manifest_path = os.path.join(DECK_DIR, 'manifest.json')
with open(manifest_path, 'r', encoding='utf-8') as fh:
    manifest = json.load(fh)

deck_counts = {}
for f, data in decks.items():
    deck_counts[data['deckId']] = len(data.get('cards', []))

for entry in manifest.get('decks', []):
    deck_id = entry.get('id', '')
    for did, count in deck_counts.items():
        if did.lower() == deck_id.lower():
            entry['count'] = count
            print('Updated manifest count for', did, '->', count)
            break

with open(manifest_path, 'w', encoding='utf-8') as fh:
    json.dump(manifest, fh, indent=4, ensure_ascii=False)
    fh.write('\n')
print('Wrote manifest.json')
