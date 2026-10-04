# T-SUN-052 numbers: dial silhouette IoU against the reference, dial major axis, soil-area fraction of the face.
#   python layout_t052_metrics.py mask <black.png> <out_mask.png>       # our dial mask from a DialG1Black frame
#   python layout_t052_metrics.py iou <black.png>                       # IoU, axes, centre offset against the reference fit
#   python layout_t052_metrics.py overlay <black.png> <out.png>         # reference | ours mask overlays
import json
import os
import sys

import cv2
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, "..", "..", "..", ".."))
REF = os.path.join(REPO, "shared", "assets", "art-reference", "A2-05-field-notebook-1.png")
# Hand-checked fit of the reference dial's top face edge (cv2.fitEllipse on the cream ring's outer contour, outliers rejected;
# see ref_dial_fit.json). Centre, full axes (w along the fit angle), angle in degrees.
FIT = os.path.join(HERE, "ref_dial_fit.json")
W, H = 1824, 1024


def ref_fit():
    return json.load(open(FIT))


def ref_mask():
    """Top face ellipse plus the side band (the ellipse shifted down by the measured side thickness), as a filled hull."""
    f = ref_fit()
    (cx, cy), (w, h), ang = f["ellipse"]
    thick = f["side_px"]
    m = np.zeros((H, W), np.uint8)
    a = np.zeros((H, W), np.uint8)
    b = np.zeros((H, W), np.uint8)
    cv2.ellipse(a, ((cx, cy), (w, h), ang), 255, -1)
    cv2.ellipse(b, ((cx, cy + thick), (w, h), ang), 255, -1)
    pts = cv2.findNonZero(a | b)
    hull = cv2.convexHull(pts)
    cv2.fillConvexPoly(m, hull, 255)
    return m > 0


def top_mask():
    f = ref_fit()
    (cx, cy), (w, h), ang = f["ellipse"]
    m = np.zeros((H, W), np.uint8)
    cv2.ellipse(m, ((cx, cy), (w, h), ang), 255, -1)
    return m > 0


def our_mask(black):
    im = cv2.imread(black)
    luma = im.astype(np.float32).mean(axis=2)
    m = (luma > 6).astype(np.uint8)
    m = cv2.morphologyEx(m, cv2.MORPH_CLOSE, np.ones((5, 5), np.uint8))
    n, lab, st, _ = cv2.connectedComponentsWithStats(m)
    i = 1 + int(np.argmax(st[1:, 4]))
    m = (lab == i).astype(np.uint8)
    # fill holes
    ff = m.copy()
    cv2.floodFill(ff, np.zeros((H + 2, W + 2), np.uint8), (0, 0), 2)
    return (m > 0) | (ff == 0)


def axes(mask):
    cnts, _ = cv2.findContours(mask.astype(np.uint8), cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_NONE)
    c = max(cnts, key=cv2.contourArea)
    (cx, cy), (w, h), ang = cv2.fitEllipse(c)
    return (cx, cy), max(w, h), min(w, h)


def iou(a, b):
    return float((a & b).sum()) / float((a | b).sum())


REF_SOIL = os.path.join(HERE, "ref_soil_mask.png")


def lum_mask(path, thr=6):
    im = cv2.imread(path)
    return im.astype(np.float32).mean(axis=2) > thr


def soil_numbers(soil_png, dial_png):
    soil = lum_mask(soil_png)
    soil = cv2.morphologyEx(soil.astype(np.uint8), cv2.MORPH_CLOSE, np.ones((5, 5), np.uint8)) > 0
    dial = our_mask(dial_png)
    ref_soil = cv2.imread(REF_SOIL, 0) > 127
    ref_dial = ref_mask()
    return {
        "soil_area_px": int(soil.sum()),
        "soil_fraction_of_dial": round(float(soil.sum()) / float(dial.sum()), 4),
        "soil_fraction_of_dial_ref": round(float(ref_soil.sum()) / float(ref_dial.sum()), 4),
        "soil_iou_vs_ref_bed": round(iou(soil, ref_soil), 4),
        "soil_bbox_ours": [int(v) for v in (np.nonzero(soil)[1].min(), np.nonzero(soil)[0].min(), np.nonzero(soil)[1].max(), np.nonzero(soil)[0].max())],
        "soil_bbox_ref": [int(v) for v in (np.nonzero(ref_soil)[1].min(), np.nonzero(ref_soil)[0].min(), np.nonzero(ref_soil)[1].max(), np.nonzero(ref_soil)[0].max())],
    }


def diff_mask(with_plant, without, thr=18):
    """Pixels the plant added. The contact shadows only darken what is under them (same hue), so they are not counted."""
    a = cv2.imread(with_plant).astype(np.float32)
    b = cv2.imread(without).astype(np.float32)
    changed = np.abs(a - b).max(axis=2) > thr
    dot = (a * b).sum(axis=2)
    cos = dot / (np.linalg.norm(a, axis=2) * np.linalg.norm(b, axis=2) + 1e-6)
    shadow = (cos > 0.995) & (a.sum(axis=2) < b.sum(axis=2))
    m = (changed & ~shadow).astype(np.uint8)
    m = cv2.morphologyEx(m, cv2.MORPH_OPEN, np.ones((3, 3), np.uint8))
    return m > 0


def halo_numbers(halo_png, plant_dir, tag, pinched, neighbours, empty_png):
    """Ring pixels on black (isolate=halo) against the pinched plant's mask and the neighbours'."""
    ring = lum_mask(halo_png, 40)
    plants = {}
    for arc in ("morning", "midday", "winddown"):
        plants[arc] = diff_mask(os.path.join(plant_dir, "plant-%s-%s.png" % (tag, arc)), empty_png)
    own = plants[pinched]
    dist = cv2.distanceTransform((~own).astype(np.uint8), cv2.DIST_L2, 5)
    d = dist[ring]
    other = np.zeros_like(own)
    for n in neighbours:
        other |= plants[n]
    over = float((ring & other & ~own).sum()) / max(float(ring.sum()), 1.0)
    return {
        "ring_px": int(ring.sum()),
        "ring_to_plant_px": {"median": round(float(np.median(d)), 1), "p10": round(float(np.percentile(d, 10)), 1), "p90": round(float(np.percentile(d, 90)), 1), "max": round(float(d.max()), 1)},
        "ring_components": int(cv2.connectedComponents(cv2.dilate(ring.astype(np.uint8), np.ones((5, 5), np.uint8)))[0] - 1),
        "spill_on_neighbours": round(over, 4),
        "pinched_plant_px": int(own.sum()),
        "ring_width_x_height": [int(np.ptp(np.nonzero(ring)[1])), int(np.ptp(np.nonzero(ring)[0]))],
        "plant_width_x_height": [int(np.ptp(np.nonzero(own)[1])), int(np.ptp(np.nonzero(own)[0]))],
    }


def main(argv):
    cmd = argv[0]
    if cmd == "mask":
        m = our_mask(argv[1])
        cv2.imwrite(argv[2], m.astype(np.uint8) * 255)
    elif cmd == "iou":
        ours = our_mask(argv[1])
        ref = ref_mask()
        (ocx, ocy), omaj, omin = axes(ours)
        (rcx, rcy), rmaj, rmin = axes(ref)
        out = {
            "iou": round(iou(ours, ref), 4),
            "iou_top_face_vs_ours": round(iou(ours, top_mask()), 4),
            "ours": {"centre": [round(ocx, 1), round(ocy, 1)], "major_px": round(omaj, 1), "minor_px": round(omin, 1), "area_px": int(ours.sum())},
            "ref": {"centre": [round(rcx, 1), round(rcy, 1)], "major_px": round(rmaj, 1), "minor_px": round(rmin, 1), "area_px": int(ref.sum())},
            "major_ratio_ours_over_ref": round(omaj / rmaj, 4),
        }
        print(json.dumps(out, indent=1))
    elif cmd == "soil":
        print(json.dumps(soil_numbers(argv[1], argv[2]), indent=1))
    elif cmd == "halo":
        # halo <halo.png> <plant_dir> <tag a|b> <empty black frame>
        print(json.dumps(halo_numbers(argv[1], argv[2], argv[3], "midday", ["morning", "winddown"], argv[4]), indent=1))
    elif cmd == "overlay":
        ours = our_mask(argv[1])
        ref = ref_mask()
        im = cv2.imread(REF)
        left = im.copy()
        right = cv2.imread(argv[1])
        vis = np.zeros((H, W, 3), np.uint8)
        vis[ref & ours] = (200, 200, 200)
        vis[ref & ~ours] = (60, 60, 255)     # reference only: red
        vis[~ref & ours] = (255, 120, 40)    # ours only: blue
        left[ref] = (left[ref] * 0.5 + np.array([0, 0, 255]) * 0.5).astype(np.uint8)
        cv2.imwrite(argv[2], np.hstack([left, vis]))


if __name__ == "__main__":
    main(sys.argv[1:])
