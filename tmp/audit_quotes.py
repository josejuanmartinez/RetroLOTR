import json, glob, os

files = glob.glob('Assets/Resources/Cards/Modular/*.json')
exclude = {'SharedBase.json', 'ActionsDeck.json', 'SpellsDeck.json', 'manifest.json'}

needs_quote = []
for f in files:
    basename = os.path.basename(f)
    if basename in exclude:
        continue
    data = json.load(open(f, encoding='utf-8'))
    for c in data.get('cards', []):
        # Skip copies/references
        if c.get('referenceDeckId') and c.get('referenceCardId'):
            continue
        quote = c.get('quote', '')
        if not quote or quote.strip() == '':
            needs_quote.append({
                'file': f,
                'deck': data['deckId'],
                'cardId': c.get('cardId'),
                'name': c.get('name', ''),
                'type': c.get('type', ''),
                'region': c.get('region', ''),
                'action': c.get('action', ''),
                'actionEffect': c.get('actionEffect', '')
            })

print(f'Total original cards needing quotes: {len(needs_quote)}')
for item in needs_quote:
    print(f"{item['deck']} | {item['name']} ({item['type']}) id={item['cardId']}")
