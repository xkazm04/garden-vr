"""Rebuild the uncropped DialG1 comparisons and verification summary from this run's evidence."""
from pathlib import Path
import hashlib
import json
import re
import xml.etree.ElementTree as ET
from PIL import Image, ImageDraw, ImageFont

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
actual = Image.open(HERE / 'DialG1-monthly-record-nine-plants.png').convert('RGB')
assert actual.size == (1824, 1024)
font = ImageFont.truetype('C:/Windows/Fonts/arial.ttf', 25)
for relative, label, output in [
    ('docs/art/artboards/field-notebook/4-record.jpg', 'B9 record artboard', 'DialG1-record-side-by-side.jpg'),
    ('shared/assets/art-reference/A2-05-field-notebook-1.png', 'Field Notebook G1 style reference', 'DialG1-reference-side-by-side.jpg'),
]:
    reference = Image.open(ROOT / relative).convert('RGB')
    assert reference.size == actual.size
    board = Image.new('RGB', (3648, 1072), '#f3eee2')
    board.paste(reference, (0, 48))
    board.paste(actual, (1824, 48))
    draw = ImageDraw.Draw(board)
    draw.text((20, 10), label + ' | 1824 x 1024, uncropped', fill='#2a2622', font=font)
    draw.text((1844, 10), 'T-SUN-066 | actual nine-plant monthly record, DialG1', fill='#2a2622', font=font)
    board.save(HERE / output, quality=95, subsampling=0)

lines = []
guard_source = (ROOT / 'shared/packages/com.gardenvr.core/Runtime/Coach/CoachGuards.cs').read_text()
stems = re.findall(r'"([^"]+)"', guard_source.split('MedicalStems =', 1)[1].split('};', 1)[0])
catalog = ['kept {N} days this month', '{count} of {target}', 'Body', 'Mind', 'Work', 'Connection']
for line in catalog:
    assert '\u2014' not in line
    normal = ' ' + re.sub(r'[^a-z0-9]', ' ', line.lower())
    assert not any(' ' + stem in normal for stem in stems), line
lines.append('B9 string census: 2 new templates, 4 reused zone names; em dash and medical stem hits=0')
for name in ['editmode', 'playmode']:
    result = ET.parse(HERE / (name + '.xml')).getroot()
    lines.append(name + ' ' + ' '.join(f'{key}={result.attrib[key]}' for key in ['result', 'total', 'passed', 'failed', 'skipped']))
    assert int(result.attrib['total']) > 0 and result.attrib['failed'] == '0'
    if name == 'playmode':
        for case in result.iter('test-case'):
            if '.MonthlyRecordTests.' in case.attrib.get('fullname', ''):
                lines.append(case.attrib['name'] + ' ' + case.attrib['result'])
for name in ['editmode', 'playmode', 'build', 'terrarium-compile']:
    log = (HERE / (name + '.log')).read_text(encoding='utf-8-sig', errors='replace')
    errors = re.findall(r'^.*(?:error CS\d+|Shader error|Build Failed).*$', log, re.M)
    lines.append(f'{name} compiler/shader/build errors={len(errors)}')
    assert not errors, errors
    for line in log.splitlines():
        if 'Build Succeeded ' in line or (name == 'terrarium-compile' and 'Exiting batchmode successfully' in line):
            lines.append(line)
for png in HERE.glob('*.png'):
    check_path = Path(str(png) + '.check.json')
    check = json.loads(check_path.read_text())
    check['postIngestSha256'] = hashlib.sha256(png.read_bytes()).hexdigest()
    check_path.write_text(json.dumps(check, indent=2) + '\n')
    lines.append(png.name + ' magentaFrac=' + str(check.get('magentaFrac')))
    assert check.get('magentaFrac') == 0
exe = ROOT / 'apps/sundial/Build/sundial/Sundial.exe'
lines.append('Windows executable sha256=' + hashlib.sha256(exe.read_bytes()).hexdigest())
(HERE / 'verification.txt').write_text('\n'.join(lines) + '\n', encoding='utf-8')
print('\n'.join(lines))
