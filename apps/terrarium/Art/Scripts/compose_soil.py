"""Fine loam for the soil band.

The generated soil plate (Assets/Art/Source/soil.png) was wrapped once around
the short cylinder, so its crumbs stretched into horizontal rings ("stacked
coins"). This keeps that plate, throws away anything larger than a crumb, and
writes a seamless fine grain. Nothing is sampled from the reference frames.

    python apps/terrarium/Art/Scripts/compose_soil.py
"""
import os

import cv2
import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))))
SRC = os.path.join(ROOT, "apps", "terrarium", "Assets", "Art", "Source", "soil.png")
TEX = os.path.join(ROOT, "apps", "terrarium", "Assets", "Art", "Textures", "soil_band.png")
RUN = os.path.join(ROOT, "orchestration", "runs", "terrarium", "T-TER-015")
# Dark loam, a step above the crushed #0D231D so the grain still reads under the moss.
LOAM = np.array([32.0, 52.0, 42.0], np.float32)


def seam_error(image):
    lr = float(np.abs(image[:, 0].astype(np.float32) - image[:, -1].astype(np.float32)).mean())
    tb = float(np.abs(image[0].astype(np.float32) - image[-1].astype(np.float32)).mean())
    return lr, tb


def seamless(image):
    """Offset by half and blend across the new centre, so the tile has no seam."""
    height, width = image.shape[:2]
    rolled = np.roll(np.roll(image, height // 2, 0), width // 2, 1)
    band = max(8, width // 8)
    center = width // 2
    left = rolled[:, center - band:center].copy()
    right = rolled[:, center:center + band].copy()
    fade = np.linspace(0.0, 1.0, band, dtype=np.float32)[None, :, None]
    rolled[:, center - band:center] = left * (1.0 - 0.5 * fade) + right[:, ::-1] * (0.5 * fade)
    fade = np.linspace(1.0, 0.0, band, dtype=np.float32)[None, :, None]
    rolled[:, center:center + band] = right * (1.0 - 0.5 * fade) + left[:, ::-1] * (0.5 * fade)
    band = max(8, height // 8)
    center = height // 2
    top = rolled[center - band:center].copy()
    bot = rolled[center:center + band].copy()
    fade = np.linspace(0.0, 1.0, band, dtype=np.float32)[:, None, None]
    rolled[center - band:center] = top * (1.0 - 0.5 * fade) + bot[::-1] * (0.5 * fade)
    fade = np.linspace(1.0, 0.0, band, dtype=np.float32)[:, None, None]
    rolled[center:center + band] = bot * (1.0 - 0.5 * fade) + top[::-1] * (0.5 * fade)
    return rolled


def main():
    if not os.path.isfile(SRC):
        raise SystemExit("missing generated soil plate: " + SRC)
    rgb = np.asarray(Image.open(SRC).convert("RGB")).astype(np.float32)
    rgb = cv2.resize(rgb, (1024, 1024), interpolation=cv2.INTER_AREA)
    low = cv2.GaussianBlur(rgb, (0, 0), 28.0)
    high = rgb - low
    crumb = cv2.resize(high, (256, 256), interpolation=cv2.INTER_AREA)
    crumb = seamless(crumb)
    grain = np.tile(crumb, (4, 4, 1))
    out = LOAM + grain * 0.28
    rng = np.random.RandomState(11)
    # Soft crumbs, a few millimetres across once the tile repeats on the jar. Not rings.
    crumbs = rng.randn(128, 128).astype(np.float32)
    crumbs = cv2.GaussianBlur(cv2.resize(crumbs, (1024, 1024), interpolation=cv2.INTER_LINEAR), (0, 0), 14.0)
    crumbs = (crumbs - crumbs.mean()) / (crumbs.std() + 1e-6)
    out += crumbs[..., None] * np.array([9.0, 12.0, 9.0], np.float32)
    specks = rng.rand(256, 256).astype(np.float32)
    specks = (specks > 0.992).astype(np.float32) * rng.uniform(10.0, 22.0, size=(256, 256))
    specks = cv2.GaussianBlur(specks, (0, 0), 0.8)
    specks = np.tile(specks, (4, 4))
    out += specks[..., None] * np.array([0.7, 0.85, 0.6], np.float32)
    out = np.clip(out, 0, 255)
    before = seam_error(out)
    out = seamless(out)
    after = seam_error(out)
    mean = out.reshape(-1, 3).mean(axis=0)
    os.makedirs(os.path.dirname(TEX), exist_ok=True)
    os.makedirs(RUN, exist_ok=True)
    Image.fromarray(np.clip(np.round(out), 0, 255).astype(np.uint8)).save(TEX)
    tile = np.concatenate([np.concatenate([out, out], 1), np.concatenate([out, out], 1)], 0)
    preview = cv2.resize(tile, (512, 512), interpolation=cv2.INTER_AREA)
    Image.fromarray(np.clip(np.round(preview), 0, 255).astype(np.uint8)).save(os.path.join(RUN, "soil-2x2.png"))
    print("wrote", os.path.relpath(TEX, ROOT), out.shape)
    print("edge-diff soil before L/R {0:.2f} T/B {1:.2f} after {2:.2f} {3:.2f} mean {4}".format(
        before[0], before[1], after[0], after[1], np.round(mean, 1)))


if __name__ == "__main__":
    main()
