# Spike S1. Watercolour control map, tiled paper height, and the dusk repair.
# Look A keeps dial_face.png (the geometric stamp stays there). Variant B uses
# dial_face_s1.png, where the clipped outer band is feathered back onto the
# painted arc, plus _ControlTex and _PaperH in Fidelity/Toon.
#
#   python apps/sundial/Art/Scripts/watercolour_s1.py
#   python apps/sundial/Art/Scripts/watercolour_s1.py --score capture.png --out metrics.json
#   python apps/sundial/Art/Scripts/watercolour_s1.py --flip a.png b.png out.gif
#   python apps/sundial/Art/Scripts/watercolour_s1.py --grey a.png b.png out.png
import json
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)

import grade_a2

TEX = grade_a2.TEX
FACE_R = grade_a2.FACE_R
REPO = os.path.normpath(os.path.join(HERE, "..", "..", "..", ".."))
REF1 = os.path.join(REPO, "shared", "assets", "art-reference", "A2-05-field-notebook-1.png")
FIDELITY = os.path.join(REPO, "tools", "fidelity")

# DialView arc spans. 0 is +X (file left), 90 is +Z (file top).
ARCS = (
    ("morning", 242.0, 112.0, 85),
    ("midday", 106.0, 22.0, 170),
    ("dusk", 16.0, -100.0, 255),
)
# R = 1 in the control texture is this many metres inside the wash.
SDF_METRES = 0.018
CONTROL_N = 1024
PAPER_N = 512
# The other two washes end near 0.105 m. The geometric grade ran to 0.120 and clipped.
PAINTED_R0 = 0.056
PAINTED_R1 = 0.108
IOU_MIN = 0.9


def polar(rgb):
    world_x, world_z = grade_a2.world_of(rgb)
    rad = np.hypot(world_x, world_z)
    ang = np.degrees(np.arctan2(world_z, world_x))
    return world_x, world_z, rad, ang


def hue_of(rgb):
    r = rgb[..., 0]
    g = rgb[..., 1]
    b = rgb[..., 2]
    return np.degrees(np.arctan2(np.sqrt(3.0) * (g - b), 2.0 * r - g - b)) % 360.0


def in_arc(ang, a0, a1, inset):
    span = (a0 - a1) % 360.0
    frac = ((a0 - ang) % 360.0) / span
    return (frac >= inset) & (frac <= 1.0 - inset)


def clipped_slab(rgb, rad):
    """The geometric multiply drove the outer dusk band into a flat 255 blue."""
    r = rgb[..., 0]
    g = rgb[..., 1]
    b = rgb[..., 2]
    return (b > 248.0) & (r > 185.0) & (g > 185.0) & (rad > 0.09) & (rad < 0.14)


def sector_stamp_present(rgb):
    img = np.asarray(rgb, dtype=np.float32)
    _wx, _wz, rad, _ang = polar(img)
    return int(clipped_slab(img, rad).sum()) > 500


def legacy_sector_mask(rgb):
    """The T-SUN-028 annular sector. It does not follow the painted arc."""
    img = np.asarray(rgb, dtype=np.float32)
    _wx, _wz, rad, ang = polar(img)
    inside = in_arc(ang, 16.0, -100.0, 0.06)
    band = (rad >= 0.055) & (rad <= 0.120)
    ink = grade_a2.luma(img) < 96.0
    return inside & band & ~ink


def grade_dusk_mask(rgb):
    """Fresh-face grade mask. Same dusk arc, outer radius of the painted washes.

    Paper, ink, and the clipped slab are not multiplied. A hard sector out to
    0.120 m is what painted the blue band across the ring.
    """
    img = np.asarray(rgb, dtype=np.float32)
    _wx, _wz, rad, ang = polar(img)
    lum = grade_a2.luma(img)
    chroma = img.max(axis=2) - img.min(axis=2)
    hue = hue_of(img)
    arc = in_arc(ang, 16.0, -100.0, 0.04)
    band = (rad >= PAINTED_R0) & (rad <= PAINTED_R1)
    paper = (chroma < 18.0) & (lum > 220.0)
    ink = lum < 96.0
    morning = (hue > 26.0) & (hue < 70.0) & (chroma > 40.0)
    return arc & band & ~paper & ~ink & ~clipped_slab(img, rad) & ~morning


def painted_arc_masks(rgb):
    """Hue of the paint that is actually on the face, inside each arc."""
    img = np.asarray(rgb, dtype=np.float32)
    _wx, _wz, rad, ang = polar(img)
    lum = grade_a2.luma(img)
    chroma = img.max(axis=2) - img.min(axis=2)
    hue = hue_of(img)
    ink = lum < 100.0
    base = (
        (rad >= PAINTED_R0)
        & (rad <= PAINTED_R1)
        & (chroma > 28.0)
        & (lum > 140.0)
        & (lum < 248.0)
        & ~ink
        & ~clipped_slab(img, rad)
    )
    morning = base & in_arc(ang, 242.0, 112.0, 0.04) & (hue > 26.0) & (hue < 70.0)
    midday = base & in_arc(ang, 106.0, 22.0, 0.04) & (hue >= 5.0) & (hue <= 26.0)
    dusk = base & in_arc(ang, 16.0, -100.0, 0.04) & (hue > 230.0) & (hue < 300.0)
    midday = midday & ~morning
    dusk = dusk & ~morning & ~midday
    morning = morning & ~midday & ~dusk
    return {"morning": morning, "midday": midday, "dusk": dusk}


def iou(a, b):
    inter = np.logical_and(a, b).sum()
    union = np.logical_or(a, b).sum()
    if int(union) == 0:
        return 0.0
    return float(inter) / float(union)


def sample_radius(img, radius):
    """Colour at the same angle, at a fixed radius. Wraps the disc."""
    world_x, world_z, rad, _ang = polar(img)
    scale = radius / np.maximum(rad, 1e-6)
    wx = world_x * scale
    wz = world_z * scale
    s = img.shape[0]
    u = 0.5 - wx / (2.0 * FACE_R)
    v_top = 1.0 - (wz / (2.0 * FACE_R) + 0.5)
    xi = np.clip(np.rint(u * (s - 1)).astype(np.int32), 0, s - 1)
    yi = np.clip(np.rint(v_top * (s - 1)).astype(np.int32), 0, s - 1)
    return img[yi, xi]


def repair_clipped_slab(rgb):
    """Feather the clipped blue band back to the painted outer edge.

    The inner lilac is left alone. Pixels the sector multiply clipped are
    rebuilt from the wash just inside that edge and the paper just outside it.
    The edge radius wobbles so it is not a second hard circle.
    """
    img = np.asarray(rgb, dtype=np.float32).copy()
    _wx, _wz, rad, ang = polar(img)
    slab = clipped_slab(img, rad)
    if int(slab.sum()) < 10:
        print("dusk slab already clear", int(slab.sum()))
        return img
    wobble = (
        np.sin(np.radians(ang) * 7.0) * 0.55
        + np.sin(np.radians(ang) * 17.0 + 1.3) * 0.35
        + np.sin(np.radians(ang) * 41.0 + 0.4) * 0.20
    )
    edge = 0.1035 + 0.0038 * wobble
    # 0.100 is still lilac (B stays under 248). 0.122 is the paper gap
    # before the ink ring. 0.128 still sits in the clipped band on some rays.
    inner = sample_radius(img, 0.100)
    paper = sample_radius(img, 0.122)
    t = np.clip((rad - (edge - 0.0025)) / 0.0075, 0.0, 1.0)
    t = t * t * (3.0 - 2.0 * t)
    feather = inner * (1.0 - t[..., None]) + paper * t[..., None]
    img[slab] = feather[slab]
    left_mask = clipped_slab(img, rad)
    if int(left_mask.sum()) > 0:
        # A ray whose paper sample was still clipped. Pull from outside the band.
        img[left_mask] = sample_radius(img, 0.136)[left_mask]
    left = int(clipped_slab(img, rad).sum())
    print("dusk slab repainted", int(slab.sum()), "clipped left", left)
    return img


def down_bool(mask, n):
    src = Image.fromarray(np.where(mask, np.uint8(255), np.uint8(0)), "L")
    out = src.resize((n, n), Image.Resampling.NEAREST)
    return np.asarray(out) > 127


def blur_wrap(field, radius):
    """Wrap-aware Gaussian. Pillow rejects mode F, so this is scipy (sigma = radius)."""
    from scipy.ndimage import gaussian_filter

    return gaussian_filter(np.asarray(field, dtype=np.float32), sigma=float(radius), mode="wrap")


def paper_height(n=PAPER_N, seed=41):
    """Tileable paper tooth. Spectral noise wraps, so the shader can repeat it."""
    rng = np.random.RandomState(seed)
    acc = np.zeros((n, n), np.float32)
    bands = (
        (0.006, 0.035, 1.05, 0.70),
        (0.035, 0.10, 1.15, 0.45),
        (0.10, 0.22, 1.25, 0.28),
        (0.22, 0.42, 1.35, 0.16),
    )
    fy = np.fft.fftfreq(n)[:, None]
    fx = np.fft.rfftfreq(n)[None, :]
    freq = np.sqrt(fx * fx + fy * fy).astype(np.float32)
    for lo, hi, power, weight in bands:
        shape = (n, n // 2 + 1)
        spec = (rng.randn(*shape) + 1j * rng.randn(*shape)).astype(np.complex64)
        amp = np.where((freq >= lo) & (freq < hi), np.power(np.maximum(freq, 1e-4), -power), 0.0)
        field = np.fft.irfft2(spec * amp.astype(np.float32), s=(n, n)).astype(np.float32)
        field /= float(field.std()) + 1e-6
        acc += field * weight
    acc -= float(acc.mean())
    acc = acc / (float(acc.std()) + 1e-6) * 0.20 + 0.50
    # Pits: pigment catches in a few deep valleys. Still tileable.
    pits = acc < np.quantile(acc, 0.04)
    acc = acc.copy()
    acc[pits] -= 0.12
    height = np.clip(acc, 0.0, 1.0)
    # Confirm the seam matches. A shifted copy must agree on the overlap.
    rolled = np.roll(np.roll(height, n // 2, 0), n // 2, 1)
    if height.shape != rolled.shape:
        raise SystemExit("paper height is not square")
    print(
        "paper height",
        n,
        "mean %.3f std %.3f" % (float(height.mean()), float(height.std())),
    )
    return height


def low_density(n, seed=7):
    rng = np.random.RandomState(seed)
    field = rng.rand(12, 12).astype(np.float32)
    # 8-bit is enough for a 12x12 field. Pillow mode F is not a safe resize source.
    src = Image.fromarray(np.clip(np.round(field * 255.0), 0, 255).astype(np.uint8), "L")
    up = np.asarray(src.resize((n, n), Image.Resampling.BICUBIC), dtype=np.float32) / 255.0
    up = blur_wrap(up, n / 48.0)
    up -= float(up.min())
    up /= float(up.max()) + 1e-6
    return up


def pencil_channel(n, masks, rad, ang):
    """Thin strokes over the washes. Morning carries the radial hatch."""
    yy, xx = np.mgrid[0:n, 0:n]
    grain = ((xx * 17 + yy * 31) % 97) / 97.0
    out = np.zeros((n, n), np.float32)
    morning = masks["morning"]
    midday = masks["midday"]
    dusk = masks["dusk"]
    # Morning is the flat one. Strokes every 2 degrees, plus a short hatch.
    # Midday is already inside the hp band and sits on the delta-E cap, so it
    # gets no extra pencil.
    radial = (np.abs((ang - 1.7) % 2.0) < 0.22) & (rad > 0.058) & (rad < 0.104)
    out[morning & radial & (grain > 0.12)] = 1.0
    hatch = ((xx + yy * 2) % 5) == 0
    out[morning & hatch & (grain > 0.30) & (rad > 0.060) & (rad < 0.102)] = np.maximum(
        out[morning & hatch & (grain > 0.30) & (rad > 0.060) & (rad < 0.102)], 0.85
    )
    dusk_h = ((xx + yy * 3) % 11) == 0
    out[dusk & dusk_h & (grain > 0.55)] = 0.45
    return np.clip(out, 0.0, 1.0)


def build_control(repaired):
    masks_full = painted_arc_masks(repaired)
    masks = {name: down_bool(mask, CONTROL_N) for name, mask in masks_full.items()}
    dummy = np.zeros((CONTROL_N, CONTROL_N, 3), np.float32)
    _wx, _wz, rad, ang = polar(dummy)
    inside = masks["morning"] | masks["midday"] | masks["dusk"]
    # Distance to the outside of whichever wash this pixel belongs to.
    sdf = np.zeros((CONTROL_N, CONTROL_N), np.float32)
    pixel_m = (2.0 * FACE_R) / float(CONTROL_N - 1)
    try:
        from scipy.ndimage import distance_transform_edt
    except ImportError as exc:
        raise SystemExit("scipy is required for the arc SDF: %s" % exc)
    for name in ("morning", "midday", "dusk"):
        dist = distance_transform_edt(masks[name]) * pixel_m
        sdf[masks[name]] = dist[masks[name]]
    density = low_density(CONTROL_N)
    pencil = pencil_channel(CONTROL_N, masks, rad, ang)
    rgba = np.zeros((CONTROL_N, CONTROL_N, 4), np.uint8)
    rgba[..., 0] = np.clip(sdf / SDF_METRES, 0.0, 1.0) * 255.0
    rgba[..., 1] = np.clip(density, 0.0, 1.0) * 255.0
    rgba[..., 2] = np.clip(pencil, 0.0, 1.0) * 255.0
    alpha = np.zeros((CONTROL_N, CONTROL_N), np.uint8)
    for name, _a0, _a1, value in ARCS:
        alpha[masks[name]] = value
    rgba[..., 3] = alpha
    return rgba, masks, masks_full, inside


def decode_id(alpha):
    """Match the shader thresholds: 0 paper, 1 morning, 2 midday, 3 dusk."""
    a = alpha.astype(np.float32) / 255.0
    code = np.zeros(alpha.shape, np.uint8)
    code[a > 0.16] = 1
    code[a > 0.50] = 2
    code[a > 0.83] = 3
    return code


def highpass_std(rgb, mask):
    from scipy.ndimage import gaussian_filter

    lum = grade_a2.luma(np.asarray(rgb, dtype=np.float32))
    blur = gaussian_filter(lum, sigma=4.0, mode="nearest")
    hp = lum - blur
    chosen = mask & np.isfinite(hp)
    if int(chosen.sum()) < 20:
        return None
    return float(hp[chosen].std())


def shape_report(rgb):
    img = np.asarray(rgb, dtype=np.float32)
    legacy = legacy_sector_mask(img)
    painted = painted_arc_masks(img)
    grade = grade_dusk_mask(img)
    _wx, _wz, rad, ang = polar(img)
    hue = hue_of(img)
    chroma = img.max(axis=2) - img.min(axis=2)
    dusk_hue = (hue > 230.0) & (hue < 300.0) & (chroma > 28.0) & (rad > 0.04) & (rad < 0.14)
    outside = dusk_hue & ~in_arc(ang, 16.0, -100.0, 0.0)
    leak = float(outside.sum()) / float(max(int(dusk_hue.sum()), 1))
    rows = {
        "legacyIoU": round(iou(legacy, painted["dusk"]), 4),
        "gradeMaskIoU": round(iou(grade, painted["dusk"]), 4),
        "paintedPx": {name: int(mask.sum()) for name, mask in painted.items()},
        "legacyPx": int(legacy.sum()),
        "clippedPx": int(clipped_slab(img, rad).sum()),
        "duskHueLeak": round(leak, 4),
        "stamp": bool(sector_stamp_present(img)),
    }
    for name, mask in painted.items():
        rows.setdefault("textureHp", {})[name] = None if int(mask.sum()) < 20 else round(
            highpass_std(img, mask), 3
        )
    return rows


def write_png(path, array, mode):
    Image.fromarray(array, mode).save(path)
    print("wrote", path)


def build():
    face_path = os.path.join(TEX, "dial_face.png")
    src = np.asarray(Image.open(face_path).convert("RGB")).astype(np.float32)
    before = shape_report(src)
    print("locked face", json.dumps(before))
    repaired = repair_clipped_slab(src)
    after = shape_report(repaired)
    print("repaired face", json.dumps(after))
    if after["clippedPx"] > 80:
        raise SystemExit("clipped slab remains: %s" % after["clippedPx"])
    if after["duskHueLeak"] > 0.02:
        raise SystemExit("dusk hue leaks outside the arc: %s" % after["duskHueLeak"])
    rgba, masks, _full, _inside = build_control(repaired)
    code = decode_id(rgba[..., 3])
    ious = {}
    for index, name in ((1, "morning"), (2, "midday"), (3, "dusk")):
        ious[name] = round(iou(code == index, masks[name]), 4)
        if ious[name] < IOU_MIN:
            raise SystemExit("control IoU %s %.3f is under %.2f" % (name, ious[name], IOU_MIN))
        if int(masks[name].sum()) < 1000:
            raise SystemExit("painted %s mask is empty" % name)
    print("control IoU", json.dumps(ious))
    # The legacy sector is the gate the geometric stamp fails.
    if before["legacyIoU"] >= IOU_MIN:
        print("warning: legacy sector already matches the painted dusk arc")
    height = paper_height()
    face_u8 = np.clip(repaired, 0, 255).astype(np.uint8)
    write_png(os.path.join(TEX, "dial_face_s1.png"), face_u8, "RGB")
    write_png(os.path.join(TEX, "dial_control.png"), rgba, "RGBA")
    write_png(os.path.join(TEX, "dial_paper_h.png"), np.clip(height * 255.0, 0, 255).astype(np.uint8), "L")
    note = {
        "sdfMetres": SDF_METRES,
        "control": CONTROL_N,
        "paper": PAPER_N,
        "extraSamples": 3,
        "iou": ious,
        "locked": before,
        "repaired": after,
        "questFallback": "s1-quest",
    }
    # Art/Scripts sits outside Assets, so Unity will not import this sidecar.
    out_json = os.path.join(HERE, "s1-shape.json")
    with open(out_json, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(note, handle, indent=2)
        handle.write("\n")
    print("wrote", out_json)
    provenance = (
        "asset: dial_control.png, dial_paper_h.png, dial_face_s1.png\n"
        "method: apps/sundial/Art/Scripts/watercolour_s1.py\n"
        "note: Control is arc SDF (R), low-frequency density (G), pencil (B), wash id (A). "
        "Paper height is tileable spectral tooth, seed 41. dial_face_s1 feathers the clipped "
        "dusk band onto the painted arc. dial_face.png is unchanged (look A). "
        "Not copied from the art reference.\n"
        "derived_from_art_reference: no\n"
    )
    for name in ("dial_control.png", "dial_paper_h.png", "dial_face_s1.png"):
        path = os.path.join(TEX, name + ".provenance.txt")
        with open(path, "w", encoding="utf-8", newline="\n") as handle:
            handle.write(provenance)
        print("wrote", path)
    return note


# Section 1 measured hp on a clean patch, not the whole wash mask (the mask
# includes edges and reads ~20). Morning is the published patch: locked
# dial-g1-nohalo lands at hp 1.9, ref-1 at about 8.7 (the dossier quotes 9.2).
# Morning matches section 1: locked hp about 1.9, ref-1 about 9.2, on a pale
# window with no plant. Midday and dusk are the same kind of window.
HP_PATCHES = (
    ("morning", 165.6, 0.088),
    ("midday", 46.2, 0.096),
    ("dusk", -8.7, 0.088),
)
HP_WINDOW = 24


def patch_hp(img, xy, window=HP_WINDOW):
    cx, cy = float(xy[0]), float(xy[1])
    x0 = int(round(cx - window))
    y0 = int(round(cy - window))
    x1 = x0 + window * 2
    y1 = y0 + window * 2
    if x0 < 0 or y0 < 0 or x1 > img.shape[1] or y1 > img.shape[0]:
        return None
    crop = img[y0:y1, x0:x1]
    return highpass_std(crop, np.ones(crop.shape[:2], dtype=bool))


def score_capture(path, out_path, ref_path=REF1):
    if FIDELITY not in sys.path:
        sys.path.insert(0, FIDELITY)
    from lab.dial_wash import on_face, project, wash_samples
    from lab.regions import load

    img = np.asarray(Image.open(path).convert("RGB"))
    ref = np.asarray(Image.open(ref_path).convert("RGB"))
    if img.shape[0] != 1024 or img.shape[1] != 1824:
        raise SystemExit("%s is %s, expected 1024x1824" % (path, img.shape))
    pack = load("DialG1")
    washes = {}
    for name, deg, radius in HP_PATCHES:
        xy = project(on_face(deg, radius))[0]
        hp = patch_hp(img, xy)
        hp_ref = patch_hp(ref, xy)
        ratio = None if hp is None or not hp_ref else hp / hp_ref
        key = "wash." + name
        mask = pack["masks"][key] > 0
        mask_hp = highpass_std(img, mask)
        washes[name] = {
            "deg": deg,
            "radius": radius,
            "px": [round(float(xy[0]), 1), round(float(xy[1]), 1)],
            "hp": None if hp is None else round(hp, 3),
            "hpRef": None if hp_ref is None else round(hp_ref, 3),
            "ratio": None if ratio is None else round(ratio, 3),
            "hpPass": bool(ratio is not None and 0.6 <= ratio <= 1.4),
            "maskHp": None if mask_hp is None else round(mask_hp, 3),
            "maskPx": int(mask.sum()),
        }
    measured = wash_samples(img)
    delta = {}
    for name in ("morning", "midday", "dusk"):
        rec = measured[name]["ref1"]
        mean = None if rec is None else rec["meanDeltaE"]
        delta[name] = {
            "meanDeltaE": mean,
            "pixels": None if rec is None else rec["pixels"],
            "meanRgb": None if rec is None else rec["meanRgb"],
            "pass": bool(mean is not None and mean <= 8.0),
        }
    payload = {"frame": path, "washes": washes, "deltaE": delta}
    with open(out_path, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(payload, handle, indent=2)
        handle.write("\n")
    print(json.dumps(payload, indent=2))
    print("wrote", out_path)
    return payload


def flip_gif(path_a, path_b, out_path):
    frame_a = Image.open(path_a).convert("RGB")
    frame_b = Image.open(path_b).convert("RGB")
    if frame_b.size != frame_a.size:
        frame_b = frame_b.resize(frame_a.size, Image.Resampling.BILINEAR)
    frames = []
    for _ in range(3):
        frames.append(frame_a)
        frames.append(frame_b)
    frames[0].save(
        out_path,
        save_all=True,
        append_images=frames[1:],
        duration=400,
        loop=0,
        disposal=2,
    )
    print("wrote", out_path)


def grey_sbs(path_a, path_b, out_path, label_a="A", label_b="B"):
    def grey(path):
        rgb = np.asarray(Image.open(path).convert("RGB")).astype(np.float32)
        y = grade_a2.luma(rgb)
        g = np.clip(y, 0, 255).astype(np.uint8)
        return Image.fromarray(np.stack([g, g, g], axis=-1), "RGB")

    left = grey(path_a)
    right = grey(path_b)
    if right.size != left.size:
        right = right.resize(left.size, Image.Resampling.BILINEAR)
    gap = 8
    canvas = Image.new("RGB", (left.width * 2 + gap, left.height), (24, 24, 24))
    canvas.paste(left, (0, 0))
    canvas.paste(right, (left.width + gap, 0))
    draw = ImageDraw.Draw(canvas)
    font = ImageFont.load_default()
    draw.rectangle((8, 8, 36, 24), fill=(0, 0, 0))
    draw.rectangle((left.width + gap + 8, 8, left.width + gap + 36, 24), fill=(0, 0, 0))
    draw.text((12, 10), label_a, fill=(255, 255, 255), font=font)
    draw.text((left.width + gap + 12, 10), label_b, fill=(255, 255, 255), font=font)
    canvas.save(out_path)
    print("wrote", out_path)


def main(argv):
    if len(argv) >= 1 and argv[0] == "--score":
        if len(argv) < 4 or argv[2] != "--out":
            raise SystemExit("usage: watercolour_s1.py --score capture.png --out metrics.json")
        score_capture(argv[1], argv[3])
        return
    if len(argv) >= 1 and argv[0] == "--flip":
        if len(argv) != 4:
            raise SystemExit("usage: watercolour_s1.py --flip a.png b.png out.gif")
        flip_gif(argv[1], argv[2], argv[3])
        return
    if len(argv) >= 1 and argv[0] == "--grey":
        if len(argv) != 4:
            raise SystemExit("usage: watercolour_s1.py --grey a.png b.png out.png")
        grey_sbs(argv[1], argv[2], argv[3])
        return
    if argv:
        raise SystemExit("unknown args: %s" % " ".join(argv))
    build()


if __name__ == "__main__":
    main(sys.argv[1:])
