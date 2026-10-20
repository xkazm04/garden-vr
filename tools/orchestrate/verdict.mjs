#!/usr/bin/env node
// Writes one host verdict to docs/knowledge/verifications.jsonl (docs/AI_HARNESS.md, Definition of Done step 9), so the
// record is built by the tooling instead of typed by hand.
//
//   node tools/orchestrate/verdict.mjs --app <app> --task <id> --merge <sha> [checks]      after a verified merge
//   node tools/orchestrate/verdict.mjs --app <app> --task <id> --reject "<reason>" [checks] for a rejection (no merge)
//   node tools/orchestrate/verdict.mjs --self-test                                          the line rules; needs no git
//
//   [checks]  --oracles --acceptance --core --evidence --report --postMerge  pass | fail | n/a, one per check the host
//             re-ran itself (steps 1, 2, 3, 6, 7, 8). A check left out is recorded as "not run", and "not run" is never
//             a pass, so a verified line cannot be written until every one is given.
//   other     --base <sha> --head <sha>   default: the task's line in orchestration/runs/<app>/tasks.jsonl
//             --worktree <path>           default: the worktree that has agent/<app> checked out
//             --scope pass|fail|n/a       default: measured (below)
//             --judge-script <path> --judge-model <model> --judge-median <score>   when the A3 judge ran
//             --date YYYY-MM-DD           default: today
//             --dry-run                   print the line, write nothing
//
// What it measures itself, never on the agent's word: step 4 (guard) runs `tools/guard/check.mjs --all` and
// `--range <base>..<head> --as agent/<app>` in the agent's worktree, which must be a clean checkout at <head>. Step 5
// (scope) passes when every W1 path of that range run is named by the task file's `touches:`. The judge's blob is
// `git hash-object` of its script, which pins the prompt it ran. The line is checked against the rules of guard V1
// before it is written; V1 in tools/guard/check.mjs stays the authority and re-checks it when the ledger is committed.
// The tool appends, it never commits: the host commits the line on main right after the merge.
import fs from 'node:fs';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const main = path.resolve(here, '..', '..');
const LEDGER_REL = 'docs/knowledge/verifications.jsonl';
const LEDGER = path.join(main, ...LEDGER_REL.split('/'));

// The ledger's shape, as guard rule V1 reads it (tools/guard/check.mjs, DOD_CHECKS and checkLedger).
const DOD_CHECKS = ['oracles', 'acceptance', 'core', 'guard', 'scope', 'evidence', 'report', 'postMerge'];
const CHECK_VALUES = ['pass', 'fail', 'not run', 'n/a'];
const ATTESTED = ['oracles', 'acceptance', 'core', 'evidence', 'report', 'postMerge'];
const SHA = /^[0-9a-f]{7,64}$/;
const TASK = /^T-[A-Z]+-\d+$/;
const APPS = ['terrarium', 'sundial'];
const VALUED = ['app', 'task', 'merge', 'reject', 'base', 'head', 'worktree', 'date', 'scope', ...ATTESTED,
  'judge-script', 'judge-model', 'judge-median'];
const SWITCHES = ['dry-run', 'self-test'];

const USAGE = 'usage: node tools/orchestrate/verdict.mjs --app terrarium|sundial --task <T-XXX-nnn> (--merge <sha> | --reject "<reason>")\n' +
  '         [--oracles|--acceptance|--core|--evidence|--report|--postMerge pass|fail|n/a] [--scope pass|fail|n/a]\n' +
  '         [--base <sha> --head <sha>] [--worktree <path>] [--judge-script <path> --judge-model <m> --judge-median <n>]\n' +
  '         [--date YYYY-MM-DD] [--dry-run]\n' +
  '       node tools/orchestrate/verdict.mjs --self-test';

function parseArgs(argv) {
  const opts = {}, flags = new Set();
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    const k = a.startsWith('--') ? a.slice(2) : null;
    if (k && SWITCHES.includes(k)) { flags.add(k); continue; }
    if (!k || !VALUED.includes(k)) throw new Error(`unknown argument: ${a}`);
    if (i + 1 >= argv.length) throw new Error(`missing value for ${a}`);
    opts[k] = argv[++i];
  }
  return { opts, flags };
}

// ---- the line: pure functions, covered by --self-test ----

function buildEntry(o) {
  const checks = o.checks || {};
  return {
    task: o.task,
    app: o.app,
    verdict: o.verdict,
    reason: o.verdict === 'rejected' ? (o.reason ?? null) : null,
    base: o.base,
    head: o.head,
    merge: o.verdict === 'verified' ? (o.merge || null) : null,
    date: o.date,
    checks: Object.fromEntries(DOD_CHECKS.map((k) => [k, checks[k] || 'not run'])),
    guardOut: o.guardOut || null,
    w1: o.w1 || [],
    judge: o.judge || null,
  };
}

// The V1 rules, applied before anything is written. An empty list means the line is valid.
function problems(e) {
  const out = [];
  if (!TASK.test(e.task || '')) out.push(`task must be a task id such as T-TER-012 (found: ${e.task})`);
  if (!APPS.includes(e.app)) out.push(`app must be terrarium or sundial (found: ${e.app})`);
  if (!['verified', 'rejected'].includes(e.verdict)) out.push(`verdict must be verified or rejected (found: ${e.verdict})`);
  if (!/^\d{4}-\d{2}-\d{2}/.test(e.date || '')) out.push(`date must start YYYY-MM-DD (found: ${e.date})`);
  for (const k of ['base', 'head']) if (!SHA.test(e[k] || '')) out.push(`${k} must be a commit sha (found: ${e[k]})`);
  const checks = e.checks && typeof e.checks === 'object' ? e.checks : {};
  for (const k of DOD_CHECKS)
    if (!CHECK_VALUES.includes(checks[k])) out.push(`checks.${k} must be one of ${CHECK_VALUES.join(' | ')} (found: ${checks[k]})`);
  if (e.judge != null && (typeof e.judge.script !== 'string' || !SHA.test(e.judge.blob || '')))
    out.push('judge must be null or name the judge script and its git blob sha');
  if (e.verdict === 'rejected' && !(typeof e.reason === 'string' && e.reason.trim())) out.push('a rejected verdict needs a reason');
  if (e.verdict === 'verified') {
    if (!SHA.test(e.merge || '')) out.push('a verified verdict needs the merge sha on main (--merge <sha>)');
    const open = DOD_CHECKS.filter((k) => checks[k] !== 'pass' && checks[k] !== 'n/a');
    if (open.length)
      out.push(`verified with checks that did not pass: ${open.join(', ')} ("not run" is never a pass; give --<check> pass or n/a for each one you re-ran)`);
    if (!/^guard: 0 error/.test(e.guardOut || '')) out.push(`a verified verdict needs the guard re-run to show 0 errors (found: ${e.guardOut || 'not run'})`);
  }
  return out;
}

// W1 paths from guard output. A finding prints as `path W1 (warning) message`; a path may contain spaces.
function w1Paths(out) {
  const paths = [];
  for (const l of (out || '').split(/\r?\n/)) {
    const m = /^(.+?) W1 \(warning\) /.exec(l);
    if (m && !paths.includes(m[1])) paths.push(m[1]);
  }
  return paths;
}
const summaryLine = (out) => ((out || '').split(/\r?\n/).find((l) => l.startsWith('guard: ') && /error\(s\)/.test(l)) || '').trim();

// The task file's `touches:` list, inline (`touches: [a, b]`) or as a block of `- item` lines; null when it has none.
function touchesOf(text) {
  const m = (text || '').replace(/\r/g, '').match(/^---\n([\s\S]*?)\n---/);
  if (!m) return null;
  const lines = m[1].split('\n');
  const i = lines.findIndex((l) => /^touches:/.test(l));
  if (i < 0) return null;
  const clean = (s) => s.trim().replace(/^['"`]|['"`]$/g, '');
  const inline = lines[i].replace(/^touches:\s*/, '').trim();
  if (inline) return inline.replace(/^\[|\]$/g, '').split(',').map(clean).filter(Boolean);
  const out = [];
  for (const l of lines.slice(i + 1)) {
    const item = /^\s+-\s+(.+)$/.exec(l);
    if (!item) break;
    out.push(clean(item[1]));
  }
  return out;
}

function globToRegExp(g) {
  let re = '';
  for (let i = 0; i < g.length; i++) {
    const c = g[i];
    if (c === '*') {
      if (g[i + 1] === '*') { re += '.*'; i++; if (g[i + 1] === '/') i++; } else re += '[^/]*';
    } else if (c === '?') re += '[^/]';
    else re += c.replace(/[.+^${}()|[\]\\]/g, '\\$&');
  }
  return new RegExp('^' + re + '$');
}
// A path is covered when a `touches:` entry names it, a folder above it, or a glob that matches it.
function covered(p, touches) {
  return touches.some((t) => {
    const q = t.replace(/\\/g, '/').replace(/^\.\//, '');
    if (/[*?]/.test(q)) return globToRegExp(q).test(p);
    const d = q.replace(/\/+$/, '');
    return p === d || p.startsWith(d + '/');
  });
}

// ---- the record: reads git and the orchestration folders ----

const run = (cmd, args, cwd = main) => spawnSync(cmd, args, { cwd, encoding: 'utf8', windowsHide: true, maxBuffer: 1 << 26 });
const die = (msg) => { console.error(`verdict: ${msg}`); process.exit(1); };
const today = () => { const d = new Date(); const p = (n) => String(n).padStart(2, '0'); return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}`; };

// The agent loop's line for this task: base and head as recorded at dispatch and after the run.
function dispatchOf(app, task) {
  const file = path.join(main, 'orchestration', 'runs', app, 'tasks.jsonl');
  if (!fs.existsSync(file)) return null;
  let found = null;
  for (const raw of fs.readFileSync(file, 'utf8').split(/\r?\n/)) {
    try { const e = JSON.parse(raw); if (e && e.id === task) found = e; } catch { /* a blank or torn line */ }
  }
  return found;
}

function taskFile(app, task) {
  for (const d of ['merged', 'done', 'failed', 'running', path.join('queue', app)]) {
    const dir = path.join(main, 'orchestration', d);
    if (!fs.existsSync(dir)) continue;
    const f = fs.readdirSync(dir).find((n) => n.startsWith(task) && n.endsWith('.md') && !/\d/.test(n.charAt(task.length)));
    if (f) return path.join(dir, f);
  }
  return null;
}

function worktreeOf(branch) {
  const r = run('git', ['worktree', 'list', '--porcelain']);
  if (r.status !== 0) return null;
  let cur = null;
  for (const l of r.stdout.split(/\r?\n/)) {
    if (l.startsWith('worktree ')) cur = l.slice('worktree '.length);
    else if (l === `branch refs/heads/${branch}`) return cur;
  }
  return null;
}

// Step 4, in the agent's worktree: only a clean checkout at the task's head counts (Definition of Done, opening line).
function measureGuard(wt, app, base, head, notes) {
  if (!wt || !fs.existsSync(wt)) { notes.push(`guard not run: no worktree has agent/${app} checked out (pass --worktree <path>)`); return null; }
  const at = run('git', ['-C', wt, 'rev-parse', 'HEAD']);
  const sha = (at.stdout || '').trim();
  if (at.status !== 0 || !sha || !(sha.startsWith(head) || head.startsWith(sha))) {
    notes.push(`guard not run: ${wt} is at ${sha ? sha.slice(0, 10) : 'an unknown commit'}, not the task head ${head.slice(0, 10)}; ` +
      `check the head out on its own (git worktree add --detach <path> ${head.slice(0, 10)}) and pass --worktree <path>`);
    return null;
  }
  const status = run('git', ['-C', wt, 'status', '--porcelain']);
  if (status.status !== 0 || status.stdout.trim()) {
    notes.push(`guard not run: ${wt} is not a clean checkout:\n${(status.stdout || status.stderr || '').trim()}`);
    return null;
  }
  const script = path.join(wt, 'tools', 'guard', 'check.mjs');
  if (!fs.existsSync(script)) { notes.push(`guard not run: ${wt} has no tools/guard/check.mjs`); return null; }
  const guard = (args) => {
    const r = spawnSync(process.execPath, [script, ...args], { cwd: wt, encoding: 'utf8', windowsHide: true, timeout: 300000, maxBuffer: 1 << 26 });
    return { status: r.status, out: `${r.stdout || ''}${r.stderr || ''}${r.error ? `guard: could not run: ${r.error.message}\n` : ''}` };
  };
  return { all: guard(['--all']), range: guard(['--range', `${base}..${head}`, '--as', `agent/${app}`]) };
}

function record(opts, flags) {
  const { app, task } = opts;
  if (!APPS.includes(app) || !TASK.test(task || '')) die(`need --app terrarium|sundial and --task <T-XXX-nnn>\n${USAGE}`);
  if ((opts.merge === undefined) === (opts.reject === undefined)) die('give exactly one of --merge <sha> (verified) or --reject "<reason>"');
  const verdict = opts.merge !== undefined ? 'verified' : 'rejected';

  const dispatch = dispatchOf(app, task) || {};
  const base = opts.base || dispatch.base;
  const head = opts.head || dispatch.head;
  if (!SHA.test(base || '') || !SHA.test(head || ''))
    die(`no base and head for ${task}: it has no line in orchestration/runs/${app}/tasks.jsonl; pass --base <sha> --head <sha>`);

  const checks = {};
  for (const k of [...ATTESTED, 'scope']) {
    if (opts[k] === undefined) continue;
    if (!['pass', 'fail', 'n/a'].includes(opts[k])) die(`--${k} must be pass, fail or n/a (leave it out for "not run")`);
    checks[k] = opts[k];
  }

  let merge = null;
  if (verdict === 'verified') {
    const r = run('git', ['rev-parse', '--verify', '--quiet', `${opts.merge}^{commit}`]);
    merge = (r.stdout || '').trim();
    if (r.status !== 0 || !SHA.test(merge)) die(`--merge ${opts.merge} is not a commit in ${main}`);
    if (run('git', ['merge-base', '--is-ancestor', head, merge]).status !== 0)
      die(`merge ${merge.slice(0, 10)} does not contain the task head ${head.slice(0, 10)}; record the merge that brought it to main`);
  }

  const notes = [];
  let guardOut = null, w1 = [];
  const measured = measureGuard(opts.worktree || worktreeOf(`agent/${app}`), app, base, head, notes);
  if (measured) {
    guardOut = summaryLine(measured.all.out) || null;
    w1 = w1Paths(measured.range.out);
    checks.guard = measured.all.status === 0 && measured.range.status === 0 ? 'pass' : 'fail';
    if (checks.guard === 'fail')
      notes.push(`guard: --all exit ${measured.all.status} (${guardOut || 'no summary'}); --range exit ${measured.range.status} (${summaryLine(measured.range.out) || 'no summary'})`);
    if (opts.scope === undefined) {
      const tf = taskFile(app, task);
      const touches = tf ? touchesOf(fs.readFileSync(tf, 'utf8')) : null;
      if (touches === null) {
        notes.push(`scope not run: no touches: list found for ${task} under orchestration/; read git diff --stat ${base.slice(0, 10)}..${head.slice(0, 10)} and pass --scope`);
      } else {
        const outside = w1.filter((p) => !covered(p, touches));
        checks.scope = outside.length ? 'fail' : 'pass';
        if (outside.length) notes.push(`scope: paths outside the agent's own and outside touches: ${outside.join(', ')}`);
      }
    }
  }

  let judge = null;
  if (opts['judge-script'] !== undefined) {
    const h = run('git', ['hash-object', opts['judge-script']]);
    if (h.status !== 0) die(`cannot hash ${opts['judge-script']}: ${(h.stderr || '').trim()}`);
    const median = opts['judge-median'] === undefined ? null : Number(opts['judge-median']);
    if (median !== null && !Number.isFinite(median)) die('--judge-median must be a number');
    judge = { script: opts['judge-script'].replace(/\\/g, '/'), blob: h.stdout.trim(), model: opts['judge-model'] || null, median };
  }

  const entry = buildEntry({ task, app, verdict, reason: opts.reject, base, head, merge, date: opts.date || today(), checks, guardOut, w1, judge });
  for (const n of notes) console.log(`verdict: ${n}`);
  const bad = problems(entry);
  if (bad.length) {
    for (const b of bad) console.log(`verdict: refused: ${b}`);
    console.log('verdict: nothing written');
    process.exit(1);
  }

  const ledger = fs.existsSync(LEDGER) ? fs.readFileSync(LEDGER, 'utf8') : '';
  for (const raw of ledger.split(/\r?\n/)) {
    try {
      const e = JSON.parse(raw);
      if (e && e.task === task && e.head === entry.head && e.verdict === verdict) die(`${LEDGER_REL} already has a ${verdict} line for ${task} at ${head.slice(0, 10)}`);
    } catch { /* blank line; V1 reports a malformed one */ }
  }

  const line = JSON.stringify(entry);
  console.log(line);
  if (flags.has('dry-run')) { console.log('verdict: --dry-run, nothing written'); return; }
  fs.appendFileSync(LEDGER, (ledger && !ledger.endsWith('\n') ? '\n' : '') + line + '\n');
  console.log(`verdict: appended to ${LEDGER_REL}. Commit it on main now:`);
  console.log(`  git add ${LEDGER_REL} && git commit -m "verify ${task}: ${verdict}"`);
  console.log('verdict: the pre-commit hook and CI run guard rule V1 over the ledger change.');
}

function selfTest() {
  const a = 'a'.repeat(40), b = 'b'.repeat(40), m = 'c'.repeat(40);
  const all = Object.fromEntries(DOD_CHECKS.map((k) => [k, 'pass']));
  const common = { task: 'T-TER-045', app: 'terrarium', base: a, head: b, date: '2026-10-05', guardOut: 'guard: 0 error(s), 1 warning(s)' };
  const v = (over) => buildEntry({ ...common, verdict: 'verified', merge: m, checks: all, ...over });
  const r = (over) => buildEntry({ ...common, verdict: 'rejected', reason: 'acceptance XML had 0 test cases', checks: { guard: 'pass' }, ...over });
  const guardText = [
    'range a..b: 3 changed file(s)',
    'apps/sundial/Assets/TextMesh Pro/x.cs W1 (warning) sundial-owned path on agent/terrarium; allowed only if the task\'s touches: names it',
    'docs/PLAN.md W1 (warning) host-owned path on agent/terrarium; allowed only if the task\'s touches: names it',
    'apps/terrarium/Assets/Scripts/x.cs:4 R1 raw Input/Keyboard/Mouse read',
    'guard: 1 error(s), 2 warning(s)',
    'guard: fix the errors above; tools/guard/README.md says what each rule protects',
  ].join('\n');
  const same = (x, y) => JSON.stringify(x) === JSON.stringify(y);
  const cases = [
    ['verified, every check pass', problems(v({})).length === 0],
    ['verified, evidence n/a', problems(v({ checks: { ...all, evidence: 'n/a' } })).length === 0],
    ['verified with a check not given is refused', problems(v({ checks: { ...all, acceptance: undefined } })).length > 0],
    ['a check not given is recorded as not run', v({ checks: { ...all, acceptance: undefined } }).checks.acceptance === 'not run'],
    ['verified with a failed check is refused', problems(v({ checks: { ...all, core: 'fail' } })).length > 0],
    ['verified without a merge sha is refused', problems(v({ merge: undefined })).length > 0],
    ['verified with guard errors is refused', problems(v({ guardOut: 'guard: 2 error(s), 0 warning(s)' })).length > 0],
    ['verified without a guard run is refused', problems(v({ guardOut: null })).length > 0],
    ['rejected with a reason and open checks', problems(r({})).length === 0],
    ['rejected without a reason is refused', problems(r({ reason: '  ' })).length > 0],
    ['rejected records no merge', r({ merge: m }).merge === null],
    ['verified records no reason', v({ reason: 'x' }).reason === null],
    ['a bad task id is refused', problems(v({ task: 'TER-45' })).length > 0],
    ['an unknown app is refused', problems(v({ app: 'audio' })).length > 0],
    ['a short base is refused', problems(v({ base: 'abc' })).length > 0],
    ['an unknown check value is refused', problems(v({ checks: { ...all, scope: 'ok' } })).length > 0],
    ['a judge without its blob is refused', problems(v({ judge: { script: 'x.py', blob: null } })).length > 0],
    ['a judge with its blob', problems(v({ judge: { script: 'x.py', blob: a, model: 'm', median: 2 } })).length === 0],
    ['the line keeps the ledger field order', Object.keys(v({})).join(',') === 'task,app,verdict,reason,base,head,merge,date,checks,guardOut,w1,judge'],
    ['the checks keep the Definition of Done order', Object.keys(v({}).checks).join(',') === DOD_CHECKS.join(',')],
    ['W1 paths, spaces kept, other rules ignored', same(w1Paths(guardText), ['apps/sundial/Assets/TextMesh Pro/x.cs', 'docs/PLAN.md'])],
    ['no W1 in clean output', same(w1Paths('guard: 0 error(s), 0 warning(s)'), [])],
    ['guard summary line', summaryLine(guardText) === 'guard: 1 error(s), 2 warning(s)'],
    ['touches inline list', same(touchesOf('---\nid: T-TER-1\ntouches: [docs/PLAN.md, "apps/sundial/**"]\n---\n'), ['docs/PLAN.md', 'apps/sundial/**'])],
    ['touches inline list, CRLF', same(touchesOf('---\r\ntouches: [docs/PLAN.md]\r\n---\r\n'), ['docs/PLAN.md'])],
    ['touches empty list', same(touchesOf('---\ntouches: []\n---\n'), [])],
    ['touches block list', same(touchesOf('---\ntouches:\n  - docs/PLAN.md\n  - shared/packages/com.gardenvr.fx/\nid: x\n---\n'), ['docs/PLAN.md', 'shared/packages/com.gardenvr.fx/'])],
    ['touches missing', touchesOf('---\nid: x\n---\n') === null],
    ['no front matter', touchesOf('# T-TER-1\n') === null],
    ['covered: exact file', covered('docs/PLAN.md', ['docs/PLAN.md'])],
    ['covered: folder', covered('shared/packages/com.gardenvr.fx/Runtime/Shaders/FToon.shader', ['shared/packages/com.gardenvr.fx/'])],
    ['covered: glob', covered('apps/sundial/Assets/Editor/SceneSetup.cs', ['apps/sundial/**'])],
    ['not covered: one star stays in its folder', !covered('apps/sundial/Assets/Editor/SceneSetup.cs', ['apps/sundial/*'])],
    ['not covered: a sibling with a shared prefix', !covered('docs/PLAN.md.bak', ['docs/PLAN.md'])],
    ['not covered: empty touches', !covered('docs/PLAN.md', [])],
    ['arguments: a typo is refused', (() => { try { parseArgs(['--postmerge', 'pass']); return false; } catch { return true; } })()],
    ['arguments: values and switches', (() => { const p = parseArgs(['--app', 'sundial', '--dry-run', '--reject', 'no XML']); return p.opts.app === 'sundial' && p.opts.reject === 'no XML' && p.flags.has('dry-run'); })()],
  ];
  let failed = 0;
  for (const [name, ok] of cases) if (!ok) { failed++; console.log(`FAIL ${name}`); }
  console.log(`verdict self-test: ${cases.length - failed} passed, ${failed} failed`);
  process.exit(failed ? 1 : 0);
}

let parsed;
try { parsed = parseArgs(process.argv.slice(2)); } catch (e) { console.error(`verdict: ${e.message}\n${USAGE}`); process.exit(2); }
if (parsed.flags.has('self-test')) selfTest();
else record(parsed.opts, parsed.flags);
