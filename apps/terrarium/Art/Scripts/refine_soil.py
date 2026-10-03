"""Turn the T-TER-017 soil tile from perlite gravel into fine dark loam.

soil_band.png stays the seamless crumb field from compose_nb.py. Bright grains
are what read as lava rock once the band is a mound. This pass pulls only those
grains down and shifts the mean toward measured soil #0D231D. The grade is
global, so a seamless tile stays seamless. Moss and fern plates are not touched.

    python apps/terrarium/Art/Scripts/refine_soil.py
"""
import os
import sys

import numpy as np
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "..", ".."))
TEX = os.path.join(ROOT, "apps", "terrarium", "Assets", "Art", "Textures", "soil_band.png")
# Measured soil in the style bible.
LOAM = np.array([13.0, 35.0, 29.0], np.float32)


def seam_error(rgb):
    left = np.abs(rgb[:, 0].astype(np.float32) - rgb[:, -1].astype(np.float32)).mean()
    top = np.abs(rgb[0, :].astype(np.float32) - rgb[-1, :].astype(np.float32)).mean()
    return float(left), float(top)


def main():
    src = np.asarray(Image.open(TEX).convert("RGB")).astype(np.float32)
    before_seam = seam_error(src)
    before_mean = src.reshape(-1, 3).mean(axis=0)
    luma = 0.2126 * src[:, :, 0] + 0.7152 * src[:, :, 1] + 0.0722 * src[:, :, 2]
    # Pale perlite only. The crumb below this stays.
    speck = np.clip((luma - 48.0) / 80.0, 0.0, 1.0)
    speck = speck * speck
    dark = np.array([16.0, 24.0, 20.0], np.float32)
    out = src * (1.0 - speck[..., None] * 0.88) + dark * (speck[..., None] * 0.88)
    mean = out.reshape(-1, 3).mean(axis=0)
    out = out + (LOAM - mean) * 0.62
    out = np.clip(out, 0, 255)
    after_seam = seam_error(out)
    after_mean = out.reshape(-1, 3).mean(axis=0)
    Image.fromarray(out.astype(np.uint8), "RGB").save(TEX)
    print("soil before mean", np.round(before_mean, 1), "seam", "{:.2f}".format(before_seam[0]), "{:.2f}".format(before_seam[1]))
    print("soil after  mean", np.round(after_mean, 1), "seam", "{:.2f}".format(after_seam[0]), "{:.2f}".format(after_seam[1]))
    print("speck coverage", "{:.3f}".format(float((speck > 0.15).mean())))
    return 0


if __name__ == "__main__":
    sys.exit(main())
