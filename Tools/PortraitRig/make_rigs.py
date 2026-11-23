"""Builds the Spine idle rig (<ID>.spine.json) for portraits that do not have one, in the layout the hand-made rigs use.

    python Tools/PortraitRig/make_rigs.py            every portrait without a rig
    python Tools/PortraitRig/make_rigs.py HUM_F2     named portraits (overwrites)

Bones: root > head > hair_front, hair_back. Slots: hair_back, iris, base, eye_closed, hair_front.
The body mesh bends at the neck (found from the eyes), hair hangs from the head and sways at its tips.
Every mesh stays inside the layer's own non-empty rectangle, so it also works when the sprite is packed into an atlas.
"""
import json, os, random, sys, uuid
import numpy as np
from PIL import Image
from scipy import ndimage as ndi
from scipy.spatial import ConvexHull, Delaunay

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'Assets', 'Arts', 'Portraits')
W, H = 1024, 1536


def alpha(path):
    return np.array(Image.open(path).convert('RGBA'))[..., 3] if os.path.exists(path) else None


def world(px, py):
    return px - W / 2, H / 2 - py


def smooth(t):
    t = np.clip(t, 0, 1)
    return t * t * (3 - 2 * t)


def mesh(a, spacing, hull_points):
    """Convex outline of the layer plus a grid of inner points, triangulated. Returns points in pixels and triangles."""
    ys, xs = np.where(a > 8)
    if len(xs) < 200: return None
    corners = np.concatenate([np.stack([xs, ys], 1), np.stack([xs + 1, ys + 1], 1), np.stack([xs + 1, ys], 1), np.stack([xs, ys + 1], 1)]).astype(float)
    hull = corners[ConvexHull(corners).vertices]
    # resample the outline to an even number of points
    closed = np.vstack([hull, hull[:1]]); seg = np.hypot(*np.diff(closed, axis=0).T); total = seg.sum(); cum = np.concatenate([[0], np.cumsum(seg)])
    outline = np.array([[np.interp(d, cum, closed[:, 0]), np.interp(d, cum, closed[:, 1])] for d in np.linspace(0, total, hull_points, endpoint=False)])
    outline = np.vstack([hull, outline]); outline = outline[ConvexHull(outline).vertices]      # keeps the true corners, stays convex
    x0, x1, y0, y1 = xs.min(), xs.max() + 1, ys.min(), ys.max() + 1
    gx, gy = np.meshgrid(np.arange(x0 + spacing / 2, x1, spacing), np.arange(y0 + spacing / 2, y1, spacing))
    inner = np.stack([gx.ravel(), gy.ravel()], 1)
    from matplotlib.path import Path
    inside = Path(outline).contains_points(inner, radius=-spacing * 0.25)
    near = ndi.binary_dilation(a > 8, iterations=int(spacing))[np.clip(inner[:, 1].astype(int), 0, H - 1), np.clip(inner[:, 0].astype(int), 0, W - 1)]
    pts = np.vstack([outline, inner[inside & near]])
    tris = Delaunay(pts).simplices
    return pts, tris, len(outline)


def attachment(name, pts, tris, hull, weights, bones):
    """weights: list of dicts bone name -> weight per vertex. Vertices are stored bone-local, as Spine expects for weighted meshes."""
    order = [b['name'] for b in bones]; pos = {}
    for b in bones:
        p = pos.get(b.get('parent'), (0, 0)); pos[b['name']] = (p[0] + b.get('x', 0), p[1] + b.get('y', 0))
    verts = []
    for (px, py), ws in zip(pts, weights):
        wx, wy = world(px, py); ws = {k: v for k, v in ws.items() if v > 0.004}; s = sum(ws.values())
        verts.append(len(ws))
        for bone, w in ws.items():
            verts += [order.index(bone), round(wx - pos[bone][0], 2), round(wy - pos[bone][1], 2), round(w / s, 4)]
    return {'path': name, 'type': 'mesh', 'uvs': [round(float(v), 6) for p in pts for v in (p[0] / W, p[1] / H)],
            'triangles': [int(i) for t in tris for i in t], 'vertices': verts, 'hull': hull, 'width': W, 'height': H}


def wave(period, amplitude, phase=0.0, steps=4):
    keys = []
    for i in range(steps + 1):
        t = period * i / steps
        keys.append({'time': round(t, 3), 'value': round(amplitude * float(np.sin(2 * np.pi * (i / steps) + phase)), 4)})
    return keys


def build(code):
    d = os.path.join(ROOT, code); rnd = random.Random(code)
    empty = os.path.join(d, 'Iris', f'POR_{code}_BASE_IRIS_EMPTY.png')
    base_name = f'POR_{code}_BASE_IRIS_EMPTY' if os.path.exists(empty) else f'POR_{code}_BASE'
    base = alpha(empty if os.path.exists(empty) else os.path.join(d, f'POR_{code}_BASE.png'))
    eyes = alpha(os.path.join(d, f'POR_{code}_IRIS_SOLID_MASK.png'))
    lab, n = ndi.label(eyes > 0)
    centres = sorted(ndi.center_of_mass(eyes > 0, lab, [i + 1 for i in np.argsort(ndi.sum(eyes > 0, lab, range(1, n + 1)))[-2:]]), key=lambda c: c[1])
    (ly, lx), (ry, rx) = centres; ex, ey = (lx + rx) / 2, (ly + ry) / 2; iod = float(np.hypot(rx - lx, ry - ly))
    neck = (ex, ey + 1.7 * iod); crown = (ex, ey - 1.03 * iod); nape = (ex, ey + 0.56 * iod)
    hw, cw, nw = world(*neck), world(*crown), world(*nape)
    bones = [{'name': 'root'}, {'name': 'head', 'parent': 'root', 'x': round(hw[0]), 'y': round(hw[1]), 'length': 190},
             {'name': 'hair_front', 'parent': 'head', 'x': round(cw[0] - hw[0]), 'y': round(cw[1] - hw[1]), 'length': 100},
             {'name': 'hair_back', 'parent': 'head', 'x': round(nw[0] - hw[0]), 'y': round(nw[1] - hw[1]), 'length': 100}]
    atts = {}
    pts, tris, hull = mesh(base, 72, 40)
    head_w = smooth((neck[1] + 0.9 * iod - pts[:, 1]) / (1.3 * iod))            # 1 above the chin, 0 below the collar
    atts['base'] = {'base': attachment(base_name, pts, tris, hull, [{'head': float(w), 'root': float(1 - w)} for w in head_w], bones)}
    slots = [{'name': 'hair_back', 'bone': 'hair_back'}, {'name': 'iris', 'bone': 'head', 'attachment': 'iris'},
             {'name': 'base', 'bone': 'root', 'attachment': 'base'}, {'name': 'eye_closed', 'bone': 'head'}, {'name': 'hair_front', 'bone': 'hair_front'}]
    for slot, pivot in (('hair_back', nape), ('hair_front', crown)):
        m = mesh(alpha(os.path.join(d, f'POR_{code}_{slot.upper()}.png')), 56, 36) if os.path.exists(os.path.join(d, f'POR_{code}_{slot.upper()}.png')) else None
        if m is None: continue
        pts, tris, hull = m
        sway = 0.22 * smooth((pts[:, 1] - (ey + 0.2 * iod)) / (1.6 * iod))       # only what hangs below the eyes sways
        atts[slot] = {slot: attachment(f'POR_{code}_{slot.upper()}', pts, tris, hull, [{'head': float(1 - s), slot: float(s)} for s in sway], bones)}
        next(s for s in slots if s['name'] == slot)['attachment'] = slot
    for slot, suffix in (('iris', 'IRIS_SOLID_MASK'), ('eye_closed', 'EYE_CLOSED')):
        atts[slot] = {slot: {'path': f'POR_{code}_{suffix}', 'x': -round(hw[0]), 'y': -round(hw[1]), 'width': W, 'height': H}}
    golem = code.startswith('GOL')
    period = round(rnd.uniform(4.8, 5.6) if golem else rnd.uniform(3.6, 4.6), 2)
    tilt = rnd.uniform(0.9, 1.2) if golem else rnd.uniform(1.8, 2.6)
    blink = round(rnd.uniform(1.8, period - 0.6), 2)
    anim = {'slots': {'eye_closed': {'attachment': [{'time': 0}, {'time': blink, 'name': 'eye_closed'}, {'time': round(blink + 0.1, 2)}]}},
            'bones': {'head': {'rotate': wave(period, tilt), 'translatey': wave(period, 0.6 if golem else 1.5, -0.8)}}}
    if not golem:
        anim['bones']['hair_front'] = {'rotate': wave(period, rnd.uniform(2.4, 3.2), -0.45)}
        anim['bones']['hair_back'] = {'rotate': wave(period, rnd.uniform(3.0, 4.2), -0.85)}
    rig = {'skeleton': {'hash': '', 'spine': '4.3.42', 'x': -W // 2, 'y': -H // 2, 'width': W, 'height': H, 'fps': 30, 'images': './'},
           'bones': bones, 'slots': slots, 'skins': [{'name': 'default', 'attachments': atts}], 'animations': {'idle': anim}}
    out = os.path.join(d, f'{code}.spine.json')
    json.dump(rig, open(out, 'w', encoding='utf8'), separators=(',', ':'))
    if not os.path.exists(out + '.meta'):
        open(out + '.meta', 'w', encoding='utf8', newline='\n').write(
            f'fileFormatVersion: 2\nguid: {uuid.uuid4().hex}\nTextScriptImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
    return f"{code}: head {bones[1]['x']},{bones[1]['y']}  verts base {len(atts['base']['base']['uvs']) // 2}" + ''.join(f", {s} {len(atts[s][s]['uvs']) // 2}" for s in ('hair_front', 'hair_back') if s in atts)


if __name__ == '__main__':
    names = sys.argv[1:] or [c for c in sorted(os.listdir(ROOT)) if os.path.exists(os.path.join(ROOT, c, f'POR_{c}_IRIS_SOLID_MASK.png'))
                             and not os.path.exists(os.path.join(ROOT, c, f'{c}.spine.json'))]
    for c in names: print(build(c))
