"""T-SUN-049 evidence from the captured frames: flip GIFs, greyscale side-by-sides, orbit GIFs and montages, stereo pairs,
plant crops for the judge, and the contour (outline width) of each plant at DialG1.

  python apps/sundial/Art/Scripts/leafplant_s3b_post.py            # everything
  python apps/sundial/Art/Scripts/leafplant_s3b_post.py contour    # one step: flips greys orbits stereo crops contour
"""
import json
import os
import subprocess
import sys

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, "..", "..", "..", ".."))
RUN = os.path.join(REPO, "orchestration", "runs", "sundial", "T-SUN-049")
S3 = os.path.join(HERE, "leafplant_s3.py")
REGIONS = os.path.join(REPO, "tools", "fidelity", "regions", "DialG1.json")
ORBITS = (-15, -10, -5, 0, 5, 10, 15)
CLOSES = ("", "-morning", "-evening")


def r(name):
    return os.path.join(RUN, name)


def s3(*args):
    subprocess.run([sys.executable, S3] + [str(a) for a in args], check=True, cwd=REPO)


def plant_boxes():
    regions = json.load(open(REGIONS))["regions"]
    boxes = {}
    for reg in regions:
        if reg["name"].startswith("plant."):
            xs = [p[0] for p in reg["polygon"]]
            ys = [p[1] for p in reg["polygon"]]
            boxes[reg["name"]] = [int(min(xs)), int(min(ys)), int(max(xs)), int(max(ys))]
    return boxes


def flips():
    s3("flip", r("dial-g1-a-nohalo.png"), r("dial-g1-b-nohalo.png"), r("g1-flip.gif"), "--scale", 0.6)
    s3("flip", r("pinch-a.png"), r("pinch-b.png"), r("pinch-flip.gif"), "--scale", 0.6)
    s3("flip", r("seated-a.png"), r("seated-b.png"), r("seated-flip.gif"), "--scale", 0.6)
    for tag in CLOSES:
        s3("flip", r("close%s-a.png" % tag), r("close%s-b.png" % tag), r("close%s-flip.gif" % tag), "--scale", 0.7)
    for tag in ("", "-morning", "-evening"):
        s3("flip", r("pinch%s-a.png" % tag), r("pinch%s-b.png" % tag), r("pinch%s-flip.gif" % tag), "--scale", 0.6)
    for tag in ("open", "bud"):
        s3("flip", r("bloom-%s-a.png" % tag), r("bloom-%s-b.png" % tag), r("bloom-%s-flip.gif" % tag), "--scale", 0.6)


def greys():
    s3("grey", r("dial-g1-a-nohalo.png"), r("dial-g1-b-nohalo.png"), r("g1-grey.sbs.png"))
    s3("grey", r("pinch-a.png"), r("pinch-b.png"), r("pinch-grey.sbs.png"))
    for tag in CLOSES:
        s3("grey", r("close%s-a.png" % tag), r("close%s-b.png" % tag), r("close%s-grey.sbs.png" % tag))
    # The three plants at DialG1, cropped, for a closer look at the same framing.
    boxes = plant_boxes()
    for who in ("a", "b"):
        im = Image.open(r("dial-g1-%s-nohalo.png" % who)).convert("RGB")
        x0 = min(b[0] for b in boxes.values())
        y0 = min(b[1] for b in boxes.values())
        x1 = max(b[2] for b in boxes.values())
        y1 = max(b[3] for b in boxes.values())
        im.crop((x0, y0, x1, y1)).save(r("g1-plants-crop-%s.png" % who))
    a = Image.open(r("g1-plants-crop-a.png")).convert("L")
    b = Image.open(r("g1-plants-crop-b.png")).convert("L")
    sheet = Image.new("L", (a.width * 2 + 8, a.height), 255)
    sheet.paste(a, (0, 0))
    sheet.paste(b, (a.width + 8, 0))
    sheet.save(r("g1-plants-grey.sbs.png"))
    print("greys done")


def orbits():
    names = [("seated", "orbit-seated")] + [("close%s" % t, "orbit-close%s" % t) for t in CLOSES]
    for _, base in names:
        for who in ("a", "b"):
            frames = [r("%s-%s.o%d.png" % (base, who, o)) for o in ORBITS]
            s3("orbit", r("%s-%s.gif" % (base, who)), "--scale", 0.7, *frames)
            # montage -15 / 0 / +15 degrees, the three views the judge sees
            ims = [Image.open(r("%s-%s.o%d.png" % (base, who, o))).convert("RGB") for o in (-15, 0, 15)]
            w, h = ims[0].size
            sheet = Image.new("RGB", (w * 3 + 12, h), (255, 255, 255))
            for i, im in enumerate(ims):
                sheet.paste(im, (i * (w + 6), 0))
            sheet.save(r("%s-%s.montage.png" % (base, who)))
    print("orbits done")


def stereo():
    for who in ("a", "b"):
        s3("stereo", r("stereo-seated-%s-L.png" % who), r("stereo-seated-%s-R.png" % who),
           r("stereo-seated-%s.sbs.png" % who), r("stereo-seated-%s.anaglyph.png" % who))


def crops():
    boxes = plant_boxes()
    for name, box in boxes.items():
        for who in ("a", "b"):
            Image.open(r("dial-g1-%s-nohalo.png" % who)).convert("RGB").crop(box).save(r("g1-%s-crop-%s.png" % (name.replace("plant.", ""), who)))


def contour():
    out = {}
    boxes = plant_boxes()
    pad = 14
    for name, box in boxes.items():
        key = name.replace("plant.", "")
        b = [box[0] - pad, box[1] - pad, box[2] + pad, box[3] + pad]
        for who, other in (("b", "a"), ("a", "b")):
            path = r("contour-%s-%s.json" % (key, who))
            s3("contour", r("dial-g1-%s-nohalo.png" % who), "--box", *b, "--other", r("dial-g1-%s-nohalo.png" % other), "--out", path)
            out["%s.%s" % (key, who)] = json.load(open(path))
    # all three together, over the plants' union
    x0 = min(v[0] for v in boxes.values()) - pad
    y0 = min(v[1] for v in boxes.values()) - pad
    x1 = max(v[2] for v in boxes.values()) + pad
    y1 = max(v[3] for v in boxes.values()) + pad
    for who, other in (("b", "a"), ("a", "b")):
        path = r("contour-all-%s.json" % who)
        s3("contour", r("dial-g1-%s-nohalo.png" % who), "--box", x0, y0, x1, y1, "--other", r("dial-g1-%s-nohalo.png" % other), "--out", path)
        out["all.%s" % who] = json.load(open(path))
    with open(r("contour-summary.json"), "w") as f:
        json.dump(out, f, indent=1)
    for k, v in out.items():
        print(k, v["median_width_px"], v["p25"], v["p75"], v["modal_0p5_bin_share"], v["axis_samples"])


STEPS = {"flips": flips, "greys": greys, "orbits": orbits, "stereo": stereo, "crops": crops, "contour": contour}


def main(argv):
    for name in (argv or list(STEPS)):
        print("==", name)
        STEPS[name]()


if __name__ == "__main__":
    main(sys.argv[1:])
