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
SRC = os.path.join(ROOT, "apps", "terrarium", "Assets", "Art", "Source")
TEX = os.path.join(ROOT, "apps", "terrarium", "Assets", "Art", "Textures")
RUN = os.path.join(ROOT, "orchestration", "runs", "terrarium", "T-TER-007")
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
    """Two thin curls. y=0 in the buffer is the top of the cell.

    Round 1 normalized a wide gaussian into a solid bell. These ribbons stay
    narrow, break into wisps, and thin out before the top of the frame.
    """
    t = index / float(FRAMES)
    fields = [soft_field(alpha) for _color, alpha in wisps]
    yy, xx = np.mgrid[0:CELL, 0:CELL]
    x = xx / (CELL - 1.0) - 0.5
    y = 1.0 - yy / (CELL - 1.0)  # 0 at the cork, 1 at the thin top
    sway = math.sin(t * math.tau) * 0.07
    spine_a = sway + np.sin(y * math.pi * 1.7 + t * math.tau) * (0.04 + 0.26 * y)
    spine_b = sway * 0.35 + 0.06 + np.sin(y * math.pi * 2.4 + t * math.tau + 2.0) * (0.02 + 0.14 * y)
    width_a = 0.014 + 0.030 * ((1.0 - y) ** 1.05)
    width_b = 0.009 + 0.018 * ((1.0 - y) ** 1.15)
    rib_a = np.exp(-((x - spine_a) ** 2) / (2.0 * width_a ** 2))
    rib_b = np.exp(-((x - spine_b) ** 2) / (2.0 * width_b ** 2))
    # Gaps along the rise, so a ribbon is a chain of curls and not a stroke.
    gap_a = 0.15 + 0.85 * np.clip(0.5 + 0.5 * np.sin(y * math.pi * 4.5 - t * math.tau * 2.0), 0.0, 1.0)
    gap_b = 0.15 + 0.85 * np.clip(0.5 + 0.5 * np.sin(y * math.pi * 6.5 + t * math.tau * 2.0 + 1.1), 0.0, 1.0)
    column = rib_a * gap_a + rib_b * gap_b * 0.55
    # Alpha starts at the cork. The card is tucked into the cork, so the plume leaves the lip.
    column *= smooth(0.0, 0.02, y) * (1.0 - smooth(0.62, 0.97, y))
    column *= 0.35 + 0.65 * (1.0 - y)
    density = np.zeros((CELL, CELL), np.float32)
    for i, field in enumerate(fields):
        phase = (t * 0.65 + i / float(len(fields))) % 1.0
        rolled = np.roll(field, int(phase * field.shape[0]), axis=0)
        rolled = np.roll(rolled, int((i * 23) % rolled.shape[1]), axis=1)
        sampled = cv2.resize(rolled, (CELL, CELL), interpolation=cv2.INTER_LINEAR)
        band = np.exp(-((y - (0.08 + 0.7 * phase)) ** 2) / 0.035)
        # The ribbon is there from the cork. A wisp only brightens its own band.
        density += column * (0.42 + 0.58 * sampled) * (0.4 + 0.6 * band)
    density = cv2.GaussianBlur(density, (0, 0), 1.3)
    # Leave the peak where the curls land. Normalizing filled the bell in round 1.
    alpha = np.clip(density * 1.2, 0.0, 0.40)
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
            preview.append(np.dstack([color, alpha * 255.0]))
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
