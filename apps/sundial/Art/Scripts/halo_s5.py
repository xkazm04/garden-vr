# Spike S5, halo v2. One smooth closed curve around the plant plus a ground ellipse, drawn once.
#
# Offline bake. For every plant card (and the bloom overlay on the three hero plants) it finds the soft hull of the
# drawing: close the alpha (10 to 15 percent of the plant height), union a ground ellipse under the base, smooth the
# union until the curve has at most two concavities, then draw a gold core a few pixels outside that hull. The result
# is the same R (core) and G (falloff) mask Fidelity/Card reads through _Silhouette, so the shader is untouched.
#
# The mask lives on a canvas that is wider than the card and reaches below it, in card-plane metres, isotropic in
# screen pixels at the DialG1 pitch (the card plane is vertical, so a screen pixel is 1 / cos(pitch) card metres
# tall). DialView draws the part above the card base on the card and the part below it on a flat quad on the soil.
#
#   python apps/sundial/Art/Scripts/halo_s5.py bake            # masks, halo2.json, contact sheet
#   python apps/sundial/Art/Scripts/halo_s5.py bake plant_midday_full   # one card
#   python apps/sundial/Art/Scripts/halo_s5.py measure A.png B.png NOHALO.png --out metrics.json
#   python apps/sundial/Art/Scripts/halo_s5.py flip|grey ...
import argparse
import json
import math
import os
import sys

import cv2
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, "..", "..", "..", ".."))
TEX = os.path.join(REPO, "apps", "sundial", "Assets", "Art", "Textures")
OUT = os.path.join(REPO, "apps", "sundial", "Assets", "Resources", "Halo2")
PREVIEW = os.path.join(REPO, "apps", "sundial", "Art", "Source", "halo2")

# Hero card in metres (DialView.PlantSize[1]) and the card texture.
CARD_W = 0.072
CARD_H = 0.112
TEX_W = 256
TEX_H = 512
# DialG1: eye (0, 0.322, -0.411) looking at (0, 0.012, 0).
PITCH_DEG = math.degrees(math.atan2(0.31, 0.411))
COS_P = math.cos(math.radians(PITCH_DEG))
# Canvas pixel. 0.2 mm across the card, 0.2 / cos(pitch) up it, so one canvas pixel is one square piece of screen.
MPP_X = 0.0002
MPP_Y = MPP_X / COS_P
# DialG1: 1910.8 px focal at 0.534 m. One G1 screen pixel on the hero card, in canvas pixels.
G1_PX = 0.2790e-3 / MPP_X
X_HALF = 0.064
V_TOP = 0.0806
V_BOTTOM = -0.032
CANVAS_W = int(round(2 * X_HALF / MPP_X))
CANVAS_H = int(round((V_TOP - V_BOTTOM) / MPP_Y))
# The bloom card is drawn BloomCardLift above the plant (DialView.BloomCardLift).
BLOOM_LIFT_M = 0.0015

# Look, in G1 screen pixels. The core keeps the T-SUN-031 stroke (3 px, gold). The gap is the spec's 4 to 10 px.
GAP_PX = 5.5
CORE_PX = 3.0
GLOW_PX = 5.0
CLOSE_FRACTION = 0.12
SMOOTH_FRACTION = 0.075
MIN_ELLIPSE_A_M = 0.014
ELLIPSE_A_FRACTION = 0.60
MAX_CONCAVITIES = 2

SPECIES = ("sunrise", "midday", "dusk", "reed", "clover", "vine", "sprig", "bell", "page")
STAGES = ("seed", "sprout", "young", "leafy", "full")
HEROES = ("sunrise", "midday", "dusk")
BLOOMS = ("bud", "open")


def load_alpha(name):
    path = os.path.join(TEX, name + ".png")
    return np.asarray(Image.open(path).convert("RGBA"))[:, :, 3]


def to_canvas(alpha, lift_rows=0):
    """The card alpha on the canvas, thresholded the way DialView does (a >= 128)."""
    sx = (CARD_W / TEX_W) / MPP_X
    sy = (CARD_H / TEX_H) / MPP_Y
    ox = (X_HALF - CARD_W / 2) / MPP_X
    oy = (V_TOP - CARD_H) / MPP_Y
    shifted = alpha
    if lift_rows > 0:
        shifted = np.zeros_like(alpha)
        shifted[:-lift_rows] = alpha[lift_rows:]
    m = np.array([[sx, 0.0, ox], [0.0, sy, oy]], dtype=np.float32)
    out = cv2.warpAffine(shifted.astype(np.float32), m, (CANVAS_W, CANVAS_H), flags=cv2.INTER_LINEAR,
                         borderMode=cv2.BORDER_CONSTANT, borderValue=0)
    return out >= 128.0


def ellipse_mask(cx, cy, a, b):
    img = np.zeros((CANVAS_H, CANVAS_W), np.uint8)
    cv2.ellipse(img, (int(round(cx)), int(round(cy))), (int(round(a)), int(round(b))), 0, 0, 360, 255, -1, cv2.LINE_AA)
    return img >= 128


def close(mask, radius):
    k = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (2 * radius + 1, 2 * radius + 1))
    m8 = mask.astype(np.uint8) * 255
    return cv2.morphologyEx(m8, cv2.MORPH_CLOSE, k) >= 128


def fill_holes(mask):
    m8 = mask.astype(np.uint8) * 255
    contours, _ = cv2.findContours(m8, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_NONE)
    out = np.zeros_like(m8)
    cv2.drawContours(out, contours, -1, 255, -1)
    return out >= 128


def smooth_hull(union, plant, sigma):
    """Blur the union and cut it at one half, growing the union by the least margin that keeps the plant inside.

    A tip blurs below the cut, so the margin comes before the blur: the curve stays one smooth contour."""
    k = int(sigma * 6) | 1
    hull = None
    for m in range(0, int(2.5 * sigma) + 1, 2):
        grown = union
        if m > 0:
            grown = cv2.dilate(union.astype(np.uint8), cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (2 * m + 1, 2 * m + 1))) > 0
        soft = cv2.GaussianBlur(grown.astype(np.float32), (k, k), sigma)
        hull = soft >= 0.5
        if hull[plant].all():
            break
    hull |= plant
    return fill_holes(hull)


def largest_contour(mask):
    m8 = mask.astype(np.uint8) * 255
    contours, _ = cv2.findContours(m8, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_NONE)
    if not contours:
        return None
    return max(contours, key=cv2.contourArea)[:, 0, :].astype(np.float64)


def curvature_runs(points, sigma=6.0, min_run=20.0, eps=0.004):
    """Count the concave runs of a closed curve. Positive curvature is the sign of the whole (convex majority)."""
    n = len(points)
    if n < 24:
        return 0, []
    # Resample to a fixed arc length first so sigma means the same thing everywhere.
    seg = np.hypot(*(np.roll(points, -1, axis=0) - points).T)
    cum = np.concatenate([[0.0], np.cumsum(seg)])
    total = cum[-1]
    count = max(int(total), 64)
    t = np.linspace(0.0, total, count, endpoint=False)
    xs = np.interp(t, cum, np.concatenate([points[:, 0], points[:1, 0]]))
    ys = np.interp(t, cum, np.concatenate([points[:, 1], points[:1, 1]]))
    step = total / count
    s = max(sigma / step, 1.0)
    r = int(s * 4)
    kern = np.exp(-0.5 * (np.arange(-r, r + 1) / s) ** 2)
    kern /= kern.sum()

    def sm(v):
        ext = np.concatenate([v[-r:], v, v[:r]])
        return np.convolve(ext, kern, mode="same")[r:-r]

    x = sm(xs)
    y = sm(ys)
    dx = (np.roll(x, -1) - np.roll(x, 1)) / (2 * step)
    dy = (np.roll(y, -1) - np.roll(y, 1)) / (2 * step)
    ddx = (np.roll(x, -1) - 2 * x + np.roll(x, 1)) / step ** 2
    ddy = (np.roll(y, -1) - 2 * y + np.roll(y, 1)) / step ** 2
    kappa = (dx * ddy - dy * ddx) / np.maximum((dx * dx + dy * dy) ** 1.5, 1e-9)
    sign = 1.0 if np.sum(kappa) >= 0 else -1.0
    concave = (kappa * sign) < -eps
    runs = []
    i = 0
    # Walk the ring once, joining a run that wraps.
    start = 0
    while start < count and concave[start]:
        start += 1
    if start == count:
        return 1, [(0, count)]
    order = [(start + k) % count for k in range(count)]
    run_len = 0
    run_start = None
    for k, idx in enumerate(order):
        if concave[idx]:
            if run_start is None:
                run_start = k
            run_len += 1
        else:
            if run_start is not None:
                if run_len * step >= min_run:
                    runs.append((order[run_start], run_len))
                run_start = None
                run_len = 0
    if run_start is not None and run_len * step >= min_run:
        runs.append((order[run_start], run_len))
    return len(runs), runs


def content_stats(plant):
    ys, xs = np.where(plant)
    if xs.size == 0:
        return None
    x0, x1, y0, y1 = int(xs.min()), int(xs.max()), int(ys.min()), int(ys.max())
    low = ys >= y1 - max(4, int(0.12 * (y1 - y0 + 1)))
    base_x = float((xs[low].min() + xs[low].max()) / 2.0)
    return {"x0": x0, "x1": x1, "y0": y0, "y1": y1, "w": x1 - x0 + 1, "h": y1 - y0 + 1, "base_x": base_x}


def drop_specks(mask, min_area=60):
    n, labels, stats, _ = cv2.connectedComponentsWithStats(mask.astype(np.uint8), connectivity=8)
    keep = np.zeros(n, bool)
    keep[1:] = stats[1:, cv2.CC_STAT_AREA] >= min_area
    return keep[labels]


def convex_of(mask):
    pts = cv2.findNonZero(mask.astype(np.uint8))
    out = np.zeros(mask.shape, np.uint8)
    cv2.fillConvexPoly(out, cv2.convexHull(pts), 255)
    return out >= 128


def build_hull(plant):
    plant = drop_specks(plant)
    st = content_stats(plant)
    if st is None:
        return None
    h = st["h"]
    close_r = max(3, int(round(CLOSE_FRACTION * h)))
    closed = close(plant, close_r)
    a = max(ELLIPSE_A_FRACTION * st["w"], MIN_ELLIPSE_A_M / MPP_X)
    # A ground circle of radius a seen at the G1 pitch is an ellipse a by a * sin(pitch). The canvas is isotropic in
    # screen pixels, so that is a by a * sin(pitch) here.
    b = a * math.sin(math.radians(PITCH_DEG))
    cx = st["base_x"]
    cy = st["y1"] - 0.02 * h
    ell = ellipse_mask(cx, cy, a, b)
    sigma0 = max(SMOOTH_FRACTION * h, 5.0)
    hull = None
    n_conc = None
    mode = "closed"
    for upper, mode in ((closed, "closed"), (convex_of(closed), "convex")):
        union = fill_holes(upper | ell)
        sigma = sigma0
        for _ in range(4):
            hull = smooth_hull(union, plant, sigma)
            pts = largest_contour(hull)
            n_conc, _ = curvature_runs(pts)
            if n_conc <= MAX_CONCAVITIES:
                break
            sigma *= 1.25
        if n_conc <= MAX_CONCAVITIES:
            break
    return {"hull": hull, "plant": plant, "ellipse": (cx, cy, a, b), "sigma": sigma, "close_r": close_r,
            "concavities": n_conc, "stats": st, "mode": mode}


def profile(hull):
    """R core and G falloff, as 0..1 floats. Distances are canvas pixels outside the hull."""
    m8 = hull.astype(np.uint8)
    outside = cv2.distanceTransform(1 - m8, cv2.DIST_L2, 5)
    inside = cv2.distanceTransform(m8, cv2.DIST_L2, 5)
    sd = outside - inside
    gap = GAP_PX * G1_PX
    core = CORE_PX * G1_PX
    glow = GLOW_PX * G1_PX
    d0 = gap + core / 2.0
    d = np.abs(sd - d0)
    aa = 1.0 * G1_PX * 0.6
    r = np.clip((core / 2.0 - d) / aa + 0.5, 0.0, 1.0)
    g = np.clip(1.0 - np.maximum(d - core / 2.0, 0.0) / glow, 0.0, 1.0) ** 1.4
    g *= (1.0 - r)
    # Nothing inside the hull: the plant reads through and the glow does not wash it.
    inner = sd < gap * 0.35
    r = np.where(inner, 0.0, r)
    g = np.where(inner, 0.0, g)
    return r, g, sd


def encode(r, g):
    img = np.zeros((CANVAS_H, CANVAS_W, 4), np.uint8)
    img[:, :, 0] = np.round(np.clip(r, 0, 1) * 255)
    img[:, :, 1] = np.round(np.clip(g, 0, 1) * 255)
    img[:, :, 3] = 255
    return img


def bake_one(plant_name, bloom_name=None):
    alpha = load_alpha(plant_name)
    plant = to_canvas(alpha)
    if bloom_name:
        lift = int(round(BLOOM_LIFT_M / CARD_H * TEX_H))
        plant = plant | to_canvas(load_alpha(bloom_name), lift)
    res = build_hull(plant)
    if res is None:
        return None
    r, g, sd = profile(res["hull"])
    img = encode(r, g)
    edge_touch = bool(r[0, :].max() > 0 or r[-1, :].max() > 0 or r[:, 0].max() > 0 or r[:, -1].max() > 0)
    # Gap from the plant to the core centre line, in G1 pixels, over the core pixels.
    core_ring = r > 0.5
    plant_dist = cv2.distanceTransform((~res["plant"]).astype(np.uint8), cv2.DIST_L2, 5)
    gaps = plant_dist[core_ring] / G1_PX if core_ring.any() else np.array([0.0])
    key = plant_name if not bloom_name else plant_name + "__" + bloom_name
    cx, cy, a, b = res["ellipse"]
    meta = {
        "key": key,
        "concavities": int(res["concavities"]),
        "closeRadiusPx": int(res["close_r"]),
        "upperShape": res["mode"],
        "sigmaPx": round(float(res["sigma"]), 2),
        "gapMinG1Px": round(float(gaps.min()), 2),
        "gapMedianG1Px": round(float(np.median(gaps)), 2),
        "gapP90G1Px": round(float(np.percentile(gaps, 90)), 2),
        "touchesCanvasEdge": edge_touch,
        # Ellipse centre in card-plane metres from the card centre and above the card base, semi axis in metres.
        "ellipseU": round((cx * MPP_X - X_HALF), 5),
        "ellipseV": round(V_TOP - cy * MPP_Y, 5),
        "ellipseA": round(a * MPP_X, 5),
    }
    return img, meta, res, (r, g)


def preview_image(plant_name, bloom_name, img, res):
    """Plant in card colours over the paper, halo added, on the canvas."""
    rgba = np.asarray(Image.open(os.path.join(TEX, plant_name + ".png")).convert("RGBA")).astype(np.float32)
    sx = (CARD_W / TEX_W) / MPP_X
    sy = (CARD_H / TEX_H) / MPP_Y
    ox = (X_HALF - CARD_W / 2) / MPP_X
    oy = (V_TOP - CARD_H) / MPP_Y
    m = np.array([[sx, 0.0, ox], [0.0, sy, oy]], dtype=np.float32)
    warped = cv2.warpAffine(rgba, m, (CANVAS_W, CANVAS_H), flags=cv2.INTER_LINEAR)
    if bloom_name:
        lift = int(round(BLOOM_LIFT_M / CARD_H * TEX_H))
        b = np.asarray(Image.open(os.path.join(TEX, bloom_name + ".png")).convert("RGBA")).astype(np.float32)
        shifted = np.zeros_like(b)
        shifted[:-lift] = b[lift:]
        bw = cv2.warpAffine(shifted, m, (CANVAS_W, CANVAS_H), flags=cv2.INTER_LINEAR)
        a = bw[:, :, 3:4] / 255.0
        warped[:, :, :3] = warped[:, :, :3] * (1 - a) + bw[:, :, :3] * a
        warped[:, :, 3:4] = np.maximum(warped[:, :, 3:4], bw[:, :, 3:4])
    bg = np.full((CANVAS_H, CANVAS_W, 3), (92, 62, 40), np.float32)
    a = warped[:, :, 3:4] / 255.0
    out = bg * (1 - a) + warped[:, :, :3] * a
    c1 = np.array([1.15, 0.86, 0.32]) * 255
    c2 = np.array([0.58, 0.38, 0.12]) * 255
    out = out + c1 * (img[:, :, 0:1] / 255.0) + c2 * (img[:, :, 1:2] / 255.0)
    return np.clip(out, 0, 255).astype(np.uint8)


def all_jobs():
    jobs = []
    for sp in SPECIES:
        for st in STAGES:
            jobs.append(("plant_%s_%s" % (sp, st), None))
    for sp in HEROES:
        for st in STAGES:
            for bl in BLOOMS:
                jobs.append(("plant_%s_%s" % (sp, st), "bloom_%s_%s" % (sp, bl)))
    return jobs


def cmd_bake(args):
    os.makedirs(OUT, exist_ok=True)
    os.makedirs(PREVIEW, exist_ok=True)
    jobs = all_jobs()
    if args.only:
        jobs = [j for j in jobs if args.only in (j[0] if not j[1] else j[0] + "__" + j[1])]
    metas = []
    sheet = []
    for plant, bloom in jobs:
        out = bake_one(plant, bloom)
        if out is None:
            print("skip", plant)
            continue
        img, meta, res, _ = out
        Image.fromarray(img, "RGBA").save(os.path.join(OUT, meta["key"] + ".png"), optimize=True)
        metas.append(meta)
        flag = "" if meta["concavities"] <= MAX_CONCAVITIES and not meta["touchesCanvasEdge"] else "  <-- check"
        print("%-44s conc %d gap min %.1f med %.1f p90 %.1f%s" % (
            meta["key"], meta["concavities"], meta["gapMinG1Px"], meta["gapMedianG1Px"], meta["gapP90G1Px"], flag))
        if plant in ("plant_midday_full", "plant_sunrise_full", "plant_dusk_full", "plant_sprig_full",
                     "plant_midday_seed", "plant_bell_young") and (bloom is None or bloom.endswith("open")):
            sheet.append(preview_image(plant, bloom, img, res))
    if args.only:
        return
    meta = {
        "version": 1,
        "pitchDeg": round(PITCH_DEG, 3),
        "mppX": MPP_X,
        "mppY": round(MPP_Y, 8),
        "xHalf": X_HALF,
        "vTop": V_TOP,
        "vBottom": V_BOTTOM,
        "cardW": CARD_W,
        "cardH": CARD_H,
        "canvasW": CANVAS_W,
        "canvasH": CANVAS_H,
        "g1PxCanvas": round(G1_PX, 4),
        "gapPx": GAP_PX,
        "corePx": CORE_PX,
        "glowPx": GLOW_PX,
        "masks": metas,
    }
    with open(os.path.join(OUT, "halo2.json"), "w", encoding="utf-8") as f:
        json.dump(meta, f, indent=1)
    lines = (
        "asset: halo2 masks (one png per plant card, plus the bloom overlay on the three hero plants)",
        "method: apps/sundial/Art/Scripts/halo_s5.py bake, computed from the plant card alpha: closing, ground ellipse union, "
        "smoothed hull, core and falloff profile (R core, G falloff)",
        "derived_from_art_reference: no",
    )
    with open(os.path.join(OUT, "halo2.provenance.txt"), "w", newline="\n") as f:
        f.write("\n".join(lines) + "\n")
    if sheet:
        strip = np.concatenate(sheet, axis=1)
        Image.fromarray(strip).save(os.path.join(PREVIEW, "contact.png"))
    worst = max(m["concavities"] for m in metas)
    print("masks %d, canvas %dx%d, max concavities %d, any edge touch %s" % (
        len(metas), CANVAS_W, CANVAS_H, worst, any(m["touchesCanvasEdge"] for m in metas)))


# ---------------------------------------------------------------- measurement on captured frames

RUN = os.path.join(REPO, "orchestration", "runs", "sundial", "T-SUN-047")
REF = os.path.join(REPO, "shared", "assets", "art-reference", "A2-05-field-notebook-1.png")


def read_rgb(path):
    return np.asarray(Image.open(path).convert("RGB")).astype(np.float32)


def halo_score(on, off):
    """Added light of the halo, the same weighting the T-SUN-031 glow gate uses (red 0.7, green 0.3)."""
    diff = on - off
    return np.clip(diff[:, :, 0], 0, None) * 0.7 + np.clip(diff[:, :, 1], 0, None) * 0.3


def glow_gate(score):
    """T-SUN-031 gate: peak excess >= 4 (8 bit) and the fall from the peak to a tenth >= 2 px. Crop is the lit area."""
    hot = score > 6
    if not hot.any():
        return {"peakExcess8bit": 0.0, "tenthFalloffPx": None, "pass": False}
    ys, xs = np.where(hot)
    y0, y1, x0, x1 = max(ys.min() - 24, 0), ys.max() + 25, max(xs.min() - 24, 0), xs.max() + 25
    crop = score[y0:y1, x0:x1]
    hot = crop > 6
    peak = float(np.percentile(crop[hot], 95))
    core = hot & (crop >= max(peak * 0.55, 8.0))
    dist = cv2.distanceTransform(np.where(core, 0, 255).astype(np.uint8), cv2.DIST_L2, 3)
    near = cv2.dilate(hot.astype(np.uint8), cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (17, 17))) > 0
    prof = []
    for d in range(1, 16):
        sel = near & (dist >= d - 0.5) & (dist < d + 0.5) & ~core
        if int(sel.sum()) < 12:
            continue
        prof.append((d, float(crop[sel].mean())))
    tenth = None
    if prof:
        peak_d, peak_v = max(prof, key=lambda t: t[1])
        for d, v in prof:
            if d >= peak_d and tenth is None and v <= max(peak_v, 1e-3) * 0.1:
                tenth = d - peak_d
        if tenth is None and prof[-1][0] > peak_d and prof[-1][1] > peak_v * 0.1:
            tenth = prof[-1][0] - peak_d
    return {"peakExcess8bit": round(peak, 2), "tenthFalloffPx": tenth, "hotPixels": int(hot.sum()),
            "pass": bool(peak >= 4.0 and tenth is not None and tenth >= 2)}


def ring_shape(on, off, plant_mask):
    """The core stroke on screen: components, closedness, concavities of the outline, gap to the plant.

    """
    # Read on black (the isolate=halo frames), so nothing clips: the stroke is the bright part of the added light.
    score = np.clip(on - off, 0, None)
    score = score[:, :, 0] * 0.7 + score[:, :, 1] * 0.3
    hot = score > 6
    if not hot.any():
        return {"components": 0}
    peak = float(np.percentile(score[hot], 95))
    core = (score >= max(peak * 0.55, 8.0)).astype(np.uint8)
    core = cv2.morphologyEx(core, cv2.MORPH_CLOSE, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (3, 3)))
    n, labels, stats, _ = cv2.connectedComponentsWithStats(core, connectivity=8)
    areas = [(int(stats[i, cv2.CC_STAT_AREA]), i) for i in range(1, n) if stats[i, cv2.CC_STAT_AREA] >= 25]
    areas.sort(reverse=True)
    out = {"components": len(areas), "componentAreas": [a for a, _ in areas[:8]]}
    if not areas:
        return out
    big = (labels == areas[0][1]).astype(np.uint8)
    out["largestShare"] = round(areas[0][0] / max(sum(a for a, _ in areas), 1), 3)
    # Closed: bridge gaps up to about 8 px,
    # then the pixels the stroke encloses (not reachable from the frame border) are a real area.
    h, w = big.shape
    bridged = cv2.morphologyEx(core, cv2.MORPH_CLOSE, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (17, 17)))
    n2, labels2, stats2, _ = cv2.connectedComponentsWithStats(bridged, connectivity=8)
    big2 = (labels2 == (1 + int(np.argmax(stats2[1:, cv2.CC_STAT_AREA])))).astype(np.uint8)
    flood = big2.copy() * 255
    pad = np.zeros((h + 2, w + 2), np.uint8)
    cv2.floodFill(flood, pad, (0, 0), 128)
    enclosed = int((flood == 0).sum())
    out["enclosedPxAfter8pxBridge"] = enclosed
    out["closedWith8pxBridge"] = bool(enclosed > 1500)
    filled = (flood != 128).astype(np.uint8)
    contours, _ = cv2.findContours(filled, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_NONE)
    if contours:
        pts = max(contours, key=cv2.contourArea)[:, 0, :].astype(np.float64)
        nconc, runs = curvature_runs(pts, sigma=6.0, min_run=20.0, eps=0.004)
        out["concavities"] = int(nconc)
        out["outlinePx"] = int(len(pts))
    if plant_mask is not None and plant_mask.any():
        dist = cv2.distanceTransform((~plant_mask).astype(np.uint8), cv2.DIST_L2, 5)
        rows = np.where(plant_mask.any(axis=1))[0]
        plant_bottom = int(rows.max())
        yy = np.arange(big.shape[0])[:, None] * np.ones((1, big.shape[1]))
        for label, sel in (("gapToPlantPx", big > 0), ("gapToPlantPxUpperContour", (big > 0) & (yy < plant_bottom - 15))):
            g = dist[sel]
            if g.size == 0:
                continue
            out[label] = {
                "p2": round(float(np.percentile(g, 2)), 2), "p10": round(float(np.percentile(g, 10)), 2),
                "p25": round(float(np.percentile(g, 25)), 2), "median": round(float(np.median(g)), 2),
                "shareIn4to10": round(float(((g >= 4) & (g <= 10)).mean()), 3),
                "shareIn4to20": round(float(((g >= 4) & (g <= 20)).mean()), 3),
            }
    return out


def plant_mask_from(with_plants, without_plants):
    d = np.abs(with_plants - without_plants).sum(2)
    m = (d > 90).astype(np.uint8)
    m = cv2.morphologyEx(m, cv2.MORPH_OPEN, np.ones((2, 2), np.uint8))
    return m > 0


def cmd_measure(args):
    """Scores the ring on the DialG1 frames. Midday is the pinch frame; morning is the unoccluded one."""
    res = {"gate": {
        "closedCurve": "one component, closed, at most 2 concavities",
        "gapPx": "4 to 10 px between the plant and the halo core",
        "glow": "T-SUN-031: peak excess >= 4 and tenth falloff >= 2 px"}}
    sets = {
        "midday": ("dial-g1-a-nohalo", "dial-g1-noplants", "iso-midday-a", "iso-midday-b", "g1-a-halo", "g1-b0-halo"),
        "morning": ("morning-nohalo", "morning-noplants", "iso-morning-a", "iso-morning-b", "morning-a", "morning-b0"),
        "dusk": ("dial-g1-a-nohalo", "dial-g1-noplants", "iso-dusk-a", "iso-dusk-b", "dusk-a", "dusk-b0"),
    }
    black = np.zeros((1024, 1824, 3), np.float32)
    for name, names in sets.items():
        nohalo, noplants, isoa, isob, ina, inb = [os.path.join(RUN, n + ".png") for n in names]
        # The target plant alone: the frame with only that plant against the frame with none (the same camera, no halo).
        plants = plant_mask_from(read_rgb(os.path.join(RUN, "only-%s.png" % name)), read_rgb(noplants))
        entry = {}
        for tag, path, in_scene, off_scene in (("A", isoa, ina, nohalo), ("B", isob, inb, nohalo)):
            iso = read_rgb(path)
            scene = read_rgb(in_scene)
            entry[tag] = {
                "ringOnBlack": ring_shape(iso, black, plants),
                # The T-SUN-031 glow gate as it was written, read on the lit scene against the same scene without the halo.
                "glowInScene": glow_gate(halo_score(scene, read_rgb(off_scene))),
                "glowOnBlack": glow_gate(halo_score(iso, black)),
            }
        res[name] = entry
    with open(args.out, "w", encoding="utf-8", newline="\n") as f:
        json.dump(res, f, indent=2)
        f.write("\n")
    print(json.dumps(res, indent=2))


def frames(a_path, b_path, grey=False):
    a = Image.open(a_path).convert("RGB")
    b = Image.open(b_path).convert("RGB")
    if grey:
        a, b = a.convert("L").convert("RGB"), b.convert("L").convert("RGB")
    return a, b


def cmd_flip(args):
    a, b = frames(args.a, args.b)
    box = tuple(int(v) for v in args.crop.split(",")) if args.crop else None
    if box:
        a, b = a.crop(box), b.crop(box)
    a.save(args.out, save_all=True, append_images=[b], duration=900, loop=0)
    print("wrote", args.out)


def cmd_grey(args):
    ref = Image.open(REF).convert("L").convert("RGB")
    a, b = frames(args.a, args.b, True)
    box = tuple(int(v) for v in args.crop.split(",")) if args.crop else None
    if box:
        ref, a, b = ref.crop(box), a.crop(box), b.crop(box)
    size = (912, 512) if not box else (a.width, a.height)
    sheet = Image.new("RGB", (size[0] * 3, size[1]), (255, 255, 255))
    for i, im in enumerate((ref, a, b)):
        sheet.paste(im.resize(size), (size[0] * i, 0))
    sheet.save(args.out)
    print("wrote", args.out)


def main(argv):
    p = argparse.ArgumentParser()
    sub = p.add_subparsers(dest="cmd", required=True)
    b = sub.add_parser("bake")
    b.add_argument("only", nargs="?", default=None)
    b.set_defaults(fn=cmd_bake)
    m = sub.add_parser("measure")
    m.add_argument("--out", default=os.path.join(RUN, "metrics.json"))
    m.set_defaults(fn=cmd_measure)
    for name, fn in (("flip", cmd_flip), ("grey", cmd_grey)):
        q = sub.add_parser(name)
        q.add_argument("a")
        q.add_argument("b")
        q.add_argument("out")
        q.add_argument("--crop", default=None, help="x0,y0,x1,y1")
        q.set_defaults(fn=fn)
    args = p.parse_args(argv)
    args.fn(args)


if __name__ == "__main__":
    main(sys.argv[1:])
