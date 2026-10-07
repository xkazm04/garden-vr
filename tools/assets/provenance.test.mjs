// node --test tools/assets/provenance.test.mjs
import test from 'node:test';
import assert from 'node:assert/strict';
import { parseRows, parseSidecar, readProvenance, totals } from './provenance.mjs';

const D = 'apps/x/Assets/Art/Textures';
const md = [
  '| File | Origin | Source | Status |', '|---|---|---|---|',
  '| `s5_steam_b.png` | generated | a | b |',
  '| moss.png.bak | retired | a | b |',
  '| plant_{a,b}.png | generated | a | b |',
  '| soil.png | painted-by-code | a \\| b | c |',
].join('\n');

test('a name that is a prefix of another row does not count as covered', () => {
  const tracked = new Set([`${D}/PROVENANCE.md`, `${D}/s5_steam.png`, `${D}/moss.png`, `${D}/plant_a.png`, `${D}/soil.png`]);
  const recs = readProvenance(tracked, () => md);
  const by = Object.fromEntries(recs.map(r => [r.name, r.status]));
  assert.deepEqual(by, { 's5_steam.png': 'missing', 'moss.png': 'missing', 'plant_a.png': 'missing', 'soil.png': 'row' });
  assert.deepEqual(totals(recs), { pngs: 4, sidecar: 0, row: 1, missing: 3, derived: 0 });
});

test('a sidecar wins, reads method and the derived flag', () => {
  const tracked = new Set([`${D}/a.png`, `${D}/a.png.provenance.txt`]);
  const recs = readProvenance(tracked, () => 'asset: a.png\nmethod: s.py\nderived_from_art_reference: yes\n');
  assert.deepEqual(recs.map(r => [r.status, r.origin, r.derived]), [['sidecar', 's.py', true]]);
});

test('row cells: backticks stripped, escaped pipe kept, derived wording', () => {
  assert.equal(parseRows('| `a.png` | x \\| y |').get('a.png').origin, 'x | y');
  assert.equal(parseRows('| a.png | derived from the art reference |').get('a.png').derived, true);
  assert.equal(parseRows('| a.png | not derived from the art reference |').get('a.png').derived, false);
  assert.equal(parseSidecar('derived_from_art_reference: no').derived, false);
});
