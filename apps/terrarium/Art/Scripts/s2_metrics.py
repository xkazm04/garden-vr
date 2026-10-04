"""Spike S2 (T-TER-044) sheets and numbers. Reads the PNGs s2_shots.py wrote into the run folder.

    python apps/terrarium/Art/Scripts/s2_metrics.py sheets    # greyscale side-by-sides, flip GIFs, crop strips
    python apps/terrarium/Art/Scripts/s2_metrics.py metrics   # glass p5, edge energy, wall band, glow windows -> glass-metrics.json
    python apps/terrarium/Art/Scripts/s2_metrics.py diff      # where B1 and B2 differ (the "what B1 loses" crop)

Metrics reuse the S1 masks (s1_structured_glass.py): the glass body is the front-cylinder hit between the heel and the
lip, the silhouette band is 4 px around the projected wall. The wall band added here is the 30 px inside the
silhouette, where a thick wall and the refraction both live.
"""
import json
import os
import sys

import numpy as np
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
REPO = os.path.normpath(os.path.join(HERE, "..", "..", "..", ".."))
RUN = os.path.join(REPO, "orchestration", "runs", "terrarium", "T-TER-044")
REF = os.path.join(REPO, "shared", "assets", "art-reference", "A1-03-night-moss-1.png")
LOOKS = ["a", "s1", "b0", "b1", "b2"]
LABELS = {"a": "A locked", "s1": "S1 (A mesh)", "b0": "B0 shell", "b1": "B1 baked cube", "b2": "B2 opaque tex"}
G1_BOX = (690, 250, 1134, 900)


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
    # Reference first on the G1 frame.
    strip([REF] + [path(l, "jar-g1") for l in LOOKS], ["reference"] + [LABELS[l] for l in LOOKS], "grey-sbs-g1.png",
          box=G1_BOX, convert=grey)
    strip([REF] + [path(l, "jar-g1") for l in LOOKS], ["reference"] + [LABELS[l] for l in LOOKS], "crops-g1.png", box=G1_BOX)
    strip([path(l, "seated") for l in LOOKS], [LABELS[l] for l in LOOKS], "grey-sbs-seated.png", box=(560, 250, 1360, 900),
          scale=0.6, convert=grey)
    strip([path(l, "glass-base") for l in LOOKS], [LABELS[l] for l in LOOKS], "grey-sbs-glass-base.png", scale=0.5, convert=grey)
    strip([path(l, "glass-rim") for l in LOOKS], [LABELS[l] for l in LOOKS], "grey-sbs-glass-rim.png", scale=0.5, convert=grey)
    strip([path(l, "glass-base") for l in LOOKS], [LABELS[l] for l in LOOKS], "crops-glass-base.png", scale=0.5)
    strip([path(l, "glass-rim") for l in LOOKS], [LABELS[l] for l in LOOKS], "crops-glass-rim.png", scale=0.5)
    strip([path(l, "moss-close") for l in LOOKS], [LABELS[l] for l in LOOKS], "crops-moss-close.png", scale=0.5)
    flip([("a", "jar-g1"), ("b0", "jar-g1"), ("b1", "jar-g1"), ("b2", "jar-g1")], "flip-g1.gif", box=(560, 250, 1260, 960), scale=0.8)
    flip([("a", "seated"), ("b0", "seated"), ("b1", "seated"), ("b2", "seated")], "flip-seated.gif", box=(560, 250, 1360, 900), scale=0.6)
    flip([("b1", "jar-g1"), ("b2", "jar-g1")], "flip-b1-b2.gif", box=(560, 250, 1260, 960), scale=0.8)
    print("sheets written")


def diff():
    a = np.asarray(Image.open(path("b1", "jar-g1")).convert("RGB")).astype(np.int16)
    b = np.asarray(Image.open(path("b2", "jar-g1")).convert("RGB")).astype(np.int16)
    d = np.abs(a - b).max(axis=2)
    heat = np.clip(d * 6, 0, 255).astype(np.uint8)
    Image.fromarray(heat).crop(G1_BOX).save(os.path.join(RUN, "diff-b1-b2.png"))
    print("pixels differing by >8:", int((d > 8).sum()), "max", int(d.max()))


def wall_band(anchor, inner=30):
    """The 30 px just inside the projected silhouette, heel to lip."""
    mask = np.zeros((anchor.H, anchor.W), dtype=bool)
    for y in np.linspace(0.012, 0.118, 80):
        for side in (-1.0, 1.0):
            p = anchor.project((side * 0.045, float(y), 0.0))
            if p is None:
                continue
            x = int(round(p[0]))
            py = int(round(p[1]))
            if side < 0:
                mask[py - 3:py + 4, x - 3:x + inner] = True
            else:
                mask[py - 3:py + 4, x - inner:x + 4] = True
    return mask


def metrics():
    import s1_structured_glass as s1
    anchor = s1._import_anchor()
    body = s1.glass_body_mask(anchor)
    edge = s1.silhouette_mask(anchor)
    band = wall_band(anchor)
    rows = {}
    for name, p in [("reference", REF)] + [(l, path(l, "jar-g1")) for l in LOOKS]:
        luma = s1.load_luma(anchor, p)
        p5, p50, count = s1.glass_p5(luma, body)
        glow = anchor.measure_glow(anchor.load_rgb(p))
        rows[name] = {
            "glassP5": round(p5, 4), "glassP50": round(p50, 4),
            "edgeEnergy": round(s1.edge_energy(luma, edge), 6),
            "wallBandEdgeEnergy": round(s1.edge_energy(luma, band), 6),
            "wallBandMeanLuma": round(float(luma[band].mean()), 4),
            "glassMean": glow["measured"]["glassMean"], "crozierPeak": glow["measured"]["crozierPeak"],
            "ringPeak": glow["measured"]["ringPeak"], "glowInside": glow["inside"],
        }
    out = {"mask": "glass body: front cylinder hit, y 0.012 to 0.124; edge: 4 px around the silhouette; wall band: 30 px inside it",
           "images": rows}
    with open(os.path.join(RUN, "glass-metrics.json"), "w") as f:
        json.dump(out, f, indent=2)
    print("%-10s %7s %7s %9s %9s %8s %8s %8s %8s" % ("image", "p5", "p50", "edge", "wallEdge", "wallLum", "glassMn", "crozier", "ring"))
    for k, v in rows.items():
        print("%-10s %7.4f %7.4f %9.5f %9.5f %8.4f %8.4f %8.4f %8.4f" % (
            k, v["glassP5"], v["glassP50"], v["edgeEnergy"], v["wallBandEdgeEnergy"], v["wallBandMeanLuma"],
            v["glassMean"], v["crozierPeak"], v["ringPeak"]))
        print("           glow inside:", v["glowInside"])


if __name__ == "__main__":
    {"sheets": sheets, "metrics": metrics, "diff": diff}[sys.argv[1]]()
