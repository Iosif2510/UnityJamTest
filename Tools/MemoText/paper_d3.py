"""Turns a generated blank parchment into the memo paper (look "D3"): the AI image's fine grain is removed, its big
stains come back softly, broad brush strokes are laid over it, and the yellow is calmed. The white surround stays
white so the paper can be cut out when baking.

  python paper_d3.py <raw_parchment.png> <out.png>

The raw parchments come from GPT image edits (see Tools/MemoText/README.md); the outputs live in
Tools/MemoText/papers and are what bake_sheets.py uses.
"""
import sys
import numpy as np
import cv2

src_path, out_path = sys.argv[1:3]
src = cv2.imread(src_path)
h, w = src.shape[:2]
seed = abs(hash(out_path)) % 10000
rng = np.random.default_rng(seed)

# 1) painterly base: the fine grain knocked out at quarter size, edges kept
small = cv2.resize(src, (w // 4, h // 4), interpolation=cv2.INTER_AREA)
for _ in range(4):
    small = cv2.bilateralFilter(small, 9, 30, 9)
small = cv2.edgePreservingFilter(small, flags=1, sigma_s=40, sigma_r=0.25)
smooth = cv2.resize(small, (w, h), interpolation=cv2.INTER_CUBIC).astype(np.float32)

# 2) the original's big stains, as soft shapes only
f = src.astype(np.float32)
paper = smooth + 0.3 * (cv2.GaussianBlur(f, (0, 0), 22) - cv2.GaussianBlur(f, (0, 0), 70))

# 3) broad, mostly horizontal brush strokes, very low contrast
strokes = np.zeros((h, w), np.float32)
for _ in range(700):
    x, y = rng.uniform(0, w), rng.uniform(0, h)
    length, angle = rng.uniform(90, 220), rng.normal(0.05, 0.15)
    cv2.line(strokes, (int(x), int(y)), (int(x + length * np.cos(angle)), int(y + length * np.sin(angle))),
             float(rng.normal(0, 1)), int(rng.uniform(10, 22)), cv2.LINE_AA)
strokes = cv2.GaussianBlur(strokes, (0, 0), 9)
strokes /= strokes.std() + 1e-6
paper *= (1 + 0.013 * strokes)[..., None]

# 4) calmer yellow, a slightly lighter middle
hsv = cv2.cvtColor(np.clip(paper, 0, 255).astype(np.uint8), cv2.COLOR_BGR2HSV).astype(np.float32)
hsv[..., 1] *= 0.85
yy, xx = np.mgrid[0:h, 0:w]
rr = ((xx - w * 0.5) / (w * 0.55)) ** 2 + ((yy - h * 0.48) / (h * 0.6)) ** 2
hsv[..., 2] = np.clip(hsv[..., 2] * (1.05 - 0.06 * np.clip(rr, 0, 1.5)), 0, 255)
out = cv2.cvtColor(hsv.astype(np.uint8), cv2.COLOR_HSV2BGR)

# the surround stays white for the cut-out
src_hsv = cv2.cvtColor(src, cv2.COLOR_BGR2HSV)
out[(src.mean(axis=2) > 236) & (src_hsv[..., 1] < 24)] = 255
cv2.imwrite(out_path, out)
print(out_path)
