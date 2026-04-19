---
name: card-description-writer
description: Write RetroLOTR card descriptions and flavor text. Use when creating or rewriting a card description that should begin with a short effect summary, then a blank line, then an italicized Lord of the Rings style quote that matches the card context.
---

# Card Description Writer

Write card descriptions in this exact shape:

1. Start with the card effect in short, clear wording.
2. Leave one blank line.
3. Wrap the quote block in `<align="center">...</align>`.
4. Inside that centered block, add a matching Lord of the Rings style quote inside `<color=#d3d3d388><i> </i></color>`.

## Output Rules

- Keep the effect line concise and gameplay-focused.
- Keep the quote brief, atmospheric, and fitting to the card.
- Make the quote feel like it could belong in the books, even if it is newly written.
- Preserve the exact blank-line separation between the effect and the quote.
- Center the quote block with `<align="center">` before and after the colored italic quote.
- Use light grey for the quote color, such as `#d3d3d388`.
- Do not add extra explanation unless the user asks for it.
