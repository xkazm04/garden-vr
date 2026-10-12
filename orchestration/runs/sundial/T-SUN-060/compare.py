from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import json
import numpy as np
root = Path.cwd()
run = root / 'orchestration/runs/sundial/T-SUN-060'
current = Image.open(run / 'DialG1-listening.png').convert('RGB')
released = Image.open(run / 'DialG1-released.png').convert('RGB')
a, b = np.array(current), np.array(released)
changed = np.any(a != b, axis=2)
y, x = np.where(changed)
metrics = {'changedPixels': int(changed.sum()), 'boundsXYXY': [int(x.min()), int(y.min()), int(x.max()), int(y.max())], 'size': list(current.size)}
(run / 'ripple-pixels.json').write_text(json.dumps(metrics, indent=2) + '\n')
font = ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf', 23)
for name, path, title in [
 ('reference-side-by-side.jpg', 'shared/assets/art-reference/A2-05-field-notebook-1.png', 'Owner reference: Field Notebook'),
 ('artboard-side-by-side.jpg', 'docs/art/artboards/field-notebook/3-reflection.jpg', 'Reflection artboard: speech layout target')]:
 ref = Image.open(root / path).convert('RGB')
 assert ref.size == current.size
 canvas = Image.new('RGB', (current.width * 2, current.height + 48), '#211f1b')
 canvas.paste(ref, (0, 48)); canvas.paste(current, (current.width, 48))
 draw = ImageDraw.Draw(canvas)
 draw.text((16, 8), title, fill='#eee8da', font=font)
 draw.text((current.width + 16, 8), 'B3 scripted listening capture | DialG1 | existing preset dial', fill='#eee8da', font=font)
 canvas.save(run / name, quality=95, subsampling=0)
print(json.dumps(metrics))
