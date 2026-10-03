"""Compose the T-TER-032 moss maps from the Nano Banana plates.

Sources live in apps/terrarium/Art/Source:
moss_carpet_nb.png and moss_sprig_0.png through moss_sprig_5.png.
Nothing is sampled from the A1-03 reference frames.

Writes moss_macro.png (the mound) and moss_card.png (8x8 tuft atlas).
Ferns, cork, soil, and glass are not touched.

    python apps/terrarium/Art/Scripts/compose_moss.py
"""
import os

import cv2
import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))))
SRC = os.path.join(ROOT, "apps", "terrarium", "Art", "Source")
TEX = os.path.join(ROOT, "apps", "terrarium", "Assets", "Art", "Textures")
# Measured moss in the style bible. Gaps stay darker than this; tips stay brighter.
# Brighter than the bible swatch. The swatch is a shadow gap; the carpet has to glow.
MOSS = np.array([72.0, 128.0, 42.0], np.float32)


def save_png(path, array):
    array = np.clip(np.round(array), 0, 255).astype(np.uint8)
    Image.fromarray(array).save(path)
    print("wrote", os.path.relpath(path, ROOT), array.shape)


def load_rgb(name):
    path = os.path.join(SRC, name)
    if not os.path.isfile(path):
        raise SystemExit("missing source plate: " + path)
    return np.asarray(Image.open(path).convert("RGB")).astype(np.float32)


def seam_error(image):
    lr = float(np.abs(image[:, 0] - image[:, -1]).mean())
    tb = float(np.abs(image[0] - image[-1]).mean())
    return lr, tb


def blend_center(image, axis, band):
    image = image.copy()
    count = image.shape[axis]
    center = count // 2
    band = min(band, center - 2)
    fade_up = np.linspace(0.0, 1.0, band, dtype=np.float32)
    fade_down = np.linspace(1.0, 0.0, band, dtype=np.float32)
    if axis == 1:
        left = image[:, center - band:center].copy()
        right = image[:, center:center + band].copy()
        fade = fade_up[None, :, None]
        image[:, center - band:center] = left * (1.0 - 0.5 * fade) + right[:, ::-1] * (0.5 * fade)
        fade = fade_down[None, :, None]
        image[:, center:center + band] = right * (1.0 - 0.5 * fade) + left[:, ::-1] * (0.5 * fade)
    else:
        top = image[center - band:center].copy()
        bot = image[center:center + band].copy()
        fade = fade_up[:, None, None]
        image[center - band:center] = top * (1.0 - 0.5 * fade) + bot[::-1] * (0.5 * fade)
        fade = fade_down[:, None, None]
        image[center:center + band] = bot * (1.0 - 0.5 * fade) + top[::-1] * (0.5 * fade)
    return image


def make_seamless(image, frac=0.08):
    height, width = image.shape[:2]
    rolled = np.roll(np.roll(image, height // 2, 0), width // 2, 1)
    rolled = blend_center(rolled, 1, max(4, int(width * frac)))
    rolled = blend_center(rolled, 0, max(4, int(height * frac)))
    return rolled


def grade_keep_tips(image, target):
    """Move the mean to the measured moss and keep the dark gaps and the bright tips."""
    flat = image.reshape(-1, image.shape[-1])
    mean = flat.mean(axis=0)
    contrast = 1.18
    shifted = (image - mean) * contrast + target
    # A hard yellow pea reads as a spot. Leave the tip brighter than the body.
    knee = 210.0
    hot = shifted > knee
    shifted = np.where(hot, knee + (shifted - knee) * 0.45, shifted)
    return np.clip(shifted, 0, 255)


def carpet():
    rgb = cv2.resize(load_rgb("moss_carpet_nb.png"), (1024, 1024), interpolation=cv2.INTER_AREA)
    # One photo across the mound. A seamless blend paints a cross through the middle.
    rgb = grade_keep_tips(rgb, MOSS)
    print("  carpet mean", np.round(rgb.reshape(-1, 3).mean(axis=0), 1), "seam", ["%.2f" % v for v in seam_error(rgb)])
    save_png(os.path.join(TEX, "moss_macro.png"), rgb)
    return rgb


def key_black(rgb):
    corners = np.concatenate([
        rgb[:16, :16].reshape(-1, 3),
        rgb[:16, -16:].reshape(-1, 3),
        rgb[-16:, :16].reshape(-1, 3),
        rgb[-16:, -16:].reshape(-1, 3),
    ], 0)
    plate = np.median(corners, axis=0)
    dist = np.linalg.norm(rgb - plate, axis=2)
    alpha = np.clip((dist - 10.0) / 28.0, 0.0, 1.0)
    alpha = cv2.GaussianBlur(alpha, (0, 0), 0.8)
    # A grey studio floor is not a leaf.
    sat = rgb.max(axis=2) - rgb.min(axis=2)
    luma = rgb.mean(axis=2)
    alpha[(sat < 14.0) & (luma < 40.0)] = 0
    # Bark and soil are not green-dominant. Keep the leaf.
    green = rgb[..., 1]
    alpha[(green + 8.0 < rgb[..., 0]) & (green < 90.0)] = 0
    alpha[alpha < 0.04] = 0
    return alpha, plate


def too_round(alpha):
    ys, xs = np.where(alpha > 0.45)
    if len(xs) < 30:
        return True
    height = int(ys.max() - ys.min() + 1)
    width = int(xs.max() - xs.min() + 1)
    fill = float((alpha > 0.45).sum()) / float(max(1, height * width))
    return fill > 0.62


def rag(alpha, seed):
    """Break a round plate into a tuft. Crevices stay, so the mound shows through."""
    height, width = alpha.shape
    rng = np.random.RandomState(seed)
    mask = alpha.copy()
    if too_round(alpha):
        yy, xx = np.mgrid[0:height, 0:width]
        for _ in range(4):
            cx = rng.uniform(0.2, 0.8) * width
            cy = rng.uniform(0.15, 0.85) * height
            rx = rng.uniform(0.07, 0.18) * width
            ry = rng.uniform(0.10, 0.28) * height
            blob = ((xx - cx) / max(rx, 1.0)) ** 2 + ((yy - cy) / max(ry, 1.0)) ** 2
            mask *= np.clip(blob / 1.1, 0.0, 1.0)
    field = rng.rand(height, width).astype(np.float32)
    field = cv2.GaussianBlur(field, (0, 0), max(1.4, height * 0.035))
    mask *= np.clip(0.20 + 1.25 * field, 0.0, 1.0)
    mask = cv2.GaussianBlur(mask, (0, 0), 0.6)
    return np.clip(mask, 0.0, 1.0)


def sprig_sprite(index, variant):
    rgb = load_rgb("moss_sprig_%d.png" % index)
    alpha, plate = key_black(rgb)
    print("  sprig", index, "variant", variant, "plate", np.round(plate, 1), "round", too_round(alpha))
    alpha = rag(alpha, index * 17 + variant * 3 + 1)
    ys, xs = np.where(alpha > 0.12)
    if len(xs) < 20:
        raise SystemExit("sprig %d keyed to nothing" % index)
    pad = 6
    y0 = max(0, int(ys.min()) - pad)
    y1 = min(rgb.shape[0], int(ys.max()) + pad + 1)
    x0 = max(0, int(xs.min()) - pad)
    x1 = min(rgb.shape[1], int(xs.max()) + pad + 1)
    rgb = rgb[y0:y1, x0:x1]
    alpha = alpha[y0:y1, x0:x1]
    opaque = alpha > 0.25
    if opaque.sum() > 10:
        mean = rgb[opaque].mean(axis=0)
        scale = np.clip(MOSS / np.maximum(mean, 1.0), 0.45, 1.7)
        rgb = np.clip(rgb * scale, 0, 255)
    # The photo's tips are at the top of the frame. Darken the base.
    height = rgb.shape[0]
    ramp = np.linspace(1.08, 0.62, height, dtype=np.float32)[:, None, None]
    rgb = np.clip(rgb * ramp, 0, 255)
    rgba = np.dstack([rgb, alpha * 255.0])
    if variant % 2 == 1:
        rgba = rgba[:, ::-1].copy()
    return rgba


def fit_cell(rgba, size):
    canvas = np.zeros((size, size, 4), np.float32)
    pad = 4
    inner = size - pad * 2
    sh, sw = rgba.shape[:2]
    scale = min(inner / max(sw, 1), inner / max(sh, 1))
    nw = max(1, int(round(sw * scale)))
    nh = max(1, int(round(sh * scale)))
    resized = cv2.resize(rgba, (nw, nh), interpolation=cv2.INTER_AREA)
    x = (size - nw) // 2
    y = pad
    canvas[y:y + nh, x:x + nw] = resized
    return canvas


def atlas():
    cells = []
    for n in range(64):
        sprite = sprig_sprite(n % 6, n // 6)
        cells.append(fit_cell(sprite, 128))
    sheet = np.zeros((1024, 1024, 4), np.float32)
    for n, cell in enumerate(cells):
        row, col = divmod(n, 8)
        y, x = row * 128, col * 128
        sheet[y:y + 128, x:x + 128] = cell
    save_png(os.path.join(TEX, "moss_card.png"), sheet)
    covered = float((sheet[..., 3] > 16).mean())
    print("  atlas coverage", "%.3f" % covered)


def main():
    carpet()
    atlas()


if __name__ == "__main__":
    main()
