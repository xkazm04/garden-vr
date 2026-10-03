# Grain the structured dial face with the generated wash plates.
# dial_face.mjs owns the arc shapes, the soft edges, the soil and the ink.
# The plates only lend high-frequency brush grain, so a spiral or a hard
# rectangle in the plate cannot cut the sector.
#   python apps/sundial/Art/Scripts/compose_paint.py
import os
from PIL import Image, ImageFilter
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
TEX = os.path.normpath(os.path.join(HERE, "..", "..", "Assets", "Art", "Textures"))
SRC = os.path.normpath(os.path.join(HERE, "..", "..", "Assets", "Art", "Source", "paint"))

S = 2048
C = S / 2
R = S / 2 - 8

WASHES = (
    # id, a0, a1, pale RGB, wet RGB, plate file
    ("sunrise", 270, 108, (246, 222, 183), (217, 118, 42), "wash_morning.png"),
    ("midday", 112, 18, (244, 182, 161), (201, 72, 63), "wash_midday.png"),
    ("dusk", 22, -128, (202, 176, 197), (111, 85, 173), "wash_dusk.png"),
)


def is_line(rgb):
    lum = 0.2126 * rgb[:, :, 0] + 0.7152 * rgb[:, :, 1] + 0.0722 * rgb[:, :, 2]
    sat = rgb.max(axis=2) - rgb.min(axis=2)
    ink = (lum < 95) & (sat < 48)
    pencil = (lum > 78) & (lum < 155) & (sat < 30)
    return ink | pencil


def main():
    face_path = os.path.join(TEX, "dial_face.png")
    base = np.asarray(Image.open(face_path).convert("RGB")).astype(np.float32)
    if base.shape[0] != S or base.shape[1] != S:
        raise SystemExit("dial_face.png is %s, expected %d" % (base.shape, S))
    keep = is_line(base)

    yy, xx = np.mgrid[0:S, 0:S]
    dx = xx - C
    dy = C - yy
    ang = np.degrees(np.arctan2(dy, dx))
    rad = np.hypot(dx, dy)

    out = base.copy()
    r0 = R * 0.20
    r1 = R * 0.86
    for _name, a0, a1, _pale, _wet, filename in WASHES:
        span = (a0 - a1) % 360.0
        frac = ((a0 - ang) % 360.0) / span
        # Stay off the sector boundary so the SVG's soft edge remains.
        inside = (frac >= 0.06) & (frac <= 0.94) & (rad >= r0) & (rad <= r1)
        plate = np.asarray(
            Image.open(os.path.join(SRC, filename)).convert("L").resize((512, 512), Image.Resampling.LANCZOS)
        ).astype(np.float32)
        h, w = plate.shape
        t = np.clip((rad - r0) / (r1 - r0), 0.0, 1.0)
        sx = np.clip((frac * (w - 1)).astype(np.int32), 0, w - 1)
        sy = np.clip(((1.0 - t) * (h - 1)).astype(np.int32), 0, h - 1)
        lum = plate[sy, sx]
        lum_img = Image.fromarray(lum.astype(np.uint8), "L")
        blur = np.asarray(lum_img.filter(ImageFilter.GaussianBlur(14))).astype(np.float32)
        local = lum / np.maximum(blur, 1.0)
        factor = np.clip(local, 0.8, 1.2)
        paint = inside & ~keep
        gained = np.clip(out * ((1.0 - 0.7) + 0.7 * factor[..., None]), 0, 255)
        out[paint] = gained[paint]

    # Soil plate: high-frequency grit only, graded so the bed stays near #A0836C.
    soil = np.asarray(
        Image.open(os.path.join(SRC, "soil.png")).convert("L").resize((S, S), Image.Resampling.LANCZOS)
    ).astype(np.float32)
    soil_blur = np.asarray(
        Image.fromarray(soil.astype(np.uint8), "L").filter(ImageFilter.GaussianBlur(6))
    ).astype(np.float32)
    grit = np.clip(soil / np.maximum(soil_blur, 1.0), 0.86, 1.14)
    sx0 = C
    sy0 = C + R * 0.012
    rx = R * 0.40
    ry = R * 0.37
    ellipse = ((xx - sx0) / rx) ** 2 + ((yy - sy0) / ry) ** 2 <= 1.0
    bed = ellipse & ~keep
    gained = np.clip(out * grit[..., None], 0, 255)
    out[bed] = gained[bed]

    Image.fromarray(out.astype(np.uint8), "RGB").save(face_path)
    paint_shadow(os.path.join(TEX, "gnomon_shadow.png"))
    print("composed", face_path)


def paint_shadow(path):
    # Soft painted wedge. Narrow end is the top of the image, matching the mesh UV
    # the dial already used. Blur is premultiplied so the edge does not fringe.
    w = h = 1024
    yy, xx = np.mgrid[0:h, 0:w]
    t = yy / (h - 1.0)
    half = 42.0 + t * 280.0
    dist = np.abs(xx - (w / 2.0))
    edge = np.clip((half - dist) / 36.0, 0.0, 1.0)
    edge = edge * edge * (3.0 - 2.0 * edge)
    along = np.clip(1.0 - t, 0.0, 1.0) ** 1.2
    along *= np.clip((t - 0.02) / 0.08, 0.0, 1.0)
    rng = np.random.RandomState(3)
    grit = 0.78 + 0.22 * (rng.rand(h, w) > 0.55)
    alpha = edge * along * grit * 0.92
    soft = np.asarray(
        Image.fromarray((np.clip(alpha, 0, 1) * 255).astype(np.uint8), "L").filter(ImageFilter.GaussianBlur(10))
    )
    colour = np.zeros((h, w, 4), np.uint8)
    # Darker than the soil (#A0836C) so the wash reads on the tan bed.
    colour[:, :, 0] = 62
    colour[:, :, 1] = 44
    colour[:, :, 2] = 34
    colour[:, :, 3] = soft
    Image.fromarray(colour, "RGBA").save(path)
    print("shadow", path)


if __name__ == "__main__":
    main()
