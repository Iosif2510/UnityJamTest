"""Cuts the Chinese / Japanese memo font down to the characters the memos use.

  python subset_font.py [<Xiaolai-Regular.ttf>]

Xiaolai (OFL, https://github.com/lxgw/kose-font, v3.126) is 22 MB; the game only needs the characters in the
ja / zh-Hans / zh-Hant memo tables plus kana, ASCII and CJK punctuation. Without a path the full font is downloaded
to the system temp folder first. Writes Assets/Resources/MemoText/Xiaolai-Regular.ttf. Run again after any change
to those tables, or new characters will show as blanks.
"""
import json, sys, tempfile, urllib.request
from pathlib import Path
from fontTools import subset

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Assets" / "Resources" / "MemoText"
URL = "https://github.com/lxgw/kose-font/releases/download/v3.126/Xiaolai-Regular.ttf"
TABLES = ["ja", "zh-Hans", "zh-Hant"]

source = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(tempfile.gettempdir()) / "Xiaolai-Regular.ttf"
if not source.exists():
    print("downloading", URL)
    urllib.request.urlretrieve(URL, source)

chars = set()
for lang in TABLES:
    table = OUT / f"text_{lang}.json"
    if table.exists():
        chars.update("".join(json.load(open(table, encoding="utf-8")).values()))
codes = {ord(c) for c in chars}
codes |= set(range(0x20, 0x7F))                       # ASCII
codes |= set(range(0x3000, 0x3040))                   # CJK punctuation
codes |= set(range(0x3040, 0x3100))                   # hiragana, katakana
codes |= set(range(0xFF01, 0xFF5F)) | {0x2026, 0x2192, 0x2190, 0x00B7, 0x2014}  # full-width forms, … → ← · —

options = subset.Options()
options.layout_features = ["*"]
options.name_IDs = ["*"]
options.notdef_outline = True
font = subset.load_font(str(source), options)
subsetter = subset.Subsetter(options)
subsetter.populate(unicodes=codes)
subsetter.subset(font)
target = OUT / "Xiaolai-Regular.ttf"
subset.save_font(font, str(target), options)
print(f"{len(chars)} table characters -> {target.stat().st_size // 1024} KB")
