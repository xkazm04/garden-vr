// A4 provenance census, the pure part: no git, no file system. census.mjs feeds it the blobs tracked at one commit.
//
// Every .png under apps/<app>/Assets/Art/Textures/ gets exactly one status, the first that applies:
//   sidecar  a tracked <name>.png.provenance.txt beside it; its `method:` line is the declared origin
//   row      a row of the PROVENANCE.md in the same folder whose first cell, backticks stripped, EQUALS the file name;
//            the declared origin is the second cell (Origin in the four column table, Note in the two column one)
//   missing  neither
// The match is on the whole file name, so a name that is a prefix of another row's name ("s5_steam.png" against
// "s5_steam_b.png") does not count as covered. Brace rows ("plant_{a,b}.png") name no single file and never match.
// This reads declarations, not pixels.
import path from 'node:path';

export const TEXTURE_DIR = /^apps\/([^/]+)\/Assets\/Art\/Textures\//;
// A row that declares a texture derived from the art reference, and not the usual "not sampled from the reference frames".
const ROW_DERIVED = /(?<!not )(?<!not sampled )\bderived from the art reference\b/i;

export function splitRow(line) {
  const t = line.trim();
  if (!t.startsWith('|')) return null;
  const cells = [];
  let cur = '';
  for (let i = 1; i < t.length; i++) {
    if (t[i] === '\\' && t[i + 1] === '|') { cur += '|'; i++; } else if (t[i] === '|') { cells.push(cur.trim()); cur = ''; } else cur += t[i];
  }
  if (cur.trim()) cells.push(cur.trim());
  return cells;
}

// name -> { origin, derived } for every table row whose first cell is a file name.
export function parseRows(markdown) {
  const rows = new Map();
  for (const line of markdown.split(/\r?\n/)) {
    const cells = splitRow(line);
    if (!cells || cells.length < 2) continue;
    const name = cells[0].replace(/`/g, '').trim();
    if (!name || /^-+:?$/.test(name) || rows.has(name)) continue;
    rows.set(name, { origin: cells[1], derived: ROW_DERIVED.test(cells.slice(1).join(' | ')) });
  }
  return rows;
}

export function parseSidecar(text) {
  const field = k => ((text.match(new RegExp(`^${k}:[ \t]*(.*)$`, 'mi')) || [])[1] || '').trim();
  return { origin: field('method'), derived: /^yes\b/i.test(field('derived_from_art_reference')) };
}

// tracked: Set of tracked paths; read: path -> text. Returns one record per texture png, sorted by path.
export function readProvenance(tracked, read) {
  const out = [];
  const rowCache = new Map();
  for (const p of [...tracked].sort()) {
    const m = p.match(TEXTURE_DIR);
    if (!m || path.posix.extname(p).toLowerCase() !== '.png') continue;
    const dir = path.posix.dirname(p), name = path.posix.basename(p);
    const rec = { path: p, app: m[1], name };
    const sidecar = `${p}.provenance.txt`;
    if (tracked.has(sidecar)) {
      Object.assign(rec, { status: 'sidecar', ...parseSidecar(read(sidecar)) });
    } else {
      const doc = `${dir}/PROVENANCE.md`;
      if (!rowCache.has(dir)) rowCache.set(dir, tracked.has(doc) ? parseRows(read(doc)) : new Map());
      const row = rowCache.get(dir).get(name);
      Object.assign(rec, row ? { status: 'row', ...row } : { status: 'missing', origin: '', derived: false });
    }
    out.push(rec);
  }
  return out;
}

export function totals(records) {
  const t = { pngs: 0, sidecar: 0, row: 0, missing: 0, derived: 0 };
  for (const r of records) { t.pngs++; t[r.status]++; if (r.derived) t.derived++; }
  return t;
}
