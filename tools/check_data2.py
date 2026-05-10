import json
with open('Assets/Resources/Cards/Modular/TheDarkEye.json') as f:
    d = json.load(f)
for c in d['cards']:
    if c.get('name') in ('Murazor', 'Mouth of Sauron', 'Ji Indur'):
        print(c['name'])
        print(json.dumps(c.get('inspireEffectData'), indent=2))
        print('actionEffect:', c.get('actionEffect'))
        print()
