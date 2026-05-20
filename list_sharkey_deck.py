import json
with open('Assets/Resources/Cards/Modular/Sharkey.json', encoding='utf-8') as f:
    data = json.load(f)
print(f"deckId: {data.get('deckId','')}")
print(f"Total cards: {len(data['cards'])}\n")
for i, c in enumerate(data['cards']):
    print(f"  [{i:02d}] {c['cardId']} '{c['name']}' [{c['type']}] x{c.get('amount',1)}")
