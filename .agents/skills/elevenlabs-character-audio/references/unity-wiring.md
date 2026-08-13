# Unity import and Sounds wiring

## Inspect before editing

- Runtime: `Assets/Scripts/UI/Config/Sounds.cs`
- Serialized prefab: `Assets/GameObjects/Sounds.prefab`
- Selection trigger: `Board.SetSelectedCharacter`
- Race enum: `Assets/Data/Enums/RacesEnum.cs`
- Sex enum: `Assets/Data/Enums/SexEnum.cs`
- Voice plan: `PLAN_character_selection_voices.md`

The current repository may evolve from direct prefab lists to data-driven voice profiles. Follow the architecture present at execution time.

## Import

1. Place approved files under `Assets/Sounds/Voices` using the skill's folder convention.
2. Refresh/import through Unity so every audio file receives a `.meta` file and GUID.
3. For short selection one-shots, use mono where appropriate, disable looping, and avoid 3D spatial playback unless intentionally required.
4. Do not manufacture a `.meta` by copying another asset's GUID.

## Direct prefab-list wiring

Use only if `Sounds.cs` still exposes the required serialized list and no newer voice-profile data owns the assignment.

Create a temporary JSON mapping:

```json
{
  "voiceDragonClips": [
    "Assets/Sounds/Voices/Shared/dragon/male/select/dragon_male_select_01.mp3",
    "Assets/Sounds/Voices/Shared/dragon/male/select/dragon_male_select_02.mp3",
    "Assets/Sounds/Voices/Shared/dragon/male/select/dragon_male_select_03.mp3"
  ]
}
```

Dry-run:

```powershell
python .agents/skills/elevenlabs-character-audio/scripts/wire_sounds_prefab.py --manifest <manifest.json>
```

Apply after reviewing the dry-run:

```powershell
python .agents/skills/elevenlabs-character-audio/scripts/wire_sounds_prefab.py --manifest <manifest.json> --apply
```

The script replaces the complete target list. Include all intended clips.

## Race/sex and named profiles

The legacy `Sounds.cs` race buckets are not sufficient for the approved male/female race design or named Maia overrides. If those structures do not yet exist:

1. Add data-backed profile IDs and sex data according to the plan.
2. Store ordinary assignments by race + sex.
3. Store named overrides in character/leader data, not character-name switch statements.
4. Make Spider, Balrog, and Eagle profiles `CreatureSfx`; make Machine silent.
5. Wire the generated clips through the new data asset/JSON rather than forcing them into legacy generic humanoid lists.

## Verification

- Resolve each prefab/data GUID back to exactly one audio `.meta` file.
- Confirm no old voice list remains active for the replaced profile.
- Confirm clips rotate rather than pinning one stable clip per character.
- Confirm ownership and visibility checks remain intact.
