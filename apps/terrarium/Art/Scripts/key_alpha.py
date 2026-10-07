"""Key moss tufts and fern fronds, and make the moss albedo tile.

Run from the repo root:

    python apps/terrarium/Art/Scripts/key_alpha.py

Alpha comes from luminance distance off the plate's corner colour (pure black
on a clean plate). Fern emission is the round-3 distance-field edge glow from
tools/blender/paint_textures.py. The albedo gets a wrap-around edge blend, and
the script prints the leftover edge diff.

Nothing is sampled from shared/assets/art-reference. The script hashes its
outputs against those frames and stops if one matches.
"""
import hashlib
import os
import sys

import cv2
import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))))
SRC = os.path.join(ROOT, "apps", "terrarium", "Art", "Source")
TEX = os.path.join(ROOT, "apps", "terrarium", "Assets", "Art", "Textures")
RUN = os.path.join(ROOT, "orchestration", "runs", "terrarium", "T-TER-006")
REFS = [
    os.path.join(ROOT, "shared", "assets", "art-reference", "A1-03-night-moss-1.png"),
    os.path.join(ROOT, "shared", "assets", "art-reference", "A1-03-night-moss-2.png"),
]
MOSS_TARGET = np.array([55, 98, 34], np.float32)  # measured moss #376222


def sha256(path):
    digest = hashlib.sha256()
    with open(path, "rb") as handle:
        for chunk in iter(lambda: handle.read(1 << 16), b""):
            digest.update(chunk)
    return digest.hexdigest()


def load_rgb(path):
    image = Image.open(path).convert("RGB")
    return np.asarray(image).astype(np.float32)


def save_png(path, array):
    array = np.clip(np.round(array), 0, 255).astype(np.uint8)
    Image.fromarray(array).save(path)
    print("wrote", os.path.relpath(path, ROOT), array.shape)


def corner_colour(rgb):
    patches = np.concatenate([
        rgb[:10, :10].reshape(-1, 3),
        rgb[:10, -10:].reshape(-1, 3),
        rgb[-10:, :10].reshape(-1, 3),
        rgb[-10:, -10:].reshape(-1, 3),
    ], 0)
    return np.median(patches, axis=0)


def key_luminance(rgb):
    """Alpha from how far each pixel is from the plate colour. Straighten the fringe."""
    plate = corner_colour(rgb)
    dist = np.linalg.norm(rgb - plate, axis=2)
    alpha = np.clip((dist - 14.0) / 26.0, 0.0, 1.0)
    alpha = cv2.GaussianBlur(alpha, (0, 0), 0.6)
    safe = np.maximum(alpha, 0.18)[..., None]
    straight = np.clip((rgb - plate) / safe + plate, 0, 255)
    straight[alpha < 0.04] = 0
    alpha[alpha < 0.04] = 0
    return straight, alpha, plate


def trim(rgba, pad=12, thresh=16):
    ys, xs = np.where(rgba[..., 3] > thresh)
    if len(xs) == 0:
        return rgba
    y0 = max(0, int(ys.min()) - pad)
    y1 = min(rgba.shape[0], int(ys.max()) + pad + 1)
    x0 = max(0, int(xs.min()) - pad)
    x1 = min(rgba.shape[1], int(xs.max()) + pad + 1)
    return rgba[y0:y1, x0:x1]


def fit(rgba, width, height, pad):
    """Centre rgba on a clear canvas. The source keeps its aspect."""
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


def edge_glow(alpha):
    """paint_textures.py: distance field, bright on the silhouette, dim in the blade."""
    mask = (alpha > 0.45).astype(np.uint8)
    dist = cv2.distanceTransform(mask, cv2.DIST_L2, 5)
    edge = np.exp(-dist / 5.0) * (alpha > 0.02)
    emi = np.clip(0.30 + 0.85 * edge, 0.0, 1.6) / 1.6
    return np.dstack([emi, emi, emi]) * 255.0


def blend_center(image, axis, band):
    """Crossfade the join that an offset roll leaves in the middle of the tile.

    The outer edges are left alone. They were neighbours in the source, so they
    already meet when the tile repeats. Blending the outer edges in place paints
    a light cross on every repeat.
    """
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


def make_seamless(image, frac=0.10):
    """Roll the source so its edges meet in the centre, then hide that join."""
    height, width = image.shape[:2]
    rolled = np.roll(np.roll(image, height // 2, 0), width // 2, 1)
    rolled = blend_center(rolled, 1, max(4, int(width * frac)))
    rolled = blend_center(rolled, 0, max(4, int(height * frac)))
    return rolled


def seam_error(image):
    lr = float(np.abs(image[:, 0].astype(np.float32) - image[:, -1].astype(np.float32)).mean())
    tb = float(np.abs(image[0].astype(np.float32) - image[-1].astype(np.float32)).mean())
    return lr, tb


def flatten_lighting(image, sigma_frac=0.22):
    """Remove a broad radial glow so a repeat does not checkerboard. Cushion detail stays."""
    sigma = max(8.0, image.shape[0] * sigma_frac)
    blur = cv2.GaussianBlur(image, (0, 0), sigma)
    mean = blur.mean(axis=(0, 1), keepdims=True)
    return np.clip(image - blur + mean, 0, 255)


def grade_toward(image, target):
    mean = image.reshape(-1, image.shape[-1]).mean(axis=0)
    # The bead plate is teal. A wider floor lets the blue channel reach measured moss #376222.
    ratio = np.clip(target / np.maximum(mean, 1.0), 0.45, 1.55)
    return np.clip(image * ratio, 0, 255)


def quilt_tile(rgb, size=1024, seed=7, copies=180, patch_min=220, patch_max=380):
    """Stamp feathered patches with wrap, so the tile repeats without a landmark.

    The generated plate is one photograph with a radial glow. Small wrapped
    patches keep the cushion detail and break the glow and the seam.
    """
    rng = np.random.RandomState(seed)
    # A flat base keeps any gap from becoming a black mark. The full photograph
    # is not used as the base, because its radial glow would tile.
    mean = rgb.reshape(-1, 3).mean(axis=0)
    canvas = np.ones((size, size, 3), np.float32) * mean * 0.45
    weight = np.full((size, size, 1), 0.45, np.float32)
    height, width = rgb.shape[:2]
    for _ in range(copies):
        patch = int(rng.randint(patch_min, patch_max))
        y0 = int(rng.randint(0, height - patch))
        x0 = int(rng.randint(0, width - patch))
        src = rgb[y0:y0 + patch, x0:x0 + patch]
        span = np.arange(patch)
        fade = np.clip(np.minimum(span, patch - 1 - span) / (patch * 0.14), 0.0, 1.0)
        feather = np.minimum(fade[:, None], fade[None, :])[..., None]
        oy = int(rng.randint(0, size))
        ox = int(rng.randint(0, size))
        ys = (span + oy) % size
        xs = (span + ox) % size
        grid_y, grid_x = np.meshgrid(ys, xs, indexing="ij")
        canvas[grid_y, grid_x] += src * feather
        weight[grid_y, grid_x] += feather
    filled = np.clip(canvas / np.maximum(weight, 1e-4), 0, 255)
    print("  quilt weight min {0:.3f} near-black {1}".format(
        float(weight.min()), int((filled.max(axis=2) < 8).sum())))
    return filled


def crop_letterbox(rgb):
    """Drop a gray frame when the corners are not the black field behind the subject.

    fern_v1 arrived with a dark gray margin around a black rectangle. Keying from
    that gray corner treats the black field as solid content and leaves a box.
    """
    plate = corner_colour(rgb)
    if float(np.linalg.norm(plate)) < 10.0:
        return rgb
    luma = rgb.mean(axis=2)
    plate_luma = float(np.mean(plate))
    dark = luma < min(12.0, plate_luma * 0.55)
    row_frac = dark.mean(axis=1)
    col_frac = dark.mean(axis=0)
    rows = np.where(row_frac > 0.35)[0]
    cols = np.where(col_frac > 0.35)[0]
    if len(rows) < 16 or len(cols) < 16:
        return rgb
    cropped = rgb[int(rows[0]):int(rows[-1]) + 1, int(cols[0]):int(cols[-1]) + 1]
    print("  letterbox crop", cropped.shape[:2], "from plate", np.round(plate, 1))
    return cropped


def keyed_plate(path):
    rgb = crop_letterbox(load_rgb(path))
    straight, alpha, plate = key_luminance(rgb)
    rgba = np.dstack([straight, alpha * 255.0])
    rgba = trim(rgba)
    coverage = float((rgba[..., 3] > 16).mean())
    print("  keyed", os.path.basename(path), "plate", np.round(plate, 1), "coverage", "{:.3f}".format(coverage), "size", rgba.shape[:2])
    if coverage < 0.02 or coverage > 0.92:
        print("  WARN coverage out of range for", os.path.basename(path))
    return rgba


def moss_albedo():
    # The cushion plate keeps dark gaps between clumps. The bead plate is the fallback.
    cushion = os.path.join(SRC, "moss_cushion.png")
    src_name = "moss_cushion.png" if os.path.isfile(cushion) else "moss_albedo.png"
    rgb = load_rgb(os.path.join(SRC, src_name))
    rgb = cv2.resize(rgb, (1024, 1024), interpolation=cv2.INTER_AREA)
    before = seam_error(rgb)
    print("  moss source", src_name)
    rgb = quilt_tile(rgb, seed=21, copies=160, patch_min=240, patch_max=360)
    rgb = grade_toward(rgb, MOSS_TARGET)
    # Open the crevices. A flat grade reads as a smooth bulb once the rim light is on.
    mean = rgb.reshape(-1, 3).mean(axis=0)
    rgb = np.clip((rgb - mean) * 1.65 + mean, 0, 255)
    rgb = grade_toward(rgb, MOSS_TARGET)
    after = seam_error(rgb)
    final = after
    mean = rgb.reshape(-1, 3).mean(axis=0)
    print("  moss_macro seam before L/R {0:.2f} T/B {1:.2f} after {2:.2f} {3:.2f} final {4:.2f} {5:.2f} mean {6}".format(
        before[0], before[1], after[0], after[1], final[0], final[1], np.round(mean, 1)))
    save_png(os.path.join(TEX, "moss_macro.png"), rgb)
    tile = np.concatenate([np.concatenate([rgb, rgb], 1), np.concatenate([rgb, rgb], 1)], 0)
    os.makedirs(RUN, exist_ok=True)
    save_png(os.path.join(RUN, "moss-albedo-2x2.png"), tile)
    return final


def moss_atlas():
    cells = []
    for index in range(4):
        path = os.path.join(SRC, "moss_tuft_%d.png" % index)
        if not os.path.isfile(path) and index == 3:
            # The fourth plate was rate-limited. A flip of the lopsided tuft fills the cell.
            flipped = keyed_plate(os.path.join(SRC, "moss_tuft_2.png"))
            flipped = flipped[:, ::-1].copy()
            cells.append(fit(flipped, 256, 256, 18))
            print("  moss_tuft_3 is a horizontal flip of moss_tuft_2")
            continue
        cells.append(fit(keyed_plate(path), 256, 256, 18))
    order = [
        (0, False), (1, False), (2, False), (3, False),
        (3, True), (0, True), (1, False), (2, True),
        (2, False), (3, False), (0, True), (1, True),
        (1, False), (2, True), (3, False), (0, True),
    ]
    atlas = np.zeros((1024, 1024, 4), np.float32)
    for n, (src, flip) in enumerate(order):
        cell = cells[src][:, ::-1].copy() if flip else cells[src]
        row, col = divmod(n, 4)
        y, x = row * 256, col * 256
        atlas[y:y + 256, x:x + 256] = cell
    save_png(os.path.join(TEX, "moss_card.png"), atlas)
    return atlas


def fern_atlas():
    columns = []
    glows = []
    for index in range(3):
        rgba = fit(keyed_plate(os.path.join(SRC, "fern_v%d.png" % index)), 512, 1024, 16)
        columns.append(rgba)
        glows.append(edge_glow(rgba[..., 3] / 255.0))
    albedo = np.concatenate(columns, 1)
    emission = np.concatenate(glows, 1)
    save_png(os.path.join(TEX, "fern_albedo.png"), albedo)
    save_png(os.path.join(TEX, "fern_emission.png"), emission)
    return albedo


def refuse_reference_pixels(paths):
    banned = {sha256(path) for path in REFS if os.path.isfile(path)}
    for path in paths:
        digest = sha256(path)
        if digest in banned:
            raise SystemExit("refuses to ship a texture that matches a reference frame: " + path)
    print("provenance ok: no output matches the reference frames")


def main():
    os.makedirs(TEX, exist_ok=True)
    required = ["moss_albedo.png", "moss_tuft_0.png", "moss_tuft_1.png", "moss_tuft_2.png",
                "fern_v0.png", "fern_v1.png", "fern_v2.png"]
    missing = [name for name in required if not os.path.isfile(os.path.join(SRC, name))]
    if missing:
        raise SystemExit("missing source plates: " + ", ".join(missing))
    final = moss_albedo()
    moss_atlas()
    fern_atlas()
    outputs = [
        os.path.join(TEX, "moss_macro.png"),
        os.path.join(TEX, "moss_card.png"),
        os.path.join(TEX, "fern_albedo.png"),
        os.path.join(TEX, "fern_emission.png"),
    ]
    refuse_reference_pixels(outputs)
    if final[0] > 8 or final[1] > 8:
        print("WARN moss edge diff still high", final)
        return 2
    return 0


if __name__ == "__main__":
    sys.exit(main())
