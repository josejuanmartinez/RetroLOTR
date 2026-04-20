---
name: card-description-writer
description: Write RetroLOTR card flavor text. Use when creating or rewriting card quotes and action effects in the JSON data model. Non-action cards store only a quote. Event, Action, and Spell cards store an actionEffect plus a quote. The rendered body is generated automatically from the card data.
---

# Card Description Writer

Write card text in this exact shape:

1. Do not write a freeform description blob.
2. For non-action cards, store only a short quote in the `quote` field.
3. For Event, Action, and Spell cards, store the effect text in `actionEffect`.
4. Put the quote in the `quote` field too.
5. Keep both fields concise, lore-rooted, and unique.

For PC cards:

1. Store only the quote in `quote`.
2. The body is generated automatically from the PC's region and resources.
3. Keep the quote to exactly one sentence.
4. Make the line feel rooted in the specific lore of the PC, not just the map region.
5. Do not use generic openings like "At {place}" or repeated sentence scaffolds.
6. Never reuse the same sentence scaffold across multiple PCs.
7. Use Tolkien quotes or fake them; write an original line that could plausibly sit beside it.

## Output Rules

- Keep `actionEffect` concise and gameplay-focused.
- Keep quotes brief, atmospheric, and tied to the specific lore of the card.
- Make the lore feel like it could belong in the books, even if it is newly written.
- Keep PC quotes to exactly one sentence.
- Do not add extra explanation unless the user asks for it.
