import json
with open('Assets/Resources/Cards/Modular/DisturberOfThePeace.json') as f:
    d = json.load(f)
for c in d['cards']:
    if c.get('name') == 'Tom Bombadil':
        print(json.dumps(c, indent=2))
        break
