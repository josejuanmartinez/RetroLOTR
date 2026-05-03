import json

with open('Assets/Resources/Cards.json', 'r', encoding='utf-8') as f:
    data = json.load(f)

for deck in data.get('decks', []):
    if deck.get('deckId') == 'the_necromancer':
        deck['cardCount'] = 57
        print('Updated the_necromancer cardCount to', deck['cardCount'])
        break

with open('Assets/Resources/Cards.json', 'w', encoding='utf-8') as f:
    json.dump(data, f, indent=4, ensure_ascii=False)
