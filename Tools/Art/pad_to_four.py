"""Pads images so both sides are a multiple of 4, for the browser build's texture compression.

  python Tools/Art/pad_to_four.py <list.txt | image.png ...>

DXT, the compression browsers take, works in 4x4 blocks: Unity leaves an image whose width or height is not a
multiple of 4 uncompressed in the WebGL build (a 1672x941 background is 6-12 MB instead of 1-2 MB). This adds 1-3
pixels on the right and top, copies of the edge pixels, so nothing new shows and seams stay invisible. Right and
top, because Unity counts sprite rectangles from the bottom-left corner: sprites cut from a sheet keep their places.
The picture, its pivot and the other layers drawn over it keep their place to within a pixel or two.
New images for the game should simply be made at sizes divisible by 4.
"""
import sys
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]


def pad(path: Path) -> str:
    image = Image.open(path)
    image.load()
    w, h = image.size
    nw, nh = (w + 3) // 4 * 4, (h + 3) // 4 * 4
    if (nw, nh) == (w, h):
        return f"ok      {w}x{h} {path}"
    mode = image.mode
    if mode not in ("RGB", "RGBA", "L", "LA"):
        image = image.convert("RGBA")
    top = nh - h
    out = Image.new(image.mode, (nw, nh))
    out.paste(image, (0, top))
    if nw > w:  # repeat the right column
        column = image.crop((w - 1, 0, w, h)).resize((nw - w, h))
        out.paste(column, (w, top))
    if top:  # repeat the top row (with the new corner)
        row = out.crop((0, top, nw, top + 1)).resize((nw, top))
        out.paste(row, (0, 0))
    info = {k: v for k, v in image.info.items() if k in ("dpi", "icc_profile")}
    out.save(path, **info)
    return f"padded  {w}x{h} -> {nw}x{nh} {path}"


args = sys.argv[1:]
paths = []
for a in args:
    p = Path(a)
    if p.suffix == ".txt":
        paths += [ROOT / line.strip() for line in p.read_text(encoding="utf-8").splitlines() if line.strip()]
    else:
        paths.append(p if p.is_absolute() else ROOT / p)
for p in paths:
    print(pad(p))
