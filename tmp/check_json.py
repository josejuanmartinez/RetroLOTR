import json
with open(r'Assets/Resources/Cards/Modular/SarumanTheWhite.json', 'r', encoding='utf-8') as f:
    data = json.load(f)
cards = data.get('cards', [])
for card in cards:
    acn = card.get('actionClassName', '---')
    print(f"{card['cardId']:4d}: {card['name']:<30s} type={card['type']:<8s} actionClassName={acn}")
print('Cards:', len(cards))
