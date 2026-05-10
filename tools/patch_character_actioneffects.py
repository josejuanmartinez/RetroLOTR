#!/usr/bin/env python3
"""
Patches Character cards in modular deck JSONs with:
- actionEffect      (human-readable description with sprites)
- inspireEffectData (structured effect parameters for runtime)

Reads the hardcoded C# registry once, writes the data into JSON,
then the registry can be deleted.
"""

import json
import os
import re
import sys

PROJECT_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
REGISTRY_PATH = os.path.join(PROJECT_ROOT, "Assets/Scripts/Actions/CharacterCardInspireEffectRegistry.cs")
MODULAR_DIR = os.path.join(PROJECT_ROOT, "Assets/Resources/Cards/Modular")


def sp(name):
    """Return a word with its matching sprite tag appended."""
    return f'{name} <sprite name="{name.lower()}">'


# ---------------------------------------------------------------------------
# Description generators (with sprites)
# ---------------------------------------------------------------------------

def describe_ApplyStatusEffectInspireEffect(effect, turns, all_chars=True):
    target = "all controlled characters" if all_chars else "a random controlled character"
    return f"Apply {sp(effect)} ({turns} turns) to {target}."

def describe_ClearStatusEffectInspireEffect(effect):
    return f"Clear {sp(effect)} from all controlled characters."

def describe_HealInspireEffect(amount, all_chars=True):
    target = "all controlled characters" if all_chars else "a random controlled character"
    return f"Heal {target} by {amount}."

def describe_IncreaseSkillInspireEffect(skill, amount, all_chars=False):
    target = "all controlled characters" if all_chars else "a random controlled character"
    return f"Increase {sp(skill)} by {amount} on {target}."

def describe_GainResourceInspireEffect(resource, amount):
    return f"Gain {amount} {sp(resource)}."

def describe_RecruitTroopsInspireEffect(troop_type, amount):
    return f"Recruit {amount} {sp(troop_type)} to a controlled army."

def describe_ArmyXpInspireEffect(xp):
    return f"Grant {xp} XP to all controlled armies."

def describe_RevealHexesInspireEffect(radius):
    return f"Reveal hexes in radius {radius} around all controlled characters."

def describe_RevealArtifactInspireEffect():
    return f"Reveal a hidden {sp('artifact')} on a visible hex."

def describe_IncreaseLoyaltyInspireEffect(amount):
    return f"Increase {sp('loyalty')} by {amount} on a random controlled settlement."

def describe_DecreaseLoyaltyInspireEffect(amount):
    return f"Decrease {sp('loyalty')} by {amount} on a visible enemy settlement."

def describe_ResetMovementInspireEffect(all_chars=True):
    target = "all controlled characters" if all_chars else "a random controlled character"
    return f"Restore full {sp('movement')} to {target}."

def describe_ResetActionInspireEffect(all_chars=True):
    target = "all controlled characters" if all_chars else "a random controlled character"
    return f"Restore {sp('action')} to {target}."

def describe_ResetMovementAndActionInspireEffect(all_chars=False):
    target = "all controlled characters" if all_chars else "a random controlled character"
    return f"Grant {target} a full extra turn."

def describe_EncourageInspireEffect(turns, all_chars=True):
    target = "all controlled characters" if all_chars else "a random controlled character"
    return f"Encourage <sprite name=\"encouraged\"> {target} for {turns} turn(s)."

def describe_FreeCaptivesInspireEffect():
    return "Free all controlled characters from captivity."

def describe_IncreaseFortInspireEffect():
    return f"Upgrade {sp('fort')} on a random controlled settlement."

def describe_DecreaseFortEnemyInspireEffect(nearest=True):
    target = "nearest visible enemy settlement" if nearest else "random visible enemy settlement"
    return f"Downgrade {sp('fort')} on the {target}."

def describe_CreatePortInspireEffect():
    return f"Build a {sp('port')} on a controlled settlement that lacks one."

def describe_SabotagePortInspireEffect(nearest=True):
    target = "nearest visible enemy settlement" if nearest else "random visible enemy settlement"
    return f"Sabotage the {sp('port')} of the {target}."

def describe_IncreaseCitySizeInspireEffect():
    return "Grow a random controlled settlement."

def describe_DecreaseCitySizeEnemyInspireEffect(nearest=True):
    target = "nearest visible enemy settlement" if nearest else "random visible enemy settlement"
    return f"Reduce the size of the {target}."

def describe_HidePCInspireEffect(turns=1):
    return f"Conceal a random controlled settlement from enemies for {turns} turn(s)."

def describe_RevealEnemyPCInspireEffect(turns=1, nearest=True):
    target = "nearest enemy settlement" if nearest else "random enemy settlement"
    return f"Reveal the {target} for {turns} turn(s)."

# ---------------------------------------------------------------------------
# Structured data builders
# ---------------------------------------------------------------------------

def build_inspire_data(class_name, params):
    args = [clean_param(p) for p in params]
    d = {"type": class_name.replace("InspireEffect", "")}
    
    if class_name == "ApplyStatusEffectInspireEffect":
        d["statusEffect"] = args[0]
        d["turns"] = args[1]
        d["allCharacters"] = args[2] if len(args) > 2 else True
    elif class_name == "ClearStatusEffectInspireEffect":
        d["statusEffect"] = args[0]
    elif class_name == "HealInspireEffect":
        d["amount"] = args[0]
        d["allCharacters"] = args[1] if len(args) > 1 else True
    elif class_name == "IncreaseSkillInspireEffect":
        d["skillType"] = args[0]
        d["amount"] = args[1]
        d["allCharacters"] = args[2] if len(args) > 2 else False
    elif class_name == "GainResourceInspireEffect":
        d["resourceType"] = args[0]
        d["amount"] = args[1]
    elif class_name == "RecruitTroopsInspireEffect":
        d["troopType"] = args[0]
        d["amount"] = args[1]
    elif class_name == "ArmyXpInspireEffect":
        d["amount"] = args[0]
    elif class_name == "RevealHexesInspireEffect":
        d["amount"] = args[0]
    elif class_name == "RevealArtifactInspireEffect":
        pass
    elif class_name == "IncreaseLoyaltyInspireEffect":
        d["amount"] = args[0]
    elif class_name == "DecreaseLoyaltyInspireEffect":
        d["amount"] = args[0]
    elif class_name == "ResetMovementInspireEffect":
        d["allCharacters"] = args[0] if len(args) > 0 else True
    elif class_name == "ResetActionInspireEffect":
        d["allCharacters"] = args[0] if len(args) > 0 else True
    elif class_name == "ResetMovementAndActionInspireEffect":
        d["allCharacters"] = args[0] if len(args) > 0 else False
    elif class_name == "EncourageInspireEffect":
        d["turns"] = args[0]
        d["allCharacters"] = args[1] if len(args) > 1 else True
    elif class_name == "FreeCaptivesInspireEffect":
        pass
    elif class_name == "IncreaseFortInspireEffect":
        pass
    elif class_name == "DecreaseFortEnemyInspireEffect":
        d["nearest"] = args[0] if len(args) > 0 else True
    elif class_name == "CreatePortInspireEffect":
        pass
    elif class_name == "SabotagePortInspireEffect":
        d["nearest"] = args[0] if len(args) > 0 else True
    elif class_name == "IncreaseCitySizeInspireEffect":
        pass
    elif class_name == "DecreaseCitySizeEnemyInspireEffect":
        d["nearest"] = args[0] if len(args) > 0 else True
    elif class_name == "HidePCInspireEffect":
        d["turns"] = args[0] if len(args) > 0 else 1
    elif class_name == "RevealEnemyPCInspireEffect":
        d["turns"] = args[0] if len(args) > 0 else 1
        d["nearest"] = args[1] if len(args) > 1 else True
    
    return d

# ---------------------------------------------------------------------------
# Parsing helpers
# ---------------------------------------------------------------------------

def clean_param(p):
    p = p.strip()
    for prefix in ("StatusEffectEnum.", "InspireResourceType.", "InspireSkillType.", "TroopsTypeEnum."):
        if p.startswith(prefix):
            return p.split(".")[-1]
    if p == "true":
        return True
    if p == "false":
        return False
    try:
        return int(p)
    except ValueError:
        return p


def parse_registry(path):
    entries = {}
    with open(path, "r", encoding="utf-8") as f:
        for line in f:
            m = re.match(
                r'^\s*\["([^"]+)"\]\s*=\s*\(\)\s*=>\s*new\s+(\w+)\((.*)\),?\s*$',
                line,
            )
            if m:
                name = m.group(1)
                class_name = m.group(2)
                params_str = m.group(3).strip()
                params = [p.strip() for p in params_str.split(",")] if params_str else []
                entries[name] = (class_name, params)
    return entries


def generate_description(class_name, params):
    args = [clean_param(p) for p in params]
    fn = globals().get(f"describe_{class_name}")
    if fn is None:
        return None
    try:
        return fn(*args)
    except TypeError as exc:
        print(f"  WARN: bad signature for {class_name}({args}): {exc}", file=sys.stderr)
        return None


def patch_modular_decks(entries):
    patched_files = 0
    patched_cards = 0

    for filename in sorted(os.listdir(MODULAR_DIR)):
        if not filename.endswith(".json"):
            continue
        path = os.path.join(MODULAR_DIR, filename)
        with open(path, "r", encoding="utf-8") as f:
            data = json.load(f)

        modified = False
        for card in data.get("cards", []):
            if card.get("type") != "Character":
                continue
            name = card.get("name")
            if name not in entries:
                continue
            class_name, params = entries[name]
            
            # Always regenerate actionEffect so sprites are injected
            desc = generate_description(class_name, params)
            if desc:
                card["actionEffect"] = desc
                modified = True
            
            # Patch inspireEffectData if missing
            if not card.get("inspireEffectData"):
                card["inspireEffectData"] = build_inspire_data(class_name, params)
                modified = True
            
            if modified:
                patched_cards += 1

        if modified:
            with open(path, "w", encoding="utf-8") as f:
                json.dump(data, f, indent=4, ensure_ascii=False)
                f.write("\n")
            patched_files += 1
            print(f"  patched {filename}")

    print(f"\nDone: {patched_cards} cards across {patched_files} files.")


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

def main():
    print("Parsing CharacterCardInspireEffectRegistry...")
    entries = parse_registry(REGISTRY_PATH)
    print(f"  found {len(entries)} registry entries")

    print("Patching modular deck JSONs...")
    patch_modular_decks(entries)


if __name__ == "__main__":
    main()
