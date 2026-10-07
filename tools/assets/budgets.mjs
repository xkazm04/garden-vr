// Texture memory against the milestone 4 budgets: the reading at one commit and its markdown. census.mjs runs it for
// `--only texmem`; the size and byte rules live in texmem.mjs. Git access and the census result (for each image's
// reference state) are passed in by census.mjs.
import path from 'node:path';
import { parseImporter, textureMemory, tgaHeader, BLOCK, BLOCK_BYTES } from './texmem.mjs';

const TEX_EXT = new Set(['.png', '.jpg', '.jpeg', '.tga']);
const SHIPPED = new Set(['referenced', 'resources']);
const MIB = 1048576;
const CAP_PX = 1024; // PLAN A5: textures <= 1024 px
const ext = p => path.posix.extname(p).toLowerCase();
const byPath = (a, b) => (a.path < b.path ? -1 : a.path > b.path ? 1 : 0);
const byBytes = (a, b) => b.bytes - a.bytes || byPath(a, b);
const sum = list => list.reduce((s, x) => s + x.bytes, 0);

// census: the census() result at the same commit (images carry width, height and unity.state).
export function texmemReading(census, git, readBlobs) {
  const sha = census.commit.sha;
  const listed = git(['ls-tree', '-r', '-z', sha]).toString('utf8').split('\0').filter(Boolean).map(rec => {
    const tab = rec.indexOf('\t');
    const [, type, blob] = rec.slice(0, tab).split(' ');
    return { type, blob, path: rec.slice(tab + 1) };
  }).filter(e => e.type === 'blob');
  const tracked = new Map(listed.map(e => [e.path, e]));
  const textures = listed.filter(e => TEX_EXT.has(ext(e.path)) && /^(apps\/[^/]+\/Assets|shared\/packages)\//.test(e.path) && tracked.has(`${e.path}.meta`));
  const apps = [...new Set(listed.map(e => (e.path.match(/^apps\/([^/]+)\/Assets\//) || [])[1]).filter(Boolean))].sort();
  const wanted = [
    ...textures.map(e => tracked.get(`${e.path}.meta`)),
    ...textures.filter(e => ext(e.path) === '.tga'),
    ...apps.map(app => tracked.get(`docs/budgets/${app}.json`)).filter(Boolean),
    ...apps.map(app => tracked.get(`apps/${app}/Packages/manifest.json`)).filter(Boolean),
  ];
  const blobs = readBlobs(wanted.map(e => e.blob));
  const blobOf = p => blobs.get(tracked.get(p).blob);
  const images = new Map(census.images.map(r => [r.path, r]));

  const records = [];
  for (const e of textures) {
    const imp = parseImporter(blobOf(`${e.path}.meta`).toString('utf8'));
    if (imp.importer !== 'TextureImporter') continue;
    const img = images.get(e.path) || {};
    const head = ext(e.path) === '.tga' ? tgaHeader(blobOf(e.path)) : img;
    const app = (e.path.match(/^apps\/([^/]+)\//) || [])[1] || null;
    const pkg = app ? null : e.path.split('/')[2];
    const rec = { path: e.path, app, pkg, state: img.unity ? img.unity.state : null };
    if (!head.width || !head.height) { records.push({ ...rec, error: head.error || 'dimensions not read', bytes: 0 }); continue; }
    records.push({ ...rec, ...textureMemory({ width: head.width, height: head.height }, imp) });
  }
  records.sort(byPath);

  const perApp = apps.map(app => {
    const own = records.filter(r => r.app === app);
    const shipped = own.filter(r => SHIPPED.has(r.state) && !r.error);
    const noRef = own.filter(r => r.state === 'no-reference-found' && !r.error);
    const manifestPath = `apps/${app}/Packages/manifest.json`;
    const deps = tracked.has(manifestPath) ? Object.keys(JSON.parse(blobOf(manifestPath).toString('utf8')).dependencies || {}) : [];
    // A texture under shared/packages ships with this app when its package is in the manifest. Its reference state is not
    // read (the census reads states for apps/*/Assets only), so it is listed with its size and not counted.
    const fromPackages = records.filter(r => r.pkg && deps.includes(r.pkg));
    const budgetPath = `docs/budgets/${app}.json`;
    let ceiling = null;
    if (tracked.has(budgetPath)) {
      const tm = JSON.parse(blobOf(budgetPath).toString('utf8')).textureMemory || {};
      ceiling = { path: budgetPath, hardBytes: tm.hardBytes ?? null, hardMiB: tm.hardMiB ?? null, softMiB: tm.softMiB ?? null };
    }
    const total = sum(shipped);
    const verdict = !ceiling || ceiling.hardBytes == null ? 'no ceiling' : total <= ceiling.hardBytes ? 'pass' : 'fail';
    const soft = ceiling && ceiling.softMiB != null ? (total <= ceiling.softMiB * MIB ? 'under soft cap' : 'over soft cap') : null;
    return {
      app, assetsTree: git(['rev-parse', `${sha}:apps/${app}/Assets`]).toString().trim(),
      textures: own.length, shipped: { count: shipped.length, bytes: total }, noReferenceFound: { count: noRef.length, bytes: sum(noRef) },
      unread: own.filter(r => r.error).map(r => r.path),
      fromPackages: fromPackages.map(r => ({ path: r.path, pkg: r.pkg, bytes: r.bytes, counted: false })),
      ceiling, verdict, soft,
      largest: [...shipped].sort(byBytes).slice(0, 5).map(r => ({ path: r.path, bytes: r.bytes, imported: r.imported })),
      otherFormat: own.filter(r => r.androidOverrideFormat).map(r => ({ path: r.path, format: r.androidOverrideFormat, bytes: r.bytes, ownFormatBytes: r.ownFormatBytes, state: r.state })),
      overCap: own.filter(r => r.imported && Math.max(...r.imported) > CAP_PX).map(r => ({ path: r.path, imported: r.imported, state: r.state })),
      uncompressed: own.filter(r => !r.androidOverrideFormat && r.androidFormat).map(r => ({ path: r.path, bytes: r.bytes, ownFormatBytes: r.ownFormatBytes, state: r.state })),
    };
  });
  let budgetsTree = null;
  try { budgetsTree = git(['rev-parse', `${sha}:docs/budgets`]).toString().trim(); } catch { budgetsTree = null; }
  return { schema: 'garden-vr/texture-memory/1', sha, subject: census.commit.subject, budgetsTree, apps: perApp, records };
}

const mibs = b => (b / MIB).toFixed(2);

export function texmemMarkdown(r) {
  const row = cells => `| ${cells.join(' | ')} |`;
  const dim = d => (d ? `${d[0]}x${d[1]}` : '-');
  const out = [];
  out.push('# Texture memory against the milestone 4 budgets', '');
  out.push(`Generated by \`node tools/assets/census.mjs --only texmem --rev ${r.sha}\`. Do not edit by hand; rerun the tool.`, '');
  out.push(`Commit read: \`${r.sha}\` (${r.subject}).`, '');
  out.push('Trees read, by git tree id, so the reading stays checkable after a rebase that leaves them alone (`git rev-parse <rev>:<folder>`):', '');
  for (const a of r.apps) out.push(`- \`apps/${a.app}/Assets\`: \`${a.assetsTree}\``);
  out.push(`- \`docs/budgets\`: ${r.budgetsTree ? `\`${r.budgetsTree}\`` : 'not tracked at this commit'}`, '');
  out.push('**Nothing here was measured on a headset or by Unity.** It is arithmetic over the tracked source images and their `.meta` files. H2 (2026-11-03) is the first headset reading.', '');
  out.push('## Rules', '');
  out.push('- Scope: every png, jpg or tga under `apps/<app>/Assets` whose `.meta` is a TextureImporter.');
  out.push('- Imported size: first, the meta\'s `nPOTScale` is applied to a non power of two source, per side (1 nearest, a tie going up; 2 larger; 3 smaller). Unity ignores it for Sprite and GUI textures. Then the result is scaled down, aspect kept, so its longer side fits the effective max size. The shorter side is rounded to the nearest pixel, and is at least 1.');
  out.push('- Effective max size: the Android entry when it is overridden, else the Default platform entry, else the legacy top-level `maxTextureSize`.');
  out.push(`- GPU bytes: ASTC ${BLOCK}x${BLOCK}, ceil(w/${BLOCK}) x ceil(h/${BLOCK}) x ${BLOCK_BYTES} bytes per mip level. When the meta enables mipmaps, the levels are summed down to 1x1, each side halving (floored, at least 1).`);
  out.push('- **Counted** is the per-app total. It holds only textures whose census state is `referenced` (a material, scene, prefab or other asset names its guid) or `resources` (under a Resources folder). Textures with state `no-reference-found` are listed with their own total and are not counted: code can still load them by a name the static scan cannot see, so they are not proved shipped, and not proved unused.');
  out.push('- A texture under `shared/packages/<package>` ships with an app when that package is in the app\'s `Packages/manifest.json`. The census reads no reference state for those, so each is listed with its size as not counted.');
  out.push('- Pass or fail compares the counted total with `textureMemory.hardBytes` in `docs/budgets/<app>.json` at the same commit. The soft cap is `textureMemory.softMiB`.', '');
  out.push('## Totals', '');
  out.push(row(['App', 'Counted (referenced + resources)', 'no-reference-found (not counted)', 'Ceiling', 'Verdict', 'Soft cap']), row(['---', '---:', '---:', '---:', '---', '---']));
  for (const a of r.apps) {
    out.push(row([a.app, `${a.shipped.count} textures, ${a.shipped.bytes} bytes (${mibs(a.shipped.bytes)} MiB)`,
      `${a.noReferenceFound.count} textures, ${a.noReferenceFound.bytes} bytes (${mibs(a.noReferenceFound.bytes)} MiB)`,
      a.ceiling && a.ceiling.hardBytes != null ? `${a.ceiling.hardBytes} bytes (${a.ceiling.hardMiB} MiB)` : 'none at this commit',
      `**${a.verdict}**`, a.soft || '-']));
  }
  out.push('');
  for (const a of r.apps) {
    out.push(`## ${a.app}`, '');
    out.push(`Five largest counted textures by GPU bytes:`, '');
    out.push(row(['Texture', 'Imported', 'Bytes']), row(['---', '---', '---:']));
    for (const x of a.largest) out.push(row([`\`${x.path}\``, dim(x.imported), x.bytes]));
    out.push('');
    out.push(`Android override with a format other than ASTC 6x6 or Automatic: ${a.otherFormat.length ? '' : 'none.'}`, '');
    if (a.otherFormat.length) {
      out.push(row(['Texture', 'State', 'Format', 'Bytes at ASTC 6x6 (counted)', 'Bytes at its own format']), row(['---', '---', '---', '---:', '---:']));
      for (const x of a.otherFormat) out.push(row([`\`${x.path}\``, x.state, x.format, x.bytes, x.ownFormatBytes ?? 'not computed']));
      out.push('');
    }
    out.push(`Automatic format with compression off on Android (no Android override, and the Default entry says \`textureCompression: 0\`). Unity imports these uncompressed. They are counted at ASTC 6x6 like every other texture, and an upper figure at 4 bytes per pixel (RGBA32; a texture without alpha can import as RGB24) is given beside them: ${a.uncompressed.length ? `${a.uncompressed.length} textures, ${sum(a.uncompressed)} bytes at ASTC 6x6, ${a.uncompressed.reduce((s, x) => s + x.ownFormatBytes, 0)} bytes as RGBA32; of the counted ones, ${a.uncompressed.filter(x => SHIPPED.has(x.state)).reduce((s, x) => s + x.ownFormatBytes, 0)} bytes as RGBA32.` : 'none.'}`, '');
    out.push(`Imported with a side over the PLAN A5 cap of ${CAP_PX} px: ${a.overCap.length ? a.overCap.map(x => `\`${x.path}\` (${dim(x.imported)}, ${x.state})`).join(', ') + '.' : 'none.'}`, '');
    out.push(`Textures from shared packages this app's manifest names: ${a.fromPackages.length ? '' : 'none at this commit.'}`, '');
    for (const x of a.fromPackages) out.push(`- \`${x.path}\` (${x.pkg}): ${x.bytes} bytes, not counted`);
    if (a.fromPackages.length) out.push('');
    if (a.unread.length) out.push(`Dimensions not read, so not counted: ${a.unread.map(p => `\`${p}\``).join(', ')}.`, '');
  }
  out.push('## Every texture', '');
  out.push(row(['Texture', 'State', 'Source', 'nPOT', 'Max (from)', 'Imported', 'Mips', 'Bytes', 'Android note']), row(['---', '---', '---', '---', '---', '---', '---:', '---:', '---']));
  for (const x of r.records) {
    if (x.error) { out.push(row([`\`${x.path}\``, x.state || '-', x.error, '-', '-', '-', '-', 0, '-'])); continue; }
    out.push(row([`\`${x.path}\``, x.state || `package ${x.pkg}`, dim(x.source), dim(x.npot), `${x.maxTextureSize} (${x.maxFrom})`, dim(x.imported), x.levels, x.bytes, x.androidFormat || '-']));
  }
  out.push('');
  return out.join('\n');
}

// The json beside the markdown: totals and per-texture rows, one record per line.
export function texmemJson(r) {
  const head = { schema: r.schema, commit: r.sha, subject: r.subject, budgetsTree: r.budgetsTree, apps: r.apps };
  const lines = JSON.stringify(head, null, 2).replace(/\n}$/, ',\n  "records": [\n');
  return lines + r.records.map(x => `    ${JSON.stringify(x)}`).join(',\n') + '\n  ]\n}\n';
}
