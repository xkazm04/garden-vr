"""Painted / derived textures for both heroes. Every texture is either painted here from code ('painted') or
cropped from the owner's reference frames ('crop'). Output: tex/*.png, copied into Unity (Assets/Fidelity/Textures)."""
import cv2, numpy as np, math, os
from PIL import Image, ImageDraw, ImageFilter
T = 'tex'; P = 'plates'; os.makedirs(T, exist_ok=True)
rng = np.random.default_rng(7)


def fbm(h, w, octaves=5, base=4, seed=0):
    r = np.random.default_rng(seed); acc = np.zeros((h, w), np.float32); amp = 1; tot = 0
    for o in range(octaves):
        s = base * 2 ** o
        n = r.random((s + 1, s + 1)).astype(np.float32)
        acc += amp * cv2.resize(n, (w, h), interpolation=cv2.INTER_CUBIC); tot += amp; amp *= 0.5
    acc /= tot; return (acc - acc.min()) / (acc.max() - acc.min() + 1e-6)


def save(name, arr):
    arr = np.clip(arr, 0, 255).astype(np.uint8)
    Image.fromarray(arr).save(f'{T}/{name}.png'); print('wrote', name, arr.shape)


# ======================================================================= JAR (Night Moss)
# --- painted fern frond: pinnate, lobed pinnae, brighter midribs; alpha + emission (edge glow from a distance field)
W, H, S = 1024, 2048, 2
img = Image.new('L', (W * S, H * S), 0); d = ImageDraw.Draw(img)
vein = Image.new('L', (W * S, H * S), 0); dv = ImageDraw.Draw(vein)
cx = W * S / 2; y0, y1 = H * S * 0.98, H * S * 0.03


def rach(t): return (cx + math.sin(t * 2.2) * W * S * 0.025, y0 + (y1 - y0) * t)


dv.line([rach(t / 100) for t in range(101)], fill=255, width=int(9 * S))
d.line([rach(t / 100) for t in range(101)], fill=255, width=int(12 * S))
n_pairs = 17
for i in range(n_pairs):
    t = 0.10 + 0.86 * i / (n_pairs - 1)
    env = math.sin(math.pi * min(1, (t - 0.04) / 0.98)) ** 0.8 * (1 - 0.35 * t)
    L = W * S * 0.47 * env
    for side in (-1, 1):
        tt = t + (0.012 if side > 0 else 0)
        bx, by = rach(tt)
        ang = math.radians(58 - 18 * t)  # pinnae sweep upward
        lobes = max(3, int(8 * env + 3))
        dx, dy = side * math.sin(ang), -math.cos(ang)          # pinna axis
        nx, ny = -dy, dx                                        # its normal
        upper, lower, pts = [], [], []
        for k in range(61):
            uu = k / 60
            px = bx + dx * L * uu; py = by + dy * L * uu - (L * 0.10) * uu * uu   # slight upward curl
            wdt = L * 0.17 * math.sin(math.pi * min(1, uu ** 0.75)) * (1 - 0.25 * uu)
            wdt *= 0.70 + 0.30 * abs(math.sin(lobes * math.pi * uu))            # scalloped pinnule lobes
            upper.append((px + nx * wdt, py + ny * wdt)); lower.append((px - nx * wdt * 0.85, py - ny * wdt * 0.85)); pts.append((px, py))
        d.polygon(upper + lower[::-1], fill=255)
        dv.line([(bx, by)] + pts, fill=200, width=int(4 * S))
        d.line([(bx, by)] + pts, fill=255, width=int(7 * S))
img = img.resize((W, H), Image.LANCZOS); vein = vein.resize((W, H), Image.LANCZOS)
a = np.asarray(img).astype(np.float32) / 255; v = np.asarray(vein).astype(np.float32) / 255
dist = cv2.distanceTransform((a > 0.5).astype(np.uint8), cv2.DIST_L2, 5)
edge = np.exp(-dist / 5.0) * (a > 0.02)
tone = fbm(H, W, 5, 6, 3)
base = np.stack([0.42 + 0.10 * tone, 0.78 + 0.10 * tone, 0.50 + 0.08 * tone], -1)  # pale fern green
alb = base * (0.80 + 0.20 * (1 - edge[..., None])) + v[..., None] * np.array([0.25, 0.20, 0.12])
save('fern_albedo', np.dstack([alb * 255, a * 255]))
emi = np.clip(0.30 + 0.85 * edge + 0.55 * v, 0, 1.6) / 1.6   # 0..1, scaled in the shader
save('fern_emission', np.dstack([emi * 255] * 3))

# --- crops from the owner's frame (A1-03-night-moss-1): moss band, soil band, cork side
ref = cv2.cvtColor(cv2.imread(f'{P}/A1-03-night-moss-1.png'), cv2.COLOR_BGR2RGB)


def crop_band(x0, y0, x1, y1, w, h, name, mirror=True):
    c = cv2.resize(ref[y0:y1, x0:x1], (w // 2 if mirror else w, h), interpolation=cv2.INTER_CUBIC)
    if mirror: c = np.concatenate([c, c[:, ::-1]], 1)  # seamless wrap around a cylinder
    save(name, c)


crop_band(952, 640, 1088, 712, 1024, 256, 'moss_band')        # crop: the moss cap
crop_band(745, 712, 1090, 800, 1024, 256, 'soil_band')        # crop: soil + green specks
crop_band(775, 200, 1055, 236, 1024, 128, 'cork_side')        # crop: cork
ct = fbm(512, 512, 6, 16, 9); sp = (rng.random((512, 512)) < 0.05).astype(np.float32)   # painted: cork top
sp = cv2.GaussianBlur(sp, (0, 0), 1.2) * 3
col = np.clip(np.stack([0.30 + 0.16 * ct - 0.12 * sp, 0.21 + 0.12 * ct - 0.09 * sp, 0.14 + 0.08 * ct - 0.06 * sp], -1), 0, 1)
save('cork_top', col * 255)
mt = fbm(512, 512, 6, 12, 4); spk = (rng.random((512, 512)) < 0.02).astype(np.float32); spk = cv2.GaussianBlur(spk, (0, 0), 0.8) * 4
save('moss_top', np.stack([0.12 + 0.20 * mt + 0.2 * spk, 0.30 + 0.40 * mt + 0.4 * spk, 0.08 + 0.12 * mt], -1) * 255)


# moss tile (crop, mirrored 2x2 so it tiles) and the fuzz mask for the shells (painted: clumped tuft noise)
mc = cv2.resize(ref[652:712, 955:1085], (256, 256), interpolation=cv2.INTER_CUBIC)
mc = np.concatenate([np.concatenate([mc, mc[:, ::-1]], 1), np.concatenate([mc[::-1], mc[::-1, ::-1]], 1)], 0)
save('moss_tile', mc)
tuft = np.zeros((512, 512), np.float32)
for _ in range(5200):
    x_, y_, r_ = rng.integers(0, 512), rng.integers(0, 512), rng.integers(2, 6)
    cv2.circle(tuft, (int(x_), int(y_)), int(r_), float(0.5 + 0.5 * rng.random()), -1, cv2.LINE_AA)
tuft = cv2.GaussianBlur(tuft, (0, 0), 1.2); tuft = np.clip(tuft * (0.55 + 0.6 * fbm(512, 512, 5, 6, 77)), 0, 1)
mt3 = cv2.resize(mc, (512, 512)).astype(np.float32) * (0.75 + 0.5 * tuft[..., None])
save('moss_fuzz', np.dstack([mt3, tuft * 255]))
# gnomon: a fountain-pen body, dark lacquer with a gold collar, along the lathe profile parameter (v)
gn = np.zeros((256, 64, 3), np.float32) + np.array([0.20, 0.19, 0.19])
vv = np.linspace(1, 0, 256)[:, None]
gold = ((vv > 0.10) & (vv < 0.24)).astype(np.float32)
gn = gn * (1 - gold[..., None]) + gold[..., None] * np.array([0.78, 0.62, 0.30])
gn[:, 20:26] += 0.35   # a painted highlight stripe down the barrel
save('gnomon', gn * 255)

# moss tuft card (crop): the moss ridge against the dark glass, keyed by how much greener than blue it is
mcard = ref[622:700, 952:1092].astype(np.float32)
key = np.clip((mcard[..., 1] - mcard[..., 2] - 22) / 38, 0, 1)
key[int(key.shape[0] * 0.55):] = np.maximum(key[int(key.shape[0] * 0.55):], np.linspace(0, 1, key.shape[0] - int(key.shape[0] * 0.55))[:, None])
key = cv2.GaussianBlur(key, (0, 0), 0.6)
save('moss_card', np.dstack([cv2.resize(mcard, (1024, 256)), cv2.resize(key * 255, (1024, 256))]))

# --- particle + glow sprites (painted)
def radial(n, power=2.0):
    yy, xx = np.mgrid[0:n, 0:n] / (n - 1) * 2 - 1; r = np.sqrt(xx * xx + yy * yy); return np.clip(1 - r, 0, 1) ** power


sp = radial(64, 3.0) + 0.6 * radial(64, 12.0)
save('spore', np.dstack([np.clip(sp, 0, 1) * 255] * 3))
save('halo', np.dstack([radial(256, 2.2) * 255] * 3))
m = fbm(512, 256, 6, 3, 11) * fbm(512, 256, 4, 2, 12)   # mist card: soft noise smoke, transparent edges
yy, xx = np.mgrid[0:512, 0:256]
mask = np.clip(1 - np.abs(xx / 255 * 2 - 1) ** 1.5, 0, 1) * np.clip(np.sin(np.pi * yy / 511), 0, 1) ** 1.2
mist = np.clip(m * 1.8, 0, 1) * mask ** 1.6 * 1.25
save('mist', np.dstack([np.full_like(mist, 255)] * 3 + [mist * 255]))
cond = np.zeros((1024, 1024), np.float32); hi = np.zeros_like(cond)   # condensation droplets: R coverage, G highlight, B haze
for _ in range(700):
    px, py, r = rng.integers(0, 1024), rng.integers(0, 1024), rng.choice([2, 3, 3, 4, 5, 7, 9])
    cv2.circle(cond, (int(px), int(py)), int(r), 1.0, -1, cv2.LINE_AA)
    cv2.circle(hi, (int(px - r * 0.35), int(py - r * 0.35)), max(1, int(r * 0.35)), 1.0, -1, cv2.LINE_AA)
haze = fbm(1024, 1024, 5, 4, 21)
save('condensation', np.dstack([cond * 255, hi * 255, haze * 255]))
n = 1024; yy, xx = np.mgrid[0:n, 0:n] / (n - 1) * 2 - 1; r = np.sqrt(xx * xx + yy * yy)   # breath ring + light pool
core = np.exp(-((r - 0.92) / 0.0045) ** 2); soft = np.exp(-((r - 0.92) / 0.025) ** 2) * 0.30
pool = np.clip(1 - r / 0.9, 0, 1) ** 1.5 * 0.5
save('ring', np.dstack([np.clip(core + soft, 0, 1) * 255, pool * 255, np.zeros_like(r)]))

# ======================================================================= DIAL (Field Notebook)
N = 2048; yy, xx = np.mgrid[0:N, 0:N].astype(np.float32); u = (xx / (N - 1)) * 2 - 1; v = -((yy / (N - 1)) * 2 - 1)
R = np.sqrt(u * u + v * v); A = np.degrees(np.arctan2(v, u)) % 360   # 90 = back (away from viewer), 270 = front
grain = fbm(N, N, 7, 32, 31); fib = cv2.GaussianBlur(rng.random((N, N)).astype(np.float32), (0, 0), 0.7)
paper = np.array([0.94, 0.90, 0.82], np.float32)[None, None] * (0.93 + 0.05 * grain[..., None] + 0.04 * fib[..., None])
img = paper.copy()


def wash(mask, c0, c1=None, gradient=None, strength=0.75, seed=1):
    """watercolour: noisy coverage, granulation, a darker wet edge where the pigment pooled as it dried"""
    global img
    soft = cv2.GaussianBlur(mask, (0, 0), 6)
    n1 = fbm(N, N, 6, 6, seed); n2 = fbm(N, N, 6, 40, seed + 1)
    cov = np.clip(soft * (0.40 + 0.85 * n1) - 0.08, 0, 1)
    edge = np.clip(soft - cv2.GaussianBlur(soft, (0, 0), 14), 0, 1) * 3.2
    al = np.clip((cov * (0.80 + 0.35 * n2) + edge * 0.5) * strength, 0, 1)
    c = np.array(c0, np.float32)[None, None]
    if c1 is not None: c = gradient[..., None] * np.array(c1, np.float32) + (1 - gradient[..., None]) * np.array(c0, np.float32)
    img = img * (1 - al[..., None] * (1 - c))


band = ((R > 0.60) & (R < 0.885)).astype(np.float32)


def sector(a0, a1): return (((A - a0) % 360) < ((a1 - a0) % 360)).astype(np.float32)


# sunrise (left-back), midday (back-right), dusk (right-front); the front stays paper, as in the owner's frame
g = np.clip((A - 120) / 70, 0, 1)
wash(band * sector(118, 192), [0.98, 0.62, 0.36], [0.99, 0.86, 0.46], g, 0.85, 3)
wash(band * sector(52, 118), [0.97, 0.52, 0.50], strength=0.85, seed=5)
wash(band * sector(318, 52), [0.70, 0.58, 0.86], strength=0.80, seed=7)
wash(((R > 0.56) & (R < 0.62)).astype(np.float32) * sector(100, 200), [0.98, 0.80, 0.55], strength=0.35, seed=9)
img8 = Image.fromarray((np.clip(img, 0, 1) * 255).astype(np.uint8)).convert('RGBA')
ink = Image.new('RGBA', (N, N), (0, 0, 0, 0)); di = ImageDraw.Draw(ink)
C = N / 2; RR = N / 2 - 4


def circ(r, w, col, wob=0.0, seed=0):
    rr = np.random.default_rng(seed); ph = rr.random(4) * 6.28
    pts = []
    for k in range(721):
        t = k / 720 * 2 * math.pi; ro = r * RR * (1 + wob * (math.sin(3 * t + ph[0]) * 0.5 + math.sin(7 * t + ph[1]) * 0.3))
        pts.append((C + ro * math.cos(t), C - ro * math.sin(t)))
    di.line(pts, fill=col, width=w, joint='curve')


INK = (52, 42, 34, 235); PEN = (95, 88, 80, 150)
for k_ in range(26):                              # pencil sun-rays in the sunrise arc
    t = math.radians(124 + k_ * 2.6 + rng.random() * 1.2); r0_, r1_ = 0.60, 0.62 + 0.20 * rng.random()
    di.line([(C + r0_ * RR * math.cos(t), C - r0_ * RR * math.sin(t)), (C + r1_ * RR * math.cos(t), C - r1_ * RR * math.sin(t))], fill=(190, 120, 60, 120), width=2)
circ(0.995, 5, INK, 0.0015, 1); circ(0.885, 4, INK, 0.002, 2); circ(0.60, 3, INK, 0.003, 3); circ(0.935, 2, PEN, 0.001, 4)
circ(0.565, 2, PEN, 0.004, 5)
for deg in range(0, 360, 3):                      # pencil ticks on the rim
    t = math.radians(deg); big = deg % 15 == 0
    r0, r1 = (0.935, 0.995) if big else (0.96, 0.995)
    di.line([(C + r0 * RR * math.cos(t), C - r0 * RR * math.sin(t)), (C + r1 * RR * math.cos(t), C - r1 * RR * math.sin(t))],
            fill=INK if big else PEN, width=3 if big else 2)
for deg in (52, 118, 192, 318, 255):              # sector dividers in ink
    t = math.radians(deg)
    di.line([(C + 0.60 * RR * math.cos(t), C - 0.60 * RR * math.sin(t)), (C + 0.995 * RR * math.cos(t), C - 0.995 * RR * math.sin(t))], fill=INK, width=3)
tile_col = {'sun': (249, 205, 140), 'mid': (240, 150, 140), 'dusk': (186, 160, 220)}
groups = [('sun', 222, [1, 1, 1, 1, 0, 1, 0]), ('mid', 266, [1, 1, 1, 0, 1, 1, 1]), ('dusk', 310, [1, 1, 1, 1, 1, 0, 0])]
for name, a0, filled in groups:                   # seven tiles per habit on the rim band
    for k, f in enumerate(filled):
        t = math.radians(a0 + (k - 3) * 5.0); rc = 0.745 * RR
        px, py = C + rc * math.cos(t), C - rc * math.sin(t); s = 0.030 * RR
        tile = Image.new('RGBA', (int(s * 2.4), int(s * 2.4)), (0, 0, 0, 0)); dt = ImageDraw.Draw(tile)
        col = tile_col[name] + (220,) if f else (236, 230, 216, 200)
        dt.rounded_rectangle([s * 0.2, s * 0.2, s * 2.2, s * 2.2], radius=s * 0.35, fill=col, outline=(70, 58, 46, 230), width=3)
        tile = tile.rotate(math.degrees(t) - 90, resample=Image.BICUBIC, expand=False)
        ink.alpha_composite(tile, (int(px - s * 1.2), int(py - s * 1.2)))
ink = ink.filter(ImageFilter.GaussianBlur(0.6))
img8.alpha_composite(ink)
top = np.asarray(img8.convert('RGB')).astype(np.float32) / 255
refd = cv2.cvtColor(cv2.imread(f'{P}/A2-05-field-notebook-1.png'), cv2.COLOR_BGR2RGB)
# painted soil bed: warm brown wash, darker clods, ink specks, a few outlined pebbles, lighter toward the gnomon
sn = fbm(N, N, 7, 10, 51); sc = fbm(N, N, 6, 48, 52)
soil = np.stack([0.47 + 0.16 * sn, 0.38 + 0.13 * sn, 0.29 + 0.10 * sn], -1) * (0.75 + 0.35 * sc[..., None])
soil *= (0.80 + 0.30 * np.clip(1 - R / 0.575, 0, 1) ** 0.5)[..., None]
specks = (rng.random((N, N)) < 0.012).astype(np.float32); specks = cv2.GaussianBlur(specks, (0, 0), 1.1) * 2.2
soil *= (1 - np.clip(specks, 0, 0.85))[..., None]
sim = Image.fromarray((np.clip(soil, 0, 1) * 255).astype(np.uint8)).convert('RGBA'); ds = ImageDraw.Draw(sim)
for _ in range(34):
    ang = rng.random() * 6.283; rad = (0.25 + 0.75 * rng.random() ** 0.5) * 0.55 * RR; pr = 6 + rng.random() * 12
    px, py = C + rad * math.cos(ang), C - rad * math.sin(ang); tone = int(170 + rng.random() * 60)
    ds.ellipse([px - pr, py - pr * 0.75, px + pr, py + pr * 0.75], fill=(tone, tone - 8, tone - 22, 255), outline=(60, 48, 38, 255), width=2)
soil = np.asarray(sim.convert('RGB')).astype(np.float32) / 255
sm = cv2.GaussianBlur((R < 0.575).astype(np.float32), (0, 0), 3)[..., None]
top = top * (1 - sm) + soil * sm
save('dial_top', top * 255)
hh = 256; side = np.ones((hh, 2048, 3), np.float32) * np.array([0.90, 0.86, 0.78])   # rim: stacked paper edge
for k in range(5, hh, 34): side[k:k + 2] *= 0.82
side *= (0.92 + 0.08 * fbm(hh, 2048, 5, 16, 41))[..., None]
for xk in range(0, 2048, 9): side[hh - 70:hh, xk:xk + 1] *= 0.88      # pencil hatching low on the rim
save('dial_side', side * 255)


def card(x0, y0, x1, y1, name, kill_glow=False, violet_sat=70, green_sat=38, stems=()):
    """plant sprite card keyed out of the owner's frame by colour (leaf green, pink, violet) plus the ink line around it.
    An art-time stand-in for a 2D artist's card; the plant drawing itself is the owner's image, not ours."""
    sub = refd[y0:y1, x0:x1].copy()
    hsv = cv2.cvtColor(sub, cv2.COLOR_RGB2HSV).astype(int); hue, sat, val = hsv[..., 0], hsv[..., 1], hsv[..., 2]
    green = (hue > 28) & (hue < 95) & (sat > green_sat) & (val > 50)
    pink = ((hue > 150) | (hue < 6)) & (sat > 55) & (val > 120)
    violet = (hue > 118) & (hue <= 150) & (sat > violet_sat)
    mm = (green | pink | violet).astype(np.uint8) * 255
    mm = cv2.morphologyEx(mm, cv2.MORPH_CLOSE, np.ones((5, 5), np.uint8))
    near = cv2.dilate(mm, np.ones((5, 5), np.uint8)) > 0
    inky = (val < 110) & near                                  # the ink contour hugging the colour
    mm = np.where((mm > 0) | inky, 255, 0).astype(np.uint8)
    if kill_glow:  # the reference's own pinch halo is a warm bright line: never let it into the card
        mm[(val > 205) & (hue > 14) & (hue < 40) & (sat > 60)] = 0
    nlab, lab, st, _ = cv2.connectedComponentsWithStats(mm)
    hh2, ww2 = mm.shape
    keepi = [i for i in range(1, nlab) if st[i, cv2.CC_STAT_AREA] > 60 and st[i, 0] > 2 and st[i, 1] > 2 and st[i, 0] + st[i, 2] < ww2 - 2]  # drop wash blobs touching the crop edge
    mm = np.isin(lab, keepi).astype(np.uint8) * 255
    mm = cv2.GaussianBlur(mm, (0, 0), 0.7)
    # painted: the thin stems the colour key cannot separate from the wash, redrawn as green-ink strokes
    rgba = Image.fromarray(np.dstack([sub, mm])); dr = ImageDraw.Draw(rgba)
    for st_ in stems:
        dr.line(st_, fill=(58, 70, 40, 255), width=4, joint='curve'); dr.line(st_, fill=(118, 146, 84, 255), width=2, joint='curve')
    save(name, np.asarray(rgba))


card(430, 490, 590, 690, 'plant_sunrise')
card(908, 392, 1060, 620, 'plant_midday', kill_glow=True, stems=[[(42, 42), (58, 120), (72, 185)], [(28, 90), (52, 140), (70, 186)], [(88, 70), (80, 130), (76, 185)]])
card(1030, 588, 1155, 742, 'plant_dusk', violet_sat=60, green_sat=22, stems=[[(96, 42), (76, 100), (58, 140)], [(30, 88), (44, 115), (55, 140)]])
# pinch halo: a glowing line that follows the plant silhouette (dilate the card alpha, keep a thin ring) - painted
pm = np.asarray(Image.open(f'{T}/plant_midday.png'))[..., 3]
pad = 40; pm = np.pad(pm, pad); big = cv2.dilate(pm, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (41, 41)))
big = cv2.GaussianBlur(big, (0, 0), 7); line = np.exp(-((big.astype(np.float32) / 255 - 0.5) / 0.11) ** 2)
glow = np.clip(cv2.GaussianBlur(line, (0, 0), 9) * 1.6, 0, 1)
save('halo_line', np.dstack([np.clip(line, 0, 1) * 255, np.clip(glow, 0, 1) * 255, np.zeros_like(line)]))
