# T-SUN-052 evidence images from the captured frames (no Unity).
#   python apps/sundial/Art/Scripts/layout_t052_evidence.py
# triptych.png        reference | A | layout (the pinch frames, hand matte on)
# triptych-g1.png     the same without the hand
# mask-overlay-{a,b}.png  the reference dial mask (green outline) and ours (red outline) on the reference and on our frame
# soil-overlay-{a,b}.png  the same for the soil bed
# pinch-flip.gif / g1-flip.gif   A and layout, 0.8 s each
import json
import os
import sys

import cv2
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import layout_t052_metrics as M  # noqa: E402

REPO = os.path.normpath(os.path.join(HERE, "..", "..", "..", ".."))
RUN = os.path.join(REPO, "orchestration", "runs", "sundial", "T-SUN-052")
REF = M.REF


def p(name):
    return os.path.join(RUN, name)


def label(img, text):
    img = img.copy()
    cv2.rectangle(img, (0, 0), (img.shape[1], 34), (238, 232, 220), -1)
    cv2.putText(img, text, (12, 24), cv2.FONT_HERSHEY_SIMPLEX, 0.7, (40, 36, 32), 2, cv2.LINE_AA)
    return img


def outline(img, mask, color, width=3):
    cnts, _ = cv2.findContours(mask.astype(np.uint8), cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_NONE)
    cv2.drawContours(img, cnts, -1, color, width, cv2.LINE_AA)


def triptych(a, b, out):
    ref = cv2.imread(REF)
    ia = cv2.imread(p(a))
    ib = cv2.imread(p(b))
    row = np.hstack([label(ref, "reference A2-05"), label(ia, "A (look A)"), label(ib, "layout (layout+soilmound+leafplant)")])
    cv2.imwrite(p(out), cv2.resize(row, None, fx=0.6, fy=0.6, interpolation=cv2.INTER_AREA))


def mask_overlay(tag):
    ref = cv2.imread(REF)
    ours = M.our_mask(p("black-%s.png" % tag))
    refm = M.ref_mask()
    left = ref.copy()
    outline(left, refm, (60, 200, 60))
    outline(left, ours, (40, 40, 230))
    frame = cv2.imread(p("g1-%s.png" % tag))
    right = frame.copy()
    outline(right, refm, (60, 200, 60))
    outline(right, ours, (40, 40, 230))
    iou = M.iou(ours, refm)
    row = np.hstack([label(left, "reference: green = ref dial mask, red = ours (%s)" % tag.upper()), label(right, "ours with both outlines, IoU %.3f" % iou)])
    cv2.imwrite(p("mask-overlay-%s.png" % tag), cv2.resize(row, None, fx=0.6, fy=0.6, interpolation=cv2.INTER_AREA))


def soil_overlay(tag):
    ref = cv2.imread(REF)
    soil = M.lum_mask(p("soil-%s.png" % tag))
    soil = cv2.morphologyEx(soil.astype(np.uint8), cv2.MORPH_CLOSE, np.ones((5, 5), np.uint8)) > 0
    refsoil = cv2.imread(M.REF_SOIL, 0) > 127
    left = ref.copy()
    left[refsoil] = (left[refsoil] * 0.6 + np.array([0, 160, 0]) * 0.4).astype(np.uint8)
    outline(left, soil, (40, 40, 230))
    frame = cv2.imread(p("g1-%s.png" % tag))
    right = frame.copy()
    outline(right, refsoil, (60, 200, 60))
    outline(right, soil, (40, 40, 230))
    row = np.hstack([label(left, "reference bed (green) and ours (red outline)"), label(right, "our frame, IoU %.3f" % M.iou(soil, refsoil))])
    cv2.imwrite(p("soil-overlay-%s.png" % tag), cv2.resize(row, None, fx=0.6, fy=0.6, interpolation=cv2.INTER_AREA))


def flip(a, b, out):
    frames = []
    for name in (a, b):
        im = Image.open(p(name)).convert("RGB").resize((912, 512), Image.LANCZOS)
        frames.append(im)
    frames[0].save(p(out), save_all=True, append_images=frames[1:], duration=800, loop=0)


def main():
    triptych("pinch-a.png", "pinch-b.png", "triptych.png")
    triptych("g1-a.png", "g1-b.png", "triptych-g1.png")
    for tag in "ab":
        mask_overlay(tag)
        soil_overlay(tag)
    flip("pinch-a.png", "pinch-b.png", "pinch-flip.gif")
    flip("g1-a.png", "g1-b.png", "g1-flip.gif")
    print("ok")


if __name__ == "__main__":
    main()
