import json

for deck in ['ShadowOfTheEast.json', 'TheIronCrown.json']:
    with open(f'Assets/Resources/Cards/Modular/{deck}', 'r', encoding='utf-8-sig') as f:
        data = json.load(f)
    for card in data['cards']:
        if card['name'] in ['Shadow of the East', 'The Iron Crown']:
            print(f"{deck}: {card['name']} -> spriteName={card['spriteName']}, type={card['type']}")
