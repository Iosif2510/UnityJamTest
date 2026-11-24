"""Cuts LXGW WenKai down to the Japanese and Chinese characters the game shows, for builds without system fonts.

  python Tools/WebFont/subset_web_cjk.py [<LXGWWenKai-Regular.ttf>]

A browser (the WebGL build) cannot lend the game the computer's CJK fonts the way the desktop builds do
(Core/Loc.cs, LocFonts), so Japanese and Chinese would show as boxes there. This writes
Assets/Resources/Fonts/WebCJK.ttf with every kana / CJK character found in Assets/Resources, Assets/GameData and the
scripts, plus ASCII and CJK punctuation. It is the TextMeshPro fallback when no system CJK font is found, and the
fallback font of the Maplestory fonts for the uGUI texts.

LXGW WenKai (SIL OFL 1.1, https://github.com/lxgw/LxgwWenKai) is about 25 MB. Without a path, the copy in
Build/fonts_dl next to the project is used, else it is downloaded to the system temp folder. Run again after adding
text in Japanese or Chinese, or new characters will show as boxes in the browser.
"""
import sys, tempfile, urllib.request
from pathlib import Path
from fontTools import subset

ROOT = Path(__file__).resolve().parents[2]
ASSETS = ROOT / "Assets"
OUT = ASSETS / "Resources" / "Fonts" / "WebCJK.ttf"
URL = "https://github.com/lxgw/LxgwWenKai/releases/download/v1.330/LXGWWenKai-Regular.ttf"


def cjk(o):
    return 0x3000 <= o <= 0x30FF or 0x3400 <= o <= 0x9FFF or 0xF900 <= o <= 0xFAFF or 0xFF00 <= o <= 0xFFEF


def source_font():
    if len(sys.argv) > 1:
        return Path(sys.argv[1])
    local = ROOT.parent / "Build" / "fonts_dl" / "LXGWWenKai-Regular.ttf"
    if local.exists():
        return local
    temp = Path(tempfile.gettempdir()) / "LXGWWenKai-Regular.ttf"
    if not temp.exists():
        print("downloading", URL)
        urllib.request.urlretrieve(URL, temp)
    return temp


codes = set()
files = list((ASSETS / "Resources").rglob("*.json")) + list((ASSETS / "GameData").rglob("*.json")) \
    + list((ASSETS / "Scripts").rglob("*.cs"))
for path in files:
    codes |= {ord(c) for c in path.read_text(encoding="utf-8", errors="ignore") if cjk(ord(c))}
found = len(codes)
codes |= set(range(0x20, 0x7F))                      # ASCII
codes |= set(range(0x3000, 0x3100))                  # CJK punctuation, hiragana, katakana
codes |= set(range(0xFF01, 0xFF5F))                  # full-width forms
codes |= {0x00B7, 0x2014, 0x2026, 0x2190, 0x2192, 0x00D7}  # · — … ← → ×

options = subset.Options()
options.layout_features = ["*"]
options.name_IDs = ["*"]
options.notdef_outline = True
font = subset.load_font(str(source_font()), options)
subsetter = subset.Subsetter(options)
subsetter.populate(unicodes=codes)
subsetter.subset(font)
# a modified copy may not carry the Reserved Font Names ('LXGW', '霞鹜'...): it is renamed WebCJK
for record in font["name"].names:
    if record.nameID in (1, 3, 4, 16, 21):
        record.string = "WebCJK"
    elif record.nameID == 6:
        record.string = "WebCJK-Regular"
    elif record.nameID in (2, 17, 22):
        record.string = "Regular"
OUT.parent.mkdir(parents=True, exist_ok=True)
subset.save_font(font, str(OUT), options)
print(f"{found} CJK characters in use -> {OUT.relative_to(ROOT)} ({OUT.stat().st_size // 1024} KB)")
