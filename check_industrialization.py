import json
with open('Assets/Resources/Cards/Modular/Sharkey.json', encoding='utf-8') as f:
    data = json.load(f)
for c in data['cards']:
    if c['cardId'] == 9079:
        print(f"actionEffect: '{c.get('actionEffect','')}'")
        print(f"actionClassName: '{c.get('actionClassName','')}'")
