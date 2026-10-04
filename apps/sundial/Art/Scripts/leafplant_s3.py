"""Spike S3 (T-SUN-045): one plant as a drawn-leaf assembly.

Everything the spike makes outside Unity is made here, from the generated parts sheet.

  python apps/sundial/Art/Scripts/leafplant_s3.py slice        sheet -> atlas png + parts json in Assets/Resources/LeafPlant
  python apps/sundial/Art/Scripts/leafplant_s3.py flip a.png b.png out.gif
  python apps/sundial/Art/Scripts/leafplant_s3.py grey a.png b.png out.png
  python apps/sundial/Art/Scripts/leafplant_s3.py orbit out.gif f1.png f2.png ...
  python apps/sundial/Art/Scripts/leafplant_s3.py stereo left.png right.png out_sbs.png out_anaglyph.png
  python apps/sundial/Art/Scripts/leafplant_s3.py contour frame.png --box x0 y0 x1 y1 [--out json]

The sheet comes from `bash tools/agy/image.sh` (prompt only, no reference image in), see parts-sheet-v1.png.prompt.txt.
"""
import argparse
import json
import os
import sys

import numpy as np
from PIL import Image
from scipy import ndimage as ndi

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, "..", "..", "..", ".."))
SHEET = os.path.join(REPO, "apps", "sundial", "Art", "Source", "plants", "leafplant", "parts-sheet-v1.png")
RES = os.path.join(REPO, "apps", "sundial", "Assets", "Resources", "LeafPlant")

# Palette the atlas is graded to. Sage leaf, coral-pink petal and ink are the house values of the plant cards.
SAGE = np.array([126.0, 140.0, 90.0])
PETAL = np.array([226.0, 150.0, 164.0])
INK = np.array([52.0, 40.0, 26.0])
GRADE_STRENGTH_LEAF = 0.85
GRADE_STRENGTH_PETAL = 0.75
INK_GROW_PX = 1.5
PAD = 6


def load_matte(path):
    rgb = np.asarray(Image.open(path).convert("RGB")).astype(np.float32)
    mn = rgb.min(axis=2)
    bg = mn >= 236
    fg = ndi.binary_closing(~bg, iterations=2)
    fg = ndi.binary_fill_holes(fg)
    # soft edge: alpha ramps from the paper white to a firmly inked pixel, only inside a thin halo of the mask
    halo = ndi.binary_dilation(fg, iterations=2)
    soft = np.clip((250.0 - mn) / (250.0 - 205.0), 0.0, 1.0)
    alpha = np.where(fg, 1.0, np.where(halo, soft, 0.0))
    return rgb, alpha.astype(np.float32), fg


def classify(lab, idx, rgb, sl):
    m = (lab[sl] == idx)
    h = sl[0].stop - sl[0].start
    w = sl[1].stop - sl[1].start
    area = int(m.sum())
    px = rgb[sl][m]
    pink = float(((px[:, 0] - px[:, 1]) > 30).mean())
    green = float(((px[:, 1] - px[:, 0]) > 8).mean())
    fill = area / float(w * h)
    kind = None
    if pink > 0.8 and 0.88 <= w / float(h) <= 1.15 and fill > 0.6:
        kind = "flower"
    elif pink > 0.7 and w > 130:
        kind = "flowerside"
    elif 0.3 < pink < 0.75 and w < 75:
        kind = "bud"
    elif green > 0.3 and fill < 0.3 and h > 250:
        kind = "stem"
    elif green > 0.25 and h < 150 and fill < 0.45:
        kind = "tuft"
    elif green > 0.4 and fill >= 0.5:
        kind = "leaf"
    return kind, sl, m, (w, h, area, fill, pink, green)


def pivot_for(kind, m, sl):
    h, w = m.shape
    rows = np.where(m.any(axis=1))[0]
    if kind == "flower":
        ys, xs = np.nonzero(m)
        return [float(xs.mean() + sl[1].start), float(ys.mean() + sl[0].start)]
    # lowest few rows: where the piece meets the stem
    low = rows[-max(3, h // 40):]
    xs = np.nonzero(m[low])[1]
    return [float(xs.mean() + sl[1].start), float(sl[0].start + rows[-1])]


def grade(rgb, alpha, fg):
    """Pull leaf green and petal pink to the house palette. Ink is left alone, then thickened."""
    out = rgb.copy()
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    ink = (rgb.sum(axis=2) < 330) & fg
    leaf = ((g - r) > 6) & ((g - b) > 10) & ~ink & fg
    petal = ((r - g) > 30) & ~ink & fg
    for mask, target, k in ((leaf, SAGE, GRADE_STRENGTH_LEAF), (petal, PETAL, GRADE_STRENGTH_PETAL)):
        mean = rgb[mask].mean(axis=0)
        gain = target / mean
        gain = 1.0 + (gain - 1.0) * k
        out[mask] = np.clip(rgb[mask] * gain, 0, 255)
    return out, ink


def thicken_ink(rgb, alpha, ink):
    """Ink lines are 3 px on the sheet. At G1 a leaf is about a fifth of its sheet size, so the stroke needs to grow
    or it is a half pixel stroke that crawls. Grow the dark mask and the alpha by the same radius."""
    r = INK_GROW_PX
    k = int(np.ceil(r))
    yy, xx = np.mgrid[-k:k + 1, -k:k + 1]
    disc = (xx * xx + yy * yy) <= r * r + 0.25
    grown = ndi.binary_dilation(ink, structure=disc)
    out = rgb.copy()
    out[grown] = INK
    # the grown ink may sit just outside the old matte: let alpha follow it, with a soft outer edge
    soft = np.clip(ndi.gaussian_filter(grown.astype(np.float32), 0.7) * 1.4, 0, 1)
    a = np.maximum(alpha, np.where(ndi.binary_dilation(grown, iterations=1), soft, 0.0))
    return out, np.clip(a, 0, 1)


def bleed(rgb, alpha):
    """Fill colour under the transparent pixels so filtering never pulls in the white paper."""
    solid = alpha > 0.5
    idx = ndi.distance_transform_edt(~solid, return_distances=False, return_indices=True)
    return rgb[idx[0], idx[1]]


def cmd_slice(args):
    rgb, alpha, fg = load_matte(args.sheet)
    lab, n = ndi.label(fg, structure=np.ones((3, 3)))
    parts = []
    skipped = []
    objs = ndi.find_objects(lab)
    for i in range(1, n + 1):
        area = int((lab[objs[i - 1]] == i).sum())
        if area < 150:
            continue
        kind, sl, m, feat = classify(lab, i, rgb, objs[i - 1])
        if kind is None:
            skipped.append({"component": i, "features": [float(x) for x in feat]})
            continue
        parts.append((kind, sl, m, feat, i))
    graded, ink = grade(rgb, alpha, fg)
    graded, alpha2 = thicken_ink(graded, alpha, ink)
    graded = bleed(graded, alpha2)
    atlas = np.dstack([np.clip(graded, 0, 255), alpha2 * 255.0]).astype(np.uint8)

    H, W = alpha.shape
    order = {"leaf": 0, "flower": 1, "flowerside": 2, "bud": 3, "stem": 4, "tuft": 5}
    parts.sort(key=lambda p: (order[p[0]], -p[3][2]))
    counts = {}
    out_parts = []
    for kind, sl, m, feat, i in parts:
        x0 = max(0, sl[1].start - PAD)
        y0 = max(0, sl[0].start - PAD)
        x1 = min(W, sl[1].stop + PAD)
        y1 = min(H, sl[0].stop + PAD)
        pivot = pivot_for(kind, m, sl)
        entry = {
            "kind": kind,
            "index": counts.get(kind, 0),
            "rect": [x0, y0, x1 - x0, y1 - y0],
            "pivot": [round(pivot[0], 1), round(pivot[1], 1)],
            "content": [int(sl[1].start), int(sl[0].start), int(sl[1].stop - sl[1].start), int(sl[0].stop - sl[0].start)],
        }
        if kind in ("stem",):
            ys, xs = np.nonzero(m)
            top = ys.argmin()
            entry["tip"] = [round(float(xs[top] + sl[1].start), 1), round(float(ys[top] + sl[0].start), 1)]
        counts[kind] = counts.get(kind, 0) + 1
        out_parts.append(entry)
    os.makedirs(RES, exist_ok=True)
    png = os.path.join(RES, "midday-parts.png")
    Image.fromarray(atlas, "RGBA").save(png)
    meta = {"atlas": "midday-parts", "size": [W, H], "counts": counts, "skipped": skipped, "parts": out_parts}
    with open(os.path.join(RES, "midday-parts.json"), "w") as f:
        json.dump(meta, f, indent=1)
    with open(png + ".provenance.txt", "w") as f:
        f.write(
            "generator: agy (Antigravity CLI image tool) parts sheet, sliced and graded by apps/sundial/Art/Scripts/leafplant_s3.py\n"
            "source: apps/sundial/Art/Source/plants/leafplant/parts-sheet-v1.png (prompt only, no reference image)\n"
            "derived_from_art_reference: no\n"
            "grade: leaf green, petal pink and ink pulled to the house palette hexes in the script (sage 7E8C5A, petal E296A4)\n"
        )
    print("parts", counts, "skipped", len(skipped), "->", png)
    if args.debug:
        dbg = Image.fromarray(atlas, "RGBA")
        bgc = Image.new("RGBA", dbg.size, (200, 200, 205, 255))
        bgc.alpha_composite(dbg)
        from PIL import ImageDraw
        d = ImageDraw.Draw(bgc)
        for p in out_parts:
            x, y, w, h = p["rect"]
            d.rectangle([x, y, x + w, y + h], outline=(255, 0, 0, 255))
            d.text((x + 2, y + 2), "%s%d" % (p["kind"][:2], p["index"]), fill=(0, 0, 0, 255))
            px, py = p["pivot"]
            d.ellipse([px - 3, py - 3, px + 3, py + 3], outline=(0, 0, 255, 255))
        bgc.convert("RGB").save(args.debug)
        print("debug", args.debug)


def load_rgb(path):
    return Image.open(path).convert("RGB")


def cmd_flip(args):
    a, b = load_rgb(args.a), load_rgb(args.b)
    frames = [a, b]
    frames[0].save(args.out, save_all=True, append_images=frames[1:], duration=700, loop=0, optimize=True)
    print("flip", args.out)


def cmd_grey(args):
    a = load_rgb(args.a).convert("L")
    b = load_rgb(args.b).convert("L")
    w, h = a.size
    sheet = Image.new("L", (w * 2 + 8, h), 255)
    sheet.paste(a, (0, 0))
    sheet.paste(b, (w + 8, 0))
    sheet.save(args.out)
    print("grey", args.out)


def cmd_orbit(args):
    frames = [load_rgb(f) for f in args.frames]
    seq = frames + frames[-2:0:-1]
    seq[0].save(args.out, save_all=True, append_images=seq[1:], duration=140, loop=0, optimize=True)
    print("orbit", args.out, len(seq), "frames")


def cmd_stereo(args):
    left, right = load_rgb(args.left), load_rgb(args.right)
    w, h = left.size
    sbs = Image.new("RGB", (w * 2 + 8, h), (255, 255, 255))
    sbs.paste(left, (0, 0))
    sbs.paste(right, (w + 8, 0))
    sbs.save(args.sbs)
    l = np.asarray(left)
    r = np.asarray(right)
    ana = np.dstack([l[..., 0], r[..., 1], r[..., 2]])
    Image.fromarray(ana).save(args.anaglyph)
    print("stereo", args.sbs, args.anaglyph)


def cmd_contour(args):
    """Median half-max ink stroke width inside a box, same idea as the T-SUN-028 contour measure: for dark ink pixels
    on the plant, the thickness of the dark run perpendicular to the stroke. Here: thickness = twice the distance to
    the nearest non-ink pixel, taken along the medial axis."""
    img = np.asarray(load_rgb(args.frame)).astype(np.float32)
    x0, y0, x1, y1 = args.box
    crop = img[y0:y1, x0:x1]
    luma = crop @ np.array([0.2126, 0.7152, 0.0722])
    ink = luma < args.threshold
    dist = ndi.distance_transform_edt(ink)
    # medial axis: local maxima of the distance map
    mx = ndi.maximum_filter(dist, size=3)
    axis = (dist >= mx - 1e-6) & (dist > 0.5)
    widths = dist[axis] * 2.0
    stats = {
        "box": args.box,
        "ink_px": int(ink.sum()),
        "axis_px": int(axis.sum()),
        "median_width": float(np.median(widths)) if len(widths) else None,
        "p25": float(np.percentile(widths, 25)) if len(widths) else None,
        "p75": float(np.percentile(widths, 75)) if len(widths) else None,
    }
    print(json.dumps(stats))
    if args.out:
        with open(args.out, "w") as f:
            json.dump(stats, f, indent=1)


def main(argv):
    p = argparse.ArgumentParser()
    sub = p.add_subparsers(dest="cmd", required=True)
    s = sub.add_parser("slice")
    s.add_argument("--sheet", default=SHEET)
    s.add_argument("--debug", default=None)
    s.set_defaults(fn=cmd_slice)
    s = sub.add_parser("flip")
    s.add_argument("a")
    s.add_argument("b")
    s.add_argument("out")
    s.set_defaults(fn=cmd_flip)
    s = sub.add_parser("grey")
    s.add_argument("a")
    s.add_argument("b")
    s.add_argument("out")
    s.set_defaults(fn=cmd_grey)
    s = sub.add_parser("orbit")
    s.add_argument("out")
    s.add_argument("frames", nargs="+")
    s.set_defaults(fn=cmd_orbit)
    s = sub.add_parser("stereo")
    s.add_argument("left")
    s.add_argument("right")
    s.add_argument("sbs")
    s.add_argument("anaglyph")
    s.set_defaults(fn=cmd_stereo)
    s = sub.add_parser("contour")
    s.add_argument("frame")
    s.add_argument("--box", nargs=4, type=int, required=True)
    s.add_argument("--threshold", type=float, default=95.0)
    s.add_argument("--out", default=None)
    s.set_defaults(fn=cmd_contour)
    args = p.parse_args(argv)
    args.fn(args)


if __name__ == "__main__":
    main(sys.argv[1:])
