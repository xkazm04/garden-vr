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
SRC = os.path.normpath(os.path.join(HERE, "..", "..", "Art", "Source", "paint"))

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
    # The Nano Banana plate is fitted by fit_notebook.py. This entry only
    # refreshes the painted gnomon shadow, using that script's wash.
    print("compose_paint: dial face is owned by fit_notebook.py; shadow only")
    from fit_notebook import paint_shadow as paint
    paint(os.path.join(TEX, "gnomon_shadow.png"))


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
