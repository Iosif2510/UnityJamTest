"""Bakes the drawing-only landscape memos onto aged paper for the game, and packs the text layout.

  python bake_sheets.py <drawings_dir> <layout.json>

Each drawing (<name>_drawing.png, black pen on white, no writing) is shrunk by MARGIN and multiplied onto its own
paper (Tools/MemoPaper/papers, the same paper each portrait memo used, turned landscape). Writes
Assets/Resources/MemoText/Sheets/<name>.png (1920x1080) and Assets/Resources/MemoText/layout.json, whose phrase
boxes are fractions of the drawing; the game maps them through the same margin.
"""
import json, sys
from pathlib import Path
from PIL import Image, ImageChops

ROOT = Path(__file__).resolve().parents[2]
PAPERS = ROOT / "Tools" / "MemoPaper" / "papers"
OUT = ROOT / "Assets" / "Resources" / "MemoText"
SIZE = (1920, 1080)
MARGIN = 0.04
SHEET_PAPER = {"Cover": "paper_1", "POT_01": "paper_2", "POT_02": "paper_5", "POT_03": "paper_6",
               "POT_04": "paper_7", "POT_05": "paper_3", "POT_06": "paper_8", "CounterManual": "paper_4"}

drawings, layout_path = Path(sys.argv[1]), Path(sys.argv[2])
(OUT / "Sheets").mkdir(parents=True, exist_ok=True)
for name, paper_name in SHEET_PAPER.items():
    paper = Image.open(PAPERS / f"{paper_name}.jpg").convert("RGB").rotate(90, expand=True)
    w, h = paper.size
    ch = round(w * SIZE[1] / SIZE[0])
    paper = paper.crop((0, (h - ch) // 2, w, (h - ch) // 2 + ch)).resize(SIZE, Image.LANCZOS)
    drawing = Image.open(drawings / f"{name}_drawing.png").convert("L")
    # the generated sheets carry a thin scan frame along the edges: whiten a 1.2 % border
    bw, bh = round(drawing.width * 0.012), round(drawing.height * 0.012)
    from PIL import ImageDraw
    edge = ImageDraw.Draw(drawing)
    for box in ((0, 0, drawing.width, bh), (0, drawing.height - bh, drawing.width, drawing.height),
                (0, 0, bw, drawing.height), (drawing.width - bw, 0, drawing.width, drawing.height)):
        edge.rectangle(box, fill=255)
    drawing = drawing.convert("RGB")
    inner = drawing.resize((round(SIZE[0] * (1 - 2 * MARGIN)), round(SIZE[1] * (1 - 2 * MARGIN))), Image.LANCZOS)
    page = Image.new("RGB", SIZE, "white")
    page.paste(inner, ((SIZE[0] - inner.width) // 2, (SIZE[1] - inner.height) // 2))
    ImageChops.multiply(page, paper).save(OUT / "Sheets" / f"{name}.png", optimize=True)
    print(name, paper_name)

layout = json.load(open(layout_path, encoding="utf-8"))
layout["margin"] = MARGIN
json.dump(layout, open(OUT / "layout.json", "w", encoding="utf-8"), ensure_ascii=False, indent=1)
