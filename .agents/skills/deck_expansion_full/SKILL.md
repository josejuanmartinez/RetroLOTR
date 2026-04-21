---
name: deck-expansion-full
description: Add fully integrated RetroLOTR cards to a modular subdeck, including deck JSON entries, gameplay logic/action wiring, current card-data wiring, and original art generation. Use when expanding any subdeck in Assets/Resources/Cards/Modular with complete card content.
---

# Deck Expansion (Full: Subdeck + Card + Art)

Use this skill when the user wants new modular subdeck cards fully implemented:
- Modular subdeck JSON entries
- Card data that matches the current loader and `Card` UI
- Gameplay logic/action wiring for every card that has an effect
- New card art generated through the `new-image` skill

## Scope
- Target one modular subdeck at a time under `Assets/Resources/Cards/Modular`
- Exclude the Encounter deck unless the user explicitly asks for it
- Follow the current schema used by `CardData` in `Assets/Scripts/UI/DeckManager.cs`
- Follow the rendering and lookup behavior in `Assets/Scripts/UI/Card.cs`
- Do not leave effect cards as text-only placeholders.
- Every new non-Character/non-Army card with an effect must be wired to actual gameplay logic before you report it as generated or complete.
- `actionClassName` and `action` are required whenever the card effect is not a passive data-only pattern already supported by the engine.
- If a new effect needs logic, create or reuse an action class during the same generation pass.
- Every new card mechanic must be unique and immersive; avoid near-duplicate effects, even across different card names.
- Do not keep repeating the same status effects over and over again across new cards.

## Source of Truth
- Decks: `Assets/Resources/Cards/*.json`
- Card model and loader: `Assets/Scripts/UI/Card.cs`, `Assets/Scripts/UI/DeckManager.cs`
- Art workflow: the bundled `new-image` skill
- Art files: `Assets/Art/Cards/**`
- For modular subdeck work, the target deck's `manifest.json` flavor fields and `DeckFlavorRules.md` are mandatory inputs.

## Required Workflow

1. **Pick the subdeck and count**
   - Identify the target subdeck JSON.
   - Ask how many cards to generate: `1`, `2`, or `5`.
   - Continue only after the user chooses a number.

2. **Create one card at a time**
   - Compute the next `cardId` globally across the modular deck set.
   - Add the new card object using the fields the current loader reads.
   - Match nearby subdeck entries for shape and tone.
   - Use `referenceDeckId` and `referenceCardId` when creating a reference card.
   - Set `spriteName` to match the final art file.
   - If the card has gameplay text, implement the gameplay logic in the same pass.
   - Set `actionClassName` and `action` to a valid action class whenever the card needs custom or reused gameplay logic.
   - Do not postpone logic implementation unless the user explicitly asks for concept-only card ideation.

3. **Generate original art**
   - Use the `new-image` skill for each card art asset.
   - Generate one card at a time.
   - Keep the prompt specific to the card name, subject, and scene.
   - Save the final image in the correct folder for the card type.

4. **Ask after each generation**
   - After each generated image, show the preview and ask whether to generate `1`, `2`, or `5` more cards.
   - Do not batch the next generation blindly.
   - Stop and wait for the user before continuing.

5. **Validation checklist**
   - Card IDs are unique within the modular set.
   - Every new card matches the current card schema used by the loader.
   - Every effect card has working gameplay logic, not just descriptive text.
   - `actionClassName` and `action` resolve to a real implemented class when required.
   - `spriteName` matches the final art file.
   - `referenceDeckId` and `referenceCardId` resolve when used.
   - JSON remains valid.

## Non-Negotiables
- Do not claim "new art" if images are only renamed or copied.
- Be explicit whether art is:
  1) newly generated,
  2) transformed derivative,
  3) reused existing.
- If the user asks for a subdeck expansion, deliver the card data, gameplay logic, and art layers that the subdeck actually needs.
- Never present a card as finished if its effect exists only in text and is not wired in code.
- Do not ship a card whose mechanic feels like a reskinned version of the last few additions.

## Suggested Output Report
After completion, report:
1. Cards created in the target subdeck (name + cardId)
2. Logic created or reused for each card (action class names)
3. Reference fields used, if any
4. Art files created (paths)
5. Any remaining TODOs (balance pass, text pass, art pass, etc.)
