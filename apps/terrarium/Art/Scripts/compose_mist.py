"""Compose an 8x8 steam flipbook from generated wisps on black.

    python apps/terrarium/Art/Scripts/compose_mist.py

Each cell is one frame of a soft plume: wisps rise, curl, and thin out toward
the top. Frame 0 sits at the bottom-left of the sheet so Unity's v=0 offset
shows it. The sheet replaces the five loop cards with one flipbook texture.
"""
import glob
import math
import os

import cv2
import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))))
SRC = os.path.join(ROOT, "apps", "terrarium", "Art", "Source")
TEX = os.path.join(ROOT, "apps", "terrarium", "Assets", "Art", "Textures")
RUN = os.path.join(ROOT, "orchestration", "runs", "terrarium", "T-TER-015")
GRID = 8
CELL = 256
FRAMES = GRID * GRID


def load_rgb(path):
    return np.asarray(Image.open(path).convert("RGB")).astype(np.float32)


def corner_colour(rgb):
    patches = np.concatenate([
        rgb[:12, :12].reshape(-1, 3),
        rgb[:12, -12:].reshape(-1, 3),
        rgb[-12:, :12].reshape(-1, 3),
        rgb[-12:, -12:].reshape(-1, 3),
    ], 0)
    return np.median(patches, axis=0)


def key_wisp(path):
    rgb = load_rgb(path)
    plate = corner_colour(rgb)
    dist = np.linalg.norm(rgb - plate, axis=2)
    alpha = np.clip((dist - 16.0) / 36.0, 0.0, 1.0)
    alpha = cv2.GaussianBlur(alpha, (0, 0), 1.1)
    alpha[alpha < 0.05] = 0.0
    coverage = float((alpha > 0.08).mean())
    # Dense plates are veils. Keep their shape, but do not let them fill the cell.
    alpha *= min(1.0, 0.28 / max(coverage, 0.05))
    soft = np.clip(rgb, 0, 220)
    print("  wisp", os.path.basename(path), "plate", np.round(plate, 1), "coverage", "{:.3f}".format(coverage))
    return soft, alpha


def smooth(edge0, edge1, value):
    span = max(1e-4, edge1 - edge0)
    t = np.clip((value - edge0) / span, 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def soft_field(alpha):
    """Blur a wisp into a density field. The plume shape comes from the curl, not the silhouette."""
    small = cv2.resize(alpha, (96, 144), interpolation=cv2.INTER_AREA)
    small = cv2.GaussianBlur(small, (0, 0), 7.0)
    small = small / max(float(small.max()), 1e-4)
    return small


def frame(wisps, index):
    """One soft plume. y=0 in the buffer is the top of the cell.

    Wide and soft at the cork, thinner as it rises, with a slow curl. Wisps
    punch holes through the body so it is not a solid bell and not two ribbons.
    """
    t = index / float(FRAMES)
    fields = [soft_field(alpha) for _color, alpha in wisps]
    yy, xx = np.mgrid[0:CELL, 0:CELL]
    x = xx / (CELL - 1.0) - 0.5
    y = 1.0 - yy / (CELL - 1.0)  # 0 at the cork, 1 at the thin top
    sway = math.sin(t * math.tau) * 0.06
    curl = np.sin(y * math.pi * 1.15 + t * math.tau) * (0.02 + 0.20 * y)
    spine = sway * (0.2 + y) + curl
    # Wide at the lip (about half the card), a wisp by the top of the frame.
    width = 0.145 * ((1.0 - y) ** 1.15) + 0.010
    body = np.exp(-((x - spine) ** 2) / (2.0 * width ** 2))
    # Break the bell into curls. A high floor here is what filled the cone.
    gap = 0.25 + 0.75 * np.clip(0.5 + 0.5 * np.sin(y * math.pi * 3.2 - t * math.tau * 1.5 + x * 6.0), 0.0, 1.0)
    column = body * gap
    column *= smooth(0.0, 0.04, y) * (1.0 - smooth(0.50, 0.96, y))
    column *= 0.35 + 0.65 * ((1.0 - y) ** 0.65)
    density = np.zeros((CELL, CELL), np.float32)
    for i, field in enumerate(fields):
        phase = (t * 0.5 + i / float(len(fields))) % 1.0
        rolled = np.roll(field, int(phase * field.shape[0]), axis=0)
        rolled = np.roll(rolled, int((i * 17 + 5) % rolled.shape[1]), axis=1)
        sampled = cv2.resize(rolled, (CELL, CELL), interpolation=cv2.INTER_LINEAR)
        band = np.exp(-((y - (0.02 + 0.85 * phase)) ** 2) / 0.045)
        density += column * sampled * (0.35 + 0.65 * band)
    density = cv2.GaussianBlur(density, (0, 0), 2.2)
    peak = max(float(density.max()), 1e-4)
    alpha = np.clip(density / peak * 0.34, 0.0, 0.34)
    alpha[alpha < 0.02] = 0.0
    vapor = np.array([198.0, 214.0, 208.0], np.float32)
    color = np.ones((CELL, CELL, 3), np.float32) * vapor
    color[alpha < 0.02] = 0
    return color, alpha


def bleed(cell_c, cell_a, pad=3):
    """Copy the inset edge outward so bilinear filtering does not pull the next frame."""
    out_c = cell_c.copy()
    out_a = cell_a.copy()
    out_c[:pad] = out_c[pad:pad + 1]
    out_a[:pad] = out_a[pad:pad + 1]
    out_c[-pad:] = out_c[-pad - 1:-pad]
    out_a[-pad:] = out_a[-pad - 1:-pad]
    out_c[:, :pad] = out_c[:, pad:pad + 1]
    out_a[:, :pad] = out_a[:, pad:pad + 1]
    out_c[:, -pad:] = out_c[:, -pad - 1:-pad]
    out_a[:, -pad:] = out_a[:, -pad - 1:-pad]
    return out_c, out_a


def main():
    paths = sorted(glob.glob(os.path.join(SRC, "mist_wisp_*.png")))
    if len(paths) < 6:
        raise SystemExit("need 6-8 mist wisps in Art/Source, found %d" % len(paths))
    wisps = [key_wisp(path) for path in paths[:8]]
    sheet_c = np.zeros((GRID * CELL, GRID * CELL, 3), np.float32)
    sheet_a = np.zeros((GRID * CELL, GRID * CELL), np.float32)
    preview = []
    for index in range(FRAMES):
        color, alpha = frame(wisps, index)
        color, alpha = bleed(color, alpha)
        col = index % GRID
        row = index // GRID
        # Row 0 is the bottom of the PNG (Unity v=0).
        y = (GRID - 1 - row) * CELL
        x = col * CELL
        sheet_c[y:y + CELL, x:x + CELL] = color
        sheet_a[y:y + CELL, x:x + CELL] = alpha
        if index in (0, 16, 32, 48):
            # Show relative density. A flat RGB fill hides whether the plume is a solid bell.
            shown = color * (alpha / max(float(alpha.max()), 1e-4))[..., None]
            preview.append(np.dstack([shown, np.full_like(alpha, 255.0)]))
    rgba = np.dstack([sheet_c, sheet_a * 255.0])
    os.makedirs(TEX, exist_ok=True)
    os.makedirs(RUN, exist_ok=True)
    out = os.path.join(TEX, "mist.png")
    Image.fromarray(np.clip(np.round(rgba), 0, 255).astype(np.uint8)).save(out)
    contact = np.concatenate(preview, 1)
    Image.fromarray(np.clip(np.round(contact), 0, 255).astype(np.uint8)).save(os.path.join(RUN, "mist-frames.png"))
    print("wrote", os.path.relpath(out, ROOT), rgba.shape, "wisps", len(wisps), "frames", FRAMES)
    print("mist alpha mean", "{:.3f}".format(float(sheet_a.mean())), "max", "{:.3f}".format(float(sheet_a.max())))


if __name__ == "__main__":
    main()
