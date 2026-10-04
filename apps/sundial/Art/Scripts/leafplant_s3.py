"""Spike S3 (T-SUN-045): one plant as a drawn-leaf assembly.

Everything the spike makes outside Unity is made here, from the generated parts sheet.

  python apps/sundial/Art/Scripts/leafplant_s3.py slice [--species midday|morning|evening|all] [--debug out.png]
                                                           sheet -> atlas png + parts json in Assets/Resources/LeafPlant
                                                           (T-SUN-049: one sheet per species, veins as a separate lighter pencil layer)
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
SRC = os.path.join(REPO, "apps", "sundial", "Art", "Source", "plants", "leafplant")
SHEET = os.path.join(SRC, "parts-sheet-v1.png")
RES = os.path.join(REPO, "apps", "sundial", "Assets", "Resources", "LeafPlant")

# Palette the atlas is graded to. Sage leaf, coral-pink petal and ink are the house values of the plant cards.
SAGE = np.array([126.0, 140.0, 90.0])
PETAL = np.array([226.0, 150.0, 164.0])
INK = np.array([52.0, 40.0, 26.0])
# T-SUN-049. Ink the sheet draws as the outline of a piece (within OUTLINE_BAND px of the matte edge) is grown for G1.
# Ink further in (veins, petal creases) is a separate pencil layer: not grown, and only PENCIL_MIX of the way to a
# lighter graphite brown, so at G1 it is a faint line and not a second outline.
PENCIL = np.array([104.0, 92.0, 70.0])
PENCIL_MIX = 0.50
OUTLINE_BAND = 5.5
GRADE_STRENGTH_LEAF = 0.85
GRADE_STRENGTH_PETAL = 0.75
INK_GROW_PX = 1.2
# Stems and tufts are thin on screen: their ink is left as drawn, or the whole piece goes black.
INK_GROW_THIN_PX = 0.0
PAD = 6

FLOWER_KINDS = ("flower", "flowerside", "bud")


def petal_midday(rgb):
    return (rgb[..., 0] - rgb[..., 1]) > 30


def petal_morning(rgb):
    return ((rgb[..., 0] - rgb[..., 2]) > 70) & ((rgb[..., 1] - rgb[..., 2]) > 45)


def petal_evening(rgb):
    return ((rgb[..., 2] - rgb[..., 1]) > 18) & ((rgb[..., 2] - rgb[..., 0]) > -10)


# Per species: the sheet, the palette the pieces are pulled to, and which pixels are petal.
SPECIES = {
    "midday": dict(sheet="parts-sheet-v1.png", petal_fn=petal_midday, leaf=SAGE, petal=PETAL, bud=PETAL,
                   what="coral-pink phlox-like herb"),
    "morning": dict(sheet="morning-parts-sheet-v1.png", petal_fn=petal_morning,
                    leaf=np.array([130.0, 150.0, 92.0]), petal=np.array([238.0, 200.0, 96.0]),
                    bud=np.array([226.0, 164.0, 56.0]), what="sage-like herb with buttercup-yellow flowers"),
    "evening": dict(sheet="evening-parts-sheet-v1.png", petal_fn=petal_evening,
                    leaf=np.array([150.0, 166.0, 140.0]), petal=np.array([158.0, 118.0, 196.0]),
                    bud=np.array([112.0, 82.0, 164.0]), what="lavender"),
}


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


def features(lab, idx, rgb, sl, petal_fn):
    m = (lab[sl] == idx)
    h = sl[0].stop - sl[0].start
    w = sl[1].stop - sl[1].start
    area = int(m.sum())
    px = rgb[sl][m]
    petal = petal_fn(px[None, ...])[0]
    pink = float(petal.mean())
    green = float(((px[:, 1] - px[:, 0]) > 8).mean())
    fill = area / float(w * h)
    return m, dict(w=w, h=h, area=area, fill=fill, pink=pink, green=green, mw=area / float(h))


def classify(species, f):
    """Kind of one cut piece from its colour and shape. None when it fits no kind (it is listed as skipped)."""
    w, h, fill, pink, green = f["w"], f["h"], f["fill"], f["pink"], f["green"]
    if species == "midday":
        if pink > 0.8 and 0.88 <= w / float(h) <= 1.15 and fill > 0.6:
            return "flower"
        if pink > 0.7 and w > 130:
            return "flowerside"
        if 0.3 < pink < 0.75 and w < 75:
            return "bud"
        if green > 0.3 and fill < 0.3 and h > 250:
            return "stem"
        if green > 0.25 and h < 150 and fill < 0.45:
            return "tuft"
        if green > 0.4 and fill >= 0.5:
            return "leaf"
        return None
    if species == "morning":
        if pink > 0.55 and 0.85 <= w / float(h) <= 1.2 and fill > 0.5:
            return "flower"
        if 0.25 < pink < 0.85 and h > w and h < 140:
            return "bud"
        if pink > 0.3 and w > 1.2 * h:
            return "flowerside"
        if ((fill < 0.45 and h / float(w) > 3.0) or (fill < 0.3 and h / float(w) > 2.0)) and h > 110 and pink < 0.2:
            return "stem"
        if w / float(h) >= 1.0 and h < 140 and pink < 0.2:
            return "tuft"
        if fill >= 0.45 and pink < 0.25:
            return "leaf"
        return None
    if species == "evening":
        if pink > 0.35:
            return "flowerside" if h > 220 else "bud"
        if h > 200 and (fill < 0.3 or f["mw"] < 12):
            return "stem"
        if w / float(h) >= 0.85 and h < 190 and fill < 0.6:
            return "tuft"
        if fill >= 0.4 and h / float(w) > 2.2:
            return "leaf"
        return None
    raise ValueError(species)


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


def bare_run(m, sl):
    """The longest run of rows whose width is close to the shaft width: the part of a stem piece with no leaf nubs.
    Returns [y_top, y_bottom, half_width_px] in sheet pixels (y down)."""
    rows = np.where(m.any(axis=1))[0]
    widths = np.array([np.ptp(np.nonzero(m[r])[0]) + 1 for r in rows], dtype=np.float32)
    shaft = float(np.percentile(widths, 25))
    ok = widths <= shaft * 1.5 + 2.0
    best, cur, start, best_span = 0, 0, 0, (0, 0)
    for i, flag in enumerate(ok):
        if flag:
            if cur == 0:
                start = i
            cur += 1
            if cur > best:
                best, best_span = cur, (start, i)
        else:
            cur = 0
    r0, r1 = rows[best_span[0]], rows[best_span[1]]
    return [int(sl[0].start + r0), int(sl[0].start + r1), round(shaft * 0.5, 1)]


KIND_CODE = {"leaf": 1, "flower": 2, "flowerside": 3, "bud": 4, "stem": 5, "tuft": 6}


def grade(rgb, kindmap, fg, ink, spec):
    """Pull leaf green and petal colour to the species palette. Ink is left alone here (see compose_ink)."""
    out = rgb.copy()
    petal = spec["petal_fn"](rgb) & ~ink & fg
    rest = fg & ~ink & ~petal
    leaf_kinds = np.isin(kindmap, [KIND_CODE[k] for k in ("leaf", "stem", "tuft")])
    base = rest & leaf_kinds
    if base.sum() == 0:
        base = rest
    gain = spec["leaf"] / rgb[base].mean(axis=0)
    gain = 1.0 + (gain - 1.0) * GRADE_STRENGTH_LEAF
    out[rest] = np.clip(rgb[rest] * gain, 0, 255)
    for name, kinds in (("petal", ("flower", "flowerside")), ("bud", ("bud",))):
        sel = petal & np.isin(kindmap, [KIND_CODE[k] for k in kinds])
        if sel.sum() == 0:
            continue
        g = spec[name] / rgb[sel].mean(axis=0)
        g = 1.0 + (g - 1.0) * GRADE_STRENGTH_PETAL
        out[sel] = np.clip(rgb[sel] * g, 0, 255)
    return out


def split_ink(rgb, fg):
    """Ink pixels, and which of them are the outline of the piece (near the matte edge) and which are inside it."""
    # dark and not saturated: the dark indigo petals of the lavender buds are dark too, and are not ink
    chroma = rgb.max(axis=2) - rgb.min(axis=2)
    ink = (rgb.sum(axis=2) < 330) & (chroma < 50) & fg
    inside = ndi.distance_transform_edt(fg)
    outline = ink & (inside <= OUTLINE_BAND)
    return ink, outline, ink & ~outline


def compose_ink(graded, alpha, ink, outline, veins, thin):
    """Outline ink grown for G1 and set to the house ink. Inside ink becomes the pencil layer: the colour under it
    pulled PENCIL_MIX of the way to graphite, one sheet pixel wide as drawn, never grown."""
    # colour of the surrounding fill under every ink pixel (nearest non-ink graded colour)
    idx = ndi.distance_transform_edt(ink, return_distances=False, return_indices=True)
    under = graded[idx[0], idx[1]]
    out = graded.copy()
    out[ink] = under[ink]
    out[veins] = under[veins] * (1.0 - PENCIL_MIX) + PENCIL * PENCIL_MIX
    grown = np.zeros_like(outline)
    for sel, r in ((outline & ~thin, INK_GROW_PX), (outline & thin, INK_GROW_THIN_PX)):
        k = max(1, int(np.ceil(r)))
        yy, xx = np.mgrid[-k:k + 1, -k:k + 1]
        disc = (xx * xx + yy * yy) <= r * r + 0.25
        grown |= ndi.binary_dilation(sel, structure=disc)
    out[grown] = INK
    # the grown ink may sit just outside the old matte: let alpha follow it, with a soft outer edge
    soft = np.clip(ndi.gaussian_filter(grown.astype(np.float32), 0.7) * 1.4, 0, 1)
    a = np.maximum(alpha, np.where(ndi.binary_dilation(grown, iterations=1), soft, 0.0))
    return out, np.clip(a, 0, 1), grown


def bleed(rgb, alpha):
    """Fill colour under the transparent pixels so filtering never pulls in the white paper."""
    solid = alpha > 0.5
    idx = ndi.distance_transform_edt(~solid, return_distances=False, return_indices=True)
    return rgb[idx[0], idx[1]]


def slice_species(name, debug=None, sheet=None, layers_dir=None):
    spec = SPECIES[name]
    path = sheet or os.path.join(SRC, spec["sheet"])
    rgb, alpha, fg = load_matte(path)
    lab, n = ndi.label(fg, structure=np.ones((3, 3)))
    parts = []
    skipped = []
    objs = ndi.find_objects(lab)
    kindmap = np.zeros(lab.shape, dtype=np.uint8)
    for i in range(1, n + 1):
        area = int((lab[objs[i - 1]] == i).sum())
        if area < 150:
            continue
        m, feat = features(lab, i, rgb, objs[i - 1], spec["petal_fn"])
        kind = classify(name, feat)
        if kind is None:
            skipped.append({"component": i, "features": {k: round(float(v), 3) for k, v in feat.items()}})
            continue
        sub = kindmap[objs[i - 1]]
        sub[m] = KIND_CODE[kind]
        parts.append((kind, objs[i - 1], m, feat, i))
    ink, outline, veins = split_ink(rgb, fg)
    graded = grade(rgb, kindmap, fg, ink, spec)
    thin = np.isin(kindmap, [KIND_CODE["stem"], KIND_CODE["tuft"]])
    graded, alpha2, grown = compose_ink(graded, alpha, ink, outline, veins, thin)
    graded = bleed(graded, alpha2)
    atlas = np.dstack([np.clip(graded, 0, 255), alpha2 * 255.0]).astype(np.uint8)

    H, W = alpha.shape
    order = {"leaf": 0, "flower": 1, "flowerside": 2, "bud": 3, "stem": 4, "tuft": 5}
    parts.sort(key=lambda p: (order[p[0]], -p[3]["area"]))
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
            entry["bare"] = bare_run(m, sl)
        counts[kind] = counts.get(kind, 0) + 1
        out_parts.append(entry)
    os.makedirs(RES, exist_ok=True)
    png = os.path.join(RES, name + "-parts.png")
    Image.fromarray(atlas, "RGBA").save(png)
    meta = {"atlas": name + "-parts", "species": name, "size": [W, H], "counts": counts, "skipped": skipped, "parts": out_parts,
            "ink": {"outline_px": int(grown.sum()), "pencil_px": int(veins.sum()), "outline_band": OUTLINE_BAND,
                    "grow_px": INK_GROW_PX, "pencil_mix": PENCIL_MIX}}
    with open(os.path.join(RES, name + "-parts.json"), "w") as f:
        json.dump(meta, f, indent=1)
    with open(png + ".provenance.txt", "w") as f:
        f.write(
            "generator: agy (Antigravity CLI image tool) parts sheet, sliced and graded by apps/sundial/Art/Scripts/leafplant_s3.py\n"
            "source: apps/sundial/Art/Source/plants/leafplant/%s (prompt only, no reference image)\n"
            "derived_from_art_reference: no\n"
            "grade: leaf and petal colour pulled to the species palette in the script; outline ink grown, interior ink "
            "(veins) kept thin as a separate lighter pencil layer\n" % os.path.basename(path)
        )
    if layers_dir:
        os.makedirs(layers_dir, exist_ok=True)
        ol = np.zeros(atlas.shape, np.uint8)
        ol[..., :3] = INK.astype(np.uint8)
        ol[..., 3] = (grown * 255).astype(np.uint8)
        Image.fromarray(ol, "RGBA").save(os.path.join(layers_dir, name + "-layer-outline.png"))
        pl = np.zeros(atlas.shape, np.uint8)
        pl[..., :3] = PENCIL.astype(np.uint8)
        pl[..., 3] = (veins * 255 * PENCIL_MIX).astype(np.uint8)
        Image.fromarray(pl, "RGBA").save(os.path.join(layers_dir, name + "-layer-pencil.png"))
    print(name, "parts", counts, "skipped", len(skipped), "outline px", int(grown.sum()), "pencil px", int(veins.sum()), "->", png)
    for sk in skipped:
        print("  skipped", sk)
    if debug:
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
        bgc.convert("RGB").save(debug)
        print("debug", debug)
    return out_parts


def cmd_slice(args):
    names = list(SPECIES) if args.species == "all" else [args.species]
    for name in names:
        debug = None
        if args.debug:
            root, ext = os.path.splitext(args.debug)
            debug = args.debug if len(names) == 1 else "%s-%s%s" % (root, name, ext)
        slice_species(name, debug, args.sheet, args.layers)


def load_rgb(path):
    return Image.open(path).convert("RGB")


def shrink(im, scale):
    if scale >= 0.999:
        return im
    return im.resize((max(1, int(im.width * scale)), max(1, int(im.height * scale))), Image.LANCZOS)


def cmd_flip(args):
    a, b = shrink(load_rgb(args.a), args.scale), shrink(load_rgb(args.b), args.scale)
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
    frames = [shrink(load_rgb(f), args.scale) for f in args.frames]
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
    """Median ink stroke width in pixels inside a box. The crop is upsampled 4x (bicubic) so the cut at mean-channel 130
    lands between pixels, then width = twice the distance to the nearest non-ink pixel along the medial axis.
    With --other, only pixels that differ from that frame count, so the room, the wash and the soil drop out and the
    stroke is the plant's own (use the same state with the other variant)."""
    import cv2
    img = np.asarray(load_rgb(args.frame)).astype(np.float32)
    x0, y0, x1, y1 = args.box
    crop = img[y0:y1, x0:x1]
    keep = np.ones(crop.shape[:2], dtype=bool)
    if args.other:
        oth = np.asarray(load_rgb(args.other)).astype(np.float32)[y0:y1, x0:x1]
        keep = np.abs(crop - oth).max(axis=2) > args.diff
        keep = ndi.binary_dilation(keep, iterations=1)
    up = cv2.resize(crop, None, fx=4, fy=4, interpolation=cv2.INTER_CUBIC)
    keep_up = cv2.resize(keep.astype(np.uint8), None, fx=4, fy=4, interpolation=cv2.INTER_NEAREST) > 0
    # Mean of the channels, the same cut as plants_key.ink_width and the T-SUN-028 contour gate (luma < 130).
    luma = up.mean(axis=2)
    ink = (luma < args.threshold) & keep_up
    dist = ndi.distance_transform_edt(ink)
    mx = ndi.maximum_filter(dist, size=3)
    axis = (dist >= mx - 1e-6) & (dist > 1.0)
    widths = dist[axis] * 2.0 / 4.0
    widths = widths[widths <= args.max_width]
    hist, edges = np.histogram(widths, bins=np.arange(0, args.max_width + 0.5, 0.5))
    modal = float(hist.max() / max(hist.sum(), 1))
    stats = {
        "frame": os.path.basename(args.frame),
        "box": args.box,
        "ink_px": int(ink.sum() / 16),
        "axis_samples": int(len(widths)),
        "median_width_px": round(float(np.median(widths)), 3) if len(widths) else None,
        "p25": round(float(np.percentile(widths, 25)), 3) if len(widths) else None,
        "p75": round(float(np.percentile(widths, 75)), 3) if len(widths) else None,
        "modal_0p5_bin_share": round(modal, 3),
    }
    print(json.dumps(stats))
    if args.out:
        with open(args.out, "w") as f:
            json.dump(stats, f, indent=1)


def main(argv):
    p = argparse.ArgumentParser()
    sub = p.add_subparsers(dest="cmd", required=True)
    s = sub.add_parser("slice")
    s.add_argument("--species", default="all", choices=list(SPECIES) + ["all"])
    s.add_argument("--sheet", default=None)
    s.add_argument("--layers", default=None, help="write the outline and pencil layers here")
    s.add_argument("--debug", default=None)
    s.set_defaults(fn=cmd_slice)
    s = sub.add_parser("flip")
    s.add_argument("a")
    s.add_argument("b")
    s.add_argument("out")
    s.add_argument("--scale", type=float, default=1.0)
    s.set_defaults(fn=cmd_flip)
    s = sub.add_parser("grey")
    s.add_argument("a")
    s.add_argument("b")
    s.add_argument("out")
    s.set_defaults(fn=cmd_grey)
    s = sub.add_parser("orbit")
    s.add_argument("out")
    s.add_argument("--scale", type=float, default=1.0)
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
    s.add_argument("--threshold", type=float, default=130.0)
    s.add_argument("--other", default=None)
    s.add_argument("--diff", type=float, default=24.0)
    s.add_argument("--max-width", type=float, default=8.0)
    s.add_argument("--out", default=None)
    s.set_defaults(fn=cmd_contour)
    args = p.parse_args(argv)
    args.fn(args)


if __name__ == "__main__":
    main(sys.argv[1:])
