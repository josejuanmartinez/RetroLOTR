import json, os

seen = set()
for fname in os.listdir('Assets/Resources/Cards/Modular'):
    if not fname.endswith('.json'): continue
    with open(f'Assets/Resources/Cards/Modular/{fname}', 'r', encoding='utf-8-sig') as f:
        data = json.load(f)
    for card in data.get('cards', []):
        if card.get('type') == 'Character':
            key = f"{card['name']} -> race={card['race']}"
            if key not in seen:
                seen.add(key)
                print(key)
