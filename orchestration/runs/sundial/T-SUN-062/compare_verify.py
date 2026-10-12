"""Rebuild full-frame comparisons and summarize the actual final test/build evidence."""
from pathlib import Path
import re
import hashlib
import xml.etree.ElementTree as ET
from PIL import Image, ImageDraw

RUN = Path(__file__).resolve().parent
ROOT = RUN.parents[3]
capture = Image.open(RUN / 'DialG1-onboarding.png').convert('RGB')
for source, name, label in [
    ('docs/art/artboards/field-notebook/1-onboarding-zones.jpg', 'DialG1-onboarding-side-by-side.jpg', 'B5 onboarding artboard'),
    ('shared/assets/art-reference/A2-05-field-notebook-1.png', 'DialG1-reference-side-by-side.jpg', 'Field Notebook reference'),
]:
    reference = Image.open(ROOT / source).convert('RGB')
    assert reference.size == capture.size == (1824, 1024)
    board = Image.new('RGB', (3648, 1064), '#f3eee2')
    board.paste(reference, (0, 40))
    board.paste(capture, (1824, 40))
    draw = ImageDraw.Draw(board)
    draw.text((16, 12), label, fill='#2a2622')
    draw.text((1840, 12), 'Unity DialG1: one accepted packet, two offered, zone tabs visible', fill='#2a2622')
    board.save(RUN / name, quality=95, subsampling=0)

lines = []
for name in ['editmode', 'playmode']:
    root = ET.parse(RUN / (name + '.xml')).getroot()
    lines.append(f'{name}: result={root.get("result")} total={root.get("total")} passed={root.get("passed")} failed={root.get("failed")}')
    assert int(root.get('total')) > 0 and int(root.get('failed')) == 0
    if name == 'playmode':
        for case in root.iter('test-case'):
            if 'Interview' in case.get('name', ''):
                lines.append(f'  {case.get("name")}: {case.get("result")} ({case.get("duration")} s)')
for name in ['editmode', 'playmode', 'build', 'terrarium-compile']:
    log = (RUN / (name + '.log')).read_text(encoding='utf-8-sig', errors='replace')
    errors = re.findall(r'error CS\d+|Shader error|BuildFailedException|^Exception:', log, flags=re.M)
    lines.append(f'{name}: compiler/shader/build errors={len(errors)}')
    assert not errors
    if name == 'build':
        success = re.findall(r'^Build Succeeded .+$', log, flags=re.M)
        assert success
        lines.extend(success)
for image in sorted(RUN.glob('*.png')):
    with Image.open(image) as im:
        lines.append(f'{image.name}: {im.size}; SHA256={hashlib.sha256(image.read_bytes()).hexdigest()}')
raw_input = re.compile(r'Keyboard\.current|Mouse\.current|UnityEngine\.Input\.|Input\.Get(?:Key|Button|Mouse)')
hits = [str(p) for p in (ROOT / 'apps/sundial/Assets/Scripts').rglob('*.cs') if raw_input.search(p.read_text(encoding='utf-8-sig'))]
assert not hits, hits
lines.append('App direct keyboard/mouse reads=0')
strings = (RUN / 'user-facing-strings.txt').read_text(encoding='utf-8')
assert '\u2014' not in strings
guards = (ROOT / 'shared/packages/com.gardenvr.core/Runtime/Coach/CoachGuards.cs').read_text(encoding='utf-8-sig')
normal = ' ' + re.sub(r'[^\w\d]', ' ', strings.lower()) + ' '
for array in ['MedicalStems', 'CountPhrases', 'GuiltStems']:
    body = re.search(array + r'\s*=\s*\{(.*?)\}', guards, re.S).group(1)
    for word in re.findall(r'"([^"]+)"', body):
        assert (word if array == 'CountPhrases' else ' ' + word) not in normal, (array, word)
lines.append('String census: no em dash; no CoachGuards medical/count/guilt matches')
(RUN / 'verification.txt').write_text('\n'.join(lines) + '\n', encoding='utf-8')
print('\n'.join(lines))
