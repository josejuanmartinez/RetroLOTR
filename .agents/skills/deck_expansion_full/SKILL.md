---
name: deck-expansion-full
description: Add fully integrated RetroLOTR cards (deck entries + action logic + action metadata + original art pipeline) across Gandalf/Sauron/Saruman decks. Use when expanding decks with complete, playable card content.
---

# Deck Expansion (Full: Card + Logic + Art)

Use this skill when the user wants **new cards fully implemented** (not placeholders):
- Deck JSON entries
- Action class logic wiring
- `Actions.json` metadata wiring
- New card art files in proper folders

## Scope
- Decks: `GandalfDeck.json`, `SauronDeck.json`, `SarumanDeck.json`
- Excludes Encounter deck unless user explicitly asks.
- Must produce playable cards with valid `actionClassName`/`actionId` and real image assets.

## Source of Truth
- Decks: `Assets/Resources/Cards/*.json`
- Action metadata: `Assets/Resources/Actions.json`
- Action classes: `Assets/Scripts/Actions/**`
- Art: `Assets/Art/Cards/**`

## Required Workflow

1. **Plan cards per deck**
   - Identify missing themes/mechanics between decks.
   - Propose 5 cards per selected deck.
   - Wait for user approval.

2. **Create deck entries**
   - Compute next `cardId` globally across alignment decks (`max + 1`).
   - Add new card objects with valid schema.
   - Set `spriteName`, `actionClassName`, `action`, `actionId`.

3. **Create logic classes**
   - Create one class per new card under `Assets/Scripts/Actions/Events` (or relevant type folder).
   - Minimum valid implementation: wrapper class inheriting existing stable action logic.
   - Preferred implementation: custom logic in `Initialize(...)` following project pattern.

4. **Register actions in Actions.json**
   - Add one metadata entry per new action.
   - Ensure unique `actionId` and matching `className`.
   - Keep required fields consistent with existing action schema.

5. **Generate original art**
   - Use `gpt-image-1.5` via `skills/openai-image-gen/scripts/gen.py` with explicit prompts.
   - One image per new card, square format (`1024x1024` or `512x512`).
   - **Generate one-by-one (single request per run), not big batches**: large multi-image runs can timeout/fail mid-way.
   - **After each generated image, send a preview to the user before generating the next one** (normal iterative review flow).
   - Style target: retro LOTR black-and-white ink, high-contrast print texture.
   - Save to the correct card folder (usually `Assets/Art/Cards/Actions/Events`).

6. **Post-process to pure B/W**
   - Apply strict threshold pass to force output pixels to `0/255` when needed.
   - Keep final files game-ready and readable.

7. **Validation checklist**
   - Card IDs unique across alignment decks.
   - Every new card points to an existing action class and action metadata entry.
   - Every new action metadata entry resolves to class in code.
   - Every new card has an image file with matching `spriteName`.
   - JSON remains valid.

## Non-Negotiables
- Do not claim “new art” if images are only renamed/copied.
- Be explicit whether art is:
  1) newly generated,
  2) transformed derivative,
  3) reused existing.
- If user asks for full implementation, deliver all 3 layers: card + logic + art.

## Suggested Output Report
After completion, report:
1. Cards created per deck (name + cardId)
2. New action classes created (paths)
3. New `Actions.json` IDs
4. Art files created (paths)
5. Any remaining TODOs (balance pass, custom logic pass, VFX, etc.)
