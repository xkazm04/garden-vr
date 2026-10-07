"""Turn generated Night Moss plates into game textures.

Sources land in apps/terrarium/Art/Source with a .prompt.txt sidecar.
Processed textures (<= 1024 px) land in apps/terrarium/Assets/Art/Textures.

Nothing is sampled from shared/assets/art-reference. Run from the repo root:

    python tools/blender/terrarium_textures.py
"""
import hashlib
import os

import cv2
import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
GEN = (
    r"C:\Users\kazda\.grok\sessions"
    r"\C%3A%5CUsers%5Ckazda%5Ckiro%5Cgvr-terrarium"
    r"\01a0fe1e-8ee2-7823-b038-086bc81b42e2\images"
)
SRC = os.path.join(ROOT, "apps", "terrarium", "Art", "Source")
TEX = os.path.join(ROOT, "apps", "terrarium", "Assets", "Art", "Textures")
REFS = [
    os.path.join(ROOT, "shared", "assets", "art-reference", "A1-03-night-moss-1.png"),
    os.path.join(ROOT, "shared", "assets", "art-reference", "A1-03-night-moss-2.png"),
]

# Prompts are the exact image_gen text. native files are the tool outputs.
JOBS = [
    ("fern_a", "1.jpg", "2:3", "frond",
     "A single sword-fern frond stands vertical and centered, filling the frame from a short rachis at the bottom to a pointed tip at the top. The frond is bipinnate and lacy, with irregular pinnae of uneven lengths and fine leaflets separated by deep gaps, colored a soft yellow-green with a brighter mint edge along each leaflet. It is a front-lit botanical macro photograph on a pure black background, sharp from base to tip, with natural asymmetry."),
    ("fern_b", "10.jpg", "2:3", "frond",
     "A single broader fern frond stands centered on a pure black background, fully inside the frame with a wide margin of pure black around every leaflet. The pinnae are slightly arched and uneven, with lacy leaflets and deep black gaps between them, pale green blades and a luminous mint rim. Front-lit macro photograph, sharp, nothing else in the frame."),
    ("moss_macro", "12.jpg", "1:1", "tile",
     "Edge-to-edge seamless texture of cushion moss photographed from directly above. Every part of the frame is moss, including the corners: irregular clumps of tiny overlapping leaves, deep green crevices and brighter yellow-green tips, damp and soft. Even diffuse light, the same density from center to corner, a tileable macro photograph."),
    ("moss_tuft", "8.jpg", "1:1", "tuft",
     "A single bright clump of cushion moss floats in the center of a pure black frame, with a wide margin of pure black on every side. The clump is irregular and starry, lit from the front, with vivid mint-green tips and deeper green bases. Macro photograph, sharp, the whole tuft fully inside the frame."),
    ("cork", "9.jpg", "1:1", "tile",
     "Edge-to-edge seamless texture of natural wine cork, warm tan-brown bark covered in small oval pores and fine granules. The same pore density reaches every corner. Even diffuse light, matte, a tileable macro photograph of cork filling the whole frame."),
    ("soil", "11.jpg", "1:1", "tile",
     "Edge-to-edge seamless texture of very dark damp forest loam, nearly black with a deep green undertone, fine crumbs and a few pale sand grains. The same darkness reaches every corner. Even low light, a tileable macro photograph of bare soil filling the whole frame."),
    ("condensation", "7.jpg", "1:1", "drops",
     "A macro photograph of clear water droplets clinging to glass, scattered across a pure black background. Beads range from tiny specks to a few larger drops, each with a bright cyan-white specular highlight and a soft transparent body. The droplets are the only subject, evenly distributed, sharp, and ready to read as a texture."),
]

# Style-bible targets used only as a grade (no reference pixels).
GRADE = {
    "moss_macro": np.array([55, 98, 34], np.float32),   # measured moss #376222
    "cork": np.array([138, 90, 59], np.float32),        # cork #8A5A3B
    "soil": np.array([13, 35, 29], np.float32),         # soil #0D231D
}
MINT = np.array([143, 240, 200], np.float32)            # #8FF0C8
MINT_CAP = np.array([232, 255, 244], np.float32)        # #E8FFF4 emission cap


def sha256(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 16), b""):
            h.update(chunk)
    return h.hexdigest()


def fit_1024(im):
    w, h = im.size
    m = max(w, h)
    if m <= 1024:
        return im
    s = 1024 / m
    return im.resize((max(1, int(round(w * s))), max(1, int(round(h * s)))), Image.LANCZOS)


def blend_axis(a, axis, frac=0.16):
    """Average opposite outer edges so the image tiles. Index 0 of each strip is the outer edge."""
    n = a.shape[axis]
    b = max(4, int(n * frac))
    if axis == 1:
        left = a[:, :b].copy()
        right = a[:, -1:-b - 1:-1].copy()
        fade = np.linspace(1.0, 0.0, b, dtype=np.float32)[None, :, None]
        out_l = left * (1 - 0.5 * fade) + right * (0.5 * fade)
        out_r = right * (1 - 0.5 * fade) + left * (0.5 * fade)
        a = a.copy()
        a[:, :b] = out_l
        a[:, -b:] = out_r[:, ::-1]
    else:
        top = a[:b].copy()
        bot = a[-1:-b - 1:-1].copy()
        fade = np.linspace(1.0, 0.0, b, dtype=np.float32)[:, None, None]
        out_t = top * (1 - 0.5 * fade) + bot * (0.5 * fade)
        out_b = bot * (1 - 0.5 * fade) + top * (0.5 * fade)
        a = a.copy()
        a[:b] = out_t
        a[-b:] = out_b[::-1]
    return a


def make_seamless(a, frac=0.16, horizontal_only=False):
    a = blend_axis(a, 1, frac)
    if not horizontal_only:
        a = blend_axis(a, 0, frac)
    return a


def seam_error(a):
    lr = np.abs(a[:, 0].astype(np.float32) - a[:, -1].astype(np.float32)).mean()
    tb = np.abs(a[0].astype(np.float32) - a[-1].astype(np.float32)).mean()
    return float(lr), float(tb)


def grade_toward(a, target):
    mean = a.reshape(-1, a.shape[-1]).mean(axis=0)
    ratio = target / np.maximum(mean, 1.0)
    ratio = np.clip(ratio, 0.7, 1.55)
    return np.clip(a * ratio, 0, 255)


def key_black(rgb, thresh=12.0, knee=26.0):
    mx = rgb.max(axis=2)
    alpha = np.clip((mx - thresh) / max(1.0, knee - thresh), 0.0, 1.0)
    # Straighten edges that were mixed with the black plate.
    safe = np.maximum(alpha, 0.18)[..., None]
    straight = np.clip(rgb / safe, 0, 255)
    straight[alpha < 0.04] = 0
    return straight, alpha


def trim(rgba, pad=10):
    a = rgba[..., 3]
    ys, xs = np.where(a > 16)
    if len(xs) == 0:
        return rgba
    y0 = max(0, int(ys.min()) - pad)
    y1 = min(rgba.shape[0], int(ys.max()) + pad + 1)
    x0 = max(0, int(xs.min()) - pad)
    x1 = min(rgba.shape[1], int(xs.max()) + pad + 1)
    return rgba[y0:y1, x0:x1]


def emission_from_alpha(alpha):
    mask = (alpha > 0.45).astype(np.uint8)
    dist = cv2.distanceTransform(mask, cv2.DIST_L2, 3)
    edge = np.exp(-dist / 2.0) * (alpha > 0.12)
    fill = 0.22 * (alpha > 0.35)
    rgb = MINT_CAP * edge[..., None] + MINT * fill[..., None]
    return np.clip(rgb, 0, 255).astype(np.uint8)


def save_png(path, arr, mode):
    Image.fromarray(arr, mode).save(path)
    print("wrote", os.path.relpath(path, ROOT), arr.shape)


def write_sidecar(path, name, aspect, prompt, native, stored, processing):
    text = (
        f"tool: image_gen\n"
        f"aspect_ratio: {aspect}\n"
        f"native_size: {native[0]}x{native[1]}\n"
        f"stored_size: {stored[0]}x{stored[1]}\n"
        f"source_name: {name}\n"
        f"processing: {processing}\n"
        f"pixels: generated, not cropped or keyed from shared/assets/art-reference/\n"
        f"prompt:\n{prompt}\n"
    )
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)


def process_frond(name, rgb):
    straight, alpha = key_black(rgb, 12, 28)
    rgba = np.dstack([straight, alpha * 255.0]).astype(np.uint8)
    rgba = trim(rgba, pad=8)
    emit = emission_from_alpha(rgba[..., 3].astype(np.float32) / 255.0)
    albedo = Image.fromarray(rgba, "RGBA")
    emission = Image.fromarray(emit, "RGB")
    albedo = fit_1024(albedo)
    emission = emission.resize(albedo.size, Image.LANCZOS)
    save_png(os.path.join(TEX, f"{name}_albedo.png"), np.asarray(albedo), "RGBA")
    save_png(os.path.join(TEX, f"{name}_emission.png"), np.asarray(emission), "RGB")
    a = np.asarray(albedo)[..., 3]
    print(f"  {name} alpha coverage { (a > 16).mean():.3f} size {albedo.size}")
    return "keyed off black, trimmed to the frond, edge emission from a distance field, max side 1024"


def process_tuft(rgb):
    straight, alpha = key_black(rgb, 16, 32)
    # Harder cut and a darker green so the card reads as moss, not a white fringe.
    alpha = np.clip((alpha - 0.40) / 0.28, 0.0, 1.0)
    straight = np.clip(straight * 0.58, 0, 255)
    rgba = np.dstack([straight, alpha * 255.0]).astype(np.uint8)
    rgba = trim(rgba, pad=6)
    im = fit_1024(Image.fromarray(rgba, "RGBA"))
    save_png(os.path.join(TEX, "moss_tuft.png"), np.asarray(im), "RGBA")
    a = np.asarray(im)[..., 3]
    print(f"  tuft alpha coverage {(a > 16).mean():.3f} size {im.size}")
    return "keyed off black, trimmed to the clump, max side 1024"


def process_tile(name, rgb, horizontal_only=False):
    a = make_seamless(rgb.astype(np.float32), 0.16, horizontal_only)
    if name in GRADE:
        a = grade_toward(a, GRADE[name])
    im = fit_1024(Image.fromarray(a.astype(np.uint8), "RGB"))
    arr = np.asarray(im)
    save_png(os.path.join(TEX, f"{name}.png"), arr, "RGB")
    lr, tb = seam_error(arr)
    print(f"  {name} seam L/R {lr:.2f} T/B {tb:.2f} mean {arr.reshape(-1, 3).mean(0).round(1)}")
    return "seamless edge blend, graded toward the style-bible hex, max side 1024"


def process_drops(rgb):
    lum = rgb.max(axis=2).astype(np.float32)
    corners = np.concatenate([
        lum[:24, :24].ravel(), lum[:24, -24:].ravel(),
        lum[-24:, :24].ravel(), lum[-24:, -24:].ravel(),
    ])
    bg = float(np.median(corners))
    coverage = np.clip((lum - bg - 6.0) / 36.0, 0, 1)
    highlight = np.clip((lum - 150.0) / 90.0, 0, 1)
    haze = cv2.GaussianBlur(coverage, (0, 0), 6.0)
    packed = np.dstack([coverage, highlight, haze]) * 255.0
    packed = make_seamless(packed, 0.12, horizontal_only=True)
    im = fit_1024(Image.fromarray(packed.astype(np.uint8), "RGB"))
    save_png(os.path.join(TEX, "condensation.png"), np.asarray(im), "RGB")
    print(f"  drops bg {bg:.1f} coverage mean {coverage.mean():.3f} highlight mean {highlight.mean():.3f}")
    return "R coverage, G highlight, B soft haze; horizontal seamless blend for the jar wrap; max side 1024"


def main():
    os.makedirs(SRC, exist_ok=True)
    os.makedirs(TEX, exist_ok=True)
    ref_hash = {os.path.basename(p): sha256(p) for p in REFS if os.path.exists(p)}
    for name, fname, aspect, kind, prompt in JOBS:
        raw = Image.open(os.path.join(GEN, fname)).convert("RGB")
        native = raw.size
        rgb = np.asarray(raw).astype(np.float32)
        if kind == "frond":
            note = process_frond(name, rgb)
        elif kind == "tuft":
            note = process_tuft(rgb)
        elif kind == "tile":
            note = process_tile(name, rgb)
        elif kind == "drops":
            note = process_drops(rgb)
        else:
            raise SystemExit(kind)
        source = fit_1024(raw)
        source_path = os.path.join(SRC, f"{name}.png")
        source.save(source_path)
        write_sidecar(
            os.path.join(SRC, f"{name}.prompt.txt"),
            name, aspect, prompt, native, source.size, note,
        )
        print("source", name, native, "->", source.size)
        digest = sha256(source_path)
        for ref_name, ref_digest in ref_hash.items():
            if digest == ref_digest:
                raise SystemExit(f"{name} matches reference {ref_name}")
    # Game textures must not match the reference frames either.
    for dirpath, _, files in os.walk(TEX):
        for fn in files:
            if not fn.lower().endswith(".png"):
                continue
            digest = sha256(os.path.join(dirpath, fn))
            for ref_name, ref_digest in ref_hash.items():
                if digest == ref_digest:
                    raise SystemExit(f"texture {fn} matches reference {ref_name}")
    print("provenance ok: no texture or source matches the reference frames")


if __name__ == "__main__":
    main()
