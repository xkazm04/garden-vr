"""T-TER-049 sheets and numbers. Reads the PNGs s6_shots.py wrote into the run folder.

    python apps/terrarium/Art/Scripts/s6_metrics.py metrics   # S5 regions + glow windows + pixel boxes -> s6-metrics.json, metrics.txt
    python apps/terrarium/Art/Scripts/s6_metrics.py sheets    # triptych, greyscale triptych, flip GIFs, crop strips
"""
import json
import os
import sys

import numpy as np
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import s5_metrics as s5m  # noqa: E402

RUN = os.path.join(s5m.REPO, "orchestration", "runs", "terrarium", "T-TER-049")
s5m.RUN = RUN
s5m.LABELS = {"c": "C = s2b2+s5 (PC)", "s6": "s6 = s2b2+s5+s6 (PC)", "c1": "C1 = s2b1+s5 (Quest, before)", "s61": "s6 on s2b1+s5 (Quest)"}
REF = s5m.REF
# Pixel boxes of the JarG1 frame (x0, y0, x1, y1): the cork cap side, the plug through the neck, the interior above the moss,
# the bed (lower glass, moss and soil).
BOXES = {"corkCap": (790, 205, 1040, 235), "plug": (800, 262, 1030, 330), "interior": (800, 380, 1030, 560),
         "bedGlass": (720, 700, 1110, 800), "steamBase": (860, 150, 960, 196)}


def box_stats(path, box):
    a = np.asarray(Image.open(path).convert("RGB")).astype(float)[box[1]:box[3], box[0]:box[2]].reshape(-1, 3)
    lin = ((a / 255.0 + 0.055) / 1.055) ** 2.4
    lin = np.where(a / 255.0 <= 0.04045, a / 255.0 / 12.92, lin)
    luma = float((lin @ np.array([0.2126, 0.7152, 0.0722])).mean())
    return {"meanRGB": [round(v, 1) for v in a.mean(0)], "luma": round(luma, 4)}


def metrics():
    s5m.metrics()
    src = os.path.join(RUN, "s5-metrics.json")
    out = json.load(open(src))
    os.remove(src)
    names = [("reference", REF)] + [(k, os.path.join(RUN, k + "-jar-g1.png")) for k in s5m.LABELS if os.path.isfile(os.path.join(RUN, k + "-jar-g1.png"))]
    for name, p in names:
        out["images"][name]["boxes"] = {k: box_stats(p, b) for k, b in BOXES.items()}
    json.dump(out, open(os.path.join(RUN, "s6-metrics.json"), "w"), indent=2)
    for name, _ in names:
        print(name, {k: v["meanRGB"] for k, v in out["images"][name]["boxes"].items()})


def label(im, text):
    d = ImageDraw.Draw(im)
    d.rectangle((0, 0, im.width, 22), fill=(0, 0, 0))
    d.text((6, 5), text, fill=(255, 255, 255))
    return im


def sheets():
    box = (640, 150, 1190, 880)
    ims = [("reference", REF), ("C = s2b2+s5", os.path.join(RUN, "c-jar-g1.png")), ("s6 = s2b2+s5+s6", os.path.join(RUN, "s6-jar-g1.png"))]
    crops = [label(Image.open(p).convert("RGB").crop(box), t) for t, p in ims]
    sheet = Image.new("RGB", (sum(c.width for c in crops) + 8 * 2, crops[0].height), (16, 16, 16))
    x = 0
    for c in crops:
        sheet.paste(c, (x, 0)); x += c.width + 8
    sheet.save(os.path.join(RUN, "triptych.png"))
    grey = Image.new("RGB", sheet.size)
    grey.paste(sheet.convert("L").convert("RGB"), (0, 0))
    grey.save(os.path.join(RUN, "triptych-grey.png"))
    frames = [Image.open(os.path.join(RUN, n + "-jar-g1.png")).convert("RGB").crop(box) for n in ("c", "s6")]
    frames = [label(f, t) for f, t in zip(frames, ("C", "s6"))]
    frames[0].save(os.path.join(RUN, "flip-c-s6.gif"), save_all=True, append_images=frames[1:], duration=900, loop=0)
    ref = Image.open(REF).convert("RGB").crop(box)
    refs = [label(ref.copy(), "reference"), frames[0], label(ref.copy(), "reference"), frames[1]]
    refs[0].save(os.path.join(RUN, "flip-ref-c-s6.gif"), save_all=True, append_images=refs[1:], duration=900, loop=0)
    for name, framing_box, files in (("crops-seated", None, ("c-seated", "c1-seated", "s6-seated", "s61-seated")),
                                     ("crops-close", None, ("c-glass-base", "s6-glass-base", "c-glass-rim", "s6-glass-rim", "c-moss-close", "s6-moss-close"))):
        ims = [label(Image.open(os.path.join(RUN, f + ".png")).convert("RGB").resize((512, 288) if "seated" in name else (480, 480)), f) for f in files]
        cols = 2 if "seated" in name else 2
        rows = (len(ims) + cols - 1) // cols
        w, h = ims[0].size
        sh = Image.new("RGB", (cols * w + (cols - 1) * 6, rows * h + (rows - 1) * 6), (16, 16, 16))
        for i, im in enumerate(ims):
            sh.paste(im, ((i % cols) * (w + 6), (i // cols) * (h + 6)))
        sh.save(os.path.join(RUN, name + ".png"))
    print("sheets written")


if __name__ == "__main__":
    {"sheets": sheets, "metrics": metrics}[sys.argv[1]]()
