#!/usr/bin/env python3
"""
Migrate all inline artifact definitions in JSON files to Artifacts.json catalog references.
- Converts inline Artifact object arrays to simple name-string arrays in biome/card JSONs
- Adds any missing artifacts to Artifacts.json with best-effort deterministic field mapping
"""

import json
import os
import re
from collections import OrderedDict

# Mapping from old passiveEffectId to new deterministic fields
PASSIVE_MAP = {
    "HealPerTurn":              {"healPerTurn": 2},
    "RandomHexRevealChancePerTurn": {"autoScoutRadius": 1},
    "FindArtifactDifficultyReduction": {"scryArtifactBonus": 25},
    "HasteChancePerTurn":       {"movementBonus": 1},
    "HopeChancePerTurn":        {"positiveStatusDurationBonus": 1},
    "RevealHiddenEnemyPcOnOccupiedHex": {"autoScoutRadius": 1},
    "HexEnemyFearAndDespairChancePerTurn": {"enemyArmyDefensePenaltySameHex": 1},
    "HexEnemyHaltChancePerTurn": {"enemyArmyDefensePenaltySameHex": 1},
    "HexEnemyPoisonChancePerTurn": {"negativeStatusImmunity": "Poisoned"},
    "EncouragedChancePerTurn":  {"positiveStatusDurationBonus": 1},
    "MountsChancePerTurn":      {"movementBonus": 1},
    "HexEnemyFearChancePerTurn": {"enemyArmyDefensePenaltySameHex": 1},
}

ARTIFACTS_JSON = "Assets/Resources/Artifacts.json"

def load_json(path):
    with open(path, "r", encoding="utf-8") as f:
        return json.load(f)

def save_json(path, data):
    with open(path, "w", encoding="utf-8") as f:
        json.dump(data, f, indent=4, ensure_ascii=False)
        f.write("\n")

def make_artifact_entry(src):
    """Build a full artifact entry from an inline artifact object."""
    entry = OrderedDict()
    entry["artifactName"] = src.get("artifactName", "")
    entry["hidden"] = src.get("hidden", False)
    entry["alignment"] = src.get("alignment", 0)
    entry["transferable"] = src.get("transferable", True)
    entry["spriteString"] = src.get("spriteString", "")
    entry["commanderBonus"] = src.get("commanderBonus", 0)
    entry["agentBonus"] = src.get("agentBonus", 0)
    entry["emmissaryBonus"] = src.get("emmissaryBonus", 0)
    entry["mageBonus"] = src.get("mageBonus", 0)
    entry["bonusAttack"] = src.get("bonusAttack", 0)
    entry["bonusDefense"] = src.get("bonusDefense", 0)

    # Start with zero new fields
    entry["healPerTurn"] = 0
    entry["movementBonus"] = 0
    entry["ignoreTerrainMovementPenalty"] = False
    entry["grantsHasteAtSea"] = False
    entry["autoScoutRadius"] = 0
    entry["detectionEvasion"] = 0
    entry["attackBonusVsRace"] = ""
    entry["attackBonusVsRaceValue"] = 0
    entry["attackBonusVsTroopType"] = ""
    entry["attackBonusVsTroopTypeValue"] = 0
    entry["defenseBonusVsRace"] = ""
    entry["defenseBonusVsRaceValue"] = 0
    entry["defenseBonusVsTroopType"] = ""
    entry["defenseBonusVsTroopTypeValue"] = 0
    entry["armyAttackStrengthBonus"] = 0
    entry["armyDefenseStrengthBonus"] = 0
    entry["enemyArmyDefensePenaltySameHex"] = 0
    entry["recruitBonusMenAtArms"] = 0
    entry["scryAreaBonus"] = 0
    entry["scryArtifactBonus"] = 0
    entry["negativeStatusImmunity"] = ""
    entry["negativeStatusDurationReduction"] = 0
    entry["negativeStatusDamageReduction"] = 0
    entry["positiveStatusDurationBonus"] = 0
    entry["positiveStatusEffectBonus"] = 0
    entry["grantsEnvironmentalImmunity"] = False
    entry["passiveEffectId"] = ""
    entry["passiveEffectValue"] = 0

    # Apply deterministic mapping from old passive
    pid = src.get("passiveEffectId", "")
    pval = src.get("passiveEffectValue", 0)
    if pid and pid in PASSIVE_MAP:
        for k, v in PASSIVE_MAP[pid].items():
            entry[k] = v

    return entry

def process_artifacts_in_data(data, missing):
    """Recursively find 'artifacts' arrays and convert them. Returns modified data."""
    if isinstance(data, dict):
        new_data = OrderedDict()
        for k, v in data.items():
            if k == "artifacts" and isinstance(v, list) and len(v) > 0 and isinstance(v[0], dict):
                # Inline artifact array found
                names = []
                for art in v:
                    if isinstance(art, dict):
                        name = art.get("artifactName", "")
                        if name:
                            names.append(name)
                            key = name.lower()
                            if key not in missing:
                                missing[key] = art
                new_data[k] = names
            else:
                new_data[k] = process_artifacts_in_data(v, missing)
        return new_data
    elif isinstance(data, list):
        return [process_artifacts_in_data(item, missing) for item in data]
    else:
        return data

def main():
    # Load existing Artifacts.json
    artifacts_data = load_json(ARTIFACTS_JSON)
    existing_names = {a["artifactName"].lower() for a in artifacts_data.get("artifacts", [])}

    missing = OrderedDict()  # key: lower name, value: inline artifact dict

    # Scan all JSON files in Assets/Resources
    json_files = []
    for root, dirs, files in os.walk("Assets/Resources"):
        for f in files:
            if f.endswith(".json"):
                json_files.append(os.path.join(root, f))

    files_to_modify = []
    for path in json_files:
        if path.endswith("Artifacts.json"):
            continue
        with open(path, "r", encoding="utf-8") as f:
            text = f.read()
        if re.search(r'"artifacts"\s*:\s*\[\s*\{', text):
            files_to_modify.append(path)

    print(f"Found {len(files_to_modify)} files with inline artifacts:")
    for p in files_to_modify:
        print(f"  {p}")

    # Process each file
    for path in files_to_modify:
        data = load_json(path)
        modified = process_artifacts_in_data(data, missing)
        save_json(path, modified)
        print(f"  Updated: {path}")

    # Add missing artifacts to Artifacts.json
    added = 0
    for key, art in missing.items():
        if key in existing_names:
            continue
        entry = make_artifact_entry(art)
        artifacts_data["artifacts"].append(entry)
        existing_names.add(key)
        added += 1
        print(f"  Added artifact: {entry['artifactName']}")

    save_json(ARTIFACTS_JSON, artifacts_data)
    print(f"\nDone. Added {added} new artifacts to Artifacts.json.")
    print(f"Total artifacts in catalog: {len(artifacts_data['artifacts'])}")

if __name__ == "__main__":
    main()
