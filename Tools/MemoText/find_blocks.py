"""Finds the written lines on a landscape memo, apart from the drawings, and writes a numbered overlay to label them.

  python find_blocks.py <memo.png> <out_prefix>

Ink is split into strokes (connected components). Long or large strokes are drawings, arrows and underlines;
glyph-sized strokes are writing. Glyph strokes close together on one baseline are joined into a line.
Writes <out_prefix>_lines.json ([{"id", "box": [x0, y0, x1, y1]}], memo pixels) and <out_prefix>_overlay.png.
"""
import json, sys
import cv2, numpy as np

GLYPH_MAX = 70      # px: a stroke longer than this on either side is a drawing, arrow or underline
GLYPH_MIN_AREA = 6  # px: specks smaller than this are paper noise


def glyph_mask(gray):
    ink = (gray < 120).astype(np.uint8)
    n, labels, stats, _ = cv2.connectedComponentsWithStats(ink, connectivity=8)
    keep = np.zeros(n, bool)
    for i in range(1, n):
        x, y, w, h, area = stats[i]
        keep[i] = w <= GLYPH_MAX and h <= GLYPH_MAX and area >= GLYPH_MIN_AREA
    return keep[labels].astype(np.uint8)


def lines(mask):
    joined = cv2.dilate(mask, cv2.getStructuringElement(cv2.MORPH_RECT, (30, 7)))
    n, _, stats, _ = cv2.connectedComponentsWithStats(joined)
    out = []
    for i in range(1, n):
        x, y, w, h, _ = stats[i]
        if w < 22 or h < 18:
            continue  # lone dots, spots on a drawing
        out.append([int(x), int(y), int(x + w), int(y + h)])
    out.sort(key=lambda b: (b[1] // 30, b[0]))
    return out


if __name__ == "__main__":
    src, out = sys.argv[1], sys.argv[2]
    img = cv2.imread(src)
    boxes = lines(glyph_mask(cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)))
    data = [{"id": i, "box": b} for i, b in enumerate(boxes)]
    json.dump(data, open(out + "_lines.json", "w"))
    over = img.copy()
    for d in data:
        x0, y0, x1, y1 = d["box"]
        cv2.rectangle(over, (x0, y0), (x1, y1), (0, 0, 255), 2)
        cv2.putText(over, str(d["id"]), (x0 - 2, max(16, y0 - 2)), cv2.FONT_HERSHEY_SIMPLEX, 0.75, (255, 0, 0), 2)
    cv2.imwrite(out + "_overlay.png", over)
    print(len(data), "lines")
