---
name: provide-feedback-over-image-options
description: Let the user give visual feedback on an image by opening a temp copy in MS Paint. Blocks until they save and close, then the annotated copy is read back. Use whenever the user wants to draw on, mark up, circle, or annotate an image (typically one they just picked from a show-images-inline grid) instead of describing changes in words.
---

# Provide Feedback Over Image Options

Open a temp copy of the chosen image in the system image editor (MS Paint), wait for the user to save and close, then read the annotated copy back to see their drawn feedback.

## Workflow

1. Resolve which image the user means. If they answer with a grid marker ("B"), map it through the marker → path list from the most recent show-images-inline run. Otherwise Glob for the name.
2. Run `scripts/annotate_image.py` with the path — **always with `run_in_background: true`**, because it blocks until the user finishes (possibly many minutes).
3. Tell the user Paint is open and they should draw their feedback, then **save and close** the window.
4. When the background task completes, parse its stdout:
   - `ANNOTATED: <path>` — the user saved feedback. Read `<path>` with the Read tool (it renders images) and interpret the markings. Compare against the original if helpful.
   - `UNCHANGED: <path>` (exit 2) — closed without saving; ask the user if they want to retry.
   - `TIMEOUT` (exit 3) — no save within the timeout; ask before retrying.
5. Act on the visual feedback: circles/arrows mark regions to change, drawn text is instructions. Restate your reading of the feedback in chat so the user can correct it.

## CLI Contract

```powershell
python .agents/skills/provide-feedback-over-image-options/scripts/annotate_image.py `
  "Assets/Art/Cards/PC/Hobbiton.jpg"
```

## Parameters

| Flag | Default | Description |
|---|---|---|
| `image` | required | Image to collect feedback on (a temp copy is edited, never this file) |
| `--editor` | `mspaint` | Editor executable to launch |
| `--timeout` | `1800` | Max seconds to wait for the editor session |
| `--out` | temp file | Explicit path for the editable copy instead of `%TEMP%` |

## Notes

- The original asset is **never** opened in the editor — the script copies it to `%TEMP%\feedback_<timestamp>_<name>` and detects a save via the copy's mtime (`copy2` preserves the source mtime, so any save is newer).
- Windows 11 Paint is a Store app: the `mspaint` launcher can exit immediately, so after the launched process exits the script keeps waiting while any Paint process (`mspaint.exe`, `PaintApp.exe`, `Paint.exe`) is running. A pre-existing unrelated Paint window will therefore hold the wait — ask the user to close stray Paint windows if it seems stuck.
- JPG/PNG both work; the copy keeps the source extension so Paint saves in place without a format dialog.

## Completion Report

Always report:
- The source image and the annotated-copy path
- Whether a save was detected (ANNOTATED / UNCHANGED / TIMEOUT)
- Your interpretation of each marking the user drew, as a checklist of requested changes
