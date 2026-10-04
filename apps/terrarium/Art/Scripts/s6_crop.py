"""Reference | candidate crops of the jar at 1:1. python s6_crop.py out.png cand.png [cand2.png ...] [--box x0,y0,x1,y1]"""
import sys
from PIL import Image
REF = "shared/assets/art-reference/A1-03-night-moss-1.png"
args = sys.argv[1:]
box = (620, 150, 1210, 900)
if "--box" in args:
    i = args.index("--box")
    box = tuple(int(v) for v in args[i + 1].split(","))
    args = args[:i] + args[i + 2:]
out, cands = args[0], args[1:]
ims = [Image.open(REF).convert("RGB").crop(box)] + [Image.open(c).convert("RGB").crop(box) for c in cands]
w = sum(i.width for i in ims) + 8 * (len(ims) - 1)
sheet = Image.new("RGB", (w, ims[0].height), (20, 20, 20))
x = 0
for im in ims:
    sheet.paste(im, (x, 0))
    x += im.width + 8
sheet.save(out)
