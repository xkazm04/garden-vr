"""Spike S5 (T-TER-045) sheets and numbers. Reads the PNGs s5_shots.py wrote into the run folder.

    python apps/terrarium/Art/Scripts/s5_metrics.py sheets    # greyscale side-by-sides, flip GIFs, crop strips, ablation strip
    python apps/terrarium/Art/Scripts/s5_metrics.py metrics   # regions of the JarG1 frame vs the reference -> s5-metrics.json

Regions are boxes in jar space projected with the JarG1 camera (spec_anchor.project), the same camera the captures use:
  interior  the air inside the glass above the moss     (the chord haze should light it, the glass stays clear)
  cork      the cork body
  steam     the air just above the cork                 (the plume)
  desk      the varnished desk in front of the foot     (the reflection streak)
  spores    two side zones around the jar               (gold streak count)
Glass p5 / p50, edge energy and the three glow windows (crozier, glass mean, ring) are the S1/S2 measures, so the S5 looks
can be checked against the T-TER-032 floor and ceiling.
"""
import json
import os
import sys

import numpy as np
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
REPO = os.path.normpath(os.path.join(HERE, "..", "..", "..", ".."))
RUN = os.path.join(REPO, "orchestration", "runs", "terrarium", "T-TER-045")
REF = os.path.join(REPO, "shared", "assets", "art-reference", "A1-03-night-moss-1.png")
LOOKS = ["a", "s1", "b", "c", "c1"]
LABELS = {"a": "A locked", "s1": "S1 (host-reviewed)", "b": "B = s5", "c": "C = s2b2+s5 (PC)", "c1": "C1 = s2b1+s5 (Quest)",
          "b-nohaze": "B no haze", "b-nosteam": "B no steam", "b-nospore": "B no spores", "b-nodesk": "B no desk", "b-nocork": "B locked cork"}
ABLATE = ["b", "b-nohaze", "b-nosteam", "b-nospore", "b-nodesk", "b-nocork"]
G1_BOX = (690, 150, 1134, 900)

REGIONS = {
    "interior": [(-0.030, 0.045, 0.0), (0.030, 0.105, 0.0)],
    "cork": [(-0.028, 0.119, 0.0), (0.028, 0.133, 0.0)],
    "steam": [(-0.030, 0.146, 0.0), (0.030, 0.172, 0.0)],
    "desk": [(-0.035, 0.0, -0.110), (0.035, 0.0, -0.055)],
}
SPORE_ZONES = [[(-0.115, 0.02, 0.0), (-0.050, 0.17, 0.0)], [(0.050, 0.02, 0.0), (0.115, 0.17, 0.0)]]


def path(look, frame):
    return os.path.join(RUN, "%s-%s.png" % (look, frame))


def grey(im):
    return im.convert("L").convert("RGB")


def strip(images, labels, out, box=None, scale=1.0, convert=None):
    ims = []
    for p in images:
        im = Image.open(p).convert("RGB")
        if box:
            im = im.crop(box)
        if scale != 1.0:
            im = im.resize((int(im.width * scale), int(im.height * scale)), Image.LANCZOS)
        if convert:
            im = convert(im)
        ims.append(im)
    w, h = ims[0].size
    sheet = Image.new("RGB", (w * len(ims), h + 22), (24, 24, 24))
    d = ImageDraw.Draw(sheet)
    for i, (im, lab) in enumerate(zip(ims, labels)):
        sheet.paste(im, (i * w, 22))
        d.text((i * w + 6, 5), lab, fill=(230, 230, 230))
    sheet.save(os.path.join(RUN, out))


def flip(names, out, box=None, scale=1.0):
    ims = []
    for n in names:
        im = Image.open(path(n[0], n[1])).convert("RGB")
        if box:
            im = im.crop(box)
        if scale != 1.0:
            im = im.resize((int(im.width * scale), int(im.height * scale)), Image.LANCZOS)
        ims.append(im)
    ims[0].save(os.path.join(RUN, out), save_all=True, append_images=ims[1:], duration=700, loop=0)


def sheets():
    strip([REF] + [path(l, "jar-g1") for l in LOOKS], ["reference"] + [LABELS[l] for l in LOOKS], "crops-g1.png", box=G1_BOX)
    strip([REF] + [path(l, "jar-g1") for l in LOOKS], ["reference"] + [LABELS[l] for l in LOOKS], "grey-sbs-g1.png", box=G1_BOX, convert=grey)
    strip([path(l, "jar-g1") for l in ABLATE], [LABELS[l] for l in ABLATE], "crops-ablation.png", box=G1_BOX, scale=0.8)
    strip([path(l, "seated") for l in LOOKS], [LABELS[l] for l in LOOKS], "grey-sbs-seated.png", box=(560, 150, 1360, 900), scale=0.6, convert=grey)
    strip([path(l, "seated") for l in LOOKS], [LABELS[l] for l in LOOKS], "crops-seated.png", box=(560, 150, 1360, 900), scale=0.6)
    for frame, name in (("glass-base", "crops-glass-base.png"), ("glass-rim", "crops-glass-rim.png"), ("moss-close", "crops-moss-close.png")):
        looks = [l for l in ("a", "b", "c") if os.path.isfile(path(l, frame))]
        if looks:
            strip([path(l, frame) for l in looks], [LABELS[l] for l in looks], name, scale=0.5)
    flip([("a", "jar-g1"), ("b", "jar-g1")], "flip-a-b.gif", box=(560, 150, 1260, 960), scale=0.8)
    flip([("a", "jar-g1"), ("s1", "jar-g1"), ("b", "jar-g1"), ("c", "jar-g1")], "flip-g1.gif", box=(560, 150, 1260, 960), scale=0.8)
    flip([("a", "seated"), ("b", "seated"), ("c", "seated")], "flip-seated.gif", box=(560, 150, 1360, 900), scale=0.6)
    print("sheets written")


def rect(anchor, corners):
    pts = [anchor.project(c) for c in corners]
    xs = [p[0] for p in pts]
    ys = [p[1] for p in pts]
    return int(min(xs)), int(min(ys)), int(max(xs)), int(max(ys))


def region_stats(anchor, image, box):
    x0, y0, x1, y1 = box
    px = image[y0:y1, x0:x1].reshape(-1, 3)
    mean = px.mean(axis=0)
    luma = anchor.relative_luminance(px.reshape(-1, 1, 3)).mean()
    return {"rect": [x0, y0, x1, y1], "meanRGB": [round(float(v), 1) for v in mean], "meanLuma": round(float(luma), 4),
            "greenShare": round(float(mean[1] / max(mean.sum(), 1e-6)), 4)}


def gold_count(anchor, image, boxes):
    from scipy import ndimage
    total = 0
    pixels = 0
    for box in boxes:
        x0, y0, x1, y1 = box
        sub = image[y0:y1, x0:x1]
        r, g, b = sub[..., 0], sub[..., 1], sub[..., 2]
        mask = (r > 130) & (g > 95) & (r > b + 45) & (g > b + 20)
        labels, n = ndimage.label(mask)
        total += int(n)
        pixels += int(mask.sum())
    return total, pixels


def metrics():
    import s1_structured_glass as s1
    anchor = s1._import_anchor()
    body = s1.glass_body_mask(anchor)
    edge = s1.silhouette_mask(anchor)
    boxes = {k: rect(anchor, v) for k, v in REGIONS.items()}
    spore_boxes = [rect(anchor, z) for z in SPORE_ZONES]
    rows = {}
    names = [("reference", REF)] + [(l, path(l, "jar-g1")) for l in LABELS if os.path.isfile(path(l, "jar-g1"))]
    for name, p in names:
        luma = s1.load_luma(anchor, p)
        image = anchor.load_rgb(p)
        p5, p50, _ = s1.glass_p5(luma, body)
        glow = anchor.measure_glow(image)
        row = {
            "glassP5": round(p5, 4), "glassP50": round(p50, 4), "edgeEnergy": round(s1.edge_energy(luma, edge), 6),
            "glassMean": glow["measured"]["glassMean"], "crozierPeak": glow["measured"]["crozierPeak"],
            "ringPeak": glow["measured"]["ringPeak"], "glowInside": glow["inside"],
        }
        for key, box in boxes.items():
            row[key] = region_stats(anchor, image, box)
        n, px = gold_count(anchor, image, spore_boxes)
        row["sporeComponents"] = n
        row["sporePixels"] = px
        rows[name] = row
    out = {"regions": {k: list(v) for k, v in boxes.items()}, "sporeZones": [list(b) for b in spore_boxes], "images": rows}
    with open(os.path.join(RUN, "s5-metrics.json"), "w") as f:
        json.dump(out, f, indent=2)
    hdr = "%-10s %7s %7s %8s | %8s %8s %8s | %8s %8s %8s %8s | %6s %6s"
    print(hdr % ("image", "p5", "p50", "edge", "glassMn", "crozier", "ring", "interior", "intGreen", "cork", "steam", "desk", "spores"))
    for k, v in rows.items():
        print("%-10s %7.4f %7.4f %8.5f | %8.4f %8.4f %8.4f | %8.4f %8.4f %8.4f %8.4f | %6.4f %6d" % (
            k, v["glassP5"], v["glassP50"], v["edgeEnergy"], v["glassMean"], v["crozierPeak"], v["ringPeak"],
            v["interior"]["meanLuma"], v["interior"]["greenShare"], v["cork"]["meanLuma"], v["steam"]["meanLuma"],
            v["desk"]["meanLuma"], v["sporeComponents"]))
        print("           glow inside:", v["glowInside"])


if __name__ == "__main__":
    {"sheets": sheets, "metrics": metrics}[sys.argv[1]]()
