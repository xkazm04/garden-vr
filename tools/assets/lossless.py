#!/usr/bin/env python3
"""Lossless PNG pass for Garden VR: recompress IDAT only, prove the pixels are identical.

  python tools/assets/lossless.py                dry run: measure, write docs/assets/lossless.json and LOSSLESS.md
  python tools/assets/lossless.py --apply        also write the files that clear the threshold into the working tree
  python tools/assets/lossless.py --verify       re-decode every applied file at HEAD against its blob at measuredAt

Reads tracked blobs at --base (default: git merge-base HEAD main), never the working tree, so a dry run after an
apply gives the same ledger. Only the IDAT data of a file changes: IHDR and every other chunk are copied byte for
byte in their original order. Needs Python 3.12, Pillow and numpy; installs nothing.
"""
import argparse, hashlib, io, json, re, struct, subprocess, sys, zlib
from collections import Counter
from pathlib import Path

import numpy as np  # noqa: F401  (required by the brief; Pillow's tobytes needs no numpy, kept as an environment check)
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
LEDGER_JSON = 'docs/assets/lossless.json'
LEDGER_MD = 'docs/assets/LOSSLESS.md'
SIG = b'\x89PNG\r\n\x1a\n'
MIN_PERCENT = 10
MIN_BYTES = 65536
ROOTS = ('apps/sundial/Assets/', 'apps/terrarium/Assets/', 'apps/sundial/Art/', 'apps/terrarium/Art/')
CHERRY_PICK_SHAS = ('00b92d2', 'f3340bf', '146a176', 'e8e7e6c')
CHERRY_PICK_DIRS = ('apps/sundial/Assets/Resources/LeafPlant/', 'apps/sundial/Art/Source/plants/leafplant/')
WORKTREES = ('C:/Users/kazda/kiro/gvr-terrarium', 'C:/Users/kazda/kiro/gvr-sundial')
# Files that snapshot a measurement at a past commit. A blob id in one of them is history, not a lock, so a png held
# back only by these is reported as held back with its potential saving, and never applied.
SNAPSHOTS = ('docs/assets/baseline.json', 'docs/health/')
REASONS = ('duplicate-group', 'cherry-pick-path', 'recorded-hash', 'dirty-in-worktree')
HEX = re.compile(rb'(?<![0-9a-fA-F])(?:[0-9a-fA-F]{64}|[0-9a-fA-F]{40})(?![0-9a-fA-F])')


def git(*args, cwd=ROOT, raw=False):
    out = subprocess.run(['git', *args], cwd=cwd, capture_output=True, check=True).stdout
    return out if raw else out.decode('utf8')


class Blobs:
    """One long-lived git cat-file --batch process."""
    def __init__(self):
        self.p = subprocess.Popen(['git', 'cat-file', '--batch'], cwd=ROOT, stdin=subprocess.PIPE, stdout=subprocess.PIPE)

    def read(self, oid):
        self.p.stdin.write(oid.encode() + b'\n'); self.p.stdin.flush()
        head = self.p.stdout.readline().split()
        if len(head) < 3: raise KeyError(oid)
        data = self.p.stdout.read(int(head[2])); self.p.stdout.read(1)
        return data

    def close(self):
        self.p.stdin.close(); self.p.wait()


def tree(rev):
    out = git('ls-tree', '-r', '-z', '-l', rev, raw=True).decode('utf8', 'surrogateescape')
    files = {}
    for rec in out.split('\0'):
        if not rec: continue
        meta, path = rec.split('\t', 1)
        _, _, oid, size = meta.split()
        files[path] = (oid, int(size) if size != '-' else 0)
    return files


def chunks(data):
    """(list of (type, data) in order, trailing bytes after IEND). Raises ValueError when not a well-formed PNG."""
    if data[:8] != SIG: raise ValueError('no signature')
    pos, out = 8, []
    while pos + 12 <= len(data):
        n, = struct.unpack('>I', data[pos:pos + 4]); typ = data[pos + 4:pos + 8]
        body = data[pos + 8:pos + 8 + n]
        if len(body) != n: raise ValueError('truncated')
        if zlib.crc32(typ + body) != struct.unpack('>I', data[pos + 8 + n:pos + 12 + n])[0]: raise ValueError('bad crc')
        out.append((typ, body)); pos += 12 + n
        if typ == b'IEND': return out, data[pos:]
    raise ValueError('no IEND')


def chunk(typ, body):
    return struct.pack('>I', len(body)) + typ + body + struct.pack('>I', zlib.crc32(typ + body))


def assemble(chs, trailing, idat):
    out, placed = [SIG], False
    for typ, body in chs:
        if typ == b'IDAT':
            if not placed: out.append(chunk(b'IDAT', idat)); placed = True
            continue
        out.append(chunk(typ, body))
    out.append(trailing)
    return b''.join(out)


def deflate(raw, strategy):
    c = zlib.compressobj(9, zlib.DEFLATED, 15, 9, strategy)
    return c.compress(raw) + c.flush()


def decode(data):
    im = Image.open(io.BytesIO(data)); im.load()
    pal = im.getpalette() if im.mode == 'P' else None
    return (im.mode, im.size, pal, repr(im.info.get('transparency')), hashlib.sha256(im.tobytes()).hexdigest())


def candidates(data, chs):
    """Yield (method, new file bytes) for each candidate IDAT."""
    idat = b''.join(b for t, b in chs if t == b'IDAT')
    trailing = chunks(data)[1]
    raw = zlib.decompress(idat)
    for name, strat in (('zlib9-default', zlib.Z_DEFAULT_STRATEGY), ('zlib9-filtered', zlib.Z_FILTERED)):
        yield name, assemble(chs, trailing, deflate(raw, strat))
    im = Image.open(io.BytesIO(data)); im.load()
    buf = io.BytesIO()
    kw = {'optimize': True}
    if 'transparency' in im.info: kw['transparency'] = im.info['transparency']
    try:
        im.save(buf, 'PNG', **kw)
        pchs, _ = chunks(buf.getvalue())
    except Exception:
        return
    old = {t: b for t, b in chs if t in (b'IHDR', b'PLTE', b'tRNS')}
    new = {t: b for t, b in pchs if t in (b'IHDR', b'PLTE', b'tRNS')}
    if new.get(b'IHDR') != old[b'IHDR']: return
    if im.mode == 'P' and (new.get(b'PLTE') != old.get(b'PLTE') or new.get(b'tRNS') != old.get(b'tRNS')): return
    yield 'pillow-optimize', assemble(chs, trailing, b''.join(b for t, b in pchs if t == b'IDAT'))


def exclusions(base, files, blobs):
    """(candidates, path -> list of reasons, path -> files that record its hash)."""
    cand = sorted(p for p in files if p.lower().endswith('.png') and p.startswith(ROOTS) and blobs.read(files[p][0])[:8] == SIG)
    why = {p: [] for p in cand}
    # (a) duplicates.json groups, keyed by blob id
    doc = json.loads(blobs.read(f'{base}:docs/assets/duplicates.json'))
    dup_blobs = {g['blob'] for g in doc['groups']}
    dup_paths = {x for g in doc['groups'] for x in g['paths']}
    for p in cand:
        if files[p][0] in dup_blobs or p in dup_paths: why[p].append(REASONS[0])
    # (b) pngs of the cherry-picked agent commits, and the leafplant folders
    picked = set()
    for sha in CHERRY_PICK_SHAS:
        picked.update(x for x in git('show', '--name-only', '--format=', '-z', sha, raw=True).decode('utf8').split('\0') if x.lower().endswith('.png'))
    for p in cand:
        if p in picked or p.startswith(CHERRY_PICK_DIRS): why[p].append(REASONS[1])
    # (c) a sha256 or blob id that appears in tracked text; collected once
    tokens = {}
    for path, (oid, size) in files.items():
        if path in (LEDGER_JSON, LEDGER_MD) or path.lower().endswith('.png') or size == 0: continue
        data = blobs.read(oid)
        if b'\0' in data[:8000]: continue
        for m in HEX.finditer(data): tokens.setdefault(m.group().lower().decode(), set()).add(path)
    recorded = {}
    for p in cand:
        data = blobs.read(files[p][0])
        hit = tokens.get(files[p][0], set()) | tokens.get(hashlib.sha256(data).hexdigest(), set())
        if hit: why[p].append(REASONS[2]); recorded[p] = sorted(hit)
    # (d) dirty or untracked in the two sibling worktrees (read only)
    dirty = set()
    for w in WORKTREES:
        out = git('status', '--porcelain', '-z', '--untracked-files=all', cwd=w, raw=True).decode('utf8').split('\0')
        i = 0
        while i < len(out):
            rec = out[i]; i += 1
            if len(rec) < 4: continue
            dirty.add(rec[3:])
            if rec[0] in 'RC': i += 1
    for p in cand:
        if p in dirty: why[p].append(REASONS[3])
    return cand, why, recorded


def measure(base, files, blobs, path):
    data = blobs.read(files[path][0])
    old = len(data)
    chs, _ = chunks(data)
    try:
        ref = decode(data)
    except Exception as e:
        return dict(path=path, oldBytes=old, newBytes=old, pixelSha256=None, method='none', applied=False, reason=f'decode failed: {e}'), None
    best, best_data = ('none', old), None
    for name, new in candidates(data, chs):
        if len(new) >= best[1]: continue
        try:
            got = decode(new)
        except Exception as e:
            return dict(path=path, oldBytes=old, newBytes=old, pixelSha256=ref[4], method=name, applied=False, reason=f'PROOF FAILED: new file does not decode: {e}'), None
        if got != ref:
            return dict(path=path, oldBytes=old, newBytes=old, pixelSha256=ref[4], method=name, applied=False, reason='PROOF FAILED: decoded mode, size, palette, transparency or pixels differ'), None
        best, best_data = (name, len(new)), new
    return dict(path=path, oldBytes=old, newBytes=best[1], pixelSha256=ref[4], method=best[0], applied=False), best_data


def clears(e):
    s = e['oldBytes'] - e['newBytes']
    return s > 0 and s * 100 >= MIN_PERCENT * e['oldBytes'] and s >= MIN_BYTES


def run(args):
    base = args.base or git('merge-base', 'HEAD', 'main').strip()
    base = git('rev-parse', base).strip()
    files = tree(base)
    blobs = Blobs()
    cand, why, recorded = exclusions(base, files, blobs)
    excluded = [dict(path=p, reasons=why[p], **({'recordedIn': recorded[p]} if p in recorded else {})) for p in cand if why[p]]
    entries, writes, held = [], {}, []
    for p in cand:
        if why[p] == [REASONS[2]] and all(f.startswith(SNAPSHOTS) for f in recorded[p]):
            e, _ = measure(base, files, blobs, p)
            e['recordedIn'] = recorded[p]; held.append(e)
    for p in cand:
        if why[p]: continue
        e, data = measure(base, files, blobs, p)
        if 'reason' in e:
            pass
        elif data is None:
            e['reason'] = 'no candidate is smaller'
        elif not clears(e):
            e['reason'] = f'saves under {MIN_PERCENT} percent or under {MIN_BYTES} bytes'
        elif args.apply:
            writes[p] = data; e['applied'] = True
        else:
            e['reason'] = 'dry run (would apply)'
        entries.append(e)
        print(f"{e['method']:16} {e['oldBytes']:>9} -> {e['newBytes']:>9}  {p}", file=sys.stderr)
    blobs.close()
    for p, data in writes.items(): (ROOT / p).write_bytes(data)
    doc = dict(schema=1, measuredAt=base,
               thresholds=dict(minPercent=MIN_PERCENT, minBytes=MIN_BYTES),
               excluded=excluded, heldBack=held, files=entries)
    (ROOT / LEDGER_JSON).write_text(json.dumps(doc, indent=2) + '\n', encoding='utf8')
    (ROOT / LEDGER_MD).write_text(render(doc, len(cand), args.apply), encoding='utf8')
    print(f'wrote {LEDGER_JSON} and {LEDGER_MD}; {len(writes)} file(s) written', file=sys.stderr)


def mib(n): return f'{n} ({n / 1048576:.2f} MiB)'


def render(doc, n_cand, applied_run):
    fs = doc['files']
    saving = lambda e: e['oldBytes'] - e['newBytes']
    would = [e for e in fs if clears(e) and e['pixelSha256']]
    app = [e for e in fs if e['applied']]
    exc = Counter(r for x in doc['excluded'] for r in x['reasons'][:1])
    failed = [e for e in fs if 'PROOF FAILED' in e.get('reason', '') or 'decode failed' in e.get('reason', '')]
    t = doc['thresholds']
    L = ['# Lossless PNG pass', '',
         f"Generated by `python tools/assets/lossless.py{' --apply' if applied_run else ''}` from `docs/assets/lossless.json`, measured at `{doc['measuredAt']}`. Do not edit by hand; rerun the tool.", '',
         'Only the IDAT data of a PNG changes. IHDR and every other chunk are copied byte for byte. A file is written only when the decoded mode, size, palette, transparency and the sha256 of the pixels are identical, and it saves at '
         f"least {t['minPercent']} percent and {t['minBytes']} bytes (git keeps the old blob forever, so a small saving at HEAD costs more history than it saves).", '',
         '## Totals', '', '| Measure | Value |', '| --- | ---: |',
         f'| candidate pngs | {n_cand} |', f'| excluded | {len(doc["excluded"])} |']
    for r in REASONS: L.append(f'| excluded, first reason {r} | {exc.get(r, 0)} |')
    L += [f'| measured | {len(fs)} |',
          f'| potential saving, every file that clears the threshold | {mib(sum(map(saving, would)))} in {len(would)} files |',
          f'| potential saving, every smaller candidate with no threshold | {mib(sum(saving(e) for e in fs if saving(e) > 0))} in {sum(1 for e in fs if saving(e) > 0)} files |',
          f'| applied saving | {mib(sum(map(saving, app)))} in {len(app)} files |',
          f'| history growth (sum of newBytes over applied files) | {mib(sum(e["newBytes"] for e in app))} |',
          f'| proof failures | {len(failed)} |', '']
    if failed:
        L += ['## Proof failures', ''] + [f"- `{e['path']}`: {e['reason']}" for e in failed] + ['']
    hb = doc['heldBack']
    L += ['## Held back by snapshot files only', '',
          f"{len(hb)} excluded pngs are named by the recorded-hash rule only through measurement snapshots ({', '.join(f'`{x}`' for x in SNAPSHOTS)}), which list the blob id of nearly every tracked file at a past commit. "
          'They are excluded, as the brief says, and measured here so the owner can see the trade. Nothing in this table is applied.', '',
          f"Potential if released: {mib(sum(map(saving, [e for e in hb if clears(e)])))} in {sum(1 for e in hb if clears(e))} files that clear the threshold; "
          f"{mib(sum(max(0, saving(e)) for e in hb))} with no threshold.", '',
          '| Path | Old | New | Saved | Method |', '| --- | ---: | ---: | ---: | --- |']
    L += [f"| `{e['path']}` | {e['oldBytes']} | {e['newBytes']} | {saving(e)} | {e['method']} |" for e in sorted(hb, key=lambda e: (-saving(e), e['path'])) if saving(e) > 0]
    L += ['', '## Excluded', '', 'A path with several reasons is counted under the first in the order of the table above.', '', '| Path | Reasons |', '| --- | --- |']
    L += [f"| `{x['path']}` | {', '.join(x['reasons'])} |" for x in doc['excluded']]
    L += ['', '## Files', '', '| Path | Old | New | Saved | Method | Applied | Reason |', '| --- | ---: | ---: | ---: | --- | --- | --- |']
    for e in sorted(fs, key=lambda e: (-saving(e), e['path'])):
        L.append(f"| `{e['path']}` | {e['oldBytes']} | {e['newBytes']} | {saving(e)} | {e['method']} | {'yes' if e['applied'] else 'no'} | {e.get('reason', '')} |")
    return '\n'.join(L) + '\n'


def verify():
    doc = json.loads((ROOT / LEDGER_JSON).read_text(encoding='utf8'))
    base = doc['measuredAt']
    files, head = tree(base), tree('HEAD')
    blobs, bad, n = Blobs(), [], 0
    for e in doc['files']:
        if not e['applied']: continue
        n += 1
        p = e['path']
        try:
            a, b = decode(blobs.read(files[p][0])), decode(blobs.read(head[p][0]))
            if a != b or a[4] != e['pixelSha256'] or head[p][1] != e['newBytes']: bad.append(p)
        except Exception as ex:
            bad.append(f'{p}: {ex}')
    blobs.close()
    print(f'verified {n} applied file(s) at HEAD against {base[:7]}: {len(bad)} mismatches')
    for p in bad: print('  MISMATCH', p)
    return 1 if bad else 0


if __name__ == '__main__':
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('--apply', action='store_true', help='write the files that clear the threshold')
    ap.add_argument('--verify', action='store_true', help='check applied files at HEAD against the start sha')
    ap.add_argument('--base', help='commit whose blobs are measured (default: merge-base HEAD main)')
    a = ap.parse_args()
    sys.exit(verify() if a.verify else run(a) or 0)
