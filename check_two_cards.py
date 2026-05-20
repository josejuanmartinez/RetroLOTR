import json
with open('Assets/Resources/Cards/Modular/Sharkey.json', encoding='utf-8') as f:
    data = json.load(f)
for c in data['cards']:
    if c['cardId'] in (9014, 9016):
        print(f"\n--- {c['cardId']} '{c['name']}' ---")
        print(f"  actionClassName: '{c.get('actionClassName','')}'")
        print(f"  actionEffect: '{c.get('actionEffect','')}'")
        print(f"  quote: '{c.get('quote','')[:80]}'")
