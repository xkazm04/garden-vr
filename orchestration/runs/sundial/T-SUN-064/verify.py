from pathlib import Path
import hashlib
import re
import xml.etree.ElementTree as ET

run = Path(__file__).resolve().parent
root = run.parents[3]
lines = []
for name in ['editmode.xml', 'playmode.xml', 'reflection-final.xml']:
    result = ET.parse(run / name).getroot()
    assert int(result.get('total')) > 0 and int(result.get('failed')) == 0
    lines.append(f"{name}: result={result.get('result')} total={result.get('total')} passed={result.get('passed')} failed={result.get('failed')}")
tests = {t.get('name'): t.get('result') for t in ET.parse(run / 'playmode.xml').iter('test-case')}
for name in ['Reflection_Stone_KeepsScriptedLine', 'Reflection_Speech_FakeClient_KeepsModelLine',
             'Reflection_DirtyLine_FallsBack', 'Journal_Delete_Empties', 'Save_HoldsNoTranscript',
             'Reflection_CancelAndDelete_RejectLateReply', 'Journal_WeekPage_ShowsOnlySelectedWeek', 'Journal_SevenLines_FitCard']:
    assert tests[name] == 'Passed'
    lines.append(name + ': Passed')
total = 0
for path in sorted((run / 'dotnet-results').glob('*.trx')):
    result = ET.parse(path).getroot()
    counters = result.find('.//{*}Counters')
    assert int(counters.get('failed')) == 0 and int(counters.get('total')) > 0
    total += int(counters.get('total'))
lines.append(f'.NET solution: {total} passed, 0 failed')
assert total == 543
for name in ['editmode-final.log', 'playmode-final.log', 'build-final.log', 'terrarium-compile.log']:
    log = (run / name).read_text(encoding='utf-8-sig', errors='replace')
    assert not re.search(r'error CS\d+|Shader error|BuildFailedException|Scripts have compiler errors', log)
    lines.append(name + ': 0 compiler/shader/build errors')
build = (run / 'build-final.log').read_text(encoding='utf-8-sig', errors='replace')
assert 'Build Succeeded' in build
lines.append(next(line for line in build.splitlines() if line.startswith('Build Succeeded')))
raw_input = re.compile(r'Keyboard\.current|Mouse\.current|UnityEngine\.Input\.|\bInput\.Get(?:Key|Button|Mouse)')
for path in (root / 'apps/sundial/Assets/Scripts').rglob('*.cs'):
    assert not raw_input.search(path.read_text(encoding='utf-8-sig'))
lines.append('App raw keyboard/mouse reads: 0')
copy = (run / 'strings.txt').read_text()
assert '\u2014' not in copy
assert not re.search(r'\bstreak\b|\bin a row\b|\banxiety\b|\btherapy\b|\bcure\b|\bheal\b', copy, re.I)
lines.append('String census: no em dash, consecutive-day count, or medical claim')
for path in sorted((root / 'apps/sundial/Build/sundial/Sundial_Data/Managed').glob('GardenVR*.dll')):
    lines.append(path.name + ' sha256=' + hashlib.sha256(path.read_bytes()).hexdigest())
for path in sorted(run.glob('DialG1-*.png')):
    lines.append(path.name + ' sha256=' + hashlib.sha256(path.read_bytes()).hexdigest())
text = '\n'.join(lines) + '\n'
(run / 'verification.txt').write_text(text)
print(text)
