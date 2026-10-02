"""Background plates for the composite: the owner's reference frames with the hero object removed (OpenCV inpaint).
Passthrough cannot be captured on this machine, so the 'real room' is the room in the owner's own frame.
Also extracts a hand matte (GrabCut) so the real hand can be laid back OVER the render, as Quest hand occlusion would."""
import cv2, numpy as np, os
P = 'plates'
def ell(mask, cx, cy, rx, ry, val=255): cv2.ellipse(mask, (cx, cy), (rx, ry), 0, 0, 360, val, -1)

# ---------------- jar plate (A1-03-night-moss-1)
im = cv2.imread(f'{P}/A1-03-night-moss-1.png'); h, w = im.shape[:2]
m = np.zeros((h, w), np.uint8)
cv2.rectangle(m, (700, 180), (1140, 845), 255, -1)          # jar + cork
cv2.rectangle(m, (742, 175), (1085, 250), 255, -1)
ell(m, 905, 825, 315, 75)                                   # breath ring on the desk
cv2.rectangle(m, (790, 0), (1000, 200), 255, -1)            # rising mist
# gold spores: small bright yellow dots anywhere around the jar
b, g, r = [im[..., i].astype(int) for i in range(3)]
med = cv2.medianBlur(im, 21).astype(int)
spore = ((r + g) / 2 - (med[..., 2] + med[..., 1]) / 2 > 28) & (r > b + 25)
spore[:, :480] = False; spore[:, 1330:] = False
spore = cv2.dilate(spore.astype(np.uint8) * 255, np.ones((7, 7), np.uint8))
# keep the left hand's fingertips (they sit just left of the glass)
keep = np.zeros_like(m); cv2.rectangle(keep, (560, 470), (712, 760), 255, -1)
m = cv2.bitwise_and(cv2.bitwise_or(m, spore), cv2.bitwise_not(keep))
cv2.imwrite(f'{P}/jar-mask.png', m)
plate = cv2.inpaint(im, m, 9, cv2.INPAINT_TELEA)
# the inpaint smears; soften it into the dark room so the fill reads as out-of-focus background
blur = cv2.GaussianBlur(plate, (0, 0), 18)
mf = cv2.GaussianBlur(m.astype(np.float32) / 255, (0, 0), 12)[..., None]
plate = (plate * (1 - mf) + blur * mf)
# the fill inherits the reference jar's own green spill; pull it back toward the unlit room so our jar supplies the light
grey = plate.mean(axis=2, keepdims=True)
plate = plate * (1 - 0.55 * mf) + (grey * 0.55) * (0.55 * mf)
cv2.imwrite(f'{P}/plate-jar.png', np.clip(plate, 0, 255).astype(np.uint8))

# ---------------- dial plate (A2-05-field-notebook-1)
im = cv2.imread(f'{P}/A2-05-field-notebook-1.png'); h, w = im.shape[:2]
# hand matte: GrabCut seeded by a rectangle around the hand, with sure-foreground strokes on the back of the hand
gc = np.full((h, w), cv2.GC_BGD, np.uint8)
cv2.rectangle(gc, (1060, 170), (w - 1, h - 1), cv2.GC_PR_BGD, -1)
poly = np.array([[1120, 345], [1200, 260], [1330, 200], [1450, 230], [1560, 300], [1680, 480], [1824, 640], [1824, 1024],
                 [1500, 1024], [1400, 760], [1300, 600], [1140, 520], [1080, 450]], np.int32)
cv2.fillPoly(gc, [poly], cv2.GC_PR_FGD)
cv2.fillPoly(gc, [np.array([[1040, 560], [1285, 605], [1330, 650], [1420, 790], [1520, 1024], [1040, 1024]], np.int32)], cv2.GC_BGD)
for pts in ([[1400, 300], [1500, 420], [1600, 560], [1750, 700]], [[1300, 330], [1380, 420]], [[1600, 700], [1700, 900]]):
    cv2.polylines(gc, [np.array(pts, np.int32)], False, cv2.GC_FGD, 40)
bgd, fgd = np.zeros((1, 65), np.float64), np.zeros((1, 65), np.float64)
cv2.grabCut(im, gc, None, bgd, fgd, 6, cv2.GC_INIT_WITH_MASK)
hand = np.where((gc == cv2.GC_FGD) | (gc == cv2.GC_PR_FGD), 255, 0).astype(np.uint8)
hand = cv2.morphologyEx(hand, cv2.MORPH_OPEN, np.ones((5, 5), np.uint8))
n, lab, st, _ = cv2.connectedComponentsWithStats(hand)
if n > 1: hand = np.where(lab == 1 + np.argmax(st[1:, cv2.CC_STAT_AREA]), 255, 0).astype(np.uint8)
hand = cv2.GaussianBlur(hand, (0, 0), 1.6)
cv2.imwrite(f'{P}/dial-hand-matte.png', hand)
m = np.zeros((h, w), np.uint8)
ell(m, 785, 615, 512, 330)                                    # dial top + rim
ell(m, 800, 650, 520, 330)                                    # rim depth / thickness
cv2.ellipse(m, (840, 700), (500, 300), 0, 0, 180, 255, -1)    # contact shadow
m = cv2.bitwise_and(m, cv2.bitwise_not(cv2.threshold(hand, 200, 255, cv2.THRESH_BINARY)[1]))
cv2.imwrite(f'{P}/dial-mask.png', m)
low = cv2.GaussianBlur(cv2.inpaint(im, m, 15, cv2.INPAINT_TELEA), (0, 0), 6).astype(np.float32)
# re-grain the smear: high-frequency wood detail tiled from the visible table on the left, on top of the inpaint's lighting
src = im[330:1000, 0:270].astype(np.float32); hf = src - cv2.GaussianBlur(src, (0, 0), 6)
tile = np.zeros_like(low)
for y in range(0, h, hf.shape[0]):
    for x in range(0, w, hf.shape[1]):
        blk = hf if (x // hf.shape[1]) % 2 == 0 else hf[:, ::-1]
        hh, ww = min(hf.shape[0], h - y), min(hf.shape[1], w - x); tile[y:y + hh, x:x + ww] = blk[:hh, :ww]
mf = cv2.GaussianBlur(m.astype(np.float32) / 255, (0, 0), 4)[..., None]
plate = im * (1 - mf) + (low + tile * 0.9) * mf
cv2.imwrite(f'{P}/plate-dial.png', np.clip(plate, 0, 255).astype(np.uint8))
print('plates ok')
