# T-SUN-051 (S4b): the soil bed as a wide crescent along the near rim, with a wash-bleed edge. Supersedes the S4 build
# (soilmound_s4.py build) for Resources/SoilMound; the T-SUN-046 frames in orchestration/runs stay as its evidence.
#   python apps/sundial/Art/Scripts/soilmound_s4b.py template     # the zoned structure-lock template (we draw the shape)
#   python apps/sundial/Art/Scripts/soilmound_s4b.py build        # outline diff, grade, own edge, heightfield, pebbles
#   python apps/sundial/Art/Scripts/soilmound_s4b.py score a.png b.png [c.png] --out score.json
# The painting comes from tools/agy/image.sh (an edit of the template). The model fills the interior only: the bed's edge
# (shape, feather, wash bleed) is drawn here and the generated outline is diffed against the template, never trusted.
# The art reference is measurement-only and is never an input to the image model.
import json
import math
import os
import sys

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import soilmound_s4 as s4  # noqa: E402  (metrics helpers: score windows, region stats)

SRC = s4.SRC
RES = s4.RES
GEN = os.path.join(SRC, "gen-2.png")
TEMPLATE = os.path.join(SRC, "template-2.png")

# Dial-plane domain of the painting, metres. Square for the image model, cropped to ZMAX for the texture.
SPAN = 0.125
TEXN = 1024
CROP_ROWS = 288                       # rows cut from the top of the square (z above ZMAX)
ZMAX = SPAN - CROP_ROWS / TEXN * 2 * SPAN
ZMIN = -SPAN
ROUT = 0.106                          # the near rim of the bed, inside the wash disc (0.1076 R-wash) and the tile rows
CREST = 0.040                         # z of the far edge at x = 0 (the midday plant stands at z 0.0265)
CREST_DROP = 0.032                    # the far edge falls by this at x = +-ROUT
GRID_X = 63                           # 4 mm cells, so the triangle budget stays under S4's 6.1k
GRID_Z = 45
PEAK = 0.0052                         # crown above the paper (m), S4 was 8.35 mm
PAPER_Y = 0.0003
LIP = 0.010                           # the bed rises from the paper over this distance inside the edge
FEATHER_NEAR = 0.0030                 # metres, 10 to 90 percent of the edge ramp
FEATHER_FAR = 0.0120
SAT = 0.40
PLANTS = ((-0.0442, -0.0118), (0.0236, 0.0265), (0.0383, -0.0236))   # look A card spots, metres
PAPER = s4.PAPER
DARK = np.array([92, 68, 50], np.float32)
PALE = np.array([205, 189, 157], np.float32)


def grid_xy(n=TEXN):
    ax = (np.arange(n) + 0.5) / n * 2.0 * SPAN - SPAN
    return np.meshgrid(ax, -ax)


def smooth(a, b, x):
    t = np.clip((x - a) / (b - a), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def edge_fields(x, z, wobble=True):
    """d: signed distance-ish (m, positive inside the bed). wf: how much the far edge binds (0 rim, 1 far edge)."""
    c = ROUT - np.hypot(x, z)
    zfar = CREST - CREST_DROP * (x / ROUT) ** 2
    g = zfar - z
    k = 0.012
    h = np.clip(0.5 + 0.5 * (g - c) / k, 0.0, 1.0)
    d = (1 - h) * g + h * c - k * h * (1 - h)           # polynomial smooth min
    wf = smooth(-0.02, 0.02, c - g)
    if wobble:
        d = d + wobble_at(x, z)
    return d, wf, c


_WOB = {}


def wobble_at(x, z):
    """Low-frequency wobble of the edge (an earth bed, not a torn edge): 3 to 6 cm and 1 cm wavelengths, a function of the dial
    position so the template, the alpha and the heightfield all see the same edge."""
    from scipy import ndimage
    if not _WOB:
        _WOB["w"] = (0.0024 * (s4.fbm(1024, 5, ((5, 1.0), (9, 0.6))) - 0.5) * 2.0
                     + 0.0009 * (s4.fbm(1024, 9, ((17, 1.0), (33, 0.5))) - 0.5) * 2.0)
    col = (x + SPAN) / (2 * SPAN) * 1024 - 0.5
    row = (SPAN - z) / (2 * SPAN) * 1024 - 0.5
    return ndimage.map_coordinates(_WOB["w"], [row.ravel(), col.ravel()], order=1, mode="nearest").reshape(x.shape)


def pocket_weight(x, z):
    w = np.zeros_like(x)
    for px, pz in PLANTS:
        w = np.maximum(w, np.exp(-(((x - px) ** 2 + (z - pz) ** 2) / (0.024 ** 2))))
    return w


def zone_pale(x, z):
    """0 dark earth, 1 pale dry sand. The reference: dark earth along the near rim, the sides and the thin far edge, a pale dry
    zone in the middle, and dark soil round the plant bases and the nib hole (where its dark-earth gate windows sit)."""
    c = ROUT - np.hypot(x, z)
    zfar = CREST - CREST_DROP * (x / ROUT) ** 2
    g = zfar - z
    edge_dark = 1.0 - smooth(0.012, 0.050, c)
    far_dark = 0.75 * (1.0 - smooth(0.002, 0.018, g))
    centre = np.exp(-((((x - 0.012) / 0.064) ** 2 + ((z + 0.002) / 0.045) ** 2) ** 1.3))
    pale = centre * (1.0 - edge_dark) * (1.0 - far_dark)
    for px, pz in PLANTS:
        pale = pale * (1.0 - 0.90 * np.exp(-(((x - px) ** 2 + (z - pz) ** 2) / (0.024 ** 2))))
    pale = pale * (1.0 - 0.85 * np.exp(-((x ** 2 + z ** 2) / (0.013 ** 2))))
    return np.clip(pale, 0.0, 1.0)


def template():
    x, z = grid_xy(2048)
    d, _, _ = edge_fields(x, z, wobble=True)
    inside = d > 0
    pale = zone_pale(x, z)
    tone = DARK[None, None, :] * (1 - pale[..., None]) + PALE[None, None, :] * pale[..., None]
    img = np.empty((2048, 2048, 3), np.float32)
    img[:] = PAPER
    img[inside] = tone[inside]
    os.makedirs(SRC, exist_ok=True)
    Image.fromarray(img.astype(np.uint8)).save(TEMPLATE)
    print("wrote", TEMPLATE)


def outline_diff(rgb):
    """Fit the generated bed against the paper and compare it with the template mask pixel for pixel, no re-fit."""
    import cv2
    t = np.asarray(Image.open(TEMPLATE).convert("RGB").resize((TEXN, TEXN), Image.LANCZOS))
    tm = s4.fit_mask(t) > 0
    gm = s4.fit_mask(rgb) > 0
    inter = float((tm & gm).sum())
    union = float((tm | gm).sum())
    # Boundary offset: distance of each generated boundary pixel from the template boundary, in mm.
    mpp = 2.0 * SPAN / TEXN * 1000.0
    tb = cv2.distanceTransform((~tm).astype(np.uint8), cv2.DIST_L2, 5) + cv2.distanceTransform(tm.astype(np.uint8), cv2.DIST_L2, 5)
    gb = gm & ~cv2.erode(gm.astype(np.uint8), np.ones((3, 3), np.uint8)).astype(bool)
    off = tb[gb] * mpp
    ys, xs = np.nonzero(gm)
    ty, tx = np.nonzero(tm)
    return {
        "iou": round(inter / union, 4),
        "templateAreaPx": int(tm.sum()), "generatedAreaPx": int(gm.sum()),
        "centroidShiftMm": round(float(math.hypot(xs.mean() - tx.mean(), ys.mean() - ty.mean()) * mpp), 2),
        "boundaryOffsetMeanMm": round(float(off.mean()), 2), "boundaryOffsetP95Mm": round(float(np.percentile(off, 95)), 2),
        "boundaryOffsetMaxMm": round(float(off.max()), 2),
        "px": round(mpp, 4),
    }


def build():
    import cv2
    from scipy import ndimage
    rgb = np.asarray(Image.open(GEN).convert("RGB"))
    if rgb.shape[0] != TEXN or rgb.shape[1] != TEXN:
        rgb = np.asarray(Image.fromarray(rgb).resize((TEXN, TEXN), Image.LANCZOS))
    diff = outline_diff(rgb)
    print("outline diff", json.dumps(diff))

    x, z = grid_xy()
    d, wf, c = edge_fields(x, z)
    mask = (d > 0).astype(np.uint8) * 255

    # Interior colour only: erode our own mask past the model's edge pencil and halo, then push the earth outward under the
    # feather. The model's outline is never read.
    core = cv2.erode(mask, np.ones((3, 3), np.uint8), iterations=9) > 0
    idx = ndimage.distance_transform_edt(~core, return_distances=False, return_indices=True)
    col = rgb.astype(np.float32)[idx[0], idx[1]]
    # The nearest-pixel extension smears the rim into radial streaks; blur it where it is extension, ramping in with distance.
    ext = ndimage.distance_transform_edt(~core).astype(np.float32)
    soft = cv2.GaussianBlur(col, (0, 0), 4.0)
    wext = np.clip(ext / 5.0, 0.0, 1.0)[..., None]
    col = col * (1.0 - wext) + soft * wext
    lum = s4.luma(col)[..., None]
    col = lum + (col - lum) * SAT
    pale = zone_pale(x, z)
    gain = GAIN_DARK + (GAIN_PALE - GAIN_DARK) * pale ** PALE_EXP
    gain = gain * (1.0 + POCKET_LIFT * pocket_weight(x, z))      # the generated pockets read as craters; lift them toward earth
    col = col * gain[..., None]
    lum2 = s4.luma(col)[..., None]
    blur = cv2.GaussianBlur(lum2[..., 0], (0, 0), 3.0)[..., None]
    col = col + (lum2 - blur) * HP_BOOST
    col = 255.0 * (1.0 - np.exp(-np.clip(col, 0, None) / 255.0 * 1.35)) / (1.0 - math.exp(-1.35))
    col = np.clip(col, 0, 255)
    # Fine grain the generation leaves out in its smooth transitions: a 1 texel multiplicative speckle, seeded, stronger in the dark.
    rng = np.random.default_rng(51)
    grain = cv2.GaussianBlur(rng.standard_normal((TEXN, TEXN)).astype(np.float32), (0, 0), 0.7)
    grain = grain / max(float(grain.std()), 1e-6)
    col = np.clip(col * (1.0 + GRAIN * (1.0 - 0.5 * pale) * grain)[..., None], 0, 255)

    # Edge parameter e (alpha channel): 0 is the wash, 1 is earth. The shader mixes the face's own wash colour in by 1 - e, so
    # the wash enters the soil edge. No dither, no crumb noise: the ramp is smooth and 3 mm (rim) to 12 mm (far edge) wide.
    width = FEATHER_NEAR + (FEATHER_FAR - FEATHER_NEAR) * wf
    e = smooth(0.0, 1.0, np.clip(0.5 + d / width, 0.0, 1.0))
    e = np.where(d < -0.5 * width, 0.0, e)

    rgba = np.dstack([col, e * 255.0]).clip(0, 255).astype(np.uint8)
    rgba = rgba[CROP_ROWS:]
    os.makedirs(RES, exist_ok=True)
    Image.fromarray(rgba, "RGBA").save(os.path.join(RES, "soil_mound.png"))
    with open(os.path.join(RES, "soil_mound.png.provenance.txt"), "w", newline="\n") as f:
        f.write("asset: soil_mound.png\n"
                "method: apps/sundial/Art/Source/soil/gen-2.png (tools/agy/image.sh, edit of the zoned crescent template-2.png; the model "
                "fills the interior only), regraded; the bed edge (shape, feather) is drawn by apps/sundial/Art/Scripts/soilmound_s4b.py build "
                "and the wash enters it in the shader from the face texture. Alpha is the edge parameter (0 wash, 1 earth), not coverage.\n"
                "derived_from_art_reference: no\n")

    # Heightfield: a low bed, not a heap. Rises from the paper over LIP inside the edge, a shallow dome over the interior.
    hx = np.linspace(-SPAN, SPAN, GRID_X + 1)
    hz = np.linspace(ZMIN, ZMAX, GRID_Z + 1)
    HX, HZ = np.meshgrid(hx, hz)
    hd, _, hc = edge_fields(HX, HZ)
    lip = smooth(0.0, LIP, hd)
    dome = smooth(0.0, 0.075, hd) ** 0.8
    lumps = (s4.fbm(GRID_X + 1, 23, ((3, 1.0), (7, 0.5)))[:GRID_Z + 1, :] - 0.5) * 0.0020
    h = np.maximum(PAPER_Y + (PEAK - PAPER_Y) * (0.35 + 0.65 * dome) * lip + lumps * lip, PAPER_Y)
    seen = hd > -0.5 * FEATHER_FAR - 0.002
    seen = ndimage.binary_dilation(seen, iterations=1)
    h = np.where(seen, h, -1.0)

    # Pebbles: a row along the near rim as in the reference, a few scattered. Seeded.
    rng = np.random.default_rng(51)
    peb = []

    def place(theta, rad, size):
        px, pz = rad * math.cos(theta), rad * math.sin(theta)
        dd, _, _ = edge_fields(np.full((16, 16), px), np.full((16, 16), pz), wobble=False)
        if dd[0, 0] < size * 1.6 + 0.004:
            return
        j = int(round((pz - ZMIN) / (ZMAX - ZMIN) * GRID_Z))
        i = int(round((px + SPAN) / (2 * SPAN) * GRID_X))
        y0 = max(float(h[min(max(j, 0), GRID_Z), min(max(i, 0), GRID_X)]), PAPER_Y)
        tone = rng.uniform(0.0, 1.0)
        peb.append({
            "x": round(px, 5), "z": round(pz, 5), "y": round(y0, 5), "r": round(size, 5),
            "sy": round(rng.uniform(0.45, 0.7), 3), "rot": round(rng.uniform(0, 360), 1),
            "c": [round(0.70 + 0.12 * tone, 3), round(0.65 + 0.11 * tone, 3), round(0.56 + 0.10 * tone, 3)],
        })

    for i in range(12):
        place(math.radians(200 + i * 11.6 + rng.uniform(-3, 3)), ROUT * rng.uniform(0.84, 0.92), rng.uniform(0.0016, 0.0030))
    for i in range(8):
        a = rng.uniform(math.radians(185), math.radians(355))
        place(a, ROUT * math.sqrt(rng.uniform(0.2, 0.8)), rng.uniform(0.0014, 0.0026))
    out = {
        "span": SPAN, "grid": GRID_X, "gridZ": GRID_Z, "zMin": ZMIN, "zMax": ZMAX,
        "moundRadius": ROUT, "peak": PEAK,
        "bedX": ROUT, "bedZ0": -ROUT, "bedZ1": CREST,
        "heights": [round(float(v), 5) for v in h.reshape(-1)],
        "pebbles": peb,
    }
    with open(os.path.join(RES, "soil-mound.json"), "w", newline="\n") as f:
        json.dump(out, f, separators=(",", ":"))
    prev = col * e[..., None] + PAPER * (1 - e[..., None])
    Image.fromarray(prev.clip(0, 255).astype(np.uint8)).save(os.path.join(SRC, "preview-2.png"))
    info = {
        "outlineDiff": diff, "textureSize": [int(rgba.shape[1]), int(rgba.shape[0])],
        "zRangeM": [round(ZMIN, 5), round(ZMAX, 5)], "pebbles": len(peb), "meshCellsKept": int((h >= 0).sum()),
        "heightMaxMm": round(float(h.max() * 1000), 2), "bedAreaCm2": round(float((d > 0).sum()) * (2 * SPAN / TEXN) ** 2 * 1e4, 1),
        "textureLumaMean": round(float(s4.luma(col)[d > 0].mean()), 1),
        "gains": {"dark": GAIN_DARK, "pale": GAIN_PALE, "hpBoost": HP_BOOST, "sat": SAT},
    }
    with open(os.path.join(SRC, "build-2.json"), "w", newline="\n") as f:
        json.dump(info, f, indent=2)
    print(json.dumps(info, indent=2))


GAIN_DARK = float(os.environ.get("S4B_GAIN_DARK", "0.62"))
GAIN_PALE = float(os.environ.get("S4B_GAIN_PALE", "1.30"))
GRAIN = float(os.environ.get("S4B_GRAIN", "0.14"))
PALE_EXP = float(os.environ.get("S4B_EXP", "2.0"))
POCKET_LIFT = float(os.environ.get("S4B_POCKET", "0.2"))
HP_BOOST = float(os.environ.get("S4B_HP", "2.0"))


def main(argv):
    cmd = argv[0] if argv else ""
    if cmd == "template":
        template()
    elif cmd == "build":
        build()
    elif cmd == "score":
        out = argv[argv.index("--out") + 1] if "--out" in argv else "score-soil.json"
        s4.score(argv[1], argv[2], out)
    else:
        raise SystemExit(__doc__)


if __name__ == "__main__":
    main(sys.argv[1:])
