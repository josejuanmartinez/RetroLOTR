# RetroLOTR Agent Notes

This file provides repo-wide guidance for coding agents working in this project.

## Project Priorities

- Preserve existing game data and content unless the task explicitly asks for changes.
- Prefer small, verifiable edits over broad refactors.
- Keep lore tone, deck identity, and visual style consistent with the existing RetroLOTR project.

## Available Skills

Skills live under `.agents/skills/<skill-name>/SKILL.md`. Use them by name when instructing an agent.

| Skill | Trigger / use when |
|---|---|
| `deck-stats` | Regenerate `economy/deck_stats.json` after any card change (costs, grants, levels, troop type). Runs `python economy/gen_deck_stats.py`. |
| `new-card` | Add a new card entry to a modular deck JSON and wire its image. |
| `new-character-action` | Create or update a C# action class under `Assets/Scripts/Actions` and link it from the card JSON. |
| `create-card-logic` | Audit the full card art folder, find cards missing deck entries or action linkage, and resolve them interactively. |
| `deck-expansion-full` | Add fully integrated cards to a modular subdeck (data + logic + art) in one pass. |
| `subdeck-creator` | Expand a single modular subdeck one card at a time, enforcing diversity and flavor rules. |
| `card-description-writer` | Write or rewrite card `quote` and `actionEffect` text in lore-accurate style. |
| `new-image` | Generate new card art in the RetroLOTR retro painted style and save to the correct `Assets/Art/Cards/` subfolder. |
| `colorify` | Colorize an existing B&W card image using the standard two-pass workflow. |
| `colorify-banner` | Colorize a banner/crest image while preserving its transparent cutout silhouette. |
| `new-banner` | Generate a new heraldic banner sprite and save to `Assets/Art/UI/Alignment/Banners/`. |
| `extract-sprite-slices` | Extract sliced sub-assets from a Unity multi-sprite atlas into standalone PNG files. |

## Card And Deck Work

- Treat `Assets/Resources/Cards/Modular` as the source of truth for modular deck work.
- Do not move cards between shared decks, base decks, and subdecks unless explicitly requested.
- New subdeck cards should be genuinely new entries with unique names, ids, logic, and art.
- Avoid duplicating a card concept, gameplay role, or image across sibling subdecks.
- When expanding a modular subdeck, add one card at a time and verify deck count after each addition.
- Do not hardcode card names, card-to-action mappings, or tutorial card exceptions in gameplay code; resolve them from deck/card data or tutorial JSON instead.

### Card Mechanic Quality Bar — No Pure-Status Cards

A card fails this bar if its entire effect can be described as one of these templates and nothing else:

- `"All [X] gain [Status] (N turns)."`
- `"Target allied character: gain [Status] (N turns)."`
- `"Enemy [X] gain [Status] (N turns)."`
- `"Apply [Status] (N turns) to [X]."`

**Every card must include at least one mechanic that cannot be described as a buff/debuff to a group.** A status effect may appear as a *secondary* component alongside a primary mechanic, but it cannot be the whole card.

Good primary mechanics to reach for first:

| Category | Examples |
|---|---|
| Movement / repositioning | Teleport, forced displacement, extra movement, westward compulsion |
| Combat / damage | Fixed damage, troop loss, auto-hit, charge damage |
| Army modification | Unit type conversion (ma → hi), permanent troop gain, warship grant |
| Resource change | Gold steal, skill increase (`AddCommander`), loyalty boost |
| Information | Reveal hidden units, obscure scouting, reveal artifact sites |
| Terrain interaction | Forest fire, coastal reveal, mountain charge bonus |
| Targeted disruption | Halt + damage, card denial (Blocked), expose-then-damage |
| Resurrection / revival | Revive dead characters, extra action this turn |

When a status effect is the only mechanic on a card, reject the design and replace it with something from the list above. A status effect added *on top of* a real mechanic is fine and often good for flavor.

This rule was established after auditing 65 pure-status cards and migrating all of them — see `improving_terrible_status_effect_cards.md` for the reference implementations.

## Actions And Gameplay Logic

- Treat `Assets/Resources/Actions.json` and `Assets/Scripts/Actions` as the source of truth for action wiring.
- Follow existing `CharacterAction` and `EventAction` patterns instead of inventing parallel execution paths.
- Keep new effects implementable with current systems unless the task explicitly asks for system expansion.
- Reuse project naming and metadata conventions exactly, including existing legacy spellings such as `Emmissary`.

## Art And Image Work

- Save new card art under `Assets/Art/Cards/...` in the correct type-specific folder.
- New card art should be original and should not reuse or repurpose an existing card image as the shipped asset.
- Match the repo's black-and-white retro ink style for new generated card art.
- **Unity import rule:** every new card image must be imported with `Sprite Mode = Single` (`spriteMode: 1`). `Multiple` sprite mode breaks Addressables sprite lookup and causes the card to show no image in-game. If a `.meta` file shows `spriteMode: 2`, change it to `spriteMode: 1` and clear `internalIDToNameTable: []`.

## Card Text And Lore

- Keep card names and descriptions short, immersive, and lore-like.
- Prefer indirect Tolkien-inspired phrasing over modern, explanatory, or literal wording.
- When rewriting a card description, preserve the feeling of the source material rather than narrating the mechanics directly.
- If a card needs a quote or description, keep both parts:
  - a short lore-based immersive line
  - the effect text, stated clearly and without removing gameplay meaning
- When writing card, army, or UI text for abilities, troop types, or status effects, always append the matching `<sprite name="...">` icon alongside the term instead of leaving the raw name by itself.
- **Before inserting any `<sprite name="...">` tag, check `ProjectContext/available_sprites.md`** for the exact valid name. Using an unlisted name will show a broken icon in-game.

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
