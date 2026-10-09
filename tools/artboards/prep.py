"""Cut the project's own art into transparent sprites for the artboard frames.

Usage: python3 tools/artboards/prep.py <build-dir>
Reads only files tracked in this repository (each has its provenance sidecar beside it). Writes PNGs into <build-dir>.
"""
import os
import sys

from PIL import Image, ImageChops, ImageFilter

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))


def src(*parts):
    return os.path.join(ROOT, *parts)


def on_white(path, inset=0.08):
    """Ink and watercolour drawn on light paper: alpha from darkness and colour."""
    im = Image.open(path).convert("RGB")
    w, h = im.size
    im = im.crop((int(w * inset), int(h * inset), int(w * (1 - inset)), int(h * (1 - inset))))
    hsv = im.convert("HSV")
    sat = hsv.getchannel("S")
    lum = im.convert("L")
    dark = lum.point(lambda v: max(0, min(255, int((238 - v) * 255 / 70))))
    colour = sat.point(lambda v: max(0, min(255, int((v - 18) * 255 / 50))))
    alpha = ImageChops.lighter(dark, colour).filter(ImageFilter.GaussianBlur(0.6))
    out = im.convert("RGBA")
    out.putalpha(alpha)
    return out.crop(out.getbbox())


def on_black(path, low=28, span=60, median=0):
    """Art on a black ground: alpha from the brightest channel."""
    im = Image.open(path).convert("RGB")
    r, g, b = im.split()
    alpha = ImageChops.lighter(ImageChops.lighter(r, g), b)
    alpha = alpha.point(lambda v: max(0, min(255, int((v - low) * 255 / span))))
    if median:
        alpha = alpha.filter(ImageFilter.MedianFilter(median))
    out = im.convert("RGBA")
    out.putalpha(alpha)
    return out.crop(out.getbbox())


def main(build):
    os.makedirs(build, exist_ok=True)
    plants = ["plant_sunrise", "plant_sunrise_bloom", "plant_midday", "plant_midday_bloom", "plant_dusk", "plant_dusk_bloom"]
    for name in plants:
        on_white(src("apps", "sundial", "Art", "Source", name + ".png")).save(os.path.join(build, name + ".png"))
    for name in ["packet-morning", "packet-midday", "packet-winddown"]:
        on_black(src("apps", "sundial", "Art", "Source", "packets", name + ".png"), low=40, span=50, median=5).save(
            os.path.join(build, name + ".png"))
    on_black(src("apps", "terrarium", "Art", "Source", "fern_a.png"), low=20, span=70).save(os.path.join(build, "fern.png"))
    Image.open(src("apps", "sundial", "Art", "Source", "paper.png")).convert("RGB").save(os.path.join(build, "paper.png"))
    print("sprites in", build)


if __name__ == "__main__":
    if len(sys.argv) != 2:
        sys.exit("usage: prep.py <build-dir>")
    main(sys.argv[1])
