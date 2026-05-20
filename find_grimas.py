import json
with open('Assets/Resources/Cards/Modular/Sharkey.json', encoding='utf-8') as f:
    data = json.load(f)
for c in data['cards']:
    if 'grima' in c.get('name','').lower() or 'knife' in c.get('name','').lower():
        print(c)
