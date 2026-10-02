"""Stack same-pixel crops of a reference and a render. Top-left origin.

    python apps/terrarium/Art/Scripts/compare_crops.py ref.png render.png out.png

Each band is reference | render. Default regions are the G1 jar: glass rim, fronds, moss.
"""
import sys

from PIL import Image, ImageDraw

# x, y, w, h in the 1824x1024 G1 frame. Jar base is near (915, 830).
REGIONS = [
    ("glass rim", 740, 200, 360, 130),
    ("fronds", 750, 400, 400, 230),
    ("moss", 720, 640, 420, 180),
]


def crop(image, box):
    x, y, w, h = box
    return image.crop((x, y, x + w, y + h))


def main():
    if len(sys.argv) != 4:
        raise SystemExit("usage: compare_crops.py ref.png render.png out.png")
    ref = Image.open(sys.argv[1]).convert("RGB")
    ren = Image.open(sys.argv[2]).convert("RGB")
    if ref.size != ren.size:
        raise SystemExit("size mismatch %s vs %s" % (ref.size, ren.size))
    bands = []
    width = 0
    height = 0
    label_h = 18
    for name, x, y, w, h in REGIONS:
        if x + w > ref.width or y + h > ref.height:
            raise SystemExit("region outside frame: %s" % name)
        left = crop(ref, (x, y, w, h))
        right = crop(ren, (x, y, w, h))
        band = Image.new("RGB", (w * 2, h + label_h), (236, 232, 220))
        draw = ImageDraw.Draw(band)
        draw.text((6, 2), "%s  %d,%d %dx%d" % (name, x, y, w, h), fill=(28, 26, 22))
        band.paste(left, (0, label_h))
        band.paste(right, (w, label_h))
        bands.append(band)
        width = max(width, band.width)
        height += band.height
    sheet = Image.new("RGB", (width, height), (20, 20, 20))
    y = 0
    for band in bands:
        sheet.paste(band, (0, y))
        y += band.height
    sheet.save(sys.argv[3])
    print("wrote", sys.argv[3], sheet.size)


if __name__ == "__main__":
    main()
