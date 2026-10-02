"""Compose Field Notebook hero textures from generated plates.

Sources live in apps/sundial/Assets/Art/Source (png + .prompt.txt).
Game textures land in apps/sundial/Assets/Art/Textures with a .provenance.txt
sidecar each. Nothing is sampled from shared/assets/art-reference.

    python tools/blender/sundial_textures.py

The first run copies the image_gen plates in from the session folder when the
source png is missing. Later runs only read the committed sources.
"""
import hashlib
import os
import math

import cv2
import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SRC = os.path.join(ROOT, "apps", "sundial", "Assets", "Art", "Source")
TEX = os.path.join(ROOT, "apps", "sundial", "Assets", "Art", "Textures")
RUN = os.path.join(ROOT, "orchestration", "runs", "sundial", "T-SUN-013")
REFS = [
    os.path.join(ROOT, "shared", "assets", "art-reference", "A2-05-field-notebook-1.png"),
    os.path.join(ROOT, "shared", "assets", "art-reference", "A2-05-field-notebook-2.png"),
]
GEN = (
    r"C:\Users\kazda\.grok\sessions"
    r"\C%3A%5CUsers%5Ckazda%5Ckiro%5Cgvr-sundial"
    r"\01a0fe53-30df-7d93-8bab-7afed02f60c8\images"
)

# filename in the session folder, aspect, exact prompt
PLATES = {
    "wash_morning": ("8.jpg", "1:1",
        "A square sheet of warm watercolour paper covered edge to edge with a loose sunrise wash. "
        "Pale cream and honey at the edges pool into soft ochre and a few deeper amber blooms, "
        "with visible granulation, feathered wet edges, and the tooth of the paper. "
        "No objects, no lines, no letters, only the gold wash."),
    "wash_midday": ("5.jpg", "1:1",
        "A square sheet of warm watercolour paper covered edge to edge with a loose midday wash. "
        "Pale peach and coral bloom into a few deeper rose pools, with granulation, soft wet edges, "
        "and the tooth of the paper showing through. No objects, no lines, no letters, only the wash."),
    "wash_dusk": ("2.jpg", "1:1",
        "A square sheet of warm watercolour paper covered edge to edge with a loose dusk wash. "
        "Pale lilac and mauve bloom into a few deeper violet pools, with granulation, soft wet edges, "
        "and the tooth of the paper showing through. No objects, no lines, no letters, only the wash."),
    "paper": ("1.jpg", "1:1",
        "A square close-up of blank warm cotton watercolour paper, ivory with a faint honey tone, "
        "showing fibre, tooth, and a few soft deckle variations. Even diffuse light, no drawing, "
        "no text, no picture."),
    "soil": ("6.jpg", "1:1",
        "An overhead painting of garden soil done in ink and gouache on warm paper, filling the frame "
        "edge to edge. Soft umber and tawny washes, darker pools, thousands of tiny ink stipple crumbs, "
        "and a few pale pebble marks. It reads as a drawing with visible brush and pen, matte, no plants, no text."),
    "plant_sunrise": ("3.jpg", "2:3",
        "A small rosemary-like herb sprig drawn as a frame of a hand-animated film, centered on a pure "
        "white background with a wide white margin. Several slender stems carry paired narrow leaves. "
        "Clean confident ink contours of varying weight, flat sage-green cel colour with one softer shade "
        "step, a few pencil construction ticks left in the leaves. No pot, no ground, no shadow, no text."),
    "plant_midday": ("7.jpg", "2:3",
        "A small flowering plant drawn as a frame of a hand-animated film, centered on a pure white "
        "background with a wide white margin all around. One upright green stem holds several open pink "
        "five-petal blossoms and two buds, with a few simple leaves. Clean confident ink contours of "
        "varying weight, flat cel colour with one softer shade step. The whole plant is fully inside the "
        "frame. No pot, no ground, no shadow, no text."),
    "plant_dusk": ("4.jpg", "2:3",
        "A small lavender plant drawn as a frame of a hand-animated film, centered on a pure white "
        "background with a wide white margin. A few narrow grey-green stems rise into soft purple flower "
        "spikes. Clean confident ink contours of varying weight, flat cel colour with one softer shade step. "
        "No pot, no ground, no shadow, no text."),
    "plant_sunrise_bloom": ("9.jpg", "2:3",
        "The same rosemary sprig on the same pure white background. Keep the stems, the paired leaves, "
        "the ink contours, and the sage green. A few tiny cream blossoms are just opening at the tips of "
        "the upper stems."),
    "plant_midday_bloom": ("10.jpg", "2:3",
        "The same pink flowering stem on the same pure white background. Keep the stem, the leaves, and "
        "the ink contours. The blossoms are more fully open, the petals spread a little wider, and one "
        "more small flower has opened near the top."),
    "plant_dusk_bloom": ("11.jpg", "2:3",
        "The same lavender on the same pure white background. Keep the stems, the narrow leaves, and the "
        "ink. The purple flower spikes are fuller, with more open florets along each spike."),
}

INK = np.array([42, 38, 34], np.float32)       # #2A2622
PENCIL = np.array([138, 129, 120], np.float32)  # #8A8178
PAPER = np.array([243, 238, 226], np.float32)   # #F3EEE2
SOIL_MEAN = np.array([160, 131, 108], np.float32)  # #A0836C

# 0 is +X, 90 is up the page. Bands are thick on purpose.
# Wider than dial_svg.mjs (sunrise 212 to 112, dusk 16 to -76). The first
# side-by-side left a blank wedge at the front, so sunrise starts at 242
# and dusk ends at -100. About 18 degrees of paper remains. The .mjs is unchanged.
# name, wash file, a0, a1, pale, mid, wet.
ARCS = [
    ("sunrise", "wash_morning", 242, 112, (246, 222, 183), (226, 184, 102), (217, 118, 42)),
    ("midday", "wash_midday", 106, 22, (244, 182, 161), (227, 156, 130), (201, 72, 63)),
    ("dusk", "wash_dusk", 16, -100, (191, 161, 188), (167, 154, 214), (111, 85, 173)),
]


def sha256(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 16), b""):
            h.update(chunk)
    return h.hexdigest()


def smoothstep(x):
    x = np.clip(x, 0.0, 1.0)
    return x * x * (3.0 - 2.0 * x)


def fbm(shape, seed, octaves=5, base=8):
    rng = np.random.default_rng(seed)
    out = np.zeros(shape, np.float32)
    amp = 1.0
    total = 0.0
    h, w = shape
    for o in range(octaves):
        n = base * (2 ** o)
        grid = rng.random((n, n), np.float32)
        up = cv2.resize(grid, (w, h), interpolation=cv2.INTER_CUBIC)
        out += up * amp
        total += amp
        amp *= 0.5
    return out / total


def hex_rgb(t):
    return np.array(t, np.float32)


def srgb_to_lab(rgb):
    x = np.asarray(rgb, np.float32) / 255.0
    lin = np.where(x <= 0.04045, x / 12.92, ((x + 0.055) / 1.055) ** 2.4)
    m = np.array([
        [0.4124564, 0.3575761, 0.1804375],
        [0.2126729, 0.7151522, 0.0721750],
        [0.0193339, 0.1191920, 0.9503041],
    ], np.float32)
    xyz = lin @ m.T
    xyz = xyz / np.array([0.95047, 1.0, 1.08883], np.float32)
    d = np.where(xyz > 0.008856, np.cbrt(xyz), 7.787 * xyz + 16.0 / 116.0)
    lab = np.stack([116.0 * d[1] - 16.0, 500.0 * (d[0] - d[1]), 200.0 * (d[1] - d[2])])
    return lab


def ciede2000(lab1, lab2):
    L1, a1, b1 = [float(v) for v in lab1]
    L2, a2, b2 = [float(v) for v in lab2]
    c1 = math.hypot(a1, b1)
    c2 = math.hypot(a2, b2)
    cbar = (c1 + c2) / 2.0
    g = 0.5 * (1.0 - math.sqrt(cbar ** 7 / (cbar ** 7 + 25.0 ** 7)))
    a1p, a2p = a1 * (1 + g), a2 * (1 + g)
    c1p, c2p = math.hypot(a1p, b1), math.hypot(a2p, b2)
    h1p = math.atan2(b1, a1p) % (2 * math.pi)
    h2p = math.atan2(b2, a2p) % (2 * math.pi)
    dlp = L2 - L1
    dcp = c2p - c1p
    if c1p * c2p == 0:
        dhp = 0.0
    else:
        dhp = h2p - h1p
        if dhp > math.pi:
            dhp -= 2 * math.pi
        elif dhp < -math.pi:
            dhp += 2 * math.pi
    dhp_v = 2 * math.sqrt(c1p * c2p) * math.sin(dhp / 2.0)
    lbar = (L1 + L2) / 2.0
    cpbar = (c1p + c2p) / 2.0
    if c1p * c2p == 0:
        hpbar = h1p + h2p
    elif abs(h1p - h2p) <= math.pi:
        hpbar = (h1p + h2p) / 2.0
    elif h1p + h2p < 2 * math.pi:
        hpbar = (h1p + h2p + 2 * math.pi) / 2.0
    else:
        hpbar = (h1p + h2p - 2 * math.pi) / 2.0
    t = (1 - 0.17 * math.cos(hpbar - math.pi / 6) + 0.24 * math.cos(2 * hpbar)
         + 0.32 * math.cos(3 * hpbar + math.pi / 30) - 0.20 * math.cos(4 * hpbar - math.radians(63)))
    dtheta = math.radians(30) * math.exp(-((hpbar - math.radians(275)) / math.radians(25)) ** 2)
    rc = 2 * math.sqrt(cpbar ** 7 / (cpbar ** 7 + 25.0 ** 7))
    sl = 1 + 0.015 * (lbar - 50) ** 2 / math.sqrt(20 + (lbar - 50) ** 2)
    sc = 1 + 0.045 * cpbar
    sh = 1 + 0.015 * cpbar * t
    rt = -math.sin(2 * dtheta) * rc
    return math.sqrt((dlp / sl) ** 2 + (dcp / sc) ** 2 + (dhp_v / sh) ** 2 + rt * (dcp / sc) * (dhp_v / sh))


def load_rgb(path):
    return np.array(Image.open(path).convert("RGB"))


def save_png(path, arr):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8)).save(path)


def grade_mean(img, target):
    mean = img.reshape(-1, img.shape[-1]).mean(axis=0)
    scale = target / np.maximum(mean, 1.0)
    scale = np.clip(scale, 0.55, 1.8)
    return np.clip(img * scale, 0, 255)


def center_crop(img, frac):
    h, w = img.shape[:2]
    mh, mw = int(h * (1 - frac) / 2), int(w * (1 - frac) / 2)
    return img[mh:h - mh, mw:w - mw]


def ingest():
    os.makedirs(SRC, exist_ok=True)
    for name, (fn, aspect, prompt) in PLATES.items():
        dest = os.path.join(SRC, name + ".png")
        if not os.path.exists(dest):
            src = os.path.join(GEN, fn)
            if not os.path.exists(src):
                raise SystemExit("missing source %s (%s)" % (name, src))
            im = Image.open(src).convert("RGB")
            im.save(dest)
            print("ingested", name, "from", fn, im.size)
        side = os.path.join(SRC, name + ".prompt.txt")
        tool = "image_edit" if "bloom" in name else "image_gen"
        text = "\n".join([
            "name: %s" % name,
            "tool: %s" % tool,
            "aspect_ratio: %s" % aspect,
            "date: 2026-10-02",
            "derived_from_art_reference: no",
            "prompt: %s" % prompt,
            "",
        ])
        with open(side, "w", encoding="utf-8", newline="\n") as f:
            f.write(text)


def provenance(path, lines):
    with open(path + ".provenance.txt", "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines) + "\n")


# ---------------------------------------------------------------- dial face
def paint_strokes(img, mask, color, alpha):
    m = (mask.astype(np.float32) / 255.0) * alpha
    img[:] = img * (1.0 - m[..., None]) + color * m[..., None]


def wobble_polyline(p0, p1, amp, seed, n=64):
    ang = math.atan2(p1[1] - p0[1], p1[0] - p0[0])
    px, py = math.cos(ang + math.pi / 2), math.sin(ang + math.pi / 2)
    pts = []
    for i in range(n + 1):
        t = i / n
        off = amp * math.sin(t * math.pi * 4 + seed) + 0.45 * amp * math.sin(t * math.pi * 13 + seed * 1.7)
        x = p0[0] * (1 - t) + p1[0] * t + px * off
        y = p0[1] * (1 - t) + p1[1] * t + py * off
        pts.append((int(round(x)), int(round(y))))
    return np.array(pts, np.int32)


def circle_pts(cx, cy, r, amp, seed, n=360):
    pts = []
    for i in range(n):
        a = 2 * math.pi * i / n
        wob = amp * math.sin(i * 0.11 + seed) + 0.4 * amp * math.sin(i * 0.37 + seed * 2)
        rr = r + wob
        pts.append((int(round(cx + rr * math.cos(a))), int(round(cy - rr * math.sin(a)))))
    return np.array(pts, np.int32)


def compose_face():
    s = 2048
    c = s / 2.0
    radius = s / 2.0 - 10.0
    paper = load_rgb(os.path.join(SRC, "paper.png")).astype(np.float32)
    paper = center_crop(paper, 0.55)
    paper = cv2.resize(paper, (s, s), interpolation=cv2.INTER_CUBIC)
    paper = grade_mean(paper, PAPER)
    grain = paper - cv2.GaussianBlur(paper, (0, 0), 7)
    base = np.clip(PAPER + (paper - paper.mean(axis=(0, 1))) * 0.45, 0, 255)

    yy, xx = np.mgrid[0:s, 0:s].astype(np.float32)
    dx = xx - c
    dy = c - yy
    r = np.hypot(dx, dy)
    ang = np.arctan2(dy, dx)
    n_r = fbm((s, s), 5, base=6)
    n_a = fbm((s, s), 9, base=6)
    rr = r + (n_r - 0.5) * 42.0
    aang = ang + (n_a - 0.5) * math.radians(6.0)
    aang_deg = np.degrees(aang)

    face = base.copy()
    r0, r1 = radius * 0.33, radius * 0.905
    reports = []
    for name, wash_file, a0, a1, pale, mid, wet in ARCS:
        plate = load_rgb(os.path.join(SRC, wash_file + ".png")).astype(np.float32)
        plate = cv2.resize(plate, (s, s), interpolation=cv2.INTER_CUBIC)
        span = (a0 - a1) % 360
        pos = (a0 - aang_deg) % 360
        d_ang = np.minimum(pos, span - pos)
        ang_m = smoothstep(d_ang / 6.5) * (pos <= span + 6.5)
        d0 = rr - r0
        d1 = r1 - rr
        rad_m = smoothstep(d0 / 36.0) * smoothstep(d1 / 26.0)
        mask = ang_m * rad_m
        # soft blooms: pigment that is already dark in the plate leaks past the edge
        pigment = 1.0 - plate.mean(axis=2) / 255.0
        spill = smoothstep((pigment - 0.18) / 0.22) * np.exp(-np.maximum(-d0, 0) / 48.0)
        spill *= smoothstep((span + 4 - pos) / 8.0) * (pos < span + 8)
        mask = np.clip(mask + spill * 0.45 * smoothstep((d1 + 18) / 30.0), 0, 1)

        t = np.clip((rr - r0) / (r1 - r0), 0, 1)
        pale_c = hex_rgb(pale)
        wet_c = hex_rgb(wet)
        target = wet_c * (1.0 - t)[..., None] + pale_c * t[..., None]
        var = plate - cv2.GaussianBlur(plate, (0, 0), 31)
        hf = plate - cv2.GaussianBlur(plate, (0, 0), 2.2)
        col = target + var * 0.72 + hf * 0.35
        pool = np.exp(-((t - 0.06) ** 2) / (2 * 0.055 ** 2))
        col = col * (1.0 - 0.22 * pool[..., None]) + wet_c * (0.28 * pool[..., None])
        col = np.clip(col, 0, 255)
        face = face * (1.0 - mask[..., None]) + col * mask[..., None]
        reports.append((name, mask, pale_c, a0, a1))

    # pencil construction and the tick ring, then ink rims
    overlay = np.zeros((s, s), np.uint8)
    for i, deg in enumerate((0, 45, 90, 135)):
        a = math.radians(deg)
        p0 = (c + math.cos(a) * radius * 0.20, c - math.sin(a) * radius * 0.20)
        p1 = (c + math.cos(a) * radius * 0.88, c - math.sin(a) * radius * 0.88)
        p2 = (c - math.cos(a) * radius * 0.20, c + math.sin(a) * radius * 0.20)
        p3 = (c - math.cos(a) * radius * 0.88, c + math.sin(a) * radius * 0.88)
        cv2.polylines(overlay, [wobble_polyline(p0, p1, 2.2, i + 1)], False, 180, 2, cv2.LINE_AA)
        cv2.polylines(overlay, [wobble_polyline(p2, p3, 2.2, i + 4)], False, 160, 2, cv2.LINE_AA)
    cv2.polylines(overlay, [circle_pts(c, c, radius * 0.905, 2.4, 2.0)], True, 140, 2, cv2.LINE_AA)
    cv2.polylines(overlay, [circle_pts(c, c, radius * 0.33, 3.0, 3.0)], True, 120, 2, cv2.LINE_AA)
    paint_strokes(face, overlay, PENCIL, 0.55)

    ticks = np.zeros((s, s), np.uint8)
    for d in range(0, 360, 3):
        a = math.radians(d)
        long = d % 30 == 0
        mid = d % 15 == 0
        outer = radius * 0.985
        inner = radius * (0.905 if long else 0.935 if mid else 0.955)
        p0 = (int(c + math.cos(a) * inner), int(c - math.sin(a) * inner))
        p1 = (int(c + math.cos(a) * outer), int(c - math.sin(a) * outer))
        cv2.line(ticks, p0, p1, 230 if long else 180, 3 if long else 2, cv2.LINE_AA)
    paint_strokes(face, ticks, np.array([59, 51, 41], np.float32), 0.9)

    ink = np.zeros((s, s), np.uint8)
    # varying pressure around the rim: draw the circle in short spans
    rim = circle_pts(c, c, radius * 0.995, 3.2, 1.2, 480)
    for i in range(len(rim)):
        w = 5 + int(round(3.0 * (0.5 + 0.5 * math.sin(i * 0.07 + 0.4))))
        cv2.line(ink, tuple(rim[i]), tuple(rim[(i + 1) % len(rim)]), 255, w, cv2.LINE_AA)
    inner = circle_pts(c, c, radius * 0.878, 2.6, 2.4, 420)
    cv2.polylines(ink, [inner], True, 220, 4, cv2.LINE_AA)
    paint_strokes(face, ink, INK, 0.92)

    # granulation specks inside the washes, and paper grain over the whole disc
    rng = np.random.default_rng(21)
    speck = rng.random((s, s)) < 0.035
    wash_zone = (r > r0) & (r < r1)
    face[speck & wash_zone] *= 0.86

    disc = r <= radius + 2
    face = np.clip(face + grain * 0.20, 0, 255)
    face[~disc] = PAPER
    # pull the pale band of each arc toward the measured hex
    for _ in range(3):
        for name, mask, pale_c, a0, a1 in reports:
            t = np.clip((rr - r0) / (r1 - r0), 0, 1)
            pale_m = (mask > 0.55) & (t > 0.62)
            if pale_m.sum() < 100:
                continue
            mean = face[pale_m].mean(axis=0)
            de = ciede2000(srgb_to_lab(mean), srgb_to_lab(pale_c))
            delta = pale_c - mean
            face[pale_m] = np.clip(face[pale_m] + delta * 0.65, 0, 255)
            print("  arc %-8s pale mean %6.1f %6.1f %6.1f  dE00 %5.2f" % (name, mean[0], mean[1], mean[2], de))

    # final measurement after the last correction
    print("dial face pale regions after grade:")
    for name, mask, pale_c, a0, a1 in reports:
        t = np.clip((rr - r0) / (r1 - r0), 0, 1)
        pale_m = (mask > 0.55) & (t > 0.62)
        mean = face[pale_m].mean(axis=0)
        de = ciede2000(srgb_to_lab(mean), srgb_to_lab(pale_c))
        print("  arc %-8s FINAL %6.1f %6.1f %6.1f  dE00 %5.2f  target %s" % (
            name, mean[0], mean[1], mean[2], de, tuple(int(v) for v in pale_c)))

    out = os.path.join(TEX, "dial_face.png")
    save_png(out, face)
    provenance(out, [
        "asset: dial_face.png",
        "date: 2026-10-02",
        "method: composed by tools/blender/sundial_textures.py",
        "sources: wash_morning.png, wash_midday.png, wash_dusk.png, paper.png",
        "generator: image_gen washes and paper, scripted arcs, pencil, ticks, ink rim",
        "size: 2048",
        "lettering: none",
        "derived_from_art_reference: no",
        "sha256: %s" % sha256(out),
    ])
    # close-up of the sunrise wash, left of centre
    a = math.radians(160)
    px = int(c + math.cos(a) * radius * 0.62)
    py = int(c - math.sin(a) * radius * 0.62)
    crop = face[py - 360:py + 360, px - 360:px + 360]
    save_png(os.path.join(RUN, "closeup-wash.png"), crop)
    print("wrote", out, "and closeup-wash.png")
    return face


# ---------------------------------------------------------------- soil
def compose_soil():
    img = load_rgb(os.path.join(SRC, "soil.png")).astype(np.float32)
    luma = img.mean(axis=2)
    chroma = img.max(axis=2) - img.min(axis=2)
    fg = (luma < 215) | (chroma > 16)
    ys, xs = np.where(fg)
    pad = 8
    img = img[max(0, ys.min() - pad):ys.max() + pad, max(0, xs.min() - pad):xs.max() + pad]
    img = cv2.resize(img, (1024, 1024), interpolation=cv2.INTER_AREA)
    img = grade_mean(img, SOIL_MEAN)
    # keep the pebbles, pull the brightest ones back so they stay drawn marks
    luma = img.mean(axis=2)
    hot = luma > 205
    mean = img.reshape(-1, 3).mean(axis=0)
    img[hot] = img[hot] * 0.62 + mean * 0.38
    # a darker damp pool toward the middle, still painted
    yy, xx = np.mgrid[0:1024, 0:1024]
    r = np.hypot(xx - 512, yy - 512) / 512.0
    pool = np.clip(1.0 - r, 0, 1) ** 1.4
    img *= (1.0 - 0.10 * pool[..., None])
    rng = np.random.default_rng(77)
    stipple = rng.random((1024, 1024)) < 0.05
    img[stipple] *= 0.72
    img = np.clip(img, 0, 255)
    out = os.path.join(TEX, "soil.png")
    save_png(out, img)
    mean = img.reshape(-1, 3).mean(axis=0)
    print("soil mean %6.1f %6.1f %6.1f" % (mean[0], mean[1], mean[2]))
    provenance(out, [
        "asset: soil.png",
        "date: 2026-10-02",
        "method: composed by tools/blender/sundial_textures.py",
        "sources: soil.png (image_gen gouache and ink stipple)",
        "size: 1024",
        "derived_from_art_reference: no",
        "sha256: %s" % sha256(out),
        "note: graded toward #A0836C; extra ink stipple added in script. Not a photograph.",
    ])
    return img


# ---------------------------------------------------------------- plants
def key_plant(rgb):
    luma = rgb.mean(axis=2)
    chroma = rgb.max(axis=2) - rgb.min(axis=2)
    fg = ((chroma > 14) | (luma < 186)).astype(np.uint8)
    fg = cv2.morphologyEx(fg, cv2.MORPH_OPEN, np.ones((2, 2), np.uint8))
    n, labels, stats, _ = cv2.connectedComponentsWithStats(fg, 8)
    h, w = fg.shape
    keep = np.zeros_like(fg)
    for i in range(1, n):
        x, y, bw, bh, area = stats[i]
        if area < 90:
            continue
        if x <= 1 or y <= 1 or x + bw >= w - 1 or y + bh >= h - 1:
            continue
        keep[labels == i] = 1
    if keep.sum() < 200:
        raise SystemExit("plant key kept almost nothing")
    alpha = (keep * 255).astype(np.uint8)
    # feather only the outermost pixel
    soft = cv2.GaussianBlur(alpha, (0, 0), 0.6)
    soft[keep == 1] = 255
    rgba = np.dstack([rgb, soft])
    ys, xs = np.where(keep)
    pad = 18
    y0, y1 = max(0, ys.min() - pad), min(h, ys.max() + pad)
    x0, x1 = max(0, xs.min() - pad), min(w, xs.max() + pad)
    return rgba[y0:y1, x0:x1]


def vary_weight(rgba, seed):
    """Pressure bulges on the contour so the line is not one constant width."""
    a = rgba[:, :, 3]
    fg = (a > 40).astype(np.uint8)
    cnts, _ = cv2.findContours(fg, cv2.RETR_LIST, cv2.CHAIN_APPROX_NONE)
    overlay = np.zeros(fg.shape, np.uint8)
    rng = np.random.default_rng(seed)
    for cnt in cnts:
        pts = cnt[:, 0]
        n = len(pts)
        if n < 24:
            continue
        phase = float(rng.random() * math.pi * 2)
        for i in range(0, n, 2):
            s = i / n
            wave = 0.5 + 0.5 * math.sin(s * math.pi * 2 * 3.0 + phase)
            if wave < 0.55:
                continue
            w = 2 + int(round(2.4 * wave))
            j = min(i + 2, n - 1)
            cv2.line(overlay, tuple(pts[i]), tuple(pts[j]), 255, w, cv2.LINE_AA)
    m = overlay > 30
    rgba[m, :3] = INK
    rgba[m, 3] = 255
    return rgba


def drop_ghosts(rgba):
    """Pencil smudges and paper flecks are light and low-chroma. Ink stays."""
    rgb = rgba[:, :, :3].astype(np.float32)
    luma = rgb.mean(axis=2)
    chroma = rgb.max(axis=2) - rgb.min(axis=2)
    ghost = (chroma < 22) & (luma > 145) & (rgba[:, :, 3] > 0)
    rgba[ghost, 3] = 0
    return rgba


def boil_frame(rgba, seed, amp=8.0):
    """Redraw the outline. Deep interior pixels are copied unchanged."""
    a = rgba[:, :, 3]
    fg = (a > 24).astype(np.uint8)
    dist_in = cv2.distanceTransform(fg, cv2.DIST_L2, 3)
    dist_out = cv2.distanceTransform(1 - fg, cv2.DIST_L2, 3)
    h, w = a.shape
    n1 = fbm((h, w), seed, octaves=4, base=5)
    n2 = fbm((h, w), seed + 11, octaves=4, base=5)
    weight = np.exp(-np.clip(dist_in, 0, 14) / 2.6)
    near = (dist_out > 0) & (dist_out < 5.0)
    weight = np.maximum(weight, near.astype(np.float32) * np.exp(-dist_out / 2.4))
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    sx = xx - (n1 - 0.5) * 2.0 * amp * weight
    sy = yy - (n2 - 0.5) * 2.0 * amp * weight
    warped = cv2.remap(rgba, sx, sy, cv2.INTER_LINEAR, borderMode=cv2.BORDER_CONSTANT, borderValue=(0, 0, 0, 0))
    deep = dist_in > 5.0
    warped[deep] = rgba[deep]
    return warped


def fit_long(rgba, long_side=1024):
    h, w = rgba.shape[:2]
    m = max(h, w)
    if m == long_side:
        return rgba
    scale = long_side / m
    nh, nw = max(1, int(round(h * scale))), max(1, int(round(w * scale)))
    return cv2.resize(rgba, (nw, nh), interpolation=cv2.INTER_AREA)


def prepare_plant(rgba):
    rgba = drop_ghosts(rgba)
    rgba = vary_weight(rgba, seed=3)
    return fit_long(rgba, 1024)


def halo_from(alpha):
    """Irregular gold ring around the silhouette, padded so the glow is not clipped."""
    pad = 72
    a = np.pad((alpha > 24).astype(np.uint8) * 255, pad)
    outer = cv2.dilate(a, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (34, 26)))
    inner = cv2.dilate(a, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (10, 8)))
    ring = cv2.subtract(outer, inner)
    n = fbm(ring.shape, 4, octaves=3, base=7)
    ring = np.where(n > 0.08, ring, 0).astype(np.uint8)
    ring = cv2.GaussianBlur(ring, (0, 0), 1.3)
    glow = np.clip(ring.astype(np.float32) / 180.0, 0, 1)
    out = np.zeros((*ring.shape, 4), np.float32)
    out[:, :, 0] = 245
    out[:, :, 1] = 199
    out[:, :, 2] = 106
    out[:, :, 3] = np.where(glow > 0.2, 255, 0)
    return out


def compose_plants():
    kinds = ("sunrise", "midday", "dusk")
    sheets = {}
    for kind in kinds:
        base = key_plant(load_rgb(os.path.join(SRC, "plant_%s.png" % kind)))
        base = prepare_plant(base)
        bloom = key_plant(load_rgb(os.path.join(SRC, "plant_%s_bloom.png" % kind)))
        bloom = prepare_plant(bloom)
        bloom = fit_long(bloom, max(base.shape[0], base.shape[1]))
        frames = [boil_frame(base, seed=20 + i * 9) for i in range(3)]
        # interior colour must match frame to frame
        for i in range(3):
            cov = (frames[i][:, :, 3] > 20).mean()
            print("  plant_%s_%d alpha coverage %.3f size %s" % (kind, i, cov, frames[i].shape[:2]))
            if cov < 0.02:
                raise SystemExit("plant %s frame %d keyed away" % (kind, i))
        names = ["plant_%s_%d.png" % (kind, i) for i in range(3)] + ["plant_%s_bloom.png" % kind]
        images = frames + [bloom]
        notes = [
            "boil frame %d: outline resampled from plant_%s.png, interior pixels copied" % (i, kind)
            for i in range(3)
        ] + ["bloom state from plant_%s_bloom.png (image_edit of the same plant)" % kind]
        for name, im, note in zip(names, images, notes):
            path = os.path.join(TEX, name)
            save_png(path, im)
            provenance(path, [
                "asset: %s" % name,
                "date: 2026-10-02",
                "method: composed by tools/blender/sundial_textures.py",
                "sources: plant_%s.png" % kind,
                "note: %s" % note,
                "derived_from_art_reference: no",
                "sha256: %s" % sha256(path),
            ])
        halo = halo_from(base[:, :, 3])
        hpath = os.path.join(TEX, "halo_%s.png" % kind)
        save_png(hpath, halo)
        provenance(hpath, [
            "asset: halo_%s.png" % kind,
            "date: 2026-10-02",
            "method: dilated silhouette of plant_%s_0, gold #F5C76A, scripted" % kind,
            "sources: plant_%s.png" % kind,
            "derived_from_art_reference: no",
            "sha256: %s" % sha256(hpath),
        ])
        sheets[kind] = frames
        print("wrote", kind, "frames", [im.shape[:2] for im in images])
    # contact sheet of the midday boil, the plant the halo sits on
    frames = sheets["midday"]
    h = max(f.shape[0] for f in frames)
    w = sum(f.shape[1] for f in frames) + 16 * 4
    sheet = Image.new("RGB", (w, h + 48), (243, 238, 226))
    draw = ImageDraw.Draw(sheet)
    try:
        font = ImageFont.truetype("C:/Windows/Fonts/segoeui.ttf", 22)
    except OSError:
        font = ImageFont.load_default()
    x = 16
    for i, f in enumerate(frames):
        card = Image.fromarray(f.astype(np.uint8), "RGBA")
        sheet.paste(card, (x, 40), card)
        draw.text((x, 8), "midday boil %d" % i, fill=(42, 38, 34), font=font)
        x += f.shape[1] + 16
    sheet.save(os.path.join(RUN, "closeup-boil.png"))
    print("wrote closeup-boil.png", sheet.size)


def gate_a4():
    banned = {sha256(p) for p in REFS}
    bad = []
    for folder in (SRC, TEX):
        for fn in os.listdir(folder):
            if not fn.lower().endswith(".png"):
                continue
            path = os.path.join(folder, fn)
            digest = sha256(path)
            if digest in banned:
                bad.append(path)
    if bad:
        raise SystemExit("A4 fail, byte-identical to a reference frame: %s" % bad)
    print("provenance ok: no texture or source matches the reference frames")


def main():
    os.makedirs(TEX, exist_ok=True)
    os.makedirs(RUN, exist_ok=True)
    ingest()
    compose_face()
    compose_soil()
    compose_plants()
    gate_a4()


if __name__ == "__main__":
    main()
