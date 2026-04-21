---
name: subdeck-creator
description: Add cards to a RetroLOTR modular subdeck until it has exactly 30 cards. Use when Codex must expand a specific level-2 subdeck under Assets/Resources/Cards/Modular, preserve the common -> base -> subdeck hierarchy, avoid card duplication across decks, and process additions one card at a time by delegating each card implementation to the card-creation workflow.
---

# Subdeck Creator

Expand one modular subdeck to exactly 30 cards.

## Scope

Work only with the modular hierarchy:

- common/shared decks
- base faction decks
- level-2 subdecks

Do not recreate or use legacy monolithic faction decks.
Do not edit, move, remove, rename, or repurpose cards that already exist in shared decks, base decks, parent decks, sibling subdecks, or any other deck.

## Source Of Truth

- `Assets/Resources/Cards.json`
- `Assets/Resources/Cards/Modular/manifest.json`
- target subdeck JSON in `Assets/Resources/Cards/Modular`
- target base deck JSON in `Assets/Resources/Cards/Modular`
- `DeckFlavorRules.md`

Always read the target subdeck's `thematic`, `fantasy`, `mechanicalPillars`, `themeLanes`, `avoidThemes`, and `visualDirections` from `manifest.json` before choosing or designing any card.

## Required Rules

- Treat the chosen subdeck as the only place that owns its specialty cards.
- Do not duplicate cards across `SharedBase`, base decks, or sibling subdecks.
- Never move cards out of a base deck into a subdeck.
- Never touch existing base cards, parent cards, shared cards, sibling-subdeck cards, or cards from other decks except to read and verify ownership.
- Keep common cards in common decks, general faction cards in base decks, and specialty cards in the target subdeck.
- Any card added to the target subdeck must be a genuinely new card entry (new name/id) not already present in `SharedBase`, the base deck, or sibling subdecks.
- Each added card must have newly created gameplay logic for that card; do not reuse another card's logic implementation as the shipped logic for the new card.
- Each added card must have a newly created illustration generated for that card; do not reuse, copy, or repurpose another card image.
- The target subdeck must finish with exactly `30` cards.
- Process additions one card at a time.
- Push beyond the most obvious surface motif of the subdeck; do not over-concentrate cards on the same literal joke, prop, meal, catchphrase, or scene fragment.
- Build the subdeck around a varied thematic cluster, not a single repeated reference. For any theme, actively look for contrast across people, places, moods, tactics, consequences, symbols, and story beats.
- Keep a running anti-repetition check while expanding the deck. If a proposed card feels like "another version of" an existing motif in the same subdeck, reject it unless it unlocks a clearly distinct mechanic and visual identity.
- Do not let any one narrow motif dominate the additions. As a rule of thumb, avoid spending more than roughly one quarter of the newly added cards on the same micro-theme.
- Vary gameplay patterns across additions. Avoid repeatedly producing simple stat tweaks, duplicate timing windows, or lightly renamed versions of the same effect structure.
- Do not solve too many cards with the same status package (for example repeatedly defaulting to Halt, Fear, Hidden, Hope, Courage, or other familiar soft-control bundles). Even when themes differ, push for distinct mechanic identities and broader creativity in the actual play pattern.
- Do not keep repeating the same status effects over and over again across the subdeck.
- When choosing a new card effect, prefer a mechanic that feels unique to that card's story beat instead of another status loop. Reach for movement, terrain, resource, positioning, reveal, sacrifice, interruption, or setup/payoff ideas before falling back to the usual status trio.
- Every added card must have a unique, immersive mechanic; if it reads like a renamed copy of an existing card, reject it and choose a different design.
- If a candidate card keeps collapsing into the same repeated status effects, reject it and choose a different thematic angle rather than forcing another near-duplicate.
- When proposing or implementing a card, explicitly check whether the mechanic feels like "another version of the last few cards." If yes, reject it and choose a more surprising but still coherent design.
- Vary visual composition across additions. Avoid repeated subject matter, camera framing, prop focus, and scene setup that would predictably yield near-duplicate art.
- For each card, create the card completely, then generate its image, then automatically show/send that image to the user without waiting to be asked.
- Wait for explicit user confirmation before continuing to the next card.
- Do not start proposing, implementing, or generating the next card until the user has approved the current card image.
- After each card is added, re-count the subdeck before proposing the next card.
- Stop immediately when the subdeck reaches `30`.
- If the subdeck already has `30`, make no card changes.
- If the subdeck has more than `30`, do not remove cards automatically; report the overflow and stop.

## Mandatory Delegation

For each card addition, use the existing card workflow rather than inventing a parallel process:

- Use `$create-card-logic` to audit whether the candidate already exists and whether action linkage is valid.
- Use `new-card` to create a new card in the target subdeck JSON only; never use it to transfer, copy, or mutate cards from base/shared/parent/other decks.
- Use `new-character-action` to create new gameplay logic for every added card.
- Use `new_image` to generate the card image for that single card.

Never batch-create multiple cards in one step. One card per pass only.
Finish one card completely before starting the next one.
After generating the image, show it to the user and ask whether to proceed.
Do not move to the next card until the user explicitly confirms the current card image.

## Workflow

1. Read `Assets/Resources/Cards.json` and `Assets/Resources/Cards/Modular/manifest.json`.
2. Resolve the chosen subdeck id, its base deck, and its faction.
3. Count the current cards in the target subdeck.
4. If count is `30`, report success and stop.
5. If count is greater than `30`, report the count and stop.
6. Read `DeckFlavorRules.md` and the subdeck thematic text from the manifests.
7. Build a compact theme map for the subdeck before choosing cards:
   - identify 5 to 8 distinct theme lanes the subdeck can support
   - include at least one lane each for character/persona, event/conflict, place/journey, object/symbol, and tonal reversal or consequence when the source material allows it
   - note which lanes are already represented by existing cards so new additions can fill gaps instead of repeating the loudest existing motif
8. Determine how many cards are missing to reach `30`.
9. Propose or audit exactly one candidate card at a time for the target subdeck.
10. Before committing to that card, run a diversity check against cards already in the subdeck:
   - theme check: is this drawing from an underused lane rather than the most obvious repeated one?
   - mechanic check: does it add a genuinely different play pattern?
   - visual check: can its illustration be framed in a meaningfully different way from recent additions?
   - if any answer is no, reject the candidate and choose a stronger one
11. For that single card, run the normal card-creation flow:
   - verify it is not already owned by shared, base, parent, sibling, or any other deck
   - if it already exists elsewhere, reject it and choose a different candidate
   - create new logic specific to that card; do not ship reused logic from another card
   - create it as a brand-new card and add it only to the target subdeck
   - do not modify, move, or delete any existing card outside the new card being created
12. Generate a brand-new image for that card; do not reuse an existing illustration.
13. When generating the image, give `new_image` a distinct visual brief that intentionally differs from the last few cards in subject, composition, and scene energy.
14. Show the generated image to the user and ask for confirmation to proceed.
15. Pause and wait for explicit user confirmation.
16. If approved, re-count the subdeck.
17. Repeat from step 9 until the count is exactly `30`.

## Candidate Selection Heuristics

Choose cards in this order:

1. Strong lore-title match for the subdeck.
2. Coverage of an underused theme lane within the subdeck.
3. Strong mechanic match for the subdeck.
4. Strong tone match for the subdeck.
5. Distinct visual potential from cards already in the subdeck.
6. If still unclear, keep the card out of the subdeck.

Do not pad with generic economy or filler cards just to hit `30`.
Prefer archetype-defining cards, signature events, race-linked units, and mechanically coherent support pieces.
Prefer surprising but defensible interpretations over obvious repeated references.
When a subdeck risks collapsing into a meme or single running gag, widen the card pool to adjacent story material, consequences, oppositions, and setting details that still support the deck identity.

## Creativity Guardrails

For each subdeck, deliberately spread candidate ideas across several of these lenses where applicable:

- characters and relationships
- locations and travel beats
- tools, artifacts, food, signals, and other symbolic objects
- tricks, deceptions, negotiations, interruptions, rescues, and setbacks
- environmental hazards and scene conditions
- emotional tones such as mischief, dread, embarrassment, relief, obsession, or escalation
- aftermath, consequences, and reactions to the theme rather than just the theme's cause

Mechanical creativity matters as much as theme creativity. Try to make each added card feel like it plays differently from the last few additions, not just like the same effect with a new name.
Make the result feel immersive in play and in lore, not just balanced on paper.

If the subdeck title suggests a playful or narrow premise, do not stay trapped at the literal surface. Expand outward to second-order ideas: what the theme disrupts, enables, attracts, ruins, reveals, or changes.

Before adding any card, compare it to the nearest existing cards in the target subdeck and reject it if it is merely:

- a synonym of an existing card concept
- the same joke with a different noun
- the same gameplay shell with cosmetic renaming
- likely to produce near-identical artwork to an existing card

Aim for a subdeck that feels like a curated mini-set with internal variety, not a stack of interchangeable riffs.

## Questions To Ask

Ask the user only when one of these is true:

- more than one candidate clearly fits and the choice is materially different
- a new gameplay effect must be invented
- the subdeck theme is too weakly defined to choose responsibly

When asking, use numbered options and continue one card at a time after the answer.

## Per-Card Output

After each added card, report:

1. card name
2. target subdeck
3. confirmation that new logic was created for this card
4. short note on why the card improves thematic/mechanical/visual diversity
5. image shown and awaiting/received user confirmation
6. files changed
7. new subdeck count in the form `count=NN/30`

## Final Output

At the end, report:

1. target subdeck
2. starting count
3. ending count
4. cards added
5. any cards considered but skipped
