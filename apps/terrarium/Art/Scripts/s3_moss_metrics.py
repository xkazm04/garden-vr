"""Spike S3 (T-TER-042) measurements and sheets. Reads the PNGs s3_moss_shots.py wrote.

    python apps/terrarium/Art/Scripts/s3_moss_metrics.py msaa      # MSAA x alpha-to-coverage error table
    python apps/terrarium/Art/Scripts/s3_moss_metrics.py sheets    # greyscale side-by-sides and flip GIFs
    python apps/terrarium/Art/Scripts/s3_moss_metrics.py moss      # moss-region colour and edge numbers, A vs B vs reference
"""
import json
import os
import sys

import numpy as np
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, "..", "..", "..", ".."))
RUN = os.path.join(REPO, "orchestration", "runs", "terrarium", "T-TER-042")
REF = os.path.join(REPO, "shared", "assets", "art-reference", "A1-03-night-moss-1.png")


def load(name):
    return np.asarray(Image.open(os.path.join(RUN, name)).convert("RGB")).astype(np.float32) / 255.0


def luma(rgb):
    return rgb @ np.array([0.2126, 0.7152, 0.0722], np.float32)


def msaa():
    truth = np.asarray(Image.open(os.path.join(RUN, "b-msaa-supersample.png")).convert("RGB").resize((1024, 1024), Image.BOX)).astype(np.float32) / 255.0
    truth_l = luma(truth)
    # Silhouette pixels of the moss: where the supersampled image has a strong local gradient.
    gy, gx = np.gradient(truth_l)
    edge = np.hypot(gx, gy) > 0.04
    rows = []
    for m in (1, 2, 4, 8):
        for a in (0, 1):
            img = load("b-msaa%d-a2c%d.png" % (m, a))
            diff = np.abs(img - truth).mean(axis=2)
            l = luma(img)
            jy, jx = np.gradient(l)
            hard = (np.hypot(jx, jy) > 0.12).sum()
            rows.append({
                "msaa": m, "a2c": a,
                "mad_all": round(float(diff.mean()), 5),
                "mad_edges": round(float(diff[edge].mean()), 5),
                "hard_edge_pixels": int(hard),
            })
    out = {"truth": "b-msaa-supersample.png, 2048 at 8x, box filtered to 1024", "edge_pixels": int(edge.sum()), "rows": rows}
    with open(os.path.join(RUN, "msaa-sweep.json"), "w") as f:
        json.dump(out, f, indent=1)
    print("edge pixels in the reference:", int(edge.sum()))
    print("msaa a2c   mad_all  mad_edges  hard_edge_px")
    for r in rows:
        print("%4d %3d  %8.5f  %9.5f  %12d" % (r["msaa"], r["a2c"], r["mad_all"], r["mad_edges"], r["hard_edge_pixels"]))


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


def sheets():
    grey_sbs("a-jar-g1.png", "b-jar-g1.png", "grey-sbs-g1.png", "A locked", "B s3", ref=True)
    grey_sbs("a-moss-close.png", "b-moss-close.png", "grey-sbs-moss-close.png", "A locked", "B s3")
    grey_sbs("a-seated.png", "b-seated.png", "grey-sbs-seated.png", "A locked", "B s3")
    flip("a-jar-g1.png", "b-jar-g1.png", "flip-g1.gif", box=(560, 380, 1260, 960), scale=0.8)
    flip("a-moss-close.png", "b-moss-close.png", "flip-moss-close.gif", scale=0.6)
    flip("a-seated.png", "b-seated.png", "flip-seated.gif", box=(600, 330, 1250, 900), scale=0.8)
    print("sheets written")


def region_stats(img, box):
    x0, y0, x1, y1 = box
    c = img[y0:y1, x0:x1]
    l = luma(c)
    gy, gx = np.gradient(l)
    return {
        "mean_rgb": [round(float(v), 4) for v in c.reshape(-1, 3).mean(axis=0)],
        "mean_luma": round(float(l.mean()), 4),
        "p5_luma": round(float(np.percentile(l, 5)), 4),
        "p95_luma": round(float(np.percentile(l, 95)), 4),
        "luma_std": round(float(l.std()), 4),
        "edge_energy": round(float(np.hypot(gx, gy).mean()), 5),
    }


def moss():
    ref = np.asarray(Image.open(REF).convert("RGB")).astype(np.float32) / 255.0
    a, b = load("a-jar-g1.png"), load("b-jar-g1.png")
    # The moss band at JarG1: from above the soil line down to the soil. Same box on all three images.
    boxes = {"moss": (790, 700, 1040, 790), "soil": (790, 790, 1040, 880)}
    out = {"boxes": boxes}
    for name, box in boxes.items():
        out[name] = {"reference": region_stats(ref, box), "A": region_stats(a, box), "B": region_stats(b, box)}
    with open(os.path.join(RUN, "moss-metrics.json"), "w") as f:
        json.dump(out, f, indent=1)
    print(json.dumps(out, indent=1))


if __name__ == "__main__":
    mode = sys.argv[1] if len(sys.argv) > 1 else ""
    {"msaa": msaa, "sheets": sheets, "moss": moss}[mode]()
