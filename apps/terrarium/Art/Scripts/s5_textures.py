"""Spike S5 (T-TER-045) textures: a looping steam puff flipbook, the desk reflection streak and the foot contact line.

Nothing is sampled from the reference frames. Run from the repo root:

    python apps/terrarium/Art/Scripts/s5_textures.py            # writes the three PNGs
    python apps/terrarium/Art/Scripts/s5_textures.py --sheet    # also writes a contact sheet of the flipbook next to it

s5_steam.png   1024 x 1024, 8 x 8 frames of 128 px. A soft eroded puff, RGB constant (grey-mint white), the density in alpha.
               The 64 frames are a slice of a 3D noise field that is periodic in x, y and time (built in Fourier space,
               so frame 63 runs into frame 0 with no seam). Used by Shuriken Texture Sheet Animation, no frame blending.
s5_desk_streak.png  256 x 256. R is the reflection streak, G a wider soft glow. v = 1 is at the jar foot, v = 0 is toward the eye.
s5_contact.png      512 x 512. A thin dark line and a soft shadow, alpha only. The card is 0.12 m across (the line sits at 47 mm).
"""
from __future__ import annotations

import os
import sys

import numpy as np
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "..", ".."))
TEX = os.path.join(ROOT, "apps", "terrarium", "Assets", "Art", "Textures")

FRAME = 128
GRID = 8
FRAMES = GRID * GRID
SEED = 45


def smoothstep(a, b, x):
    t = np.clip((x - a) / (b - a), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def periodic_noise(shape, beta, tscale, cutoff, rng):
    """Zero-mean unit-variance noise, periodic on every axis, power ~ 1/f^beta, low-passed at `cutoff` cycles per frame.
    tscale scales the time axis in the frequency norm: above 1 the field changes more slowly."""
    nt, ny, nx = shape
    white = rng.standard_normal(shape)
    spec = np.fft.fftn(white)
    kt = np.fft.fftfreq(nt)[:, None, None] * nt
    ky = np.fft.fftfreq(ny)[None, :, None] * ny
    kx = np.fft.fftfreq(nx)[None, None, :] * nx
    f = np.sqrt(kx * kx + ky * ky + (kt * tscale) ** 2)
    f[0, 0, 0] = 1.0
    spec = spec / np.power(f, beta * 0.5) * np.exp(-0.5 * (f / cutoff) ** 4)
    spec[0, 0, 0] = 0.0
    out = np.real(np.fft.ifftn(spec))
    out -= out.mean()
    return out / out.std()


def steam():
    rng = np.random.default_rng(SEED)
    n = periodic_noise((FRAMES, FRAME, FRAME), 3.4, 1.0, 7.0, rng)
    n2 = periodic_noise((FRAMES, FRAME, FRAME), 3.0, 1.0, 14.0, rng)
    ys, xs = np.mgrid[0:FRAME, 0:FRAME].astype(np.float64)
    u = (xs + 0.5) / FRAME * 2.0 - 1.0
    v = (ys + 0.5) / FRAME * 2.0 - 1.0
    sheet = np.zeros((GRID * FRAME, GRID * FRAME, 4), dtype=np.uint8)
    for k in range(FRAMES):
        phase = k / float(FRAMES) * 2.0 * np.pi
        # The puff leans and breathes a little, once per loop.
        cx = 0.05 * np.sin(phase)
        cy = 0.04 * np.cos(phase)
        r = np.sqrt((u - cx) ** 2 + ((v - cy) * 0.82) ** 2)
        # Eroded radius: the noise pushes the edge in and out, the second octave adds the small wisps.
        edge = 0.50 + 0.17 * n[k] + 0.07 * n2[k]
        body = smoothstep(edge, edge - 0.34, r)
        core = smoothstep(0.55, 0.0, r)
        d = np.clip(body * (0.55 + 0.45 * core), 0.0, 1.0)
        # Density structure inside the puff, so it is not a flat blob.
        d *= np.clip(0.80 + 0.22 * n2[k], 0.45, 1.0)
        # Keep the corners empty so a card does not show an edge.
        d *= 1.0 - smoothstep(0.80, 0.98, np.maximum(np.abs(u), np.abs(v)))
        a = np.clip(d, 0.0, 1.0)
        # Lit from below (the jar glow is mint): a touch more green and brightness on the lower half.
        low = smoothstep(0.6, -0.6, v)
        rgb = np.stack([0.80 + 0.04 * low, 0.92 + 0.04 * low, 0.88 + 0.04 * low], axis=-1)
        frame = np.concatenate([rgb, a[..., None]], axis=-1)
        frame = (np.clip(frame, 0, 1) * 255.0 + 0.5).astype(np.uint8)
        row, col = divmod(k, GRID)
        # Unity's v = 0 is the bottom row of the PNG, so row 0 of the sheet (frame 0) is the top of the image.
        sheet[row * FRAME:(row + 1) * FRAME, col * FRAME:(col + 1) * FRAME] = frame
    return sheet


def streak():
    size = 256
    rng = np.random.default_rng(SEED + 1)
    ys, xs = np.mgrid[0:size, 0:size].astype(np.float64)
    x = (xs + 0.5) / size * 2.0 - 1.0
    v = 1.0 - (ys + 0.5) / size
    # 1D noise along x, stretched along the card so the reflection breaks into vertical varnish streaks.
    base = rng.standard_normal(64)
    k = np.exp(-0.5 * (np.arange(-6, 7) / 2.0) ** 2)
    base = np.convolve(np.concatenate([base[-6:], base, base[:6]]), k / k.sum(), mode="valid")[:64]
    line = np.interp((x + 1.0) * 31.5, np.arange(64), base)
    line = (line - line.mean()) / line.std()
    fall = np.exp(-(1.0 - v) * 3.4)
    width = 0.20 + 0.16 * (1.0 - v)
    core = np.exp(-(x / width) ** 2)
    r = core * fall * np.clip(0.80 + 0.30 * line * (1.0 - v), 0.35, 1.2)
    r *= smoothstep(0.0, 0.10, v)
    g = np.exp(-(x / (width * 2.2)) ** 2) * np.exp(-(1.0 - v) * 2.4) * smoothstep(0.0, 0.18, v)
    out = np.zeros((size, size, 4), dtype=np.uint8)
    out[..., 0] = (np.clip(r, 0, 1) * 255 + 0.5).astype(np.uint8)
    out[..., 1] = (np.clip(g * 0.6, 0, 1) * 255 + 0.5).astype(np.uint8)
    out[..., 3] = 255
    return out


def contact():
    size = 512
    ys, xs = np.mgrid[0:size, 0:size].astype(np.float64)
    u = (xs + 0.5) / size * 2.0 - 1.0
    v = (ys + 0.5) / size * 2.0 - 1.0
    r = np.sqrt(u * u + v * v) * 60.0  # millimetres from the jar axis (card half-extent 60 mm)
    line = np.exp(-((r - 46.8) / 1.3) ** 2)
    shadow = np.exp(-((r - 48.0) / 4.5) ** 2)
    a = np.clip(0.70 * line + 0.10 * shadow, 0.0, 1.0)
    a *= smoothstep(43.0, 45.5, r)
    a *= 1.0 - smoothstep(56.0, 59.5, r)
    out = np.zeros((size, size, 4), dtype=np.uint8)
    out[..., :3] = 255
    out[..., 3] = (a * 255 + 0.5).astype(np.uint8)
    return out


def main(argv):
    os.makedirs(TEX, exist_ok=True)
    sheet = steam()
    Image.fromarray(sheet, "RGBA").save(os.path.join(TEX, "s5_steam.png"))
    Image.fromarray(streak(), "RGBA").save(os.path.join(TEX, "s5_desk_streak.png"))
    Image.fromarray(contact(), "RGBA").save(os.path.join(TEX, "s5_contact.png"))
    print("wrote s5_steam.png (%dx%d), s5_desk_streak.png, s5_contact.png" % (sheet.shape[1], sheet.shape[0]))
    if "--sheet" in argv:
        bg = np.zeros_like(sheet[..., :3])
        bg[...] = (18, 40, 44)
        a = sheet[..., 3:4].astype(np.float64) / 255.0
        comp = (sheet[..., :3] * a + bg * (1.0 - a)).astype(np.uint8)
        Image.fromarray(comp, "RGB").save(os.path.join(TEX, "..", "..", "..", "..", "..", "orchestration", "runs", "terrarium", "T-TER-045", "steam-sheet.png"))


if __name__ == "__main__":
    main(sys.argv[1:])
