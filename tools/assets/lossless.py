#!/usr/bin/env python3
"""Lossless PNG pass for Garden VR: recompress IDAT only, prove the pixels are identical.

  python tools/assets/lossless.py                dry run: measure, write docs/assets/lossless.json and LOSSLESS.md
  python tools/assets/lossless.py --apply        also write the files that clear the threshold into the working tree
  python tools/assets/lossless.py --ingest [paths...]
                                                 dry run for working-tree pngs not committed yet; add --apply to write
  python tools/assets/lossless.py --verify       re-decode every applied file at HEAD against its blob at measuredAt

Reads tracked blobs at --base (default: git merge-base HEAD main), never the working tree, so a dry run after an
apply gives the same ledger. --ingest reads the working tree and writes no ledger: a png that never reached history
costs no history to recompress, so it is written whenever the result is smaller at all. Only the IDAT data of a file changes: IHDR and every other chunk are copied byte for
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
# Files that snapshot a measurement at a past commit. A hash in one of them records the file as of its measuredAt
# commit and stays true when HEAD changes, so it does not pin a blob and rule (c) ignores a hit that comes only from
# them. docs/assets/lossless.json is not added: it records pixel hashes, not file hashes, and rule (c) never reads it.
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


def text_tokens(files, blobs):
    """hash -> tracked non-snapshot text files that record it (sha256 or blob id)."""
    tokens = {}
    for path, (oid, size) in files.items():
        if path in (LEDGER_JSON, LEDGER_MD) or path.startswith(SNAPSHOTS) or path.lower().endswith('.png') or size == 0: continue
        data = blobs.read(oid)
        if b'\0' in data[:8000]: continue
        for m in HEX.finditer(data): tokens.setdefault(m.group().lower().decode(), set()).add(path)
    return tokens


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
    tokens = text_tokens(files, blobs)
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
    return measure_data(path, blobs.read(files[path][0]))


def measure_data(path, data):
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


def net(e):
    """The HEAD saving exceeds the history growth: git keeps the old blob, so a rewrite adds newBytes to history."""
    return e['oldBytes'] - e['newBytes'] > e['newBytes']


def run(args):
    base = args.base or git('merge-base', 'HEAD', 'main').strip()
    base = git('rev-parse', base).strip()
    files = tree(base)
    blobs = Blobs()
    cand, why, recorded = exclusions(base, files, blobs)
    excluded = [dict(path=p, reasons=why[p], **({'recordedIn': recorded[p]} if p in recorded else {})) for p in cand if why[p]]
    entries, writes = [], {}
    for p in cand:
        if why[p]: continue
        e, data = measure(base, files, blobs, p)
        if 'reason' in e:
            pass
        elif data is None or not clears(e):
            e['reason'] = 'under threshold'
        elif not net(e):
            e['reason'] = 'history growth exceeds HEAD saving'
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
               excluded=excluded, files=entries)
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
         f"least {t['minPercent']} percent and {t['minBytes']} bytes and the saving at HEAD exceeds the new size (git keeps the old blob forever, so a rewrite adds newBytes to history and removes oldBytes minus newBytes from the checkout). "
         'Files not yet committed are handled by `--ingest`, which has no threshold and writes no ledger.', '',
         '## Totals', '', '| Measure | Value |', '| --- | ---: |',
         f'| candidate pngs | {n_cand} |', f'| excluded | {len(doc["excluded"])} |']
    for r in REASONS: L.append(f'| excluded, first reason {r} | {exc.get(r, 0)} |')
    L += [f'| measured | {len(fs)} |',
          f'| files that clear {t["minPercent"]} percent and {t["minBytes"]} bytes | {len(would)} |',
          f'| HEAD saving of those files | {mib(sum(map(saving, would)))} |',
          f'| history growth if those files were applied (sum of newBytes) | {mib(sum(e["newBytes"] for e in would))} |',
          f'| files that also clear the net-bytes test (saved > newBytes) | {sum(1 for e in would if net(e))} |',
          f'| potential saving, every smaller candidate with no threshold | {mib(sum(saving(e) for e in fs if saving(e) > 0))} in {sum(1 for e in fs if saving(e) > 0)} files |',
          f'| applied saving | {mib(sum(map(saving, app)))} in {len(app)} files |',
          f'| history growth (sum of newBytes over applied files) | {mib(sum(e["newBytes"] for e in app))} |',
          f'| proof failures | {len(failed)} |', '']
    if failed:
        L += ['## Proof failures', ''] + [f"- `{e['path']}`: {e['reason']}" for e in failed] + ['']
    L += ['', '## Excluded', '', 'A path with several reasons is counted under the first in the order of the table above.', '', '| Path | Reasons |', '| --- | --- |']
    L += [f"| `{x['path']}` | {', '.join(x['reasons'])} |" for x in doc['excluded']]
    L += ['', '## Files', '', '| Path | Old | New | Saved | Method | Applied | Reason |', '| --- | ---: | ---: | ---: | --- | --- | --- |']
    for e in sorted(fs, key=lambda e: (-saving(e), e['path'])):
        L.append(f"| `{e['path']}` | {e['oldBytes']} | {e['newBytes']} | {saving(e)} | {e['method']} | {'yes' if e['applied'] else 'no'} | {e.get('reason', '')} |")
    return '\n'.join(L) + '\n'


def ingest(args):
    """Dry run (or --apply) over working-tree pngs whose content is not committed yet. Writes no ledger."""
    if args.paths:
        paths = [Path(x).resolve().relative_to(ROOT).as_posix() for x in args.paths]
    else:
        out = git('status', '--porcelain', '-z', '--untracked-files=all', raw=True).decode('utf8').split('\0')
        paths, i = [], 0
        while i < len(out):
            rec = out[i]; i += 1
            if len(rec) < 4: continue
            if rec[0] in 'RC': i += 1
            if rec[3:].lower().endswith('.png') and 'D' not in rec[:2]: paths.append(rec[3:])
    base = git('rev-parse', 'HEAD').strip()
    files = tree(base)
    blobs = Blobs()
    tokens = text_tokens(files, blobs)
    blobs.close()
    old_total = new_total = failed = 0
    for p in sorted(set(paths)):
        f = ROOT / p
        if not f.is_file() or f.suffix.lower() != '.png':
            print(f'{p}  skipped: not a png file'); continue
        data = f.read_bytes()
        digest = hashlib.sha256(data).hexdigest()
        if tokens.get(digest):
            print(f'{p}  skipped: sha256 recorded in {", ".join(sorted(tokens[digest]))}'); continue
        if data[:8] != SIG:
            print(f'{p}  skipped: no png signature'); continue
        e, new = measure_data(p, data)
        if 'PROOF FAILED' in e.get('reason', '') or 'decode failed' in e.get('reason', ''):
            failed += 1
            print(f"{p}  {e['oldBytes']} -> {e['oldBytes']}  {e['method']}  proof FAIL: {e['reason']}  (file untouched)"); continue
        wrote = new is not None and e['newBytes'] < e['oldBytes']
        if wrote and args.apply: f.write_bytes(new)
        else: e['newBytes'] = e['oldBytes']
        old_total += e['oldBytes']; new_total += e['newBytes']
        state = ('written' if args.apply else 'would write') if wrote else 'no smaller candidate, unchanged'
        print(f"{p}  {e['oldBytes']} -> {e['newBytes']}  {e['method']}  proof ok (mode, size, palette, transparency, pixel sha256 {e['pixelSha256'][:12]})  {state}")
    print(f'total {old_total} -> {new_total}, saved {old_total - new_total} bytes; proof failures {failed}; {"applied" if args.apply else "dry run"}')
    return 1 if failed else 0


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
    ap.add_argument('--ingest', action='store_true', help='recompress working-tree pngs not committed yet (dry run unless --apply)')
    ap.add_argument('paths', nargs='*', help='with --ingest: pngs to process (default: untracked or modified pngs from git status)')
    a = ap.parse_args()
    sys.exit(verify() if a.verify else ingest(a) if a.ingest else run(a) or 0)
