import json
with open('Assets/Resources/Cards/Modular/TheNecromancer.json') as f:
    data = json.load(f)
cards = [c for c in data['cards'] if c.get('name')]
print('Non-empty cards:', len(cards))
print('Last few cardIds:', [c['cardId'] for c in data['cards'][-5:]])
print('Last few names:', [c['name'] for c in data['cards'][-5:]])
for deck in data.get('decks', []):
    if deck.get('deckId') == 'the_necromancer':
        print('Declared cardCount:', deck['cardCount'])
