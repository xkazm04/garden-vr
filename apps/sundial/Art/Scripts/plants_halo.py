# Warm gold pinch halo from each plant card's silhouette.
# R is the inked outline. G is a soft bloom that falls off outside the plant.
# The card is additive (Fidelity/Card, One One): those channels are masks, so
# the texture is imported linear. Interior stays empty so the plant reads through.
#
# The card shares the plant's pivot at its base, so the mask is not scaled up.
# A wide mask scaled from the base sat above the flowers and read as a flame.
#
#   python apps/sundial/Art/Scripts/plants_halo.py
from pathlib import Path

import cv2
import numpy as np
from PIL import Image

HERE = Path(__file__).resolve().parent
TEX = HERE.parents[1] / "Assets" / "Art" / "Textures"

# Kept at 1. The outline is the plant alpha, and the glow uses the card's own padding.

ARCS = ("sunrise", "midday", "dusk")
STAGES = ("seed", "sprout", "young", "leafy", "full")


def content_fraction(alpha):
    ys, xs = np.where(alpha > 24)
    if xs.size == 0:
        return 0.0
    h, w = alpha.shape
    return max((ys.max() - ys.min() + 1) / h, (xs.max() - xs.min() + 1) / w)


def halo_for(rgba):
    alpha = rgba[:, :, 3]
    out = np.zeros_like(rgba)
    mask = (alpha > 24).astype(np.uint8) * 255
    if int(mask.max()) == 0:
        return out
    closed = cv2.morphologyEx(
        mask, cv2.MORPH_CLOSE, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (5, 5))
    )
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
    wobble = 1.25 + 0.50 * np.sin(ang * 4.0) + 0.25 * np.sin(ang * 9.0 + 0.7)
    line = np.exp(-0.5 * ((dist - wobble) / 1.00) ** 2)
    line[solid > 0] = 0
    # Short falloff so the contour reads, without a pool over the petals.
    glow = np.clip(1.0 - dist / (wobble + 6.0), 0.0, 1.0) ** 1.55
    glow[solid > 0] = 0
    r = np.clip(line * 1.0, 0.0, 1.0)
    g = np.clip(glow * 0.38 + line * 0.10, 0.0, 1.0)
    a = np.clip(np.maximum(r, g), 0.0, 1.0)
    out[:, :, 0] = (r * 255.0).astype(np.uint8)
    out[:, :, 1] = (g * 255.0).astype(np.uint8)
    out[:, :, 2] = 0
    out[:, :, 3] = (a * 255.0).astype(np.uint8)
    return out


def main():
    fills = []
    for arc in ARCS:
        for stage in STAGES:
            path = TEX / ("plant_%s_%s.png" % (arc, stage))
            rgba = np.asarray(Image.open(path).convert("RGBA"))
            fills.append(content_fraction(rgba[:, :, 3]))
            halo = halo_for(rgba)
            out = TEX / ("halo_%s_%s.png" % (arc, stage))
            Image.fromarray(halo, "RGBA").save(out)
            cover = float((halo[:, :, 3] > 16).mean())
            print("%s %s cover %.3f plant_fill %.3f" % (arc, stage, cover, fills[-1]))
    print("halo masks rebuilt")


if __name__ == "__main__":
    main()
