---
description: Open an image in MS Paint so the user can draw feedback on a temp copy; read the annotations back once they save and close.
argument-hint: <image path or grid marker like "B">
---

Follow the project skill at `.agents/skills/provide-feedback-over-image-options/SKILL.md` for: $ARGUMENTS

Read the SKILL.md first, then execute its workflow. Key points: resolve a grid marker against the most recent show-images-inline mapping; run the script with `run_in_background: true` since it blocks until the user saves and closes the editor; when it completes, Read the `ANNOTATED:` path and report your interpretation of the drawn feedback as a checklist.
