# Improving Terrible Status-Effect Cards

**Audit date:** 2026-05-09  
**Total pure-status cards found:** 65 across 14 decks  
**Rule:** No card should be ONLY "apply X status to Y for Z turns." Every card needs a mechanic that couldn't be described as "buff/debuff a group."

---

## What counts as a violation

A card violates the rule if its entire `actionEffect` is one of these templates and nothing else:
- `"All [X] gain [Status] (N turns)."`
- `"Target allied character: gain [Status] (N turns)."`
- `"Enemy [X] gain [Status] (N turns)."`
- `"Apply [Status] (N turns) to [X]."`
- `"Grants [Status] to [X] for 1 turn."`

Cards that **combine** status with damage, resources, movement, reveals, card draw, or meaningful conditions are fine.

---

## Balance assessment — why removing 65 cards is safe

Before migrating, the status-effect landscape across all 21 modular decks:

| Category | Count |
|----------|-------|
| Pure-status cards being migrated away | 65 |
| Cards already combining status + other mechanics (kept as-is) | 114 |
| Cards with no status effects (pure damage/resource/reveal) | 125 |
| Code-defined cards (`actionEffect` empty — status presence unknown) | 786 |
| SpellsDeck cards (all code-defined, untouched) | 30 |

**Post-migration health check:**
- The 114 "combo" cards ensure status effects remain a meaningful part of every deck.
- The full 30-card SpellsDeck (Haste, Halt, Fear, Courage, Root, Curse, Teleport, Invisibility, etc.) is untouched — wizards retain rich status spell access.
- **8 of the 37 new mechanics in this plan include a status effect as a secondary component** (marked `(2°)` in the summary table). They stay flavorful while gaining real mechanics.
- The remaining 29 new mechanics are status-free — pure combat, movement, information, or resource effects. These are the ones that needed the most help.

**Conclusion:** After migration, status effects appear in roughly 122 of 239 non-code-defined cards (51%), plus an unknown share of 786 code-defined cards. Status effects stay central to gameplay; they just stop being the *only* thing a card does.

---

## Group A — Cross-deck duplicates (highest priority)

These card names appear in multiple decks with identical pure-status effects. Fix the logic once; it propagates across all copies.

---

### A1. "Mounts"
**Decks:** `gandalf_base` (id:446), `saruman_base` (id:447), `shadow_of_the_east` (id:1028)  
**Current:** `Target allied character in your hex: gain Haste (1 turn).`  
**Problem:** Single status, no interaction.

**New mechanic:**
> Your character immediately teleports to any allied hex within 2. If an allied army is in the destination hex, this character joins it this turn. Cannot be used while in combat.

**Why it's better:** Repositioning is a decision with real consequences; joining an army changes who you can fight with.

---

### A2. "Traps"
**Decks:** `gandalf_base` (id:496), `saruman_base` (id:497), `sauron_base` (id:498)  
**Current:** `Target enemy character: apply Blocked (1 turn) and Poisoned (2 turns).`  
**Problem:** Two statuses stacked — still just a debuff pair.

**New mechanic:**
> Place a trap in this hex. The first enemy character that enters or acts in this hex this turn takes 20 damage and loses their remaining movement. Traps are consumed on trigger.

**Why it's better:** Creates a spatial threat and a timing decision, not just "enemy is now worse."

---

### A3. "Well-equipped Army"
**Decks:** `gandalf_base` (id:504), `saruman_base` (id:506), `sauron_base` (id:505), `shadow_of_the_east` (id:1014)  
**Current:** `All allied armies in radius 2 gain Fortified (1 turn).`  
**Problem:** Mass buff, no decision.

**New mechanic:**
> Choose 1 allied army in your hex or an adjacent hex. Immediately convert 1 Men-at-arms unit in that army to Heavy Infantry. That army also gains +1 to its next attack roll.

**Why it's better:** Permanent-ish upgrade (unit conversion), affects one specific army, requires a targeting decision.

---

### A4. "Long Shadows"
**Decks:** `gandalf_base` (id:143), `saruman_base` (id:204), `sauron_base` (id:269)  
**Current:** `Grants Encouraged to all beasts for 1 turn.`  
**Problem:** Mass buff to one race type, no other effect.

**New mechanic:**
> All beast-type units in radius 4 may move again this turn. Enemy army commanders on forest tiles within radius 3 lose their scouting status — their surroundings become unseen. Beasts in radius 4 also gain Encouraged (1 turn).

**Why it's better:** Movement reset is more impactful than a status; the scouting penalty creates real information asymmetry. Encouraged here is a flavor accent on a card that already does two meaningful things.

---

### A5. "Raid From The Mountains"
**Decks:** `of_many_colours` (id:858), `the_white_hand` (id:718), `the_iron_crown` (id:1016), `the_necromancer` (id:1019), `the_dark_eye` (id:1015)  
**Current:** `All allied army commanders in mountains or hills gain Haste (1 turn) and Courage (1 turn).`  
**Problem:** Conditional double-status. The terrain condition is good but the outcome is still pure buffs.

**New mechanic:**
> Allied army commanders on mountain or hill tiles immediately move 1 hex toward the nearest enemy army and gain Haste (1 turn) for the charge. If they already share a hex, their army deals 1 automatic hit before combat resolution this turn. Does not cost movement.

**Why it's better:** Forces engagement, has direct combat consequence. Haste here is a secondary bonus that fits the charging-downhill theme without being the card's whole point.

---

### A6. "Under the Rhunic Sun"
**Decks:** `of_many_colours` (id:857), `shadow_of_the_east` (id:1021)  
**Current:** `All Easterling units gain Courage (1 turn).`  
**Problem:** Single race buff.

**New mechanic:**
> All Easterling PCs gain +10 loyalty. All Easterling army commanders reveal their current hex and adjacent hexes as scouted for 2 turns. Choose 1 Easterling army: it gains +1 Light Cavalry this turn. Easterlings currently on patrol tiles gain Courage (1 turn).

**Why it's better:** Loyalty change is persistent, scouting is an information effect, and the cavalry gain is a concrete resource bonus. Courage rewards Easterlings who are actively in the field — not a blanket buff.

---

### A7. "ElvesGoingWest"
**Decks:** `of_many_colours` (id:877), `the_deceiver` (id:1024), `the_white_hand` (id:927)  
**Current:** `Apply Despair (1 turn) to all elves in radius 2.`  
**Problem:** Pure mass debuff.

**New mechanic:**
> Choose 1 Elf character in radius 3. That character is compelled westward — immediately move them 1 hex toward the nearest coast. They gain Despair (1 turn) from the longing and cannot take offensive actions this turn. If they were Hidden, they lose Hidden.

**Why it's better:** Involuntary movement is the primary disruption; it repositions the enemy. Despair is a secondary consequence of the compulsion — thematically earned, not a standalone buff.

---

### A8. "A Mithril Coat"
**Decks:** `saruman_the_white` (id:807), `tharkun` (id:896), `gandalf_the_white` (id:1006)  
**Current:** `Target allied character: gain Fortified (3 turns).`  
**Problem:** Extended single-status.

**New mechanic:**
> Target allied character: they cannot be killed in a single hit for the next 3 turns (surviving any killing blow at 1 HP). While protected, they also cannot be targeted by Kidnap or Assassinate actions.

**Why it's better:** Actual survivability guarantee with a specific interaction, not just a defense multiplier.

---

### A9. "Northmen Stand Firm"
**Decks:** `stormcrow` (id:918), `saruman_the_white` (id:805)  
**Current:** `Target allied character: gain Fortified (1 turn).`  
**Problem:** Single status, same as a dozen other cards.

**New mechanic:**
> Target allied Human or Dunedain character: they automatically win any duel forced upon them this turn and refuse all further duels until their next action. The nearest allied PC you own gains +5 loyalty.

**Why it's better:** Duel interaction is a specific game mechanic with a clear outcome; the loyalty bonus ties it to strategic play.

---

### A10. "Luglurak Dark Magic"
**Decks:** `the_dark_eye` (id:1016), `the_deceiver` (id:1015)  
**Current:** `Enemy characters in radius 1 gain Fear (1 turn).`  
**Problem:** Simplest possible debuff.

**New mechanic:**
> Choose 1 enemy character in radius 2. Their current card is discarded without effect. They draw a random card from the bottom of their deck and must play it next turn if able.

**Why it's better:** Card disruption is a meaningful mechanic that creates real uncertainty for the opponent.

---

### A11. "Struck by a Morgul Blade"
**Decks:** `the_dark_eye` (id:1020), `the_iron_crown` (id:1018)  
**Current:** `Target enemy character: apply MorgulTouch (3 turns).`  
**Problem:** Single status, no interaction.

**New mechanic:**
> Target enemy character: they take 10 damage per turn for 3 turns (the wound festers). If their HP drops below 25 at any point, they permanently lose 1 from their highest skill. Cured only by Heal spells with Mage 2+.

**Why it's better:** Progressive damage with a permanent skill consequence adds real strategic weight to the card.

---

### A12. "A Knife in the Dark"
**Decks:** `shadow_of_the_east` (id:1029), `the_dark_eye` (id:1023), `the_deceiver` (id:1026)  
**Current:** `Target enemy character: apply Poisoned (2 turns) and Fear (1 turn).`  
**Problem:** Two statuses, still just a debuff pair.

**New mechanic:**
> Ambush: target enemy character in your hex cannot play a card this turn and takes 15 damage. If the caster is Hidden when this is played, also steal 2 gold from the target and maintain Hidden status.

**Why it's better:** Card denial is a distinct mechanic; the Hidden bonus rewards positioning play.

---

### A13. "Dragon-fire"
**Decks:** `tharkun` (id:1009), `the_necromancer` (id:1017)  
**Current:** `Enemy units in radius 1 gain Burning (1 turn); forest enemies also gain Fear (1 turn).`  
**Problem:** Status + conditional status.

**New mechanic:**
> All forest hexes in radius 2 become Burning terrain for 2 turns. Any unit entering those hexes takes 10 damage. Allied Dwarves are immune to fire damage. Enemy units currently in those hexes lose 1 weakest troop type immediately.

**Why it's better:** Terrain modification is a spatial mechanic; the immediate troop loss is a concrete combat effect.

---

## Group B — Single-deck pure-status cards

---

### B1. "Poison" — `actions_deck` (id:475)
**Current:** `Target enemy character: apply Poisoned (2 turns).`

**New mechanic:**
> Target enemy character: deal 5 damage immediately and 5 damage at the start of each of their next 2 turns. If the caster has Agent 2+, the target also cannot take Agent actions on the turn the poison ticks.

---

### B2. "Stars" — `gandalf_base` (id:140)
**Current:** `Grants Hope to all Elves for 1 turn.`

**New mechanic:**
> All Elf characters in radius 5 reveal hidden enemy units in their surrounding hexes (radius 1 each). Any Elf that was Hidden gains an extra action this turn.

---

### B3. "The Horn Calls" — `gandalf_base` (id:434)
**Current:** `All allied Human and Dunedain in radius 2 gain Encouraged + enemy units in hex gain Fear.`  
**Problem:** Two groups get opposite statuses; still just statuses.

**New mechanic:**
> Sound the horn: reveal all Hidden enemy units within radius 3. Allied Human and Dunedain characters in radius 2 may immediately move 1 hex toward the caster without spending movement and gain Encouraged (1 turn) from the call. Enemy characters in this hex cannot leave until end of turn.

---

### B4. "Bearer of Narya" — `gandalf_base` (id:1001)
**Current:** `All allied characters in radius 1 lose Fear and Despair, and gain Encouraged (1 turn).`  
**Problem:** Mass status removal + buff is still just status manipulation.

**New mechanic:**
> The fire of Narya kindles hearts. Choose 1 wounded allied character in radius 1: they are fully healed. All allied characters in radius 1 lose Fear and Despair. Reveal 1 hidden artifact site within radius 3.

---

### B5. "Full Moon" — `sauron_base` (id:268)
**Current:** `Grants Haste to all Nazgul for 1 turn.`

**New mechanic:**
> All Nazgul immediately teleport to the hex of the nearest Free People character within radius 5 and gain Haste (1 turn) upon arrival. If no target is in range, each Nazgul moves 2 hexes toward the nearest enemy instead. Targets revealed by this movement cannot become Hidden this turn.

---

### B6. "Doors of Night" — `sauron_base` (id:787) / "Shroud of Gorgoroth" — `the_dark_eye` (id:1018)
**Current:** `Grants Courage to all Dark Servants for 1 turn and dispels Dawn.`  
**Note:** Same concept, different names. Keep the dispel-Dawn interaction but add real mechanics.

**New mechanic:**
> All hexes within radius 4 of the caster lose their scouting status for 1 turn — allied and enemy scouts cannot see into them. Allied dark-aligned PCs in radius 4 become Hidden. Dispels Dawn if active.

---

### B7. "Marked by a Nazgul" — `sauron_base` (id:452)
**Current:** `Target enemy character: apply MorgulTouch (1 turn).`

**New mechanic:**
> Target enemy character is marked for death. The next allied Nazgul that enters the same hex deals double damage to this target. The mark persists for 3 turns or until triggered. Marked characters cannot become Hidden.

---

### B8. "WhenThereIsAWhip" — `sauron_base` (id:508)
**Current:** `All allied armies in radius 2 gain Haste (3 turns).`

**New mechanic:**
> Brutal efficiency: choose 1 allied Orc army in radius 2. It immediately loses 1 weakest troop type (whipped to death) but gains +1 permanent Commander skill and may act again this turn.

---

### B9. "Chains of the Lidless Eye" — `the_dark_eye` (id:710)
**Current:** `All Nazgul gain Haste (3 turns).`  
**Note:** "TheNineRideAgain" (id:1022) has the SAME effect. One must change entirely.

**New mechanic for "Chains of the Lidless Eye":**
> The Eye surveys. All Nazgul immediately reveal all Hidden units in their current hex. Enemy characters in radius 2 cannot become Hidden this turn. Each Nazgul that revealed a unit deals 5 bonus damage on their next attack.

**New mechanic for "TheNineRideAgain":**
> All Nazgul may immediately move 2 hexes. If any Nazgul moves into a hex with an enemy character, that enemy takes 15 damage and cannot be rescued by Free actions this turn.

---

### B10. "Black Gate Sortie" — `the_dark_eye` (id:1015)
**Current:** `All allied army commanders in mountains or hills gain Haste (1 turn) and Courage (1 turn).`  
**Note:** Same as "Raid From The Mountains" — double-status with a terrain condition.

**New mechanic:**
> A force pours from Morannon: choose 1 allied army at or adjacent to the Black Gate. It gains +2 Heavy Infantry immediately, gains Courage (1 turn) for the sortie, and may attack an adjacent enemy army this turn without spending an action. Can only be played within radius 4 of the Black Gate.

---

### B11. "Reach of Barad-Ungol" — `the_dark_eye` (id:470)
**Current:** `Enemy units in radius 1 gain Fear (1 turn) and Halted (1 turn).`

**New mechanic:**
> The Eye sweeps over this area: reveal all Hidden enemy units in radius 2. Enemy units that were revealed this way (were Hidden) take 15 damage from exposure and lose their action this turn.

---

### B12. "DreamsOfNumenor" — `gandalf_the_white` (id:875)
**Current:** `All allied units on shore/water-adjacent tiles gain Haste and Encouraged (1 turn).`

**New mechanic:**
> Visions of the lost kingdom. Reveal all coastal hexes and all enemy naval units within radius 4. Allied Human or Dunedain naval commanders gain 1 Warship. Allied characters on shore tiles may immediately embark on any allied ship in their hex.

---

### B13. "Stand of Dol Amroth" — `gandalf_the_white` (id:877)
**Current:** `Target allied character: gain Fortified (1 turn).`

**New mechanic:**
> The Swan-knights hold the line: target allied Human or Dunedain character. They cannot be forcibly moved this turn and cannot be targeted by Kidnap or Assassinate. If they are in a PC, that PC also gains +1 fortification.

---

### B14. "Token of the Woodland Realm" — `mithrandir` (id:910)
**Current:** `Target allied character: gain Hidden (1 turn).`

**New mechanic:**
> A gift from Lórien: target allied character gains Hidden for 3 turns and +1 to all skill checks while Hidden. If they are an Elf, they also immediately reveal all Hidden enemy units in an adjacent hex of their choice.

---

### B15. "Deceiving the White Council" — `of_many_colours` (id:1022), `saruman_the_white` (id:1092)
**Current:** `Apply Confused (3 turns) to all enemy characters in a target region. Enemy armies Halted (1 turn).`  
**Problem:** Mass status — still just a regional debuff.

**New mechanic:**
> Choose 1 allied Maia character. Plant false intelligence: enemies see a decoy location for them (1-3 hexes away) for 2 turns. Any enemy that moves to the decoy location this turn is Confused for 2 turns and loses their remaining movement.

---

### B16. "The 5 Ride Again" — `saruman_the_white` (id:1033)
**Current:** `All allied Maia gain Haste (2 turns) and Arcane Insight (2 turns).`

**New mechanic:**
> Convene the order: all allied Maia characters teleport to Orthanc or the nearest allied capital (whichever is closer). Each draws 1 card from their deck immediately upon arrival. Characters already at Orthanc instead gain +1 permanent Mage.

---

### B17. "Wormtongue's Whisper" — `the_white_hand` (id:711)
**Current:** `All allied units in radius 2 gain Strengthened (1 turn).`  
**Note:** This is an EXACT DUPLICATE of "Under the White Hand" (id:502) in the same deck. Neither can be a mass buff.

**New mechanic:**
> Poison a mind: choose 1 enemy character in radius 3. Their owner cannot see which card they played this turn. That character has a 50% chance to play a random ineffective action instead of their intended action next turn.

---

### B18. "Under the White Hand" — `the_white_hand` (id:502)
**Current:** `All allied units in radius 2 gain Strengthened (1 turn).` (SAME as Wormtongue's Whisper)

**New mechanic:**
> Rally under Saruman's banner: all allied army commanders in radius 2 immediately gain +1 Heavy Infantry. The nearest allied army converts 1 Men-at-arms into 1 Uruk-hai (Heavy Infantry) permanently.

---

### B19. "Uruk Vanguard" — `the_white_hand` (id:713)
**Current:** `All allied armies in radius 2 gain Fortified (1 turn).`

**New mechanic:**
> The Uruk-hai advance: choose 1 allied army in radius 2. It immediately moves 1 hex toward the nearest enemy army and gains +2 Heavy Infantry for this turn only (shock troops, they disperse after). If they reach an enemy, deal 1 automatic hit before combat.

---

### B20. "DreadOfTheNoldor" — `the_deceiver` (id:1014)
**Current:** `Apply Despair to Elf characters in radius 2 + Fear to enemy elves in caster hex.`

**New mechanic:**
> Ancient darkness strikes Elven hearts: Elf characters in radius 2 must make an immediate Mage check (difficulty 50) or start moving toward the Western coast — they are displaced 1 hex westward. The caster becomes Hidden if currently in a forest or on a coastline.

---

### B21. "Durin's Day" — `tharkun` (id:900)
**Current:** `Grants Hope to all Dwarves for 1 turn.`

**New mechanic:**
> On this rare day, Tharkun sings of Erebor. Reveal 1 random undiscovered artifact site in Dwarf territory. All Dwarf characters in radius 3 may take an extra action this turn and gain Hope (1 turn) from the song. If any Dwarf character is currently in Khazad-dûm or Erebor, they permanently gain +1 Mage.

---

### B22. "Mirkwood Miasma" — `the_necromancer` (id:1023)
**Current:** `Enemy units in radius 2 forest gain Poisoned (3 turns) and Halted (1 turn).`  
**Problem:** Status pair with terrain condition — still purely debuffs.

**New mechanic:**
> Toxic fog rolls out from Dol Guldur: enemy units on forest tiles in radius 2 cannot move for 1 turn and lose 1 weakest troop unit immediately (the fog claims stragglers). Allied Spiders in those hexes gain Haste and become Hidden.

---

### B23. "Dol Guldur Rises" — `the_necromancer` (id:1077)
**Current:** `Enemy units in radius 3 gain Fear (2 turns). Allied characters in hex gain Hidden (1 turn).`

**New mechanic:**
> The fortress stirs: reveal all Hidden enemy units in radius 4. Allied Dark characters at Dol Guldur may teleport to any forest hex within radius 3 immediately. All allied characters in this hex join the shadows — they cannot be targeted by card effects until the start of their next turn.

---

### B24. "Unquiet Dead" — `the_necromancer` (id:1079)  
**Current:** `Enemy units in radius 2 gain Fear + Despair; forest enemies also gain Poisoned.`

**New mechanic:**
> Raise the dead: choose 1 allied character who was killed this game. They are resurrected in your hex with 50% HP for 2 turns before falling again permanently. While risen, enemy characters in radius 1 cannot take card actions. Costs 1 Mithril.

---

## Summary table

| # | Card Name | Decks | Old mechanic | New mechanic type |
|---|-----------|-------|-------------|-------------------|
| A1 | Mounts | 3 | Haste buff | Teleport + join army |
| A2 | Traps | 3 | Blocked + Poisoned | Placed trap / triggered damage |
| A3 | Well-equipped Army | 4 | Fortified buff | Unit conversion |
| A4 | Long Shadows | 3 | Encouraged to beasts | Movement reset + scouting penalty + Encouraged (2°) |
| A5 | Raid From The Mountains | 5 | Haste + Courage, mountains | Auto-advance + combat hit + Haste (2°) |
| A6 | Under the Rhunic Sun | 2 | Courage to Easterlings | Loyalty + scouting + unit gain + Courage (2°) |
| A7 | ElvesGoingWest | 3 | Despair to elves | Forced movement westward + Despair (2°) |
| A8 | A Mithril Coat | 3 | Fortified (3 turns) | Cannot-be-one-shot + untargetable |
| A9 | Northmen Stand Firm | 2 | Fortified (1 turn) | Auto-win duel + loyalty |
| A10 | Luglurak Dark Magic | 2 | Fear (1 turn) | Card disruption |
| A11 | Struck by a Morgul Blade | 2 | MorgulTouch | Progressive damage + perm skill loss |
| A12 | A Knife in the Dark | 3 | Poisoned + Fear | Card denial + gold theft |
| A13 | Dragon-fire | 2 | Burning + conditional Fear | Terrain fire + troop loss |
| B1 | Poison | actions | Poisoned (2 turns) | Damage over time + agent block |
| B2 | Stars | gandalf_base | Hope to Elves | Reveal + extra action |
| B3 | The Horn Calls | gandalf_base | Encouraged + enemy Fear | Reveal + forced movement + Encouraged (2°) |
| B4 | Bearer of Narya | gandalf_base | Fear removal + Encouraged | Full heal + artifact reveal |
| B5 | Full Moon | sauron_base | Haste to Nazgul | Nazgul teleport to nearest enemy + Haste (2°) |
| B6 | Doors of Night / Shroud | sauron / dark_eye | Courage + dispel Dawn | Area scouting blackout + hidden PCs |
| B7 | Marked by a Nazgul | sauron_base | MorgulTouch | Mark for death (Nazgul trigger) |
| B8 | WhenThereIsAWhip | sauron_base | Haste to armies (3t) | Troop sacrifice → perm Commander |
| B9a | Chains of the Lidless Eye | dark_eye | Haste (3t) to Nazgul | Nazgul reveal hidden + bonus damage |
| B9b | TheNineRideAgain | dark_eye | Haste (3t) to Nazgul (DUPE) | Nazgul mass charge + damage |
| B10 | Black Gate Sortie | dark_eye | Haste + Courage, mountains | Free attack from Morannon + Courage (2°) |
| B11 | Reach of Barad-Ungol | dark_eye | Fear + Halted | Expose hidden units for damage |
| B12 | DreamsOfNumenor | gandalf_the_white | Haste + Encouraged, shore | Naval reveal + Warship + embark |
| B13 | Stand of Dol Amroth | gandalf_the_white | Fortified (1 turn) | Immovable + untargetable + fortify PC |
| B14 | Token of the Woodland Realm | mithrandir | Hidden (1 turn) | Hidden (3t) + skill bonus + elf reveal |
| B15 | Deceiving the White Council | of_many_colours, saruman_the_white | Region Confused | Decoy location trap |
| B16 | The 5 Ride Again | saruman_the_white | Haste + ArcaneInsight to Maias | Maia teleport to Orthanc + card draw |
| B17 | Wormtongue's Whisper | white_hand | Strengthened (DUPE) | Random action chance |
| B18 | Under the White Hand | white_hand | Strengthened (1t) | Army unit gain (permanent) |
| B19 | Uruk Vanguard | white_hand | Fortified (1t) to armies | Advance + temp troops + auto hit |
| B20 | DreadOfTheNoldor | the_deceiver | Despair + Fear | Forced westward movement |
| B21 | Durin's Day | tharkun | Hope to Dwarves | Artifact reveal + extra action + Hope (2°) |
| B22 | Mirkwood Miasma | the_necromancer | Poisoned + Halted, forest | Troop loss + Spider hidden + haste |
| B23 | Dol Guldur Rises | the_necromancer | Fear + Hidden | Reveal + teleport + untargetable |
| B24 | Unquiet Dead | the_necromancer | Fear + Despair + Poisoned | Character resurrection (temp) |

---

## What to do with the Environmental "Dawn / Stars / Sun / Long Shadows / Full Moon / Durin's Day" block

These environmental events are an entire category. The rule isn't that environmental events are bad — it's that they are interchangeable. Every one is "species X gets buff Y." Fix strategy:

1. **Keep the thematic target** (Dwarves on Durin's Day, Nazgul on Full Moon) — the flavor is right.  
2. **Add a non-status mechanic** as the primary effect. The status can stay as secondary.  
3. **Ensure each is unique** — Sun should do something Dawn cannot.

Already addressed above: Full Moon (B5), Long Shadows (A4), Durin's Day (B21), Doors of Night (B6).

**Remaining environmental events that were NOT in the pure-status list** (they already have additional mechanics) — those are fine and can serve as reference for what the above should look like after migration:
- `Dawn` — grants Encouraged to Free People AND **dispels Doors of Night** ✓ (unique interaction)
- `Sun` — grants Encouraged to Humans/Hobbits AND **halts all Trolls** ✓ (unique race counter)
- `Stars` — pure Hope to Elves ✗ → Fixed in B2 above

---

## Implementation order

1. **Fix duplicates first (Group A)** — one change fixes 3–5 cards simultaneously.  
2. **Fix the complete internal duplicates** — "Under the White Hand" and "Wormtongue's Whisper" have identical effects in the same deck. Do those immediately.  
3. **"TheNineRideAgain" / "Chains of the Lidless Eye"** — same effect, same deck. Fix immediately.  
4. **Group B** — individual card fixes, lower urgency but still needed.

---

*Reference: card data lives in `Assets/Resources/Cards/Modular/*.json`.*
