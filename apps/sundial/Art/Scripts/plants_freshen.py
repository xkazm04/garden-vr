# Recolour the keyed plant cards so every stage reads alive.
# Ink and alpha stay put, so the line weight, the pivot, and the boil
# (UV jitter on these same cards) do not move. A small or closed stage
# uses the same living colour as the full plant. It is never grey or brown.
#
#   python apps/sundial/Art/Scripts/plants_freshen.py
#   python apps/sundial/Art/Scripts/plants_key.py --report-only
from pathlib import Path

import cv2
import numpy as np
from PIL import Image, ImageDraw, ImageFont

HERE = Path(__file__).resolve().parent
APP = HERE.parents[1]
TEX = APP / "Assets" / "Art" / "Textures"
RUN = APP.parents[1] / "orchestration" / "runs" / "sundial" / "T-SUN-021"

# Living fills. Each pixel keeps its luma, so the drawn shade step stays.
LEAF = {
    "sunrise": np.array([118.0, 168.0, 52.0], np.float32),
    "midday": np.array([78.0, 150.0, 62.0], np.float32),
    "dusk": np.array([96.0, 142.0, 86.0], np.float32),
}
FLOWER = {
    "sunrise": np.array([232.0, 186.0, 48.0], np.float32),
    "midday": np.array([220.0, 78.0, 112.0], np.float32),
    "dusk": np.array([158.0, 96.0, 198.0], np.float32),
}
SEED = {
    "sunrise": np.array([176.0, 156.0, 48.0], np.float32),
    "midday": np.array([210.0, 86.0, 72.0], np.float32),
    "dusk": np.array([142.0, 90.0, 176.0], np.float32),
}
STEM = np.array([206.0, 86.0, 74.0], np.float32)
WASH = {
    "sunrise": np.array([232.0, 184.0, 96.0], np.float32),
    "midday": np.array([236.0, 156.0, 132.0], np.float32),
    "dusk": np.array([186.0, 156.0, 206.0], np.float32),
}
CREAM = (243, 238, 226, 255)
SOIL = (132, 96, 68, 255)


def luma_of(rgb):
    return 0.2126 * rgb[..., 0] + 0.7152 * rgb[..., 1] + 0.0722 * rgb[..., 2]


def paint(rgb, mask, target):
    """Move masked pixels onto target's hue, keeping each pixel's luma."""
    if not np.any(mask):
        return
    src = rgb[mask]
    src_l = luma_of(src)
    tgt = np.asarray(target, np.float32)
    tgt_l = float(luma_of(tgt))
    if tgt_l < 1.0:
        return
    painted = np.clip(tgt * (src_l / tgt_l)[..., None], 0.0, 255.0)
    got = luma_of(painted)
    fix = np.ones(src_l.shape, np.float32)
    ok = got > 1.0
    fix[ok] = src_l[ok] / got[ok]
    rgb[mask] = np.clip(painted * fix[..., None], 0.0, 255.0)


def classify(rgba):
    rgb = rgba[:, :, :3].astype(np.float32)
    alpha = rgba[:, :, 3]
    r, g, b = rgb[:, :, 0], rgb[:, :, 1], rgb[:, :, 2]
    luma = luma_of(rgb)
    chroma = np.maximum(np.maximum(r, g), b) - np.minimum(np.minimum(r, g), b)
    opaque = alpha > 24
    ink = opaque & ((luma < 78) | ((luma < 102) & (chroma < 22)))
    ys = np.where(opaque)[0]
    soil = np.zeros(alpha.shape, dtype=bool)
    if ys.size:
        yb = int(ys.max())
        band = np.zeros(alpha.shape[0], dtype=bool)
        band[max(0, yb - 52):] = True
        hsv = cv2.cvtColor(rgba[:, :, :3], cv2.COLOR_RGB2HSV)
        hue = hsv[:, :, 0].astype(np.float32) * 2.0
        brown = (hue > 8.0) & (hue < 55.0) & (r + 6.0 >= g) & (g + 12.0 >= b)
        soil = opaque & band[:, None] & brown & (~ink)
    dist = cv2.distanceTransform((~ink).astype(np.uint8), cv2.DIST_L2, 3)
    wash = opaque & (~ink) & (~soil) & (luma > 178.0) & (dist > 14.0)
    body = opaque & (~ink) & (~soil) & (~wash)
    return body, wash, ink, soil


def blossoms_on_card(rgba):
    """Round ochre buds and petal clusters on a keyed sunrise bloom card."""
    rgb = rgba[:, :, :3]
    alpha = rgba[:, :, 3]
    r = rgb[:, :, 0].astype(np.float32)
    g = rgb[:, :, 1].astype(np.float32)
    luma = luma_of(rgb.astype(np.float32))
    hsv = cv2.cvtColor(rgb, cv2.COLOR_RGB2HSV)
    hue = hsv[:, :, 0].astype(np.float32) * 2.0
    sat = hsv[:, :, 1].astype(np.float32)
    ink = (alpha > 24) & (luma < 100)
    seed = (alpha > 30) & (~ink) & (r > g + 6) & (hue < 55) & (sat > 40) & (luma > 90) & (luma < 210)
    n, labels, stats, _ = cv2.connectedComponentsWithStats(seed.astype(np.uint8), 8)
    keep = np.zeros(seed.shape, np.uint8)
    for i in range(1, n):
        area = int(stats[i, cv2.CC_STAT_AREA])
        w = int(stats[i, cv2.CC_STAT_WIDTH])
        h = int(stats[i, cv2.CC_STAT_HEIGHT])
        top = int(stats[i, cv2.CC_STAT_TOP])
        if area < 28 or area > 500 or top > 380 or w < 1 or h < 1:
            continue
        fill = area / float(w * h)
        aspect = max(w, h) / float(min(w, h))
        comp = (labels == i).astype(np.uint8)
        dist = cv2.distanceTransform(comp, cv2.DIST_L2, 3)
        thick = float(dist[comp > 0].max())
        if fill >= 0.42 and aspect < 2.4 and thick >= 2.2:
            keep[comp > 0] = 255
    if int(keep.max()) == 0:
        return np.zeros(seed.shape, dtype=bool)
    warm = (alpha > 24) & (~ink) & (hue < 54) & (r + 2 >= g) & (sat > 28) & (luma > 110) & (luma < 220)
    kernel = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (3, 3))
    grow = keep.copy()
    for _ in range(4):
        dil = cv2.dilate(grow, kernel)
        grow = np.where((dil > 0) & warm, np.uint8(255), grow)
    return grow > 0


def freshen(rgba, arc, name, blossoms=None):
    """Return a copy. Alpha is the original alpha."""
    out = rgba.copy()
    rgb = out[:, :, :3].astype(np.float32)
    body, wash, _ink, _soil = classify(rgba)
    hsv = cv2.cvtColor(rgba[:, :, :3], cv2.COLOR_RGB2HSV)
    hue = hsv[:, :, 0].astype(np.float32) * 2.0
    sat = hsv[:, :, 1].astype(np.float32)
    paint(rgb, wash, WASH[arc])
    if arc == "dusk":
        # The keyed dusk card is already violet in the spikes. Keep that hue and
        # lift the grey so a closed plant stays lavender, not a second green herb.
        violet = body & (hue > 185.0)
        green = body & (~violet)
        paint(rgb, green, LEAF[arc])
        paint(rgb, violet, FLOWER[arc])
        if name == "seed":
            paint(rgb, body, SEED[arc])
    elif name == "seed":
        paint(rgb, body, SEED[arc])
    elif name in ("bud", "open") and arc == "sunrise" and blossoms is not None:
        flowers = body & blossoms
        leaves = body & (~blossoms)
        paint(rgb, leaves, LEAF[arc])
        paint(rgb, flowers, FLOWER[arc])
    elif name in ("bud", "open"):
        green = body & (hue > 70.0) & (hue < 160.0)
        flowers = body & (~green)
        paint(rgb, green, LEAF[arc])
        paint(rgb, flowers, FLOWER[arc])
    elif arc == "midday":
        stem = body & (hue < 24.0) & (sat > 90.0)
        leaf = body & (~stem)
        paint(rgb, leaf, LEAF[arc])
        paint(rgb, stem, STEM)
    else:
        paint(rgb, body, LEAF[arc])
    out[:, :, :3] = np.clip(rgb, 0, 255).astype(np.uint8)
    out[:, :, 3] = rgba[:, :, 3]
    return out


def load_cards():
    placed = {}
    arcs = ("sunrise", "midday", "dusk")
    names = ("seed", "sprout", "young", "leafy", "full", "bud", "open")
    for arc in arcs:
        placed[arc] = {}
        for name in names:
            if name in ("bud", "open"):
                path = TEX / ("bloom_%s_%s.png" % (arc, name))
            else:
                path = TEX / ("plant_%s_%s.png" % (arc, name))
            placed[arc][name] = np.asarray(Image.open(path).convert("RGBA"))
    return placed


def save_cards(placed):
    for arc, cards in placed.items():
        for name, rgba in cards.items():
            if name in ("bud", "open"):
                path = TEX / ("bloom_%s_%s.png" % (arc, name))
            else:
                path = TEX / ("plant_%s_%s.png" % (arc, name))
            Image.fromarray(rgba, "RGBA").save(path)
            note = path.parent / (path.name + ".provenance.txt")
            note.write_text(
                "asset: %s\n"
                "method: recoloured by apps/sundial/Art/Scripts/plants_freshen.py\n"
                "note: Same ink and alpha as the keyed card. Fills moved to a living hue. "
                "Not copied from the art reference.\n"
                "derived_from_art_reference: no\n" % path.name,
                encoding="utf-8",
            )


def contact(placed, path, ground):
    cell_w, cell_h = 128, 256
    cols = ["seed", "sprout", "young", "leafy", "full", "bud", "open"]
    arcs = ["sunrise", "midday", "dusk"]
    sheet = Image.new("RGBA", (len(cols) * cell_w, len(arcs) * cell_h + 28), ground)
    draw = ImageDraw.Draw(sheet)
    try:
        font = ImageFont.truetype("C:/Windows/Fonts/segoeui.ttf", 16)
    except OSError:
        font = ImageFont.load_default()
    for c, name in enumerate(cols):
        draw.text((c * cell_w + 8, 4), name, fill=(42, 38, 34, 255), font=font)
    for r, arc in enumerate(arcs):
        for c, name in enumerate(cols):
            card = Image.fromarray(placed[arc][name], "RGBA")
            card.thumbnail((cell_w - 8, cell_h - 8), Image.Resampling.LANCZOS)
            x = c * cell_w + (cell_w - card.width) // 2
            y = 28 + r * cell_h + (cell_h - card.height) // 2
            sheet.paste(card, (x, y), card)
    path.parent.mkdir(parents=True, exist_ok=True)
    sheet.convert("RGB").save(path)


def freshened_from_disk():
    raw = load_cards()
    blossoms = {}
    out = {}
    for arc, cards in raw.items():
        out[arc] = {}
        for name, rgba in cards.items():
            mask = None
            if arc == "sunrise" and name in ("bud", "open"):
                mask = blossoms_on_card(rgba)
                blossoms[name] = mask
            out[arc][name] = freshen(rgba, arc, name, mask)
    return raw, out, blossoms


def main():
    before, after, blossoms = freshened_from_disk()
    for arc in before:
        for name in before[arc]:
            if not np.array_equal(before[arc][name][:, :, 3], after[arc][name][:, :, 3]):
                raise SystemExit("freshen changed alpha on %s %s" % (arc, name))
    run = RUN
    run.mkdir(parents=True, exist_ok=True)
    save_cards(after)
    contact(before, run / "plants-before.png", CREAM)
    contact(after, run / "plants-contact.png", CREAM)
    contact(after, run / "plants-on-soil.png", SOIL)
    old = Image.open(run / "plants-before.png").convert("RGB")
    new = Image.open(run / "plants-contact.png").convert("RGB")
    pair = Image.new("RGB", (old.width + new.width + 8, old.height), (255, 255, 255))
    pair.paste(old, (0, 0))
    pair.paste(new, (old.width + 8, 0))
    pair.save(run / "plants-before-after.png")
    print("blossoms bud", int(blossoms["bud"].sum()), "open", int(blossoms["open"].sum()))
    print("wrote", run / "plants-contact.png")


if __name__ == "__main__":
    main()
