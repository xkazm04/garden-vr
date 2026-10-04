# Spike S2. Room light on the drawing (F1 light cookie + D4 painted shadows).
# Look A is the locked drawing. Look B is capture state variant=roomlight.
#
#   python apps/sundial/Art/Scripts/roomlight_s2.py build               cookie + shadow wash textures
#   python apps/sundial/Art/Scripts/roomlight_s2.py score <png> [...] [--out f.json]
#   python apps/sundial/Art/Scripts/roomlight_s2.py flip a.png b.png out.gif
#   python apps/sundial/Art/Scripts/roomlight_s2.py grey a.png b.png out.png
#
# The cookie is the plate's own table lighting. Plate luminance outside the dial, the hand and the
# back wall is blurred with a masked convolution, divided by its local mean, capped at +-15% and
# projected from the G1 camera onto the table plane. The shader reads it in dial space, so the
# same pattern lands on the dial from any view (G1, seated, device).
import json
import math
import os
import sys

import numpy as np
from PIL import Image
from scipy import ndimage

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, "..", "..", "..", ".."))
PLATES = os.path.join(REPO, "shared", "assets", "room-plates")
REF1 = os.path.join(REPO, "shared", "assets", "art-reference", "A2-05-field-notebook-1.png")
TEX = os.path.join(REPO, "apps", "sundial", "Assets", "Art", "Textures")
RUN = os.path.join(REPO, "orchestration", "runs", "sundial", "T-SUN-044")

W, H = 1824, 1024
# DialG1, same numbers as Framings.BuiltInMap and tools/fidelity/lab/dial_wash.py.
EYE = (0.0, 0.322, -0.411)
LOOK = (0.0, 0.012, 0.0)
FOV = 30.0
LENS = (3.6, 0.5)

COOKIE_N = 256
# The cookie spans this many metres across, centred on the dial origin. A hair past the catcher quad.
COOKIE_SIZE = 0.40
AMP = 0.15
SIGMA_FIELD = 70.0
SIGMA_MEAN = 260.0


def camera(eye=EYE, look=LOOK, fov=FOV, lens=LENS, width=W, height=H):
    eye = np.array(eye, float)
    look = np.array(look, float)
    f = look - eye
    f /= np.linalg.norm(f)
    up = np.array([0.0, 1.0, 0.0])
    r = np.cross(up, f)
    r /= np.linalg.norm(r)
    u = np.cross(f, r)
    basis = np.stack([r, u, f], 1)
    lx = math.radians(lens[0])
    a = -math.radians(lens[1])
    ry = np.array([[math.cos(lx), 0, math.sin(lx)], [0, 1, 0], [-math.sin(lx), 0, math.cos(lx)]])
    rx = np.array([[1, 0, 0], [0, math.cos(a), -math.sin(a)], [0, math.sin(a), math.cos(a)]])
    rot = basis @ ry @ rx
    tan = math.tan(math.radians(fov) / 2.0)
    aspect = width / height

    def project(points):
        pts = np.asarray(points, float).reshape(-1, 3)
        cam = (pts - eye) @ rot
        x = cam[:, 0] / cam[:, 2] / (tan * aspect)
        y = cam[:, 1] / cam[:, 2] / tan
        return (x * 0.5 + 0.5) * width, (0.5 - y * 0.5) * height

    return project


def luma(path):
    a = np.asarray(Image.open(path).convert("RGB"), dtype=np.float64)
    return 0.2126 * a[..., 0] + 0.7152 * a[..., 1] + 0.0722 * a[..., 2]


def dilate(mask, px):
    return ndimage.binary_dilation(mask, structure=np.ones((3, 3), bool), iterations=int(px))


def valid_table_mask():
    """Plate pixels that show lit table, not the dial, the hand, the wall or the props."""
    dial = np.asarray(Image.open(os.path.join(PLATES, "dial-mask.png")).convert("L")) >= 128
    hand = np.asarray(Image.open(os.path.join(PLATES, "dial-hand-matte.png")).convert("L")) >= 128
    ok = np.ones((H, W), bool)
    ok &= ~dilate(dial, 28)
    ok &= ~dilate(hand, 24)
    ok[:205, :] = False
    ok[:335, :135] = False
    ok[:335, 1380:] = False
    return ok


def masked_blur(values, weight, sigma):
    num = ndimage.gaussian_filter(values * weight, sigma, mode="nearest")
    den = ndimage.gaussian_filter(weight, sigma, mode="nearest")
    return num, den


def light_field(lum, ok):
    """Blurred table luminance over its local mean, filled across the dial. Returns ratio, mean."""
    w = ok.astype(np.float64)
    filled = np.zeros_like(lum)
    have = np.zeros(lum.shape, bool)
    for sigma in (SIGMA_FIELD, 140.0, 280.0, 520.0):
        num, den = masked_blur(lum, w, sigma)
        take = (~have) & (den > 0.02)
        filled[take] = num[take] / den[take]
        have |= take
    # one more smoothing so the join between sigmas does not show
    filled = ndimage.gaussian_filter(filled, 24.0, mode="nearest")
    num, den = masked_blur(lum, w, SIGMA_MEAN)
    mean = np.where(den > 0.02, num / np.maximum(den, 1e-6), lum[ok].mean())
    mean = ndimage.gaussian_filter(mean, 60.0, mode="nearest")
    ratio = filled / np.maximum(mean, 1.0)
    return ratio, mean


def build_cookie():
    lum = luma(os.path.join(PLATES, "plate-dial.png"))
    ok = valid_table_mask()
    ratio, _mean = light_field(lum, ok)
    d_screen = np.clip((ratio - 1.0) / AMP, -1.0, 1.0)
    project = camera()
    n = COOKIE_N
    xs = (np.arange(n) + 0.5) / n * COOKIE_SIZE - COOKIE_SIZE / 2.0
    # PNG row 0 is the top of the texture, which Unity reads as v = 1 (z = +size/2, the far side).
    zs = COOKIE_SIZE / 2.0 - (np.arange(n) + 0.5) / n * COOKIE_SIZE
    gx, gz = np.meshgrid(xs, zs)
    pts = np.stack([gx.ravel(), np.zeros(gx.size), gz.ravel()], 1)
    px, py = project(pts)
    d = ndimage.map_coordinates(d_screen, [py - 0.5, px - 0.5], order=1, mode="nearest").reshape(n, n)
    d = ndimage.gaussian_filter(d, 2.0, mode="nearest")
    return d, d_screen, ratio, ok


def wash_alpha(n=512):
    """One broad wedge. Apex at the bottom centre (v = 0 is the nib end), widening away from it.
    Soft outer edge, a darker rim where the wash dried, a low-frequency mottle, fading with length."""
    rng = np.random.default_rng(44)
    yy, xx = np.mgrid[0:n, 0:n].astype(np.float64)
    v = 1.0 - (yy + 0.5) / n
    u = (xx + 0.5) / n - 0.5
    half = 0.04 + 0.30 * v
    edge = (half - np.abs(u)) * n
    soft = 1.0 / (1.0 + np.exp(-edge / 30.0))
    rim = np.exp(-np.maximum(edge, 0.0) / 26.0)
    body = 0.78 + 0.22 * rim
    t = np.clip((v - 0.30) / 0.70, 0.0, 1.0)
    fade = 1.0 - t * t * (3.0 - 2.0 * t)
    near = np.clip(v / 0.06, 0.0, 1.0)
    noise = ndimage.gaussian_filter(rng.standard_normal((n, n)), 26.0, mode="wrap")
    noise = noise / (np.abs(noise).max() + 1e-6)
    alpha = soft * body * fade * near * (1.0 + 0.16 * noise)
    return np.clip(alpha, 0.0, 1.0)


def write_png(path, array, mode):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    Image.fromarray(array, mode).save(path)
    print("wrote", path)


def disc_masks(radius=0.15):
    n = COOKIE_N
    xs = (np.arange(n) + 0.5) / n * COOKIE_SIZE - COOKIE_SIZE / 2.0
    zs = COOKIE_SIZE / 2.0 - (np.arange(n) + 0.5) / n * COOKIE_SIZE
    gx, gz = np.meshgrid(xs, zs)
    return gx, gz, np.hypot(gx, gz) < radius


def disc_mean(d):
    return d[disc_masks()[2]].mean()


def disc_left_right(d):
    gx, _gz, disc = disc_masks()
    return d[disc & (gx < -0.05)].mean() - d[disc & (gx > 0.05)].mean()


def preview(d_screen, ok, d):
    """Screen-space field over the plate (unused plate dimmed), and the cookie in table space."""
    plate = np.asarray(Image.open(os.path.join(PLATES, "plate-dial.png")).convert("RGB"), dtype=np.float64)
    g = np.clip(d_screen * 0.5 + 0.5, 0, 1)
    ramp = np.stack([g, g, g], -1) * 255.0
    shade = np.where(ok[..., None], 1.0, 0.45)
    over = np.clip(0.5 * plate * shade + 0.5 * ramp, 0, 255).astype(np.uint8)
    Image.fromarray(over).save(os.path.join(RUN, "cookie-field.png"))
    big = np.round((d * 0.5 + 0.5) * 255).astype(np.uint8)
    Image.fromarray(big, "L").resize((512, 512), Image.NEAREST).save(os.path.join(RUN, "cookie-table.png"))


def build():
    d, d_screen, _ratio, ok = build_cookie()
    cookie = np.round((d * 0.5 + 0.5) * 255.0).astype(np.uint8)
    write_png(os.path.join(TEX, "room_cookie.png"), cookie, "L")
    a = wash_alpha()
    rgba = np.zeros(a.shape + (4,), np.uint8)
    rgba[..., 0] = 77
    rgba[..., 1] = 56
    rgba[..., 2] = 41
    rgba[..., 3] = np.round(a * 255.0).astype(np.uint8)
    write_png(os.path.join(TEX, "room_shadow_wash.png"), rgba, "RGBA")
    os.makedirs(RUN, exist_ok=True)
    preview(d_screen, ok, d)
    stats = {
        "n": COOKIE_N,
        "sizeMetres": COOKIE_SIZE,
        "amp": AMP,
        "min": float(d.min()),
        "max": float(d.max()),
        "meanOnDialDisc": float(disc_mean(d)),
        "leftMinusRightOnDialDisc": float(disc_left_right(d)),
        "sigmaField": SIGMA_FIELD,
        "sigmaMean": SIGMA_MEAN,
        "validTableFrac": float(ok.mean()),
    }
    with open(os.path.join(RUN, "cookie.json"), "w", encoding="utf-8", newline="\n") as handle:
        json.dump(stats, handle, indent=2)
        handle.write("\n")
    print(json.dumps(stats, indent=2))


# ---------- scoring ----------

def dial_masks():
    """Dial pixels in G1 minus the hand."""
    dial = np.asarray(Image.open(os.path.join(PLATES, "dial-mask.png")).convert("L")) >= 128
    hand = np.asarray(Image.open(os.path.join(PLATES, "dial-hand-matte.png")).convert("L")) >= 128
    return dial & ~hand


def thirds(lum, m, x0=273, x1=1340):
    w = (x1 - x0) / 3.0
    cols = np.arange(W)[None, :]
    out = []
    for i in range(3):
        sel = m & (cols >= x0 + i * w) & (cols < x0 + (i + 1) * w)
        out.append(float(lum[sel].mean()))
    return out


def region_means(lum, frame="DialG1"):
    sys.path.insert(0, os.path.join(REPO, "tools", "fidelity"))
    from lab import regions

    masks = regions.load(frame)["masks"]
    out = {}
    for name, mask in masks.items():
        sel = mask > 0
        if sel.sum() > 50:
            out[name] = float(lum[sel].mean())
    return out


def score(paths, out_path=None):
    ref = luma(REF1)
    m = dial_masks()
    rt = thirds(ref, m)
    result = {"reference": {"thirds": rt, "leftMinusRight": rt[0] - rt[2], "regions": region_means(ref)}}
    for p in paths:
        lum = luma(p)
        t = thirds(lum, m)
        result[os.path.basename(p)] = {
            "thirds": t,
            "leftMinusRight": t[0] - t[2],
            "regions": region_means(lum),
        }
    text = json.dumps(result, indent=2)
    print(text)
    if out_path:
        with open(out_path, "w", encoding="utf-8", newline="\n") as handle:
            handle.write(text + "\n")


def main(argv):
    if not argv or argv[0] == "build":
        build()
        return
    sys.path.insert(0, HERE)
    if argv[0] == "score":
        out = None
        paths = argv[1:]
        if "--out" in paths:
            i = paths.index("--out")
            out = paths[i + 1]
            paths = paths[:i]
        score(paths, out)
        return
    import watercolour_s1 as s1

    if argv[0] == "flip" and len(argv) == 4:
        s1.flip_gif(argv[1], argv[2], argv[3])
        return
    if argv[0] == "grey" and len(argv) == 4:
        s1.grey_sbs(argv[1], argv[2], argv[3])
        return
    raise SystemExit(__doc__)


if __name__ == "__main__":
    main(sys.argv[1:])
