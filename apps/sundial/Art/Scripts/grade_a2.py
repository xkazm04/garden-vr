# Gate A2 grades on the notebook dial. Idempotent.
# The warm daylight multiplies a pale wash by about (1.056, 0.883, 0.643) in linear
# space, so a lilac texture arrives on screen as dusty rose. This pushes the dusk
# wash the other way until the DialG1 pale region lands on ref-1 #BFA1BC.
# It also clears the painted rim. Dial_Face draws that ring. The dusk contour is the card scale in
# DialView (the filled leaves measure as a two-texel shell, so a bigger card
# is what puts that shell inside 1.5-2.5 px).
#
#   python apps/sundial/Art/Scripts/grade_a2.py
import math
import os

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
TEX = os.path.normpath(os.path.join(HERE, "..", "..", "Assets", "Art", "Textures"))
FACE_R = 0.1472
# Median linear gain, texture to DialG1 screen, fitted on the three pale washes.
GAIN = np.array([1.056, 0.883, 0.643], np.float64)
DUSK_TARGET = np.array([191.0, 161.0, 188.0], np.float64)  # #BFA1BC
# The painted ring is cleared. FToon draws the dial ink at a fixed screen width
# (_InkRingR on Dial_Face). A texture stroke cannot land in 2.5-3.5 px once the
# face is imported at 1024 and the near rim magnifies it.
RIM_MEASURED_PX = 0.5
RIM_KEEP_PX = 0.0



def luma(rgb):
    return 0.2126 * rgb[..., 0] + 0.7152 * rgb[..., 1] + 0.0722 * rgb[..., 2]


def srgb_to_lin(rgb):
    x = np.clip(np.asarray(rgb, dtype=np.float64) / 255.0, 0, 1)
    return np.where(x <= 0.04045, x / 12.92, ((x + 0.055) / 1.055) ** 2.4)


def lin_to_srgb(lin):
    x = np.clip(np.asarray(lin, dtype=np.float64), 0, None)
    s = np.where(x <= 0.0031308, x * 12.92, 1.055 * np.power(np.maximum(x, 0), 1.0 / 2.4) - 0.055)
    return np.clip(s, 0, 1) * 255.0


def want_tex():
    return lin_to_srgb(srgb_to_lin(DUSK_TARGET) / GAIN)


def world_of(rgb):
    s = rgb.shape[0]
    yy, xx = np.mgrid[0:s, 0:s]
    u = xx / (s - 1.0)
    v_top = yy / (s - 1.0)
    v_unity = 1.0 - v_top
    # Saved dial_face.png is flipped so +world X is the left of the file.
    world_x = (0.5 - u) * 2.0 * FACE_R
    world_z = (v_unity - 0.5) * 2.0 * FACE_R
    return world_x, world_z


def dusk_mask(rgb):
    world_x, world_z = world_of(rgb)
    rad = np.hypot(world_x, world_z)
    ang = np.degrees(np.arctan2(world_z, world_x))
    a0, a1 = 16.0, -100.0
    span = (a0 - a1) % 360.0
    frac = ((a0 - ang) % 360.0) / span
    inside = (frac >= 0.06) & (frac <= 0.94)
    band = (rad >= 0.055) & (rad <= 0.120)
    ink = luma(rgb) < 96.0
    return inside & band & ~ink


def grade_dusk_wash(rgb):
    """Move the dusk pale wash onto the texture colour that renders as #BFA1BC.

    The factor comes from the lighter half of the annulus the A2 sampler uses
    (about 0.078-0.088 m). The rest of the dusk wash takes the same hue shift,
    so the wet edge stays darker.
    """
    img = np.asarray(rgb, dtype=np.float32)
    mask = dusk_mask(img)
    if int(mask.sum()) < 100:
        raise SystemExit("dusk wash mask is empty")
    world_x, world_z = world_of(img)
    rad = np.hypot(world_x, world_z)
    annulus = mask & (rad >= 0.078) & (rad <= 0.088)
    if int(annulus.sum()) < 50:
        annulus = mask
    cols = img[annulus]
    pale = cols[luma(cols) >= np.median(luma(cols))]
    current = pale.mean(axis=0)
    want = want_tex()
    factor = srgb_to_lin(want) / np.maximum(srgb_to_lin(current), 1e-6)
    # Already graded: the pale mean sits on the target texture colour.
    if float(np.max(np.abs(np.log(np.clip(factor, 1e-3, None))))) < 0.02:
        print("dusk wash already at", np.round(current, 1))
        return img
    lin = srgb_to_lin(img)
    graded = lin_to_srgb(lin * factor)
    out = img.copy()
    out[mask] = graded[mask]
    check = out[annulus]
    check = check[luma(check) >= np.median(luma(check))].mean(axis=0)
    print("dusk wash", np.round(current, 1), "->", np.round(check, 1),
          "want", np.round(want, 1), "factor", np.round(factor, 3))
    return out


def rim_half_widths(img):
    s = img.shape[0]
    lum_img = luma(img)
    widths = []
    rs = np.linspace(860.0, 910.0, 51)
    for deg in range(0, 360, 2):
        a = math.radians(deg)
        xs = s / 2.0 + rs * math.cos(a)
        ys = s / 2.0 - rs * math.sin(a)
        xi = np.clip(np.rint(xs).astype(int), 0, s - 1)
        yi = np.clip(np.rint(ys).astype(int), 0, s - 1)
        lum = lum_img[yi, xi]
        k = int(np.argmin(lum))
        ink = float(lum[k])
        side = float(np.percentile(lum, 80))
        if ink > 80 or side - ink < 40:
            continue
        thresh = ink + 0.5 * (side - ink)
        lo = hi = k
        while lo > 0 and lum[lo - 1] <= thresh:
            lo -= 1
        while hi < len(lum) - 1 and lum[hi + 1] <= thresh:
            hi += 1
        widths.append(float(rs[hi] - rs[lo]))
    return widths


def thin_rim_ink(rgb, measured_px=RIM_MEASURED_PX, keep_px=RIM_KEEP_PX):
    """Shrink the painted ink circle. Shoulders are replaced with the paper
    just outside the stroke. Inpainting was pulling the dark core back out.
    Pencil ticks are lighter than the ink and are left alone.

    keep_px is the window around the darkest sample. measured_px is the
    half-height width that window is meant to leave. A second run sees the
    measured width and does nothing.
    """
    img = np.asarray(rgb, dtype=np.float32).copy()
    before = rim_half_widths(img)
    med = float(np.median(before)) if before else 0.0
    # At or under the target width already. Do not eat a stroke that is thin enough.
    if med <= measured_px + 0.45:
        print("rim ink median %.2f px, kept" % med)
        return img
    s = img.shape[0]
    keep = np.zeros((s, s), np.uint8)
    drop = np.zeros((s, s), np.uint8)
    paper = np.zeros((s, s, 3), np.float32)
    votes = np.zeros((s, s), np.float32)
    ink_col = np.zeros((s, s, 3), np.float32)
    ink_votes = np.zeros((s, s), np.float32)
    half = keep_px * 0.5
    rs = np.linspace(850.0, 920.0, 141)
    lum_img = luma(img)
    for i in range(1800):
        a = 2.0 * math.pi * i / 1800.0
        xs = s / 2.0 + rs * math.cos(a)
        ys = s / 2.0 - rs * math.sin(a)
        xi = np.clip(np.rint(xs).astype(int), 0, s - 1)
        yi = np.clip(np.rint(ys).astype(int), 0, s - 1)
        cols = img[yi, xi]
        lum = lum_img[yi, xi]
        k = int(np.argmin(lum))
        ink_l = float(lum[k])
        side = float(np.percentile(lum, 80))
        if ink_l > 80 or side - ink_l < 40:
            continue
        thresh = ink_l + 0.5 * (side - ink_l)
        lo = hi = k
        while lo > 0 and lum[lo - 1] <= thresh:
            lo -= 1
        while hi < len(lum) - 1 and lum[hi + 1] <= thresh:
            hi += 1
        outside = []
        for j in range(len(lum)):
            dist = abs(float(rs[j] - rs[k]))
            if 10.0 <= dist <= 16.0 and lum[j] > 210:
                outside.append(cols[j])
        if len(outside) < 2:
            continue
        tone = np.mean(np.stack(outside, axis=0), axis=0)
        for j in range(len(lum)):
            dist = abs(float(rs[j] - rs[k]))
            y, x = int(yi[j]), int(xi[j])
            # The core stays. Gray shoulders out to 8 px become the nearby paper.
            # Pencil ticks sit further out and are lighter than this cut.
            if keep_px > 0.0 and dist <= half:
                keep[y, x] = 1
                ink_col[y, x] += cols[k]
                ink_votes[y, x] += 1.0
            elif dist <= 10.0 and lum[j] < 190:
                drop[y, x] = 1
                paper[y, x] += tone
                votes[y, x] += 1.0
    paint = (drop > 0) & (keep == 0) & (votes > 0)
    core = (keep > 0) & (ink_votes > 0)
    if int(paint.sum()) < 10 and int(core.sum()) < 10:
        print("rim ink found nothing to thin, median %.2f" % med)
        return img
    if int(paint.sum()) > 0:
        img[paint] = paper[paint] / votes[paint, None]
    # One ink colour across the core, so the downsample stays a hard dark line.
    img[core] = ink_col[core] / ink_votes[core, None]
    after = rim_half_widths(img)
    print("rim ink median %.2f -> %.2f px (%d px repainted)" % (
        med, float(np.median(after)) if after else 0.0, int(paint.sum())))
    return img


def clear_rim_specks(rgb):
    """The shader owns the ink circle. A few dark texels survived the median
    thin, and on the near rim those texels are several screen pixels wide."""
    img = np.asarray(rgb, dtype=np.float32).copy()
    world_x, world_z = world_of(img)
    rad = np.hypot(world_x, world_z)
    # The old stroke wobbles, so a single-radius median reads as paper while
    # tens of thousands of ink texels (luma near 40, low chroma) remain.
    # The dusk wash in this annulus is near luma 177 and is left alone.
    band = (rad >= 0.1248) & (rad <= 0.1292)
    lum = luma(img)
    chroma = img.max(axis=2) - img.min(axis=2)
    dark = band & (lum < 90.0) & (chroma < 40.0)
    n = int(dark.sum())
    if n < 8:
        print("rim specks already clear", n)
        return img
    print("rim specks", n, "mean", np.round(img[dark].mean(axis=0), 1))
    r_in = 0.1215
    cos_a = np.divide(world_x, np.maximum(rad, 1e-6))
    sin_a = np.divide(world_z, np.maximum(rad, 1e-6))
    s = img.shape[0]
    u = 0.5 - (cos_a * r_in) / (2.0 * FACE_R)
    v_top = 1.0 - ((sin_a * r_in) / (2.0 * FACE_R) + 0.5)
    xi = np.clip(np.rint(u * (s - 1)).astype(int), 0, s - 1)
    yi = np.clip(np.rint(v_top * (s - 1)).astype(int), 0, s - 1)
    img[dark] = img[yi[dark], xi[dark]]
    print("rim specks cleared", n)
    return img


def grade_atlas_dusk(rgb):
    """The dusk column of the painted atlas, same gain as the face. Ink borders stay."""
    img = np.asarray(rgb, dtype=np.float32).copy()
    h, w = img.shape[:2]
    if w < h * 3:
        print("atlas is not four columns", img.shape)
        return img
    col_w = w // 4
    x0, x1 = col_w * 2, col_w * 3
    column = img[:, x0:x1]
    mask = luma(column) >= 96.0
    if int(mask.sum()) < 50:
        return img
    current = column[mask].mean(axis=0)
    want = want_tex()
    factor = srgb_to_lin(want) / np.maximum(srgb_to_lin(current), 1e-6)
    if float(np.max(np.abs(np.log(np.clip(factor, 1e-3, None))))) < 0.02:
        print("atlas dusk already at", np.round(current, 1))
        return img
    graded = lin_to_srgb(srgb_to_lin(column) * factor)
    column = column.copy()
    column[mask] = graded[mask]
    img[:, x0:x1] = column
    print("atlas dusk", np.round(current, 1), "->", np.round(img[:, x0:x1][mask].mean(axis=0), 1))
    return img


def save_rgb(path, rgb):
    Image.fromarray(np.clip(rgb, 0, 255).astype(np.uint8), "RGB").save(path)


def main():
    face_path = os.path.join(TEX, "dial_face.png")
    face = np.asarray(Image.open(face_path).convert("RGB")).astype(np.float32)
    face = grade_dusk_wash(face)
    face = thin_rim_ink(face)
    face = clear_rim_specks(face)
    save_rgb(face_path, face)
    print("face", face_path)

    atlas_path = os.path.join(TEX, "tiles_atlas.png")
    atlas = np.asarray(Image.open(atlas_path).convert("RGB")).astype(np.float32)
    atlas = grade_atlas_dusk(atlas)
    save_rgb(atlas_path, atlas)
    print("atlas", atlas_path)


if __name__ == "__main__":
    main()
