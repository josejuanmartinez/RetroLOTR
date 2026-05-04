import json
with open('Assets/Resources/Cards/Modular/SarumanTheWhite.json', 'r') as f:
    data = json.load(f)
for card in data['cards']:
    if card['cardId'] in [1033, 1035, 1036, 1037, 1038, 1040, 1041, 1042, 1043, 1044]:
        print(f"cardId={card['cardId']}, name={card['name']}, actionClassName={card['actionClassName']}, actionEffect={card['actionEffect'][:60]}...")
