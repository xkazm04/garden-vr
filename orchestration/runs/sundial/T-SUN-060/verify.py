"""Summarize completed B3 runs without copying raw logs into Git."""
from pathlib import Path
import hashlib
import json
import re
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[4]
run = Path(__file__).resolve().parent
lines = []
for name in ('editmode', 'playmode', 'speech-playmode'):
    result = ET.parse(run / (name + '.xml')).getroot().attrib
    assert int(result['total']) > 0 and int(result['failed']) == 0
    lines.append(name + ' ' + ' '.join(f'{key}={result[key]}' for key in ('result', 'total', 'passed', 'failed', 'skipped')))
for name in ('editmode', 'playmode', 'build', 'build-development', 'terrarium-compile'):
    log = (run / (name + '.log')).read_text(encoding='utf-8-sig', errors='replace')
    errors = re.findall(r'error CS\d+|Shader error in', log)
    assert not errors, (name, errors)
    lines.append(f'{name} compiler/shader error entries={len(errors)}')
    if name.startswith('build'):
        assert 'Build Succeeded ' in log
    if name == 'terrarium-compile':
        assert 'Exiting batchmode successfully now!' in log
speech = (run / 'player-speech.log').read_text(encoding='utf-8-sig', errors='replace')
assert 'HRESULT=0x80045509' in speech
assert 'Available=False Listening=False Finals=0' in speech
assert 'Transient text cleared=True' in speech
assert 'ArgumentNullException' not in speech
lines.extend(line for line in speech.splitlines() if line.startswith('[Speech') or line.startswith('ERROR:'))
pattern = re.compile(r'Keyboard\.current|Mouse\.current|UnityEngine\.Input\.|\bInput\.Get(?:Key|Button|Mouse)')
hits = [str(path.relative_to(root)) for path in (root / 'apps/sundial/Assets/Scripts').rglob('*.cs') if pattern.search(path.read_text(encoding='utf-8-sig'))]
assert not hits, hits
lines.append('App raw keyboard/mouse reads=0')
for flavor in ('sundial', 'sundial-development'):
    dll = root / f'apps/sundial/Build/{flavor}/Sundial_Data/Managed/GardenVR.Sundial.dll'
    data = dll.read_bytes()
    harness = b'SpeechPlayerCheck' in data
    assert harness == (flavor == 'sundial-development')
    lines.append(f'{flavor} SpeechPlayerCheck metadata present={harness}; GardenVR.Sundial.dll sha256={hashlib.sha256(data).hexdigest()}')
lines.append('User-facing strings added: listening')
(run / 'verification.txt').write_text('\n'.join(lines) + '\n', encoding='utf-8')
print('\n'.join(lines))
