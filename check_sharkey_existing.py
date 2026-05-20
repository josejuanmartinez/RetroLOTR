import json
with open('Assets/Resources/Cards/Modular/Sharkey.json', encoding='utf-8') as f:
    data = json.load(f)
names = ["Lotho's Purse", "LothosPurse", "Pipeweed Monopoly", "PipeweedMonopoly", "The Old Mill", "Imprisonment", "Industrialization"]
for c in data['cards']:
    name = c.get('name','')
    if any(n.lower() in name.lower() for n in names):
        print(f"  {c['cardId']} '{c['name']}' [{c['type']}] actionClass='{c.get('actionClassName','')}' sprite='{c.get('spriteName','')}'")
