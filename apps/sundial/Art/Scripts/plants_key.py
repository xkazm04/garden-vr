# Key the sundial plant cards onto one canvas.
# White paper becomes alpha. Ink stays opaque. Pale wash keeps a soft alpha.
# Every stage of a species shares the full plant's scale, and the soil base
# lands on one pivot, so a stage change does not slide the plant.
#
#   python apps/sundial/Art/Scripts/plants_key.py
#   python apps/sundial/Art/Scripts/plants_key.py --report-only
import argparse
import math
import os
import sys
from pathlib import Path

import cv2
import numpy as np
from PIL import Image, ImageDraw, ImageFont

HERE = Path(__file__).resolve().parent
APP = HERE.parents[1]
SRC = APP / "Art" / "Source" / "plants"
# Kept plates live next to the prompt files. Rejected plates stay in candidates/ and are not read.
CAND = SRC
TEX = APP / "Assets" / "Art" / "Textures"
RUN = APP.parents[1] / "orchestration" / "runs" / "sundial" / "T-SUN-007"

W, H = 256, 512
# Soil contact. x is the centre, y is down from the top of the PNG.
# 36 px of canvas stay below the base so the halo can breathe.
PIVOT_X = 128
PIVOT_Y = 476
MARGIN = 28

ARCS = ("sunrise", "midday", "dusk")
STAGES = ("seed", "sprout", "young", "leafy", "full")
BLOOMS = ("bud", "open")
# Arc washes from docs/art/sundial-style.md. Pale pixels are graded a step toward these
# so a seed and a leafy stem share one wash instead of one reading as soil and the other as leaf.
WASH_RGB = {
    "sunrise": np.array([226, 184, 102], np.float32),
    "midday": np.array([227, 156, 130], np.float32),
    "dusk": np.array([167, 154, 214], np.float32),
}

# Source drawings. Rejected kitchen plates stay in candidates/ and are not keyed.
SOURCES = {
    "sunrise": {
        "seed": "sunrise-seed.jpg",
        "sprout": "sunrise-sprout.jpg",
        "young": "sunrise-young.jpg",
        "leafy": "sunrise-leafy.jpg",
        "full": "sunrise-full.jpg",
        "bud": "sunrise-bud.jpg",
        "open": "sunrise-open.jpg",
    },
    "midday": {
        "seed": "midday-seed.jpg",
        "sprout": "midday-sprout.jpg",
        "young": "midday-young.jpg",
        "leafy": "midday-leafy.jpg",
        "full": "midday-full-v2.jpg",
        "bud": "midday-bud.jpg",
        "open": "midday-open.jpg",
    },
    "dusk": {
        "seed": "dusk-seed.jpg",
        "sprout": "dusk-sprout.jpg",
        "young": "dusk-young.jpg",
        "leafy": "dusk-leafy.jpg",
        "full": "dusk-full.jpg",
        "bud": "dusk-bud.jpg",
        "open": "dusk-open.jpg",
    },
}

STYLE = (
    "Style: hand-drawn animation linework placed into a real photographed room. "
    "The sundial garden is drawn like a frame of a hand-animated film: clean confident ink "
    "contours of varying weight, flat cel colours with a single soft shade step, loose "
    "watercolour washes filling the three arcs, a few pencil construction lines and tiny tick "
    "marks, slight paper grain only on the drawn object; the surrounding kitchen stays a real "
    "photograph with natural light and depth."
)

PROMPTS = {
    "sunrise": "A sunrise herb sprig. Sage and olive cel leaves, one warm shade step, a light ochre wash. Soil mound at the bottom centre.",
    "midday": "A midday coral plant. Coral stems, green leaves with a coral blush, one shade step, a light coral wash. Soil mound at the bottom centre.",
    "dusk": "A dusk lavender. Silver-green leafy spikes, a light mauve wash. Soil mound at the bottom centre. Flower spikes are the bloom overlay, not the foliage card.",
}


def load_rgb(path):
    im = Image.open(path).convert("RGB")
    return np.asarray(im)


def key_white(rgb):
    """Ink stays solid. Paper drops out. Wash keeps an alpha that follows how far it is from white."""
    rgb = rgb.astype(np.float32)
    r, g, b = rgb[:, :, 0], rgb[:, :, 1], rgb[:, :, 2]
    luma = 0.2126 * r + 0.7152 * g + 0.0722 * b
    chroma = np.maximum(np.maximum(r, g), b) - np.minimum(np.minimum(r, g), b)
    paper = ((luma > 242) & (chroma < 18)) | (luma > 250)
    ink = (luma < 92) | ((luma < 118) & (chroma < 32))
    far = np.maximum(np.maximum(255 - r, 255 - g), 255 - b)
    wash = np.clip(far * 3.1 + chroma * 1.4, 0, 255)
    wash = np.where((luma > 232) & (chroma < 20), 0, wash)
    alpha = np.where(paper, 0, np.where(ink, 255, wash)).astype(np.uint8)
    # Specks of paper grain.
    n, labels, stats, _ = cv2.connectedComponentsWithStats((alpha > 16).astype(np.uint8), 8)
    for i in range(1, n):
        if stats[i, cv2.CC_STAT_AREA] < 18:
            alpha[labels == i] = 0
    out = np.zeros((*alpha.shape, 4), np.uint8)
    out[:, :, :3] = np.clip(rgb, 0, 255).astype(np.uint8)
    out[:, :, 3] = alpha
    return out


def drop_paper_crust(rgba):
    """Pale low-chroma paper that survived the white key. On the soil it reads as frost or dead roots."""
    rgb = rgba[:, :, :3].astype(np.float32)
    r, g, b = rgb[:, :, 0], rgb[:, :, 1], rgb[:, :, 2]
    luma = 0.2126 * r + 0.7152 * g + 0.0722 * b
    chroma = np.maximum(np.maximum(r, g), b) - np.minimum(np.minimum(r, g), b)
    # Keep a one-pixel rim where ink meets paper. A wider pale field is spilled paper, not a highlight.
    # Beige paper (not a living green) as well as near-white. A green highlight stays.
    pale = (rgba[:, :, 3] > 8) & (luma > 176) & (chroma < 48) & (g + 4 <= r)
    # The mound's light skirt is the same beige, and a one-pixel ink rim turns it into a web.
    # Drop the whole skirt. The dark soil stays, and that is the contact the pivot measures.
    soil_rows = np.zeros(pale.shape[0], dtype=bool)
    ys = np.where(rgba[:, :, 3] > 24)[0]
    if ys.size and int(ys.max() - ys.min()) > 80:
        yb = int(ys.max())
        soil_rows[max(0, yb - 40) :] = True
        skirt = soil_rows[:, None] & (rgba[:, :, 3] > 8) & (luma > 150) & (chroma < 90) & (g + 2 <= r)
        pale = pale | skirt
    if not pale.any():
        return rgba
    ink = ((rgba[:, :, 3] > 160) & (luma < 96)).astype(np.uint8)
    rim = cv2.dilate(ink, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (3, 3)))
    dist = cv2.distanceTransform(pale.astype(np.uint8), cv2.DIST_L2, 3)
    keep = pale & (rim > 0) & (dist <= 1.15) & ~soil_rows[:, None]
    kill = pale & (~keep)
    if not kill.any():
        return rgba
    out = rgba.copy()
    out[kill, 3] = 0
    return out


def revive_sunrise(rgba):
    """Sunrise foliage came out brown-grey, which reads as a wilted plant. Sage stays living. Soil stays."""
    rgb = rgba[:, :, :3].astype(np.float32)
    alpha = rgba[:, :, 3]
    r, g, b = rgb[:, :, 0], rgb[:, :, 1], rgb[:, :, 2]
    luma = 0.2126 * r + 0.7152 * g + 0.0722 * b
    ink = (alpha > 150) & (luma < 96)
    ys = np.where(alpha > 24)[0]
    if ys.size == 0:
        return rgba
    yb = int(ys.max())
    soil_band = np.zeros(alpha.shape, dtype=bool)
    soil_band[max(0, yb - 46) :, :] = True
    brown = (r > g + 4) & (luma < 170)
    soil = soil_band & brown & (alpha > 24)
    # Only the leaf body. The pale arc wash stays ochre so the species hue check still holds.
    dead = (alpha > 80) & (luma > 78) & (luma < 158) & (~ink) & (~soil) & (g <= r + 6)
    if not dead.any():
        return rgba
    target = np.array([118.0, 152.0, 76.0], np.float32)
    shade = np.clip(luma[dead] / 148.0, 0.62, 1.12)[:, None]
    mixed = rgb[dead] * 0.18 + target * shade * 0.82
    out = rgba.copy()
    out[dead, :3] = np.clip(mixed, 0, 255).astype(np.uint8)
    return out


def finish_card(rgba, arc, revive):
    """Crust off, sunrise foliage back to a living sage, then seat the contact on the pivot."""
    card = drop_paper_crust(rgba)
    if revive and arc == "sunrise":
        card = revive_sunrise(card)
    card = extrude(snap_pivot(card))
    card = drop_paper_crust(card)
    return snap_pivot(card)


def drop_ochre_puddle(rgba):
    """A saturated ochre mass that does not touch a leaf is spilled wash, not the plant."""
    hsv = cv2.cvtColor(rgba[:, :, :3], cv2.COLOR_RGB2HSV)
    hue = hsv[:, :, 0].astype(np.float32) * 2.0
    sat = hsv[:, :, 1].astype(np.float32)
    luma = rgba[:, :, :3].astype(np.float32).mean(axis=2)
    alpha = rgba[:, :, 3]
    leaf = ((alpha > 40) & (hue > 55) & (hue < 150) & (sat > 18)).astype(np.uint8)
    away = cv2.distanceTransform((1 - leaf).astype(np.uint8), cv2.DIST_L2, 3)
    puddle = (alpha > 16) & (luma > 155) & (sat > 48) & (hue > 24) & (hue < 58) & (away > 10)
    if not puddle.any():
        return rgba
    out = rgba.copy()
    out[puddle, 3] = 0
    return out


def fade_distant_wash(rgba):
    """A puddle of wash that does not touch the drawing is paper, not the plant."""
    rgb = rgba[:, :, :3].astype(np.float32)
    luma = rgb.mean(axis=2)
    ink = (rgba[:, :, 3] > 170) & (luma < 115)
    if int(ink.sum()) < 8:
        return rgba
    dist = cv2.distanceTransform((~ink).astype(np.uint8), cv2.DIST_L2, 3)
    pale = (luma > 185) & (rgba[:, :, 3] > 0)
    out = rgba.copy()
    kill = pale & (dist > 18)
    out[kill, 3] = 0
    band = pale & (dist > 11) & (dist <= 18)
    if band.any():
        fade = np.clip((18 - dist[band]) / 7.0, 0, 1)
        out[band, 3] = (out[band, 3].astype(np.float32) * fade).astype(np.uint8)
    return out


def seat_floating(rgba):
    """If the drawing left the plant hovering over its soil, drop it onto the mound."""
    mask = (rgba[:, :, 3] > 28).astype(np.uint8)
    n, labels, stats, _ = cv2.connectedComponentsWithStats(mask, 8)
    comps = []
    for i in range(1, n):
        area = int(stats[i, cv2.CC_STAT_AREA])
        if area < 40:
            continue
        y = int(stats[i, cv2.CC_STAT_TOP])
        h = int(stats[i, cv2.CC_STAT_HEIGHT])
        comps.append((i, y, y + h, area))
    if len(comps) < 2:
        return rgba
    comps.sort(key=lambda c: -c[2])
    soil = comps[0]
    others = comps[1:]
    gap = soil[1] - max(c[2] for c in others)
    if gap < 28:
        return rgba
    shift = int(gap - 2)
    move = np.zeros(mask.shape, np.uint8)
    for c in others:
        move[labels == c[0]] = 1
    piece = rgba.copy()
    piece[move == 0] = 0
    out = rgba.copy()
    out[move == 1] = 0
    affine = np.float32([[1, 0, 0], [0, 1, shift]])
    shifted = cv2.warpAffine(
        piece, affine, (rgba.shape[1], rgba.shape[0]),
        flags=cv2.INTER_LINEAR, borderMode=cv2.BORDER_CONSTANT, borderValue=(0, 0, 0, 0),
    )
    m = shifted[:, :, 3] > 8
    out[m] = shifted[m]
    return out


def seat_detached(rgba):
    """A plant drawn above its mound, with paper specks on the soil line, still needs to sit down.

    Only the upper drawing moves. The mound stays, so the soil contact, and the pivot, stay put.
    """
    mask = (rgba[:, :, 3] > 28).astype(np.uint8)
    n, labels, stats, _ = cv2.connectedComponentsWithStats(mask, 8)
    comps = []
    for i in range(1, n):
        area = int(stats[i, cv2.CC_STAT_AREA])
        if area < 80:
            continue
        y = int(stats[i, cv2.CC_STAT_TOP])
        h = int(stats[i, cv2.CC_STAT_HEIGHT])
        comps.append((i, y, y + h, area))
    if len(comps) < 2:
        return rgba
    ymax = max(c[2] for c in comps)
    soil_cands = [c for c in comps if c[2] >= ymax - 12]
    soil = max(soil_cands, key=lambda c: c[3])
    above = [c for c in comps if c[0] != soil[0] and c[2] < soil[1] - 36]
    if not above:
        return rgba
    plant = max(above, key=lambda c: c[3])
    gap = soil[1] - plant[2]
    if gap < 36:
        return rgba
    shift = int(gap - 2)
    move_ids = []
    for c in comps:
        if c[0] == soil[0]:
            continue
        if c[2] <= plant[2] + 10 and c[1] + 10 >= plant[1]:
            move_ids.append(c[0])
    if not move_ids:
        return rgba
    move = np.isin(labels, np.array(move_ids, np.int32))
    move = cv2.dilate(move.astype(np.uint8), cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (5, 5)))
    move[soil[1] :, :] = 0
    if int(move.sum()) < 40:
        return rgba
    piece = rgba.copy()
    piece[move == 0] = 0
    out = rgba.copy()
    out[move == 1] = 0
    affine = np.float32([[1, 0, 0], [0, 1, shift]])
    shifted = cv2.warpAffine(
        piece, affine, (rgba.shape[1], rgba.shape[0]),
        flags=cv2.INTER_LINEAR, borderMode=cv2.BORDER_CONSTANT, borderValue=(0, 0, 0, 0),
    )
    m = shifted[:, :, 3] > 8
    out[m] = shifted[m]
    return out


def content_box(alpha, thresh=24):
    ys, xs = np.where(alpha > thresh)
    if len(ys) == 0:
        return None
    return int(xs.min()), int(ys.min()), int(xs.max()), int(ys.max())


def base_point(alpha, thresh=24):
    """Centre of the soil contact, not the median of a thick band (that drifts with the mound)."""
    ys, xs = np.where(alpha > thresh)
    if len(ys) == 0:
        return None
    yb = int(ys.max())
    for y in range(yb, max(-1, yb - 4), -1):
        cols = np.where(alpha[y] > thresh)[0]
        if len(cols) >= 3:
            return float(cols.min() + cols.max()) / 2.0, float(y)
    band = ys >= yb - 2
    return float(np.median(xs[band])), float(yb)


def snap_pivot(rgba):
    """Resampling moves the contact by a few pixels. Put it back on the pivot."""
    out = rgba
    for _ in range(3):
        base = base_point(out[:, :, 3])
        if base is None:
            return out
        dx = PIVOT_X - base[0]
        dy = PIVOT_Y - base[1]
        if abs(dx) < 0.6 and abs(dy) < 0.6:
            return out
        affine = np.float32([[1, 0, dx], [0, 1, dy]])
        out = cv2.warpAffine(
            out, affine, (W, H),
            flags=cv2.INTER_LINEAR, borderMode=cv2.BORDER_CONSTANT, borderValue=(0, 0, 0, 0),
        )
    return out


WASH_HUE = {"sunrise": 40.0, "midday": 16.0, "dusk": 275.0}


def grade_wash(rgba, arc):
    """Turn the pale wash toward the arc hue. Ink and the darker cel body stay put."""
    target = WASH_HUE[arc] / 2.0
    bgr = cv2.cvtColor(rgba[:, :, :3], cv2.COLOR_RGB2HSV).astype(np.float32)
    luma = rgba[:, :, :3].astype(np.float32).mean(axis=2)
    ink = (rgba[:, :, 3] > 160) & (luma < 112)
    grade = (rgba[:, :, 3] > 16) & (~ink) & (luma > 100)
    if not grade.any():
        return rgba
    strength = np.zeros(luma.shape, np.float32)
    # Pale wash adopts the arc hue. The darker cel body only leans toward it.
    strength[grade] = np.clip((luma[grade] - 115.0) / 45.0, 0.0, 1.0)
    hue = bgr[:, :, 0]
    hue_deg = hue * 2.0
    # Brown soil stays brown. The grade is for the leaf wash, not the mound.
    soil = (hue_deg > 8.0) & (hue_deg < 58.0) & (bgr[:, :, 1] < 110)
    grade = grade & (~soil)
    strength = np.where(grade, strength, 0)
    delta = (target - hue + 90.0) % 180.0 - 90.0
    bgr[:, :, 0] = np.where(grade, (hue + delta * strength) % 180.0, hue)
    bgr[:, :, 1] = np.where(grade, np.maximum(bgr[:, :, 1], 28.0 + 50.0 * strength), bgr[:, :, 1])
    rgb = cv2.cvtColor(np.clip(bgr, 0, 255).astype(np.uint8), cv2.COLOR_HSV2RGB)
    out = rgba.copy()
    out[grade, :3] = rgb[grade]
    return out


def thin_to(rgba, target):
    """Shave a thick outline down toward the species line weight. Never thicken."""
    current = ink_width(rgba)
    if target <= 0.4 or current <= target * 1.18:
        return rgba
    rgb = rgba[:, :, :3].astype(np.float32)
    luma = rgb.mean(axis=2)
    ink = ((rgba[:, :, 3] > 150) & (luma < 105)).astype(np.uint8)
    # Distance is at least ~1 on an edge pixel, so a sub-pixel threshold removes nothing.
    dist = cv2.distanceTransform(ink, cv2.DIST_L2, 3)
    shave = (ink > 0) & (dist <= 1.05)
    out = rgba.copy()
    out[shave, 3] = 0
    return out


def species_scale(rgba):
    """One scale for the species, taken from the full plant, so line weight does not jump."""
    alpha = rgba[:, :, 3]
    box = content_box(alpha)
    base = base_point(alpha)
    if box is None or base is None:
        raise SystemExit("full plant keyed to nothing")
    bx, by = base
    x0, y0, x1, _y1 = box
    height = max(1.0, by - y0)
    half = max(1.0, max(bx - x0, x1 - bx))
    max_h = PIVOT_Y - MARGIN
    max_w = (W / 2) - MARGIN
    return min(max_h / height, max_w / half)


def place(rgba, scale):
    base = base_point(rgba[:, :, 3])
    if base is None:
        return np.zeros((H, W, 4), np.uint8)
    bx, by = base
    affine = np.float32([
        [scale, 0, PIVOT_X - bx * scale],
        [0, scale, PIVOT_Y - by * scale],
    ])
    return cv2.warpAffine(
        rgba, affine, (W, H),
        flags=cv2.INTER_LINEAR, borderMode=cv2.BORDER_CONSTANT, borderValue=(0, 0, 0, 0),
    )


def extrude(rgba, radius=2):
    """Copy the edge colour under transparent texels so bilinear does not pull the paper."""
    opaque = (rgba[:, :, 3] > 12).astype(np.uint8)
    if int(opaque.sum()) == 0:
        return rgba
    kernel = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (radius * 2 + 1, radius * 2 + 1))
    near = cv2.dilate(opaque, kernel)
    hole = ((near > 0) & (opaque == 0)).astype(np.uint8) * 255
    if int(hole.max()) == 0:
        return rgba
    bgr = cv2.cvtColor(rgba[:, :, :3], cv2.COLOR_RGB2BGR)
    filled = cv2.inpaint(bgr, hole, radius, cv2.INPAINT_TELEA)
    rgb = cv2.cvtColor(filled, cv2.COLOR_BGR2RGB)
    out = rgba.copy()
    m = hole > 0
    out[m, :3] = rgb[m]
    return out


def blossom_pixels(arc, rgba):
    """Flower colour for this arc. OpenCV hue is doubled into degrees."""
    hsv = cv2.cvtColor(rgba[:, :, :3], cv2.COLOR_RGB2HSV)
    hue = hsv[:, :, 0].astype(np.float32) * 2.0
    sat = hsv[:, :, 1]
    val = hsv[:, :, 2]
    a = rgba[:, :, 3]
    if arc == "sunrise":
        band = (hue > 22) & (hue < 58)
    elif arc == "midday":
        band = (hue < 24) | (hue > 345)
    else:
        band = (hue > 200) & (hue < 340)
    sat_min = 18 if arc == "dusk" else 36
    return (a > 30) & (sat > sat_min) & (val > 80) & band


def midday_flowers(rgba, kind):
    """Buds and open heads only. The coral body is the same hue as the blossom, so hue alone keeps the plant."""
    hsv = cv2.cvtColor(rgba[:, :, :3], cv2.COLOR_RGB2HSV)
    hue = hsv[:, :, 0].astype(np.float32) * 2.0
    sat = hsv[:, :, 1].astype(np.float32)
    val = hsv[:, :, 2].astype(np.float32)
    red = ((rgba[:, :, 3] > 40) & ((hue < 20) | (hue > 348)) & (sat > 70) & (val > 100)).astype(np.uint8)
    if int(red.sum()) < 8:
        return np.zeros_like(rgba)
    dist = cv2.distanceTransform(red, cv2.DIST_L2, 5)
    thr = 4.6 if kind == "bud" else 6.4
    rad = 11 if kind == "bud" else 16
    local = cv2.dilate(dist, np.ones((9, 9), np.uint8))
    peaks = (dist >= local - 1e-3) & (dist >= thr) & (red > 0)
    peaks = peaks.copy()
    peaks[PIVOT_Y - 36:, :] = False
    grown = cv2.dilate(peaks.astype(np.uint8), cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (rad, rad)))
    kept = ((grown > 0) & (red > 0)).astype(np.uint8)
    n, labels, stats, _ = cv2.connectedComponentsWithStats(kept, 8)
    clean = np.zeros(kept.shape, np.uint8)
    for i in range(1, n):
        area = int(stats[i, cv2.CC_STAT_AREA])
        top = int(stats[i, cv2.CC_STAT_TOP])
        height = int(stats[i, cv2.CC_STAT_HEIGHT])
        if area < 18 or top + height > PIVOT_Y - 30:
            continue
        if kind == "bud":
            comp = (labels == i).astype(np.uint8)
            cnts, _ = cv2.findContours(comp, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
            if not cnts:
                continue
            contour = max(cnts, key=cv2.contourArea)
            peri = cv2.arcLength(contour, True)
            circ = 0.0 if peri == 0 else 4.0 * math.pi * float(cv2.contourArea(contour)) / (peri * peri)
            if circ < 0.70:
                continue
        clean[labels == i] = 255
    out = np.zeros_like(rgba)
    out[:, :, :3] = rgba[:, :, :3]
    out[:, :, 3] = np.where(clean > 0, rgba[:, :, 3], 0)
    return out


def flower_mask(full, bloom, arc):
    """Keep the blossom, not a second copy of the stem."""
    af = full[:, :, 3]
    ab = bloom[:, :, 3]
    dist = np.abs(full[:, :, :3].astype(np.int16) - bloom[:, :, :3].astype(np.int16)).sum(axis=2)
    flowers = blossom_pixels(arc, bloom)
    keep = flowers & ((af < 28) | (dist > 55))
    mask = keep.astype(np.uint8) * 255
    mask = cv2.morphologyEx(mask, cv2.MORPH_CLOSE, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (5, 5)))
    mask = cv2.morphologyEx(mask, cv2.MORPH_OPEN, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (3, 3)))
    n, labels, stats, _ = cv2.connectedComponentsWithStats((mask > 0).astype(np.uint8), 8)
    cleaned = np.zeros(mask.shape, np.uint8)
    for i in range(1, n):
        area = int(stats[i, cv2.CC_STAT_AREA])
        top = int(stats[i, cv2.CC_STAT_TOP])
        if area < 10:
            continue
        if top > PIVOT_Y - 28:
            continue
        cleaned[labels == i] = 255
    out = np.zeros_like(bloom)
    out[:, :, :3] = bloom[:, :, :3]
    out[:, :, 3] = np.where(cleaned > 0, ab, 0)
    return out


def prepare(path):
    rgb = load_rgb(path)
    rgba = key_white(rgb)
    rgba = fade_distant_wash(rgba)
    rgba = drop_paper_crust(rgba)
    rgba = seat_floating(rgba)
    rgba = seat_detached(rgba)
    return rgba


def ink_width(rgba):
    """Mean contour width above the soil. Filled bodies (a seed's interior) are not a stroke."""
    rgb = rgba[:, :, :3].astype(np.float32)
    luma = rgb.mean(axis=2)
    # 130, not 105: the midday young stem is a light coral line and the stricter cut counted nothing.
    ink = ((rgba[:, :, 3] > 120) & (luma < 130)).astype(np.uint8)
    ink[PIVOT_Y - 18:, :] = 0
    if int(ink.sum()) < 12:
        return 0.0
    dist = cv2.distanceTransform(ink, cv2.DIST_L2, 3)
    # A stroke's medial distance stays under ~2 px. Deeper than that is a filled shape.
    band = (ink > 0) & (dist >= 0.7) & (dist <= 2.15)
    if int(band.sum()) < 6:
        band = (ink > 0) & (dist > 0.4)
    if int(band.sum()) < 6:
        return 0.0
    return float(np.median(dist[band]) * 2.0)


def wash_stats(rgba, target_deg):
    """Mean hue of the wash: non-ink pixels already near the arc wash, which is what the grade aims at."""
    rgb = rgba[:, :, :3]
    hsv = cv2.cvtColor(rgb, cv2.COLOR_RGB2HSV)
    hue = hsv[:, :, 0].astype(np.float32) * 2.0
    sat = hsv[:, :, 1].astype(np.float32)
    luma = rgb.astype(np.float32).mean(axis=2)
    ink = (rgba[:, :, 3] > 150) & (luma < 105)
    delta = np.abs((hue - target_deg + 180.0) % 360.0 - 180.0)
    wash = (rgba[:, :, 3] > 30) & (~ink) & (sat > 15) & (delta <= 22.0)
    if int(wash.sum()) < 8:
        return None, None, 0
    ang = np.deg2rad(hue[wash])
    mean = math.degrees(math.atan2(float(np.sin(ang).mean()), float(np.cos(ang).mean()))) % 360.0
    if mean < 0:
        mean += 360.0
    return mean, float(sat[wash].mean()) * (100.0 / 255.0), int(wash.sum())


def circ_dist(a, b):
    d = abs(a - b) % 360.0
    return min(d, 360.0 - d)


def measure(rgba, arc):
    hue, sat, count = wash_stats(rgba, WASH_HUE[arc])
    width = ink_width(rgba)
    base = base_point(rgba[:, :, 3])
    if base is None:
        return {"hue": hue, "sat": sat, "wash_n": count, "width": width, "pivot": 999.0, "base": None}
    dx = base[0] - PIVOT_X
    dy = base[1] - PIVOT_Y
    return {
        "hue": hue,
        "sat": sat,
        "wash_n": count,
        "width": width,
        "pivot": math.hypot(dx, dy),
        "base": base,
    }


def save_png(path, rgba):
    path.parent.mkdir(parents=True, exist_ok=True)
    Image.fromarray(rgba, "RGBA").save(path)


def write_prompts():
    note = (
        "\n\nFraming: single plant, centred, base at the bottom centre, on plain white paper, "
        "no shadow, no text.\n"
        "Negative: 3D render, CGI, plastic, photorealistic plants, chibi characters, faces, "
        "speech bubbles, manga text, screen tones covering the room.\n"
        "The model call led with the white-paper framing. Putting the style sentence first "
        "made the model photograph the plant on a sheet in a kitchen; those plates were rejected.\n"
    )
    stage_line = {
        "seed": "Stage seed: one seed on a small mound.",
        "sprout": "Stage sprout: two leaves on a short stem.",
        "young": "Stage young: about one third of the mature height.",
        "leafy": "Stage leafy: about two thirds of the mature height.",
        "full": "Stage full: the mature foliage, no open bloom.",
        "bud": "Bloom bud: the same plant with closed buds. The keyed card keeps only the buds.",
        "open": "Bloom open: the same plant in flower. The keyed card keeps only the blossom.",
    }
    for arc, files in SOURCES.items():
        for name in list(STAGES) + list(BLOOMS):
            text = STYLE + "\n\n" + PROMPTS[arc] + " " + stage_line[name] + note
            (SRC / ("src_%s_%s.prompt.txt" % (arc, name))).write_text(text, encoding="utf-8")


def process():
    write_prompts()
    placed = {}
    for arc in ARCS:
        raw = {}
        for name, file in SOURCES[arc].items():
            path = CAND / file
            if not path.exists():
                raise SystemExit("missing source %s" % path)
            raw[name] = prepare(path)
            if arc == "sunrise":
                raw[name] = drop_ochre_puddle(raw[name])
            save_png(SRC / ("src_%s_%s.png" % (arc, name)), raw[name])
        scale = species_scale(raw["full"])
        print("scale %s %.3f" % (arc, scale))
        cards = {}
        for stage in STAGES:
            graded = grade_wash(place(raw[stage], scale), arc)
            cards[stage] = finish_card(graded, arc, True)
            save_png(TEX / ("plant_%s_%s.png" % (arc, stage)), cards[stage])
        full = cards["full"]
        for bloom in BLOOMS:
            if arc == "midday":
                seated = place(raw[bloom], scale)
                over = finish_card(grade_wash(midday_flowers(seated, bloom), arc), arc, False)
            else:
                seated = grade_wash(place(raw[bloom], scale), arc)
                over = finish_card(flower_mask(full, seated, arc), arc, False)
            cards[bloom] = over
            save_png(TEX / ("bloom_%s_%s.png" % (arc, bloom)), over)
            cover = float((over[:, :, 3] > 24).mean())
            print("  bloom %s %s alpha cover %.3f" % (arc, bloom, cover))
        placed[arc] = cards
    run = Path(os.environ["GARDEN_RUN_DIR"]) if os.environ.get("GARDEN_RUN_DIR") else RUN
    contact(placed, run / "plants-contact.png")
    text = report_from_memory(placed)
    print(text)
    return text


def contact(placed, path):
    cell_w, cell_h = 128, 256
    cols = list(STAGES) + list(BLOOMS)
    sheet = Image.new("RGBA", (len(cols) * cell_w, len(ARCS) * cell_h + 28), (243, 238, 226, 255))
    draw = ImageDraw.Draw(sheet)
    try:
        font = ImageFont.truetype("C:/Windows/Fonts/segoeui.ttf", 16)
    except OSError:
        font = ImageFont.load_default()
    for c, name in enumerate(cols):
        draw.text((c * cell_w + 8, 4), name, fill=(42, 38, 34, 255), font=font)
    for r, arc in enumerate(ARCS):
        for c, name in enumerate(cols):
            card = Image.fromarray(placed[arc][name], "RGBA")
            card.thumbnail((cell_w - 8, cell_h - 8), Image.Resampling.LANCZOS)
            x = c * cell_w + (cell_w - card.width) // 2
            y = 28 + r * cell_h + (cell_h - card.height) // 2
            sheet.paste(card, (x, y), card)
    path.parent.mkdir(parents=True, exist_ok=True)
    sheet.convert("RGB").save(path)


def report_from_memory(placed):
    rows = []
    for arc in ARCS:
        for name in list(STAGES) + list(BLOOMS):
            m = measure(placed[arc][name], arc)
            m["arc"] = arc
            m["name"] = name
            m["kind"] = "stage" if name in STAGES else "bloom"
            rows.append(m)
    return format_report(rows)


def report_from_files():
    rows = []
    for arc in ARCS:
        for stage in STAGES:
            path = TEX / ("plant_%s_%s.png" % (arc, stage))
            rgba = np.asarray(Image.open(path).convert("RGBA"))
            m = measure(rgba, arc)
            m.update(arc=arc, name=stage, kind="stage")
            rows.append(m)
        for bloom in BLOOMS:
            path = TEX / ("bloom_%s_%s.png" % (arc, bloom))
            rgba = np.asarray(Image.open(path).convert("RGBA"))
            m = measure(rgba, arc)
            m.update(arc=arc, name=bloom, kind="bloom")
            rows.append(m)
    text = format_report(rows)
    print(text)
    failed = "FAIL" in text.split("SPECIES")[-1] if "SPECIES" in text else "FAIL" in text
    # format_report ends with a verdict line. Exit non-zero when a species fails.
    if text.strip().endswith("FAIL"):
        sys.exit(1)


def format_report(rows):
    lines = []
    lines.append("plant card coherence")
    lines.append("canvas %d x %d  pivot (%d, %d) image-y down" % (W, H, PIVOT_X, PIVOT_Y))
    lines.append("tolerance: wash hue within 8 deg of the species median, ink width within 25% of the median, base within 2 px")
    lines.append("bloom rows are overlays. Their content sits on the flower, so the pivot check is the stage cards.")
    lines.append("")
    lines.append("%-8s %-7s %7s %7s %7s %7s %6s" % ("arc", "card", "hue", "sat", "width", "pivot", "wash_n"))
    by = {}
    for row in rows:
        by.setdefault(row["arc"], []).append(row)
        hue = "-" if row["hue"] is None else "%7.1f" % row["hue"]
        sat = "-" if row["sat"] is None else "%7.1f" % row["sat"]
        lines.append("%-8s %-7s %7s %7s %7.2f %7.2f %6d" % (
            row["arc"], row["name"], hue.strip() if isinstance(hue, str) else hue,
            sat, row["width"], row["pivot"], row["wash_n"],
        ))
    lines.append("")
    verdict = "PASS"
    for arc, group in by.items():
        stages = [r for r in group if r["kind"] == "stage" and r["hue"] is not None]
        hues = [r["hue"] for r in stages]
        widths = [r["width"] for r in stages if r["width"] > 0.2]
        pivots = [r["pivot"] for r in stages]
        # Circular median via the mean, then the max distance. A tight set's mean is the centre.
        ang = np.deg2rad(np.array(hues))
        centre = math.degrees(math.atan2(float(np.sin(ang).mean()), float(np.cos(ang).mean()))) % 360.0
        if centre < 0:
            centre += 360.0
        hue_span = max(circ_dist(h, centre) for h in hues) if hues else 999
        med_w = float(np.median(widths)) if widths else 0
        width_err = max(abs(w - med_w) / med_w for w in widths) if med_w else 999
        pivot_max = max(pivots) if pivots else 999
        ok = hue_span <= 8.0 and width_err <= 0.25 and pivot_max <= 2.0
        if not ok:
            verdict = "FAIL"
        lines.append(
            "SPECIES %-8s hue_centre %6.1f hue_span %5.1f deg  width_med %4.2f width_err %5.1f%%  pivot_max %4.2f px  %s"
            % (arc, centre, hue_span, med_w, width_err * 100.0, pivot_max, "PASS" if ok else "FAIL")
        )
    lines.append(verdict)
    return "\n".join(lines) + "\n"


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--report-only", action="store_true")
    args = parser.parse_args()
    if args.report_only:
        report_from_files()
    else:
        text = process()
        if text.strip().endswith("FAIL"):
            sys.exit(1)


if __name__ == "__main__":
    main()
