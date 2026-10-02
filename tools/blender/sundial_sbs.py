"""Composite the Blender dial over the kitchen plate and build the side-by-side.

    python tools/blender/sundial_sbs.py

Reference on the left, preview on the right, both 1824x1024. The hand matte
lays the photographed hand back over the dial, which is how the G1 framing is
reviewed. The hand pixels are not a texture.
"""
import os

import cv2
import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
RUN = os.path.join(ROOT, "orchestration", "runs", "sundial", "T-SUN-013")
PLATE = os.path.join(ROOT, "shared", "assets", "room-plates", "plate-dial.png")
REF = os.path.join(ROOT, "shared", "assets", "art-reference", "A2-05-field-notebook-1.png")
HAND = os.path.join(ROOT, "shared", "assets", "room-plates", "dial-hand-matte.png")
RENDER = os.path.join(RUN, "dial-render.png")


def caption(img, title, sub):
    bar = 56
    canvas = Image.new("RGB", (img.width, img.height + bar), (28, 24, 20))
    canvas.paste(img, (0, bar))
    draw = ImageDraw.Draw(canvas)
    try:
        font = ImageFont.truetype("C:/Windows/Fonts/segoeui.ttf", 22)
        small = ImageFont.truetype("C:/Windows/Fonts/segoeui.ttf", 16)
    except OSError:
        font = small = ImageFont.load_default()
    draw.text((16, 6), title, fill=(244, 240, 232), font=font)
    draw.text((16, 30), sub, fill=(186, 176, 164), font=small)
    return canvas


def composite():
    plate = np.array(Image.open(PLATE).convert("RGB")).astype(np.float32)
    ref = np.array(Image.open(REF).convert("RGB")).astype(np.float32)
    hand = np.array(Image.open(HAND).convert("L")).astype(np.float32) / 255.0
    render = np.array(Image.open(RENDER).convert("RGBA")).astype(np.float32)
    if render.shape[1] != plate.shape[1] or render.shape[0] != plate.shape[0]:
        raise SystemExit("render %s does not match plate %s" % (render.shape, plate.shape))
    alpha = render[:, :, 3] / 255.0
    # contact shadow on the table, under the dial, shifted down the way the light falls
    shadow = cv2.GaussianBlur(alpha, (0, 0), 14)
    shadow = cv2.warpAffine(shadow, np.float32([[1, 0, 8], [0, 1, 16]]), (plate.shape[1], plate.shape[0]))
    # keep the shadow off the hand
    shadow *= (1.0 - hand)
    plate *= (1.0 - 0.38 * shadow[..., None])
    a = alpha[..., None]
    preview = plate * (1.0 - a) + render[:, :, :3] * a
    # photographed hand back on top, only where the matte is white
    h = hand[..., None]
    preview = preview * (1.0 - h) + ref * h
    preview = np.clip(preview, 0, 255).astype(np.uint8)
    out = os.path.join(RUN, "preview.png")
    Image.fromarray(preview).save(out)
    ys, xs = np.where(alpha > 0.2)
    if len(xs):
        print("render alpha bbox", int(xs.min()), int(ys.min()), int(xs.max()), int(ys.max()),
              "size", int(xs.max() - xs.min()), int(ys.max() - ys.min()))
    print("wrote", out, preview.shape)
    return preview


def main():
    preview = composite()
    ref = Image.open(REF).convert("RGB")
    left = caption(ref, "REFERENCE", "A2-05-field-notebook-1.png, pinch halo")
    right = caption(Image.fromarray(preview), "BLENDER PREVIEW", "Eevee toon, 37 deg, 30 deg FOV, over plate-dial")
    gap = 12
    out = Image.new("RGB", (left.width + gap + right.width, left.height), (18, 14, 12))
    out.paste(left, (0, 0))
    out.paste(right, (left.width + gap, 0))
    path = os.path.join(RUN, "sbs-preview.png")
    out.save(path)
    print("wrote", path, out.size)


if __name__ == "__main__":
    main()
