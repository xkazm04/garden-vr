#!/usr/bin/env node
// Mechanical check for the AGENTS.md rules an agent slips on under pressure. Rules and why: tools/guard/README.md.
//
//   node tools/guard/check.mjs --staged      what is about to be committed (tools/guard/hooks/pre-commit and
//                                            pre-merge-commit run this)
//   node tools/guard/check.mjs --all         every tracked file in the working tree (host Definition of Done step)
//   node tools/guard/check.mjs --range <base>..<head> [--as <branch>]
//                                            every file a task's commits changed, read at <head>; the agent loop runs
//                                            it after each task, so a commit that skipped the hook is still checked
//   node tools/guard/check.mjs --self-test   the rules against in-memory fixtures; needs no git
//
// Each finding prints as `path[:line] RULE message`. Any error exits 1; warnings alone exit 0.
import fs from 'node:fs';
import path from 'node:path';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');

const INPUT_PROVIDER = 'shared/packages/com.gardenvr.input/Runtime/Providers/';
const CORE_ASMDEF = 'shared/packages/com.gardenvr.core/Runtime/GardenVR.Core.asmdef';
const MANIFEST = '.ai/manifest.yaml';
const LEDGER = 'docs/knowledge/verifications.jsonl';
const HOOKS = 'tools/guard/hooks';
const EM_DASH = '—';

// Raw device reads that must stay inside the keyboard/mouse provider (AGENTS rule 1, gate S1).
const RAW_INPUT = [
  /\bKeyboard\.current\b/,
  /\bMouse\.current\b/,
  /\bUnityEngine\.Input\./,
  /(?<![\w.])Input\.(Get\w*|mousePosition|anyKey\w*)\b/,
];

// Paths only the host changes (PLAN section 5, ownership table, last row).
const HOST_OWNED = [
  /^AGENTS\.md$/, /^CLAUDE\.md$/, /^docs\//, /^tools\/orchestrate\//, /^tools\/guard\//, /^tools\/audio\/elevenlabs\.mjs$/,
  /\.csproj$/, /(^|\/)Packages\/manifest\.json$/, /^\.ai\//,
];

// Owner per path prefix (PLAN section 5, ownership table). First match wins, so the specific prefixes come first.
const OWNERS = [
  ['shared/packages/com.gardenvr.fx/Runtime/Shaders/FGlass.shader', 'terrarium'],
  ['shared/packages/com.gardenvr.fx/Runtime/Shaders/FGlow.shader', 'terrarium'],
  ['shared/packages/com.gardenvr.fx/', 'sundial'],
  ['shared/packages/com.gardenvr.input/', 'terrarium'],
  ['shared/packages/com.gardenvr.room/', 'terrarium'],
  ['shared/packages/com.gardenvr.audio/', 'terrarium'],
  ['shared/packages/com.gardenvr.capture/', 'sundial'],
  ['shared/packages/com.gardenvr.core/Runtime/Breath/', 'terrarium'],
  ['shared/packages/com.gardenvr.core/Runtime/Terrarium/', 'terrarium'],
  ['shared/packages/com.gardenvr.core/Runtime/Common/', 'sundial'],
  ['shared/packages/com.gardenvr.core/Runtime/Sundial/', 'sundial'],
  ['shared/core-dotnet/GardenVR.Core.Tests/Terrarium', 'terrarium'],
  ['shared/core-dotnet/GardenVR.Core.Tests/Common', 'sundial'],
  ['shared/core-dotnet/GardenVR.Core.Tests/Sundial', 'sundial'],
  ['tools/blender/terrarium_', 'terrarium'],
  ['tools/blender/sundial_', 'sundial'],
  ['tools/audio/mixdown.mjs', 'terrarium'],
  ['apps/terrarium/', 'terrarium'],
  ['apps/sundial/', 'sundial'],
];

const isComment = (line) => /^\s*(\/\/|\/\*|\*)/.test(line);
const finding = (p, line, rule, msg, level = 'error') => ({ path: p, line, rule, msg, level });

// Rules that read a file's content: R1 raw input, R4 core purity, R7 em dash in strings.
function checkContent(p, text) {
  const out = [];
  const lines = text.split(/\r?\n/);
  const cs = p.endsWith('.cs');
  if (cs && (p.startsWith('apps/') || p.startsWith('shared/packages/')) && !p.startsWith(INPUT_PROVIDER)) {
    lines.forEach((l, i) => {
      if (!isComment(l) && RAW_INPUT.some((re) => re.test(l)))
        out.push(finding(p, i + 1, 'R1', 'raw Input/Keyboard/Mouse read; go through com.gardenvr.input intents (AGENTS rule 1)'));
    });
  }
  if (cs && (p.startsWith('shared/packages/com.gardenvr.core/') || p.startsWith('shared/core-dotnet/'))) {
    lines.forEach((l, i) => {
      if (!isComment(l) && /\bUnity(Engine|Editor)\b/.test(l))
        out.push(finding(p, i + 1, 'R4', 'core is pure C#; no UnityEngine/UnityEditor (AGENTS rule 4)'));
    });
  }
  if (p === CORE_ASMDEF) {
    let kept = false;
    try { kept = JSON.parse(text.replace(/^﻿/, '')).noEngineReferences === true; } catch { kept = false; }
    if (!kept) out.push(finding(p, 0, 'R4', 'GardenVR.Core.asmdef must keep "noEngineReferences": true (AGENTS rule 4)'));
  }
  if (cs && /^(apps\/[^/]+\/Assets\/|shared\/packages\/[^/]+\/Runtime\/)/.test(p) && !/\/Tests\/|\/TextMesh Pro\//.test(p)) {
    lines.forEach((l, i) => {
      if (isComment(l) || !l.includes(EM_DASH)) return;
      for (const m of l.matchAll(/"(?:[^"\\]|\\.)*"/g)) {
        if (m[0].includes(EM_DASH)) { out.push(finding(p, i + 1, 'R7', 'em dash in a string; use a hyphen or rephrase (AGENTS rule 7)')); break; }
      }
    });
  }
  return out;
}

// A1: files that must never be committed (AGENTS "How to work a task").
function checkPath(p) {
  const out = [];
  const base = p.split('/').pop();
  if (/^apps\/[^/]+\/(library|temp|obj|logs|builds?|usersettings)\//i.test(p))
    out.push(finding(p, 0, 'A1', 'Unity generated folder; never commit Library/, Temp/, Logs/ or builds'));
  if (/^\.env(\..*)?$/.test(base) && base !== '.env.example') out.push(finding(p, 0, 'A1', 'secrets file; .env stays local'));
  if (base.endsWith('.blend1')) out.push(finding(p, 0, 'A1', 'Blender backup file'));
  return out;
}

// W1: on an agent branch, a path the agent does not own. A warning, not an error: PLAN section 5 allows a path the
// task's `touches:` names, and the report must flag it.
function checkOwnership(branch, p) {
  const m = /^agent\/(terrarium|sundial)$/.exec(branch || '');
  if (!m) return [];
  const app = m[1];
  if (HOST_OWNED.some((re) => re.test(p)))
    return [finding(p, 0, 'W1', `host-owned path on agent/${app}; allowed only if the task's touches: names it, and REPORT.md must flag it`, 'warn')];
  const owner = (OWNERS.find(([prefix]) => p.startsWith(prefix)) || [])[1];
  if (owner && owner !== app)
    return [finding(p, 0, 'W1', `${owner}-owned path on agent/${app}; allowed only if the task's touches: names it, and REPORT.md must flag it`, 'warn')];
  return [];
}

// G1: CLAUDE.md carries exactly the canonical command list from AGENTS.md, so Claude and Grok sessions run the same
// commands. The fence must sit inside the "## Commands" section; a later section's fence does not count.
function commandsBlock(text) {
  const s = text.replace(/\r/g, '').match(/^## Commands[^\n]*\n([\s\S]*?)(?=^## |(?![\s\S]))/m);
  const m = s && s[1].match(/^```[^\n]*\n([\s\S]*?)^```/m);
  return m ? m[1].split('\n').map((l) => l.trim()).filter(Boolean) : null;
}
function checkGuidance(claude, agents) {
  const a = commandsBlock(agents);
  if (!a) return [finding('AGENTS.md', 0, 'G1', 'no "## Commands" fenced block; it is the canonical command list')];
  const c = commandsBlock(claude);
  if (!c) return [finding('CLAUDE.md', 0, 'G1', 'no "## Commands" fenced block; copy it from AGENTS.md')];
  const inA = new Set(a), inC = new Set(c);
  return [
    ...c.filter((l) => !inA.has(l)).map((l) => finding('CLAUDE.md', 0, 'G1', `command not in the AGENTS.md Commands block: ${l}`)),
    ...a.filter((l) => !inC.has(l)).map((l) => finding('CLAUDE.md', 0, 'G1', `AGENTS.md command missing from the CLAUDE.md Commands block: ${l}`)),
  ];
}

// G2: .ai/manifest.yaml machine-declares AGENTS.md as the canonical guidance and lists every canonical command, so a
// tool reading the manifest sees the same rules and commands an agent reads. A text check; the guard has no YAML parser.
function checkManifest(manifest, agents) {
  const text = manifest.replace(/\r/g, '');
  if (!text) return [finding(MANIFEST, 0, 'G2', 'missing; it declares guidance.canonical and the commands')];
  const g = text.match(/^guidance:[ \t]*\n((?:[ \t]+[^\n]*\n?|[ \t]*#[^\n]*\n?|[ \t]*\n)*)/m);
  const canon = g && (g[1].match(/^[ \t]+canonical:[ \t]*["']?([^"'\s#]+)/m) || [])[1];
  const out = [];
  if (canon !== 'AGENTS.md')
    out.push(finding(MANIFEST, 0, 'G2', `guidance.canonical must be AGENTS.md (found: ${canon || 'none'})`));
  for (const l of commandsBlock(agents) || [])
    if (!text.includes(l)) out.push(finding(MANIFEST, 0, 'G2', `AGENTS.md command missing from commands: ${l}`));
  return out;
}

// H1: the commit hooks are installed. core.hooksPath is shared by the main checkout and every agent worktree, so setting
// it is the owner's call (README); until then every host commit and merge skips the guard, and this says so on each run.
function checkHook(hooksPath) {
  const v = (hooksPath || '').trim().replace(/\\/g, '/').replace(/\/+$/, '');
  if (v === HOOKS || v.endsWith('/' + HOOKS)) return [];
  return [finding('core.hooksPath', 0, 'H1', `commit hooks not installed (core.hooksPath is ${v ? `"${v}"` : 'unset'}); commits and merges here skip the guard until the owner runs: git config core.hooksPath ${HOOKS}`, 'warn')];
}

// V1: the verification ledger, one host verdict per line (docs/AI_HARNESS.md, Definition of Done). It is the committed
// record that the Definition of Done was checked, so a "verified" line must show every check passed or not applicable:
// "not run" is never a pass (registry unmeasured-is-not-a-pass).
const DOD_CHECKS = ['oracles', 'acceptance', 'core', 'guard', 'scope', 'evidence', 'report', 'postMerge'];
const CHECK_VALUES = ['pass', 'fail', 'not run', 'n/a'];
const SHA = /^[0-9a-f]{7,64}$/;
function checkLedger(text) {
  const out = [];
  const bad = (n, msg) => out.push(finding(LEDGER, n, 'V1', msg));
  text.replace(/\r/g, '').split('\n').forEach((raw, i) => {
    const n = i + 1;
    if (!raw.trim()) return;
    let e;
    try { e = JSON.parse(raw); } catch { e = null; }
    if (!e || typeof e !== 'object' || Array.isArray(e)) { bad(n, 'not a JSON object; the ledger holds one verdict object per line'); return; }
    if (!/^T-[A-Z]+-\d+$/.test(e.task || '')) bad(n, `task must be a task id such as T-TER-012 (found: ${e.task})`);
    if (!['terrarium', 'sundial'].includes(e.app)) bad(n, `app must be terrarium or sundial (found: ${e.app})`);
    if (!['verified', 'rejected'].includes(e.verdict)) bad(n, `verdict must be verified or rejected (found: ${e.verdict})`);
    if (!/^\d{4}-\d{2}-\d{2}/.test(e.date || '')) bad(n, `date must start YYYY-MM-DD (found: ${e.date})`);
    for (const k of ['base', 'head']) if (!SHA.test(e[k] || '')) bad(n, `${k} must be a commit sha (found: ${e[k]})`);
    const checks = e.checks && typeof e.checks === 'object' ? e.checks : {};
    for (const k of DOD_CHECKS)
      if (!CHECK_VALUES.includes(checks[k])) bad(n, `checks.${k} must be one of ${CHECK_VALUES.join(' | ')} (found: ${checks[k]})`);
    if (e.judge != null && (typeof e.judge.script !== 'string' || !SHA.test(e.judge.blob || '')))
      bad(n, 'judge must be null or name the judge script and its git blob sha, which pins the prompt it ran');
    if (e.verdict === 'rejected' && !(typeof e.reason === 'string' && e.reason.trim())) bad(n, 'a rejected verdict needs a reason');
    if (e.verdict === 'verified') {
      if (!SHA.test(e.merge || '')) bad(n, 'a verified verdict needs the merge sha on main');
      const open = DOD_CHECKS.filter((k) => checks[k] !== 'pass' && checks[k] !== 'n/a');
      if (open.length) bad(n, `verified with checks that did not pass: ${open.join(', ')} ("not run" is never a pass)`);
      if (!/^guard: 0 error/.test(e.guardOut || '')) bad(n, 'a verified verdict needs guardOut "guard: 0 error(s), ..." from the host re-run');
    }
  });
  return out;
}

// V2: every merge of an agent branch into main since the ledger was added has a verdict line naming its merge sha, so
// no merged change lacks a committed record. `merges` are main's first-parent merge commits as { sha, subject }. An
// error on main; a warning on any other branch, so the agent loop's run surfaces a missing line without failing the
// agent for a host lapse.
function checkMergeRecords(merges, ledgerText, level = 'error') {
  const recorded = [];
  for (const raw of ledgerText.replace(/\r/g, '').split('\n')) {
    try { const e = JSON.parse(raw); if (e && typeof e.merge === 'string' && SHA.test(e.merge)) recorded.push(e.merge); } catch { /* V1 reports it */ }
  }
  return merges
    .filter((m) => /^Merge branch '?agent\/(terrarium|sundial)\b/.test(m.subject) && !/ into agent\//.test(m.subject))
    .filter((m) => !recorded.some((r) => m.sha.startsWith(r)))
    .map((m) => finding(LEDGER, 0, 'V2', `merge ${m.sha.slice(0, 10)} (${m.subject}) on main has no verdict line; append it (docs/AI_HARNESS.md, Definition of Done)`, level));
}

// The path, ownership and content rules over a list of changed files, plus G1/G2 and V1 when their files changed.
// `show(p)` returns the file's content at the state being checked (the index for --staged, <head> for --range).
function checkFiles(branch, files, show) {
  const get = (p) => { try { return show(p) || ''; } catch { return ''; } };
  const out = [];
  for (const p of files) {
    out.push(...checkPath(p), ...checkOwnership(branch, p));
    if (readsContent(p)) out.push(...checkContent(p, get(p)));
  }
  if (['CLAUDE.md', 'AGENTS.md', MANIFEST].some((p) => files.includes(p)))
    out.push(...checkGuidance(get('CLAUDE.md'), get('AGENTS.md')), ...checkManifest(get(MANIFEST), get('AGENTS.md')));
  if (files.includes(LEDGER)) out.push(...checkLedger(get(LEDGER)));
  return out;
}

const git = (args) => execFileSync('git', args, { cwd: root, maxBuffer: 1 << 28, stdio: ['ignore', 'pipe', 'pipe'] });
const zlist = (buf) => buf.toString('utf8').split('\0').filter(Boolean);
const readsContent = (p) => p.endsWith('.cs') || p === CORE_ASMDEF;
const currentBranch = () => { try { return git(['rev-parse', '--abbrev-ref', 'HEAD']).toString().trim(); } catch { return ''; } };
const hooksPath = () => { try { return git(['config', '--get', 'core.hooksPath']).toString(); } catch { return ''; } };

// The first-parent merges on `ref` since the commit that added the ledger (V2). Empty before the ledger is committed.
function mergesSinceLedger(ref) {
  try {
    const added = git(['log', '--diff-filter=A', '--format=%H', ref, '--', LEDGER]).toString().split(/\r?\n/).filter(Boolean).pop();
    if (!added) return [];
    return git(['log', '--first-parent', '--merges', '--format=%H%x09%s', `${added}..${ref}`]).toString().split(/\r?\n/).filter(Boolean)
      .map((l) => { const [sha, ...s] = l.split('\t'); return { sha, subject: s.join('\t') }; });
  } catch { return []; }
}
const showAt = (ref, p) => { try { return git(['show', `${ref}:${p}`]).toString('utf8'); } catch { return null; } };

function staged() {
  const files = zlist(git(['diff', '--cached', '--name-only', '-z', '--diff-filter=ACMR']));
  return checkFiles(currentBranch(), files, (p) => git(['show', ':' + p]).toString('utf8'));
}

function range(spec, asBranch) {
  const [base, head] = (spec || '').split('..');
  if (!base || !head || (spec.match(/\.\./g) || []).length !== 1 || head.startsWith('.')) {
    console.error('usage: node tools/guard/check.mjs --range <base>..<head> [--as <branch>]');
    process.exit(2);
  }
  let files;
  try { files = zlist(git(['diff', '--name-only', '-z', '--diff-filter=ACMR', base, head])); } catch (e) {
    console.error(`guard: cannot read the range ${spec}: ${String((e.stderr && e.stderr.toString()) || e.message).trim()}`);
    process.exit(2);
  }
  console.log(`range ${spec}: ${files.length} changed file(s)`);
  const out = checkFiles(asBranch || currentBranch(), files, (p) => git(['show', `${head}:${p}`]).toString('utf8'));
  out.push(...checkHook(hooksPath()));
  return out;
}

function all() {
  const files = zlist(git(['ls-files', '-z']));
  const read = (p) => { const abs = path.join(root, p); return fs.existsSync(abs) ? fs.readFileSync(abs, 'utf8') : null; };
  const out = [];
  for (const p of files) {
    out.push(...checkPath(p));
    if (readsContent(p)) { const text = read(p); if (text !== null) out.push(...checkContent(p, text)); }
  }
  if (!files.includes(CORE_ASMDEF)) out.push(finding(CORE_ASMDEF, 0, 'R4', 'core asmdef is not tracked; the engine-free flag cannot be checked'));
  out.push(...checkGuidance(read('CLAUDE.md') || '', read('AGENTS.md') || ''));
  out.push(...checkManifest(read(MANIFEST) || '', read('AGENTS.md') || ''));
  const ledger = read(LEDGER);
  if (ledger !== null) out.push(...checkLedger(ledger));
  // V2 always reads main: its ledger and its merges. On another branch (an agent worktree) it is a warning.
  const onMain = currentBranch() === 'main';
  const mainLedger = onMain ? ledger : showAt('main', LEDGER);
  if (mainLedger !== null) out.push(...checkMergeRecords(mergesSinceLedger(onMain ? 'HEAD' : 'main'), mainLedger, onMain ? 'error' : 'warn'));
  out.push(...checkHook(hooksPath()));
  return out;
}

function report(findings) {
  for (const f of findings)
    console.log(`${f.path}${f.line ? ':' + f.line : ''} ${f.rule}${f.level === 'warn' ? ' (warning)' : ''} ${f.msg}`);
  const errors = findings.filter((f) => f.level !== 'warn').length;
  console.log(`guard: ${errors} error(s), ${findings.length - errors} warning(s)`);
  if (errors) console.log('guard: fix the errors above; tools/guard/README.md says what each rule protects');
  process.exit(errors ? 1 : 0);
}

function selfTest() {
  const app = 'apps/terrarium/Assets/Scripts/Fixture.cs';
  const ui = 'apps/sundial/Assets/Scripts/Ui/Fixture.cs';
  const doc = (lines) => `# Fixture\n\n## Commands\n\n\`\`\`powershell\n${lines.join('\n')}\n\`\`\`\n\n## Next\n`;
  const manifest = (canonical, cmds) =>
    `schema: ai-manifest\n# which file wins\nguidance:\n  canonical: ${canonical}\n  adapters: [CLAUDE.md]\n\n# commands\ncommands:\n` +
    cmds.map((c, i) => `  c${i}: >-\n    ${c}\n`).join('');
  const PASS = { oracles: 'pass', acceptance: 'pass', core: 'pass', guard: 'pass', scope: 'pass', evidence: 'n/a', report: 'pass', postMerge: 'pass' };
  const MERGE = 'c3d4e5f';
  const verdict = (o) => JSON.stringify({
    task: 'T-TER-045', app: 'terrarium', verdict: 'verified', reason: null, base: 'a1b2c3d', head: 'b2c3d4e', merge: MERGE,
    date: '2026-10-05', checks: PASS, guardOut: 'guard: 0 error(s), 1 warning(s)', w1: [], judge: null, ...o,
  });
  // [name, findings, the one rule expected to fire, or null for none]
  const cases = [
    ['R1 app reads Keyboard.current', checkContent(app, 'var k = Keyboard.current;'), 'R1'],
    ['R1 app reads Mouse.current', checkContent(app, 'var p = UnityEngine.InputSystem.Mouse.current.position;'), 'R1'],
    ['R1 legacy Input.GetKeyDown', checkContent(ui, 'if (Input.GetKeyDown(KeyCode.Space)) Tend();'), 'R1'],
    ['R1 qualified UnityEngine.Input', checkContent(ui, 'var m = UnityEngine.Input.mousePosition;'), 'R1'],
    ['R1 shared package outside the provider', checkContent('shared/packages/com.gardenvr.room/Runtime/Rig.cs', 'if (Input.anyKey) Wake();'), 'R1'],
    ['R1 the provider may read devices', checkContent(INPUT_PROVIDER + 'KeyboardMouseIntentSource.cs', 'keyboard = Keyboard.current;'), null],
    ['R1 InputSystem namespace is fine', checkContent(app, 'using UnityEngine.InputSystem;'), null],
    ['R1 our Input namespace is fine', checkContent(app, 'var s = GardenVR.Input.GetSource();'), null],
    ['R1 a comment is fine', checkContent(app, '// never read Keyboard.current here'), null],
    ['R1 tools are out of scope', checkContent('tools/capture/Fixture.cs', 'var k = Keyboard.current;'), null],
    ['R4 core uses UnityEngine', checkContent('shared/packages/com.gardenvr.core/Runtime/Common/Fixture.cs', 'using UnityEngine;'), 'R4'],
    ['R4 core test uses UnityEditor', checkContent('shared/core-dotnet/GardenVR.Core.Tests/Fixture.cs', 'using UnityEditor;'), 'R4'],
    ['R4 pure core', checkContent('shared/packages/com.gardenvr.core/Runtime/Common/Fixture.cs', 'using System;\nnamespace GardenVR.Core {}'), null],
    ['R4 asmdef flag flipped', checkContent(CORE_ASMDEF, '{ "name": "GardenVR.Core", "noEngineReferences": false }'), 'R4'],
    ['R4 asmdef flag removed', checkContent(CORE_ASMDEF, '{ "name": "GardenVR.Core" }'), 'R4'],
    ['R4 asmdef flag kept (with BOM)', checkContent(CORE_ASMDEF, '﻿{ "name": "GardenVR.Core", "noEngineReferences": true }'), null],
    ['R7 em dash in a string', checkContent(ui, `label.text = "Breathe ${EM_DASH} slowly";`), 'R7'],
    ['R7 em dash in a second string', checkContent(ui, `Say("ok", "rest ${EM_DASH} now");`), 'R7'],
    ['R7 em dash in a comment', checkContent(ui, `// rest ${EM_DASH} now`), null],
    ['R7 em dash in a trailing comment', checkContent(ui, `Say("rest"); // ${EM_DASH} later`), null],
    ['R7 tests are out of scope', checkContent('apps/sundial/Assets/Tests/EditMode/Fixture.cs', `var s = "a ${EM_DASH} b";`), null],
    ['R7 hyphen is fine', checkContent(ui, 'label.text = "Breathe - slowly";'), null],
    ['A1 Unity Library', checkPath('apps/terrarium/Library/ArtifactDB'), 'A1'],
    ['A1 Unity Builds', checkPath('apps/sundial/Builds/Sundial.exe'), 'A1'],
    ['A1 .env', checkPath('.env'), 'A1'],
    ['A1 .env.local', checkPath('tools/audio/.env.local'), 'A1'],
    ['A1 .env.example is fine', checkPath('.env.example'), null],
    ['A1 blend backup', checkPath('tools/blender/jar.blend1'), 'A1'],
    ['A1 a Temp folder inside Assets is fine', checkPath('apps/sundial/Assets/Art/Temp/notes.txt'), null],
    ['W1 the other app', checkOwnership('agent/terrarium', 'apps/sundial/Assets/Editor/SceneSetup.cs'), 'W1'],
    ['W1 a shared path the other agent owns', checkOwnership('agent/sundial', 'shared/packages/com.gardenvr.input/Runtime/Intent.cs'), 'W1'],
    ['W1 a terrarium shader inside sundial fx', checkOwnership('agent/sundial', 'shared/packages/com.gardenvr.fx/Runtime/Shaders/FGlass.shader.meta'), 'W1'],
    ['W1 a host-owned path', checkOwnership('agent/sundial', 'AGENTS.md'), 'W1'],
    ['W1 the own app is fine', checkOwnership('agent/terrarium', 'apps/terrarium/Assets/Scripts/Jar.cs'), null],
    ['W1 an owned shared path is fine', checkOwnership('agent/sundial', 'shared/packages/com.gardenvr.fx/Runtime/Shaders/FCard.shader'), null],
    ['W1 the host branch is not checked', checkOwnership('main', 'apps/sundial/Assets/Editor/SceneSetup.cs'), null],
    ['G1 in sync', checkGuidance(doc(['a', 'b']), doc(['b', 'a'])), null],
    ['G1 CRLF in sync', checkGuidance(doc(['a', 'b']).replace(/\n/g, '\r\n'), doc(['b', 'a'])), null],
    ['G1 drift', checkGuidance(doc(['a', 'x']), doc(['a'])), 'G1'],
    ['G1 AGENTS.md gained a command CLAUDE.md lacks', checkGuidance(doc(['a', 'b']), doc(['a', 'b', 'c'])), 'G1'],
    ['G1 AGENTS.md lost its block', checkGuidance(doc(['a']), '# Fixture\n'), 'G1'],
    ['G1 CLAUDE.md lost its block', checkGuidance('# Fixture\n', doc(['a'])), 'G1'],
    ['G1 a later section\'s fence does not count', checkGuidance('# F\n\n## Commands\n\nsee AGENTS.md\n\n## Next\n\n```\na\n```\n', doc(['a'])), 'G1'],
    ['G2 in sync', checkManifest(manifest('AGENTS.md', ['run-one', "run two '-x'"]), doc(['run-one', "run two '-x'"])), null],
    ['G2 CRLF and quoted canonical in sync', checkManifest(manifest('"AGENTS.md"', ['run-one']).replace(/\n/g, '\r\n'), doc(['run-one'])), null],
    ['G2 manifest missing', checkManifest('', doc(['run-one'])), 'G2'],
    ['G2 canonical missing', checkManifest('guidance:\n  plan: docs/PLAN.md\ncommands:\n  t: run-one\n', doc(['run-one'])), 'G2'],
    ['G2 canonical outside guidance', checkManifest('canonical: AGENTS.md\nguidance:\n  plan: docs/PLAN.md\ncommands:\n  t: run-one\n', doc(['run-one'])), 'G2'],
    ['G2 canonical names the wrong file', checkManifest(manifest('CLAUDE.md', ['run-one']), doc(['run-one'])), 'G2'],
    ['G2 command missing', checkManifest(manifest('AGENTS.md', ['run-one']), doc(['run-one', 'run-two'])), 'G2'],
    ['H1 hooks unset', checkHook(''), 'H1'],
    ['H1 hooks point elsewhere', checkHook('.husky\n'), 'H1'],
    ['H1 hooks installed', checkHook('tools/guard/hooks\n'), null],
    ['H1 hooks installed by absolute Windows path', checkHook('C:\\Users\\x\\garden-vr\\tools\\guard\\hooks\\'), null],
    ['range: the other app on an agent branch', checkFiles('agent/terrarium', ['apps/sundial/Assets/Editor/SceneSetup.cs'], () => 'class S {}'), 'W1'],
    ['range: raw input in a changed file', checkFiles('main', [app], () => 'var k = Keyboard.current;'), 'R1'],
    ['range: a committed Library file', checkFiles('agent/sundial', ['apps/sundial/Library/ArtifactDB'], () => ''), 'A1'],
    ['range: guidance drift in a changed file', checkFiles('main', ['CLAUDE.md'],
      (p) => ({ 'CLAUDE.md': doc(['a', 'x']), 'AGENTS.md': doc(['a']), [MANIFEST]: manifest('AGENTS.md', ['a']) })[p]), 'G1'],
    ['range: a bad ledger line in a changed file', checkFiles('main', [LEDGER], () => '{oops\n'), 'V1'],
    ['range: a clean change', checkFiles('agent/sundial', ['apps/sundial/Assets/Scripts/Dial.cs'], () => 'class Dial {}'), null],
    ['V1 empty ledger', checkLedger(''), null],
    ['V1 a verified line', checkLedger(verdict({}) + '\n'), null],
    ['V1 CRLF and blank lines', checkLedger(`${verdict({})}\r\n\r\n${verdict({ task: 'T-SUN-002', app: 'sundial' })}\r\n`), null],
    ['V1 a rejected line with a reason', checkLedger(verdict({ verdict: 'rejected', merge: null, reason: 'acceptance XML: 2 failed', checks: { ...PASS, acceptance: 'fail' } })), null],
    ['V1 a judge pinned by blob', checkLedger(verdict({ judge: { script: 'apps/sundial/Art/Scripts/sprint_t050_rubric.py', blob: '0123456789abcdef0123456789abcdef01234567', model: 'gemini-3.8-flash-high', median: 2 } })), null],
    ['V1 not JSON', checkLedger('{oops'), 'V1'],
    ['V1 an array, not an object', checkLedger('[1, 2]'), 'V1'],
    ['V1 verified with a check not run', checkLedger(verdict({ checks: { ...PASS, core: 'not run' } })), 'V1'],
    ['V1 verified with a failed check', checkLedger(verdict({ checks: { ...PASS, scope: 'fail' } })), 'V1'],
    ['V1 verified with a check missing', checkLedger(verdict({ checks: { oracles: 'pass' } })), 'V1'],
    ['V1 verified without a merge', checkLedger(verdict({ merge: null })), 'V1'],
    ['V1 verified with guard errors', checkLedger(verdict({ guardOut: 'guard: 2 error(s), 0 warning(s)' })), 'V1'],
    ['V1 rejected without a reason', checkLedger(verdict({ verdict: 'rejected', merge: null, reason: ' ' })), 'V1'],
    ['V1 a bad sha', checkLedger(verdict({ base: 'not-a-sha' })), 'V1'],
    ['V1 an unknown app', checkLedger(verdict({ app: 'garden' })), 'V1'],
    ['V1 a bad task id', checkLedger(verdict({ task: 'fix the jar' })), 'V1'],
    ['V1 a judge without its blob', checkLedger(verdict({ judge: { script: 'x_judge.py' } })), 'V1'],
    ['V2 an agent merge with its line', checkMergeRecords([{ sha: MERGE + '0'.repeat(33), subject: "Merge branch 'agent/terrarium'" }], verdict({})), null],
    ['V2 an agent merge into main with its line', checkMergeRecords([{ sha: MERGE + '0'.repeat(33), subject: "Merge branch 'agent/terrarium' into main" }], verdict({})), null],
    ['V2 an agent merge without a line', checkMergeRecords([{ sha: 'f'.repeat(40), subject: "Merge branch 'agent/sundial'" }], verdict({})), 'V2'],
    ['V2 an agent merge with an empty ledger', checkMergeRecords([{ sha: 'f'.repeat(40), subject: "Merge branch 'agent/sundial' into main" }], ''), 'V2'],
    ['V2 main merged into an agent branch is not a verdict', checkMergeRecords([{ sha: 'f'.repeat(40), subject: "Merge branch 'main' into agent/sundial" }], ''), null],
    ['V2 another merge is not a verdict', checkMergeRecords([{ sha: 'f'.repeat(40), subject: "Merge branch 'ascent/loop-1'" }], ''), null],
  ];
  let failed = 0;
  for (const [name, found, rule] of cases) {
    const ok = rule === null ? found.length === 0 : found.length > 0 && found.every((f) => f.rule === rule);
    if (!ok) { failed++; console.log(`FAIL ${name}: expected ${rule || 'nothing'}, got ${JSON.stringify(found)}`); }
  }
  const warnOnly = checkOwnership('agent/terrarium', 'apps/sundial/x.cs').every((f) => f.level === 'warn');
  if (!warnOnly) { failed++; console.log('FAIL W1 must be a warning, never an error'); }
  const hookWarnOnly = checkHook('').every((f) => f.level === 'warn');
  if (!hookWarnOnly) { failed++; console.log('FAIL H1 must be a warning, never an error'); }
  const lost = [{ sha: 'f'.repeat(40), subject: "Merge branch 'agent/sundial'" }];
  const v2Levels = checkMergeRecords(lost, '').every((f) => f.level === 'error') && checkMergeRecords(lost, '', 'warn').every((f) => f.level === 'warn');
  if (!v2Levels) { failed++; console.log('FAIL V2 must be an error on main and a warning elsewhere'); }
  console.log(`guard self-test: ${cases.length + 3 - failed} passed, ${failed} failed`);
  process.exit(failed ? 1 : 0);
}

const mode = process.argv[2];
if (mode === '--self-test') selfTest();
else if (mode === '--staged') report(staged());
else if (mode === '--all') report(all());
else if (mode === '--range') report(range(process.argv[3], process.argv[4] === '--as' ? process.argv[5] : ''));
else { console.error('usage: node tools/guard/check.mjs --staged | --all | --range <base>..<head> [--as <branch>] | --self-test'); process.exit(2); }
