# Character Selection Voice Replacement Plan

## Goal

When the human player selects one of their characters, the character gives a short spoken acknowledgement or, for a non-speaking creature, an identifying vocal SFX.

The system will use shared male and female race voices for most characters. Named voices are reserved for characters whose identity genuinely requires one, especially the Maia. There will be no culture, nation, or regional voice layer.

## Confirmed design rules

- Every speaking race has a male and a female voice profile.
- Each profile begins with three short selection responses.
- Most characters share their race-and-sex profile.
- All male Hobbits share the Male Hobbit voice, including Frodo, Sam, Merry, Pippin, and Bilbo.
- All male Dwarves share the Male Dwarf voice, including Gimli and Thorin.
- Treebeard uses the Male Ent voice; he does not have a separate Treebeard voice.
- Smaug uses the Male Dragon voice; he does not have a separate Smaug voice.
- Boromir and Faramir both use the Male Human voice.
- Theoden and Denethor both use the Male Human voice.
- Eowyn uses the Female Human voice.
- Galadriel uses the Female Elf voice.
- Elrond and Thranduil share a named Elf-lord voice; other male Elves use the shared Male Elf voice.
- Balrog now uses an approved designed speaking voice. Spider/Shelob and Eagle/Gwaihir remain non-speaking and use creature SFX.
- Machine receives no character-selection audio.
- There are no Gondor, Rohan, Bree, Uruk-hai, Noldor, woodland, or other cultural voice profiles.

## Current technical situation

- `Board.SetSelectedCharacter` calls `Sounds.PlayVoiceExpression` when the human player selects one of their own characters.
- Humanoids currently use generic male/female expression clips; most creatures use a race clip.
- The existing system assigns one stable expression clip to each spawned character. It does not rotate through three selection lines.
- Rapid character browsing can queue obsolete voices.
- Modular character cards currently do not store sex, and the relevant spawn paths default new card characters to `SexEnum.Male`.

The sex-data issue must be fixed before the new voice assignment is trusted. Otherwise Eowyn and Galadriel will incorrectly resolve to male voices regardless of the audio assets available.

## Resolution hierarchy

Selection audio resolves in this order:

1. **Named profile override** — used primarily for distinct Maia identities.
2. **Race + sex profile** — normal path for speaking characters.
3. **Race creature-SFX profile** — Balrog, Spider, and Eagle.
4. **Generic male/female voice** — emergency fallback for missing data or clips.
5. **Silence** — Machine, or any profile explicitly configured not to play audio.

All assignments live in data. Character names and aliases must not be hardcoded in `Sounds.cs`.

## Shared speaking profiles

Each row requires a Male and Female profile, with three selection lines per profile. The sample wording establishes intent; male and female versions may use the same text with different performances unless a line does not fit.

| Race profile | Direction | Three selection responses |
|---|---|---|
| Human (`Common`) | Grounded, alert, practical | “At your service.” / “What is required?” / “I am ready.” |
| Elf | Clear, calm, ancient without affectation | “I hear you.” / “Speak, and I shall heed.” / “The stars are watchful.” |
| Dwarf | Low, direct, workmanlike | “Aye, what work?” / “Name the road.” / “My axe is ready.” |
| Hobbit | Warm, wary, quietly brave | “Yes? Is it time?” / “Which way?” / “I am ready… I think.” |
| Maia fallback | Grave and restrained; only for an unmapped Maia | “I am listening.” / “The hour moves.” / “There is work before us.” |
| Orc | Harsh, impatient, intelligible | “What now?” / “Give the word.” / “Who do we hunt?” |
| Troll | Slow, blunt, threatening | “What?” / “Point the way.” / “Ready to break things.” |
| Nazgul | Thin, cold, inhuman whisper | “We hear.” / “The hunt continues.” / “None shall escape.” |
| Dragon | Proud, resonant, dangerous | “You dare disturb me?” / “Speak, little one.” / “What treasure awaits?” |
| Undead | Hollow, distant, unwillingly awakened | “Who calls the dead?” / “The grave remembers.” / “We do not rest.” |
| Dunedain | Weathered, disciplined, quietly noble | “I keep watch.” / “The road is known.” / “Give me your word.” |
| Beorning | Large, plainspoken, suspicious | “Speak plainly.” / “The wild is listening.” / “I am no tame hound.” |
| Wildman | Watchful, terse, woodland cadence | “I hear the hills.” / “Show me the trail.” / “We move unseen.” |
| Goblin | Quick, spiteful, nervous aggression | “What d'you want?” / “I heard you!” / “Show me the dark way.” |
| Ent | Extremely deep and unhurried | “Do not be hasty.” / “The wood is awake.” / “I am listening… slowly.” |
| Southron | Proud soldier, warm resonance, no caricature | “I await the command.” / “The sun is high.” / “My spear is ready.” |
| Easterling | Controlled, martial, confident, no caricature | “The host awaits.” / “Name our road.” / “Your command is heard.” |

This creates 34 shared speaking profiles: 17 races × 2 sexes. At three clips per profile, the complete shared speaking set is **102 clips**.

`SexEnum.Other` should use an explicit profile override when one exists. Otherwise it falls back to the race's configured default rather than being silently treated as female or male by code.

## Confirmed shared character assignments

| Character(s) | Assigned profile |
|---|---|
| Frodo, Sam, Merry, Pippin, Bilbo, and every other male Hobbit | Male Hobbit |
| Every male Dwarf, including Gimli and Thorin | Male Dwarf |
| Treebeard and other male Ents | Male Ent |
| Smaug and other male Dragons | Male Dragon |
| Boromir and Faramir | Male Human |
| Theoden and Denethor | Male Human |
| Eowyn | Female Human |
| Galadriel | Female Elf |
| Elrond and Thranduil | Named Elf-lord profile |
| Shelob and other Spiders | Spider SFX |
| Balrog | Male Balrog speaking profile |
| Gwaihir and other Eagles | Eagle SFX |
| Machine | Silence |

These mappings are examples of the general rule, not individually implemented character overrides. For example, Boromir and Faramir should naturally resolve to Male Human from their race and sex data.

## Named Maia profiles

Maia remain the exception because race and sex are not enough to distinguish their identities. Each named profile gets three selection lines. Aliases and forms share the same underlying voice performer/profile; optional variant line sets can be added later without inventing a new voice.

| Named profile | Names/forms assigned to it | Direction | Example selection lines |
|---|---|---|---|
| Gandalf | Gandalf, Mithrandir, Stormcrow, Tharkun, Disturber of the Peace, Gandalf the White | Warm, weathered, alert; authority held in reserve | “What news from the road?” / “There is still time, if we use it well.” / “A small deed may turn the tide.” |
| Saruman | Saruman, Saruman the White, Saruman Multicoloured, The White Hand, Sharkey | Polished, controlled, persuasive; irritation underneath | “Speak. I am listening.” / “My counsel is not lightly set aside.” / “All proceeds according to design.” |
| Sauron | Sauron, The Dark Eye, The Deceiver, The Necromancer, Shadow of the East, The Iron Crown, and other Sauron forms | Still, immense, possessive; more will than mortal voice | “Nothing is hidden from me.” / “All wills bend in time.” / “The hour of dominion draws near.” |
| Radagast | Radagast | Gentle, distracted, suddenly perceptive | “The birds are restless. What have you seen?” / “Tread gently; the wood is listening.” / “There is news upon the wind.” |
| Alatar | Alatar | Remote and austere | “The eastern road is long.” / “I have seen this shadow before.” / “Speak, before the trail grows cold.” |
| Pallando | Pallando | Measured and scholarly | “There are other paths eastward.” / “Let us weigh what is known.” / “I will hear your counsel.” |
| Tom Bombadil | Tom Bombadil | Musical, bright, powerful without urgency | “Well now! What road shall we wander?” / “The old forest knows our feet.” / “Speak up, friend; the day is young!” |

This creates seven named Maia profiles and **21 bespoke clips**. The generic Male/Female Maia profiles remain available only as safeguards for future or unmapped Maia.

## Non-speaking creature SFX

These profiles do not contain words. They use three short, recognizable vocal sounds each and are not divided into male/female versions unless the art roster later demonstrates a real need.

| Creature profile | Used by | Three SFX intents |
|---|---|---|
| Spider | Shelob and other Spiders | Warning hiss / mandible clicks / rising hunting rasp |
| Eagle | Gwaihir and other Eagles | Alert cry / short acknowledgement call / forceful challenge screech |

This set requires **6 creature clips**. Balrog contributes three spoken selection clips instead.

Machine intentionally has no profile and produces no selection sound. `Beast` currently has no modular character card; if one is added, decide its animal family before creating audio rather than assigning a generic spoken voice.

## Line and performance rules

- Duration: approximately 0.8–2.2 seconds.
- Usually 3–9 spoken words.
- Lines acknowledge attention, ask for direction, or reveal temperament.
- Do not say “selected,” “clicked,” “unit,” “turn,” or other UI language.
- Avoid direct film quotations and deliberate imitation of identifiable screen actors.
- Use restrained close-mic performances, not speeches shouted over a battlefield.
- Male and female variants express the same race identity. They should not become exaggerated masculine/feminine caricatures.
- Creature SFX must be short enough to tolerate repeated selection.

## Required data changes

1. Add `sex` to modular character-card data and the current loader schema.
2. Populate sex for all character cards; do not infer it from the spelling of a name at runtime.
3. Update every card-character spawn path to use the card's sex instead of forcing `SexEnum.Male`.
4. Add an optional `voiceProfileId` to character and playable-leader/variant data.
5. Use `voiceProfileId` only for named overrides such as the Maia. Leave ordinary characters empty so they resolve by race and sex.
6. Add an explicit audio mode/profile for creature SFX and silence.
7. Add an editor/audit check reporting:
   - missing sex data;
   - missing race/sex voice profile;
   - missing named profile;
   - speaking profile with fewer than three selection clips;
   - Machine or silence profile with an audio clip assigned accidentally.

## Audio-system changes

1. Introduce a data-loaded `VoiceProfile` containing:
   - `profileId`
   - `audioMode`: `Speech`, `CreatureSfx`, or `Silent`
   - `selectClips`
   - optional future `orderClips`, `attackClips`, `effortClips`, and `painClips`
   - gain and optional pitch range
2. Resolve named override → race/sex → generic fallback.
3. Replace the stable single expression clip with a per-character shuffle bag:
   - use all three responses before reshuffling;
   - never immediately repeat the previous response;
   - retain the same voice identity for the character.
4. Give selection dialogue/SFX a dedicated audio source or queue.
5. When another character is selected, discard obsolete queued selection responses.
6. Add anti-spam:
   - 200–300 ms debounce between different characters;
   - 2–3 second same-profile cooldown;
   - optional silence after repeated rapid browsing.
7. Preserve the current ownership and visibility rules so audio cannot reveal an unseen enemy.
8. Keep current attack, effort, and pain sounds functioning until those categories are explicitly replaced.

## Asset layout

```text
Assets/Sounds/Voices/
  Shared/<race>/<sex>/select/<race>_<sex>_select_01.wav
  Character/<profile_id>/select/<profile_id>_select_01.wav
  Creatures/<creature>/select/<creature>_select_01.wav
```

Examples:

```text
Shared/hobbit/male/select/hobbit_male_select_01.wav
Shared/human/female/select/human_female_select_01.wav
Shared/ent/male/select/ent_male_select_01.wav
Shared/dragon/male/select/dragon_male_select_01.wav
Character/gandalf/select/gandalf_select_01.wav
Creatures/spider/select/spider_select_01.wav
```

Recommended delivery format:

- WAV, mono, 48 kHz, 24-bit source.
- Trim leading silence to roughly 30–80 ms.
- Keep a short natural tail and avoid long baked-in reverb.
- Loudness-match all voices as one set, then tune profile gain in data.
- Keep raw generation or recording masters outside the Unity import folder; commit approved game-ready clips only.

## Production phases

### Phase 1 — data and playback foundation

- Add and populate character sex data.
- Add data-driven voice profiles and named overrides.
- Implement selection rotation, anti-repeat, interruption, and cooldown.
- Add the audit tool before generating the full set.

### Phase 2 — proof set

Produce and test:

- Male Human and Female Human.
- Male Elf and Female Elf.
- Male Hobbit.
- Male Dwarf.
- Male Ent, proving Treebeard shares it.
- Male Dragon, proving Smaug shares it.
- Gandalf, Saruman, Sauron, and Radagast.
- Balrog speech, plus Spider and Eagle SFX.
- Machine silence.

This proof explicitly verifies every sharing rule in this plan before bulk production.

### Phase 3 — complete shared coverage

- Produce both male and female versions for all 17 speaking race profiles.
- Complete all three selection responses per profile.
- Populate the remaining named Maia: Alatar, Pallando, and Tom Bombadil.
- Complete all creature SFX profiles.

Full planned selection library:

- Shared male/female voices: 102 clips.
- Named Maia voices: 21 clips.
- Speaking Balrog profile: 3 clips.
- Non-speaking creature SFX: 6 clips.
- Machine: 0 clips.
- **Total: 132 selection clips.**

### Phase 4 — contextual audio, only after approval

- Consider `order`, `attack`, `effort`, `pain`, and rare repeated-selection responses.
- Continue sharing by race and sex unless a new named exception is explicitly approved.
- Do not introduce cultural profiles.

## Acceptance checklist

- Male and female characters consistently resolve to the correct version of their race voice.
- Eowyn uses Female Human and Galadriel uses Female Elf.
- Frodo and Sam share Male Hobbit.
- All male Dwarves share Male Dwarf.
- Boromir and Faramir share Male Human.
- Theoden and Denethor share Male Human.
- Treebeard sounds exactly like the Male Ent profile.
- Smaug sounds exactly like the Male Dragon profile.
- Gandalf, Saruman, Sauron, and Radagast remain clearly distinct despite all being Maia.
- Balrog uses its designed speaking profile; Shelob and Gwaihir use nonverbal SFX.
- Machine is silent.
- Selection responses rotate without immediate repeats.
- Rapid browsing does not create a backlog of stale responses.
- No audio reveals unseen enemies.
- Three minutes of ordinary play does not feel excessively talkative.

## Recommended first milestone

Implement Phase 1 and the Phase 2 proof set before generating all 132 clips. This validates sex data, shared-profile behavior, named Maia overrides, creature SFX, and silence using the exact edge cases that matter most.
