"""Compose the T-TER-017 Nano Banana plates into the bound jar textures.

Sources are the agy images under apps/terrarium/Art/Source (and Assets/Art/Source
if a plate was saved there). Nothing is sampled from the reference frames.

    python apps/terrarium/Art/Scripts/compose_nb.py
"""
import hashlib
import os
import sys

import cv2
import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))))
SRC_CANDIDATES = [
    os.path.join(ROOT, "apps", "terrarium", "Art", "Source"),
    os.path.join(ROOT, "apps", "terrarium", "Assets", "Art", "Source"),
]
TEX = os.path.join(ROOT, "apps", "terrarium", "Assets", "Art", "Textures")
RUN = os.path.join(ROOT, "orchestration", "runs", "terrarium", "T-TER-017")
REFS = [
    os.path.join(ROOT, "shared", "assets", "art-reference", "A1-03-night-moss-1.png"),
    os.path.join(ROOT, "shared", "assets", "art-reference", "A1-03-night-moss-2.png"),
]
# Measured moss in the style bible. The grade keeps the dark gaps and the bright tips.
MOSS = np.array([55.0, 98.0, 34.0], np.float32)
LOAM = np.array([22.0, 30.0, 24.0], np.float32)


def sha256(path):
    digest = hashlib.sha256()
    with open(path, "rb") as handle:
        for chunk in iter(lambda: handle.read(1 << 16), b""):
            digest.update(chunk)
    return digest.hexdigest()


def find_src(name):
    for folder in SRC_CANDIDATES:
        path = os.path.join(folder, name)
        if os.path.isfile(path):
            return path
    raise SystemExit("missing source plate: " + name)


def save_png(path, array):
    array = np.clip(np.round(array), 0, 255).astype(np.uint8)
    Image.fromarray(array).save(path)
    print("wrote", os.path.relpath(path, ROOT), array.shape)


def load_rgb(name):
    return np.asarray(Image.open(find_src(name)).convert("RGB")).astype(np.float32)


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


def make_seamless(image, frac=0.08):
    height, width = image.shape[:2]
    rolled = np.roll(np.roll(image, height // 2, 0), width // 2, 1)
    rolled = blend_center(rolled, 1, max(4, int(width * frac)))
    rolled = blend_center(rolled, 0, max(4, int(height * frac)))
    return rolled


def grade_contrast(image, target, contrast):
    mean = image.reshape(-1, image.shape[-1]).mean(axis=0)
    shifted = (image - mean) * contrast + target
    return np.clip(shifted, 0, 255)


def corner_colour(rgb):
    patches = np.concatenate([
        rgb[:12, :12].reshape(-1, 3),
        rgb[:12, -12:].reshape(-1, 3),
        rgb[-12:, :12].reshape(-1, 3),
        rgb[-12:, -12:].reshape(-1, 3),
    ], 0)
    return np.median(patches, axis=0)


def key_luminance(rgb):
    plate = corner_colour(rgb)
    dist = np.linalg.norm(rgb - plate, axis=2)
    alpha = np.clip((dist - 12.0) / 22.0, 0.0, 1.0)
    alpha = cv2.GaussianBlur(alpha, (0, 0), 0.7)
    # Feather the silhouette so a card cutoff is not a hard rectangle.
    alpha = np.clip(alpha * 1.05, 0.0, 1.0)
    safe = np.maximum(alpha, 0.18)[..., None]
    straight = np.clip((rgb - plate) / safe + plate, 0, 255)
    straight[alpha < 0.03] = 0
    alpha[alpha < 0.03] = 0
    return straight, alpha, plate


def trim(rgba, pad=8, thresh=12):
    ys, xs = np.where(rgba[..., 3] > thresh)
    if len(xs) == 0:
        return rgba
    y0 = max(0, int(ys.min()) - pad)
    y1 = min(rgba.shape[0], int(ys.max()) + pad + 1)
    x0 = max(0, int(xs.min()) - pad)
    x1 = min(rgba.shape[1], int(xs.max()) + pad + 1)
    return rgba[y0:y1, x0:x1]


def fit(rgba, width, height, pad):
    canvas = np.zeros((height, width, 4), np.float32)
    inner_w = max(1, width - pad * 2)
    inner_h = max(1, height - pad * 2)
    sh, sw = rgba.shape[:2]
    scale = min(inner_w / max(sw, 1), inner_h / max(sh, 1))
    nw = max(1, int(round(sw * scale)))
    nh = max(1, int(round(sh * scale)))
    resized = cv2.resize(rgba, (nw, nh), interpolation=cv2.INTER_AREA)
    x = (width - nw) // 2
    y = (height - nh) // 2
    canvas[y:y + nh, x:x + nw] = resized
    return canvas


def key_green(rgb):
    """Luminance key, then drop a gray studio floor. Yellow tips stay; gray does not."""
    straight, alpha, plate = key_luminance(rgb)
    sat = rgb.max(axis=2) - rgb.min(axis=2)
    luma = rgb.mean(axis=2)
    floor = (sat < 16.0) & (luma < 36.0)
    alpha = alpha.copy()
    straight = straight.copy()
    alpha[floor] = 0
    straight[floor] = 0
    return straight, alpha, plate


def keyed_moss(name):
    rgb = load_rgb(name)
    straight, alpha, plate = key_green(rgb)
    rgba = np.dstack([straight, alpha * 255.0])
    rgba = trim(rgba)
    coverage = float((rgba[..., 3] > 16).mean())
    print("  keyed", name, "plate", np.round(plate, 1), "coverage", "{:.3f}".format(coverage), "size", rgba.shape[:2])
    return rgba


def keyed_plate(name):
    rgb = load_rgb(name)
    straight, alpha, plate = key_luminance(rgb)
    rgba = np.dstack([straight, alpha * 255.0])
    rgba = trim(rgba)
    coverage = float((rgba[..., 3] > 16).mean())
    print("  keyed", name, "plate", np.round(plate, 1), "coverage", "{:.3f}".format(coverage), "size", rgba.shape[:2])
    return rgba


def grade_rgba(rgba, target):
    rgb = rgba[..., :3]
    alpha = rgba[..., 3:4] / 255.0
    mask = alpha[..., 0] > 0.2
    if mask.sum() < 10:
        return rgba
    mean = rgb[mask].mean(axis=0)
    scale = np.clip(target / np.maximum(mean, 1.0), 0.55, 1.6)
    out = rgba.copy()
    out[..., :3] = np.clip(rgb * scale, 0, 255)
    out[..., :3] *= np.clip(alpha + (1.0 - alpha) * 0.0, 0, 1)
    return out


def moss_tile():
    rgb = load_rgb("moss_nb_tile.png")
    rgb = cv2.resize(rgb, (1024, 1024), interpolation=cv2.INTER_AREA)
    before = seam_error(rgb)
    rgb = make_seamless(rgb, 0.07)
    rgb = grade_contrast(rgb, MOSS, 1.05)
    # Bright tips stay, but a hard highlight becomes a yellow pea once the jar is small.
    knee = 150.0
    hot = rgb > knee
    rgb = np.where(hot, knee + (rgb - knee) * 0.40, rgb)
    after = seam_error(rgb)
    mean = rgb.reshape(-1, 3).mean(axis=0)
    print("  moss_macro seam", "{:.2f}".format(before[0]), "{:.2f}".format(before[1]),
          "->", "{:.2f}".format(after[0]), "{:.2f}".format(after[1]), "mean", np.round(mean, 1))
    save_png(os.path.join(TEX, "moss_macro.png"), rgb)
    tile = np.concatenate([np.concatenate([rgb, rgb], 1), np.concatenate([rgb, rgb], 1)], 0)
    os.makedirs(RUN, exist_ok=True)
    save_png(os.path.join(RUN, "moss-tile-2x2.png"), tile)
    return after


def tuft_sprite(rgba, seed):
    """Interior moss only, cut into a soft standing tuft.

    A whole-bush photo stays a round plate no matter how the card is tilted.
    The card alpha is a tuft, and the colour is the leafy centre.
    """
    height, width = rgba.shape[:2]
    ch = max(8, int(height * 0.46))
    cw = max(8, int(width * 0.46))
    y0 = max(0, (height - ch) // 2)
    x0 = max(0, (width - cw) // 2)
    crop = rgba[y0:y0 + ch, x0:x0 + cw].copy()
    rgb = crop[..., :3]
    mask = (crop[..., 3] > 40).astype(np.float32)
    acc = rgb * mask[..., None]
    weight = mask.copy()
    for _ in range(14):
        acc = cv2.GaussianBlur(acc, (0, 0), 2.4)
        weight = cv2.GaussianBlur(weight, (0, 0), 2.4)
        acc = np.where(mask[..., None] > 0.5, rgb, acc)
        weight = np.where(mask > 0.5, 1.0, weight)
    rgb = acc / np.maximum(weight, 1e-3)[..., None]
    # Yellow tips read as spots once the card is small. Keep the green.
    rgb[..., 0] = np.minimum(rgb[..., 0], rgb[..., 1] * 0.70)
    rgb[..., 2] = np.minimum(rgb[..., 2], rgb[..., 1] * 0.58)
    yy, xx = np.mgrid[0:ch, 0:cw]
    nx = (xx - cw * 0.5) / (cw * 0.5)
    ny = (yy - ch * 0.45) / (ch * 0.5)
    # A broad soft blob. A strand silhouette becomes a leaf once the card is a centimetre wide.
    tuft = np.clip(1.05 - (nx * nx) / 0.72 - (ny * ny) / 0.62, 0.0, 1.0).astype(np.float32)
    tuft = cv2.GaussianBlur(tuft, (0, 0), max(1.2, ch * 0.012))
    tuft = np.clip(tuft, 0.0, 1.0)
    crop[..., :3] = rgb * tuft[..., None]
    crop[..., 3] = tuft * 255.0
    return crop


def moss_atlas():
    """Tuft photos supply the soft shape. The colour is the cushion tile.

    A card coloured with the whole-bush photo reads as a leaf. The same shape
    filled with the macro tile reads as more of the mound.
    """
    macro = grade_contrast(cv2.resize(load_rgb("moss_nb_tile.png"), (1024, 1024), interpolation=cv2.INTER_AREA), MOSS, 1.15)
    rng = np.random.RandomState(5)
    cells = []
    for index in range(6):
        sprite = tuft_sprite(grade_rgba(keyed_moss("moss_nb_tuft_%d.png" % index), MOSS), index)
        ch, cw = sprite.shape[:2]
        # The whole tile, shifted, so each card is fine cushions and not a few big peas.
        y0 = int(rng.randint(0, 1024))
        x0 = int(rng.randint(0, 1024))
        patch = cv2.resize(np.roll(np.roll(macro, y0, 0), x0, 1), (cw, ch), interpolation=cv2.INTER_AREA)
        alpha = sprite[..., 3:4] / 255.0
        sprite = sprite.copy()
        sprite[..., :3] = patch * alpha
        cells.append(fit(sprite, 256, 256, 2))
    order = [
        (0, False), (1, False), (2, False), (3, False),
        (4, False), (5, False), (1, True), (0, True),
        (3, True), (2, True), (5, True), (4, True),
        (0, False), (4, True), (2, False), (5, False),
    ]
    atlas = np.zeros((1024, 1024, 4), np.float32)
    for n, (src, flip) in enumerate(order):
        cell = cells[src][:, ::-1].copy() if flip else cells[src]
        row, col = divmod(n, 4)
        y, x = row * 256, col * 256
        atlas[y:y + 256, x:x + 256] = cell
    save_png(os.path.join(TEX, "moss_card.png"), atlas)
    os.makedirs(RUN, exist_ok=True)
    save_png(os.path.join(RUN, "moss-tufts.png"), atlas)


def vein_emission(rgba):
    rgb = rgba[..., :3]
    alpha = rgba[..., 3] / 255.0
    luma = (0.25 * rgb[..., 0] + 0.60 * rgb[..., 1] + 0.15 * rgb[..., 2])
    blur = cv2.GaussianBlur(luma, (0, 0), 5.0)
    vein = np.clip((blur - luma) / 22.0, 0.0, 1.0)
    blade = np.clip(alpha * (0.45 + 0.55 * np.clip((luma / 140.0), 0.0, 1.0)), 0.0, 1.0)
    mask = (alpha > 0.15).astype(np.uint8)
    dist = cv2.distanceTransform(mask, cv2.DIST_L2, 5)
    edge = np.exp(-dist / 4.0) * (alpha > 0.02)
    # Bright between the veins, darker on the veins, so backlight reads as a leaf and not a mint card.
    em = np.clip(blade * (1.0 - 0.62 * vein) * 0.85 + edge * 0.28, 0.0, 1.0)
    em[alpha < 0.02] = 0
    return np.dstack([em, em, em]) * 255.0


def fit_top(rgba, width, height, pad):
    """Scale to fit and pin the tip to the top of the cell. Mesh UV v=1 is that tip."""
    canvas = np.zeros((height, width, 4), np.float32)
    inner_w = max(1, width - pad * 2)
    inner_h = max(1, height - pad * 2)
    sh, sw = rgba.shape[:2]
    scale = min(inner_w / max(sw, 1), inner_h / max(sh, 1))
    nw = max(1, int(round(sw * scale)))
    nh = max(1, int(round(sh * scale)))
    resized = cv2.resize(rgba, (nw, nh), interpolation=cv2.INTER_AREA)
    x = (width - nw) // 2
    y = pad
    canvas[y:y + nh, x:x + nw] = resized
    return canvas


def fern_atlas():
    # Two fronds, two columns, 1024 square. A third column would push the atlas past the 1024 cap
    # or clip the pinnae. FrondV2 reuses the first painting with a different curl.
    plates = [keyed_plate("fern_nb_a.png"), keyed_plate("fern_nb_b.png")]
    columns = []
    glows = []
    for rgba in plates:
        fitted = fit_top(rgba, 512, 1024, 6)
        columns.append(fitted)
        glows.append(vein_emission(fitted))
    albedo = np.concatenate(columns, 1)
    emission = np.concatenate(glows, 1)
    save_png(os.path.join(TEX, "fern_albedo.png"), albedo)
    save_png(os.path.join(TEX, "fern_emission.png"), emission)
    os.makedirs(RUN, exist_ok=True)
    save_png(os.path.join(RUN, "fern-closeup.png"), albedo)


def quilt_tile(rgb, size=1024, seed=4, copies=90, patch_min=140, patch_max=240):
    """Toroidal patches over a half-rolled base.

    A mirror offset draws a cross. An empty canvas leaves black holes where the
    patches miss. The roll makes the tile edges match; the patches cover the
    seam that the roll leaves in the middle.
    """
    src = cv2.resize(np.clip(rgb, 0, 255).astype(np.float32), (size, size), interpolation=cv2.INTER_AREA)
    canvas = np.roll(np.roll(src, size // 2, 0), size // 2, 1)
    rng = np.random.RandomState(seed)
    for _ in range(copies):
        patch = int(rng.randint(patch_min, min(patch_max, size - 1)))
        y0 = int(rng.randint(0, size - patch))
        x0 = int(rng.randint(0, size - patch))
        sample = src[y0:y0 + patch, x0:x0 + patch]
        span = np.arange(patch, dtype=np.float32)
        fade = np.clip(np.minimum(span, patch - 1.0 - span) / (patch * 0.22), 0.0, 1.0)
        feather = np.minimum(fade[:, None], fade[None, :])
        feather = (feather * feather * (3.0 - 2.0 * feather))[..., None]
        ys = (np.arange(patch) + int(rng.randint(0, size))) % size
        xs = (np.arange(patch) + int(rng.randint(0, size))) % size
        grid_y, grid_x = np.meshgrid(ys, xs, indexing="ij")
        canvas[grid_y, grid_x] = canvas[grid_y, grid_x] * (1.0 - feather) + sample * feather
    return np.clip(canvas, 0, 255)


def soil_tile():
    rgb = load_rgb("soil_nb.png")
    rgb = cv2.resize(rgb, (1024, 1024), interpolation=cv2.INTER_AREA)
    low = cv2.GaussianBlur(rgb, (0, 0), 18.0)
    high = rgb - low
    base = grade_contrast(low, LOAM, 0.85)
    src = np.clip(base + high * 0.9, 0, 255)
    before = seam_error(src)
    out = quilt_tile(src)
    after = seam_error(out)
    mean = out.reshape(-1, 3).mean(axis=0)
    print("  soil seam", "{:.2f}".format(before[0]), "{:.2f}".format(before[1]),
          "->", "{:.2f}".format(after[0]), "{:.2f}".format(after[1]), "mean", np.round(mean, 1))
    save_png(os.path.join(TEX, "soil_band.png"), out)
    tile = np.concatenate([np.concatenate([out, out], 1), np.concatenate([out, out], 1)], 0)
    save_png(os.path.join(RUN, "soil-tile-2x2.png"), tile)


def fiddle_tile():
    """Thick hair strokes. One-pixel hairs vanish once the crozier is a centimetre tall."""
    rgb = load_rgb("fiddle_nb.png")
    rgb = cv2.resize(rgb, (512, 512), interpolation=cv2.INTER_AREA)
    low = cv2.GaussianBlur(rgb, (0, 0), 22.0)
    hairs = rgb - low
    base = np.array([62.0, 118.0, 44.0], np.float32)
    fuzzy = np.clip(base + hairs * 1.35, 0, 255)
    mag = np.linalg.norm(hairs, axis=2)
    lo = float(np.percentile(mag, 88))
    hi = float(np.percentile(mag, 98))
    stroke = np.clip((mag - lo) / max(hi - lo, 1.0), 0.0, 1.0).astype(np.float32)
    stroke = cv2.dilate(stroke, np.ones((3, 3), np.uint8), iterations=1)
    stroke = cv2.GaussianBlur(stroke, (0, 0), 0.7)
    pale = np.array([226.0, 242.0, 214.0], np.float32)
    crop = fuzzy * (1.0 - stroke[..., None] * 0.72) + pale * (stroke[..., None] * 0.72)
    crop = quilt_tile(crop, size=512, seed=9, copies=70, patch_min=80, patch_max=160)
    print("  fiddle hairs mean", np.round(crop.reshape(-1, 3).mean(axis=0), 1),
          "coverage", "{:.2f}".format(float((stroke > 0.25).mean())))
    save_png(os.path.join(TEX, "fiddle_hairs.png"), crop)
    save_png(os.path.join(RUN, "fiddle-hairs.png"), crop)


def refuse_reference_pixels(paths):
    banned = {sha256(path) for path in REFS if os.path.isfile(path)}
    for path in paths:
        if sha256(path) in banned:
            raise SystemExit("refuses to ship a texture that matches a reference frame: " + path)
    print("provenance ok")


def main():
    os.makedirs(TEX, exist_ok=True)
    os.makedirs(RUN, exist_ok=True)
    moss_tile()
    moss_atlas()
    fern_atlas()
    soil_tile()
    fiddle_tile()
    outputs = [
        os.path.join(TEX, "moss_macro.png"),
        os.path.join(TEX, "moss_card.png"),
        os.path.join(TEX, "fern_albedo.png"),
        os.path.join(TEX, "fern_emission.png"),
        os.path.join(TEX, "soil_band.png"),
        os.path.join(TEX, "fiddle_hairs.png"),
    ]
    refuse_reference_pixels(outputs)
    return 0


if __name__ == "__main__":
    sys.exit(main())
