---
name: create-card-logic
description: Audit cards and wire missing deck/action logic in RetroLOTR. Use when Codex must iterate card files, verify whether each card exists in any deck JSON, ensure linked character actions exist, and interactively resolve missing alignment/effect decisions with numbered user options.
---

# Create Card Logic

Audit card coverage and action wiring card-by-card with minimal noise.

## Source Of Truth
- `Assets/Resources/Cards/gandalf_deck.json`
- `Assets/Resources/Cards/sauron_deck.json`
- `Assets/Resources/Cards/saruman_deck.json`
- `Assets/Resources/Actions.json`
- `Assets/Scripts/Actions`
- Card files folder to scan:
  - Prefer `Assets/Cards` if present.
  - Otherwise use `Assets/Art/Cards`.

## Required Behavior
- Iterate cards by file name.
- If a card is already present in a deck and its linked character action exists, continue silently to the next card.
- Ask the user only when required information is missing.
- Use numbered options whenever asking for confirmation or a choice.

## Workflow
1. Resolve scan root (`Assets/Art/Cards`).
2. Build a normalized card-name index from file names (ignore extension, normalize case/spacing/underscores/hyphens).
3. For each card name, check whether it exists in any deck JSON (`gandalf`, `sauron`, `saruman`).
4. If card exists in at least one deck:
   - Of the card does not have an action linkage, use numbered options provided below to ask the action.
   - If card has action linkage, verify action exists in `Assets/Resources/Actions.json` and corresponding class exists under `Assets/Scripts/Actions`.
   - If both exist, continue silently.
   - If missing action metadata/class, ask how to fix (reuse existing action, create new action, or skip).
5. If card is not assigned to any deck, ask alignment using numbered options.
6. After deck assignment, if no action has set, ask for confirmation using numbered options.
7. If confirmed, create an action for it with the theme and nature of the card, and taking as examples other actions in Assets/Scripts/Actions. Feel free to ask for confirmation with numbered options
8. After each action ask if the user wants to continue or stop already. If stop, finish the loop.
9. If continue, Continue through remaining cards.

## Numbered Prompt Templates

Use this exact style for user interaction.

### Alignment Missing
`Card '<CardName>' is not in any deck. Choose alignment:`
1. `Gandalf (Recommended)`
2. `Sauron`
3. `Saruman`

### Missing/Invalid Action Linkage
`Card '<CardName>' has missing or invalid action linkage. Choose:`
1. `Reuse existing action (Recommended)`
2. `Create new action`
3. `Skip this card for now`

### Potential Action Confirmation
`Card '<CardName>' may need a character action. Proceed?`
1. `Yes, create/link an action (Recommended)`
2. `No, keep card without action`
3. `Skip for now`

### Effect Definition Needed
`I need the gameplay effect for '<CardName>'. Choose next step:`
1. `Use one of your suggested effects (Recommended)`
2. `I will provide custom effect text`
3. `Skip this card`

When option 1 is selected for effect definition, provide 3 concise suggestions inspired by existing actions and the card name/theme.

## Suggestion Rules
- Base suggestions on existing `Actions.json` patterns (cost, target, reward, risk).
- Keep effects implementable in current action architecture.
- Prefer simple, testable effects over novel mechanics.
- If uncertain, present a conservative default as recommended.

## Silent Processing Rule
- Do not report each successful card.
- Report only blockers, decisions required, and final summary.

## Final Summary Format
At the end of a run, provide:
1. Cards checked.
2. Cards already valid (silent-pass count).
3. Cards added to decks (with alignment).
4. Actions linked/created.
5. Cards skipped or pending user input.

## Integration Notes
- Use `new-card` skill for deck JSON edits.
- Use `new-character-action` skill for action class + `Actions.json` edits.
- Keep names consistent across card name, image name, action metadata, and class mapping.
