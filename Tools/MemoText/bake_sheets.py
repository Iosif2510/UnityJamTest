"""Bakes the landscape memos for the game: our ink on the painted parchment (look "D3"), and packs the text layout.

  python bake_sheets.py <sources_dir> <layout.json>

From <sources_dir> (Docs/PotionMemos/Landscape):
- <name>_drawing.png (no writing) -> Assets/Resources/MemoText/Sheets/<name>.png, used for every language but Korean
- <name>_ko_written.png (the Korean original) -> Assets/Resources/MemoText/Sheets_ko/<name>.png
The paper is one of Tools/MemoText/papers/paper_N.png (GPT-painted blank parchment run through paper_d3.py). It is
cut out of its white surround (transparent PNG, 1920x1080), the black pen is laid on as warm sepia ink inside a
MARGIN, and a hand-drawn double frame with corner curls goes around it. Layout boxes are fractions of the source
image; the game maps them through the same margin.
"""
import json, sys
from pathlib import Path
import numpy as np
import cv2

ROOT = Path(__file__).resolve().parents[2]
PAPERS = Path(__file__).with_name("papers")
OUT = ROOT / "Assets" / "Resources" / "MemoText"
SIZE = (1920, 1080)
MARGIN = 0.085
SEPIA = np.array([52, 30, 18], np.float32)  # RGB
# sheet -> (paper, the paper's dog-eared corner to keep the frame off: tl / tr / bl / br / none)
SHEET_PAPER = {"Cover": (4, "none"), "POT_01": (1, "tr"), "POT_02": (2, "tl"), "POT_03": (3, "br"),
               "POT_04": (1, "tr"), "POT_05": (2, "tl"), "POT_06": (3, "br"), "CounterManual": (4, "none")}


def paper(n):
    bgr = cv2.resize(cv2.imread(str(PAPERS / f"paper_{n}.png")), SIZE, interpolation=cv2.INTER_AREA)
    rgb = cv2.cvtColor(bgr, cv2.COLOR_BGR2RGB).astype(np.float32)
    hsv = cv2.cvtColor(bgr, cv2.COLOR_BGR2HSV).astype(np.float32)
    # the paper: everything that is not the near-white, unsaturated surround, as one piece
    is_paper = ((hsv[..., 1] > 22) | (hsv[..., 2] < 232)).astype(np.uint8)
    _, labels, stats, _ = cv2.connectedComponentsWithStats(is_paper)
    mask = (labels == 1 + int(np.argmax(stats[1:, cv2.CC_STAT_AREA]))).astype(np.uint8)
    mask = cv2.morphologyEx(mask, cv2.MORPH_CLOSE, np.ones((9, 9), np.uint8))
    mask = cv2.erode(mask, np.ones((5, 5), np.uint8))
    alpha = np.clip(cv2.GaussianBlur(mask.astype(np.float32), (0, 0), 0.9) * 1.1, 0, 1)
    white = np.clip((hsv[..., 2] - 222) / 22, 0, 1) * np.clip((34 - hsv[..., 1]) / 16, 0, 1)
    alpha *= 1 - cv2.dilate(white.astype(np.float32), np.ones((3, 3), np.uint8))
    rim = (cv2.GaussianBlur(mask.astype(np.float32), (0, 0), 3) < 0.9) & (mask > 0)
    rgb[rim] = rgb[rim] * 0.55 + np.array([196, 150, 102]) * 0.45
    return rgb, alpha


def frame(corner, seed):
    w, h = SIZE
    rng = np.random.default_rng(seed)
    lines = np.zeros((h, w), np.uint8)
    for inset, thick in ((48, 2), (58, 1)):
        x0, y0, x1, y1 = inset, inset, w - inset, h - inset
        pts = []
        for a, b, c, d in [(x0, y0, x1, y0), (x1, y0, x1, y1), (x1, y1, x0, y1), (x0, y1, x0, y0)]:
            for t in np.linspace(0, 1, 70):
                pts.append((a + (c - a) * t + rng.normal(0, 0.8), b + (d - b) * t + rng.normal(0, 0.8)))
        cv2.polylines(lines, [np.array(pts, np.int32)], True, 255, thick, cv2.LINE_AA)
    corners = {"tl": (58, 58, 1, 1), "tr": (w - 58, 58, -1, 1), "bl": (58, h - 58, 1, -1), "br": (w - 58, h - 58, -1, -1)}
    for name, (ux, uy, sx, sy) in corners.items():
        if name == corner:
            continue
        pts = [(ux + sx * (16 + (30 * (1 - t) + 7) * np.cos(t * 4.2) * 0.9),
                uy + sy * (16 + (30 * (1 - t) + 7) * np.sin(t * 4.2) * 0.9)) for t in np.linspace(0, 1, 44)]
        cv2.polylines(lines, [np.array(pts, np.int32)], False, 255, 2, cv2.LINE_AA)
        cv2.circle(lines, (int(ux + sx * 16), int(uy + sy * 16)), 4, 255, -1, cv2.LINE_AA)
    f = cv2.GaussianBlur(lines.astype(np.float32) / 255, (0, 0), 0.7) * 0.5
    if corner in corners:  # the dog-ear is painted there: keep the frame off it
        ux, uy, _, _ = corners[corner]
        yy, xx = np.mgrid[0:h, 0:w]
        f[(np.abs(xx - ux) < 170) & (np.abs(yy - uy) < 170)] = 0
    return f


def ink(source):
    gray = cv2.imread(str(source), cv2.IMREAD_GRAYSCALE)
    gh, gw = gray.shape
    gray = gray[int(gh * 0.012):gh - int(gh * 0.012), int(gw * 0.012):gw - int(gw * 0.012)]
    w, h = SIZE
    iw, ih = round(w * (1 - 2 * MARGIN)), round(h * (1 - 2 * MARGIN))
    gray = cv2.resize(gray, (iw, ih), interpolation=cv2.INTER_AREA)
    a = np.zeros((h, w), np.float32)
    ox, oy = (w - iw) // 2, (h - ih) // 2
    a[oy:oy + ih, ox:ox + iw] = np.clip((240 - gray.astype(np.float32)) / 140, 0, 1) ** 0.85
    return cv2.GaussianBlur(a, (0, 0), 0.55)


def bake(source, paper_n, corner, target, seed):
    rgb, alpha = paper(paper_n)
    a = ink(source)
    rgb = rgb * (1 - a[..., None]) + SEPIA * a[..., None]
    f = frame(corner, seed)
    rgb = rgb * (1 - f[..., None]) + SEPIA * f[..., None]
    rgba = np.dstack([np.clip(rgb, 0, 255), alpha * 255]).astype(np.uint8)
    target.parent.mkdir(parents=True, exist_ok=True)
    cv2.imwrite(str(target), cv2.cvtColor(rgba, cv2.COLOR_RGBA2BGRA))


sources, layout_path = Path(sys.argv[1]), Path(sys.argv[2])
for i, (name, (n, corner)) in enumerate(SHEET_PAPER.items()):
    bake(sources / f"{name}_drawing.png", n, corner, OUT / "Sheets" / f"{name}.png", i)
    bake(sources / f"{name}_ko_written.png", n, corner, OUT / "Sheets_ko" / f"{name}.png", i)
    print(name, f"paper_{n}")

layout = json.load(open(layout_path, encoding="utf-8"))
layout["margin"] = MARGIN
json.dump(layout, open(OUT / "layout.json", "w", encoding="utf-8"), ensure_ascii=False, indent=1)
