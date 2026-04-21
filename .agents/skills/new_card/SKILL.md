---
name: new-card
description: Create or update RetroLOTR cards by adding entries to the correct deck JSON (Gandalf, Sauron, or Saruman), handling card images, and keeping card data aligned with the current loader schema. Use when Codex must add a new card, duplicate/variant card, or card-linked effect.
---

# New Card

Create card definitions in deck JSON files and ensure image and reference links are valid.

## Source Of Truth
- `Assets/Resources/Cards/gandalf_deck.json`
- `Assets/Resources/Cards/sauron_deck.json`
- `Assets/Resources/Cards/saruman_deck.json`
- The owning card JSON entry for action-linked cards; this repo does not use a separate action registry file
- `Assets/Scripts/Cards/CardTypeEnum.cs` (valid card types)
- For modular subdeck cards, read `Assets/Resources/Cards/Modular/manifest.json` and `DeckFlavorRules.md` first and match the target subdeck flavor exactly.

## Card Types
Use only values defined in `CardTypeEnum` when setting card `type`.

Current enum values:
- `Action`
- `Event`
- `Land`
- `PC`
- `Character`
- `Army`
- `Rest`
- `Encounter`

## Required Clarifications
Ask only when missing or ambiguous:
1. Alignment/deck target (`Gandalf`, `Sauron`, `Saruman`).
2. Whether the card needs an existing action reference or should remain data-only.
3. Whether to reuse an existing image or create a new one.

If alignment is unknown, ask before editing files.

## Alignment To Deck Mapping
- `Gandalf` -> `Assets/Resources/Cards/gandalf_deck.json`
- `Sauron` -> `Assets/Resources/Cards/sauron_deck.json`
- `Saruman` -> `Assets/Resources/Cards/saruman_deck.json`

## Card Creation Workflow
1. Pick the target deck from alignment.
2. Find the next `cardId` by scanning all three deck files and using `max(cardId) + 1`.
3. Copy the closest existing card entry in the target deck (same `type`) as the schema template.
4. Fill card fields (`name`, `description`, `type`, `tags`, requirements, and action fields if present) using existing project naming patterns.
5. For cards that intentionally use an existing action reference, keep the card record coherent with the current loader:
   - prefer the existing `action` field when present
   - only preserve `actionClassName` when the target deck schema already uses it
   - do not add `actionId` unless the target deck schema already requires it
6. Insert the new card object in a stable location (near similar cards or at deck end) and keep JSON formatting consistent.

## Creativity Rule
- Every new card mechanic must be unique and immersive; do not ship a card that feels like a renamed copy of an existing pattern.
- Do not keep repeating the same status effects over and over again across new cards.
- Do not default to the same familiar status packages, especially repeated use of `Fear`, `Halted`, `Hope`, `Encouraged`, `Hidden`, or `Blocked`.
- Prefer a card-specific mechanic that better expresses the lore, scene, or role of the card.
- If a simple status effect is the best fit, make sure it is doing distinct work and not just another copy of the last few cards.
- For new action cards, look first for a fresh play pattern: movement manipulation, conditional targeting, resource changes, terrain interaction, cleansing, setup/reward loops, or other distinct tactical identities.
- If a proposed effect feels like “another Courage/Fear card,” reject it and choose a more original mechanic unless the lore strongly justifies the reuse.
- When reusing an existing status, use it as a deliberate choice, not the default answer.

## Action Card Rule
If the card needs a gameplay effect:
1. Ask whether to reuse an existing action/effect or create a new one.
2. If creating new gameplay logic, use the `new-character-action` skill first, then point the card JSON entry at the resulting effect reference.
3. If reusing, confirm the exact existing card record and copy only the fields the current deck schema uses.

## Effect Design Guidance
- Start from the card's lore function, not from a status-effect bucket.
- Look for mechanics that create a unique moment: travel, interruption, concealment, revelation, resource pressure, positional control, or a setup/combo payoff.
- Make the effect feel like a scene from the story, not just a mechanical payload.
- Only fall back to the standard status toolbox when it truly matches the card better than a more original design.
- Keep effects implementable in the current architecture, but do not let “implementable” collapse into “same old Courage/Fear package.”

## Image Rule
Before generating art, check whether an image already exists for the card name under `Assets/Art/Cards`.
- If the user asks to reuse existing art, use that image and skip generation.
- If a same-name image exists and the user did not request regeneration, reuse it.
- If no suitable image exists, use the `new-image` skill and save to the correct card-art folder.

## Validation Checklist
- Correct deck file selected for alignment.
- New `cardId` is unique across all decks.
- Card schema matches existing cards of the same `type`.
- Action-linked cards point to the fields required by the current deck schema.
- Image exists (reused or newly generated) and card name/image naming is consistent.
- JSON remains valid.
