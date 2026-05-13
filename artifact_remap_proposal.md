# Artifact Remap Proposal — Deterministic Stat-Only Passives

## Goal
Eliminate all random-chance per-turn passives. Every artifact becomes a bundle of **deterministic stat changes** that are always on while held. No floating text spam needed — players see the effect in their character sheet, combat math, movement, and vision ranges.

## Proposed New Schema Fields

| Field | Type | Description |
|---|---|---|
| `commanderBonus` | int | Existing — flat skill bonus |
| `agentBonus` | int | Existing — flat skill bonus |
| `emmissaryBonus` | int | Existing — flat skill bonus |
| `mageBonus` | int | Existing — flat skill bonus |
| `bonusAttack` | int | Existing — generic duel attack + army attack |
| `bonusDefense` | int | Existing — generic duel defense + army defense |
| `healPerTurn` | int | Heal X health at the start of every turn (silent, deterministic) |
| `movementBonus` | int | +X movement points per turn |
| `ignoreTerrainMovementPenalty` | bool | Ignore slowing terrain (forest, mountain, swamp) |
| `grantsHasteAtSea` | bool | +1 movement on water (replaces old random HasteAtSea) |
| `autoScoutRadius` | int | Automatically reveals hexes within X radius |
| `detectionEvasion` | int | Enemies need +X extra scry/reveal range to detect this character or their hex |
| `attackBonusVsRace` | string | Race enum name (`Orc`, `Elf`, `Undead`, etc.) |
| `attackBonusVsRaceValue` | int | Bonus attack vs that race |
| `attackBonusVsTroopType` | string | Troop type (`ma`, `hi`, `lc`, `hc`, `ca`, `ws`, etc.) |
| `attackBonusVsTroopTypeValue` | int | Bonus attack vs that troop type |
| `defenseBonusVsRace` | string | Race enum name |
| `defenseBonusVsRaceValue` | int | Bonus defense vs that race |
| `defenseBonusVsTroopType` | string | Troop type |
| `defenseBonusVsTroopTypeValue` | int | Bonus defense vs that troop type |
| `armyAttackStrengthBonus` | int | Existing — flat +X to army attack score |
| `armyDefenseStrengthBonus` | int | Existing — flat +X to army defense score |
| `enemyArmyDefensePenaltySameHex` | int | Existing — −X enemy defense on same hex |
| `recruitBonusMenAtArms` | int | Recruit +X extra men-at-arms per recruit action |
| `scryAreaBonus` | int | +X range to Scry Area spell |
| `scryArtifactBonus` | int | +X success / −X difficulty to Find Artifact / Scry Artifact |
| `negativeStatusImmunity` | string | Immune to this status (`Burning`, `Poisoned`, `Fear`, `Despair`, `Halted`, `Blocked`) |
| `negativeStatusDurationReduction` | int | All negative statuses expire X turns sooner |
| `negativeStatusDamageReduction` | int | Reduce damage from Burning/Poisoned by X per turn |
| `positiveStatusDurationBonus` | int | All positive statuses last X extra turns |
| `positiveStatusEffectBonus` | int | Healing from Hope/Encouraged increased by X |
| `grantsEnvironmentalImmunity` | bool | Existing — immune to negative environmental cards |

---

## Full Mapping (60 unique artifacts)

### Dark Servant (alignment 1)

| Artifact | Class | Atk | Def | Heal | Move / Evasion | Vs Race / Troop | Army / Recruit | Scry | Status Modifiers |
|---|---|---|---|---|---|---|---|---|---|
| **Goblin Cleaver** | — | +1 | — | — | — | +1 atk vs `Orc` | — | — | — |
| **Elfbane** | — | +1 | +1 | — | — | +1 atk vs `Elf` | — | — | — |
| **Orcring** | cmd +1 | — | — | — | — | +1 atk vs `Common` (Men) | — | — | — |
| **Troll Cleaver** | — | +1 | — | — | — | +1 atk vs `Troll` | — | — | — |
| **Voice of the Dark Tower** | em +1 | — | — | — | detectionEvasion +1 | — | — | — | Immune `Fear` |
| **Usriev** | — | +1 | — | — | — | +1 atk vs `hi` (Heavy Infantry) | army atk +1 | — | — |
| **Cloak of Duvorn** | em +2 | — | — | — | detectionEvasion +1 | — | — | — | Immune `Despair` |
| **Storm dagger** | mage +1 | +1 | — | — | — | +1 atk vs `Elf` | — | — | — |
| **Ghostbane** | — | +1 | +1 | — | — | +1 atk / +1 def vs `Undead` | — | — | — |
| **Horn of Fear** | — | +1 | — | — | — | +1 atk vs `li` (Light Infantry) | enemy def −1 same hex | — | — |
| **Dawnsword** | — | +1 | — | — | — | — | — | — | `EnvironmentalImmunity` |
| **The Black Book** | mage +1 | +1 | — | — | detectionEvasion +1 | +1 atk vs `Elf` | — | — | Immune `Despair` |

### Free People (alignment 2)

| Artifact | Class | Atk | Def | Heal | Move / Evasion | Vs Race / Troop | Army / Recruit | Scry | Status Modifiers |
|---|---|---|---|---|---|---|---|---|---|
| **River Lillies** | mage +1 | — | — | +1 | — | — | — | — | Hope duration +1 |
| **Oak Shield** | — | — | +1 | — | — | +1 def vs `Orc` | — | — | — |
| **Ring of Wind** | agent +1 | — | — | — | movement +1 | — | — | — | — |
| **Staff of Storms** | mage +1 | — | — | — | `grantsHasteAtSea` | — | — | — | — |
| **Palantir of Annuminas** | mage +1 | — | — | — | autoScoutRadius +1 | — | — | scryArea +2, scryArtifact +10 | — |
| **Palantir of Amon Sul** | agent +1 | — | — | — | autoScoutRadius +1 | — | — | scryArea +2, scryArtifact +10 | — |
| **Bracers of the Mist** | agent +1 | — | +1 | — | detectionEvasion +1 | — | — | — | Immune `Poisoned` |
| **Horse-tamer** | cmd +1 | — | — | — | — | +1 atk vs `hc` (Heavy Cavalry) | recruitMA +1 | — | — |
| **Staff of the Wanderer** | agent +1 | — | — | — | movement +1 | — | — | — | — |
| **Red Robes** | mage +1 | — | — | — | — | — | — | — | Positive status duration +1 |
| **Mantle of Doriath** | — | — | +1 | — | detectionEvasion +2 | — | — | — | — |
| **Ovir Crown** | cmd +1 | — | +1 | — | — | — | — | — | — |
| **Listening Helm** | agent +1 | — | — | — | autoScoutRadius +1 | — | — | scryArea +1 | — |
| **Wine** | em +1 | — | — | — | — | — | — | — | Negative duration −1 |
| **Trap** | — | — | +1 | — | — | +1 def vs `li` (Light Infantry) | army def +1 | — | — |
| **Staff of Light** | mage +1 | — | — | — | — | — | — | — | Hope / Courage duration +1 |
| **Fireworks** | em +1 | — | — | — | — | — | — | — | Positive status duration +1 |
| **Staff of Fire** | mage +1 | — | — | — | — | — | — | — | Immune `Burning` |
| **Dorwinion Tobacco** | agent +1 | — | — | — | — | — | — | — | Hope duration +1 |
| **Old Tobby** | cmd +1 | — | — | — | — | — | — | — | Courage duration +1 |
| **Andúril** | cmd +1 | +1 | — | — | — | +1 atk vs `Orc` | — | — | Courage duration +1 |
| **The Arkenstone** | em +1, cmd +1 | — | +1 | — | — | +1 def vs `Orc` | — | — | — |
| **Mathom** | em +1 | — | — | — | — | — | recruitMA +1 | — | — |
| **Crown of Cardolan** | cmd +1 | — | — | — | — | +1 def vs `Orc` | army atk +1 | — | — |
| **Cardolan Seal** | agent +1 | — | — | — | — | — | — | scryArtifact +15 | — |
| **Seal of Dawn** | cmd +1 | — | — | +1 | — | — | — | — | Immune `Despair`, negative duration −1 |

### Neutral (alignment 0)

| Artifact | Class | Atk | Def | Heal | Move / Evasion | Vs Race / Troop | Army / Recruit | Scry | Status Modifiers |
|---|---|---|---|---|---|---|---|---|---|
| **Tinculin** | mage +1 | — | — | — | — | — | — | scryArea +1 | — |
| **Helm of Isildur** | cmd +1 | — | +1 | — | — | — | — | — | — |
| **Durin's Armour** | — | — | +1 | — | — | +1 def vs `Orc` | — | — | — |
| **Durin's Axe** | cmd +1 | +1 | — | — | — | +1 atk / +1 def vs `Orc` | — | — | — |
| **The Blue Ring** | mage +1 | — | +1 | — | detectionEvasion +1 | +1 def vs `DarkServant` races | — | — | — |
| **Athelas** | mage +1 | — | — | +1 | — | — | — | — | Immune `Poisoned`, negative damage −1 |
| **Elven Rope** | agent +1 | — | — | — | movement +1 in forests; always `Hidden` in forests | — | — | — | — |
| **Song** | em +1 | +1 | — | — | — | +1 atk vs `Orc` | — | — | — |
| **Red Book of Westmarch** | cmd +1 | — | — | — | — | — | — | — | Courage duration +1 |
| **Book of Kings** | em +1 | — | +1 | — | — | +1 def vs `Orc` | — | — | — |
| **Book of Mazarbul** | cmd +1 | +1 | — | — | — | +1 atk vs `Goblin` | — | — | — |
| **Dwarven Key** | agent +1 | — | — | — | — | — | — | scryArtifact +10 | — |
| **Ithildin Runes** | mage +1 | — | — | — | — | — | — | scryArea +1, scryArtifact +5 | — |
| **Ring of Binding** | agent +1 | +1 | — | — | detectionEvasion +1 | +1 atk vs `DarkServant` races | — | — | — |
| **Second Age Banner** | cmd +1 | — | — | — | — | +1 atk vs `hc` (Heavy Cavalry) | army atk +1 | — | — |
| **First Age Banner** | cmd +1 | — | — | — | — | +1 def vs `lc` (Light Cavalry) | army def +1 | — | — |
| **Black Powder** | — | +1 | — | — | — | +1 atk vs `ca` (Catapults) | — | — | — |
| **Staff of Secret Fire** | mage +1 | — | — | — | — | — | — | — | Immune `Burning` |
| **Star Powder** | mage +1 | — | — | — | — | — | — | — | Hope duration +1 |
| **Black Arrow** | — | +1 | — | — | — | +1 atk vs `Dragon` / `Beast` | — | — | — |
| **TheMirrorOfGaladriel** | mage +1 | — | — | — | autoScoutRadius +1 | — | — | scryArea +2, scryArtifact +10 | — |
| **Liquour** | em +1 | — | — | — | — | — | — | — | Immune `Fear` |

---

## Notes on Duplicates

The current JSON has **5× Athelas** and **2× Elfbane**. Under this remap:
- **Athelas ×5** would all be identical (`mage +1`, `healPerTurn +1`, immune `Poisoned`, negative damage −1). You may want to split them into themed variants (e.g. Athelas, Elanor, Niphredil, Athelas of Ithilien, Athelas of the Shire) with the same stats but different sprites.
- **Elfbane ×2** would both be identical (`+1 atk, +1 def, +1 atk vs Elf`). You could make the second one **Orc-bane** or **Troll-bane** to increase variety.

---

## What Gets Removed

These random per-turn passives are **replaced entirely** by the deterministic stats above:

| Old Passive | New Equivalent (or dropped) |
|---|---|
| `HealPerTurn` (Athelas) | → Kept as deterministic `healPerTurn +1` |
| `HopeChancePerTurn` | → Hope duration +1 on Tobacco / Star Powder / Lillies |
| `HasteAtSea` | → `grantsHasteAtSea` bool (Staff of Storms) |
| `RandomHexRevealChancePerTurn` | → `autoScoutRadius +1` + `scryArea +2` (Palantíri, Mirror) |
| `MountsChancePerTurn` | → Dropped (Horse-tamer gets recruitMA +1 instead) |
| `HasteChancePerTurn` | → `movement +1` (Ring of Wind, Staff of Wanderer) |
| `GoldChancePerTurn` | → Dropped (Red Robes / Mathom get other bonuses) |
| `HideOccupiedPcWhilePresent` | → `detectionEvasion +2` (Mantle of Doriath) |
| `BlockEnemyCharactersOnHex` | → Dropped (Trap gets def +1 vs Light Infantry instead) |
| `ForestHiddenChancePerTurn` | → Always `Hidden` in forests (Elven Rope) |
| `AlliedPcMoraleChancePerTurn` | → Dropped (Fireworks gets positive status duration +1) |
| `HexEnemyFearChancePerTurn` | → Dropped (Ghostbane gets +1 atk/def vs Undead) |
| `HexEnemyDespairChancePerTurn` | → Dropped (Black Book gets immune Despair + atk vs Elf) |
| `HexEnemyFearAndDespairChancePerTurn` | → Dropped (Ghostbane gets +1 atk/def vs Undead) |
| `SelfDespairChancePerTurn` | → Dropped (Arkenstone loses self-despair penalty) |
| `ArkenstoneGoldAndDespair` | → Simplified to em +1, cmd +1, def +1 vs Orc (no self-penalty) |
| `SelfFearAndDespairCleanseChancePerTurn` | → Negative duration −1 (Seal of Dawn, Wine) |
| `HexEnemyBurningChancePerTurn` | → Dropped (Staff of Fire gets immune Burning) |
| `HexEnemyHaltChancePerTurn` | → Dropped (Staff of Secret Fire gets immune Burning; Black Arrow gets +1 atk vs Beast) |
| `HexEnemyPoisonChancePerTurn` | → Dropped (Bracers get immune Poisoned) |
| `FreePeopleNonMenHaltChancePerTurn` | → Dropped (Song gets +1 atk vs Orc) |
| `EncouragedChancePerTurn` | → Courage duration +1 (Andúril, Old Tobby) |
| `LiquourCourageAndSleep` | → Immune Fear (Liquour) — no more random sleep |
| `BlockedSelfChancePerTurn` | → Dropped (no artifact had this as sole effect) |
| `ArmySuccessfulAttackBurningChance` | → Dropped (Black Powder gets +1 atk vs Catapults) |
| `FindArtifactDifficultyReduction` | → `scryArtifact +15` (Cardolan Seal) |
| `RevealHiddenEnemyPcOnOccupiedHex` | → `scryArtifact +10` (Dwarven Key); `scryArea +1, scryArtifact +5` (Ithildin Runes) |

---

## Summary of Category Coverage

| Category | Artifacts Using It |
|---|---|
| Class levels | ~35 artifacts (most of them) |
| Heal per turn | Athelas ×5, River Lillies, Seal of Dawn (7 total) |
| Movement | Ring of Wind, Staff of Wanderer, Elven Rope, Staff of Storms |
| Detection evasion | Mantle of Doriath (+2), Blue Ring, Bracers, Ring of Binding, Cloak of Duvorn, Voice of Dark Tower, Black Book (+1 each) |
| Auto-scout | Palantíri ×2, Mirror, Listening Helm |
| Generic attack | ~15 artifacts |
| Generic defense | ~15 artifacts |
| Attack vs Race | Goblin Cleaver, Elfbane, Orcring, Troll Cleaver, Storm dagger, Ghostbane, The Black Book, Andúril, Song, Book of Mazarbul, Ring of Binding, Durin's Axe, Oak Shield, The Arkenstone |
| Attack vs Troop Type | Usriev (hi), Horn of Fear (li), Horse-tamer (hc), Black Powder (ca), Second Age Banner (hc), Trap (li) |
| Defense vs Race | Oak Shield, Durin's Armour, Ghostbane, Durin's Axe, The Blue Ring, Book of Kings, Crown of Cardolan, The Arkenstone, Seal of Dawn |
| Defense vs Troop Type | Trap (li), First Age Banner (lc) |
| Army attack bonus | Usriev, Second Age Banner, Crown of Cardolan |
| Army defense bonus | Trap, First Age Banner |
| Enemy def penalty | Horn of Fear |
| Recruit MA bonus | Horse-tamer, Mathom |
| Scry area | Palantíri ×2, Mirror, Ithildin Runes, Listening Helm, Tinculin |
| Scry artifact | Palantíri ×2, Mirror, Cardolan Seal, Dwarven Key, Ithildin Runes |
| Status immunity | Voice of Dark Tower (Fear), Cloak of Duvorn (Despair), Dawnsword (environment), The Black Book (Despair), Bracers (Poisoned), Staff of Fire (Burning), Seal of Dawn (Despair), Athelas (Poisoned), Staff of Secret Fire (Burning), Liquour (Fear) |
| Negative duration reduction | Wine (−1), Seal of Dawn (−1) |
| Negative damage reduction | Athelas (−1) |
| Positive duration bonus | River Lillies, Red Robes, Fireworks, Staff of Light, Dorwinion Tobacco, Old Tobby, Andúril, Red Book of Westmarch, Star Powder |
