---
name: deck-stats
description: Regenerate the RetroLOTR deck stats JSON (economy/deck_stats.json) that summarises resource costs, resource production, character levels, and army attack/defense for every modular deck. Run this skill whenever cards are added, removed, or modified in any deck under Assets/Resources/Cards/Modular.
---

# Deck Stats

Regenerate `economy/deck_stats.json` from the current card data.

## When To Use

Run this skill after any of the following:
- Cards added to or removed from any modular deck JSON
- Resource costs or grants changed on any card
- Character level fields changed on any Character card
- Army troop type changed on any Army card
- A new subdeck is created

## Script

```powershell
python economy/gen_deck_stats.py
```

Output is written to `economy/deck_stats.json`.

## What The Script Computes (per deck)

| Field | Source cards | Notes |
|---|---|---|
| `resources.required` | All cards | Sum of `*Required` fields (leather, mounts, timber, iron, steel, mithril, gold, joker) |
| `resources.produced` | PC and Land cards only | Sum of `*Granted` fields |
| `characterLevels` | Character cards only | Sum of `commander`, `agent`, `emmissary`, `mage` level fields |
| `armyStats` | Army cards only | Attack and defense derived from `TroopsTypeEnum` values in `Assets/Data/Enums/TroopsTypeEnum.cs` |

## Troop Type Values (from ArmyData)

| Type | Attack | Defense |
|---|---|---|
| ma | 1 | 1 |
| ar | 2 | 1 |
| li | 2 | 2 |
| hi | 3 | 3 |
| lc | 3 | 2 |
| hc | 4 | 3 |
| ca | 2 | 1 |
| ws | 0 | 0 |

## Source Files

- Script: `economy/gen_deck_stats.py`
- Output: `economy/deck_stats.json`
- Deck manifiest: `Assets/Resources/Cards/Modular/manifest.json`
- Deck JSONs: `Assets/Resources/Cards/Modular/*.json`
- Troop stats reference: `Assets/Data/Enums/TroopsTypeEnum.cs`
