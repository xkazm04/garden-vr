"""Spike S4 (T-TER-043) sheets and region numbers. Reads the PNGs s4_shots.py wrote.

    python apps/terrarium/Art/Scripts/s4_metrics.py sheets    # greyscale side-by-sides, flip GIFs, crop strips
    python apps/terrarium/Art/Scripts/s4_metrics.py regions   # frond and crozier region numbers, A vs B vs reference
"""
import json
import os
import sys

import numpy as np
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, "..", "..", "..", ".."))
RUN = os.path.join(REPO, "orchestration", "runs", "terrarium", "T-TER-043")
REF = os.path.join(REPO, "shared", "assets", "art-reference", "A1-03-night-moss-1.png")

# JarG1 plate, 1824 x 1024. Boxes are approximate and the same on all three images.
BOXES = {
    "left frond": (735, 480, 885, 690),
    "right frond": (955, 470, 1135, 700),
    "crozier": (840, 400, 975, 540),
}


def luma(rgb):
    return rgb @ np.array([0.2126, 0.7152, 0.0722], np.float32)


def srgb_to_lab(rgb):
    c = np.where(rgb <= 0.04045, rgb / 12.92, ((rgb + 0.055) / 1.055) ** 2.4)
    m = np.array([[0.4124564, 0.3575761, 0.1804375], [0.2126729, 0.7151522, 0.0721750], [0.0193339, 0.1191920, 0.9503041]])
    xyz = c @ m.T / np.array([0.95047, 1.0, 1.08883])
    f = np.where(xyz > 0.008856, np.cbrt(xyz), 7.787 * xyz + 16.0 / 116.0)
    return np.stack([116.0 * f[..., 1] - 16.0, 500.0 * (f[..., 0] - f[..., 1]), 200.0 * (f[..., 1] - f[..., 2])], axis=-1)


def region_stats(img, box):
    x0, y0, x1, y1 = box
    c = img[y0:y1, x0:x1]
    l = luma(c)
    gy, gx = np.gradient(l)
    lab = srgb_to_lab(c).reshape(-1, 3)
    chroma = np.hypot(lab[:, 1], lab[:, 2])
    return {
        "mean_rgb": [round(float(v) * 255, 1) for v in c.reshape(-1, 3).mean(axis=0)],
        "mean_luma": round(float(l.mean()), 4),
        "p5_luma": round(float(np.percentile(l, 5)), 4),
        "p95_luma": round(float(np.percentile(l, 95)), 4),
        "luma_std": round(float(l.std()), 4),
        "edge_energy": round(float(np.hypot(gx, gy).mean()), 5),
        "mean_chroma": round(float(chroma.mean()), 2),
    }


def load(path):
    return np.asarray(Image.open(path).convert("RGB")).astype(np.float32) / 255.0


def grey_sbs(a, b, out, label_a, label_b, ref=None, box=None):
    ims = [Image.open(os.path.join(RUN, a)).convert("L").convert("RGB"), Image.open(os.path.join(RUN, b)).convert("L").convert("RGB")]
    labels = [label_a, label_b]
    if ref:
        ims.insert(0, Image.open(REF).convert("L").convert("RGB"))
        labels.insert(0, "reference")
    if box:
        ims = [i.crop(box) for i in ims]
    w, h = ims[0].size
    sheet = Image.new("RGB", (w * len(ims), h + 22), (24, 24, 24))
    d = ImageDraw.Draw(sheet)
    for i, (im, lab) in enumerate(zip(ims, labels)):
        sheet.paste(im, (i * w, 22))
        d.text((i * w + 6, 5), lab, fill=(230, 230, 230))
    sheet.save(os.path.join(RUN, out))


def flip(a, b, out, box=None, scale=1.0):
    ims = [Image.open(os.path.join(RUN, n)).convert("RGB") for n in (a, b)]
    if box:
        ims = [i.crop(box) for i in ims]
    if scale != 1.0:
        ims = [i.resize((int(i.width * scale), int(i.height * scale)), Image.LANCZOS) for i in ims]
    ims[0].save(os.path.join(RUN, out), save_all=True, append_images=[ims[1]], duration=700, loop=0)


def strip(paths, labels, out, box=None, scale=1.0):
    ims = [Image.open(p).convert("RGB") for p in paths]
    if box:
        ims = [i.crop(box) for i in ims]
    if scale != 1.0:
        ims = [i.resize((int(i.width * scale), int(i.height * scale)), Image.LANCZOS) for i in ims]
    w, h = ims[0].size
    sheet = Image.new("RGB", (w * len(ims), h + 22), (24, 24, 24))
    d = ImageDraw.Draw(sheet)
    for i, (im, lab) in enumerate(zip(ims, labels)):
        sheet.paste(im, (i * w, 22))
        d.text((i * w + 6, 5), lab, fill=(230, 230, 230))
    sheet.save(os.path.join(RUN, out))


def sheets():
    grey_sbs("a-jar-g1.png", "b-jar-g1.png", "grey-sbs-g1.png", "A locked", "B s4", ref=True)
    grey_sbs("a-seated.png", "b-seated.png", "grey-sbs-seated.png", "A locked", "B s4")
    grey_sbs("a-frond-close.png", "b-frond-close.png", "grey-sbs-frond-close.png", "A locked", "B s4")
    flip("a-jar-g1.png", "b-jar-g1.png", "flip-g1.gif", box=(560, 380, 1260, 960), scale=0.8)
    flip("a-seated.png", "b-seated.png", "flip-seated.gif", box=(600, 330, 1250, 900), scale=0.8)
    flip("a-frond-close.png", "b-frond-close.png", "flip-frond-close.gif", scale=0.6)
    box = (700, 420, 1130, 700)
    strip([REF, os.path.join(RUN, "a-jar-g1.png"), os.path.join(RUN, "bf-jar-g1.png"), os.path.join(RUN, "bh-jar-g1.png"), os.path.join(RUN, "b-jar-g1.png")],
          ["reference", "A locked", "B fronds only (s4f)", "B fiddle only (s4h)", "B both (s4)"], "crops-g1.png", box=box)
    strip([os.path.join(RUN, n) for n in ("a-frond-close.png", "b-frond-close.png", "b-frond-close-fuzz0.png", "a-frond-close-open.png", "b-frond-close-open.png")],
          ["A", "B (2 shells)", "B Quest fallback (0 shells)", "A uncoil 0.75", "B uncoil 0.75"], "crops-close.png", scale=0.5)
    # Region boxes drawn on the B frame, so the numbers can be checked by eye.
    im = Image.open(os.path.join(RUN, "b-jar-g1.png")).convert("RGB")
    d = ImageDraw.Draw(im)
    for name, b in BOXES.items():
        d.rectangle(b, outline=(255, 64, 64))
        d.text((b[0] + 3, b[1] + 3), name, fill=(255, 255, 255))
    im.crop((680, 380, 1180, 740)).save(os.path.join(RUN, "region-boxes.png"))
    print("sheets written")


def regions():
    ref, a, b = load(REF), load(os.path.join(RUN, "a-jar-g1.png")), load(os.path.join(RUN, "b-jar-g1.png"))
    bf, bh = load(os.path.join(RUN, "bf-jar-g1.png")), load(os.path.join(RUN, "bh-jar-g1.png"))
    out = {"boxes": BOXES}
    for name, box in BOXES.items():
        out[name] = {k: region_stats(img, box) for k, img in (("reference", ref), ("A", a), ("B", b), ("B fronds only", bf), ("B fiddle only", bh))}
    with open(os.path.join(RUN, "region-metrics.json"), "w") as f:
        json.dump(out, f, indent=1)
    for name in BOXES:
        print(name)
        for k, v in out[name].items():
            print("  %-14s rgb %-20s luma %.3f std %.3f p5 %.3f p95 %.3f edge %.4f chroma %.1f" % (
                k, v["mean_rgb"], v["mean_luma"], v["luma_std"], v["p5_luma"], v["p95_luma"], v["edge_energy"], v["mean_chroma"]))


if __name__ == "__main__":
    mode = sys.argv[1] if len(sys.argv) > 1 else ""
    {"sheets": sheets, "regions": regions}[mode]()
