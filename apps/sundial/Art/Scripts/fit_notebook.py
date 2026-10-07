# Fit the Nano Banana plates onto the dial's top-down UV.
# dial_face_nb.png is the painted face. soil_bed_nb.png is the earth.
# tiles_nb.png is the rim cards. Geometry (circle, ticks, empty centre) is
# enforced here so a wobbly generation still projects cleanly.
#
#   python apps/sundial/Art/Scripts/fit_notebook.py
import os

import cv2
import numpy as np
from PIL import Image, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
TEX = os.path.normpath(os.path.join(HERE, "..", "..", "Assets", "Art", "Textures"))
SRC = os.path.normpath(os.path.join(HERE, "..", "..", "Art", "Source"))

S = 2048
# Soil mesh UV radius. sundial_dial.py: uv = 0.5 + x / (2 * FACE_R), SOIL_R 0.068, FACE_R 0.1472.
SOIL_UV = 0.068 / (2.0 * 0.1472)
PAPER = np.array([244, 239, 230], np.float32)
INK = np.array([42, 38, 34], np.float32)
PENCIL = np.array([138, 129, 120], np.float32)

# Same sectors as dial_face.mjs. Angle is y-up, 0 at image right.
WASHES = (
    ("sunrise", 270.0, 108.0, np.array([246, 222, 183], np.float32)),
    ("midday", 112.0, 18.0, np.array([244, 182, 161], np.float32)),
    ("dusk", 22.0, -128.0, np.array([202, 176, 197], np.float32)),
)


def load_rgb(path):
    return np.asarray(Image.open(path).convert("RGB"))


def largest_disc(rgb):
    lum = 0.2126 * rgb[:, :, 0] + 0.7152 * rgb[:, :, 1] + 0.0722 * rgb[:, :, 2]
    mask = (lum > 22).astype(np.uint8) * 255
    mask = cv2.morphologyEx(mask, cv2.MORPH_CLOSE, np.ones((9, 9), np.uint8))
    contours, _ = cv2.findContours(mask, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
    if not contours:
        raise SystemExit("no disc in the generated plate")
    contour = max(contours, key=cv2.contourArea)
    (cx, cy), radius = cv2.minEnclosingCircle(contour)
    if radius < 8:
        raise SystemExit("disc is too small")
    return float(cx), float(cy), float(radius)


def warp_disc(rgb, size, fill):
    cx, cy, radius = largest_disc(rgb)
    dest_r = size * fill * 0.5
    scale = dest_r / radius
    matrix = np.array(
        [[scale, 0.0, size * 0.5 - cx * scale], [0.0, scale, size * 0.5 - cy * scale]],
        np.float32,
    )
    warped = cv2.warpAffine(
        rgb, matrix, (size, size), flags=cv2.INTER_CUBIC, borderMode=cv2.BORDER_CONSTANT, borderValue=(0, 0, 0)
    )
    print("disc center (%.1f, %.1f) r %.1f -> %.1f" % (cx, cy, radius, dest_r))
    return warped.astype(np.float32), dest_r


def luma(rgb):
    return 0.2126 * rgb[:, :, 0] + 0.7152 * rgb[:, :, 1] + 0.0722 * rgb[:, :, 2]


def lift_washes(rgb, dest_r):
    yy, xx = np.mgrid[0:S, 0:S]
    ang = np.degrees(np.arctan2(S * 0.5 - yy, xx - S * 0.5))
    rad = np.hypot(xx - S * 0.5, yy - S * 0.5)
    band = (rad > dest_r * 0.24) & (rad < dest_r * 0.90)
    out = rgb.copy()
    for name, a0, a1, pale in WASHES:
        span = (a0 - a1) % 360.0
        frac = ((a0 - ang) % 360.0) / span
        inside = band & (frac >= 0.02) & (frac <= 0.98)
        if int(inside.sum()) < 100:
            continue
        med = float(np.median(luma(out)[inside]))
        # High-key. A muddy sector is scaled toward the reference pale, grain kept.
        target = 0.78
        if med < 0.70 and med > 1e-3:
            gain = min(target / med, 1.65)
            lifted = np.clip(out * gain, 0, 255)
            # Pull the hue toward the pale so a brown swirl cannot stay brown.
            mix = 0.28 if med < 0.55 else 0.16
            lifted = lifted * (1.0 - mix) + pale * mix
            out[inside] = np.clip(lifted, 0, 255)[inside]
            print("wash %s median %.3f gain %.2f" % (name, med, gain))
        else:
            print("wash %s median %.3f kept" % (name, med))
    return out


def paper_rim(rgb, dest_r):
    yy, xx = np.mgrid[0:S, 0:S]
    rad = np.hypot(xx - S * 0.5, yy - S * 0.5)
    lum = luma(rgb)
    rim = (rad > dest_r * 0.905) & (rad <= dest_r)
    # Keep ink and pencil. Lift everything else to cream.
    keep = lum < 168
    out = rgb.copy()
    soft = np.clip((lum - 168.0) / 50.0, 0.0, 1.0)
    lifted = rgb * (1.0 - 0.82 * soft[..., None]) + PAPER * (0.82 * soft[..., None])
    paint = rim & ~keep
    out[paint] = np.clip(lifted, 0, 255)[paint]
    outside = rad > dest_r
    out[outside] = PAPER
    return out


def draw_ticks(rgb, dest_r):
    # The generated plate already has the ink circles and the pencil ticks.
    # Draw them only when that band came out blank.
    yy, xx = np.mgrid[0:S, 0:S]
    rad = np.hypot(xx - S * 0.5, yy - S * 0.5)
    lum = luma(rgb)
    annulus = (rad > dest_r * 0.915) & (rad < dest_r * 0.985)
    dark = float(((lum < 150) & annulus).sum()) / max(1.0, float(annulus.sum()))
    print("rim dark fraction %.4f" % dark)
    if dark >= 0.012:
        return rgb
    print("drawing pencil ticks and ink circles")
    canvas = np.clip(rgb, 0, 255).astype(np.uint8).copy()
    centre = (S // 2, S // 2)
    cv2.circle(canvas, centre, int(dest_r * 0.992), (42, 38, 34), 4, lineType=cv2.LINE_AA)
    cv2.circle(canvas, centre, int(dest_r * 0.900), (42, 38, 34), 3, lineType=cv2.LINE_AA)
    for deg in range(0, 360, 3):
        ang = np.deg2rad(deg)
        long = deg % 15 == 0
        mid = deg % 6 == 0
        r0 = dest_r * 0.978
        r1 = dest_r * (0.930 if long else 0.948 if mid else 0.960)
        p0 = (int(S * 0.5 + r0 * np.cos(ang)), int(S * 0.5 - r0 * np.sin(ang)))
        p1 = (int(S * 0.5 + r1 * np.cos(ang)), int(S * 0.5 - r1 * np.sin(ang)))
        thick = 2 if long else 1
        cv2.line(canvas, p0, p1, (128, 120, 110), thick, lineType=cv2.LINE_AA)
    return canvas.astype(np.float32)


def fit_face(path):
    rgb = load_rgb(path)
    warped, dest_r = warp_disc(rgb, S, 0.992)
    warped = lift_washes(warped, dest_r)
    warped = paper_rim(warped, dest_r)
    warped = draw_ticks(warped, dest_r)
    out = np.clip(warped, 0, 255).astype(np.uint8)
    # DialTop stores a +90 X node rotation, and the UV was written from the
    # axis-converted X which negates Blender X. On the gate camera, screen-right
    # shows the left side of this file. Flip so gold lands on the sunrise herb
    # (screen left), coral on the midday flower (screen far), lilac on the lavender.
    turned = Image.fromarray(out, "RGB").transpose(Image.Transpose.FLIP_LEFT_RIGHT)
    # The warm key light roses the lilac. grade_a2 puts the pale dusk wash back on #BFA1BC
    # and clears the painted rim so Dial_Face can draw the ring. Both steps are idempotent.
    import sys
    if HERE not in sys.path:
        sys.path.insert(0, HERE)
    import grade_a2
    graded = grade_a2.thin_rim_ink(grade_a2.grade_dusk_wash(np.asarray(turned).astype(np.float32)))
    dest = os.path.join(TEX, "dial_face.png")
    Image.fromarray(np.clip(graded, 0, 255).astype(np.uint8), "RGB").save(dest)
    out = np.asarray(Image.open(dest).convert("RGB"))
    print("face", dest)
    return out


def fit_soil(path):
    rgb = load_rgb(path)
    # The mound's UV disk is SOIL_UV of the face. Fit the painted earth into it.
    warped, dest_r = warp_disc(rgb, S, SOIL_UV * 2.0)
    yy, xx = np.mgrid[0:S, 0:S]
    rad = np.hypot(xx - S * 0.5, yy - S * 0.5)
    disc = rad <= dest_r * 0.98
    med = float(np.median(luma(warped)[disc])) if int(disc.sum()) else 0.0
    target = np.array([160.0, 131.0, 108.0], np.float32)
    # The generated disc is keyed on black, so the rim is a dark fringe. Paint it out.
    lumv = luma(warped)
    fringe = (rad > dest_r * 0.82) & (lumv < med * 0.72)
    warped[fringe] = np.array([176.0, 145.0, 120.0], np.float32)
    # The mound's shade step is gentle, so the stipple has to live in the texture.
    disc_px = rad <= dest_r * 0.96
    if int(disc_px.sum()) > 100 and med > 1.0:
        warped[disc_px] = np.clip((warped[disc_px] - med) * 1.28 + med, 0, 255)
        print("soil contrast around %.1f" % med)
    if med < 120.0 and med > 1.0:
        gain = min(155.0 / med, 1.7)
        warped = np.clip(warped * gain, 0, 255)
        warped = warped * 0.82 + target * 0.18
        print("soil median %.1f gain %.2f" % (med, gain))
    else:
        print("soil median %.1f kept" % med)
    edge = np.clip((dest_r - rad) / 18.0, 0.0, 1.0)
    fill = np.array([168.0, 136.0, 112.0], np.float32)
    rgb_out = np.tile(fill, (S, S, 1))
    a = edge[..., None]
    rgb_out = rgb_out * (1.0 - a) + warped * a
    dest = os.path.join(TEX, "soil_bed.png")
    Image.fromarray(np.clip(rgb_out, 0, 255).astype(np.uint8), "RGB").save(dest)
    print("soil", dest, "radius_px %.1f" % dest_r)


def tiles_from_contours(rgb):
    lum = luma(rgb.astype(np.float32))
    mask = (lum > 28).astype(np.uint8) * 255
    mask = cv2.morphologyEx(mask, cv2.MORPH_OPEN, np.ones((3, 3), np.uint8))
    contours, _ = cv2.findContours(mask, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
    boxes = []
    h, w = lum.shape
    for contour in contours:
        x, y, bw, bh = cv2.boundingRect(contour)
        if bw < w * 0.03 or bh < h * 0.08:
            continue
        if bw > w * 0.4 or bh > h * 0.95:
            continue
        boxes.append((x, y, bw, bh))
    boxes.sort(key=lambda b: b[0])
    return boxes


def fit_tiles(path, face):
    rgb = load_rgb(path)
    boxes = tiles_from_contours(rgb)
    print("tile boxes", len(boxes))
    cells = []
    if len(boxes) >= 4:
        # Four groups: gold, coral, lilac, cream. Take the middle of each group.
        groups = np.array_split(np.arange(len(boxes)), 4)
        for group in groups:
            i = int(group[len(group) // 2])
            x, y, bw, bh = boxes[i]
            pad = int(min(bw, bh) * 0.06)
            crop = rgb[max(0, y - pad):y + bh + pad, max(0, x - pad):x + bw + pad]
            cell = cv2.resize(crop, (256, 256), interpolation=cv2.INTER_AREA)
            cells.append(fill_void(cell))
    else:
        print("tile plate had no row; sampling the fitted face washes")
        cells = tiles_from_face(face)
    atlas = np.concatenate(cells, axis=1)
    dest = os.path.join(TEX, "tiles_atlas.png")
    Image.fromarray(atlas, "RGB").save(dest)
    print("tiles", dest, atlas.shape)


def fill_void(cell):
    # Rounded tiles are keyed on black. The card is a full rectangle, so the
    # corners have to be paper or they read as holes.
    lum = luma(cell.astype(np.float32))
    void = lum < 28
    if int(void.sum()) < 8:
        return cell
    kept = cell[~void].astype(np.float32)
    light = 0.2126 * kept[:, 0] + 0.7152 * kept[:, 1] + 0.0722 * kept[:, 2]
    paper = np.median(kept[light >= np.percentile(light, 65)], axis=0)
    out = cell.copy()
    out[void] = np.clip(paper, 0, 255).astype(np.uint8)
    return out


def tiles_from_face(face):
    # 2048 face. Sample a square in each wash, plus cream paper.
    centres = (
        (0.72, 0.55),  # sunrise gold, file right (screen left after the mesh mirror)
        (0.50, 0.24),  # midday coral, top
        (0.28, 0.55),  # dusk lilac, file left (screen right after the mesh mirror)
        (0.50, 0.08),  # paper near the rim, will be recoloured if it caught a wash
    )
    cells = []
    for i, (u, v) in enumerate(centres):
        cx, cy = int(u * S), int(v * S)
        half = 70
        crop = face[cy - half:cy + half, cx - half:cx + half]
        if crop.shape[0] < 40:
            crop = np.tile(PAPER.astype(np.uint8), (140, 140, 1))
        cell = cv2.resize(crop, (256, 256), interpolation=cv2.INTER_AREA)
        if i == 3:
            cell = np.clip(cell.astype(np.float32) * 0.25 + PAPER * 0.75, 0, 255).astype(np.uint8)
        # A wobbly ink edge so a sampled wash still reads as a tile.
        tile = cell.copy()
        cv2.rectangle(tile, (8, 8), (247, 247), (42, 38, 34), 4, lineType=cv2.LINE_AA)
        cells.append(tile)
    return cells


def paint_shadow(path):
    # Narrow end is the top of the image. DialSetup's wedge has UV v = 0 there,
    # which is the end that worked in T-SUN-014.
    w = h = 1024
    yy, xx = np.mgrid[0:h, 0:w]
    t = yy / (h - 1.0)
    half = 36.0 + t * 250.0
    dist = np.abs(xx - (w / 2.0))
    edge = np.clip((half - dist) / 48.0, 0.0, 1.0)
    edge = edge * edge * (3.0 - 2.0 * edge)
    along = np.clip(1.0 - t * 0.55, 0.0, 1.0)
    along *= np.clip((t - 0.01) / 0.06, 0.0, 1.0)
    rng = np.random.RandomState(7)
    grit = 0.82 + 0.18 * (rng.rand(h, w) > 0.5)
    alpha = edge * along * grit * 1.05
    soft = np.asarray(
        Image.fromarray((np.clip(alpha, 0, 1) * 255).astype(np.uint8), "L").filter(ImageFilter.GaussianBlur(14))
    ).astype(np.float32)
    # A second, wider, lighter pass so the wash blooms like watercolour.
    wide = np.asarray(
        Image.fromarray((np.clip(alpha, 0, 1) * 180).astype(np.uint8), "L").filter(ImageFilter.GaussianBlur(28))
    ).astype(np.float32)
    soft = np.clip(soft * 0.55 + wide * 0.50, 0, 230)
    colour = np.zeros((h, w, 4), np.uint8)
    colour[:, :, 0] = 108
    colour[:, :, 1] = 86
    colour[:, :, 2] = 70
    colour[:, :, 3] = soft.astype(np.uint8)
    Image.fromarray(colour, "RGBA").save(path)
    print("shadow", path, "peak", int(soft.max()))


def main():
    face = fit_face(os.path.join(SRC, "dial_face_nb.png"))
    fit_soil(os.path.join(SRC, "soil_bed_nb.png"))
    fit_tiles(os.path.join(SRC, "tiles_nb.png"), face)
    paint_shadow(os.path.join(TEX, "gnomon_shadow.png"))


if __name__ == "__main__":
    main()
