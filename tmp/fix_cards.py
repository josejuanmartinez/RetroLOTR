import json

with open('Assets/Resources/Cards/Modular/SarumanTheWhite.json', 'r') as f:
    data = json.load(f)

for card in data['cards']:
    if card['cardId'] == 1036:
        card['actionEffect'] = 'Reveal as seen the hexes where Galadriel, Elrond, and Gandalf stand.'
    elif card['cardId'] == 1040:
        card['actionEffect'] = 'All mages at Orthanc gain Arcane Insight <sprite name="arcaneinsight"> (3 turns), but are Halted <sprite name="halted"> (1 turn).'
    elif card['cardId'] == 1042:
        card['name'] = 'Far Shore, Faded Star'
        card['quote'] = 'Go east, where the stars grow thin and no voice calls you back.'
        card['actionEffect'] = 'Allied units within 5 hexes of any map border gain Hope <sprite name="hope"> (3 turns) and Encouraged <sprite name="encouraged"> (3 turns).'
        card['actionClassName'] = 'FarShoreFadedStar'
        card['action'] = 'FarShoreFadedStar'
    elif card['cardId'] == 1043:
        card['actionEffect'] = 'Move the caster to a random hex on the map. Gain Arcane Insight <sprite name="arcaneinsight"> (3 turns).'
    elif card['cardId'] == 1044:
        card['actionEffect'] = 'Can only be played at Orthanc. All allied units in radius 1 become Hidden <sprite name="hidden"> (1 turn).'

with open('Assets/Resources/Cards/Modular/SarumanTheWhite.json', 'w') as f:
    json.dump(data, f, indent=4)

print("Done")
