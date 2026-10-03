# A soft gold halo from each plant card's silhouette.
# One closed mass, so the line does not bubble between leaves.
# The line sits just outside the mass and its width wobbles with angle.
#
#   python apps/sundial/Art/Scripts/plants_halo.py
from pathlib import Path

import cv2
import numpy as np
from PIL import Image

HERE = Path(__file__).resolve().parent
TEX = HERE.parents[1] / "Assets" / "Art" / "Textures"

# halo.gold from docs/art/sundial-style.md
GOLD = np.array([245, 199, 106], np.uint8)

ARCS = ("sunrise", "midday", "dusk")
STAGES = ("seed", "sprout", "young", "leafy", "full")


def halo_for(rgba):
    alpha = rgba[:, :, 3]
    mask = (alpha > 24).astype(np.uint8) * 255
    if int(mask.max()) == 0:
        return np.zeros_like(rgba)
    closed = cv2.morphologyEx(
        mask, cv2.MORPH_CLOSE, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (11, 11))
    )
    # Fill holes so a gap between leaves does not grow a second ring.
    holes = (closed == 0).astype(np.uint8) * 255
    flood = holes.copy()
    cv2.floodFill(flood, np.zeros((flood.shape[0] + 2, flood.shape[1] + 2), np.uint8), (0, 0), 128)
    solid = np.where(flood == 128, np.uint8(0), np.uint8(255))
    outside = (solid == 0).astype(np.uint8)
    dist = cv2.distanceTransform(outside, cv2.DIST_L2, 5)
    ys, xs = np.indices(alpha.shape)
    weight = (solid > 0).astype(np.float32)
    cy = float((ys * weight).sum() / max(1.0, weight.sum()))
    cx = float((xs * weight).sum() / max(1.0, weight.sum()))
    ang = np.arctan2(ys - cy, xs - cx)
    wobble = 1.7 + 0.85 * np.sin(ang * 3.0) + 0.45 * np.sin(ang * 7.0 + 1.2)
    line = np.exp(-0.5 * ((dist - wobble) / 1.15) ** 2)
    line[solid > 0] = 0
    glow = np.clip(1.0 - dist / (wobble + 8.0), 0.0, 1.0) ** 1.7
    glow[solid > 0] = 0
    strength = np.clip(line * 0.95 + glow * 0.22, 0.0, 1.0)
    out = np.zeros_like(rgba)
    out[:, :, 0] = GOLD[0]
    out[:, :, 1] = GOLD[1]
    out[:, :, 2] = GOLD[2]
    out[:, :, 3] = np.where(strength > 0.02, strength * 255.0, 0).astype(np.uint8)
    return out


def main():
    for arc in ARCS:
        for stage in STAGES:
            path = TEX / ("plant_%s_%s.png" % (arc, stage))
            rgba = np.asarray(Image.open(path).convert("RGBA"))
            halo = halo_for(rgba)
            out = TEX / ("halo_%s_%s.png" % (arc, stage))
            Image.fromarray(halo, "RGBA").save(out)
            cover = float((halo[:, :, 3] > 16).mean())
            print("%s %s cover %.3f" % (arc, stage, cover))


if __name__ == "__main__":
    main()
