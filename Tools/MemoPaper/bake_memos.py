"""Bake the in-game narrator memos: white print sheets (Docs/PotionMemos) multiplied onto aged paper.

The print versions stay pure white for A4 printing. The game shows the sheets on screen, so each one is
multiplied onto its own yellowed, worn paper texture and written to Assets/Arts/Memos (same file names,
so the existing .meta files and sprite references keep working).

Usage (from the repository root):  python Tools/MemoPaper/bake_memos.py
Requires Pillow.
"""
from pathlib import Path

from PIL import Image, ImageChops

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "Docs" / "PotionMemos"
TARGET = ROOT / "Assets" / "Arts" / "Memos"
PAPERS = Path(__file__).with_name("papers")

# Blank paper left around the writing on every side, as a fraction of the sheet's width/height.
# The print sheets run almost to the edge; on screen the sheet needs room to read as a piece of paper.
MARGIN = 0.07

# Every sheet lies on a different piece of paper. The counter manual is written with a thinner pen,
# so it gets the lightest paper; the cover gets the cleanest one.
SHEET_PAPER = {
    "Cover": "paper_1",
    "POT_01": "paper_2",
    "POT_02": "paper_5",
    "POT_03": "paper_6",
    "POT_04": "paper_7",
    "POT_05": "paper_3",
    "POT_06": "paper_8",
    "CounterManual": "paper_4",
}


def main():
    for name, paper_name in SHEET_PAPER.items():
        sheet = Image.open(SOURCE / f"{name}.png").convert("RGB")
        paper = Image.open(PAPERS / f"{paper_name}.jpg").convert("RGB")
        if paper.size != sheet.size:
            paper = paper.resize(sheet.size, Image.LANCZOS)
        width, height = sheet.size
        inner = sheet.resize((round(width * (1 - 2 * MARGIN)), round(height * (1 - 2 * MARGIN))), Image.LANCZOS)
        page = Image.new("RGB", sheet.size, "white")
        page.paste(inner, ((width - inner.width) // 2, (height - inner.height) // 2))
        ImageChops.multiply(page, paper).save(TARGET / f"{name}.png", optimize=True)
        print(f"{name}: {paper_name}, {sheet.size[0]}x{sheet.size[1]}")


if __name__ == "__main__":
    main()
