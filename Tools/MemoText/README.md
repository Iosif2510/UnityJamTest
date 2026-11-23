# Memo text (drawings and words kept apart)

The narrator's memos are landscape drawings without writing, plus a text layout, so every language uses the same
pictures. Sources are in `Docs/PotionMemos/Landscape/`:

- `<sheet>_ko_written.png`: the Korean landscape memo as generated (reference for where things were written)
- `<sheet>_drawing.png`: the same memo with all writing removed (image edit), drawings and arrows untouched
- `<sheet>_lines.json`: written lines found on the Korean memo (`find_blocks.py`)

Steps (from the repository root):

1. `python Tools/MemoText/find_blocks.py Docs/PotionMemos/Landscape/<sheet>_ko_written.png <prefix>` to find lines
   (also writes a numbered overlay to label them).
2. `labels/<sheet>.json`: each phrase, its key, its Korean text and the lines or boxes it covers.
   `marks.json`: the phrases easy mode highlights.
3. `python Tools/MemoText/build_layout.py Tools/MemoText/labels Docs/PotionMemos/Landscape Docs/PotionMemos/Landscape <layout.json> [<preview_dir>]`
4. `python Tools/MemoText/bake_sheets.py Docs/PotionMemos/Landscape <layout.json>` bakes the drawings onto paper into
   `Assets/Resources/MemoText/Sheets/` and copies the layout to `Assets/Resources/MemoText/layout.json`.
5. Translations: `text_<lang>.json` in `Assets/Resources/MemoText/` (key `"<sheet>.<phrase>"`); `text_en.py` makes
   the English one. A phrase missing from a table falls back to Korean.

In the game `MemoSheetView` writes each phrase into its box with the handwriting font (Nanum Pen Script, OFL),
shrinking it to fit. Chinese and Japanese need their own handwriting font before their tables are added.
