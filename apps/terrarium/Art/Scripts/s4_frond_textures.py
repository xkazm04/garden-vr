"""Spike S4 (T-TER-043): re-grade the frond atlas and derive its thickness map.

Reads the bound atlas fern_albedo.png (two fronds, RGBA, alpha is the locked silhouette) and writes

  s4_frond_albedo.png  1024 sRGB RGBA. Same alpha. The saturated yellow-green photo is regraded to the pale
                       mint of the reference (about #9FD7A8 in the mid-tones, lighter at the pinna edges).
  s4_frond_thick.png   1024 linear RGBA. R = thickness, 0 thin (pinna edge), 1 thick (rachis and veins).
                       Barre-Brisebois GDC 2011: translucency is scaled by (1 - thickness).

Nothing is sampled from the reference frames. The hues are constants taken from the style bible and the dossier.

    python apps/terrarium/Art/Scripts/s4_frond_textures.py
"""
import os

import cv2
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
TEX = os.path.normpath(os.path.join(HERE, "..", "..", "Assets", "Art", "Textures"))

# sRGB 0..1. Shadow, mid (#9FD7A8) and light mint. The photo's luminance picks the point on this ramp.
SHADOW = np.array([0.08, 0.26, 0.19], np.float32)
MID = np.array([0.624, 0.843, 0.659], np.float32)
LIGHT = np.array([0.80, 0.95, 0.84], np.float32)
EDGE_PALE = np.array([0.86, 0.97, 0.90], np.float32)


def smooth(x):
    x = np.clip(x, 0.0, 1.0)
    return x * x * (3.0 - 2.0 * x)


def main():
    src = np.asarray(Image.open(os.path.join(TEX, "fern_albedo.png")).convert("RGBA")).astype(np.float32) / 255.0
    rgb, alpha = src[..., :3], src[..., 3]
    inside = alpha > 0.5

    # Luminance of the photo, stretched over the opaque pixels, picks the ramp position.
    lum = rgb @ np.array([0.2126, 0.7152, 0.0722], np.float32)
    lo, hi = np.percentile(lum[inside], [4, 99])
    t = np.clip((lum - lo) / max(hi - lo, 1e-4), 0.0, 1.0)
    low = SHADOW + (MID - SHADOW) * smooth(t * 2.0)[..., None]
    high = MID + (LIGHT - MID) * smooth(t * 2.0 - 1.0)[..., None]
    graded = np.where((t < 0.5)[..., None], low, high)

    # Thickness. Distance to the silhouette edge (in px) makes the pinna margins thin and the body thick. The
    # photo's dark pixels (rachis, main veins, the stem) are thick. The two terms are combined with a max.
    dist = cv2.distanceTransform(inside.astype(np.uint8), cv2.DIST_L2, 5)
    body = smooth(dist / 16.0) ** 0.8
    # Keyed edges carry a dark fringe from the plate. Ignore dark texels within 3 px of the silhouette.
    dark = smooth((lo + 0.10 - lum) / 0.16) * smooth((dist - 2.0) / 3.0)
    thick = np.clip(0.12 + 0.62 * body, 0.0, 1.0)
    thick = np.maximum(thick, 0.35 + 0.65 * dark)
    thick = np.clip(cv2.GaussianBlur(thick.astype(np.float32), (0, 0), 1.2), 0.0, 1.0)

    # Lighter, more translucent edge: pull the thin texels toward the pale mint.
    edge = (1.0 - thick) ** 1.5
    graded = graded + (EDGE_PALE - graded) * (0.28 * edge)[..., None]
    # The rachis and stem keep a dark brown-green, so the structure still reads against the pale blade.
    stem = (smooth((lo + 0.06 - lum) / 0.10) * smooth((dist - 2.0) / 3.0))[..., None]
    graded = graded + (np.array([0.16, 0.22, 0.14], np.float32) - graded) * stem

    albedo = np.dstack([np.clip(graded, 0, 1), alpha])
    Image.fromarray(np.round(albedo * 255).astype(np.uint8), "RGBA").save(os.path.join(TEX, "s4_frond_albedo.png"))
    tmap = np.dstack([thick, thick, thick, alpha])
    Image.fromarray(np.round(np.clip(tmap, 0, 1) * 255).astype(np.uint8), "RGBA").save(os.path.join(TEX, "s4_frond_thick.png"))

    m = inside
    print("opaque px %d of %d" % (m.sum(), m.size))
    print("albedo mean sRGB (0-255):", np.round(albedo[m][:, :3].mean(0) * 255, 1))
    print("thickness mean %.3f edge-ring mean %.3f" % (thick[m].mean(), thick[m & (dist < 3)].mean()))


if __name__ == "__main__":
    main()
