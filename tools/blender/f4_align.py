"""Alignment gate for a JarG1 repaint, and a PNG check of the checker round trip.

    python tools/blender/f4_align.py iou --flat flat.png --repaint repaint.png --mask mask.png
    python tools/blender/f4_align.py diff --render render.png --checker checker.png --mask mask.png --out diff.png

Canny edge-map IoU inside the mask. The dossier starts the gate at 0.6.
The diff is the mean absolute byte difference on 0..1, the same quantity
project_bake.py prints. This script is the one that also writes a diff image.
"""
from __future__ import annotations

import json
import sys

import cv2
import numpy as np
from PIL import Image


def load_rgb(path):
    return np.asarray(Image.open(path).convert("RGB"), dtype=np.float32) / 255.0


def load_mask(path):
    image = np.asarray(Image.open(path).convert("L"), dtype=np.float32) / 255.0
    return image > 0.5


def canny_iou(flat, repaint, mask):
    gray_a = cv2.cvtColor((flat * 255.0).astype(np.uint8), cv2.COLOR_RGB2GRAY)
    gray_b = cv2.cvtColor((repaint * 255.0).astype(np.uint8), cv2.COLOR_RGB2GRAY)
    edges_a = cv2.Canny(gray_a, 80, 160) > 0
    edges_b = cv2.Canny(gray_b, 80, 160) > 0
    inside = mask
    if flat.shape[:2] != mask.shape:
        raise SystemExit("flat and mask sizes differ")
    if repaint.shape[:2] != mask.shape:
        raise SystemExit("repaint and mask sizes differ")
    both = np.logical_and(edges_a, edges_b) & inside
    union = np.logical_or(edges_a, edges_b) & inside
    union_count = int(union.sum())
    if union_count == 0:
        return 0.0, 0, 0
    return float(both.sum()) / float(union_count), int(both.sum()), union_count


def mean_abs(render, checker, mask):
    if render.shape != checker.shape:
        raise SystemExit("render and checker sizes differ: %s vs %s" % (render.shape, checker.shape))
    diff = np.abs(render - checker)
    values = diff[mask]
    if values.size == 0:
        return None, 0
    return float(values.mean()), int(mask.sum())


def cmd_iou(argv):
    flat = load_rgb(argv[argv.index("--flat") + 1])
    repaint = load_rgb(argv[argv.index("--repaint") + 1])
    mask = load_mask(argv[argv.index("--mask") + 1])
    if repaint.shape[:2] != flat.shape[:2]:
        repaint_img = Image.open(argv[argv.index("--repaint") + 1]).convert("RGB").resize(
            (flat.shape[1], flat.shape[0]), Image.Resampling.BILINEAR
        )
        repaint = np.asarray(repaint_img, dtype=np.float32) / 255.0
        resized = True
    else:
        resized = False
    score, inter, union = canny_iou(flat, repaint, mask)
    payload = {
        "iou": score,
        "intersection": inter,
        "union": union,
        "gate": 0.6,
        "pass": score >= 0.6,
        "resized_to_flat": resized,
        "mask_pixels": int(mask.sum()),
    }
    print(json.dumps(payload), flush=True)
    if "--json" in argv:
        with open(argv[argv.index("--json") + 1], "w", encoding="utf-8") as handle:
            json.dump(payload, handle, indent=2)
    if not payload["pass"]:
        sys.exit(2)
    return payload


def cmd_diff(argv):
    render = load_rgb(argv[argv.index("--render") + 1])
    checker = load_rgb(argv[argv.index("--checker") + 1])
    mask = load_mask(argv[argv.index("--mask") + 1])
    score, pixels = mean_abs(render, checker, mask)
    diff = np.abs(render - checker)
    show = np.clip(diff * 4.0, 0.0, 1.0)
    show[~mask] = 0.0
    out = argv[argv.index("--out") + 1]
    Image.fromarray((show * 255.0).astype(np.uint8), "RGB").save(out)
    payload = {"mean_abs": score, "mask_pixels": pixels, "gate": 0.05, "pass": score is not None and score < 0.05, "diff": out}
    print(json.dumps(payload), flush=True)
    if not payload["pass"]:
        sys.exit(1)


def main():
    if len(sys.argv) < 2:
        raise SystemExit("need iou or diff")
    if sys.argv[1] == "iou":
        cmd_iou(sys.argv)
    elif sys.argv[1] == "diff":
        cmd_diff(sys.argv)
    else:
        raise SystemExit("unknown command")


if __name__ == "__main__":
    main()
