"""Bakes the landscape memos onto aged paper for the game, and packs the text layout.

  python bake_sheets.py <sources_dir> <layout.json>

From <sources_dir> (Docs/PotionMemos/Landscape):
- <name>_drawing.png (no writing) -> Assets/Resources/MemoText/Sheets/<name>.png, used for every language but Korean
- <name>_ko_written.png (the Korean original) -> Assets/Resources/MemoText/Sheets_ko/<name>.png
Each is shrunk by MARGIN and multiplied onto its own paper (Tools/MemoPaper/papers, the paper each portrait memo
used, turned landscape), 1920x1080. The layout goes to Assets/Resources/MemoText/layout.json; its boxes are
fractions of the source image and the game maps them through the same margin.
"""
import json, sys
from pathlib import Path
from PIL import Image, ImageChops, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
PAPERS = ROOT / "Tools" / "MemoPaper" / "papers"
OUT = ROOT / "Assets" / "Resources" / "MemoText"
SIZE = (1920, 1080)
MARGIN = 0.04
SHEET_PAPER = {"Cover": "paper_1", "POT_01": "paper_2", "POT_02": "paper_5", "POT_03": "paper_6",
               "POT_04": "paper_7", "POT_05": "paper_3", "POT_06": "paper_8", "CounterManual": "paper_4"}


def paper(name):
    sheet = Image.open(PAPERS / f"{name}.jpg").convert("RGB").rotate(90, expand=True)
    w, h = sheet.size
    ch = round(w * SIZE[1] / SIZE[0])
    return sheet.crop((0, (h - ch) // 2, w, (h - ch) // 2 + ch)).resize(SIZE, Image.LANCZOS)


def bake(source, paper_name, target):
    ink = Image.open(source).convert("L")
    # the generated sheets carry a thin scan frame along the edges: whiten a 1.2 % border
    bw, bh = round(ink.width * 0.012), round(ink.height * 0.012)
    edge = ImageDraw.Draw(ink)
    for box in ((0, 0, ink.width, bh), (0, ink.height - bh, ink.width, ink.height),
                (0, 0, bw, ink.height), (ink.width - bw, 0, ink.width, ink.height)):
        edge.rectangle(box, fill=255)
    inner = ink.convert("RGB").resize((round(SIZE[0] * (1 - 2 * MARGIN)), round(SIZE[1] * (1 - 2 * MARGIN))),
                                      Image.LANCZOS)
    page = Image.new("RGB", SIZE, "white")
    page.paste(inner, ((SIZE[0] - inner.width) // 2, (SIZE[1] - inner.height) // 2))
    target.parent.mkdir(parents=True, exist_ok=True)
    ImageChops.multiply(page, paper(paper_name)).save(target, optimize=True)


sources, layout_path = Path(sys.argv[1]), Path(sys.argv[2])
for name, paper_name in SHEET_PAPER.items():
    bake(sources / f"{name}_drawing.png", paper_name, OUT / "Sheets" / f"{name}.png")
    bake(sources / f"{name}_ko_written.png", paper_name, OUT / "Sheets_ko" / f"{name}.png")
    print(name, paper_name)

layout = json.load(open(layout_path, encoding="utf-8"))
layout["margin"] = MARGIN
json.dump(layout, open(OUT / "layout.json", "w", encoding="utf-8"), ensure_ascii=False, indent=1)
