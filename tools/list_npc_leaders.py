import json

with open('Assets/Resources/NonPlayableLeaderBiomes.json', 'r', encoding='utf-8') as f:
    data = json.load(f)

for i, biome in enumerate(data.get('biomes', [])):
    print(f"{i+1}. {biome.get('characterName', '???')} | "
          f"region={biome.get('startingCityRegion', '???')} | "
          f"alignment={biome.get('alignment', '???')} | "
          f"race={biome.get('race', '???')} | "
          f"city={biome.get('startingCityName', '???')}")
