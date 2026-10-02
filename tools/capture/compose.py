"""Post: (1) lay the real hand back OVER the dial render (what Quest's hand occlusion does), (2) build labelled
side-by-sides reference | render at the same framing, (3) a crop strip for the detail comparison, (4) mp4s.
Every output image carries its source in a caption bar."""
import os, subprocess, cv2, numpy as np
from PIL import Image, ImageDraw, ImageFont
F = r'C:\hgspike\fidelity'; R = os.path.join(F, 'renders'); P = os.path.join(F, 'plates'); O = os.path.join(F, 'sbs')
os.makedirs(O, exist_ok=True)
try: font = ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf', 26); small = ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf', 20)
except Exception: font = small = ImageFont.load_default()


def hand_over(render_path, out_path):
    r = np.asarray(Image.open(render_path).convert('RGB')).astype(np.float32)
    ref = np.asarray(Image.open(os.path.join(P, 'A2-05-field-notebook-1.png')).convert('RGB')).astype(np.float32)
    m = np.asarray(Image.open(os.path.join(P, 'dial-hand-matte.png')).convert('L')).astype(np.float32)[..., None] / 255
    Image.fromarray((r * (1 - m) + ref * m).astype(np.uint8)).save(out_path)


for n in ('dial-idle', 'dial-halo'):
    hand_over(os.path.join(R, n + '.png'), os.path.join(R, n + '-hand.png'))


def caption(img, text, sub):
    w, h = img.size; bar = 64
    out = Image.new('RGB', (w, h + bar), (18, 20, 24)); out.paste(img, (0, bar))
    d = ImageDraw.Draw(out); d.text((16, 6), text, fill=(240, 240, 235), font=font); d.text((16, 36), sub, fill=(170, 175, 180), font=small)
    return out


def sbs(ref, render, name, ref_label, render_label, scale=0.5):
    a = Image.open(os.path.join(P, ref)).convert('RGB'); b = Image.open(os.path.join(R, render)).convert('RGB')
    s = (int(a.width * scale), int(a.height * scale)); a = a.resize(s, Image.LANCZOS); b = b.resize(s, Image.LANCZOS)
    a = caption(a, 'REFERENCE  ' + ref, ref_label); b = caption(b, 'RENDER  ' + render, render_label)
    out = Image.new('RGB', (a.width * 2 + 12, a.height), (8, 8, 10)); out.paste(a, (0, 0)); out.paste(b, (a.width + 12, 0))
    out.save(os.path.join(O, name)); print('sbs', name)


U = 'Unity 6.6 URP, batchmode, no post-processing, 8x MSAA, 1824x1024; room = inpainted owner frame'
sbs('A1-03-night-moss-1.png', 'jar-midbreath.png', 'sbs-jar-midbreath.png', "owner's frame (Leonardo), mid-breath", U)
sbs('A1-03-night-moss-2.png', 'jar-answer.png', 'sbs-jar-answer.png', "owner's frame 2 (no 'answer' frame exists; closest framing)", U + '; state: the frond stays')
sbs('A2-05-field-notebook-1.png', 'dial-halo-hand.png', 'sbs-dial-halo.png', "owner's frame (Leonardo), pinch halo", U + '; real hand re-laid over render')
sbs('A2-05-field-notebook-1.png', 'dial-idle-hand.png', 'sbs-dial-idle.png', "owner's frame (Leonardo)", U + '; idle, no halo')


def crops(ref, render, boxes, name):
    a = Image.open(os.path.join(P, ref)).convert('RGB'); b = Image.open(os.path.join(R, render)).convert('RGB')
    tiles = []
    for (x0, y0, x1, y1, lab) in boxes:
        ca = a.crop((x0, y0, x1, y1)).resize((360, int(360 * (y1 - y0) / (x1 - x0))), Image.LANCZOS)
        cb = b.crop((x0, y0, x1, y1)).resize(ca.size, Image.LANCZOS)
        t = Image.new('RGB', (ca.width * 2 + 6, ca.height + 34), (18, 20, 24)); t.paste(ca, (0, 34)); t.paste(cb, (ca.width + 6, 34))
        ImageDraw.Draw(t).text((8, 4), f'{lab}: reference | render', fill=(235, 235, 230), font=small); tiles.append(t)
    W = max(t.width for t in tiles); H = sum(t.height + 8 for t in tiles)
    out = Image.new('RGB', (W, H), (8, 8, 10)); y = 0
    for t in tiles: out.paste(t, (0, y)); y += t.height + 8
    out.save(os.path.join(O, name)); print('crops', name)


crops('A1-03-night-moss-1.png', 'jar-midbreath.png', [(780, 360, 1020, 560, 'fiddlehead + glass'), (720, 600, 1110, 830, 'moss, soil, base'), (560, 740, 1250, 920, 'breath ring on the desk')], 'crops-jar.png')
crops('A2-05-field-notebook-1.png', 'dial-halo-hand.png', [(880, 360, 1100, 640, 'plant + pinch halo'), (300, 300, 800, 620, 'washes, ticks, gnomon'), (350, 650, 1000, 940, 'tiles, rim, table shadow')], 'crops-dial.png')

for seq, out in (('seq-jar', 'jar-loop.mp4'), ('seq-dial', 'dial-loop.mp4')):
    src = os.path.join(R, seq, 'f%03d.png')
    subprocess.run(['ffmpeg', '-y', '-loglevel', 'error', '-framerate', '24', '-i', src, '-c:v', 'libx264', '-pix_fmt', 'yuv420p', '-crf', '22', os.path.join(R, out)], check=True)
    print('video', out)
