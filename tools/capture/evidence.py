"""Copy round-3 evidence into the report folder: labelled contact sheet of every texture (with its source),
round-2 -> round-3 evolution, renders as JPEG for the page, side-by-sides, crops, overdraw maps, video, logs."""
import os, shutil, json
from PIL import Image, ImageDraw, ImageFont
F = r'C:\hgspike\fidelity'
V = r'C:\Users\kazda\kiro\personas\.contest\arena\habit-garden-r2-r3\entries\claude-claude-opus-5-5_high-v1\variant-1'
E = os.path.join(V, 'evidence', 'r3')
for d in ('sbs', 'renders', 'video', 'measure', 'assets', 'logs'): os.makedirs(os.path.join(E, d), exist_ok=True)
font = ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf', 22); small = ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf', 17)

# --- texture contact sheet, each tile captioned with what made it
SRC = {
    'fern_albedo': 'painted (code): pinnate frond, alpha', 'fern_emission': 'painted (code): edge glow from distance field',
    'moss_band': "crop: owner's frame, moss", 'moss_tile': "crop: owner's frame, moss (2x2 mirror)", 'soil_band': "crop: owner's frame, soil",
    'cork_side': "crop: owner's frame, cork", 'cork_top': 'painted (code): cork grain', 'condensation': 'painted (code): droplets R, glint G, haze B',
    'mist': 'painted (code): smoke card', 'halo': 'painted (code): halo card', 'spore': 'painted (code): spore sprite', 'ring': 'painted (code): breath ring R + pool G',
    'dial_top': 'painted (code): paper, washes, ink, ticks, tiles, soil', 'dial_side': 'painted (code): stacked paper edge',
    'gnomon': 'painted (code): pen lacquer + gold collar', 'plant_sunrise': "keyed card: owner's drawing", 'plant_midday': "keyed card: owner's drawing + painted stems",
    'plant_dusk': "keyed card: owner's drawing + painted stems", 'halo_line': 'painted (code): pinch halo from plant silhouette',
}
tiles = []
for n, src in SRC.items():
    im = Image.open(os.path.join(F, 'tex', n + '.png')).convert('RGBA')
    bg = Image.new('RGBA', im.size, (40, 44, 48, 255) if 'plant' not in n else (225, 215, 195, 255)); bg.alpha_composite(im)
    th = bg.convert('RGB'); th.thumbnail((300, 220))
    t = Image.new('RGB', (310, 290), (250, 248, 242)); t.paste(th, ((310 - th.width) // 2, 8))
    d = ImageDraw.Draw(t); d.text((8, 232), n, fill=(29, 40, 32), font=font); d.text((8, 262), src[:40], fill=(80, 90, 84), font=small)
    tiles.append(t)
cols = 5; rows = (len(tiles) + cols - 1) // cols
sheet = Image.new('RGB', (cols * 316, rows * 296), (221, 212, 193))
for i, t in enumerate(tiles): sheet.paste(t, ((i % cols) * 316 + 3, (i // cols) * 296 + 3))
sheet.save(os.path.join(E, 'assets', 'texture-sheet.jpg'), quality=90)

# --- evolution: round-2 Night Lantern (procedural) -> round-3 jar, same idea
r2 = Image.open(os.path.join(V, 'evidence', 'looks', 'C-answer.png')).convert('RGB').resize((800, 500))
r3 = Image.open(os.path.join(F, 'renders', 'jar-answer.png')).convert('RGB').crop((412, 100, 1412, 725)).resize((800, 500))
ev = Image.new('RGB', (1612, 560), (18, 20, 24)); ev.paste(r2, (0, 60)); ev.paste(r3, (812, 60))
d = ImageDraw.Draw(ev); d.text((12, 14), 'ROUND 2: procedural primitives, sawtooth leaves, bloom', fill=(240, 240, 235), font=font)
d.text((824, 14), 'ROUND 3: Blender assets, painted cards, halo cards, no post', fill=(240, 240, 235), font=font)
ev.save(os.path.join(E, 'sbs', 'evolution-r2-r3.jpg'), quality=90)

for n in os.listdir(os.path.join(F, 'sbs')): shutil.copy(os.path.join(F, 'sbs', n), os.path.join(E, 'sbs', n))
for n in ('jar-midbreath', 'jar-answer', 'jar-lean-midbreath', 'jar-answer-noplate', 'dial-idle-hand', 'dial-halo-hand', 'dial-halo', 'debug-jar-noglass'):
    Image.open(os.path.join(F, 'renders', n + '.png')).convert('RGB').save(os.path.join(E, 'renders', n + '.jpg'), quality=92)
for n in ('overdraw-jar', 'overdraw-jar-lean', 'overdraw-dial'):
    shutil.copy(os.path.join(F, 'renders', n + '.png'), os.path.join(E, 'measure', n + '.png'))
for n in ('jar-loop.mp4', 'dial-loop.mp4'): shutil.copy(os.path.join(F, 'renders', n), os.path.join(E, 'video', n))
for n in ('measure.txt', 'gemini-jar-midbreath.txt', 'gemini-dial-halo.txt', 'apk-badging.txt', 'blender.log'):
    p = os.path.join(F, 'logs', n)
    if os.path.exists(p): shutil.copy(p, os.path.join(E, 'logs' if not n.startswith('measure') else 'measure', n))
for n in ('apk.log', 'capture.log', 'measure-unity.log'):
    p = os.path.join(F, 'logs', n)
    if os.path.exists(p):
        keep = [l for l in open(p, encoding='utf-8', errors='replace') if any(k in l for k in ('[Fid]', '[SpikeAndroid]', 'error', 'Exception', 'Build Finished', 'APK'))]
        open(os.path.join(E, 'logs', n.replace('.log', '-extract.log')), 'w', encoding='utf-8').writelines(keep[:400])
# plates + mattes as used
for n in ('plate-jar', 'plate-dial', 'dial-hand-matte'):
    Image.open(os.path.join(F, 'plates', n + '.png')).convert('RGB').resize((912, 512)).save(os.path.join(E, 'assets', n + '.jpg'), quality=88)
print('evidence ok')
