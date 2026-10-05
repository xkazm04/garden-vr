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
const BUILD_PROPS = 'shared/core-dotnet/Directory.Build.props';
const EDITORCONFIG = '.editorconfig';
const PROMPTS = 'tools/orchestrate/prompts/';
const WORKFLOW = '.github/workflows/guard.yml';
const EM_DASH = '—';

// What the CI workflow must keep running (C1). CI is where a commit that skipped the hook is still checked, so a step
// removed from it is a loosened gate, which is owner-reserved (PLAN section 5, merge policy).
const CI_REQUIRED = [
  'tools/guard/check.mjs --self-test',
  'tools/guard/check.mjs --all',
  'tools/guard/check.mjs --range',
  'dotnet test shared/core-dotnet',
];

// The docs an agent plans from (D1). Every repo path they cite must exist in a fresh clone.
const CITING_DOCS = ['AGENTS.md', 'CLAUDE.md', 'README.md', 'CONTRIBUTING.md', 'docs/PLAN.md', 'docs/AI_HARNESS.md', 'docs/knowledge/contest-record.md'];
const isCitingDoc = (p) => CITING_DOCS.includes(p) || /^docs\/plans\/[^/]+\.md$/.test(p);

// Analyzer rules promoted to errors in the core build (B1). The ratchet only moves one way: add an id here and to
// WarningsAsErrors in Directory.Build.props together, once a build shows 0 hits for it; never remove one.
const PROMOTED = ['CA2242', 'CA2200', 'CA2241', 'CA2245', 'CA1304', 'CA1310'];

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
// it is the owner's call (README); until then every host or human commit and merge skips the guard. An error on main,
// where those commits land, so a checkout without the hook cannot report green; a warning on any other branch (the
// agent loop wires the hook for agent commits through GIT_CONFIG_* env, so their worktree config never shows it).
// In CI (`ci`) there is no finding: the runner's clone makes no commits, and the workflow itself is the gate.
function checkHook(hooksPath, level = 'warn', ci = false) {
  if (ci) return [];
  const v = (hooksPath || '').trim().replace(/\\/g, '/').replace(/\/+$/, '');
  if (v === HOOKS || v.endsWith('/' + HOOKS)) return [];
  return [finding('core.hooksPath', 0, 'H1', `commit hooks not installed (core.hooksPath is ${v ? `"${v}"` : 'unset'}); commits and merges here skip the guard until the owner runs: git config core.hooksPath ${HOOKS}`, level)];
}

// C1: the CI workflow exists and still runs every check in CI_REQUIRED. Full-line YAML comments do not count.
function checkWorkflow(text) {
  const body = (text || '').replace(/\r/g, '').split('\n').filter((l) => !/^\s*#/.test(l)).join('\n');
  if (!body.trim())
    return [finding(WORKFLOW, 0, 'C1', 'missing; CI re-runs the guard and the core tests on every push, so a commit that skipped the hook is still checked')];
  return CI_REQUIRED.filter((c) => !body.includes(c))
    .map((c) => finding(WORKFLOW, 0, 'C1', `no longer runs "${c}"; removing a CI check loosens a gate, which is owner-reserved`));
}

// D1: the docs an agent plans from cite only paths a fresh clone has. A backticked or linked path under a tracked
// top-level folder must be a tracked file or folder at the state being checked. Placeholders (<app>, *, {1,2}, ranges
// such as 0001..0003) and orchestration/ (untracked by design) are skipped; an absolute local path is provenance, not a
// repo path, and is not checked. `exists(p)` answers for the checked state.
function citedPaths(text) {
  const out = [];
  text.replace(/\r/g, '').split('\n').forEach((l, i) => {
    for (const m of l.matchAll(/`([^`\s]+)`|\]\(([^)\s#]+)\)/g)) {
      let p = m[1] || m[2];
      if (!/^(apps|shared|tools|docs|\.ai|\.github)\//.test(p) && !/^(AGENTS|CLAUDE|README|CONTRIBUTING)\.md$/.test(p)) continue;
      if (/[<>*{}|]|\.\./.test(p)) continue;
      p = p.replace(/[:,.;]+$/, '').replace(/:\d+$/, '');
      out.push({ path: p, line: i + 1 });
    }
  });
  return out;
}
function checkCitations(doc, text, exists) {
  return citedPaths(text || '').filter((c) => !exists(c.path))
    .map((c) => finding(doc, c.line, 'D1', `cites ${c.path}, which is not tracked, so a fresh clone cannot read it; fix the path, or move the fact into docs/knowledge/contest-record.md`));
}
// A lookup over a list of tracked files: a path exists when it is one of them or a folder above one of them.
function pathIndex(list) {
  const files = new Set(list);
  const dirs = new Set();
  for (const f of list) {
    const parts = f.split('/');
    for (let i = 1; i < parts.length; i++) dirs.add(parts.slice(0, i).join('/'));
  }
  return (p) => { const q = p.replace(/\/+$/, ''); return files.has(q) || dirs.has(q); };
}

// B1: the analyzer ratchet never demotes. Every PROMOTED id stays in <WarningsAsErrors> of the core's
// Directory.Build.props, stays out of <NoWarn>, and keeps a `dotnet_diagnostic.<id>.severity` of warning or error in
// .editorconfig: that line is what switches the rule on, so deleting it would silently turn the error off too.
function checkRatchet(props, editorconfig) {
  const p = props.replace(/\r/g, '').replace(/<!--[\s\S]*?-->/g, '');
  if (!p.trim()) return [finding(BUILD_PROPS, 0, 'B1', 'missing; it switches the analyzers on and fails the core build on the promoted rules')];
  const ids = (tag) => {
    const set = new Set();
    for (const m of p.matchAll(new RegExp(`<${tag}>([^<]*)</${tag}>`, 'g')))
      for (const id of m[1].split(/[;,\s]+/)) if (id) set.add(id.toUpperCase());
    return set;
  };
  const asErrors = ids('WarningsAsErrors'), noWarn = ids('NoWarn');
  const severity = new Map();
  editorconfig.replace(/\r/g, '').split('\n').forEach((l, i) => {
    const m = /^\s*dotnet_diagnostic\.(\w+)\.severity\s*=\s*(\w+)/i.exec(l);
    if (m) severity.set(m[1].toUpperCase(), { level: m[2].toLowerCase(), line: i + 1 });
  });
  const out = [];
  for (const id of PROMOTED) {
    if (!asErrors.has(id)) out.push(finding(BUILD_PROPS, 0, 'B1', `${id} is promoted and must stay in <WarningsAsErrors>; the ratchet never demotes`));
    if (noWarn.has(id)) out.push(finding(BUILD_PROPS, 0, 'B1', `${id} is promoted and must not be in <NoWarn>`));
    const s = severity.get(id);
    if (!s) out.push(finding(EDITORCONFIG, 0, 'B1', `${id} is promoted but has no dotnet_diagnostic.${id}.severity line, so the analyzer may not run`));
    else if (s.level !== 'warning' && s.level !== 'error')
      out.push(finding(EDITORCONFIG, s.line, 'B1', `${id} is promoted; its severity must stay warning or error (found: ${s.level})`));
  }
  return out;
}

// P1: the prompt library is versioned (tools/orchestrate/prompts/README.md). Each prompt file opens with front matter
// `prompt: <its file name>` and an integer `prompt_version`, which the agent loop records per task with the file's blob
// sha. A change to a prompt must raise that version, so a change in what agents are told is never silent. `before` is
// the file at the commit being compared against (HEAD for --staged, <base> for --range), or null for a new file.
const isPrompt = (p) => p.startsWith(PROMPTS) && p.endsWith('.md') && !p.slice(PROMPTS.length).includes('/') && !p.endsWith('/README.md');
function promptHeader(text) {
  const m = (text || '').replace(/\r/g, '').match(/^---\n([\s\S]*?)\n---(?:\n|$)/);
  if (!m) return null;
  const get = (k) => (m[1].match(new RegExp(`^${k}:[ \\t]*(\\S.*?)[ \\t]*$`, 'm')) || [])[1];
  return { prompt: get('prompt'), version: get('prompt_version') };
}
function checkPrompt(p, text, before = null) {
  const name = p.split('/').pop().replace(/\.md$/, '');
  const h = promptHeader(text);
  if (!h) return [finding(p, 0, 'P1', `no front matter; a prompt opens with "---", "prompt: ${name}", "prompt_version: <n>", "---"`)];
  const out = [];
  if (h.prompt !== name) out.push(finding(p, 0, 'P1', `front matter must say "prompt: ${name}" (found: ${h.prompt || 'none'})`));
  if (!/^\d+$/.test(h.version || '')) {
    out.push(finding(p, 0, 'P1', `prompt_version must be an integer (found: ${h.version || 'none'})`));
    return out;
  }
  const was = before == null ? null : promptHeader(before);
  const prev = was && /^\d+$/.test(was.version || '') ? Number(was.version) : null;
  if (prev !== null && before.replace(/\r/g, '') !== text.replace(/\r/g, '') && Number(h.version) <= prev)
    out.push(finding(p, 0, 'P1', `changed without a version bump (prompt_version was ${prev}, now ${h.version}); raise it so runs on the new text are told apart`));
  return out;
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
// `show(p)` returns the file's content at the state being checked (the index for --staged, <head> for --range);
// `before(p)`, when given, returns it at the state compared against (HEAD, <base>), for P1's version bump.
// `exists(p)`, when given, says whether a path is tracked at the checked state, for D1 on changed docs.
function checkFiles(branch, files, show, before = null, exists = null) {
  const get = (p) => { try { return show(p) || ''; } catch { return ''; } };
  const prev = (p) => { if (!before) return null; try { const t = before(p); return typeof t === 'string' ? t : null; } catch { return null; } };
  const out = [];
  for (const p of files) {
    out.push(...checkPath(p), ...checkOwnership(branch, p));
    if (readsContent(p)) out.push(...checkContent(p, get(p)));
    if (isPrompt(p)) out.push(...checkPrompt(p, get(p), prev(p)));
    if (exists && isCitingDoc(p)) out.push(...checkCitations(p, get(p), exists));
  }
  if (files.includes(WORKFLOW)) out.push(...checkWorkflow(get(WORKFLOW)));
  if (['CLAUDE.md', 'AGENTS.md', MANIFEST].some((p) => files.includes(p)))
    out.push(...checkGuidance(get('CLAUDE.md'), get('AGENTS.md')), ...checkManifest(get(MANIFEST), get('AGENTS.md')));
  if (files.includes(LEDGER)) out.push(...checkLedger(get(LEDGER)));
  if ([BUILD_PROPS, EDITORCONFIG].some((p) => files.includes(p))) out.push(...checkRatchet(get(BUILD_PROPS), get(EDITORCONFIG)));
  return out;
}

const git = (args) => execFileSync('git', args, { cwd: root, maxBuffer: 1 << 28, stdio: ['ignore', 'pipe', 'pipe'] });
const zlist = (buf) => buf.toString('utf8').split('\0').filter(Boolean);
const readsContent = (p) => p.endsWith('.cs') || p === CORE_ASMDEF;
const currentBranch = () => { try { return git(['rev-parse', '--abbrev-ref', 'HEAD']).toString().trim(); } catch { return ''; } };
const hooksPath = () => { try { return git(['config', '--get', 'core.hooksPath']).toString(); } catch { return ''; } };
// GitHub Actions sets GITHUB_ACTIONS=true. There the workflow is the gate, so H1 has nothing to report (README, H1).
const inCI = () => process.env.GITHUB_ACTIONS === 'true';

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
  const index = pathIndex(zlist(git(['ls-files', '-z'])));
  return checkFiles(currentBranch(), files, (p) => git(['show', ':' + p]).toString('utf8'), (p) => git(['show', 'HEAD:' + p]).toString('utf8'), index);
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
  const branch = asBranch || currentBranch();
  const index = pathIndex(zlist(git(['ls-tree', '-r', '--name-only', '-z', head])));
  const out = checkFiles(branch, files, (p) => git(['show', `${head}:${p}`]).toString('utf8'), (p) => git(['show', `${base}:${p}`]).toString('utf8'), index);
  out.push(...checkHook(hooksPath(), branch === 'main' ? 'error' : 'warn', inCI()));
  return out;
}

function all() {
  const files = zlist(git(['ls-files', '-z']));
  const read = (p) => { const abs = path.join(root, p); return fs.existsSync(abs) ? fs.readFileSync(abs, 'utf8') : null; };
  const out = [];
  for (const p of files) {
    out.push(...checkPath(p));
    if (readsContent(p)) { const text = read(p); if (text !== null) out.push(...checkContent(p, text)); }
    if (isPrompt(p)) { const text = read(p); if (text !== null) out.push(...checkPrompt(p, text)); }
  }
  if (!files.includes(CORE_ASMDEF)) out.push(finding(CORE_ASMDEF, 0, 'R4', 'core asmdef is not tracked; the engine-free flag cannot be checked'));
  out.push(...checkGuidance(read('CLAUDE.md') || '', read('AGENTS.md') || ''));
  out.push(...checkManifest(read(MANIFEST) || '', read('AGENTS.md') || ''));
  const ledger = read(LEDGER);
  if (ledger !== null) out.push(...checkLedger(ledger));
  out.push(...checkRatchet(read(BUILD_PROPS) || '', read(EDITORCONFIG) || ''));
  out.push(...checkWorkflow(read(WORKFLOW) || ''));
  const index = pathIndex(files);
  for (const p of files.filter(isCitingDoc)) { const text = read(p); if (text !== null) out.push(...checkCitations(p, text, index)); }
  // V2 always reads main: its ledger and its merges. On another branch (an agent worktree) it is a warning.
  const onMain = currentBranch() === 'main';
  const mainLedger = onMain ? ledger : showAt('main', LEDGER);
  if (mainLedger !== null) out.push(...checkMergeRecords(mergesSinceLedger(onMain ? 'HEAD' : 'main'), mainLedger, onMain ? 'error' : 'warn'));
  out.push(...checkHook(hooksPath(), onMain ? 'error' : 'warn', inCI()));
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
  const props = (ids, extra = '') =>
    `<Project>\n  <!-- promote with <WarningsAsErrors>$(WarningsAsErrors);CA0000</WarningsAsErrors> -->\n  <PropertyGroup>\n` +
    `    <EnableNETAnalyzers>true</EnableNETAnalyzers>\n    <WarningsAsErrors>$(WarningsAsErrors);${ids.join(';')}</WarningsAsErrors>\n${extra}  </PropertyGroup>\n</Project>\n`;
  const editorconfig = (sev = () => 'warning') =>
    'root = true\n\n[*.cs]\n# NaN compared with ==\n' + ['CA1305', ...PROMOTED].map((id) => `dotnet_diagnostic.${id}.severity = ${sev(id)}\n`).join('');
  const [FIRST] = PROMOTED;
  const PROMPT = PROMPTS + 'agent-task.md';
  const pf = (version, body = 'You are the {{app}} agent.', name = 'agent-task') =>
    `---\nprompt: ${name}\nprompt_version: ${version}\nused-by: tools/orchestrate/agent-loop.mjs\n---\n${body}\n`;
  const MERGE = 'c3d4e5f';
  const verdict = (o) => JSON.stringify({
    task: 'T-TER-045', app: 'terrarium', verdict: 'verified', reason: null, base: 'a1b2c3d', head: 'b2c3d4e', merge: MERGE,
    date: '2026-10-05', checks: PASS, guardOut: 'guard: 0 error(s), 1 warning(s)', w1: [], judge: null, ...o,
  });
  const workflow = (cmds) =>
    'name: guard\n# re-runs the guard on every push\non:\n  push:\njobs:\n  guard:\n    runs-on: ubuntu-latest\n    steps:\n' +
    cmds.map((c) => `      - run: ${c.startsWith('tools/') ? 'node ' + c : c}\n`).join('');
  const tracked = pathIndex(['docs/PLAN.md', 'docs/plans/terrarium.md', 'tools/guard/check.mjs', 'shared/assets/art-reference/A1-03-night-moss-1.png']);
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
    ['H1 hooks unset on main', checkHook('', 'error'), 'H1'],
    ['H1 hooks installed on main', checkHook('tools/guard/hooks', 'error'), null],
    ['B1 ratchet in sync', checkRatchet(props(PROMOTED), editorconfig()), null],
    ['B1 CRLF in sync', checkRatchet(props(PROMOTED).replace(/\n/g, '\r\n'), editorconfig().replace(/\n/g, '\r\n')), null],
    ['B1 error severity is fine', checkRatchet(props(PROMOTED), editorconfig(() => 'error')), null],
    ['B1 a pending rule may stay a warning', checkRatchet(props(PROMOTED), editorconfig((id) => (id === 'CA1305' ? 'suggestion' : 'warning'))), null],
    ['B1 props missing', checkRatchet('', editorconfig()), 'B1'],
    ['B1 a promoted id dropped from WarningsAsErrors', checkRatchet(props(PROMOTED.slice(1)), editorconfig()), 'B1'],
    ['B1 a promoted id only in a comment', checkRatchet(props(PROMOTED.slice(1)).replace('CA0000', FIRST), editorconfig()), 'B1'],
    ['B1 a promoted id in NoWarn', checkRatchet(props(PROMOTED, `    <NoWarn>$(NoWarn);${FIRST}</NoWarn>\n`), editorconfig()), 'B1'],
    ['B1 a promoted id turned down', checkRatchet(props(PROMOTED), editorconfig((id) => (id === FIRST ? 'suggestion' : 'warning'))), 'B1'],
    ['B1 a promoted id turned off by a later line', checkRatchet(props(PROMOTED), editorconfig() + `dotnet_diagnostic.${FIRST}.severity = none\n`), 'B1'],
    ['B1 a promoted id with no severity line', checkRatchet(props(PROMOTED), editorconfig().replace(new RegExp(`dotnet_diagnostic\\.${FIRST}\\.[^\\n]*\\n`), '')), 'B1'],
    ['range: the other app on an agent branch', checkFiles('agent/terrarium', ['apps/sundial/Assets/Editor/SceneSetup.cs'], () => 'class S {}'), 'W1'],
    ['range: raw input in a changed file', checkFiles('main', [app], () => 'var k = Keyboard.current;'), 'R1'],
    ['range: a committed Library file', checkFiles('agent/sundial', ['apps/sundial/Library/ArtifactDB'], () => ''), 'A1'],
    ['range: guidance drift in a changed file', checkFiles('main', ['CLAUDE.md'],
      (p) => ({ 'CLAUDE.md': doc(['a', 'x']), 'AGENTS.md': doc(['a']), [MANIFEST]: manifest('AGENTS.md', ['a']) })[p]), 'G1'],
    ['range: a bad ledger line in a changed file', checkFiles('main', [LEDGER], () => '{oops\n'), 'V1'],
    ['range: a ratchet demotion in a changed file', checkFiles('main', [EDITORCONFIG],
      (p) => ({ [BUILD_PROPS]: props(PROMOTED), [EDITORCONFIG]: editorconfig((id) => (id === FIRST ? 'none' : 'warning')) })[p]), 'B1'],
    ['range: the ratchet in sync after a props change', checkFiles('main', [BUILD_PROPS],
      (p) => ({ [BUILD_PROPS]: props(PROMOTED), [EDITORCONFIG]: editorconfig() })[p]), null],
    ['range: a clean change', checkFiles('agent/sundial', ['apps/sundial/Assets/Scripts/Dial.cs'], () => 'class Dial {}'), null],
    ['P1 a prompt with its header', checkPrompt(PROMPT, pf(1)), null],
    ['P1 CRLF header', checkPrompt(PROMPT, pf(3).replace(/\n/g, '\r\n')), null],
    ['P1 a new prompt needs no bump', checkPrompt(PROMPT, pf(1), null), null],
    ['P1 a change with a bump', checkPrompt(PROMPT, pf(2, 'Read AGENTS.md first.'), pf(1)), null],
    ['P1 a line-ending-only change needs no bump', checkPrompt(PROMPT, pf(1), pf(1).replace(/\n/g, '\r\n')), null],
    ['P1 no front matter', checkPrompt(PROMPT, 'You are the agent.\n'), 'P1'],
    ['P1 front matter names another prompt', checkPrompt(PROMPT, pf(1, 'x', 'agent-continue')), 'P1'],
    ['P1 version not an integer', checkPrompt(PROMPT, pf('1.2')), 'P1'],
    ['P1 version missing', checkPrompt(PROMPT, '---\nprompt: agent-task\n---\nbody\n'), 'P1'],
    ['P1 a change without a bump', checkPrompt(PROMPT, pf(1, 'Read AGENTS.md first.'), pf(1)), 'P1'],
    ['P1 a change that lowers the version', checkPrompt(PROMPT, pf(1, 'Read AGENTS.md first.'), pf(2)), 'P1'],
    ['range: a prompt changed without a bump', checkFiles('main', [PROMPT], () => pf(1, 'new text'), () => pf(1)), 'P1'],
    ['range: a prompt changed with a bump', checkFiles('main', [PROMPT], () => pf(2, 'new text'), () => pf(1)), null],
    ['range: a new prompt file', checkFiles('main', [PROMPT], () => pf(1), () => { throw new Error('absent at base'); }), null],
    ['range: a prompt header is checked with no before reader', checkFiles('main', [PROMPT], () => 'no header'), 'P1'],
    ['range: the prompts README is not a prompt', checkFiles('main', [PROMPTS + 'README.md'], () => '# prompts'), null],
    ['range: a prompt in a subfolder is not a prompt', checkFiles('main', [PROMPTS + 'old/agent-task.md'], () => 'no header'), null],
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
    ['H1 not reported in CI, where the workflow is the gate', checkHook('', 'error', true), null],
    ['C1 workflow in place', checkWorkflow(workflow(CI_REQUIRED)), null],
    ['C1 CRLF workflow in place', checkWorkflow(workflow(CI_REQUIRED).replace(/\n/g, '\r\n')), null],
    ['C1 workflow missing', checkWorkflow(''), 'C1'],
    ['C1 the core tests dropped', checkWorkflow(workflow(CI_REQUIRED.slice(0, 3))), 'C1'],
    ['C1 the range run commented out', checkWorkflow(workflow(CI_REQUIRED).replace('      - run: node tools/guard/check.mjs --range', '      # - run: node tools/guard/check.mjs --range')), 'C1'],
    ['range: a workflow change that drops a check', checkFiles('main', [WORKFLOW], () => workflow(CI_REQUIRED.slice(1))), 'C1'],
    ['D1 a tracked file', checkCitations('docs/PLAN.md', 'See `tools/guard/check.mjs`.', tracked), null],
    ['D1 a tracked folder', checkCitations('docs/PLAN.md', 'Frames in `shared/assets/art-reference/`.', tracked), null],
    ['D1 a linked tracked file', checkCitations('README.md', 'Read [the plan](docs/PLAN.md).', tracked), null],
    ['D1 a file with a line number', checkCitations('docs/PLAN.md', 'At `tools/guard/check.mjs:12`, see.', tracked), null],
    ['D1 an untracked folder', checkCitations('README.md', 'Provenance in [`docs/contest/`](docs/contest/).', tracked), 'D1'],
    ['D1 an untracked file', checkCitations('docs/plans/terrarium.md', 'Driver in `docs/contest/seed-unity/FidelityHero.cs`.', tracked), 'D1'],
    ['D1 placeholders are skipped', checkCitations('docs/PLAN.md', '`docs/plans/<app>.md`, `apps/*/Assets`, `docs/decisions/0001..0003`, `x-{1,2}.png`', tracked), null],
    ['D1 orchestration is untracked by design', checkCitations('docs/PLAN.md', 'Queue in `orchestration/queue/terrarium/`.', tracked), null],
    ['D1 an absolute provenance path is not a repo path', checkCitations('docs/PLAN.md', 'Contest root `C:\\Users\\x\\.contest\\`.', tracked), null],
    ['D1 a command is not a path', checkCitations('AGENTS.md', 'Run `node tools/guard/check.mjs --all`.', tracked), null],
    ['range: a dead citation in a changed doc', checkFiles('main', ['docs/PLAN.md'], () => 'Read `docs/contest/README.md`.', null, tracked), 'D1'],
    ['range: a live citation in a changed doc', checkFiles('main', ['docs/PLAN.md'], () => 'Read `docs/PLAN.md`.', null, tracked), null],
    ['range: a doc outside the citing set is not read', checkFiles('main', ['docs/knowledge/lessons.md'], () => 'Was `docs/contest/`.', null, tracked), null],
  ];
  let failed = 0;
  for (const [name, found, rule] of cases) {
    const ok = rule === null ? found.length === 0 : found.length > 0 && found.every((f) => f.rule === rule);
    if (!ok) { failed++; console.log(`FAIL ${name}: expected ${rule || 'nothing'}, got ${JSON.stringify(found)}`); }
  }
  const warnOnly = checkOwnership('agent/terrarium', 'apps/sundial/x.cs').every((f) => f.level === 'warn');
  if (!warnOnly) { failed++; console.log('FAIL W1 must be a warning, never an error'); }
  const hookLevels = checkHook('').every((f) => f.level === 'warn') && checkHook('', 'error').every((f) => f.level === 'error');
  if (!hookLevels) { failed++; console.log('FAIL H1 must be an error on main and a warning elsewhere'); }
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
