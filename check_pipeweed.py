import json
with open('Assets/Resources/Cards/Modular/Sharkey.json', encoding='utf-8') as f:
    data = json.load(f)
for c in data['cards']:
    if 'pipe' in c.get('name','').lower() or 'pipe' in c.get('spriteName','').lower():
        print(f"  {c['cardId']} '{c['name']}' [{c['type']}] actionClass='{c.get('actionClassName','')}' sprite='{c.get('spriteName','')}'")
