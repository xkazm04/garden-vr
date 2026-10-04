"""T-TER-047 sheets and numbers. Reads the PNGs sprint_shots.py wrote into the run folder.

    python apps/terrarium/Art/Scripts/sprint_metrics.py metrics   # glow windows, glass, regions, pixel boxes -> sprint-metrics.json, metrics.txt
    python apps/terrarium/Art/Scripts/sprint_metrics.py sheets    # triptych, grey triptych, flip GIFs, seated and close-up sheets
"""
import contextlib
import io
import json
import os
import sys

from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import s5_metrics as s5m  # noqa: E402
import s6_metrics as s6m  # noqa: E402

RUN = os.path.join(s5m.REPO, "orchestration", "runs", "terrarium", "T-TER-047")
REF = s5m.REF
LABELS = {"a": "A (locked)", "s6": "s6 = s2b2+s5+s6 (T-TER-049)", "sprint": "sprint (PC)", "sprintq": "sprint+s2b1, 8 shells, fuzz 0 (Quest)"}
BOX = (640, 150, 1190, 880)
SEATED_BOX = (560, 150, 1360, 900)


def label(im, text):
    d = ImageDraw.Draw(im)
    d.rectangle((0, 0, im.width, 22), fill=(0, 0, 0))
    d.text((6, 5), text, fill=(255, 255, 255))
    return im


def metrics():
    s5m.RUN = RUN
    s5m.LABELS = dict(LABELS)
    buf = io.StringIO()
    with contextlib.redirect_stdout(buf):
        s5m.metrics()
    src = os.path.join(RUN, "s5-metrics.json")
    out = json.load(open(src))
    os.remove(src)
    names = [("reference", REF)] + [(k, os.path.join(RUN, k + "-jar-g1.png")) for k in LABELS]
    for name, p in names:
        out["images"][name]["boxes"] = {k: s6m.box_stats(p, b) for k, b in s6m.BOXES.items()}
    json.dump(out, open(os.path.join(RUN, "sprint-metrics.json"), "w"), indent=2)
    text = buf.getvalue()
    for name, _ in names:
        text += "%s boxes %s\n" % (name, {k: v["meanRGB"] for k, v in out["images"][name]["boxes"].items()})
    open(os.path.join(RUN, "metrics.txt"), "w").write(text)
    print(text)


def sheets():
    ims = [("reference", REF), ("A", os.path.join(RUN, "a-jar-g1.png")), ("sprint", os.path.join(RUN, "sprint-jar-g1.png"))]
    crops = [label(Image.open(p).convert("RGB").crop(BOX), t) for t, p in ims]
    sheet = Image.new("RGB", (sum(c.width for c in crops) + 8 * 2, crops[0].height), (16, 16, 16))
    x = 0
    for c in crops:
        sheet.paste(c, (x, 0))
        x += c.width + 8
    sheet.save(os.path.join(RUN, "triptych.png"))
    grey = Image.new("RGB", sheet.size)
    grey.paste(sheet.convert("L").convert("RGB"), (0, 0))
    grey.save(os.path.join(RUN, "grey-sbs.png"))
    # Seated view: A beside sprint, colour and grey.
    seat = [label(Image.open(os.path.join(RUN, n + "-seated.png")).convert("RGB").crop(SEATED_BOX), t) for n, t in (("a", "A seated"), ("sprint", "sprint seated"), ("sprintq", "sprint Quest seated"))]
    ss = Image.new("RGB", (sum(c.width for c in seat) + 8 * 2, seat[0].height), (16, 16, 16))
    x = 0
    for c in seat:
        ss.paste(c, (x, 0))
        x += c.width + 8
    ss.save(os.path.join(RUN, "seated-compare.png"))
    g = Image.new("RGB", ss.size)
    g.paste(ss.convert("L").convert("RGB"), (0, 0))
    g.save(os.path.join(RUN, "grey-sbs-seated.png"))
    frames = [label(Image.open(os.path.join(RUN, n + "-jar-g1.png")).convert("RGB").crop(BOX), t) for n, t in (("a", "A"), ("sprint", "sprint"))]
    frames[0].save(os.path.join(RUN, "flip-a-sprint.gif"), save_all=True, append_images=frames[1:], duration=900, loop=0)
    ref = Image.open(REF).convert("RGB").crop(BOX)
    refs = [label(ref.copy(), "reference"), frames[0], label(ref.copy(), "reference"), frames[1]]
    refs[0].save(os.path.join(RUN, "flip-ref-a-sprint.gif"), save_all=True, append_images=refs[1:], duration=900, loop=0)
    sframes = [label(Image.open(os.path.join(RUN, n + "-seated.png")).convert("RGB").crop(SEATED_BOX), t) for n, t in (("a", "A"), ("sprint", "sprint"))]
    sframes[0].save(os.path.join(RUN, "flip-seated.gif"), save_all=True, append_images=sframes[1:], duration=900, loop=0)
    ab = ["a", "s6", "sprint", "sprintq"]
    for name, tag in (("crops-moss-close", "moss-close"), ("crops-glass-base", "glass-base"), ("crops-glass-rim", "glass-rim")):
        tiles = [label(Image.open(os.path.join(RUN, "%s-%s.png" % (n, tag))).convert("RGB").resize((480, 480)), n) for n in ab]
        sh = Image.new("RGB", (480 * len(tiles) + 6 * (len(tiles) - 1), 480), (16, 16, 16))
        for i, t in enumerate(tiles):
            sh.paste(t, (i * 486, 0))
        sh.save(os.path.join(RUN, name + ".png"))
    abl = ["sprint", "sprint-nohaze", "sprint-b0", "sprint-b1", "sprint-nosteam", "sprint-nospore", "sprint-nodesk"]
    tiles = [label(Image.open(os.path.join(RUN, n + "-jar-g1.png")).convert("RGB").crop(BOX).resize((412, 548)), n) for n in abl]
    sh = Image.new("RGB", (412 * len(tiles) + 6 * (len(tiles) - 1), 548), (16, 16, 16))
    for i, t in enumerate(tiles):
        sh.paste(t, (i * 418, 0))
    sh.save(os.path.join(RUN, "crops-ablation.png"))
    print("sheets written")


if __name__ == "__main__":
    {"sheets": sheets, "metrics": metrics}[sys.argv[1]]()
