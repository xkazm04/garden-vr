"""Spike S2 (T-TER-044): crop sheets. Usage: s2_sheet.py <dir> <out.png> [box x0,y0,x1,y1] [names...]
Lays the reference (when the frame is JarG1) and the named renders side by side with labels."""
import os
import sys
from PIL import Image, ImageDraw

REPO = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "..", ".."))
REF = os.path.join(REPO, "shared", "assets", "art-reference", "A1-03-night-moss-1.png")


def main(argv):
    folder, out = argv[0], argv[1]
    box = tuple(int(v) for v in argv[2].split(",")) if len(argv) > 2 else (690, 250, 1134, 900)
    names = argv[3:] or ["a", "s1", "b0", "b1", "b2"]
    tiles = []
    ref = Image.open(REF).convert("RGB")
    tiles.append(("ref", ref.crop(box)))
    for n in names:
        path = os.path.join(folder, n + "-jar-g1.png")
        tiles.append((n, Image.open(path).convert("RGB").crop(box)))
    w, h = tiles[0][1].size
    sheet = Image.new("RGB", (w * len(tiles), h + 18), (20, 20, 20))
    d = ImageDraw.Draw(sheet)
    for i, (label, im) in enumerate(tiles):
        sheet.paste(im, (i * w, 18))
        d.text((i * w + 4, 3), label, fill=(255, 255, 255))
    sheet.save(out)
    print(out, sheet.size)


if __name__ == "__main__":
    main(sys.argv[1:])
