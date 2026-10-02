"""Side-by-side at the G1 framing: owner's reference left, Blender preview right.

    python tools/blender/compose_hero_sbs.py
"""
import os
from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
REF = os.path.join(ROOT, "shared", "assets", "art-reference", "A1-03-night-moss-1.png")
PREVIEW = os.path.join(ROOT, "orchestration", "runs", "terrarium", "T-TER-013", "preview.png")
OUT = os.path.join(ROOT, "orchestration", "runs", "terrarium", "T-TER-013", "sbs-preview.png")


def caption(img, title, sub):
    bar = 56
    canvas = Image.new("RGB", (img.width, img.height + bar), (14, 18, 20))
    canvas.paste(img, (0, bar))
    draw = ImageDraw.Draw(canvas)
    try:
        font = ImageFont.truetype("C:/Windows/Fonts/segoeui.ttf", 22)
        small = ImageFont.truetype("C:/Windows/Fonts/segoeui.ttf", 16)
    except OSError:
        font = small = ImageFont.load_default()
    draw.text((16, 6), title, fill=(236, 240, 236), font=font)
    draw.text((16, 30), sub, fill=(168, 176, 172), font=small)
    return canvas


def main():
    ref = Image.open(REF).convert("RGB")
    preview = Image.open(PREVIEW).convert("RGB")
    if preview.size != ref.size:
        preview = preview.resize(ref.size, Image.LANCZOS)
    left = caption(ref, "REFERENCE", "A1-03-night-moss-1.png, G1 mid-breath")
    right = caption(preview, "BLENDER PREVIEW", "Eevee, same framing, emission only")
    gap = 12
    out = Image.new("RGB", (left.width + gap + right.width, left.height), (8, 8, 10))
    out.paste(left, (0, 0))
    out.paste(right, (left.width + gap, 0))
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    out.save(OUT)
    print("wrote", OUT, out.size)


if __name__ == "__main__":
    main()
