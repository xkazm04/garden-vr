# T-SUN-050 evidence images from the captured frames (no Unity).
#   python apps/sundial/Art/Scripts/sprint_t050_evidence.py
# triptych-pinch.png, triptych-g1.png     reference | A | sprint
# g1-flip.gif, pinch-flip.gif, seated-flip.gif, seatedp-flip.gif     A then sprint, 0.8 s each
# g1-grey.sbs.png, pinch-grey.sbs.png     reference | A | sprint in greyscale; seated-grey.sbs.png A | sprint
# orbit-*-{a,s}.gif and .montage.png      -15 / 0 / +15 degrees
# interact-roomlight.png, interact-halo-morning.png, interact-bed-edge.png     the seams, cropped 1:1
import os

import cv2
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, "..", "..", "..", ".."))
RUN = os.path.join(REPO, "orchestration", "runs", "sundial", "T-SUN-050")
REF = os.path.join(REPO, "shared", "assets", "art-reference", "A2-05-field-notebook-1.png")
ORBITS = (-15, -10, -5, 0, 5, 10, 15)


def p(name):
    return os.path.join(RUN, name)


def label(img, text):
    img = img.copy()
    cv2.rectangle(img, (0, 0), (img.shape[1], 34), (238, 232, 220), -1)
    cv2.putText(img, text, (12, 24), cv2.FONT_HERSHEY_SIMPLEX, 0.7, (40, 36, 32), 2, cv2.LINE_AA)
    return img


def triptych(a, s, out, with_ref=True):
    cols = []
    if with_ref:
        cols.append(label(cv2.imread(REF), "reference A2-05"))
    cols.append(label(cv2.imread(p(a)), "A (look A)"))
    cols.append(label(cv2.imread(p(s)), "sprint"))
    row = np.hstack(cols)
    cv2.imwrite(p(out), cv2.resize(row, None, fx=0.6, fy=0.6, interpolation=cv2.INTER_AREA))


def flip(a, s, out, size=(912, 512)):
    frames = [Image.open(p(n)).convert("RGB").resize(size, Image.LANCZOS) for n in (a, s)]
    frames[0].save(p(out), save_all=True, append_images=frames[1:], duration=800, loop=0, optimize=True)


def grey(names, out, with_ref=True):
    ims = []
    if with_ref:
        ims.append(Image.open(REF).convert("L"))
    for n in names:
        ims.append(Image.open(p(n)).convert("L"))
    w, h = ims[0].size
    sheet = Image.new("L", (w * len(ims) + 8 * (len(ims) - 1), h), 255)
    for i, im in enumerate(ims):
        sheet.paste(im.resize((w, h)), (i * (w + 8), 0))
    sheet.save(p(out))


def orbit(base, tag):
    frames = [Image.open(p("%s-%s.o%d.png" % (base, tag, o))).convert("RGB") for o in ORBITS]
    small = [f.resize((int(f.width * 0.7), int(f.height * 0.7)), Image.LANCZOS) for f in frames]
    seq = small + small[-2:0:-1]
    seq[0].save(p("%s-%s.gif" % (base, tag)), save_all=True, append_images=seq[1:], duration=140, loop=0, optimize=True)
    ims = [Image.open(p("%s-%s.o%d.png" % (base, tag, o))).convert("RGB") for o in (-15, 0, 15)]
    w, h = ims[0].size
    sheet = Image.new("RGB", (w * 3 + 12, h), (255, 255, 255))
    for i, im in enumerate(ims):
        sheet.paste(im, (i * (w + 6), 0))
    sheet.save(p("%s-%s.montage.png" % (base, tag)))


def crop_row(names, labels, box, out, scale=1.0):
    x0, y0, x1, y1 = box
    cols = []
    for n, text in zip(names, labels):
        im = cv2.imread(p(n)) if not os.path.isabs(n) else cv2.imread(n)
        cols.append(label(im[y0:y1, x0:x1], text))
    row = np.hstack(cols)
    if scale != 1.0:
        row = cv2.resize(row, None, fx=scale, fy=scale, interpolation=cv2.INTER_AREA)
    cv2.imwrite(p(out), row)


def room_diff(box, out):
    """Sprint, sprint without the room light, and the change between them (scaled so a 4 percent change is visible)."""
    x0, y0, x1, y1 = box
    s = cv2.imread(p("nohalo-s.png")).astype(np.float32)[y0:y1, x0:x1]
    n = cv2.imread(p("nohalo-noroom.png")).astype(np.float32)[y0:y1, x0:x1]
    d = (s.mean(axis=2) - n.mean(axis=2))
    vis = np.full(s.shape, 128, np.float32)
    vis[..., :] = (128 + d * 4.0)[..., None]
    row = np.hstack([label(n.astype(np.uint8), "sprint without room light"), label(s.astype(np.uint8), "sprint"),
                     label(np.clip(vis, 0, 255).astype(np.uint8), "luma change x4, grey = none")])
    cv2.imwrite(p(out), row)


def main():
    triptych("pinch-a.png", "pinch-s.png", "triptych-pinch.png")
    triptych("nohalo-a.png", "nohalo-s.png", "triptych-g1.png")
    triptych("seated-a.png", "seated-s.png", "triptych-seated.png", with_ref=False)
    flip("nohalo-a.png", "nohalo-s.png", "g1-flip.gif")
    flip("pinch-a.png", "pinch-s.png", "pinch-flip.gif")
    flip("seated-a.png", "seated-s.png", "seated-flip.gif")
    flip("seatedp-a.png", "seatedp-s.png", "seatedp-flip.gif")
    grey(["nohalo-a.png", "nohalo-s.png"], "g1-grey.sbs.png")
    grey(["pinch-a.png", "pinch-s.png"], "pinch-grey.sbs.png")
    grey(["seated-a.png", "seated-s.png"], "seated-grey.sbs.png", with_ref=False)
    for base in ("orbit-close", "orbit-close-morning", "orbit-close-evening", "orbit-seated"):
        for tag in ("a", "s"):
            orbit(base, tag)
    orbit("orbit-close-morning", "s3")
    # The seams, 1:1.
    room_diff((420, 300, 1200, 760), "interact-roomlight.png")
    crop_row(["pinch-morning-a.png", "pinch-morning-s-noh2.png", "pinch-morning-s.png"],
             ["A halo", "sprint without halo v2", "sprint (halo v2 on the card plant)"], (380, 250, 820, 640), "interact-halo-morning.png")
    crop_row(["pinch-a.png", "pinch-s.png"], ["A", "sprint"], (700, 270, 1150, 660), "interact-halo-midday.png")
    crop_row(["nohalo-a.png", "nohalo-s.png"], ["A", "sprint"], (840, 560, 1260, 800), "interact-bed-edge.png")
    print("ok")


if __name__ == "__main__":
    main()
