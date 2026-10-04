"""Spike S3 (T-TER-042) textures. Writes two maps next to the jar textures:

  s3_strata.png  1024x256. The soil strata that stand against the glass. Built from the generated soil_band.png
                 crumb, regraded per layer: substrate with green specks, a pale grit layer with stones, dark humus,
                 a root zone, and a thin green line where the moss meets the glass. V runs up the soil.
  s3_strand.png  512x512, linear, tiling. The strand height field for the shell moss.
                 R = strand height 0..1 (a cone per strand, strands in Voronoi clumps), G = clump shade,
                 B = per-strand tint, A = 255.

    python apps/terrarium/Art/Scripts/s3_moss_textures.py
    python apps/terrarium/Art/Scripts/s3_moss_textures.py --preview <dir>
"""
import os
import sys

import numpy as np
from PIL import Image
from scipy.ndimage import gaussian_filter

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "..", ".."))
TEX = os.path.join(ROOT, "apps", "terrarium", "Assets", "Art", "Textures")


def wrap_noise(shape, sigma, rng):
    n = gaussian_filter(rng.standard_normal(shape), sigma, mode="wrap")
    n -= n.mean()
    return n / (np.abs(n).max() + 1e-9)


def blob(arr, cx, cy, rx, ry, colour, strength):
    h, w, _ = arr.shape
    y0, y1 = int(max(0, cy - ry - 2)), int(min(h, cy + ry + 3))
    x0, x1 = int(cx - rx - 2), int(cx + rx + 3)
    ys = np.arange(y0, y1)[:, None]
    xs = np.arange(x0, x1)[None, :]
    d = ((xs - cx) / rx) ** 2 + ((ys - cy) / ry) ** 2
    m = np.clip(1.0 - d, 0.0, 1.0) ** 0.6 * strength
    xi = np.mod(xs, w)[0]
    patch = arr[y0:y1][:, xi]
    patch[:] = patch * (1.0 - m[..., None]) + np.asarray(colour, np.float32) * m[..., None]
    arr[y0:y1][:, xi] = patch


def strata():
    rng = np.random.default_rng(7)
    w, h = 1024, 256
    src = np.asarray(Image.open(os.path.join(TEX, "soil_band.png")).convert("RGB")).astype(np.float32)
    # A 1024 x 256 crop of the seamless crumb. Wrap in x stays seamless.
    crumb = src[300:300 + h, :w].copy()
    luma = crumb @ np.array([0.2126, 0.7152, 0.0722], np.float32)
    crumb = crumb / max(luma.mean(), 1.0) * 1.0        # mean 1 per channel scale, graded per layer below
    v = (np.arange(h)[::-1] + 0.5) / h                 # row 0 is the top (v = 1)
    v = np.repeat(v[:, None], w, axis=1)
    warp = 0.030 * wrap_noise((h, w), (6, 40), rng) + 0.010 * wrap_noise((h, w), (2, 9), rng)
    vv = np.clip(v + warp, 0, 1)

    def band(lo, hi, soft=0.025):
        return np.clip((vv - lo) / soft, 0, 1) * np.clip((hi - vv) / soft, 0, 1)

    sub = np.array([17.0, 30.0, 24.0], np.float32)     # substrate
    grit = np.array([46.0, 58.0, 46.0], np.float32)    # pale grit
    humus = np.array([10.0, 22.0, 17.0], np.float32)   # dark humus
    root = np.array([18.0, 34.0, 24.0], np.float32)
    line = np.array([28.0, 72.0, 36.0], np.float32)   # moss meets glass
    out = np.zeros((h, w, 3), np.float32)
    weights = [band(0.00, 0.15), band(0.12, 0.34), band(0.30, 0.72), band(0.68, 0.90), band(0.86, 1.01, 0.02)]
    colours = [sub, grit, humus, root, line]
    total = np.maximum(sum(weights), 1e-4)
    for wgt, col in zip(weights, colours):
        out += (wgt / total)[..., None] * col
    out *= (crumb * 0.85 + 0.15)                       # crumb texture rides on every layer

    # Stones in the grit layer.
    for _ in range(46):
        cx = rng.uniform(0, w)
        cy = h * (1.0 - rng.uniform(0.17, 0.33))
        rx, ry = rng.uniform(5, 15), rng.uniform(3.5, 9)
        tone = rng.uniform(0.7, 1.0) * np.array([84.0, 84.0, 66.0], np.float32)
        blob(out, cx, cy, rx, ry, tone, 0.88)
        blob(out, cx - rx * 0.2, cy - ry * 0.25, rx * 0.5, ry * 0.45, tone * 1.18, 0.35)
    # Fine light specks through the whole band.
    for _ in range(900):
        cx, cy = rng.uniform(0, w), rng.uniform(0, h)
        r = rng.uniform(0.8, 2.2)
        blob(out, cx, cy, r, r, np.array([88.0, 94.0, 74.0], np.float32) * rng.uniform(0.5, 1.0), rng.uniform(0.3, 0.7))
    # Green specks low in the substrate, the sparkle the reference shows near the base.
    for _ in range(200):
        cx = rng.uniform(0, w)
        cy = h * (1.0 - rng.uniform(0.0, 0.14))
        r = rng.uniform(0.9, 2.6)
        blob(out, cx, cy, r, r, np.array([52.0, 136.0, 78.0], np.float32), rng.uniform(0.3, 0.65))
    # Root fibres in the upper zone.
    for _ in range(70):
        x0 = rng.uniform(0, w)
        y0 = h * (1.0 - rng.uniform(0.66, 0.92))
        ang = rng.uniform(-0.5, 0.5) + rng.choice([np.pi / 2, 0.0]) * rng.choice([0, 1])
        length = rng.integers(14, 46)
        for k in range(length):
            x = x0 + k * np.cos(ang) + 2.0 * np.sin(k * 0.3 + x0)
            y = y0 + k * np.sin(ang) * 0.6
            blob(out, x, y, 1.3, 1.3, np.array([54.0, 82.0, 52.0], np.float32), 0.55)
    return np.clip(out, 0, 255).astype(np.uint8)


def strand():
    rng = np.random.default_rng(11)
    n = 512
    cells = 56
    h_map = np.zeros((n, n), np.float32)
    shade = np.zeros((n, n), np.float32)
    tint = np.zeros((n, n), np.float32)

    sites_n = 13
    sites = (np.stack(np.meshgrid(np.arange(sites_n), np.arange(sites_n)), -1).reshape(-1, 2)
             + rng.uniform(0.1, 0.9, (sites_n * sites_n, 2))) * (n / sites_n)
    site_h = rng.uniform(0.62, 1.0, len(sites))
    site_shade = rng.uniform(0.35, 1.0, len(sites))

    def clump(px, py):
        d = np.hypot(np.minimum(np.abs(sites[:, 0] - px), n - np.abs(sites[:, 0] - px)),
                     np.minimum(np.abs(sites[:, 1] - py), n - np.abs(sites[:, 1] - py)))
        k = int(np.argmin(d))
        return site_h[k] * (1.0 - 0.28 * min(1.0, d[k] / (n / sites_n))), site_shade[k]

    pitch = n / cells
    for j in range(cells):
        for i in range(cells):
            cx = (i + 0.5 + rng.uniform(-0.38, 0.38)) * pitch
            cy = (j + 0.5 + rng.uniform(-0.38, 0.38)) * pitch
            base_h, sh = clump(cx, cy)
            hs = float(np.clip(base_h * rng.uniform(0.6, 1.0), 0.0, 1.0))
            radius = 0.78 * pitch * rng.uniform(0.82, 1.25)
            tone = rng.uniform(0.0, 1.0)
            r = int(np.ceil(radius))
            ys = (np.arange(-r, r + 1) + int(cy)) % n
            xs = (np.arange(-r, r + 1) + int(cx)) % n
            dy = (np.arange(-r, r + 1) + int(cy) - cy)[:, None]
            dx = (np.arange(-r, r + 1) + int(cx) - cx)[None, :]
            d = np.hypot(dx, dy) / radius
            inside = d < 1.0
            height = hs * np.clip(1.0 - d, 0.0, 1.0) ** 1.6
            sub = np.ix_(ys, xs)
            win = h_map[sub]
            win_shade = shade[sub]
            win_tint = tint[sub]
            take = inside & (height > win)
            win[take] = height[take]
            win_shade[take] = sh
            win_tint[take] = tone
            h_map[sub] = win
            shade[sub] = win_shade
            tint[sub] = win_tint
    return np.stack([h_map, shade, tint, np.ones_like(h_map)], -1)


def main(argv):
    s = strata()
    Image.fromarray(s).save(os.path.join(TEX, "s3_strata.png"))
    st = strand()
    Image.fromarray(np.clip(st * 255.0 + 0.5, 0, 255).astype(np.uint8), "RGBA").save(os.path.join(TEX, "s3_strand.png"))
    print("s3_strata mean", s.reshape(-1, 3).mean(axis=0).round(1), "s3_strand cover", float((st[..., 0] > 0.02).mean()))
    if "--preview" in argv:
        out = argv[argv.index("--preview") + 1]
        os.makedirs(out, exist_ok=True)
        Image.fromarray(s).save(os.path.join(out, "s3_strata.png"))
        Image.fromarray(np.clip(st[..., 0] * 255, 0, 255).astype(np.uint8)).save(os.path.join(out, "s3_strand_height.png"))


if __name__ == "__main__":
    main(sys.argv[1:])
