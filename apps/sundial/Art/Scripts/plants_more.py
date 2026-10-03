# Cut six species strips into sundial plant cards.
# Each strip is five stages, left to right, on aged paper.
# White and pale paper become alpha. Every stage of a species shares the
# full plant's scale, and the soil base lands on one pivot.
#
#   python apps/sundial/Art/Scripts/plants_more.py
import sys
from pathlib import Path

import cv2
import numpy as np
from PIL import Image

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
from plants_key import (  # noqa: E402
    H,
    W,
    drop_paper_crust,
    extrude,
    place,
    snap_pivot,
    species_scale,
)

APP = HERE.parents[1]
SRC = APP / "Art" / "Source" / "plants"
TEX = APP / "Assets" / "Art" / "Textures"
STAGES = ("seed", "sprout", "young", "leafy", "full")

SPECIES = ("reed", "clover", "vine", "sprig", "bell", "page")

PROMPTS = {
    "reed": (
        "Five growth stages of one reed, left to right, well separated on warm off-white paper: "
        "a seed, a two-blade sprout, a starburst tuft, a leafy clump, a tall reed with seed heads. "
        "Hand-drawn ink contours of varying weight, flat cel colour, one soft shade, a soil mound "
        "under each stage. No text, no frame letters."
    ),
    "clover": (
        "The same five-stage strip as the reed, redrawn as a low rounded clover. "
        "Each stage keeps the soil mound and the gaps between plants."
    ),
    "vine": (
        "The same five-stage strip, redrawn as a vine with heart-shaped leaves. "
        "Soil mound under each stage, gaps kept, no text."
    ),
    "sprig": (
        "The same five-stage strip, redrawn as a coral berry sprig. "
        "Soil mound under each stage, gaps kept, no text."
    ),
    "bell": (
        "The same five-stage strip, redrawn as a drooping violet bellflower. "
        "Soil mound under each stage, gaps kept, no text."
    ),
    "page": (
        "The same five-stage strip, redrawn as a plant with broad page-like leaves stacked on a stem. "
        "Soil mound under each stage, gaps kept, no text."
    ),
}


def load_rgb(path):
    return np.asarray(Image.open(path).convert("RGB"))


def key_paper(rgb):
    """Aged paper drops out. Ink and the cel body stay."""
    rgb = rgb.astype(np.float32)
    r, g, b = rgb[:, :, 0], rgb[:, :, 1], rgb[:, :, 2]
    luma = 0.2126 * r + 0.7152 * g + 0.0722 * b
    chroma = np.maximum(np.maximum(r, g), b) - np.minimum(np.minimum(r, g), b)
    paper = ((luma > 214) & (chroma < 28)) | (luma > 242)
    ink = (luma < 96) | ((luma < 128) & (chroma < 36))
    far = np.maximum(np.maximum(255 - r, 255 - g), 255 - b)
    wash = np.clip(far * 3.4 + chroma * 1.6, 0, 255)
    wash = np.where(paper, 0, wash)
    alpha = np.where(paper, 0, np.where(ink, 255, wash)).astype(np.uint8)
    n, labels, stats, _ = cv2.connectedComponentsWithStats((alpha > 20).astype(np.uint8), 8)
    for i in range(1, n):
        if stats[i, cv2.CC_STAT_AREA] < 24:
            alpha[labels == i] = 0
    out = np.zeros((*alpha.shape, 4), np.uint8)
    out[:, :, :3] = np.clip(rgb, 0, 255).astype(np.uint8)
    out[:, :, 3] = alpha
    return out


def crop_margin(rgb, frac=0.035):
    h, w = rgb.shape[:2]
    y0 = int(round(h * frac))
    x0 = int(round(w * frac))
    y1 = h - y0
    x1 = w - x0
    return rgb[y0:y1, x0:x1]


def dark_columns(rgb):
    """Ink and soil. Pale wash is ignored so neighbouring mounds do not join."""
    rgb = rgb.astype(np.float32)
    luma = 0.2126 * rgb[:, :, 0] + 0.7152 * rgb[:, :, 1] + 0.0722 * rgb[:, :, 2]
    return (luma < 150).sum(axis=0)


def column_runs(col):
    on = col > 3
    # A tendril can leave a few empty columns inside one plant.
    closed = on.copy()
    gap = 0
    for i in range(len(on)):
        if on[i]:
            gap = 0
            continue
        gap += 1
        if gap <= 14:
            closed[i] = True
        else:
            for k in range(i - gap + 1, i + 1):
                closed[k] = False
            gap = 0
    runs = []
    start = None
    for i, value in enumerate(closed):
        if value and start is None:
            start = i
        if not value and start is not None:
            if i - start > 18:
                runs.append((start, i))
            start = None
    if start is not None and len(closed) - start > 18:
        runs.append((start, len(closed)))
    return runs


def five_bands(col):
    runs = column_runs(col)
    while len(runs) > 5:
        narrow = min(range(len(runs)), key=lambda i: runs[i][1] - runs[i][0])
        runs.pop(narrow)
    while len(runs) < 5:
        widest = max(range(len(runs)), key=lambda i: runs[i][1] - runs[i][0])
        x0, x1 = runs[widest]
        margin = max(12, int((x1 - x0) * 0.18))
        if x1 - x0 <= margin * 2 + 8:
            break
        segment = col[x0 + margin:x1 - margin]
        cut_at = x0 + margin + int(np.argmin(segment))
        left = (x0, cut_at)
        right = (cut_at, x1)
        if left[1] - left[0] < 24 or right[1] - right[0] < 24:
            break
        runs = runs[:widest] + [left, right] + runs[widest + 1:]
    return runs


def cut_band(rgba, x0, x1, pad=6):
    height, width = rgba.shape[:2]
    a = max(0, x0 - pad)
    b = min(width, x1 + pad)
    slab = rgba[:, a:b]
    ys, xs = np.where(slab[:, :, 3] > 20)
    if len(ys) == 0:
        return slab
    y0 = max(0, int(ys.min()) - pad)
    y1 = min(height, int(ys.max()) + pad + 1)
    x_start = max(0, int(xs.min()) - 2)
    x_end = min(slab.shape[1], int(xs.max()) + 3)
    return keep_main(slab[y0:y1, x_start:x_end].copy())


def keep_main(rgba):
    """Drop a neighbour's tendril that crossed the cut. Leaves of this plant share its width."""
    alpha = rgba[:, :, 3]
    mask = (alpha > 24).astype(np.uint8)
    n, labels, stats, _ = cv2.connectedComponentsWithStats(mask, 8)
    if n <= 2:
        return rgba
    best = 1
    for i in range(2, n):
        if stats[i, cv2.CC_STAT_AREA] > stats[best, cv2.CC_STAT_AREA]:
            best = i
    main_x = int(stats[best, cv2.CC_STAT_LEFT])
    main_r = main_x + int(stats[best, cv2.CC_STAT_WIDTH])
    keep = np.zeros(n, dtype=bool)
    keep[best] = True
    for i in range(1, n):
        if i == best:
            continue
        x = int(stats[i, cv2.CC_STAT_LEFT])
        r = x + int(stats[i, cv2.CC_STAT_WIDTH])
        area = int(stats[i, cv2.CC_STAT_AREA])
        if area > 30 and r > main_x - 6 and x < main_r + 6:
            keep[i] = True
    drop = ~keep[labels]
    drop[labels == 0] = False
    out = rgba.copy()
    out[drop, 3] = 0
    return out


def drop_edge_specks(rgba, max_area=280):
    """A neighbour that crossed the cut lands on the card edge. The plant itself is much larger."""
    alpha = rgba[:, :, 3]
    n, labels, stats, _ = cv2.connectedComponentsWithStats((alpha > 24).astype(np.uint8), 8)
    width = alpha.shape[1]
    out = rgba
    for i in range(1, n):
        area = int(stats[i, cv2.CC_STAT_AREA])
        x = int(stats[i, cv2.CC_STAT_LEFT])
        bw = int(stats[i, cv2.CC_STAT_WIDTH])
        touches = x <= 2 or x + bw >= width - 2
        if touches and area < max_area:
            if out is rgba:
                out = rgba.copy()
            out[labels == i, 3] = 0
    return out


def finish(card):
    card = drop_paper_crust(card)
    card = extrude(snap_pivot(card))
    card = drop_paper_crust(card)
    return drop_edge_specks(snap_pivot(card))


def write_png(path, rgba):
    Image.fromarray(rgba, "RGBA").save(path)


def provenance(species, stage, source_name):
    prompt = PROMPTS[species]
    return (
        "asset: plant_{0}_{1}.png\n"
        "method: keyed by apps/sundial/Art/Scripts/plants_more.py from Art/Source/plants/{2}\n"
        "prompt: {3}\n"
        "note: Imagine made the five-stage strip (reed by image_gen, the others by image_edit of that strip). "
        "The card is one stage, keyed off the paper, seated on the shared pivot. Not copied from the art reference.\n"
        "derived_from_art_reference: no\n"
    ).format(species, stage, source_name, prompt)


def one(species):
    source_name = species + "-strip.jpg"
    path = SRC / source_name
    if not path.exists():
        raise SystemExit("missing " + str(path))
    rgb = crop_margin(load_rgb(path))
    rgba = key_paper(rgb)
    bands = five_bands(dark_columns(rgb))
    print(species, "bands", bands)
    if len(bands) != 5:
        debug = SRC / (species + "-key-debug.png")
        write_png(debug, rgba)
        raise SystemExit(species + " needs 5 plants, debug " + str(debug))
    stages = [cut_band(rgba, x0, x1) for x0, x1 in bands]
    scale = species_scale(stages[-1])
    print(" ", species, "scale", round(scale, 3))
    prompt_path = SRC / (species + "-strip.prompt.txt")
    prompt_path.write_text(PROMPTS[species] + "\n", encoding="utf-8")
    for stage, piece in zip(STAGES, stages):
        card = finish(place(piece, scale))
        name = "plant_{0}_{1}.png".format(species, stage)
        write_png(TEX / name, card)
        text = provenance(species, stage, source_name)
        (TEX / (name + ".provenance.txt")).write_text(text, encoding="utf-8")
        (SRC / (name + ".provenance.txt")).write_text(text, encoding="utf-8")
        opaque = int((card[:, :, 3] > 16).sum())
        print(" ", name, "opaque", opaque)
        if opaque < 40:
            raise SystemExit(name + " keyed to nothing")


def main():
    for species in SPECIES:
        one(species)


if __name__ == "__main__":
    main()
