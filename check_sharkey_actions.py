import json
with open('Assets/Resources/Cards/Modular/Sharkey.json', encoding='utf-8') as f:
    data = json.load(f)
for c in data['cards']:
    if c['cardId'] >= 9060:
        inspire = c.get('inspireEffectData', {}).get('type', '')
        print(f"{c['cardId']} {c['name']} [{c['type']}] action='{c.get('actionClassName','')}' inspire='{inspire}'")
