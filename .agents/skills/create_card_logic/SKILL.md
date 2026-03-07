---
name: create-card-logic
description: Audit cards and wire missing deck/action logic in RetroLOTR. Use when Codex must iterate card files, verify whether each card exists in any deck JSON, ensure linked character actions exist, and interactively resolve missing alignment/effect decisions with numbered user options.
---

# Create Card Logic

Audit card coverage and action wiring card-by-card with minimal noise.

## Source Of Truth
- `Assets/Resources/Cards/GandalfDeck.json`
- `Assets/Resources/Cards/SauronDeck.json`
- `Assets/Resources/Cards/SarumanDeck.json`
- `Assets/Resources/Actions.json`
- `Assets/Scripts/Actions`
- Card files folder to scan: `Assets/Art/Cards` (full tree), including:
  - `Assets/Art/Cards/Actions`
  - `Assets/Art/Cards/Actions/Spells`
  - `Assets/Art/Cards/Actions/Events` (including nested folders)

## Required Behavior
- Iterate cards by file name.
- Do not narrow the scan to `Actions` only when unresolved cards may exist in `Spells` or other card subfolders.
- **Presence check is mandatory before any question:** for each candidate card, first check all deck JSONs for any of these matches:
  - `name` normalized match
  - `actionClassName` match
  - `action` match
  - `actionId` match (when known)
- If a card is already present in any deck and its linked character action exists, continue silently to the next card.
- **Never ask alignment/deck assignment for a card that is already present** (even if image filename differs).
- Ask the user only when required information is missing.
- Use numbered options whenever asking for confirmation or a choice.
- Use deck-card schema as source of truth: action cards use `actionClassName`, `actionId`, `action`, and card-owned requirements (`*SkillRequired`, `*Required`).
- For action validation, require both:
  - action metadata exists in `Assets/Resources/Actions.json`
  - action class exists under `Assets/Scripts/Actions` (including subfolders)
- Before proposing effects, verify required resources/mechanics exist in code. If missing (for example a new resource type), ask whether to map to existing mechanics or expand core systems.
- Prefer data-driven requirement rendering (card fields) over hardcoded UI checks tied to specific card/action names.
- Never implement or modify gameplay logic before the user chooses one proposed logic option.

## Workflow
1. Resolve scan root (`Assets/Art/Cards`).
2. Collect candidate image files from the full scan root, explicitly including `Actions/Spells` and `Actions/Events`; ignore obvious non-card/template files (for example `CardFrame` variants).
3. Build a normalized card-name index from file names (ignore extension, normalize case/spacing/underscores/hyphens).
4. For each candidate card, perform **presence resolution before any prompt** using all of:
   - normalized `name`
   - normalized `actionClassName`
   - normalized `action`
   - `actionId` (if derivable from name/action)
   - support camel/pascal/spacing equivalence (e.g. `SellIron`, `Sell Iron`, `sell_iron`, `sell-iron`).
5. If card exists in at least one deck:
   - If the card has valid linkage (action metadata + action class), continue silently.
   - If linkage exists but is invalid, ask how to fix (reuse existing action, create new action, or skip).
   - **Never ask alignment/deck assignment for existing cards.**
6. If card is not assigned to any deck after the full presence resolution, ask alignment using numbered options.
7. After deck assignment, if the card needs a character action, proceed directly to effect-definition without asking for separate confirmation.
8. Present 3 concise logic options for the card (option 1 should be conservative/recommended), then always include option 4 exactly as: `You will provide the text`.
9. Wait for the user's option choice before any code changes.
10. Implement only the selected option (or the user's custom text if option 4).
11. After each resolved card, automatically continue through remaining cards unless the user explicitly asks to stop.

## Numbered Prompt Templates

Use this exact style for user interaction.

### Alignment Missing
`Card '<CardName>' is not in any deck. Choose alignment:`
1. `Gandalf`
2. `Saruman`
3. `Gandalf+Saruman`
4. `Sauron`
5. `Gandalf+Sauron`
6. `Sauron+Saruman`
7. `All alignments`

### Missing/Invalid Action Linkage
`Card '<CardName>' has missing or invalid action linkage. Choose:`
1. `Reuse existing action`
2. `Create new action`
3. `Skip this card for now`

### Effect Definition Needed
`I need the gameplay effect for '<CardName>'. Choose next step:`
1. `<Suggested logic A>`
2. `<Suggested logic B>`
3. `<Suggested logic C>`
4. `You will provide the text`

When presenting this prompt, always include one short sentence per suggested option before the numbered list.

## Status Reference
Use these meanings when proposing card effects, durations, and balance. Prefer existing statuses over inventing new ones.

- `MorgulTouch`: lasts 7 turns; target loses 10 health at the start of each turn. If health reaches 0 during this effect, the character becomes a `Nazgul` and joins `Sauron`.
- `Encouraged`: immune to `Fear`, `Despair`, `Halted`, and `Blocked` while active.
- `Fear`: if the character is not an army commander, each turn there is a 50% chance they lose their action.
- `Hope`: heals 5 at turn start and ignores `Despair` penalties while active.
- `Despair`: `-1` to each skill that is above 1, to a minimum of 1. Skills already at 0 stay at 0.
- `Haste`: `+2` movement while active.
- `Burning`: lasts at least 3 turns; deals 5 damage each turn. If the target is an army commander on a forest tile, the army also loses 1 random troop once. Applying `Burning` removes `Frozen`.
- `Poisoned`: lasts at least 5 turns; deals 5 damage each turn. When 3 turns remain, the target also gains `Fear`. Healing removes `Poisoned`.
- `Frozen`: no movement, `-10%` defense, cannot gain `Haste`; on mountain tiles defense penalty is `-25%`. Applying `Frozen` removes `Burning`.
- `Blocked`: the character cannot move or act for the turn while it lasts.
- `Halted`: movement is reduced on the next turn.
- `Hidden`: used for stealth/untargetability style effects.
- `RefusingDuels`: the character cannot initiate or receive duels while active.
- `ArcaneInsight`: `+1 Mage` while active.
- `Strengthened`: about `+10%` army attack for army commanders.
- `Fortified`: about `+10%` army defense for army commanders.

## Suggestion Rules
- Base suggestions on existing `Actions.json` patterns (cost, target, reward, risk).
- Keep effects implementable in current action architecture.
- Prefer simple, testable effects over novel mechanics.
- If uncertain, present a conservative default as recommended.

## Silent Processing Rule
- Do not report each successful card.
- Report only blockers, decisions required, and final summary.
- Exception: after each card is resolved (created/linked/skipped), provide a short per-card summary that states:
  - card name
  - deck/alignment decision
  - whether action logic was created, linked, reused, or skipped
  - concrete artifacts changed (deck JSON entry, `Actions.json` entry, action class file path) when applicable

## Final Summary Format
At the end of a run, provide:
1. Cards checked.
2. Cards already valid (silent-pass count).
3. Cards added to decks (with alignment).
4. Actions linked/created.
5. Cards skipped or pending user input.

## Per-Card Summary Format
After each resolved card, provide:
1. Card name.
2. Alignment/deck assignment.
3. Action outcome (`created`, `linked/reused`, `none`, or `skipped`).
4. Files changed for that card (if any).
5. Logic summary in plain language:
   - what the action does at gameplay level
   - who/what it can target
   - success/failure gate (for example difficulty roll or required conditions)
   - key effects and duration/value scaling

## Integration Notes
- Use `new-card` skill for deck JSON edits.
- Use `new-character-action` skill for action class + `Actions.json` edits.
- Keep names consistent across card name, image name, action metadata, and class mapping.
- When a dynamic-cost card needs symbolic requirements, encode them in card data fields (for example `jokerRequired`) and keep UI logic generic.
- For cards not present in any deck, default to creating/linking an action when action logic is implied by the card image/category; do not ask a separate proceed/confirm question before presenting effect options.
