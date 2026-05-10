import json
with open('Assets/Resources/Cards/Modular/GandalfBase.json') as f:
    d = json.load(f)
for c in d['cards']:
    if c.get('name') in ('Gandalf', 'Pippin', 'Bilbo'):
        print(c['name'])
        print('actionEffect:', c.get('actionEffect'))
        print()
