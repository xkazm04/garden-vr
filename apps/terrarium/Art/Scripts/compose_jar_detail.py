"""Cork, condensation, petal, and soil maps for the night jar.

    python apps/terrarium/Art/Scripts/compose_jar_detail.py

Cork side and top are made tileable. The script prints the wrap-around edge
diff before and after. Condensation stores a droplet mask, a highlight from an
estimated normal, and a haze mask. The normal is a Sobel of the luminance
height. It is an estimate, not a measured normal map.

Nothing is sampled from the reference frames. Outputs are hashed against them.
"""
import hashlib
import os
import sys

import cv2
import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))))
SRC = os.path.join(ROOT, "apps", "terrarium", "Assets", "Art", "Source")
TEX = os.path.join(ROOT, "apps", "terrarium", "Assets", "Art", "Textures")
RUN = os.path.join(ROOT, "orchestration", "runs", "terrarium", "T-TER-007")
REFS = [
    os.path.join(ROOT, "shared", "assets", "art-reference", "A1-03-night-moss-1.png"),
    os.path.join(ROOT, "shared", "assets", "art-reference", "A1-03-night-moss-2.png"),
]
# Light warm tan. The host rejected the dark varnished crop. Side and top share this mean.
CORK = np.array([186.0, 146.0, 104.0], np.float32)
# Measured soil in the style bible.
SOIL = np.array([13.0, 35.0, 29.0], np.float32)


def sha256(path):
    digest = hashlib.sha256()
    with open(path, "rb") as handle:
        for chunk in iter(lambda: handle.read(1 << 16), b""):
            digest.update(chunk)
    return digest.hexdigest()


def load_rgb(path):
    return np.asarray(Image.open(path).convert("RGB")).astype(np.float32)


def save_png(path, array):
    array = np.clip(np.round(array), 0, 255).astype(np.uint8)
    Image.fromarray(array).save(path)
    print("wrote", os.path.relpath(path, ROOT), array.shape)


def seam_error(image):
    lr = float(np.abs(image[:, 0].astype(np.float32) - image[:, -1].astype(np.float32)).mean())
    tb = float(np.abs(image[0].astype(np.float32) - image[-1].astype(np.float32)).mean())
    return lr, tb


def blend_center(image, axis, band):
    image = image.copy()
    count = image.shape[axis]
    center = count // 2
    band = min(band, center - 2)
    if axis == 1:
        left = image[:, center - band:center].copy()
        right = image[:, center:center + band].copy()
        fade = np.linspace(0.0, 1.0, band, dtype=np.float32)[None, :, None]
        image[:, center - band:center] = left * (1.0 - 0.5 * fade) + right[:, ::-1] * (0.5 * fade)
        fade = np.linspace(1.0, 0.0, band, dtype=np.float32)[None, :, None]
        image[:, center:center + band] = right * (1.0 - 0.5 * fade) + left[:, ::-1] * (0.5 * fade)
    else:
        top = image[center - band:center].copy()
        bot = image[center:center + band].copy()
        fade = np.linspace(0.0, 1.0, band, dtype=np.float32)[:, None, None]
        image[center - band:center] = top * (1.0 - 0.5 * fade) + bot[::-1] * (0.5 * fade)
        fade = np.linspace(1.0, 0.0, band, dtype=np.float32)[:, None, None]
        image[center:center + band] = bot * (1.0 - 0.5 * fade) + top[::-1] * (0.5 * fade)
    return image


def make_seamless(image, frac=0.12):
    height, width = image.shape[:2]
    rolled = np.roll(np.roll(image, height // 2, 0), width // 2, 1)
    rolled = blend_center(rolled, 1, max(8, int(width * frac)))
    rolled = blend_center(rolled, 0, max(8, int(height * frac)))
    return rolled


def grade_toward(image, target):
    mean = image.reshape(-1, image.shape[-1]).mean(axis=0)
    ratio = np.clip(target / np.maximum(mean, 1.0), 0.55, 1.8)
    return np.clip(image * ratio, 0, 255)


def lift_black(image, floor=28.0):
    """A stray black blob tiles as a hole. Lift only the darkest pores toward the local colour."""
    luma = image.mean(axis=2)
    blur = cv2.GaussianBlur(image, (0, 0), 6.0)
    mask = luma < floor
    if not np.any(mask):
        return image
    out = image.copy()
    out[mask] = np.maximum(image[mask], blur[mask] * 0.72)
    print("  lifted near-black pixels", int(mask.sum()))
    return out


def tileable(name, path, size, target):
    rgb = cv2.resize(load_rgb(path), (size, size), interpolation=cv2.INTER_AREA)
    rgb = lift_black(rgb)
    before = seam_error(rgb)
    rgb = make_seamless(rgb, 0.14)
    rgb = grade_toward(rgb, target)
    after = seam_error(rgb)
    mean = rgb.reshape(-1, 3).mean(axis=0)
    print("edge-diff {0} before L/R {1:.2f} T/B {2:.2f} after {3:.2f} {4:.2f} mean {5}".format(
        name, before[0], before[1], after[0], after[1], np.round(mean, 1)))
    return rgb, before, after


def condensation(path):
    """Beads on black. R = mask, G = highlight from an estimated normal, B = upper haze."""
    rgb = load_rgb(path)
    rgb = cv2.resize(rgb, (1024, 1536), interpolation=cv2.INTER_AREA)
    luma = rgb.mean(axis=2)
    plate = float(np.median(np.concatenate([
        luma[:16, :16].ravel(), luma[:16, -16:].ravel(), luma[-16:, :16].ravel(), luma[-16:, -16:].ravel()
    ])))
    height = np.clip((luma - plate) / 70.0, 0.0, 1.0)
    height = cv2.GaussianBlur(height, (0, 0), 0.7)
    # Estimated normal. Sobel of this height field, not a scanned surface.
    gx = cv2.Sobel(height, cv2.CV_32F, 1, 0, ksize=3)
    gy = cv2.Sobel(height, cv2.CV_32F, 0, 1, ksize=3)
    normal = np.stack([-gx * 3.5, -gy * 3.5, np.ones_like(height)], axis=2)
    normal /= np.maximum(np.linalg.norm(normal, axis=2, keepdims=True), 1e-5)
    light = np.array([-0.35, -0.55, 0.76], np.float32)
    light /= np.linalg.norm(light)
    facing = np.clip((normal * light).sum(axis=2), 0.0, 1.0)
    mask = np.clip((height - 0.08) / 0.22, 0.0, 1.0)
    highlight = (facing ** 1.6) * mask
    haze = cv2.GaussianBlur(mask, (0, 0), 14.0)
    # PIL y=0 is the top of the picture, which Unity stores as v=1 (upper glass).
    upper = np.linspace(1.0, 0.2, haze.shape[0], dtype=np.float32)[:, None]
    haze = np.clip(haze * upper, 0.0, 1.0)
    out = np.dstack([mask, highlight, haze]) * 255.0
    low = mask[mask.shape[0] // 2:].mean()
    high = mask[:mask.shape[0] // 2].mean()
    print("condensation mask mean low-half {0:.3f} high-half {1:.3f} (normal estimate in G)".format(low, high))
    print("condensation normal estimate: Sobel of luminance height, not a measured normal")
    return out


def petal(path):
    rgb = load_rgb(path)
    luma = rgb.mean(axis=2)
    plate = float(np.median(luma[:12, :12]))
    alpha = np.clip((luma - plate - 10.0) / 28.0, 0.0, 1.0)
    alpha = cv2.GaussianBlur(alpha, (0, 0), 0.8)
    ys, xs = np.where(alpha > 0.2)
    if len(xs) < 10:
        raise SystemExit("petal plate did not key")
    pad = 8
    y0 = max(0, int(ys.min()) - pad)
    y1 = min(rgb.shape[0], int(ys.max()) + pad + 1)
    x0 = max(0, int(xs.min()) - pad)
    x1 = min(rgb.shape[1], int(xs.max()) + pad + 1)
    crop = rgb[y0:y1, x0:x1]
    crop_a = alpha[y0:y1, x0:x1]
    # Fill the petal's own rectangle so the mesh UV gets colour, not a black margin.
    filled = crop.copy()
    hole = crop_a < 0.25
    blur = cv2.GaussianBlur(crop, (0, 0), 8.0)
    filled[hole] = blur[hole]
    filled = cv2.resize(filled, (512, 1024), interpolation=cv2.INTER_AREA)
    # The mesh is the petal. A keyed photo silhouette punched holes through that grid.
    rgba = np.dstack([filled, np.full(filled.shape[:2], 255, np.float32)])
    print("petal mean", np.round(filled.reshape(-1, 3).mean(0), 1))
    return rgba


def main():
    os.makedirs(TEX, exist_ok=True)
    os.makedirs(RUN, exist_ok=True)
    side, side_before, side_after = tileable("cork_side", os.path.join(SRC, "cork_macro.png"), 1024, CORK)
    top, top_before, top_after = tileable("cork_top", os.path.join(SRC, "cork_top_macro.png"), 1024, CORK)
    save_png(os.path.join(TEX, "cork_side.png"), side)
    save_png(os.path.join(TEX, "cork_top.png"), top)
    pair = np.concatenate([side, top], 1)
    save_png(os.path.join(RUN, "cork-side-top.png"), pair)
    repeat = np.concatenate([np.concatenate([side, side], 1), np.concatenate([side, side], 1)], 0)
    save_png(os.path.join(RUN, "cork-side-2x2.png"), repeat)

    soil_src = os.path.join(SRC, "soil.png")
    if not os.path.isfile(soil_src):
        raise SystemExit("missing generated soil plate: " + soil_src)
    soil, soil_before, soil_after = tileable("soil_band", soil_src, 1024, SOIL)
    # The lathe band is wide and short. A square tile still wraps on the cylinder.
    soil_band = cv2.resize(soil, (1024, 256), interpolation=cv2.INTER_AREA)
    save_png(os.path.join(TEX, "soil_band.png"), soil_band)
    print("edge-diff soil_band resized L/R {0:.2f} T/B {1:.2f}".format(*seam_error(soil_band)))

    drops = condensation(os.path.join(SRC, "condensation_beads.png"))
    save_png(os.path.join(TEX, "condensation.png"), drops)

    bloom = petal(os.path.join(SRC, "petal_macro.png"))
    save_png(os.path.join(TEX, "petal.png"), bloom)

    outputs = [
        os.path.join(TEX, "cork_side.png"),
        os.path.join(TEX, "cork_top.png"),
        os.path.join(TEX, "soil_band.png"),
        os.path.join(TEX, "condensation.png"),
        os.path.join(TEX, "petal.png"),
    ]
    banned = {sha256(path) for path in REFS if os.path.isfile(path)}
    for path in outputs:
        if sha256(path) in banned:
            raise SystemExit("refuses to ship a texture that matches a reference frame: " + path)
    print("provenance ok: no output matches the reference frames")

    log = os.path.join(RUN, "edge-diff.txt")
    with open(log, "w", encoding="utf-8") as handle:
        handle.write("cork_side edge-diff before L/R {0:.2f} T/B {1:.2f} after {2:.2f} {3:.2f}\n".format(
            side_before[0], side_before[1], side_after[0], side_after[1]))
        handle.write("cork_top edge-diff before L/R {0:.2f} T/B {1:.2f} after {2:.2f} {3:.2f}\n".format(
            top_before[0], top_before[1], top_after[0], top_after[1]))
        handle.write("soil_band edge-diff before L/R {0:.2f} T/B {1:.2f} after {2:.2f} {3:.2f}\n".format(
            soil_before[0], soil_before[1], soil_after[0], soil_after[1]))
        handle.write("condensation normal estimate: Sobel of luminance height, not a measured normal\n")
    print("wrote", os.path.relpath(log, ROOT))
    worst = max(side_after[0], side_after[1], top_after[0], top_after[1])
    if worst > 12.0:
        print("WARN cork edge diff still high", worst)
        return 2
    return 0


if __name__ == "__main__":
    sys.exit(main())
