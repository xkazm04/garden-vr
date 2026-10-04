"""T-TER-049: jar+cork silhouette vs the reference outline at JarG1.

The reference outline is traced by hand from gridded crops of A1-03-night-moss-1.png (REF_LEFT / REF_RIGHT below,
(y, x) pixels in the 1824 x 1024 frame, about 3 px). A candidate is a list of lathe profiles ((r, y) metres in the
jar frame); the JarG1 camera (eye 0, 0.175, -0.44, look 0, 0.072, 0, fov 28.2, 0.4 deg down) projects each revolved
surface and the union is filled per row. IoU is of the filled jar+cork mask against the traced polygon.

    python apps/terrarium/Art/Scripts/s6_sil.py check          # projection sanity vs S5 planes.json
    python apps/terrarium/Art/Scripts/s6_sil.py iou            # baseline C numbers
"""
import json
import math
import os
import sys

import numpy as np
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, "..", "..", "..", ".."))
W, H = 1824, 1024
REF_PNG = os.path.join(REPO, "shared", "assets", "art-reference", "A1-03-night-moss-1.png")
WORK = os.path.join(REPO, "orchestration", "runs", "terrarium", "T-TER-049")

# Hand trace, bottom to top, (y, x).
REF_LEFT = [(840, 905), (838, 880), (830, 830), (820, 790), (810, 760), (800, 742), (780, 722), (760, 712), (740, 706),
            (700, 703), (640, 702), (500, 701), (440, 701), (400, 701), (380, 703), (360, 710), (340, 722), (320, 738),
            (300, 752), (285, 758), (272, 757), (266, 746), (255, 743), (243, 746), (241, 758), (200, 758)]
REF_RIGHT = [(840, 920), (838, 960), (830, 1000), (820, 1040), (810, 1065), (800, 1080), (780, 1098), (760, 1112),
             (740, 1118), (700, 1120), (640, 1121), (500, 1126), (440, 1127), (400, 1126), (380, 1123), (360, 1115),
             (340, 1103), (320, 1087), (300, 1074), (285, 1072), (270, 1071), (262, 1082), (250, 1085), (241, 1076),
             (200, 1070)]
REF_CORK_TOP = 194


def ref_polygon():
    pts = [(x, y) for y, x in REF_LEFT]
    pts.append((REF_LEFT[-1][1], REF_CORK_TOP))
    pts.append((REF_RIGHT[-1][1], REF_CORK_TOP))
    pts.extend((x, y) for y, x in reversed(REF_RIGHT))
    return pts


def ref_mask():
    im = Image.new("L", (W, H), 0)
    ImageDraw.Draw(im).polygon(ref_polygon(), fill=255)
    return np.asarray(im) > 0


EYE = np.array([0.0, 0.175, -0.44])
LOOK = np.array([0.0, 0.072, 0.0])
FOV = 28.2
PITCH = math.radians(0.4)


def project(p):
    """p: (n, 3) jar-frame points (x right, y up, z away from eye). Returns (n, 2) pixels, y down."""
    fwd = LOOK - EYE
    fwd = fwd / np.linalg.norm(fwd)
    right = np.cross(np.array([0.0, 1.0, 0.0]), fwd)
    right /= np.linalg.norm(right)
    up = np.cross(fwd, right)
    fwd2 = fwd * math.cos(PITCH) - up * math.sin(PITCH)
    up2 = up * math.cos(PITCH) + fwd * math.sin(PITCH)
    d = p - EYE
    z = d @ fwd2
    x = d @ right
    y = d @ up2
    f = (H / 2.0) / math.tan(math.radians(FOV) / 2.0)
    return np.stack([W / 2.0 + f * x / z, H / 2.0 - f * y / z], axis=1)


def revolve_mask(profiles, n_ang=720):
    m = np.zeros((H, W), bool)
    ang = np.linspace(0, 2 * math.pi, n_ang, endpoint=False)
    for prof in profiles:
        prof = np.asarray(prof, float)
        dense = [prof[0]]
        for a, b in zip(prof[:-1], prof[1:]):
            n = max(1, int(np.hypot(*(b - a)) / 0.0003))
            for k in range(1, n + 1):
                dense.append(a + (b - a) * k / n)
        dense = np.array(dense)
        for r, y in dense:
            pts = np.stack([r * np.cos(ang), np.full_like(ang, y), r * np.sin(ang)], axis=1)
            px = np.rint(project(pts)).astype(int)
            ok = (px[:, 0] >= 0) & (px[:, 0] < W) & (px[:, 1] >= 0) & (px[:, 1] < H)
            px = px[ok]
            m[px[:, 1], px[:, 0]] = True
    out = np.zeros_like(m)
    for yy in range(H):
        xs = np.nonzero(m[yy])[0]
        if len(xs):
            out[yy, xs.min():xs.max() + 1] = True
    return out


def iou(a, b):
    return float((a & b).sum()) / float((a | b).sum())


def extents(mask):
    ys = np.nonzero(mask.any(axis=1))[0]
    xs = np.nonzero(mask.any(axis=0))[0]
    return int(xs.min()), int(xs.max()), int(ys.min()), int(ys.max())


def width_at(mask, y):
    xs = np.nonzero(mask[y])[0]
    return (int(xs.min()), int(xs.max())) if len(xs) else (0, 0)


def measures(mask, label):
    """Width/height ratio, mouth and neck widths from a mask, in pixels."""
    x0, x1, y0, y1 = extents(mask)
    body = max(width_at(mask, y)[1] - width_at(mask, y)[0] for y in range(400, 700, 10))
    out = {"label": label, "cork_top": y0, "bottom": y1, "height_px": y1 - y0, "body_w": body,
           "h_over_w": round((y1 - y0) / body, 4)}
    ws = [(width_at(mask, y)[1] - width_at(mask, y)[0], y) for y in range(y0 + 20, y0 + 130)]
    out["mouth_w"] = max(w for w, _ in ws[:80])
    out["neck_w"] = min(w for w, _ in ws)
    out["mouth_over_body"] = round(out["mouth_w"] / body, 4)
    out["neck_over_body"] = round(out["neck_w"] / body, 4)
    return out


def load_s2_profiles():
    """The A-baseline C jar: terrarium_s2.py outer profile plus the locked cork lathe, read without bpy."""
    import types
    src = open(os.path.join(REPO, "tools", "blender", "terrarium_s2.py"), encoding="utf-8").read()
    src = src.rsplit("\nmain()", 1)[0]
    mods = {name: types.ModuleType(name) for name in ("bpy", "bmesh", "mathutils")}
    mods["mathutils"].Vector = lambda *a: None
    saved = {k: sys.modules.get(k) for k in mods}
    sys.modules.update(mods)
    try:
        ns = {"__name__": "s2src"}
        exec(compile(src, "terrarium_s2.py", "exec"), ns)
    finally:
        for k, v in saved.items():
            if v is None:
                sys.modules.pop(k, None)
            else:
                sys.modules[k] = v
    glass = ns["outer_profile"]()
    cork = [(0.0, 0.1400), (0.0240, 0.1400), (0.0316, 0.1393), (0.0346, 0.1380), (0.0355, 0.1362), (0.0348, 0.1334),
            (0.0334, 0.1298), (0.0322, 0.1264), (0.0308, 0.1246), (0.0308, 0.1168), (0.0316, 0.1148), (0.0316, 0.1100)]
    return glass, cork


def overlay(mask, path):
    """Reference crop with the two masks: white = both, red = reference only, blue = candidate only."""
    ref = Image.open(REF_PNG).convert("RGB")
    ref_m = ref_mask()
    arr = np.asarray(ref).astype(float)
    a = np.zeros((H, W, 3))
    a[ref_m & ~mask] = (255, 40, 40)
    a[mask & ~ref_m] = (40, 120, 255)
    a[mask & ref_m] = (255, 255, 255)
    sel = a.any(axis=2)
    out = arr.copy()
    out[sel] = arr[sel] * 0.45 + a[sel] * 0.55
    Image.fromarray(out.astype(np.uint8)).crop((560, 150, 1270, 880)).save(path)


def load_s6_profiles():
    """The s6 jar and cork profiles from terrarium_s6.py (outer glass, cork), read without bpy."""
    import types
    src = open(os.path.join(REPO, "tools", "blender", "terrarium_s6.py"), encoding="utf-8").read()
    src = src.rsplit("if __name__", 1)[0]
    src = src.replace("import terrarium_s2 as s2  # noqa: E402", "s2 = None").replace("import terrarium_s3 as s3  # noqa: E402", "s3 = None")
    mods = {name: types.ModuleType(name) for name in ("bpy", "bmesh", "mathutils")}
    mods["mathutils"].Vector = lambda *a: None
    mods["mathutils"].noise = None
    saved = {k: sys.modules.get(k) for k in mods}
    sys.modules.update(mods)
    try:
        ns = {"__name__": "s6src", "__file__": os.path.join(REPO, "tools", "blender", "terrarium_s6.py")}
        exec(compile(src, "terrarium_s6.py", "exec"), ns)
    finally:
        for k, v in saved.items():
            if v is None:
                sys.modules.pop(k, None)
            else:
                sys.modules[k] = v
    p = ns["PROFILE"]
    cork, _ = ns["cork_profile"](p)
    return ns["outer_profile"](p), cork


def ring_edges(mask):
    from scipy import ndimage  # noqa: F401
    return mask


def contour_on(frame_path, mask, out_path, box=(560, 150, 1270, 880), color=(255, 40, 200)):
    """The projected silhouette drawn as a one-pixel line on a rendered frame (proves the projection matches the render)."""
    im = np.asarray(Image.open(frame_path).convert("RGB")).copy()
    up = np.zeros_like(mask); up[1:] = mask[:-1]
    dn = np.zeros_like(mask); dn[:-1] = mask[1:]
    lf = np.zeros_like(mask); lf[:, 1:] = mask[:, :-1]
    rt = np.zeros_like(mask); rt[:, :-1] = mask[:, 1:]
    edge = mask & ~(up & dn & lf & rt)
    im[edge] = color
    Image.fromarray(im).crop(box).save(out_path)


def report(out_dir):
    rm = ref_mask()
    cg, cc = load_s2_profiles()
    cm = revolve_mask([cg, cc], n_ang=720)
    sg, sc = load_s6_profiles()
    sm = revolve_mask([sg, sc], n_ang=720)
    numbers = {
        "iou_C_vs_reference": round(iou(cm, rm), 4),
        "iou_s6_vs_reference": round(iou(sm, rm), 4),
        "reference": measures(rm, "reference"),
        "C": measures(cm, "C"),
        "s6": measures(sm, "s6"),
    }
    # Shoulder radius and mouth width in the units of the body width, read off the masks at the shoulder rows.
    json.dump(numbers, open(os.path.join(out_dir, "silhouette.json"), "w"), indent=2)
    overlay(cm, os.path.join(out_dir, "silhouette-overlay-c.png"))
    overlay(sm, os.path.join(out_dir, "silhouette-overlay-s6.png"))
    contour_on(os.path.join(out_dir, "c-jar-g1.png"), cm, os.path.join(out_dir, "silhouette-on-render-c.png"))
    contour_on(os.path.join(out_dir, "s6-jar-g1.png"), sm, os.path.join(out_dir, "silhouette-on-render-s6.png"))
    print(json.dumps(numbers, indent=2))


if __name__ == "__main__":
    mode = sys.argv[1] if len(sys.argv) > 1 else "iou"
    if mode == "check":
        pl = json.load(open(os.path.join(REPO, "orchestration", "runs", "terrarium", "T-TER-045", "planes.json")))["cameras"]["JarG1"]
        for key, p in (("foot_left", (-0.045, 0, 0)), ("foot_right", (0.045, 0, 0)), ("foot_front", (0, 0, -0.045)), ("foot_back", (0, 0, 0.045))):
            print(key, project(np.array([p]))[0].round(2), pl[key])
    elif mode == "report":
        report(WORK)
    else:
        glass, cork = load_s2_profiles()
        cm = revolve_mask([glass, cork])
        rm = ref_mask()
        print("IoU C vs ref", round(iou(cm, rm), 4))
        print(measures(rm, "reference"))
        print(measures(cm, "C"))
        overlay(cm, os.path.join(WORK, "work", "sil_c_check.png"))
