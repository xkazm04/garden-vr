"""Structured-glass spike textures, and the JarG1 glass metrics.

Paints the studio strip and the droplet atlas. Nothing is sampled from the
reference frames. Run from the repo root:

    python apps/terrarium/Art/Scripts/s1_structured_glass.py
    python apps/terrarium/Art/Scripts/s1_structured_glass.py --metrics orchestration/runs/terrarium/T-TER-040

The metrics command reads a-jar-g1.png (variant A) and jar-g1.png (variant B)
and writes grey-sbs.png, flip.gif, and glass-metrics.json.
"""
from __future__ import annotations

import hashlib
import json
import math
import os
import sys

import numpy as np
from PIL import Image, ImageDraw

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "..", ".."))
TEX = os.path.join(ROOT, "apps", "terrarium", "Assets", "Art", "Textures")
STUDIO_PATH = os.path.join(TEX, "s1_studio.png")
DROPS_PATH = os.path.join(TEX, "s1_drops.png")
REF_PATH = os.path.join(ROOT, "shared", "assets", "art-reference", "A1-03-night-moss-1.png")

FACE = 128
DROP = 512
SEED = 40


def lin_to_srgb(c):
    c = np.clip(c, 0.0, 1.0)
    return np.where(c <= 0.0031308, c * 12.92, 1.055 * np.power(c, 1.0 / 2.4) - 0.055)


def paint_studio():
    faces = np.zeros((6, FACE, FACE, 3), dtype=np.float64)
    faces += np.array((0.004, 0.005, 0.005))
    yy, xx = np.mgrid[0:FACE, 0:FACE]
    xs = np.arange(FACE)[None, :]
    ys = np.arange(FACE)[:, None]
    vertical = np.exp(-0.5 * ((ys - FACE * 0.42) / (FACE * 0.34)) ** 2)
    for index in (0, 1, 4, 5):
        for center, sigma, color, gain in (
            (0.22, 0.045, (0.92, 0.97, 1.00), 1.00),
            (0.78, 0.038, (0.78, 0.90, 0.96), 0.80),
        ):
            strip = np.exp(-0.5 * ((xs / FACE - center) / sigma) ** 2)
            faces[index] += (vertical * strip)[..., None] * np.array(color) * gain
        blob = np.exp(-0.5 * (((xx - FACE * 0.30) / 18.0) ** 2 + ((yy - FACE * 0.22) / 16.0) ** 2))
        faces[index] += blob[..., None] * np.array((1.00, 0.62, 0.28)) * 0.42
        mint = np.clip((yy - FACE * 0.70) / (FACE * 0.30), 0.0, 1.0) ** 1.2
        faces[index] += mint[..., None] * np.array((0.22, 0.55, 0.38)) * 0.55
    rad = np.sqrt((xx - FACE * 0.5) ** 2 + (yy - FACE * 0.5) ** 2) / (FACE * 0.55)
    glow = np.clip(1.0 - rad, 0.0, 1.0) ** 1.3
    faces[3] += glow[..., None] * np.array((0.28, 0.78, 0.48))
    faces[2] += 0.008
    strip = np.clip(np.concatenate(list(faces), axis=1), 0.0, 1.0)
    return (lin_to_srgb(strip) * 255.0 + 0.5).astype(np.uint8)


def paint_drops():
    rng = np.random.RandomState(SEED)
    height = np.zeros((DROP, DROP), dtype=np.float64)
    mask = np.zeros((DROP, DROP), dtype=np.float64)
    placed = []
    for _ in range(4000):
        if len(placed) >= 40:
            break
        v = float(rng.random())
        if float(rng.random()) > (0.18 + 0.82 * v):
            continue
        radius = 7.0 + 16.0 * v
        u = float(rng.uniform(0.04, 0.96))
        cx = u * DROP
        cy = (1.0 - v) * DROP
        if any((cx - px) ** 2 + (cy - py) ** 2 < (radius + pr + 8.0) ** 2 for px, py, pr in placed):
            continue
        placed.append((cx, cy, radius))
        x0 = max(0, int(cx - radius - 1))
        x1 = min(DROP, int(cx + radius + 2))
        y0 = max(0, int(cy - radius - 1))
        y1 = min(DROP, int(cy + radius * 3.2 + 2))
        for y in range(y0, y1):
            for x in range(x0, x1):
                dx = x + 0.5 - cx
                dy = y + 0.5 - cy
                if dy <= radius and dx * dx + dy * dy <= radius * radius:
                    cap = (1.0 - (dx * dx + dy * dy) / (radius * radius)) ** 0.5
                    if cap > height[y, x]:
                        height[y, x] = cap
                        mask[y, x] = 1.0
                elif 0.0 < dy < radius * 2.8 and abs(dx) < radius * 0.28:
                    trail = (1.0 - dy / (radius * 2.8)) * (1.0 - abs(dx) / (radius * 0.28))
                    if trail > height[y, x] and mask[y, x] < 0.5:
                        height[y, x] = max(height[y, x], trail * 0.85)
    rgba = np.zeros((DROP, DROP, 4), dtype=np.uint8)
    rgba[..., 0] = 128
    rgba[..., 1] = 128
    rgba[..., 2] = 0
    ys, xs = np.mgrid[0:DROP, 0:DROP]
    inside = mask > 0.5
    h = height
    left = np.zeros_like(h)
    right = np.zeros_like(h)
    down = np.zeros_like(h)
    up = np.zeros_like(h)
    left[:, 1:] = h[:, :-1]
    right[:, :-1] = h[:, 1:]
    down[1:, :] = h[:-1, :]
    up[:-1, :] = h[1:, :]
    dx = (right - left) * 2.2
    dy = (up - down) * 2.2
    nx = np.zeros_like(h)
    ny = np.zeros_like(h)
    nz = np.ones_like(h)
    nx[inside] = -dx[inside]
    ny[inside] = -dy[inside]
    nz[inside] = 1.0
    length = np.sqrt(nx * nx + ny * ny + nz * nz)
    length = np.maximum(length, 1e-6)
    rgba[..., 0] = np.clip(np.round((nx / length) * 127.5 + 127.5), 0, 255).astype(np.uint8)
    rgba[..., 1] = np.clip(np.round((ny / length) * 127.5 + 127.5), 0, 255).astype(np.uint8)
    rgba[..., 2] = np.clip(np.round(h * 255.0), 0, 255).astype(np.uint8)
    rgba[..., 3] = np.where(inside, 255, 0).astype(np.uint8)
    # Flat normal outside the caps. Height stays in B so the wipe can read a trail.
    rgba[~inside, 0] = 128
    rgba[~inside, 1] = 128
    return rgba, len(placed)


def sha256(path):
    digest = hashlib.sha256()
    with open(path, "rb") as handle:
        digest.update(handle.read())
    return digest.hexdigest()


def write_textures():
    os.makedirs(TEX, exist_ok=True)
    studio = paint_studio()
    drops, count = paint_drops()
    if studio.shape != (FACE, FACE * 6, 3):
        raise SystemExit("studio strip is %s" % (studio.shape,))
    if drops.shape != (DROP, DROP, 4):
        raise SystemExit("drop atlas is %s" % (drops.shape,))
    if int(drops[..., 3].max()) < 255 or count < 20:
        raise SystemExit("drop atlas has no beads (%s)" % count)
    if int(studio.max()) < 200:
        raise SystemExit("studio strip is too dark")
    Image.fromarray(studio, "RGB").save(STUDIO_PATH)
    Image.fromarray(drops, "RGBA").save(DROPS_PATH)
    print("wrote", STUDIO_PATH, studio.shape, sha256(STUDIO_PATH))
    print("wrote", DROPS_PATH, "beads", count, "cut", int((drops[..., 3] > 0).sum()), sha256(DROPS_PATH))


def _import_anchor():
    sys.path.insert(0, os.path.dirname(__file__))
    import spec_anchor as anchor
    return anchor


def silhouette_mask(anchor):
    mask = np.zeros((anchor.H, anchor.W), dtype=bool)
    for y in np.linspace(0.004, 0.124, 90):
        for side in (-1.0, 1.0):
            point = anchor.project((side * 0.045, float(y), 0.0))
            if point is None:
                continue
            x = int(round(point[0]))
            py = int(round(point[1]))
            mask[max(0, py - 3):py + 4, max(0, x - 4):x + 5] = True
    return mask


def glass_body_mask(anchor):
    """Front of the jar cylinder, heel to lip. Cork is above 0.124."""
    luma_shape = (anchor.H, anchor.W)
    mask = np.zeros(luma_shape, dtype=bool)
    for py in range(200, 860, 2):
        for px in range(660, 1180, 2):
            origin, direction = anchor.ray(px + 0.5, py + 0.5)
            ox, oy, oz = origin
            dx, dy, dz = direction
            a = dx * dx + dz * dz
            if a < 1e-8:
                continue
            b = 2.0 * (ox * dx + oz * dz)
            c = ox * ox + oz * oz - 0.045 ** 2
            disc = b * b - 4.0 * a * c
            if disc < 0.0:
                continue
            t = (-b - math.sqrt(disc)) / (2.0 * a)
            if t <= 0.0:
                continue
            hit_y = oy + direction[1] * t
            if 0.012 < hit_y < 0.124:
                mask[py, px] = True
    return mask


def edge_energy(luma, mask):
    gx = np.zeros_like(luma)
    gx[:, 1:] = np.abs(luma[:, 1:] - luma[:, :-1])
    picked = gx[mask]
    if picked.size == 0:
        return 0.0
    return float(picked.mean())


def glass_p5(luma, mask):
    picked = luma[mask]
    return float(np.percentile(picked, 5)), float(np.percentile(picked, 50)), int(picked.size)


def load_luma(anchor, path):
    image = anchor.load_rgb(path)
    if image.shape[0] != anchor.H or image.shape[1] != anchor.W:
        raise SystemExit("%s is %s, expected %dx%d" % (path, image.shape, anchor.W, anchor.H))
    return anchor.relative_luminance(image)


def write_grey_and_flip(run, ref, render_a, render_b):
    def grey(path):
        rgb = np.asarray(Image.open(path).convert("RGB"), dtype=np.float32)
        y = (0.2126 * rgb[..., 0] + 0.7152 * rgb[..., 1] + 0.0722 * rgb[..., 2]).astype(np.uint8)
        return Image.fromarray(np.dstack([y, y, y]), "RGB")

    panels = [grey(ref), grey(render_a), grey(render_b)]
    gap = 8
    bar = 28
    width = panels[0].width * 3 + gap * 2
    height = panels[0].height + bar
    sheet = Image.new("RGB", (width, height), (20, 20, 20))
    draw = ImageDraw.Draw(sheet)
    labels = ("reference", "A  locked glass", "B  structured glass")
    for i, panel in enumerate(panels):
        x = i * (panel.width + gap)
        sheet.paste(panel, (x, bar))
        draw.text((x + 12, 6), labels[i], fill=(236, 232, 220))
    grey_path = os.path.join(run, "grey-sbs.png")
    sheet.save(grey_path)

    frames = []
    for path, label in (
        (ref, "reference"),
        (render_a, "A"),
        (render_b, "B"),
    ):
        frame = Image.open(path).convert("RGB").resize((912, 512), Image.Resampling.LANCZOS)
        draw = ImageDraw.Draw(frame)
        draw.rectangle((0, 0, 160, 28), fill=(16, 16, 16))
        draw.text((8, 6), label, fill=(240, 236, 224))
        frames.append(frame)
    sheet = Image.new("RGB", (912, 512 * 3))
    for i, frame in enumerate(frames):
        sheet.paste(frame, (0, 512 * i))
    palette = sheet.quantize(colors=256)
    gifs = [frame.quantize(palette=palette, dither=Image.Dither.FLOYDSTEINBERG) for frame in frames]
    gif_path = os.path.join(run, "flip.gif")
    gifs[0].save(gif_path, save_all=True, append_images=gifs[1:], duration=800, loop=0, disposal=2)
    return grey_path, gif_path


def write_pairs(run, ref, render_a, render_b):
    """Reference on the left. Middle and right swap so the judge is asked both orders."""
    def panel(path):
        return Image.open(path).convert("RGB").resize((608, 341), Image.Resampling.LANCZOS)

    ref_i, a_i, b_i = panel(ref), panel(render_a), panel(render_b)
    paths = {}
    for name, mid, right, mid_name, right_name in (
        ("pair-ab.png", a_i, b_i, "middle = A", "right = B"),
        ("pair-ba.png", b_i, a_i, "middle = B", "right = A"),
    ):
        sheet = Image.new("RGB", (608 * 3 + 16, 341 + 26), (16, 16, 16))
        sheet.paste(ref_i, (0, 26))
        sheet.paste(mid, (616, 26))
        sheet.paste(right, (1232, 26))
        draw = ImageDraw.Draw(sheet)
        draw.text((8, 4), "reference", fill=(236, 232, 220))
        draw.text((624, 4), mid_name, fill=(236, 232, 220))
        draw.text((1240, 4), right_name, fill=(236, 232, 220))
        path = os.path.join(run, name)
        sheet.save(path)
        paths[name] = path
        # The judge sheet has no A/B labels. Order is the file name only.
        plain = Image.new("RGB", (608 * 3 + 16, 341), (16, 16, 16))
        plain.paste(ref_i, (0, 0))
        plain.paste(mid, (616, 0))
        plain.paste(right, (1232, 0))
        plain_name = name.replace(".png", "-judge.png")
        plain_path = os.path.join(run, plain_name)
        plain.save(plain_path)
        paths[plain_name] = plain_path
    return paths


def metrics(run):
    anchor = _import_anchor()
    render_a = os.path.join(run, "a-jar-g1.png")
    render_b = os.path.join(run, "jar-g1.png")
    for path in (REF_PATH, render_a, render_b):
        if not os.path.isfile(path):
            raise SystemExit("missing " + path)
    body = glass_body_mask(anchor)
    edge = silhouette_mask(anchor)
    rows = {}
    for name, path in (("reference", REF_PATH), ("A", render_a), ("B", render_b)):
        luma = load_luma(anchor, path)
        p5, p50, count = glass_p5(luma, body)
        glow = anchor.measure_glow(anchor.load_rgb(path))
        rows[name] = {
            "glassP5": round(p5, 4),
            "glassP50": round(p50, 4),
            "glassMaskPixels": count,
            "edgeEnergy": round(edge_energy(luma, edge), 6),
            "glassMean": glow["measured"]["glassMean"],
            "crozierPeak": glow["measured"]["crozierPeak"],
            "ringPeak": glow["measured"]["ringPeak"],
            "glowInside": glow["inside"],
        }
    ref_p5 = rows["reference"]["glassP5"]
    b_p5 = rows["B"]["glassP5"]
    rel = abs(b_p5 - ref_p5) / max(ref_p5, 1e-4)
    report = {
        "mask": "front cylinder hit, object y 0.012 to 0.124, every second pixel",
        "edge": "mean horizontal luminance gradient within 4 px of the projected silhouette",
        "p5Within20Percent": rel <= 0.20,
        "p5RelativeError": round(rel, 4),
        "edgeEnergyUp": rows["B"]["edgeEnergy"] > rows["A"]["edgeEnergy"],
        "glassMeanInside": rows["B"]["glowInside"]["glassMean"],
        "images": rows,
    }
    grey, gif = write_grey_and_flip(run, REF_PATH, render_a, render_b)
    pairs = write_pairs(run, REF_PATH, render_a, render_b)
    report["grey"] = os.path.relpath(grey, ROOT).replace("\\", "/")
    report["flip"] = os.path.relpath(gif, ROOT).replace("\\", "/")
    report["pairs"] = {key: os.path.relpath(path, ROOT).replace("\\", "/") for key, path in pairs.items()}
    out = os.path.join(run, "glass-metrics.json")
    with open(out, "w", encoding="utf-8") as handle:
        json.dump(report, handle, indent=2)
        handle.write("\n")
    print(json.dumps(report, indent=2))
    print("wrote", out)


def main():
    if len(sys.argv) == 3 and sys.argv[1] == "--metrics":
        metrics(sys.argv[2])
        return
    if len(sys.argv) != 1:
        raise SystemExit("usage: s1_structured_glass.py [--metrics <run-dir>]")
    write_textures()


if __name__ == "__main__":
    main()
