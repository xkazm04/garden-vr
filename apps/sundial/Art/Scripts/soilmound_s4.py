# Spike S4 (T-SUN-046): soil as a raised mound with a top-down painting. Texture, heightfield and metric tooling.
#   python apps/sundial/Art/Scripts/soilmound_s4.py template     # the structure-lock template (ragged disc on paper)
#   python apps/sundial/Art/Scripts/soilmound_s4.py build        # fit and grade the generated painting, write the heightfield
#   python apps/sundial/Art/Scripts/soilmound_s4.py score a.png b.png --out score.json
#   python apps/sundial/Art/Scripts/soilmound_s4.py flip a.png b.png out.gif
#   python apps/sundial/Art/Scripts/soilmound_s4.py grey a.png b.png out.png
# The painting comes from tools/agy/image.sh (a structure-locked edit of the template). Everything else is code.
import json
import math
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, "..", "..", "..", ".."))
SRC = os.path.normpath(os.path.join(HERE, "..", "Source", "soil"))
RES = os.path.normpath(os.path.join(HERE, "..", "..", "Assets", "Resources", "SoilMound"))
GEN = os.path.join(SRC, "gen-1.png")
REF = os.path.join(REPO, "shared", "assets", "art-reference", "A2-05-field-notebook-1.png")
REGIONS = os.path.join(REPO, "tools", "fidelity", "regions", "DialG1.json")

# The texture covers +-SPAN metres of the dial, planar and top-down. The mound is about MOUND_R metres with a ragged edge.
SPAN = 0.085
MOUND_R = 0.074
SIZE = 2048
TEXN = 1024
GRID = 56
PEAK = 0.0085          # crown height above the paper (m)
PAPER_Y = 0.0003       # the rim sits a hair above the face, like the old mound
SAT = 0.38             # painting chroma kept (the paint is orange, the reference earth is grey-umber)
NEAR_GAIN = 1.10       # luma gain on the near edge, where the reference earth is darkest
FAR_GAIN = 1.55        # luma gain toward the gnomon and the far side, where the reference is pale dry sand
HP_BOOST = 0.8          # extra high-pass of the painting luma
FEATHER_NEAR = 0.0012  # metres
FEATHER_FAR = 0.0075
PAPER = np.array([244, 239, 230], np.float32)


def luma(rgb):
    return rgb @ np.array([0.2126, 0.7152, 0.0722], np.float32)


def ragged_radius(theta, seed=46):
    rng = np.random.default_rng(seed)
    r = np.full_like(theta, MOUND_R)
    for k, amp in ((2, 0.0045), (3, 0.0040), (5, 0.0030), (8, 0.0022), (13, 0.0016), (21, 0.0010), (34, 0.0006)):
        r = r + amp * np.sin(k * theta + rng.uniform(0, 2 * math.pi))
    return r


def template():
    ax = (np.arange(SIZE) + 0.5) / SIZE * 2.0 * SPAN - SPAN
    x, y = np.meshgrid(ax, -ax)
    inside = np.hypot(x, y) < ragged_radius(np.arctan2(y, x))
    img = np.empty((SIZE, SIZE, 3), np.float32)
    img[:] = PAPER
    img[inside] = np.array([112, 84, 62], np.float32)
    out = os.path.join(SRC, "template.png")
    os.makedirs(SRC, exist_ok=True)
    Image.fromarray(img.astype(np.uint8)).save(out)
    print("wrote", out)


def fbm(n, seed, octaves=((4, 1.0), (9, 0.55), (21, 0.3))):
    """Smooth value noise on an n x n grid, 0..1."""
    import cv2
    rng = np.random.default_rng(seed)
    out = np.zeros((n, n), np.float32)
    for cells, amp in octaves:
        g = rng.random((cells + 2, cells + 2)).astype(np.float32)
        out += amp * cv2.resize(g, (n, n), interpolation=cv2.INTER_CUBIC)
    return (out - out.min()) / max(out.max() - out.min(), 1e-6)


def fit_mask(rgb):
    """The painted bed against the cream paper. Largest component, holes filled. uint8 0/255."""
    import cv2
    d = np.abs(rgb.astype(np.float32) - PAPER).sum(axis=2)
    m = (d > 60).astype(np.uint8) * 255
    m = cv2.morphologyEx(m, cv2.MORPH_CLOSE, np.ones((9, 9), np.uint8))
    _, lab, st, _ = cv2.connectedComponentsWithStats(m)
    best = 1 + int(np.argmax(st[1:, cv2.CC_STAT_AREA]))
    m = ((lab == best) * 255).astype(np.uint8)
    cnts, _ = cv2.findContours(m, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
    filled = np.zeros_like(m)
    cv2.drawContours(filled, cnts, -1, 255, -1)
    return filled


def build():
    import cv2
    from scipy import ndimage
    rgb = np.asarray(Image.open(GEN).convert("RGB"))
    if rgb.shape[0] != TEXN:
        rgb = np.asarray(Image.fromarray(rgb).resize((TEXN, TEXN), Image.LANCZOS))
    mask = fit_mask(rgb)
    ys, xs = np.nonzero(mask)
    cx, cy = xs.mean(), ys.mean()
    r_eq = math.sqrt(float((mask > 0).sum()) / math.pi)
    k = MOUND_R / (2.0 * SPAN) * TEXN / r_eq
    mat = np.array([[k, 0, TEXN / 2 - cx * k], [0, k, TEXN / 2 - cy * k]], np.float32)
    rgb = cv2.warpAffine(rgb, mat, (TEXN, TEXN), flags=cv2.INTER_CUBIC, borderMode=cv2.BORDER_CONSTANT,
                         borderValue=tuple(int(v) for v in PAPER))
    mask = (cv2.warpAffine(mask, mat, (TEXN, TEXN), flags=cv2.INTER_LINEAR) > 127).astype(np.uint8) * 255
    mpp = 2.0 * SPAN / TEXN
    inside = mask > 0
    sdf = (cv2.distanceTransform(mask, cv2.DIST_L2, 5) - cv2.distanceTransform(255 - mask, cv2.DIST_L2, 5)) * mpp

    # Pull the painted edge pencil and the paper halo out, then extend the earth outward under the feather.
    core = cv2.erode(mask, np.ones((3, 3), np.uint8), iterations=7) > 0
    idx = ndimage.distance_transform_edt(~core, return_distances=False, return_indices=True)
    col = rgb.astype(np.float32)[idx[0], idx[1]]

    ax = (np.arange(TEXN) + 0.5) / TEXN * 2.0 * SPAN - SPAN
    gx, gy = np.meshgrid(ax, -ax)
    th = np.arctan2(gy, gx)
    # Chroma down, then a luma gain field: dark near the viewer (-y), pale far and at the centre.
    lum = luma(col)[..., None]
    col = lum + (col - lum) * SAT
    t = np.clip(0.5 + 0.5 * (gy / MOUND_R) + 0.35 * (fbm(TEXN, 7) - 0.5), 0, 1)
    t = t * t * (3 - 2 * t)
    centre = np.exp(-((np.hypot(gx, gy) / (0.55 * MOUND_R)) ** 2))
    gain = NEAR_GAIN + (FAR_GAIN - NEAR_GAIN) * np.clip(t + 0.25 * centre, 0, 1)
    col = col * gain[..., None]
    # Fine texture energy up (the 1024 paint loses some to mips at the dial), and a soft shoulder so no crumb blows out.
    lum2 = luma(col)[..., None]
    blur = cv2.GaussianBlur(lum2[..., 0], (0, 0), 3.0)[..., None]
    col = col + (lum2 - blur) * HP_BOOST
    col = 255.0 * (1.0 - np.exp(-np.clip(col, 0, None) / 255.0 * 1.35)) / (1.0 - math.exp(-1.35))
    col = np.clip(col, 0, 255)

    # Alpha. A soft far edge into the wash, a crisp near edge, both broken up by crumb noise.
    feather = FEATHER_NEAR + (FEATHER_FAR - FEATHER_NEAR) * (0.5 + 0.5 * np.sin(th)) ** 1.5
    crumb = fbm(TEXN, 11, ((40, 1.0), (90, 0.7), (180, 0.5)))
    base = np.clip(0.5 + sdf / feather, 0.0, 1.0)
    alpha = np.clip(base * 1.25 - (1.0 - crumb) * 0.45 * (1.0 - np.abs(base * 2 - 1) ** 3), 0, 1)
    alpha = np.where(sdf > 0.5 * feather, 1.0, alpha)
    alpha = np.where(sdf < -1.0 * feather, 0.0, alpha)

    rgba = np.dstack([col, alpha * 255.0]).clip(0, 255).astype(np.uint8)
    os.makedirs(RES, exist_ok=True)
    Image.fromarray(rgba, "RGBA").save(os.path.join(RES, "soil_mound.png"))
    with open(os.path.join(RES, "soil_mound.png.provenance.txt"), "w", newline="\n") as f:
        f.write("asset: soil_mound.png\n"
                "method: apps/sundial/Art/Source/soil/gen-1.png (tools/agy/image.sh, structure-locked edit of template.png), "
                "fitted, regraded and feathered by apps/sundial/Art/Scripts/soilmound_s4.py build\n"
                "derived_from_art_reference: no\n")

    # Heightfield: a crown, a lip that rises from the paper over the first 12 mm inside the painted edge, low lumps.
    n = GRID
    h_ax = np.linspace(-SPAN, SPAN, n + 1)
    hx, hy = np.meshgrid(h_ax, h_ax)  # hy is dial +z, away from the viewer
    px = ((hx + SPAN) / (2 * SPAN) * (TEXN - 1)).round().astype(int)
    py = ((SPAN - hy) / (2 * SPAN) * (TEXN - 1)).round().astype(int)
    s_h = sdf[py, px]
    rr = np.minimum(np.hypot(hx, hy) / MOUND_R, 1.0)
    crown = 1.0 - rr ** 2.15
    lumps = (fbm(n + 1, 23, ((3, 1.0), (7, 0.5))) - 0.5) * 0.0028
    lip = np.clip((s_h + 0.002) / 0.012, 0, 1)
    lip = lip * lip * (3 - 2 * lip)
    h = np.maximum(PAPER_Y + (PEAK - PAPER_Y) * (0.22 + 0.78 * crown) * lip + lumps * lip, PAPER_Y)
    seen = alpha[py, px] > 0.0
    seen = ndimage.binary_dilation(seen, iterations=2)
    h = np.where(seen, h, -1.0)

    # Pebbles. A row along the near rim as in the reference, a few scattered. Seeded.
    rng = np.random.default_rng(46)
    peb = []

    def place(theta, rad, size):
        x, z = rad * math.cos(theta), rad * math.sin(theta)
        ix = int(round((x + SPAN) / (2 * SPAN) * (TEXN - 1)))
        iy = int(round((SPAN - z) / (2 * SPAN) * (TEXN - 1)))
        if sdf[iy, ix] < size * 1.6:
            return
        rr_ = min(math.hypot(x, z) / MOUND_R, 1.0)
        y0 = PAPER_Y + (PEAK - PAPER_Y) * (0.22 + 0.78 * (1 - rr_ ** 2.15))
        tone = rng.uniform(0.0, 1.0)
        peb.append({
            "x": round(x, 5), "z": round(z, 5), "y": round(y0, 5), "r": round(size, 5),
            "sy": round(rng.uniform(0.45, 0.7), 3), "rot": round(rng.uniform(0, 360), 1),
            "c": [round(0.70 + 0.12 * tone, 3), round(0.65 + 0.11 * tone, 3), round(0.56 + 0.10 * tone, 3)],
        })

    for i in range(14):
        place(math.radians(205 + i * 10.5 + rng.uniform(-3, 3)), MOUND_R * rng.uniform(0.80, 0.92), rng.uniform(0.0016, 0.0030))
    for i in range(10):
        place(rng.uniform(0, 2 * math.pi), MOUND_R * math.sqrt(rng.uniform(0.15, 0.85)), rng.uniform(0.0014, 0.0026))
    out = {
        "span": SPAN, "grid": n, "moundRadius": MOUND_R, "peak": PEAK,
        "heights": [round(float(v), 5) for v in h.reshape(-1)],
        "pebbles": peb,
    }
    with open(os.path.join(RES, "soil-mound.json"), "w", newline="\n") as f:
        json.dump(out, f, separators=(",", ":"))
    prev = col * alpha[..., None] + PAPER * (1 - alpha[..., None])
    Image.fromarray(prev.clip(0, 255).astype(np.uint8)).save(os.path.join(SRC, "preview.png"))
    print(json.dumps({
        "equivalentRadiusM": round(math.sqrt(float(inside.sum()) / math.pi) * mpp, 5),
        "pebbles": len(peb),
        "meshVerticesKept": int((h >= 0).sum()),
        "heightMaxMm": round(float(h.max() * 1000), 2),
        "textureLumaMean": round(float(luma(col)[inside].mean()), 1),
    }))


# ---- metrics -------------------------------------------------------------------------------------------------------

def soil_mask():
    with open(REGIONS, "r", encoding="utf-8") as f:
        d = json.load(f)
    poly = [r for r in d["regions"] if r["name"] == "soil"][0]["polygon"]
    m = Image.new("L", (1824, 1024), 0)
    ImageDraw.Draw(m).polygon([tuple(p) for p in poly], fill=255)
    return np.asarray(m) > 0


def hp_std(rgb, mask):
    l8 = Image.fromarray(luma(rgb).clip(0, 255).astype("uint8"))
    blur = np.asarray(l8.filter(ImageFilter.GaussianBlur(4))).astype(np.float32)
    return float((luma(rgb) - blur)[mask].std())


def region_stats(rgb, mask):
    l = luma(rgb)[mask]
    px = rgb[mask]
    return {
        "luma": round(float(l.mean()), 2), "std": round(float(l.std()), 2),
        "rOverB": round(float(px[:, 0].mean() / max(px[:, 2].mean(), 1e-3)), 3),
        "hp": round(hp_std(rgb, mask), 2), "n": int(mask.sum()),
    }


def load(path):
    return np.asarray(Image.open(path).convert("RGB")).astype(np.float32)


def near_band(poly_mask):
    """The dark near edge of the earth: soil polygon rows from 72% down its extent, away from the plants and the gnomon."""
    ys = np.nonzero(poly_mask.any(axis=1))[0]
    y0 = ys.min() + 0.72 * (ys.max() - ys.min())
    band = poly_mask.copy()
    band[: int(y0)] = False
    return band


def boundary_roughness(rgb_b, rgb_a):
    """Std of the radius (px) of the earth boundary around its centroid, from where B differs from the face
    behind it, compared with A. Computed on a 'is earth' colour class: R > B + 18 and luma < 190 inside the soil polygon dilated."""
    pm = soil_mask()
    out = {}
    for name, im in (("A", rgb_a), ("B", rgb_b)):
        r, g, b = im[..., 0], im[..., 1], im[..., 2]
        earth = (luma(im) < 150) & (r > b + 8)
        grown = np.asarray(Image.fromarray((pm * 255).astype("uint8")).filter(ImageFilter.MaxFilter(41))) > 0
        earth &= grown
        ys, xs = np.nonzero(earth)
        if len(xs) < 100:
            out[name] = None
            continue
        cx, cy = xs.mean(), ys.mean()
        # Outer boundary per angle, in a frame squashed by the view tilt (y stretched by 1/0.62 so a circle is round).
        sq = 1.0 / 0.62
        ang = np.arctan2((ys - cy) * sq, xs - cx)
        rad = np.hypot((ys - cy) * sq, xs - cx)
        bins = np.linspace(-math.pi, math.pi, 73)
        which = np.digitize(ang, bins) - 1
        rmax = np.array([np.percentile(rad[which == i], 98) if (which == i).sum() > 5 else np.nan for i in range(72)])
        rmax = rmax[~np.isnan(rmax)]
        out[name] = {"radiusMeanPx": round(float(rmax.mean()), 1), "radiusStdPx": round(float(rmax.std()), 2),
                     "stdOverMeanPct": round(float(100 * rmax.std() / rmax.mean()), 2), "bins": int(len(rmax))}
    return out


def score(a_path, b_path, out_path):
    ref, a, b = load(REF), load(a_path), load(b_path)
    pm = soil_mask()
    nb = near_band(pm)
    res = {"frames": {"ref": REF, "A": a_path, "B": b_path}, "gate": {"luma": [83, 103], "rOverB": 1.7, "hp": 20}}
    for tag, mask in (("soilPolygon", pm), ("nearBand", nb)):
        res[tag] = {"ref": region_stats(ref, mask), "A": region_stats(a, mask), "B": region_stats(b, mask)}
    # The dossier's soil row was a dark patch. Find the ref window nearest (93, 43) inside the polygon, report all three on it.
    best = None
    for y in range(380, 700, 10):
        for x in range(520, 1050, 10):
            w = np.zeros(pm.shape, bool)
            w[y:y + 30, x:x + 60] = True
            if not pm[y:y + 30, x:x + 60].all():
                continue
            l = luma(ref)[w]
            err = abs(l.mean() - 93) + abs(l.std() - 43)
            if best is None or err < best[0]:
                best = (err, x, y)
    _, bx, by = best
    w = np.zeros(pm.shape, bool)
    w[by:by + 30, bx:bx + 60] = True
    res["dossierPatch"] = {"x": bx, "y": by, "w": 60, "h": 30, "ref": region_stats(ref, w), "A": region_stats(a, w), "B": region_stats(b, w)}
    # The set version, so one lucky window is not the gate: every 60x30 window fully in the soil polygon where the reference
    # is dark stippled earth (mean luma 75 to 110, std over 30), same pixels in A and B.
    wins = []
    lref = luma(ref)
    for y in range(380, 700, 30):
        for x in range(520, 1050, 60):
            if not pm[y:y + 30, x:x + 60].all():
                continue
            seg = lref[y:y + 30, x:x + 60]
            if 75 <= seg.mean() <= 110 and seg.std() > 30:
                wins.append((x, y))
    wm = np.zeros(pm.shape, bool)
    for x, y in wins:
        wm[y:y + 30, x:x + 60] = True
    if wins:
        res["darkEarthWindows"] = {"windows": len(wins), "ref": region_stats(ref, wm), "A": region_stats(a, wm), "B": region_stats(b, wm)}
    res["boundary"] = boundary_roughness(b, a)
    for tag in ("dossierPatch", "darkEarthWindows"):
        if tag not in res:
            continue
        g = res[tag]["B"]
        res[tag]["verdict"] = {
            "luma93pm10": abs(g["luma"] - 93) <= 10,
            "rOverBAtMost1.7": g["rOverB"] <= 1.7,
            "hpAtLeast20": g["hp"] >= 20,
        }
    with open(out_path, "w", encoding="utf-8", newline="\n") as f:
        json.dump(res, f, indent=2)
        f.write("\n")
    print(json.dumps(res, indent=2))


def frames(a_path, b_path, grey=False):
    a = Image.open(a_path).convert("RGB")
    b = Image.open(b_path).convert("RGB")
    if grey:
        a, b = a.convert("L").convert("RGB"), b.convert("L").convert("RGB")
    return a, b


def flip(a_path, b_path, out_path):
    a, b = frames(a_path, b_path)
    a.save(out_path, save_all=True, append_images=[b], duration=900, loop=0)
    print("wrote", out_path)


def grey(a_path, b_path, out_path):
    ref = Image.open(REF).convert("L").convert("RGB").resize((912, 512))
    a, b = frames(a_path, b_path, True)
    a, b = a.resize((912, 512)), b.resize((912, 512))
    sheet = Image.new("RGB", (912 * 3, 512), (255, 255, 255))
    for i, im in enumerate((ref, a, b)):
        sheet.paste(im, (912 * i, 0))
    sheet.save(out_path)
    print("wrote", out_path)


def main(argv):
    cmd = argv[0] if argv else ""
    if cmd == "template":
        template()
    elif cmd == "build":
        build()
    elif cmd == "score":
        out = argv[argv.index("--out") + 1] if "--out" in argv else "score-soil.json"
        score(argv[1], argv[2], out)
    elif cmd == "flip":
        flip(argv[1], argv[2], argv[3])
    elif cmd == "grey":
        grey(argv[1], argv[2], argv[3])
    else:
        raise SystemExit(__doc__)


if __name__ == "__main__":
    main(sys.argv[1:])
