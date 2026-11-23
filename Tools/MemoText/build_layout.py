"""Builds the text layout of every memo from its labels and renders a check sheet.

  python build_layout.py <labels_dir> <lines_dir> <drawings_dir> <out_json> [<preview_dir>]

A label gives each phrase the written lines it covers ("lines": ids from find_blocks.py), boxes in memo pixels
("boxes") or boxes measured on the 2000 px wide overlay ("dboxes"). The layout stores each phrase's box as fractions
of the sheet, how many rows it was written in and its row height, so the game can write any language in the same
place. The preview writes the Korean back onto the drawing-only sheet with the handwriting font, to check by eye.
"""
import glob, json, os, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFont

labels_dir, lines_dir, drawings_dir, out_json = sys.argv[1:5]
preview_dir = sys.argv[5] if len(sys.argv) > 5 else None
W, H = 2048, 1152
FONT = "C:/Windows/Fonts/NanumPenScript-Regular.ttf"


def inside(box, outer, pad=4):
    cx, cy = (box[0] + box[2]) / 2, (box[1] + box[3]) / 2
    return outer[0] - pad <= cx <= outer[2] + pad and outer[1] - pad <= cy <= outer[3] + pad


def wrap(draw, text, font, width):
    rows, row = [], ""
    for word in text.split(" "):
        test = (row + " " + word).strip()
        if row and draw.textlength(test, font=font) > width:
            rows.append(row)
            row = word
        else:
            row = test
    return rows + [row]


marks = json.load(open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "marks.json"), encoding="utf-8"))
sheets = {}
for path in sorted(glob.glob(os.path.join(labels_dir, "*.json"))):
    labels = json.load(open(path, encoding="utf-8"))
    name = os.path.splitext(labels["image"])[0]
    lines = {d["id"]: d["box"] for d in json.load(open(os.path.join(lines_dir, name + "_lines.json")))}
    phrases = []
    for p in labels["phrases"]:
        boxes = [lines[i] for i in p.get("lines", [])] + p.get("boxes", [])
        boxes += [[v * W / 2000 for v in b] for b in p.get("dboxes", [])]
        x0, y0 = min(b[0] for b in boxes), min(b[1] for b in boxes)
        x1, y1 = max(b[2] for b in boxes), max(b[3] for b in boxes)
        # rows: the phrase's own written lines grouped by baseline; boxes given by hand count by the sheet's
        # typical line height
        typical = float(np.median([b[3] - b[1] for b in lines.values()]))
        own = [lines[i] for i in p.get("lines", [])]
        if own and not p.get("boxes") and not p.get("dboxes"):
            centres = sorted((b[1] + b[3]) / 2 for b in own)
            rows = 1 + sum(1 for a, b in zip(centres, centres[1:]) if b - a > typical * 0.6)
        else:
            rows = max(1, round((y1 - y0) / (typical * 1.15)))
        row_h = (y1 - y0) / rows
        phrases.append({"key": p["key"], "ko": p["ko"],
                        "box": [round(x0 / W, 4), round(y0 / H, 4), round(x1 / W, 4), round(y1 / H, 4)],
                        "rows": int(rows), "rowHeight": round(row_h / H, 4)})
    sheet_marks = marks.get(name, {})
    for ph in phrases:
        ph["mark"] = next((kind for kind, keys in sheet_marks.items() if ph["key"] in keys), "")
    missing = [k for keys in sheet_marks.values() for k in keys if k not in {ph["key"] for ph in phrases}]
    assert not missing, (name, missing)
    sheets[name] = phrases

    if preview_dir:
        img = Image.open(os.path.join(drawings_dir, name + "_drawing.png")).convert("RGB").resize((W, H))
        draw = ImageDraw.Draw(img)
        for ph in phrases:
            bx = [ph["box"][0] * W, ph["box"][1] * H, ph["box"][2] * W, ph["box"][3] * H]
            # the game's rule: start at the written row height and shrink until the wrapped phrase fits the box
            size = int(min(bx[3] - bx[1], ph["rowHeight"] * H * 1.6))
            while size > 8:
                font = ImageFont.truetype(FONT, size)
                rows = wrap(draw, ph["ko"], font, bx[2] - bx[0])
                if len(rows) * size * 1.05 <= (bx[3] - bx[1]) * 1.12:
                    break
                size -= 1
            for i, row in enumerate(rows):
                draw.text((bx[0], bx[1] + i * size), row, fill=(20, 20, 20), font=font)
            draw.rectangle(bx, outline=(80, 160, 255))
        os.makedirs(preview_dir, exist_ok=True)
        img.save(os.path.join(preview_dir, name + "_preview.png"))

json.dump({"size": [W, H], "sheets": sheets}, open(out_json, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print({k: len(v) for k, v in sheets.items()})
