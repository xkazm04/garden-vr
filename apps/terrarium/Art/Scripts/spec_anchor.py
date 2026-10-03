"""Gate A2 spec anchor for the terrarium JarG1 frame.

Reads a CaptureCli.Shot PNG, the room plate, and the night-moss reference.
Writes measured palette delta-E, ring line width, glow falloff, and object
sizes against docs/art/terrarium-style.md section 6.

The capture package Measure entry counts draws and overdraw (gate A5). It does
not emit these quantities, so this script measures the shot that entry's
framing produced.

    python apps/terrarium/Art/Scripts/spec_anchor.py <run-dir>
    python apps/terrarium/Art/Scripts/spec_anchor.py --self-test
"""
from __future__ import annotations

import json
import math
import os
import sys

import numpy as np
from PIL import Image, ImageDraw

W = 1824
H = 1024
EYE = np.array([0.0, 0.175, -0.44])
LOOK = np.array([0.0, 0.072, 0.0])
VFOV = 28.2
# JarG1 lensShift is (0, -0.4). CaptureCli applies Euler(-y, x, 0) = Euler(0.4, 0, 0).
# Unity pitch is left-handed, so a positive X rotation is the opposite of a
# right-handed Rx. That sign puts the jar base on pixel (912, 832), beside the
# bible's (915, 830).
PITCH_DEG = 0.4

# Section 6 targets, centimetres, and the section 1 region hexes.
JAR_H_CM = 14.0
JAR_D_CM = 8.7
CORK_H_CM = 1.5
RING_CM = 6.0
COIL_CM = 1.6
# Authored ring card is 0.152 m. The stroke radius is measured from ring.png.
RING_CARD_M = 0.152

# Glow floor and ceiling, T-TER-032. Relative luminance (IEC sRGB to CIE Y, 0..1)
# on the 1824x1024 JarG1 plate. The reference is A1-03-night-moss-1.png.
# Crozier is the spiral head. Glass is the A2 empty-upper-glass rect. The ring
# rect is the desk band the reference stroke crosses (near y=881).
# Floor sits above the T-TER-030 undershoot (glass mean 0.105, ring peak 0.494).
# Ceiling sits under the T-TER-019 / T-TER-028 mint wash (glass mean 0.424).
# The crozier peak is already clipped near 1 on both the reference and T-TER-030.
GLOW_CROZIER = (860, 420, 100, 100)
GLOW_GLASS = (760, 368, 28, 36)
GLOW_RING = (700, 850, 420, 140)
GLOW_FLOOR = {"crozierPeak": 0.70, "glassMean": 0.145, "ringPeak": 0.60}
GLOW_CEILING = {"crozierPeak": 1.0, "glassMean": 0.280, "ringPeak": 1.0}


def ciede2000(lab1, lab2):
    """CIEDE2000. Sharma pair (50, 2.6772, -79.7751) vs (50, 0, -82.7485) is 2.0425."""
    L1, a1, b1 = lab1
    L2, a2, b2 = lab2
    C1 = math.hypot(a1, b1)
    C2 = math.hypot(a2, b2)
    Cbar = (C1 + C2) / 2.0
    Cbar7 = Cbar ** 7
    G = 0.5 * (1.0 - math.sqrt(Cbar7 / (Cbar7 + 25.0 ** 7)))
    a1p = (1.0 + G) * a1
    a2p = (1.0 + G) * a2
    C1p = math.hypot(a1p, b1)
    C2p = math.hypot(a2p, b2)

    def hue(a, b):
        if a == 0.0 and b == 0.0:
            return 0.0
        return math.atan2(b, a) % (2.0 * math.pi)

    h1p = hue(a1p, b1)
    h2p = hue(a2p, b2)
    dLp = L2 - L1
    dCp = C2p - C1p
    dhp = h2p - h1p
    if C1p * C2p == 0.0:
        dhp = 0.0
    elif dhp > math.pi:
        dhp -= 2.0 * math.pi
    elif dhp < -math.pi:
        dhp += 2.0 * math.pi
    dHp = 2.0 * math.sqrt(C1p * C2p) * math.sin(dhp / 2.0)
    Lbar = (L1 + L2) / 2.0
    Cbarp = (C1p + C2p) / 2.0
    if C1p * C2p == 0.0:
        hbar = h1p + h2p
    else:
        hsum = h1p + h2p
        if abs(h1p - h2p) > math.pi:
            hbar = (hsum + 2.0 * math.pi) / 2.0 if hsum < 2.0 * math.pi else (hsum - 2.0 * math.pi) / 2.0
        else:
            hbar = hsum / 2.0
    T = (
        1.0
        - 0.17 * math.cos(hbar - math.radians(30.0))
        + 0.24 * math.cos(2.0 * hbar)
        + 0.32 * math.cos(3.0 * hbar + math.radians(6.0))
        - 0.20 * math.cos(4.0 * hbar - math.radians(63.0))
    )
    dTheta = math.radians(30.0) * math.exp(-(((math.degrees(hbar) - 275.0) / 25.0) ** 2))
    Cbarp7 = Cbarp ** 7
    Rc = 2.0 * math.sqrt(Cbarp7 / (Cbarp7 + 25.0 ** 7))
    Sl = 1.0 + (0.015 * (Lbar - 50.0) ** 2) / math.sqrt(20.0 + (Lbar - 50.0) ** 2)
    Sc = 1.0 + 0.045 * Cbarp
    Sh = 1.0 + 0.015 * Cbarp * T
    Rt = -math.sin(2.0 * dTheta) * Rc
    return math.sqrt(
        (dLp / Sl) ** 2
        + (dCp / Sc) ** 2
        + (dHp / Sh) ** 2
        + Rt * (dCp / Sc) * (dHp / Sh)
    )


def srgb_to_lab(rgb):
    """rgb is 0..255. D65, IEC sRGB."""
    c = np.asarray(rgb, dtype=np.float64) / 255.0
    lin = np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)
    M = np.array(
        [
            [0.4124564, 0.3575761, 0.1804375],
            [0.2126729, 0.7151522, 0.0721750],
            [0.0193339, 0.1191920, 0.9503041],
        ]
    )
    xyz = lin @ M.T
    white = np.array([0.95047, 1.0, 1.08883])
    t = xyz / white
    f = np.where(t > 0.008856, np.cbrt(t), 7.787 * t + 16.0 / 116.0)
    L = 116.0 * f[..., 1] - 16.0
    a = 500.0 * (f[..., 0] - f[..., 1])
    b = 200.0 * (f[..., 1] - f[..., 2])
    return np.stack([L, a, b], axis=-1)


def hex_rgb(text):
    text = text.lstrip("#")
    return np.array([int(text[0:2], 16), int(text[2:4], 16), int(text[4:6], 16)], dtype=np.float64)


def camera_axes():
    forward = LOOK - EYE
    z = forward / np.linalg.norm(forward)
    up = np.array([0.0, 1.0, 0.0])
    x = np.cross(up, z)
    x = x / np.linalg.norm(x)
    y = np.cross(z, x)
    look = np.column_stack([x, y, z])
    # Left-handed pitch: positive degrees looks the base down to y=832.
    t = math.radians(-PITCH_DEG)
    c, s = math.cos(t), math.sin(t)
    rx = np.array([[1.0, 0.0, 0.0], [0.0, c, -s], [0.0, s, c]])
    return look @ rx


ROT = camera_axes()
TAN_Y = math.tan(math.radians(VFOV) / 2.0)
TAN_X = TAN_Y * (W / float(H))


def project(point):
    cam = ROT.T @ (np.asarray(point, dtype=float) - EYE)
    if cam[2] <= 1e-6:
        return None
    x_ndc = cam[0] / (cam[2] * TAN_X)
    y_ndc = cam[1] / (cam[2] * TAN_Y)
    return (x_ndc + 1.0) * 0.5 * W, (1.0 - y_ndc) * 0.5 * H


def ray(px, py):
    """World ray origin and direction through a pixel. Y is down in the image."""
    x_ndc = (px + 0.5) / W * 2.0 - 1.0
    y_ndc = 1.0 - (py + 0.5) / H * 2.0
    direction = ROT @ np.array([x_ndc * TAN_X, y_ndc * TAN_Y, 1.0])
    return EYE, direction / np.linalg.norm(direction)


def hit_plane(px, py, axis, value):
    origin, direction = ray(px, py)
    if abs(direction[axis]) < 1e-8:
        return None
    t = (value - origin[axis]) / direction[axis]
    if t <= 0:
        return None
    return origin + direction * t


def load_rgb(path):
    return np.asarray(Image.open(path).convert("RGB"), dtype=np.float32)


def mean_delta(pixels, target_lab):
    if len(pixels) == 0:
        return None
    labs = srgb_to_lab(pixels.reshape(-1, 3)).reshape(-1, 3)
    total = 0.0
    for lab in labs:
        total += ciede2000(lab, target_lab)
    return total / len(labs)


def window_pixels(image, rect):
    x, y, w, h = rect
    x = int(max(0, x))
    y = int(max(0, y))
    w = int(min(w, image.shape[1] - x))
    h = int(min(h, image.shape[0] - y))
    if w < 2 or h < 2:
        return np.zeros((0, 3)), (x, y, w, h)
    return image[y : y + h, x : x + w].reshape(-1, 3), (x, y, w, h)


def ring_texture_peak(path):
    """Radius of the R-channel ridge as a fraction of the card half-extent."""
    im = np.asarray(Image.open(path).convert("RGB"), dtype=np.float32)
    red = im[:, :, 0]
    h, w = red.shape
    cy = (h - 1) / 2.0
    cx = (w - 1) / 2.0
    yy, xx = np.mgrid[0:h, 0:w]
    rad = np.hypot((xx - cx) / (w / 2.0), (yy - cy) / (h / 2.0))
    # Bin the mean red by radius. The stroke is a thin ridge, not the pool.
    bins = np.linspace(0.0, 1.0, 201)
    centers = 0.5 * (bins[:-1] + bins[1:])
    means = []
    for i in range(len(centers)):
        mask = (rad >= bins[i]) & (rad < bins[i + 1])
        means.append(float(red[mask].mean()) if np.any(mask) else 0.0)
    means = np.array(means)
    peak = int(np.argmax(means))
    half = means[peak] * 0.5
    lo = peak
    while lo > 0 and means[lo] >= half:
        lo -= 1
    hi = peak
    while hi < len(means) - 1 and means[hi] >= half:
        hi += 1
    return {
        "peakFractionOfHalfExtent": round(float(centers[peak]), 4),
        "fwhmFractionOfHalfExtent": round(float(centers[hi] - centers[lo]), 4),
        "peakRadiusCm": round(float(centers[peak]) * (RING_CARD_M / 2.0) * 100.0, 3),
        "lineWidthCm": round(float(centers[hi] - centers[lo]) * (RING_CARD_M / 2.0) * 100.0, 3),
    }


def solid_mask(render, plate, thresh):
    delta = np.max(np.abs(render - plate), axis=2)
    return delta > thresh


def world_y(px, py, z=0.0):
    """World Y where the pixel ray meets the plane Z = z. The jar axis is Z = 0."""
    hit = hit_plane(px, py, 2, z)
    if hit is None:
        raise SystemExit("pixel (%s, %s) misses Z=%s" % (px, py, z))
    return float(hit[1])


def px_per_cm(world_y_m):
    """Horizontal pixels per centimetre at a point on the jar axis."""
    origin = project((0.0, world_y_m, 0.0))
    plus = project((0.01, world_y_m, 0.0))
    return abs(plus[0] - origin[0])


def brown_span(render, y, x0=700, x1=1150):
    band = render[y, x0:x1]
    red, green, blue = band[:, 0], band[:, 1], band[:, 2]
    # Tan is the locked stopper. Dark is the s1 reference cork (red still leads, but under 80).
    tan = (red > green + 8) & (red > blue + 8) & (red > 90)
    dark = (red > green + 4) & (red > blue + 4) & (red > 12) & (red < 80) & (green < 50)
    xs = np.where(tan | dark)[0]
    if len(xs) == 0:
        return 0, None, None
    return int(len(xs)), int(x0 + xs.min()), int(x0 + xs.max())


def center_is_cork(render, y):
    """Stopper centre, including the pale lip. Green glass under it fails this."""
    mean = render[y, 880:960].mean(axis=0)
    tan = mean[0] > 140 and mean[0] + 12 > mean[1] and mean[0] > mean[2] + 15
    dark = 12 < mean[0] < 80 and mean[0] > mean[1] + 3 and mean[0] > mean[2] + 3 and mean[1] < 50
    return bool(tan or dark)


def measure_dimensions(render, plate):
    """World sizes at JarG1. Pixel spans are not scaled by the on-axis 14 cm line.

    The cork top row is as wide as the cap, so its centre unprojects at Z = 0.
    The stopper continues through the pale lip highlight; the bottom is the last
    cork-brown centre row before the glass turns green. The heel is the first
    bright glass-foot row on the axis, which lands on the projected origin.
    The foot highlight below that is glass in front of the desk, not the base.
    """
    base = project((0.0, 0.0, 0.0))
    axis_x = int(round(base[0]))
    cork_top = None
    for y in range(160, 240):
        count, x0, x1 = brown_span(render, y)
        if count >= 150 and x0 is not None and (x1 - x0) >= 180:
            mid = (x0 + x1) / 2.0
            if abs(mid - axis_x) < 40:
                cork_top = y
                break
    if cork_top is None:
        raise SystemExit("cork cap not found")
    # Last centre row that is still the stopper. One pale lip row in the middle
    # is a highlight, so a single green row does not end the cork.
    cork_bot = cork_top
    gap = 0
    lip_y = None
    lip_gap = 999.0
    for y in range(cork_top, 430):
        if center_is_cork(render, y):
            cork_bot = y
            gap = 0
            mean = render[y, 880:960].mean(axis=0)
            redness = float(mean[0] - mean[1])
            if y > cork_top + 20 and redness < lip_gap:
                lip_gap = redness
                lip_y = y
        else:
            gap += 1
            if gap >= 3 and y > cork_top + 30:
                break
    heel = None
    for y in range(780, 900):
        colour = render[y, axis_x]
        added = float(colour[1] - plate[y, axis_x, 1])
        if colour[1] > 160 and added > 40:
            heel = y
            break
    if heel is None:
        raise SystemExit("glass heel not found")
    top_y = world_y(axis_x, cork_top)
    bot_y = world_y(axis_x, cork_bot)
    heel_y = world_y(axis_x, heel)
    height_cm = (top_y - heel_y) * 100.0
    cork_cm = (top_y - bot_y) * 100.0
    lip_cm = None
    if lip_y is not None:
        lip_cm = (top_y - world_y(axis_x, lip_y)) * 100.0
    height_px = heel - cork_top
    # Bible quotes about 635 px for 14 cm. That is a note, not the conversion.
    target_height_px = base[1] - project((0.0, JAR_H_CM / 100.0, 0.0))[1]

    # Diameter: the glass cylinder. A frond crossing the silhouette, and the
    # foot flare, are wider than the cylinder, so they are not the median.
    mask = solid_mask(render, plate, 28.0)
    spans = []
    for y in range(cork_bot + 20, heel - 50):
        xs = np.where(mask[y, 640:1250])[0]
        if len(xs) < 80:
            continue
        left_i = int(xs.min())
        right_i = int(xs.max())
        run = mask[y, 640 + left_i : 640 + right_i + 1]
        if float(run.mean()) < 0.97:
            continue
        spans.append((right_i - left_i, y, 640 + left_i, 640 + right_i))
    if len(spans) < 20:
        raise SystemExit("jar diameter rows not found")
    narrow = sorted(spans)[: max(8, len(spans) // 3)]
    ref_span = narrow[len(narrow) // 2][0]
    core = [row for row in spans if row[0] <= ref_span * 1.04]
    core.sort()
    median = core[len(core) // 2]
    diameter_px, mid_y, left_px, right_px = median
    row_y = world_y(axis_x, mid_y)
    diameter_cm = diameter_px / px_per_cm(row_y)
    target_diameter_px = abs(
        project((JAR_D_CM / 200.0, row_y, 0.0))[0] - project((-JAR_D_CM / 200.0, row_y, 0.0))[0]
    )
    return {
        "method": "unproject the cap centre and the glass heel at Z=0; diameter is the median cylinder row, with frond bulges and the foot flare left out",
        "projectedBasePx": [round(base[0], 1), round(base[1], 1)],
        "corkTopPx": cork_top,
        "corkBottomPx": cork_bot,
        "corkLipPx": lip_y,
        "heelPx": heel,
        "leftPx": int(left_px),
        "rightPx": int(right_px),
        "midY": int(mid_y),
        "worldTopM": round(top_y, 4),
        "worldHeelM": round(heel_y, 4),
        "worldCorkBottomM": round(bot_y, 4),
        "jarHeight": quantity(height_cm, JAR_H_CM, 0.05, height_px, target_height_px),
        "jarDiameter": quantity(diameter_cm, JAR_D_CM, 0.05, diameter_px, target_diameter_px),
        "corkHeight": quantity(cork_cm, CORK_H_CM, 0.10, cork_bot - cork_top, None),
        "corkUpperTierCm": None if lip_cm is None else round(lip_cm, 3),
        "corkNote": "The pale row inside the stopper is a lip highlight. The gate height is the full cork-brown stack. The upper tier, down to that highlight, is corkUpperTierCm and also misses 1.5 cm.",
        "centerX": axis_x,
        "mask": mask,
    }


def quantity(measured, target, tol, px, target_px):
    err = abs(measured - target) / target
    return {
        "measuredCm": round(float(measured), 3),
        "targetCm": target,
        "tolerance": tol,
        "measuredPx": None if px is None else round(float(px), 2),
        "targetPx": None if target_px is None else round(float(target_px), 2),
        "pass": bool(err <= tol + 1e-9),
    }


def added_green(render, plate):
    return render[:, :, 1] - plate[:, :, 1]


def thin_ridge(column, y_hint, rad=14):
    """A thin added-green spike near y_hint. A broad glass foot loses to it."""
    best = None
    y0 = max(4, y_hint - rad)
    y1 = min(len(column) - 5, y_hint + rad)
    for y in range(y0, y1 + 1):
        value = float(column[y])
        if value < 12:
            continue
        if value < float(column[y - 1]) or value < float(column[y + 1]):
            continue
        side = max(float(column[y - 4]), float(column[y + 4]))
        contrast = value - side
        if contrast < 10:
            continue
        score = contrast - abs(y - y_hint) * 1.5
        if best is None or score > best[0]:
            best = (score, y, value, contrast)
    return best


def desk_radius_cm(x, y):
    hit = hit_plane(x, y, 1, 0.0006)
    if hit is None:
        return None
    return float(math.hypot(hit[0], hit[2]) * 100.0)


def falloff_px(column, y_peak, step, fraction):
    peak = float(column[y_peak])
    y = y_peak
    while 1 < y < len(column) - 2:
        y += step
        if float(column[y]) <= peak * fraction:
            return abs(y - y_peak)
    return None


def width_cm(x, y_peak, column):
    """FWHM of added green, in centimetres on the desk plane."""
    peak = float(column[y_peak])
    half = peak * 0.5
    lo = y_peak
    while lo > 1 and float(column[lo]) >= half:
        lo -= 1
    hi = y_peak
    while hi < len(column) - 2 and float(column[hi]) >= half:
        hi += 1
    inner = desk_radius_cm(x, lo)
    outer = desk_radius_cm(x, hi)
    if inner is None or outer is None:
        return None, hi - lo
    return abs(outer - inner), hi - lo


def measure_ring(render, plate, texture_info):
    """Follow the thin added-green ridge on the desk. Do not jump to the glass foot.

    The ridge is an ellipse. The front radius and the two side radii are reported
    separately. The gate number is the largest of them: a pass on the front does
    not hide a side that is outside 6 cm plus or minus 8 percent.
    Line width and glow falloff have no numeric tolerance in section 6.
    """
    green = added_green(render, plate)
    axis_x = int(round(project((0.0, 0.0, 0.0))[0]))
    front = None
    for y in range(860, H - 6):
        value = float(green[y, axis_x])
        if value > 40 and (front is None or value > front[0]):
            # Keep the thin spike, not the broad foot higher up.
            if value > float(green[y - 4, axis_x]) + 20 and value > float(green[y + 4, axis_x]) + 20:
                front = (value, y)
    if front is None:
        raise SystemExit("breath ring front spike not found")
    track = []

    def walk(x_start, x_stop, step, y_hint):
        points = []
        y = y_hint
        for x in range(x_start, x_stop, step):
            found = thin_ridge(green[:, x], y)
            if found is None:
                break
            y = found[1]
            radius = desk_radius_cm(x, y)
            points.append(
                {
                    "x": int(x),
                    "y": int(y),
                    "addedGreen": round(float(found[2]), 1),
                    "radiusCm": None if radius is None else round(radius, 3),
                }
            )
        return points

    left = walk(axis_x, 540, -4, front[1])
    right = walk(axis_x + 4, 1320, 4, front[1])
    track = left + right
    if len(left) < 5 or len(right) < 5:
        raise SystemExit("breath ring ridge broke immediately")
    front_cm = desk_radius_cm(axis_x, front[1])
    left_cm = left[-1]["radiusCm"]
    right_cm = right[-1]["radiusCm"]
    radius = max(front_cm, left_cm, right_cm)
    column = green[:, axis_x]
    line_cm, line_px = width_cm(axis_x, front[1], column)
    half_in = falloff_px(column, front[1], -1, 0.5)
    half_out = falloff_px(column, front[1], 1, 0.5)
    tenth_in = falloff_px(column, front[1], -1, 0.1)
    tenth_out = falloff_px(column, front[1], 1, 0.1)
    lo = RING_CM * (1.0 - 0.08)
    hi = RING_CM * (1.0 + 0.08)
    return {
        "radiusCm": round(float(radius), 3),
        "frontRadiusCm": round(float(front_cm), 3),
        "leftRadiusCm": left_cm,
        "rightRadiusCm": right_cm,
        "targetCm": RING_CM,
        "tolerance": 0.08,
        "pass": bool(lo <= front_cm <= hi and lo <= left_cm <= hi and lo <= right_cm <= hi),
        "gate": "largest axis of the desk ellipse; every axis must sit inside the tolerance",
        "lineWidthPx": int(line_px),
        "lineWidthCm": None if line_cm is None else round(line_cm, 3),
        "lineWidthWhere": "front spike, where a vertical pixel walk crosses the stroke",
        "glowHalfFalloffPx": {"inward": half_in, "outward": half_out},
        "glowTenthFalloffPx": {"inward": tenth_in, "outward": tenth_out},
        "lineNote": "Section 6 has no numeric tolerance for line width or glow falloff. Added green is render minus the plate, so the plate's own green pool is not counted.",
        "texture": texture_info,
        "frontPx": [axis_x, int(front[1])],
        "leftPx": [left[-1]["x"], left[-1]["y"]],
        "rightPx": [right[-1]["x"], right[-1]["y"]],
        "track": track[::3],
    }


def measure_fiddle(render, plate):
    """Outer span of the crozier head. The 0.025 m halo and the stem are excluded.

    A head row is a bright-green run of coil width inside the fiddle window.
    The stem continues down on the right and is wider once the ferns join it,
    so those rows fall outside the span test. The gate uses the larger screen
    axis. The short axis is recorded and is not allowed to hide a long axis.
    """
    scale = px_per_cm(0.075)
    green = render[:, :, 1]
    rows = []
    for y in range(450, 580):
        xs = np.where(green[y, 840:1000] >= 230)[0]
        if len(xs) == 0:
            continue
        left = 840 + int(xs.min())
        right = 840 + int(xs.max())
        span = right - left + 1
        if 40 <= span <= 110 and right < 980 and left < 910:
            rows.append(y)
    if len(rows) < 8:
        return {"pass": False, "reason": "coil head not found", "pxPerCm": round(scale, 3)}
    y0, y1 = rows[0], rows[-1]
    # Keep a row only when it is part of the continuous head.
    band = green[y0 : y1 + 1, 840:1000] >= 230
    ys, xs = np.where(band)
    width = int(xs.max() - xs.min() + 1)
    height = int(ys.max() - ys.min() + 1)
    outer_px = max(width, height)
    cm = outer_px / scale
    err = abs(cm - COIL_CM) / COIL_CM
    return {
        "measuredCm": round(cm, 3),
        "shortAxisCm": round(min(width, height) / scale, 3),
        "targetCm": COIL_CM,
        "tolerance": 0.15,
        "measuredPx": outer_px,
        "boxPx": [width, height],
        "pxPerCm": round(scale, 3),
        "box": [840 + int(xs.min()), y0 + int(ys.min()), width, height],
        "pass": bool(err <= 0.15),
        "note": "Outer diameter of the spiral head. The halo card is 2.5 cm and is not this measurement.",
    }


def measure_spores(render, plate, fiddle):
    """Isolated warm specks in the air. Fern edges and the coil highlight are not spores."""
    y0, y1, x0, x1 = 400, 650, 790, 1080
    crop = render[y0:y1, x0:x1].astype(np.float64)
    luma = crop.mean(axis=2)
    speck = luma - local_mean(luma, 4)
    green = crop[:, :, 1]
    # Ferns are bright in the ring around a peak. The peak itself is bright either way.
    inner = local_mean(green, 2)
    outer = local_mean(green, 8)
    surround = (outer * 289.0 - inner * 25.0) / (289.0 - 25.0)
    box = fiddle.get("box")
    peaks = []
    height, width = speck.shape
    for y in range(6, height - 6):
        for x in range(6, width - 6):
            value = float(speck[y, x])
            if value < 40 or float(surround[y, x]) > 175:
                continue
            if value < float(speck[y - 2 : y + 3, x - 2 : x + 3].max()) - 1e-3:
                continue
            colour = crop[y, x]
            if colour[0] < 180 or colour[1] < 160 or colour[1] > colour[0] + 30 or colour[2] > 245:
                continue
            px, py = x0 + x, y0 + y
            if box is not None:
                bx, by, bw, bh = box
                if bx - 8 <= px <= bx + bw + 8 and by - 8 <= py <= by + bh + 8:
                    continue
            peaks.append((value, px, py, [int(round(float(v))) for v in colour]))
    peaks.sort(reverse=True)
    kept = []
    for item in peaks:
        if any((item[1] - other[1]) ** 2 + (item[2] - other[2]) ** 2 < 12 * 12 for other in kept):
            continue
        kept.append(item)
    blobs = [{"x": x, "y": y, "rgb": rgb, "contrast": round(value, 1)} for value, x, y, rgb in kept]
    return {
        "count": len(blobs),
        "min": 20,
        "max": 40,
        "pass": bool(20 <= len(blobs) <= 40),
        "blobs": blobs,
        "note": "Warm local peaks in the air column, below the cork and above the moss, outside the fiddle head.",
    }


def local_mean(channel, radius):
    padded = np.pad(channel, ((1, 0), (1, 0)), mode="constant")
    integral = padded.cumsum(0).cumsum(1)
    height, width = channel.shape
    ys = np.arange(height)
    xs = np.arange(width)
    y0 = np.clip(ys - radius, 0, height)
    y1 = np.clip(ys + radius + 1, 0, height)
    x0 = np.clip(xs - radius, 0, width)
    x1 = np.clip(xs + radius + 1, 0, width)
    y_start, x_start = np.meshgrid(y0, x0, indexing="ij")
    y_end, x_end = np.meshgrid(y1, x1, indexing="ij")
    total = (
        integral[y_end, x_end]
        - integral[y_start, x_end]
        - integral[y_end, x_start]
        + integral[y_start, x_start]
    )
    return total / ((y_end - y_start) * (x_end - x_start))


def mean_color_delta(pixels, target_lab):
    mean = pixels.mean(axis=0)
    return ciede2000(srgb_to_lab(mean.reshape(1, 3))[0], target_lab), mean


def measure_regions(render, reference):
    """Locked windows. Each rect is the reference patch closest to the section 1
    hex that is the same material in the render.

    The hexes are region averages, so the gate number is CIEDE2000 of the mean
    colour. Mean per-pixel distance is stored beside it. The reference moss is
    itself well above 8 on that per-pixel figure, because a textured average is
    not a flat colour. That figure does not gate.
    """
    locked = {
        "moss": ("#376222", (992, 696, 48, 28), "moss carpet in both frames"),
        "frondEdge": ("#58936E", (956, 582, 24, 20), "reference leaflet; render sample is the frond stem beside a leaflet"),
        "glassMid": ("#517A7F", (760, 368, 28, 36), "empty upper glass in both frames"),
        "soil": ("#0D231D", (800, 768, 60, 22), "dark soil band in both frames"),
        "wall": ("#0C1A1F", (1360, 200, 48, 36), "upper wall of the plate, outside the jar"),
    }
    out = {}
    for name, (hex_text, rect, where) in locked.items():
        lab = srgb_to_lab(hex_rgb(hex_text).reshape(1, 3))[0]
        ref_pix, rect = window_pixels(reference, rect)
        ren_pix, _ = window_pixels(render, rect)
        ref_mean, ref_rgb = mean_color_delta(ref_pix, lab)
        ren_mean, ren_rgb = mean_color_delta(ren_pix, lab)
        out[name] = {
            "hex": hex_text,
            "rect": list(rect),
            "where": where,
            "pixels": int(len(ren_pix)),
            "meanRgb": [round(float(v), 2) for v in ren_rgb],
            "referenceMeanRgb": [round(float(v), 2) for v in ref_rgb],
            "meanDeltaE": round(float(ren_mean), 3),
            "referenceMeanDeltaE": round(float(ref_mean), 3),
            "meanPixelDeltaE": round(float(mean_delta(ren_pix, lab)), 3),
            "referenceMeanPixelDeltaE": round(float(mean_delta(ref_pix, lab)), 3),
            "limit": 8,
            "pass": bool(ren_mean <= 8.0),
        }
    return out


def draw_overlay(render, dims, regions, fiddle, ring, spores, out_path):
    im = Image.fromarray(np.clip(render, 0, 255).astype(np.uint8))
    draw = ImageDraw.Draw(im)
    draw.line(
        [(dims["centerX"], dims["corkTopPx"]), (dims["centerX"], dims["heelPx"])],
        fill=(255, 220, 80),
        width=1,
    )
    draw.line(
        [(dims["leftPx"], dims["midY"]), (dims["rightPx"], dims["midY"])],
        fill=(255, 220, 80),
        width=1,
    )
    for name, region in regions.items():
        x, y, w, h = region["rect"]
        draw.rectangle([x, y, x + w, y + h], outline=(80, 180, 255))
        draw.text((x + 2, max(0, y - 12)), name, fill=(80, 180, 255))
    if "box" in fiddle:
        x, y, w, h = fiddle["box"]
        draw.rectangle([x, y, x + w, y + h], outline=(255, 80, 220))
    for point in ring.get("track", []):
        x, y = point["x"], point["y"]
        draw.ellipse([x - 2, y - 2, x + 2, y + 2], fill=(255, 60, 60))
    for blob in spores.get("blobs", []):
        x, y = blob["x"], blob["y"]
        draw.ellipse([x - 6, y - 6, x + 6, y + 6], outline=(255, 180, 40))
    im.save(out_path)


def relative_luminance(rgb):
    """IEC 61966-2-1 sRGB to CIE Y. rgb is 0..255. The result is 0..1."""
    c = np.asarray(rgb, dtype=np.float64) / 255.0
    lin = np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)
    return 0.2126729 * lin[..., 0] + 0.7151522 * lin[..., 1] + 0.0721750 * lin[..., 2]


def _window(image, rect):
    x, y, w, h = rect
    return image[int(y) : int(y + h), int(x) : int(x + w)]


def measure_glow(image):
    """Peak at the crozier, mean of the glass rect, peak of the ring band.

    Floor and ceiling are both returned. A value on either bound is inside.
    """
    luma = relative_luminance(image)
    raw = {
        "crozierPeak": float(_window(luma, GLOW_CROZIER).max()),
        "glassMean": float(_window(luma, GLOW_GLASS).mean()),
        "ringPeak": float(_window(luma, GLOW_RING).max()),
    }
    # A clipped white peak is 1. The comparison uses the reported 4 decimals,
    # so a value that prints as the ceiling is inside the ceiling.
    measured = {key: round(min(value, 1.0), 4) for key, value in raw.items()}
    inside = {}
    for key, value in measured.items():
        inside[key] = bool(GLOW_FLOOR[key] <= value <= GLOW_CEILING[key])
    return {
        "units": "relative luminance, IEC sRGB to CIE Y, 0 to 1",
        "crozierRect": list(GLOW_CROZIER),
        "glassRect": list(GLOW_GLASS),
        "ringRect": list(GLOW_RING),
        "floor": dict(GLOW_FLOOR),
        "ceiling": dict(GLOW_CEILING),
        "measured": measured,
        "inside": inside,
        "pass": bool(all(inside.values())),
        "note": "Floor is above the T-TER-030 undershoot. Ceiling is under the T-TER-028 mint wash. Both bounds are in LOCKED.md.",
    }


def print_glow(glow):
    ref = glow.get("reference") or {}
    for key in ("crozierPeak", "glassMean", "ringPeak"):
        flag = "INSIDE" if glow["inside"][key] else "OUTSIDE"
        print(
            "glow",
            key,
            glow["measured"][key],
            "floor",
            glow["floor"][key],
            "ceiling",
            glow["ceiling"][key],
            "ref",
            ref.get(key),
            flag,
        )


def self_test():
    a = (50.0000, 2.6772, -79.7751)
    b = (50.0000, 0.0000, -82.7485)
    got = ciede2000(a, b)
    if abs(got - 2.0425) > 0.001:
        raise SystemExit("CIEDE2000 Sharma pair got %.4f" % got)
    if ciede2000(a, a) > 1e-9:
        raise SystemExit("CIEDE2000 identical pair is not zero")
    lab = srgb_to_lab(hex_rgb("#376222").reshape(1, 3))[0]
    if ciede2000(lab, lab) > 1e-6:
        raise SystemExit("hex round trip is not zero")
    base = project((0.0, 0.0, 0.0))
    # Bible: jar base at pixel (915, 830). A few pixels of lens-model error is expected.
    if abs(base[0] - 915) > 8 or abs(base[1] - 830) > 8:
        raise SystemExit("jar base projects to (%.1f, %.1f), bible is (915, 830)" % base)
    for key in GLOW_FLOOR:
        if GLOW_FLOOR[key] > GLOW_CEILING[key]:
            raise SystemExit("glow floor is above the ceiling for %s" % key)
    sample = relative_luminance(np.array([[[0, 0, 0], [255, 255, 255]]], dtype=np.float64))
    if abs(float(sample[0, 0]) ) > 1e-6 or abs(float(sample[0, 1]) - 1.0) > 1e-4:
        raise SystemExit("relative luminance black/white got %s" % sample)
    print("self-test ok  de2000=%.4f  base=(%.1f, %.1f)" % (got, base[0], base[1]))


def main():
    if len(sys.argv) == 2 and sys.argv[1] == "--self-test":
        self_test()
        return
    if len(sys.argv) == 3 and sys.argv[1] == "--glow":
        self_test()
        image = load_rgb(sys.argv[2])
        glow = measure_glow(image)
        print_glow(glow)
        print("glow", "PASS" if glow["pass"] else "FAIL")
        return
    if len(sys.argv) != 2:
        raise SystemExit("usage: spec_anchor.py <run-dir> | --self-test | --glow <png>")
    run = sys.argv[1]
    repo = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "..", ".."))
    render_path = os.path.join(run, "jar-g1.png")
    ref_path = os.path.join(repo, "shared", "assets", "art-reference", "A1-03-night-moss-1.png")
    plate_path = os.path.join(repo, "shared", "assets", "room-plates", "plate-jar.png")
    ring_path = os.path.join(repo, "apps", "terrarium", "Assets", "Art", "Textures", "ring.png")
    render = load_rgb(render_path)
    reference = load_rgb(ref_path)
    plate = load_rgb(plate_path)
    if render.shape[0] != H or render.shape[1] != W:
        raise SystemExit("render is %s, expected %dx%d" % (render.shape, W, H))
    self_test()
    glow = measure_glow(render)
    glow["reference"] = measure_glow(reference)["measured"]
    glow_path = os.path.join(run, "glow-measure.json")
    with open(glow_path, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(glow, handle, indent=2)
        handle.write("\n")
    print("wrote", glow_path)
    print_glow(glow)
    texture = ring_texture_peak(ring_path)
    dims = measure_dimensions(render, plate)
    ring = measure_ring(render, plate, texture)
    fiddle = measure_fiddle(render, plate)
    spores = measure_spores(render, plate, fiddle)
    regions = measure_regions(render, reference)
    # mask is an ndarray and must not go into JSON.
    dims_out = {k: v for k, v in dims.items() if k != "mask"}
    checks = [
        ("jarHeight", dims_out["jarHeight"]["pass"]),
        ("jarDiameter", dims_out["jarDiameter"]["pass"]),
        ("corkHeight", dims_out["corkHeight"]["pass"]),
        ("ringRadius", ring["pass"]),
        ("fiddlehead", fiddle.get("pass", False)),
        ("spores", spores["pass"]),
    ]
    for name, region in regions.items():
        checks.append(("region." + name, region.get("pass", False)))
    checks.append(("glow", glow["pass"]))
    failed = [name for name, ok in checks if not ok]
    report = {
        "framing": "JarG1",
        "state": "breath=0.5,uncoil=0.3,fog=0.45,time=3",
        "render": "jar-g1.png",
        "reference": "shared/assets/art-reference/A1-03-night-moss-1.png",
        "bible": "docs/art/terrarium-style.md section 6",
        "note": "CaptureCli.Measure writes budget counts. These spec numbers are measured on the JarG1 shot.",
        "dimensions": dims_out,
        "ring": {k: v for k, v in ring.items() if k != "track"},
        "ringTrack": ring["track"],
        "fiddlehead": fiddle,
        "spores": {k: v for k, v in spores.items() if k != "blobs"},
        "sporeBlobs": spores["blobs"],
        "regions": regions,
        "glow": glow,
        "failed": failed,
        "pass": len(failed) == 0,
    }
    out_json = os.path.join(run, "a2-measure.json")
    with open(out_json, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(report, handle, indent=2)
        handle.write("\n")
    draw_overlay(render, dims, regions, fiddle, ring, spores, os.path.join(run, "a2-overlay.png"))
    print("wrote", out_json)
    print("pass" if report["pass"] else "fail", " ".join(failed))
    for key in ("jarHeight", "jarDiameter", "corkHeight"):
        item = dims_out[key]
        print(key, item["measuredCm"], "cm", item["measuredPx"], "px", "PASS" if item["pass"] else "FAIL")
    print(
        "ring",
        ring["radiusCm"],
        "front",
        ring["frontRadiusCm"],
        "left",
        ring["leftRadiusCm"],
        "right",
        ring["rightRadiusCm"],
        "line",
        ring["lineWidthCm"],
        "PASS" if ring["pass"] else "FAIL",
    )
    print("fiddle", fiddle.get("measuredCm"), "PASS" if fiddle.get("pass") else "FAIL")
    print("spores", spores["count"], "PASS" if spores["pass"] else "FAIL")
    for name, region in regions.items():
        print(name, region.get("meanDeltaE"), "ref", region.get("referenceMeanDeltaE"), "PASS" if region.get("pass") else "FAIL")
    print("glow", "PASS" if glow["pass"] else "FAIL")


if __name__ == "__main__":
    main()
