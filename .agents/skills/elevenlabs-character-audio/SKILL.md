---
name: elevenlabs-character-audio
description: Find, create, generate, download, and wire RetroLOTR character-selection voices and creature SFX with ElevenLabs. Use when Codex needs to replace a race or named-character voice, search ElevenLabs voices before designing one, write selection lines, generate TTS or nonverbal creature sounds, import audio under Assets/Sounds, or assign approved clips to the Unity Sounds system.
---

# ElevenLabs Character Audio

Produce one approved voice or creature profile at a time. Read `PLAN_character_selection_voices.md` before writing lines or assigning profiles.

## Guardrails

- Read the API key only from `ELEVENLABS_KEY` (preferred) or `ELEVENLABS_API_KEY`. Check the current process first, then Windows User and Machine environment scopes. Never print, persist, commit, or place it in a command argument.
- Search before generating. Prefer a suitable existing account/shared voice over Voice Design.
- Never create a new voice without asking the user and receiving an explicit yes.
- Treat Voice Design, TTS, and generated SFX as billable calls. State what will be generated before calling them.
- Use ElevenLabs Sound Effects, not TTS, for Balrog, Spider/Shelob, and Eagle/Gwaihir.
- Generate no character-selection audio for Machine.
- Do not imitate a named actor or reproduce film dialogue. Describe original vocal traits.
- Preserve unrelated project and prefab changes.
- Do not process the entire roster in one unreviewed batch. Finish and verify one profile before continuing.

## Tools

Use `scripts/elevenlabs_audio.py` for API operations. Run `python scripts/elevenlabs_audio.py --help` for commands. Mutating or billable commands require an explicit confirmation flag.

Use `scripts/wire_sounds_prefab.py` only when the current architecture still stores the target list directly on `Assets/GameObjects/Sounds.prefab`. Run it without `--apply` first.

Read `references/api-and-search.md` when searching, creating, or generating. Read `references/unity-wiring.md` before importing or wiring clips.

## Workflow

### 1. Inspect the target

1. Identify the race, sex, named profile, aliases, and whether the target speaks.
2. Inspect `Assets/Scripts/UI/Config/Sounds.cs`, `Assets/GameObjects/Sounds.prefab`, and current character data. Do not assume the plan's future profile architecture has already been implemented.
3. Search existing local audio under `Assets/Sounds` first.
4. For ordinary characters, resolve by race and sex. Use named overrides primarily for the approved Maia identities.
5. Apply the confirmed sharing rules in the plan: Treebeard uses Ent, Smaug uses Dragon, male Hobbits share, male Dwarves share, and the listed Humans/Elves use their shared sex profile.

### 2. Find existing audio

For speech:

1. List owned/saved voices.
2. Search the shared Voice Library with the exact target plus two or three useful synonyms. Search both sexes where required.
3. Exclude live-moderated voices and custom-rate voices by default. Prefer English-capable character/animation voices with a useful notice period.
4. Download up to three promising preview files to an OS temporary directory and let the user audition them when suitability is subjective.
5. Reuse the selected voice. Add a shared voice to the account collection only when needed for TTS.

Metadata alone is insufficient when several candidates are plausible. Do not claim a voice sounds correct without auditioning it or having the user select it.

For nonverbal SFX:

1. Search local shipped assets by creature name and sound description.
2. Check whether an existing local clip is original/licensed and suitable for selection use.
3. ElevenLabs has a Sound Effects generator, not a searchable stock-SFX library endpoint. If no local asset fits, report that generation is required.

### 3. Ask before Voice Design

If no suitable existing voice is found, report the search terms and why the best candidates failed. Then ask:

```text
No suitable <profile> voice was found. Should I create one with ElevenLabs Voice Design?
1. Yes - design three previews for review.
2. No - keep searching or leave this profile unchanged.
Please choose a number and I will implement that option.
```

Stop until the user answers. After approval:

1. Write an original 20-1000 character voice description covering sex, apparent age, weight, texture, pace, temperament, and recording quality.
2. Write a 100-1000 character neutral preview passage that exercises the desired range without quoting protected dialogue.
3. Generate three Voice Design previews into a temporary review folder.
4. Let the user audition and choose a numbered preview. Do not automatically save preview 1.
5. Create the chosen voice with a stable `retrolotr_<profile_id>` name and record its `voice_id` in the profile manifest/data, never in gameplay code.

If SFX generation is required and the user's request did not already authorize generation, ask the same style of numbered yes/no question before spending credits.

### 4. Write selection lines

Write exactly three initial selection lines per approved profile unless the user requests another count.

- Keep each line approximately 3-9 words and 0.8-2.2 seconds.
- Make it acknowledge attention, invite direction, or reveal temperament.
- Avoid UI terms such as "selected," "click," "unit," and "turn."
- Avoid modern phrasing, exposition, direct film quotations, and action narration.
- Male and female versions express the same race identity without caricature.
- Reuse the same written text for both sexes unless wording genuinely requires a change.
- Save the approved text in the project's voice-profile data or a reviewed JSON generation manifest before generating audio.

### 5. Generate and download

For speech:

1. Generate one file per line with the approved voice ID.
2. Default to `eleven_v3` for expressive character delivery. Use `eleven_multilingual_v2` when consistency is more important or v3 performs poorly.
3. Default to `mp3_44100_128` unless the active plan and project requirement justify another API format.
4. Use stable lowercase filenames: `<profile>_select_01.mp3` through `_03.mp3`.

For creature SFX:

1. Write three distinct prompts: acknowledgement/alert, neutral vocalization, and threat/intensity.
2. Specify 0.8-2.0 seconds; never use auto duration for selection SFX.
3. Explicitly request a dry isolated one-shot with no music, ambience, speech, or long reverb.
4. Save as `<creature>_select_01.mp3` through `_03.mp3`.

Download directly into the approved folder under:

```text
Assets/Sounds/Voices/Shared/<race>/<sex>/select/
Assets/Sounds/Voices/Character/<profile>/select/
Assets/Sounds/Voices/Creatures/<creature>/select/
```

Never overwrite approved audio silently. Generate a review suffix or move the old file only with the user's authorization.

### 6. Import and wire into Unity

1. Follow `references/unity-wiring.md`.
2. Let Unity import the new files and create `.meta` GUIDs before prefab wiring.
3. Wire all three clips to the correct race/sex, named, or creature profile.
4. If the required race+sex or named-profile system is absent, implement the data-driven profile architecture first. Do not collapse the new assets back into a generic list or hardcode character-name switches.
5. If directly editing `Sounds.prefab`, dry-run `wire_sounds_prefab.py`, inspect its proposed replacement, then run with `--apply`.
6. Ensure selection playback rotates clips without immediate repetition and discards stale queued selection dialogue.

### 7. Verify

- Confirm all audio and `.meta` files exist and are tracked.
- Confirm every configured GUID resolves to the intended audio asset.
- Confirm the target character resolves to the intended profile from data.
- Compile Unity scripts or run the project's available validation.
- Test three selections to confirm rotation and no immediate repeat.
- Rapidly select different characters and confirm stale dialogue does not backlog.
- Confirm invisible enemies cannot reveal themselves through audio.
- Report the selected ElevenLabs voice name/ID, model, generated files, credit headers when available, and Unity fields/data changed.
