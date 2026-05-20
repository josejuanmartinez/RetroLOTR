import json
with open('Assets/Resources/Cards/Modular/Sharkey.json', encoding='utf-8') as f:
    data = json.load(f)
for c in data['cards']:
    if c.get('type') == 'Army' or 'ruffian' in c.get('name','').lower() or 'ruffian' in c.get('spriteName','').lower():
        print(f"{c['cardId']} '{c['name']}' [{c['type']}] sprite='{c.get('spriteName','')}' troopType={c.get('troopType','')} ref={c.get('referenceDeckId','')}/{c.get('referenceCardId','')}")
