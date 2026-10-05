#!/usr/bin/env node
// Mechanical check for the AGENTS.md rules an agent slips on under pressure. Rules and why: tools/guard/README.md.
//
//   node tools/guard/check.mjs --staged      what is about to be committed (tools/guard/hooks/pre-commit runs this)
//   node tools/guard/check.mjs --all         every tracked file in the working tree (host Definition of Done step)
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

// G1: the command list in CLAUDE.md must not drift from the canonical one in AGENTS.md.
function commandsBlock(text) {
  const m = text.match(/^## Commands[^\n]*\n[\s\S]*?^```[^\n]*\n([\s\S]*?)^```/m);
  return m ? m[1].split('\n').map((l) => l.trim()).filter(Boolean) : null;
}
function checkGuidance(claude, agents) {
  const a = commandsBlock(agents.replace(/\r/g, ''));
  if (!a) return [finding('AGENTS.md', 0, 'G1', 'no "## Commands" fenced block; it is the canonical command list')];
  const c = commandsBlock(claude.replace(/\r/g, ''));
  if (!c) return [finding('CLAUDE.md', 0, 'G1', 'no "## Commands" fenced block; copy it from AGENTS.md')];
  const have = new Set(a);
  return c.filter((l) => !have.has(l)).map((l) => finding('CLAUDE.md', 0, 'G1', `command not in the AGENTS.md Commands block: ${l}`));
}

const git = (args) => execFileSync('git', args, { cwd: root, maxBuffer: 1 << 28 });
const zlist = (buf) => buf.toString('utf8').split('\0').filter(Boolean);
const readsContent = (p) => p.endsWith('.cs') || p === CORE_ASMDEF;

function staged() {
  const files = zlist(git(['diff', '--cached', '--name-only', '-z', '--diff-filter=ACMR']));
  let branch = '';
  try { branch = git(['rev-parse', '--abbrev-ref', 'HEAD']).toString().trim(); } catch { branch = ''; }
  const out = [];
  for (const p of files) {
    out.push(...checkPath(p), ...checkOwnership(branch, p));
    if (readsContent(p)) out.push(...checkContent(p, git(['show', ':' + p]).toString('utf8')));
  }
  if (files.includes('CLAUDE.md') || files.includes('AGENTS.md')) {
    const show = (p) => { try { return git(['show', ':' + p]).toString('utf8'); } catch { return ''; } };
    out.push(...checkGuidance(show('CLAUDE.md'), show('AGENTS.md')));
  }
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
    ['G1 in sync', checkGuidance(doc(['a', 'b']), doc(['a', 'b', 'c'])), null],
    ['G1 CRLF in sync', checkGuidance(doc(['a', 'b']).replace(/\n/g, '\r\n'), doc(['b', 'a'])), null],
    ['G1 drift', checkGuidance(doc(['a', 'x']), doc(['a'])), 'G1'],
    ['G1 AGENTS.md lost its block', checkGuidance(doc(['a']), '# Fixture\n'), 'G1'],
    ['G1 CLAUDE.md lost its block', checkGuidance('# Fixture\n', doc(['a'])), 'G1'],
  ];
  let failed = 0;
  for (const [name, found, rule] of cases) {
    const ok = rule === null ? found.length === 0 : found.length > 0 && found.every((f) => f.rule === rule);
    if (!ok) { failed++; console.log(`FAIL ${name}: expected ${rule || 'nothing'}, got ${JSON.stringify(found)}`); }
  }
  const warnOnly = checkOwnership('agent/terrarium', 'apps/sundial/x.cs').every((f) => f.level === 'warn');
  if (!warnOnly) { failed++; console.log('FAIL W1 must be a warning, never an error'); }
  console.log(`guard self-test: ${cases.length + 1 - failed} passed, ${failed} failed`);
  process.exit(failed ? 1 : 0);
}

const mode = process.argv[2];
if (mode === '--self-test') selfTest();
else if (mode === '--staged') report(staged());
else if (mode === '--all') report(all());
else { console.error('usage: node tools/guard/check.mjs --staged | --all | --self-test'); process.exit(2); }
