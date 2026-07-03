---
name: show-images-inline
description: Display one or more images to the user by composing them into a grid PNG in the OS temp folder and opening it in the default image viewer. Use whenever the user asks to "show", "see", "display", or "view" an image — the Claude Code transcript cannot render images or ANSI art, so this is the display channel that actually works.
---

# Show Images Inline

Tile any number of images into a single labeled grid PNG (written to `%TEMP%`) and open it in the Windows default image viewer.

## Workflow

1. Collect the image paths the user wants to see (Glob for them if given loose names — check exact spelling).
2. Run `scripts/show_images_grid.py` with the paths.
3. The script writes `%TEMP%\show_images_<timestamp>.png` and opens it automatically.
4. Each cell is labeled with a selection marker — `A)`, `B)`, `C)` … by default (`--numbering numbers` for `1)`, `2)` …). The script prints the marker → path mapping to stdout.
5. Report the marker → filename mapping in chat, then wait for the user's answer. The user replies with markers ("B", "1 and 3", "all except C") to pick images or give per-image feedback — resolve their reply against the mapping.

## CLI Contract

Single image:
```powershell
python .agents/skills/show-images-inline/scripts/show_images_grid.py `
  "Assets/Art/Cards/Lands/Lithlad.png"
```

Multiple images, custom grid width:
```powershell
python .agents/skills/show-images-inline/scripts/show_images_grid.py `
  "Assets/Art/Cards/Lands/Lithlad.png" `
  "Assets/Art/Cards/Actions/Events/BarrowLilies.png" `
  --cols 2 --cell 600
```

## Parameters

| Flag | Default | Description |
|---|---|---|
| `images` | required | One or more image paths (PNG/JPG/anything Pillow reads) |
| `--cols` | auto (~square) | Grid columns; rows computed automatically |
| `--cell` | `512` | Max cell width/height in pixels (images are thumbnailed, aspect kept) |
| `--no-labels` | off | Skip the filename label under each cell |
| `--numbering` | `letters` | Selection marker style: `letters` (A, B, …), `numbers` (1, 2, …), or `none` |
| `--out` | temp file | Explicit output path instead of `%TEMP%` |
| `--no-open` | off | Write the grid but do not launch the viewer |

## Notes

- Filename stems are drawn as labels under each cell so grids of many cards stay identifiable.
- Windows-only open (`os.startfile`); pass `--no-open` elsewhere.
- Dependencies: Pillow (already installed system-wide).

## Completion Report

Always report:
- Number of images and grid dimensions (cols × rows)
- The marker → filename mapping (so the user can answer by marker)
- Output PNG path
- Whether the viewer was opened
