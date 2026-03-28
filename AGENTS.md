# RetroLOTR Agent Notes

This file provides repo-wide guidance for coding agents working in this project.

## Project Priorities

- Preserve existing game data and content unless the task explicitly asks for changes.
- Prefer small, verifiable edits over broad refactors.
- Keep lore tone, deck identity, and visual style consistent with the existing RetroLOTR project.

## Card And Deck Work

- Treat `Assets/Resources/Cards/Modular` as the source of truth for modular deck work.
- Do not move cards between shared decks, base decks, and subdecks unless explicitly requested.
- New subdeck cards should be genuinely new entries with unique names, ids, logic, and art.
- Avoid duplicating a card concept, gameplay role, or image across sibling subdecks.
- When expanding a modular subdeck, add one card at a time and verify deck count after each addition.

## Actions And Gameplay Logic

- Treat `Assets/Resources/Actions.json` and `Assets/Scripts/Actions` as the source of truth for action wiring.
- Follow existing `CharacterAction` and `EventAction` patterns instead of inventing parallel execution paths.
- Keep new effects implementable with current systems unless the task explicitly asks for system expansion.
- Reuse project naming and metadata conventions exactly, including existing legacy spellings such as `Emmissary`.

## Art And Image Work

- Save new card art under `Assets/Art/Cards/...` in the correct type-specific folder.
- New card art should be original and should not reuse or repurpose an existing card image as the shipped asset.
- Match the repo's black-and-white retro ink style for new generated card art.

## Safety And Collaboration

- Do not overwrite or revert unrelated user changes.
- If the worktree is dirty, isolate your changes and avoid broad formatting churn.
- If a decision has hidden design or balance consequences, pause and ask the user before committing.
- When asking the user a question, present choices as a numbered list using the exact style `1.`, `2.`, `3.` and end with: `Please choose a number and I will implement that option`.

Example:

Which effect should this card use?
1. Radius 1: enemies gain Halted.
2. Radius 2: enemies gain Fear.
3. Single target: enemy loses Hope.
Please choose a number and I will implement that option.
