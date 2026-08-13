# ElevenLabs API and search reference

API base: `https://api.elevenlabs.io`

Authenticate with `xi-api-key` read from `ELEVENLABS_KEY`; fall back to `ELEVENLABS_API_KEY`. Check the current process first, then Windows User and Machine environment scopes. Never log the header.

## Discovery

- Owned/saved voices: `GET /v2/voices`
- Shared Voice Library: `GET /v1/shared-voices`
- Useful shared filters: `search`, `gender`, `language=en`, `use_cases`, `descriptives`, `include_custom_rates=false`, `include_live_moderated=false`, `sort=usage_character_count_1y`.
- Shared results include `voice_id`, `public_owner_id`, `preview_url`, description/labels, rate, and notice-period metadata when available.
- Add a shared voice: `POST /v1/voices/add/{public_user_id}/{voice_id}` with `new_name`.

Search a concept using literal and descriptive terms. Examples:

- Troll: `troll`, `ogre`, `giant`, `monster`, `deep gravelly character`.
- Dragon: `dragon`, `ancient beast`, `deep regal villain`, `creature character`.
- Elf: `elf`, `ethereal`, `ancient calm`, plus the required gender.

Do not accept a result only because its name matches. Inspect description, labels, language, notice period, custom rate, and preview.

## Voice Design

1. `POST /v1/text-to-voice/design` with `voice_description`, 100-1000 character preview `text`, and `model_id`.
2. Save all returned base64 preview audio and `generated_voice_id` values.
3. After the user chooses, `POST /v1/text-to-voice` with `voice_name`, `voice_description`, and the chosen `generated_voice_id`.

Voice Design is billable and consumes voice operations. Never call it before explicit approval.

## TTS

`POST /v1/text-to-speech/{voice_id}?output_format=mp3_44100_128`

Body:

```json
{
  "text": "Approved line",
  "model_id": "eleven_v3"
}
```

Capture `character-cost`, `request-id`, and `x-trace-id` response headers when present. Shared voices can have credit multipliers; exclude custom-rate voices unless the user knowingly approves one.

## Sound Effects

`POST /v1/sound-generation?output_format=mp3_44100_128`

Body:

```json
{
  "text": "Dry isolated short creature vocalization, no speech, music, or ambience",
  "duration_seconds": 1.5,
  "prompt_influence": 0.5,
  "loop": false
}
```

The public API generates SFX; it does not provide a searchable stock-SFX catalog. Search the repository's licensed assets before generating.

## Currency of this reference

Endpoints were checked against official ElevenLabs documentation on 2026-08-13. API behavior and billing can change. Recheck the official API reference before changing endpoint shapes, output formats, or cost assumptions.
