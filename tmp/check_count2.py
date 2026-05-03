import json
with open('Assets/Resources/Cards/Modular/TheNecromancer.json') as f:
    data = json.load(f)
total = len(data['cards'])
non_empty = len([c for c in data['cards'] if c.get('name')])
empty = total - non_empty
print('Total entries:', total)
print('Non-empty:', non_empty)
print('Empty:', empty)
for deck in data.get('decks', []):
    if deck.get('deckId') == 'the_necromancer':
        print('Declared cardCount:', deck['cardCount'])
