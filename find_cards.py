import json, os, glob

search = ["lure", "halfling", "senses"]
base = r"c:\Users\jjmca\RetroLOTR\Assets\Resources\Cards"
files = glob.glob(os.path.join(base, "*.json")) + glob.glob(os.path.join(base, "Modular", "*.json"))

for path in files:
    try:
        with open(path, encoding="utf-8") as f:
            data = json.load(f)
        cards = data.get("cards", [])
        for c in cards:
            name = c.get("name", "").lower()
            if any(s in name for s in search):
                print(f"  {os.path.basename(path)} | {c['cardId']} '{c['name']}' [{c.get('type','')}] sprite='{c.get('spriteName','')}'")
    except Exception as e:
        print(f"  Error {path}: {e}")
