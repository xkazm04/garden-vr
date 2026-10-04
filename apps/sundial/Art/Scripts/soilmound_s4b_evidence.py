# T-SUN-051 evidence: edge crops (ref / 046 / 051), orbit montages, stereo anaglyphs, flips, edge numbers, outline diff image.
#   python apps/sundial/Art/Scripts/soilmound_s4b_evidence.py
import json
import os

import numpy as np
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, "..", "..", "..", ".."))
RUN = os.path.join(REPO, "orchestration", "runs", "sundial", "T-SUN-051")
R46 = os.path.join(REPO, "orchestration", "runs", "sundial", "T-SUN-046")
REF = os.path.join(REPO, "shared", "assets", "art-reference", "A2-05-field-notebook-1.png")
SRC = os.path.join(REPO, "apps", "sundial", "Art", "Source", "soil")


def im(p):
    return Image.open(p).convert("RGB")


def label(img, text):
    d = ImageDraw.Draw(img)
    d.rectangle([0, 0, 8 + 7 * len(text), 16], fill=(255, 255, 255))
    d.text((4, 2), text, fill=(0, 0, 0))
    return img


def luma(a):
    return a @ np.array([0.2126, 0.7152, 0.0722], np.float32)


def edge_crops():
    frames = (("ref-1", REF), ("T-SUN-046", os.path.join(R46, "dial-g1-b-nohalo.png")), ("T-SUN-051", os.path.join(RUN, "dial-g1-b2-nohalo.png")))
    # The reference's dial is smaller and framed differently, so it gets its own boxes on the same feature (the soil's far edge, the near edge).
    boxes = {"far-left": (470, 395, 710, 545), "far-right": (1000, 395, 1240, 545), "near-left": (400, 590, 640, 740)}
    ref_boxes = {"far-left": (520, 500, 760, 650), "far-right": (940, 520, 1180, 670), "near-left": (330, 560, 570, 710)}
    for name, box in boxes.items():
        w, h = box[2] - box[0], box[3] - box[1]
        sheet = Image.new("RGB", (w * 3 * 2 + 8, h * 2), (255, 255, 255))
        for i, (tag, p) in enumerate(frames):
            c = im(p).crop(ref_boxes[name] if tag == "ref-1" else box).resize((w * 2, h * 2), Image.LANCZOS)
            sheet.paste(label(c, tag), (i * (w * 2 + 4), 0))
        sheet.save(os.path.join(RUN, "edge-crop-%s.png" % name))
    # One sheet with all three crops stacked.
    parts = [im(os.path.join(RUN, "edge-crop-%s.png" % n)) for n in boxes]
    sheet = Image.new("RGB", (parts[0].width, sum(p.height for p in parts) + 8), (255, 255, 255))
    y = 0
    for p in parts:
        sheet.paste(p, (0, y))
        y += p.height + 4
    sheet.save(os.path.join(RUN, "edge-crops.png"))


def triptych():
    box = (380, 300, 1240, 800)
    frames = (("ref-1", REF), ("T-SUN-046", os.path.join(R46, "dial-g1-b-nohalo.png")), ("T-SUN-051", os.path.join(RUN, "dial-g1-b2-nohalo.png")))
    sheet = Image.new("RGB", (860 * 3 + 8, 500), (255, 255, 255))
    for i, (tag, p) in enumerate(frames):
        sheet.paste(label(im(p).crop(box), tag), (i * 864, 0))
    sheet.save(os.path.join(RUN, "triptych-g1.png"))
    a = im(os.path.join(R46, "dial-g1-b-nohalo.png"))
    b = im(os.path.join(RUN, "dial-g1-b2-nohalo.png"))
    a.save(os.path.join(RUN, "g1-flip.gif"), save_all=True, append_images=[b], duration=900, loop=0)


def montage(prefix, out, box=None, scale=0.5):
    names = ["%s.o-15.png" % prefix, "%s.o-7.5.png" % prefix, "%s.o0.png" % prefix, "%s.o7.5.png" % prefix, "%s.o15.png" % prefix]
    degs = ["-15", "-7.5", "0", "7.5", "15"]
    frames = [im(os.path.join(RUN, n)) for n in names]
    if box:
        frames = [f.crop(box) for f in frames]
    w, h = frames[0].size
    w, h = int(w * scale), int(h * scale)
    sheet = Image.new("RGB", (w * 5 + 16, h), (255, 255, 255))
    for i, f in enumerate(frames):
        sheet.paste(label(f.resize((w, h), Image.LANCZOS), "orbit %s deg" % degs[i]), (i * (w + 4), 0))
    sheet.save(os.path.join(RUN, out + ".montage.png"))
    gif = [f.resize((w, h), Image.LANCZOS) for f in frames + frames[-2:0:-1]]
    gif[0].save(os.path.join(RUN, out + ".gif"), save_all=True, append_images=gif[1:], duration=220, loop=0)


def anaglyph(left, right, out, box=None):
    l, r = im(os.path.join(RUN, left)), im(os.path.join(RUN, right))
    if box:
        l, r = l.crop(box), r.crop(box)
    la, ra = np.asarray(l).astype(np.float32), np.asarray(r).astype(np.float32)
    # Red-cyan on luminance (keeps the colours calm): left eye in red, right eye in green and blue.
    out_a = np.dstack([luma(la), luma(ra), luma(ra)]).clip(0, 255).astype(np.uint8)
    Image.fromarray(out_a).save(os.path.join(RUN, out))
    # Horizontal disparity of the bed against the dial, measured by phase correlation on the bed crop (pixels).
    return disparity(la, ra)


def disparity(la, ra):
    a, b = luma(la), luma(ra)
    a, b = a - a.mean(), b - b.mean()
    f = np.fft.fft2(a) * np.conj(np.fft.fft2(b))
    f /= np.maximum(np.abs(f), 1e-9)
    cc = np.fft.ifft2(f).real
    y, x = np.unravel_index(np.argmax(cc), cc.shape)
    if y > cc.shape[0] // 2:
        y -= cc.shape[0]
    if x > cc.shape[1] // 2:
        x -= cc.shape[1]
    return {"shiftXpx": int(x), "shiftYpx": int(y)}


def band_width(img, x0, x1, y0, y1):
    """Mean 10 to 90 percent width (px) of the vertical luma step from wash (above) to earth (below) over columns x0..x1."""
    g = luma(np.asarray(img).astype(np.float32))[y0:y1, x0:x1]
    widths = []
    for c in range(g.shape[1]):
        col = np.convolve(g[:, c], np.ones(3) / 3, mode="same")
        top, bot = col[:6].mean(), col[-6:].mean()
        if abs(bot - top) < 25:
            continue
        t = (col - top) / (bot - top)
        lo = np.argmax(t > 0.1)
        hi = np.argmax(t > 0.9)
        if hi > lo:
            widths.append(hi - lo)
    return (round(float(np.mean(widths)), 2), len(widths)) if widths else (None, 0)


def jitter(img, x0, x1, y0, y1):
    """Std (px) of the 50 percent crossing row from column to column after removing a straight-line fit: how torn the edge is."""
    g = luma(np.asarray(img).astype(np.float32))[y0:y1, x0:x1]
    ys = []
    for c in range(g.shape[1]):
        col = np.convolve(g[:, c], np.ones(3) / 3, mode="same")
        top, bot = col[:6].mean(), col[-6:].mean()
        if abs(bot - top) < 25:
            ys.append(np.nan)
            continue
        t = (col - top) / (bot - top)
        ys.append(float(np.argmax(t > 0.5)))
    ys = np.array(ys)
    xs = np.arange(len(ys))
    ok = ~np.isnan(ys)
    if ok.sum() < 20:
        return None
    p = np.polyfit(xs[ok], ys[ok], 2)
    return round(float(np.std(ys[ok] - np.polyval(p, xs[ok]))), 2)


def edge_numbers():
    """Edge character from the painted alpha itself (deterministic, top-down): orphan crumbs outside the main bed at alpha 0.5, the
    10 to 90 percent ramp width along the radius (S4 alpha is a dither threshold, S4b alpha is the bleed parameter), and the
    share of edge-band pixels that are mid-alpha. T-SUN-046's asset is read from its commit (eff581a)."""
    import cv2
    import subprocess
    old = subprocess.run(["git", "show", "eff581a:apps/sundial/Assets/Resources/SoilMound/soil_mound.png"], cwd=REPO, capture_output=True).stdout
    open(os.path.join(RUN, "soil046.alpha-src.png"), "wb").write(old)
    new = os.path.join(REPO, "apps", "sundial", "Assets", "Resources", "SoilMound", "soil_mound.png")
    res = {}
    for tag, p, mpp in (("046", os.path.join(RUN, "soil046.alpha-src.png"), 0.17 / 1024), ("051", new, 0.25 / 1024)):
        a = np.asarray(Image.open(p).convert("RGBA"))[..., 3].astype(np.float32) / 255.0
        m = (a >= 0.5).astype(np.uint8)
        n, lab, st, _ = cv2.connectedComponentsWithStats(m)
        areas = st[1:, cv2.CC_STAT_AREA]
        main = int(areas.max())
        orphans = int((areas < main).sum())
        mid = (a > 0.1) & (a < 0.9)
        sdf = cv2.distanceTransform(m, cv2.DIST_L2, 5) - cv2.distanceTransform(1 - m, cv2.DIST_L2, 5)
        # Ramp width: spread of signed distance (mm) over the pixels with alpha in 0.1..0.9, 10th to 90th percentile.
        d = sdf[mid] * mpp * 1000.0
        res[tag] = {
            "orphanCrumbsAtAlpha0.5": orphans,
            "orphanAreaShareOfBedPct": round(100.0 * float(areas[areas < main].sum()) / float(areas.sum()), 3),
            "midAlphaPixelsPctOfBed": round(100.0 * float(mid.sum()) / float(m.sum()), 2),
            "midAlphaDistanceP10toP90Mm": [round(float(np.percentile(d, 10)), 2), round(float(np.percentile(d, 90)), 2)] if d.size else None,
            "texelMm": round(mpp * 1000.0, 4),
        }
    with open(os.path.join(RUN, "edge-numbers.json"), "w", newline="\n") as f:
        json.dump(res, f, indent=1)
    os.remove(os.path.join(RUN, "soil046.alpha-src.png"))
    print(json.dumps(res, indent=1))


def main():
    edge_crops()
    triptych()
    montage("orbit-g1-b2", "orbit-g1-b2", box=(380, 280, 1240, 820), scale=0.55)
    montage("orbit-seated-b2", "orbit-seated-b2", scale=0.4)
    d1 = anaglyph("stereo-seated-b2-L.png", "stereo-seated-b2-R.png", "stereo-seated-b2.anaglyph.png")
    d2 = anaglyph("stereo-g1-b2-L.png", "stereo-g1-b2-R.png", "stereo-g1-b2.anaglyph.png", box=(380, 280, 1240, 820))
    with open(os.path.join(RUN, "stereo-numbers.json"), "w", newline="\n") as f:
        json.dump({"baselineMm": 64, "seated": d1, "g1": d2}, f, indent=1)
    edge_numbers()
    # Outline diff: template silhouette vs the generated one, no re-fit.
    t = np.asarray(Image.open(os.path.join(SRC, "template-2.png")).convert("RGB").resize((1024, 1024), Image.LANCZOS)).astype(np.float32)
    g = np.asarray(Image.open(os.path.join(SRC, "gen-2.png")).convert("RGB").resize((1024, 1024), Image.LANCZOS)).astype(np.float32)
    paper = np.array([244, 239, 230], np.float32)
    tm = np.abs(t - paper).sum(2) > 60
    gm = np.abs(g - paper).sum(2) > 60
    over = np.asarray(Image.open(os.path.join(SRC, "gen-2.png")).convert("RGB").resize((1024, 1024), Image.LANCZOS)).copy()
    over[tm & ~gm] = (255, 0, 0)
    over[gm & ~tm] = (0, 80, 255)
    sheet = Image.new("RGB", (1024 * 3 + 8, 1024), (255, 255, 255))
    sheet.paste(Image.fromarray(t.astype(np.uint8)), (0, 0))
    sheet.paste(Image.fromarray(g.astype(np.uint8)), (1028, 0))
    sheet.paste(Image.fromarray(over), (2056, 0))
    sheet.resize((1536, 512)).save(os.path.join(RUN, "outline-diff.png"))


if __name__ == "__main__":
    main()
