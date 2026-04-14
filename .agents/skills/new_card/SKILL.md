---
name: new-card
description: Create or update RetroLOTR cards by adding entries to the correct deck JSON (Gandalf, Sauron, or Saruman), handling card images, and wiring action cards to existing or newly created character actions. Use when Codex must add a new card, duplicate/variant card, or card-linked action.
---

# New Card

Create card definitions in deck JSON files and ensure action/image links are valid.

## Source Of Truth
- `Assets/Resources/Cards/gandalf_deck.json`
- `Assets/Resources/Cards/sauron_deck.json`
- `Assets/Resources/Cards/saruman_deck.json`
- The owning card JSON entry for action-linked cards; this repo does not use a separate action registry file
- `Assets/Scripts/Cards/CardTypeEnum.cs` (valid card types)

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
2. Whether the card is an action card that needs a gameplay action.
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
5. For action cards, keep action linkage coherent:
   - `actionClassName` and `action` must match the intended action class/action name.
   - `actionId` must match the linked action data on the card record.
6. Insert the new card object in a stable location (near similar cards or at deck end) and keep JSON formatting consistent.

## Creativity Rule
- Do not default to the same familiar status packages, especially repeated use of `Fear`, `Halted`, `Hope`, `Encouraged`, `Hidden`, or `Blocked`.
- Prefer a card-specific mechanic that better expresses the lore, scene, or role of the card.
- If a simple status effect is the best fit, make sure it is doing distinct work and not just another copy of the last few cards.
- For new action cards, look first for a fresh play pattern: movement manipulation, conditional targeting, resource changes, terrain interaction, cleansing, setup/reward loops, or other distinct tactical identities.
- If a proposed effect feels like “another Courage/Fear card,” reject it and choose a more original mechanic unless the lore strongly justifies the reuse.
- When reusing an existing status, use it as a deliberate choice, not the default answer.

## Action Card Rule
If the card is a character action:
1. Ask whether to reuse an existing action or create a new one.
2. If creating new, use the `new-character-action` skill to implement the action class first, then point the card JSON entry at it.
3. Then set card action fields to the created action identifiers.

If reusing, confirm the exact action/class name and `actionId` from the existing card JSON entry and link to it.

## Effect Design Guidance
- Start from the card's lore function, not from a status-effect bucket.
- Look for mechanics that create a unique moment: travel, interruption, concealment, revelation, resource pressure, positional control, or a setup/combo payoff.
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
- Action cards point to valid action identifiers/classes.
- Image exists (reused or newly generated) and card name/image naming is consistent.
- JSON remains valid.
