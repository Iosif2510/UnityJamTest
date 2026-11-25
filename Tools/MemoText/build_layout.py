"""Builds the text layout of every memo from its labels and renders a check sheet.

  python build_layout.py <labels_dir> <lines_dir> <drawings_dir> <out_json> [<preview_dir>]

A label gives each phrase the written lines it covers ("lines": ids from find_blocks.py), boxes in memo pixels
("boxes") or boxes measured on the 2000 px wide overlay ("dboxes"). The layout stores each phrase's box as fractions
of the sheet, how many rows it was written in and its row height, so the game can write any language in the same
place. The preview writes the Korean back onto the drawing-only sheet with the handwriting font, to check by eye.
"""
import glob, json, os, sys
import numpy as np
import cv2
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


def ink_table(drawing_path):
    """Summed-area table of the drawing's ink (arrows, sketches, underlines), slightly thickened."""
    gray = cv2.resize(cv2.imread(drawing_path, cv2.IMREAD_GRAYSCALE), (W, H))
    ink = cv2.dilate((gray < 150).astype(np.uint8), np.ones((5, 5), np.uint8))
    return cv2.integral(ink)


def area_sum(table, b):
    x0, y0 = max(0, int(b[0])), max(0, int(b[1]))
    x1, y1 = min(W, int(b[2])), min(H, int(b[3]))
    if x1 <= x0 or y1 <= y0:
        return 0
    return int(table[y1, x1] - table[y0, x1] - table[y1, x0] + table[y0, x0])


def overlap(a, b):
    w = min(a[2], b[2]) - max(a[0], b[0])
    h = min(a[3], b[3]) - max(a[1], b[1])
    return w * h if w > 0 and h > 0 else 0


def place(boxes, table):
    """Nudges or trims each phrase box off the drawing's ink, without running into the other phrases.
    Every language shares these boxes, so this runs once on the layout, not per language."""
    moved = [list(b) for b in boxes]
    order = sorted(range(len(boxes)), key=lambda i: -area_sum(table, boxes[i]))
    trims = [(0, 0, 0, 0), (0.12, 0, 0, 0), (0, 0, 0.12, 0), (0.22, 0, 0, 0), (0, 0, 0.22, 0), (0, 0.15, 0, 0),
             (0, 0, 0, 0.15)]
    for i in order:
        x0, y0, x1, y1 = boxes[i]
        w, h = x1 - x0, y1 - y0
        if area_sum(table, boxes[i]) < 40:
            continue
        others = [moved[j] for j in range(len(boxes)) if j != i]
        best, best_score = moved[i], None
        for tl, tt, tr, tb in trims:
            for dy in range(-30, 31, 6):
                for dx in range(-48, 49, 8):
                    c = [x0 + w * tl + dx, y0 + h * tt + dy, x1 - w * tr + dx, y1 - h * tb + dy]
                    if c[0] < 8 or c[1] < 8 or c[2] > W - 8 or c[3] > H - 8:
                        continue
                    score = (area_sum(table, c) + 0.6 * sum(overlap(c, o) for o in others)
                             + 2.5 * (abs(dx) + abs(dy)) + 0.04 * (w * h - (c[2] - c[0]) * (c[3] - c[1])))
                    if best_score is None or score < best_score:
                        best, best_score = c, score
        moved[i] = best
    return moved


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
        # where the Korean original wrote it: easy mode marks this box when the original sheet is shown
        written = [round(x0 / W, 4), round(y0 / H, 4), round(x1 / W, 4), round(y1 / H, 4)]
        # handwriting fonts run wider than the hand that wrote the original: a little room on the right
        x1 = min(W - 8, x1 + (x1 - x0) * 0.12)
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
                        "rows": int(rows), "rowHeight": round(row_h / H, 4), "written": written})
    table = ink_table(os.path.join(drawings_dir, name + "_drawing.png"))
    before = sum(area_sum(table, [v * (W if k % 2 == 0 else H) for k, v in enumerate(ph["box"])]) for ph in phrases)
    placed = place([[v * (W if k % 2 == 0 else H) for k, v in enumerate(ph["box"])] for ph in phrases], table)
    for ph, b in zip(phrases, placed):
        ph["box"] = [round(b[0] / W, 4), round(b[1] / H, 4), round(b[2] / W, 4), round(b[3] / H, 4)]
        ph["rowHeight"] = round(min(ph["rowHeight"], (b[3] - b[1]) / H / ph["rows"]), 4)
    after = sum(area_sum(table, b) for b in placed)
    print(f"{name}: ink under phrase boxes {before} -> {after} px")
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
