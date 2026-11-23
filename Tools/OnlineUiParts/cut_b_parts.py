# Cuts the online / loft UI parts out of the confirmed B-style mockups (Docs/UI_디자인_핸드오프/확정_시안_B안,
# handoff/ui-design branch) so the game can use the painted look before the designer's own parts arrive.
# Lettering is removed by inpainting (the text is drawn in code). Names follow 부품_규격표.md, so the designer's
# files can later replace these one for one.
#
#   python cut_b_parts.py <folder with B01.png .. B08.png> <output folder>
#
# B01..B08 are the eight mockups in order (01_위층공방_탁자 .. 08_이동중), all 1920x1080.
import sys, os
import numpy as np
import cv2
from PIL import Image, ImageFilter

SRC, OUT = sys.argv[1], sys.argv[2]
os.makedirs(OUT, exist_ok=True)
mock = {i: np.array(Image.open(os.path.join(SRC, f"B0{i}.png")).convert("RGB")) for i in range(1, 9)}
made = []


def crop(i, box):
    x0, y0, x1, y1 = box
    return mock[i][y0:y1, x0:x1].copy()


def erase_text(rgb, inset=0.12, thresh=34, grow=4, radius=9, keep=None):
    """Inpaints lettering: pixels that stand out from the local median inside the inner area."""
    h, w = rgb.shape[:2]
    k = max(15, (min(h, w) // 3) | 1)
    med = cv2.medianBlur(rgb, min(k, 101))
    diff = np.abs(rgb.astype(int) - med.astype(int)).sum(axis=2)
    mask = (diff > thresh).astype(np.uint8)
    ix, iy = int(w * inset), int(h * inset)
    inner = np.zeros_like(mask)
    inner[iy:h - iy, ix:w - ix] = 1
    if keep is not None:
        inner[keep] = 0
    mask *= inner
    mask = cv2.dilate(mask, np.ones((grow * 2 + 1, grow * 2 + 1), np.uint8))
    return cv2.inpaint(rgb, mask * 255, radius, cv2.INPAINT_TELEA)


def alpha_rounded(rgb, radius, soft=1.5):
    """Transparent outside a rounded rectangle that hugs the crop, plus a 2 px transparent margin."""
    h, w = rgb.shape[:2]
    m = Image.new("L", (w * 4, h * 4), 0)
    from PIL import ImageDraw
    ImageDraw.Draw(m).rounded_rectangle([8, 8, w * 4 - 9, h * 4 - 9], radius=radius * 4, fill=255)
    m = m.resize((w, h), Image.LANCZOS).filter(ImageFilter.GaussianBlur(soft * 0.5))
    return np.dstack([rgb, np.array(m)])


def key_background(rgb, bg_samples, tol=38, grow=1):
    """Alpha from distance to the surrounding background colours (for shapes on wood or felt)."""
    d = np.full(rgb.shape[:2], 999.0)
    for c in bg_samples:
        d = np.minimum(d, np.abs(rgb.astype(float) - np.array(c, float)).sum(axis=2))
    a = np.clip((d - tol) / tol, 0, 1)
    a = cv2.GaussianBlur(a, (3, 3), 0)
    a = cv2.morphologyEx(a, cv2.MORPH_OPEN, np.ones((3, 3), np.uint8))
    return np.dstack([rgb, (a * 255).astype(np.uint8)])


def pad4(rgba):
    """Pads to a multiple of 4 with at least 2 transparent pixels all round."""
    h, w = rgba.shape[:2]
    W, H = ((w + 4 + 3) // 4) * 4, ((h + 4 + 3) // 4) * 4
    out = np.zeros((H, W, 4), np.uint8)
    ox, oy = (W - w) // 2, (H - h) // 2
    out[oy:oy + h, ox:ox + w] = rgba
    return out


def save(name, rgba, note=""):
    if rgba.shape[2] == 3:
        rgba = np.dstack([rgba, np.full(rgba.shape[:2], 255, np.uint8)])
    rgba = pad4(rgba)
    Image.fromarray(rgba, "RGBA").save(os.path.join(OUT, name))
    made.append(f"{name:42s} {rgba.shape[1]}x{rgba.shape[0]}  {note}")


def tint(rgb, mul=(1, 1, 1), add=0, sat=1.0):
    f = rgb.astype(float)
    g = f.mean(axis=2, keepdims=True)
    f = g + (f - g) * sat
    f = f * np.array(mul) + add
    return np.clip(f, 0, 255).astype(np.uint8)


# ---- loft: table ------------------------------------------------------------------------------------------
# Wood frame + felt (B01, 1556x936). The memo cards are covered by felt cloned from the empty bands above and
# below them, so the centre stretches without showing card ghosts.
felt = crop(1, (337, 112, 1893, 1048))
band_top = felt[40:168, 34:-34].astype(float)
band_bot = felt[742:870, 34:-34].astype(float)
fill = felt.astype(float)
y, k, feather = 150, 0, 24
while y < 780:
    src = band_top if k % 2 == 0 else band_bot
    src = src if k % 4 < 2 else src[:, ::-1]
    hh = min(src.shape[0], 790 - y)
    if hh <= feather:
        break
    ramp = np.clip(np.arange(hh) / feather, 0, 1)[:, None, None]
    fill[y:y + hh, 34:-34] = fill[y:y + hh, 34:-34] * (1 - ramp) + src[:hh] * ramp
    y += hh - feather
    k += 1
fill = cv2.GaussianBlur(fill.astype(np.uint8), (0, 0), 0.6)
save("LOFT_Felt_9slice_96.png", alpha_rounded(fill, 10), "9-slice border 96")

# ---- loft: tabs --------------------------------------------------------------------------------------------
wood_bg = [(92, 46, 28), (110, 58, 34), (74, 36, 22), (128, 70, 40)]
tab_sel = erase_text(crop(1, (25, 117, 323, 432)), inset=0.1)
tab_nrm = erase_text(crop(1, (31, 444, 319, 623)), inset=0.1)
save("LOFT_Tab_Selected_9slice_40.png", alpha_rounded(tab_sel, 18), "9-slice border 40 (blue edge)")
save("LOFT_Tab_Normal_9slice_40.png", alpha_rounded(tab_nrm, 18), "9-slice border 40")
save("LOFT_Tab_Hover_9slice_40.png", alpha_rounded(tint(tab_nrm, (1.06, 1.05, 1.0), 6), 18), "9-slice border 40")
# Blink: a warm orange rim painted over the normal tab, used to point the narrator at a tab.
blink = tab_nrm.astype(float)
h, w = blink.shape[:2]
yy, xx = np.mgrid[0:h, 0:w]
edge = np.minimum(np.minimum(xx, w - 1 - xx), np.minimum(yy, h - 1 - yy))
rim = np.clip(1 - (edge - 6) / 8, 0, 1)[..., None]
blink = blink * (1 - rim) + np.array([226, 122, 46]) * rim
save("LOFT_Tab_Blink_9slice_40.png", alpha_rounded(blink.astype(np.uint8), 18), "9-slice border 40 (orange rim)")

# ---- top bar ----------------------------------------------------------------------------------------------
plate = erase_text(crop(1, (30, 14, 242, 80)), inset=0.14, thresh=40)
save("LOFT_Nameplate_3slice_32.png", key_background(plate, wood_bg, tol=34), "3-slice border 32")
save("UI_Timer_Hourglass.png", key_background(crop(1, (256, 18, 306, 80)), wood_bg, tol=34), "")
bar = crop(1, (316, 28, 1194, 72))
# Frame: the empty right part of the bar, its rounded end mirrored for the left.
right = bar[:, 560:]
track = np.concatenate([right[:, ::-1][:, :40], np.repeat(right[:, 60:61], 160, axis=1), right[:, -40:]], axis=1)
save("UI_Timer_Frame_3slice_20.png", key_background(track, wood_bg, tol=30), "3-slice border 20")
fillbar = bar[8:36, 10:520]
fillbar = np.concatenate([fillbar[:, :24], np.repeat(fillbar[:, 200:201], 120, axis=1), fillbar[:, :24][:, ::-1]], axis=1)
save("UI_Timer_Fill_3slice_12.png", alpha_rounded(fillbar, 12), "3-slice border 12")
chip_now = erase_text(crop(1, (1288, 22, 1450, 76)), inset=0.16, thresh=40)
chip_off = erase_text(crop(1, (1462, 24, 1566, 74)), inset=0.16)
save("LOFT_Chip_Current_3slice_24.png", key_background(chip_now, wood_bg, tol=32), "3-slice border 24")
save("LOFT_Chip_Upcoming_3slice_24.png", key_background(chip_off, wood_bg, tol=32), "3-slice border 24")
save("LOFT_Chip_Done_3slice_24.png", key_background(tint(chip_off, (0.86, 0.82, 0.76), 0, 0.8), wood_bg, tol=32),
     "3-slice border 24 (check drawn in code)")

# ---- unfolded state ---------------------------------------------------------------------------------------
felt_bg = [(96, 40, 120), (110, 52, 140), (84, 34, 104), (124, 62, 150)]
arrow = crop(2, (376, 414, 474, 626))
save("LOFT_Arrow_Normal.png", key_background(arrow, felt_bg, tol=40), "left arrow; flip in code for right")
save("LOFT_Arrow_Hover.png", key_background(tint(arrow, (1.08, 1.06, 1.02), 8), felt_bg, tol=40), "")
save("LOFT_Arrow_Pressed.png", key_background(tint(arrow, (0.86, 0.84, 0.82)), felt_bg, tol=40), "")
save("LOFT_Arrow_Disabled.png", key_background(tint(arrow, (0.8, 0.8, 0.8), 0, 0.2), felt_bg, tol=40), "")
big = erase_text(crop(2, (1398, 900, 1758, 1014)), inset=0.12, thresh=44)
big_rgba = key_background(big, felt_bg, tol=40)
save("UI_BigButton_Normal_9slice_32.png", big_rgba, "9-slice border 32")
save("UI_BigButton_Hover_9slice_32.png", key_background(tint(big, (1.07, 1.05, 1.0), 8), felt_bg, tol=40), "")
save("UI_BigButton_Pressed_9slice_32.png", key_background(tint(big, (0.85, 0.82, 0.8)), felt_bg, tol=40), "")
dis = erase_text(crop(3, (1398, 900, 1758, 1014)), inset=0.12, thresh=36)
save("UI_BigButton_Disabled_9slice_32.png", key_background(dis, felt_bg, tol=40), "9-slice border 32 ('sent' state)")
small = erase_text(crop(2, (478, 912, 678, 1000)), inset=0.14)
save("UI_SmallButton_Normal_9slice_24.png", key_background(small, felt_bg, tol=40), "9-slice border 24")
save("UI_SmallButton_Hover_9slice_24.png", key_background(tint(small, (1.06, 1.05, 1.0), 6), felt_bg, tol=40), "")
save("UI_SmallButton_Pressed_9slice_24.png", key_background(tint(small, (0.86, 0.84, 0.82)), felt_bg, tol=40), "")
toast = erase_text(crop(3, (826, 140, 1384, 218)), inset=0.1, thresh=44)
save("LOFT_Toast_3slice_36.png", key_background(toast, felt_bg + [(236, 200, 150)], tol=34), "3-slice border 36")

# Page dots: painted discs in the same palette (the mockup dots are only a few pixels wide).
def dot(size, colour, ring=None):
    s = size * 4
    im = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    from PIL import ImageDraw
    d = ImageDraw.Draw(im)
    if ring:
        d.ellipse([0, 0, s - 1, s - 1], fill=ring)
        d.ellipse([s * 0.16, s * 0.16, s * 0.84, s * 0.84], fill=colour)
    else:
        d.ellipse([0, 0, s - 1, s - 1], fill=colour)
    d.ellipse([s * 0.28, s * 0.18, s * 0.55, s * 0.4], fill=tuple(min(255, c + 40) for c in colour[:3]) + (120,))
    return np.array(im.resize((size, size), Image.LANCZOS))
save("LOFT_Dot_Unseen.png", dot(16, (240, 226, 200, 255)), "")
save("LOFT_Dot_Seen.png", dot(16, (176, 140, 104, 255)), "")
save("LOFT_Dot_Current.png", dot(26, (64, 150, 214, 255), ring=(240, 226, 200, 255)), "")

# ---- online windows ---------------------------------------------------------------------------------------
# Window frame from B05: its wood rim and brass corners; the inside is replaced with clean parchment.
win = crop(5, (512, 146, 1410, 930))
paper = tab_sel[44:-44, 44:-44]  # the selected tab's parchment, lettering already removed
paper = cv2.resize(paper, (win.shape[1] - 120, win.shape[0] - 120), interpolation=cv2.INTER_CUBIC)
paper = cv2.GaussianBlur(paper, (0, 0), 1.2)
win[60:-60, 60:-60] = paper
ring = np.zeros(win.shape[:2], np.uint8)
ring[52:68, 52:-52] = ring[-68:-52, 52:-52] = 255
ring[52:-52, 52:68] = ring[52:-52, -68:-52] = 255
win = cv2.inpaint(win, ring, 9, cv2.INPAINT_TELEA)
# The mockup's close button sat inside the top-right corner: paint it out with the paper around it.
hole = np.zeros(win.shape[:2], np.uint8)
hole[28:132, -146:-30] = 255
win = cv2.inpaint(win, hole, 15, cv2.INPAINT_TELEA)
win[28:132, -146:-30] = cv2.GaussianBlur(win, (0, 0), 3)[28:132, -146:-30]
save("UI_Window_9slice_72.png", alpha_rounded(win, 14), "9-slice border 72")
close = crop(5, (1284, 192, 1366, 266))
save("UI_CloseButton_Normal.png", key_background(close, [(242, 222, 184), (232, 206, 160)], tol=36), "")
save("UI_CloseButton_Hover.png", key_background(tint(close, (1.08, 1.04, 1.0), 8), [(242, 222, 184), (232, 206, 160)], tol=36), "")
save("UI_CloseButton_Pressed.png", key_background(tint(close, (0.84, 0.82, 0.8)), [(242, 222, 184), (232, 206, 160)], tol=36), "")
# The code letters sit in the middle; rebuild the field from its clean ends and one clean column.
raw_inp = crop(5, (578, 634, 1092, 738))
inp = np.concatenate([raw_inp[:, :70], np.repeat(raw_inp[:, 70:71], raw_inp.shape[1] - 140, axis=1),
                      raw_inp[:, -70:]], axis=1)
save("NET_Input_Normal_9slice_24.png", alpha_rounded(tint(inp, (1, 1, 1), 0, 0.15), 18),
     "9-slice border 24")
save("NET_Input_Focused_9slice_24.png", alpha_rounded(inp, 18), "blue rim")
err = inp.astype(float)
blue = (err[..., 2] > err[..., 0] + 30)
err[blue] = err[blue] * 0.2 + np.array([214, 84, 60]) * 0.8
save("NET_Input_Error_9slice_24.png", alpha_rounded(err.astype(np.uint8), 18), "red rim")
tile = erase_text(crop(6, (622, 334, 734, 460)), inset=0.12, thresh=24, grow=8, radius=14)
save("NET_CodeTile.png", alpha_rounded(tile, 16), "")
wait = erase_text(crop(6, (576, 592, 1348, 688)), inset=0.08)
save("NET_WaitBar_3slice_40.png", key_background(wait, [(242, 222, 184), (236, 210, 168)], tol=16), "3-slice border 40")
def toggle(on):
    from PIL import ImageDraw
    W_, H_ = 88 * 4, 48 * 4
    im = Image.new("RGBA", (W_, H_), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    track = (86, 150, 104, 255) if on else (176, 150, 124, 255)
    d.rounded_rectangle([0, 0, W_ - 1, H_ - 1], radius=H_ // 2, fill=track)
    d.rounded_rectangle([6, H_ * 0.52, W_ - 7, H_ - 7], radius=H_ // 3, fill=tuple(int(c * 0.86) for c in track[:3]) + (255,))
    cx = W_ - H_ // 2 if on else H_ // 2
    r = H_ // 2 - 14
    d.ellipse([cx - r, H_ // 2 - r + 4, cx + r, H_ // 2 + r + 4], fill=(120, 80, 50, 90))
    d.ellipse([cx - r, H_ // 2 - r, cx + r, H_ // 2 + r], fill=(246, 230, 200, 255))
    d.ellipse([cx - r * 0.5, H_ // 2 - r * 0.7, cx + r * 0.2, H_ // 2 - r * 0.1], fill=(255, 246, 228, 255))
    rgba = np.array(im.resize((88, 48), Image.LANCZOS)).astype(float)
    grain = np.random.default_rng(7).normal(0, 6, rgba.shape[:2])[..., None]
    rgba[..., :3] = np.clip(rgba[..., :3] + grain, 0, 255)
    return rgba.astype(np.uint8)
save("NET_Toggle_Off.png", toggle(False), "")
save("NET_Toggle_On.png", toggle(True), "")
foot = crop(8, (870, 292, 1048, 444))
hh_, ww_ = foot.shape[:2]
yy_, xx_ = np.mgrid[0:hh_, 0:ww_]
circle = np.clip((min(hh_, ww_) / 2 - 2 - np.hypot(yy_ - hh_ / 2, xx_ - ww_ / 2)) / 1.5, 0, 1)
save("NET_TravelIcon.png", np.dstack([foot, (circle * 255).astype(np.uint8)]), "")

# ---- backgrounds ------------------------------------------------------------------------------------------
# Travel: the stairs mockup with its window and bottom strip painted out (the game draws its own on top).
stairs = mock[8].copy()
hole = np.zeros(stairs.shape[:2], np.uint8)
hole[200:880, 456:1462] = 255
hole[1008:, :] = 255
small = cv2.resize(stairs, (480, 270), interpolation=cv2.INTER_AREA)
small = cv2.inpaint(small, cv2.resize(hole, (480, 270), interpolation=cv2.INTER_NEAREST), 9, cv2.INPAINT_TELEA)
filled = cv2.GaussianBlur(cv2.resize(small, (1920, 1080), interpolation=cv2.INTER_CUBIC), (0, 0), 6)
soft = cv2.GaussianBlur(hole.astype(float) / 255, (0, 0), 12)[..., None]
stairs = (stairs * (1 - soft) + filled * soft).astype(np.uint8)
stairs[1008:] = stairs[936:1008][::-1]  # mirror the steps over the old status strip
Image.fromarray(stairs).save(os.path.join(OUT, "BG_Travel.png"))
made.append("BG_Travel.png 1920x1080  (window 980x640 + bottom strip cover the mockup's)")
# Loft: the wood behind the tabs and table (left column of B01), stretched into a full-screen plank wall.
wood = crop(1, (0, 790, 330, 1080))
wood = cv2.resize(wood, (1920, 1080), interpolation=cv2.INTER_CUBIC)
wood = cv2.GaussianBlur(wood, (0, 0), 2.0)
top = cv2.resize(crop(1, (420, 2, 1260, 18)), (1920, 104), interpolation=cv2.INTER_CUBIC)
wood[:104] = top
Image.fromarray(wood).save(os.path.join(OUT, "BG_Loft.png"))
made.append("BG_Loft.png 1920x1080")
# Role picking: the stairs scene, blurred and darkened so the cover memo and the panel read on top.
role = cv2.GaussianBlur(stairs, (0, 0), 9) * 0.62
Image.fromarray(role.astype(np.uint8)).save(os.path.join(OUT, "BG_Briefing.png"))
made.append("BG_Briefing.png 1920x1080")

with open(os.path.join(OUT, "_parts.txt"), "w", encoding="utf-8") as f:
    f.write("\n".join(made) + "\n")
print("\n".join(made))
