# T-SUN-052 layout face. A polar remap of the graded dial_face.png (no new paint, no reference pixels): the washes grow out to a
# wider inner disc and the cream rim band narrows to the proportion the layout pass measured, with softer ticks and a warm
# pencil-shaded outer edge. The painted wash colours are untouched (a radial stretch only), so the T-SUN-028 wash grades hold.
#   python apps/sundial/Art/Scripts/layout_t052_face.py            # writes Assets/Resources/Layout/dial_face_layout.png
#   python apps/sundial/Art/Scripts/layout_t052_face.py --profile  # prints the radii of the source and the result
import json
import math
import os
import sys

import cv2
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.normpath(os.path.join(HERE, "..", "..", "Assets", "Art", "Textures", "dial_face.png"))
OUT_DIR = os.path.normpath(os.path.join(HERE, "..", "..", "Assets", "Resources", "Layout"))
OUT = os.path.join(OUT_DIR, "dial_face_layout.png")

# Knots in units of the face radius: output radius -> source radius. The source has the wash disc out to 0.731 (painted ink line),
# the tick band from 0.88, and the paper out to 1.0. The layout wants the wash disc out to INNER and the band INNER..1.
SRC_WASH = 0.731
INNER = 0.82
CORE = 0.31
KNOTS_OUT = [0.0, CORE, INNER, 1.0, 1.2]
KNOTS_SRC = [0.0, CORE, SRC_WASH, 1.0, 1.2 - 0.0]

CORE_FROM = 0.37     # skip the pooled inner edge of the wash disc (its pencil line and dark rim)
CORE_FILL = 0.35     # how far into the wash the core mirrors (face radii per face radius of core)
TICK_KEEP = 0.5      # share of a tick's contrast kept (the source ticks read heavy at the dial's size)
WARM = np.array([0.88, 0.79, 0.66], np.float32)   # multiplied into the outer face of the band
HATCH_FROM = 0.925   # hatching starts here and runs to the edge


def remap():
    img = cv2.imread(SRC, cv2.IMREAD_COLOR)
    h, w = img.shape[:2]
    c = (w - 1) / 2.0
    R = (w - 1) / 2.0
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    dx, dy = xx - c, yy - c
    rho_out = np.hypot(dx, dy) / R
    theta = np.arctan2(dy, dx)
    rho_src = np.interp(rho_out, KNOTS_OUT, KNOTS_SRC).astype(np.float32)
    # The blank paper core (under the soil in look A) would peek out above the bed's far edge, which now sits nearer the viewer.
    # Fill it with the wash next to it: mirror the source about the core edge, stretched, so the sector colours run on to the centre.
    inside = rho_out < CORE
    rho_src = np.where(inside, CORE_FROM + (CORE - rho_out) * CORE_FILL, rho_src).astype(np.float32)
    mx = (c + np.cos(theta) * rho_src * R).astype(np.float32)
    my = (c + np.sin(theta) * rho_src * R).astype(np.float32)
    out = cv2.remap(img, mx, my, cv2.INTER_CUBIC, borderMode=cv2.BORDER_REPLICATE)
    return out, rho_out, theta


def soften_ticks(out, rho_out):
    """Ticks keep a share of their contrast: blend toward a radially smoothed band."""
    f = out.astype(np.float32)
    band = (rho_out > INNER + 0.03) & (rho_out < 0.995)
    base = cv2.medianBlur(out, 31).astype(np.float32)
    k = np.where(band, TICK_KEEP, 1.0)[..., None]
    dark = (base.mean(axis=2) - f.mean(axis=2)) > 0
    k = np.where(dark[..., None], k, 1.0)
    return base * (1 - k) + f * k


def warm_and_hatch(f, rho_out, theta):
    """Warm toning toward the outer edge, then a pencil hatch (short slanted strokes, seeded) over the outer band."""
    t = np.clip((rho_out - INNER) / (1.0 - INNER), 0, 1) ** 0.8
    t = t * t * (3 - 2 * t)
    warm = 1.0 - t[..., None] * (1.0 - WARM[::-1][None, None, :])   # BGR
    f = f * warm
    h, w = rho_out.shape
    hatch = np.zeros((h, w), np.float32)
    rng = np.random.default_rng(52)
    c = (w - 1) / 2.0
    R = (w - 1) / 2.0
    n = 2600
    for _ in range(n):
        ang = rng.uniform(0, 2 * math.pi)
        r0 = rng.uniform(HATCH_FROM, 0.992)
        ln = rng.uniform(0.012, 0.030)
        # strokes lean along the circle, 25 degrees off the tangent
        lean = ang + rng.uniform(0.012, 0.03)
        p0 = (c + math.cos(ang) * r0 * R, c + math.sin(ang) * r0 * R)
        p1 = (c + math.cos(lean) * (r0 + ln) * R, c + math.sin(lean) * (r0 + ln) * R)
        cv2.line(hatch, (int(p0[0] * 4), int(p0[1] * 4)), (int(p1[0] * 4), int(p1[1] * 4)), float(rng.uniform(0.18, 0.5)), 1, cv2.LINE_AA, shift=2)
    hatch = cv2.GaussianBlur(hatch, (0, 0), 0.8)
    edge = np.clip((rho_out - HATCH_FROM) / 0.03, 0, 1)
    shade = 1.0 - 0.22 * hatch * edge
    return f * shade[..., None]


def build():
    out, rho_out, theta = remap()
    f = soften_ticks(out, rho_out)
    f = warm_and_hatch(f, rho_out, theta)
    os.makedirs(OUT_DIR, exist_ok=True)
    cv2.imwrite(OUT, np.clip(f, 0, 255).astype(np.uint8))
    with open(OUT + ".provenance.txt", "w", newline="\n") as fh:
        fh.write("asset: dial_face_layout.png\n"
                 "method: polar remap of apps/sundial/Assets/Art/Textures/dial_face.png by apps/sundial/Art/Scripts/layout_t052_face.py "
                 "(wash disc 0.731 -> %.2f, band %.2f..1, ticks at %.0f%% contrast, warm toning and a seeded pencil hatch on the outer band)\n"
                 "derived_from_art_reference: no (only the layout ratios were measured from it)\n" % (INNER, INNER, TICK_KEEP * 100))
    print("wrote", OUT)


def profile(path):
    img = cv2.imread(path, cv2.IMREAD_GRAYSCALE).astype(np.float32)
    h, w = img.shape
    c = (w - 1) / 2.0
    R = (w - 1) / 2.0
    out = []
    for a in (0, 45, 90, 135, 180, 225, 270, 315):
        rs = np.arange(0.0, 1.0, 0.002)
        v = [img[int(round(c + math.sin(math.radians(a)) * r * R)), int(round(c + math.cos(math.radians(a)) * r * R))] for r in rs]
        v = np.array(v)
        dark = rs[np.argsort(v)[:3]]
        out.append((a, [round(float(d), 3) for d in sorted(dark)]))
    return out


if __name__ == "__main__":
    if "--profile" in sys.argv:
        print("source", profile(SRC))
        if os.path.exists(OUT):
            print("layout", profile(OUT))
    else:
        build()
