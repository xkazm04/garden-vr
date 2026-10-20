#!/usr/bin/env node
// Runs one coding agent (Codex, Claude, Grok or agy: --engine) against its task queue until the queue is empty or a STOP file appears.
//
//   node tools/orchestrate/agent-loop.mjs --app sundial --worktree C:/Users/kazda/kiro/gvr-sundial --engine codex [--codex-model gpt-6-astra]
//
// Queue lives in the MAIN checkout: orchestration/queue/<app>/*.md (sorted by name). A task whose `depends:` ids are
// not all in orchestration/done/ is skipped until they are. Running a task: the file moves to orchestration/running/,
// Grok runs headless in the agent's worktree, and the task moves to done/ when the agent left
// <worktree>/orchestration/runs/<app>/<id>/REPORT.md, else to failed/. Raw Grok output goes to
// orchestration/runs/<app>/<id>/ in the main checkout (host-side log), beside guard.txt: the output of
// `node tools/guard/check.mjs --all` and `--range <base>..<head>` run in the worktree after the task (advisory; the host
// rejects in the Definition of Done). Every commit the agent makes runs tools/guard/hooks/pre-commit: the loop sets
// core.hooksPath for the agent's process tree only, through git's GIT_CONFIG_* environment (git 2.31+), so the shared
// config stays the owner's call. The prompts it sends are the versioned files in tools/orchestrate/prompts/ (README
// there); each task's rendered prompt goes to runs/<app>/<id>/prompt.txt and the prompt versions and git blob shas to
// its tasks.jsonl line. Idle: polls every 2 min for new tasks.
// Stop: create orchestration/STOP-<app> (finishes the current task first).
import fs from 'node:fs';
import path from 'node:path';
import { spawn, spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { divergence } from './divergence.mjs';

const here = path.dirname(fileURLToPath(import.meta.url));
const main = path.resolve(here, '..', '..');
const argv = process.argv.slice(2);
const opt = (k, d) => { const i = argv.indexOf('--' + k); return i >= 0 ? argv[i + 1] : d; };
const app = opt('app'); const wt = opt('worktree'); const model = opt('model', 'grok-4.7');
const effort = opt('effort', 'high'); const timeoutMin = Number(opt('timeout-min', 150));
const agyModel = opt('agy-model', 'gemini-3.8-flash-high');
const claudeModel = opt('claude-model', 'claude-sonnet-5-5'); const claudeEffort = opt('claude-effort', 'high');
const codexModel = opt('codex-model', 'gpt-6-astra'); const codexEffort = opt('codex-effort', 'high');
const engineState = { primary: opt('engine', 'grok') }; // grok | claude | codex | agy
if (!app || !wt) { console.error('need --app and --worktree'); process.exit(1); }

const O = (...p) => path.join(main, 'orchestration', ...p);
for (const d of ['queue/' + app, 'running', 'done', 'merged', 'failed', 'runs/' + app]) fs.mkdirSync(O(d), { recursive: true });
const log = (m) => { const line = `[${new Date().toISOString()}] [${app}] ${m}`; console.log(line); fs.appendFileSync(O('runs', app, 'loop.log'), line + '\n'); };

// git in the agent's worktree; the trimmed stdout, or null when git fails (an unset config key exits 1).
const gitWt = (...args) => { const r = spawnSync('git', ['-C', wt, ...args], { encoding: 'utf8', windowsHide: true }); return r.status === 0 ? r.stdout.trim() : null; };
const HOOKS_DIR = path.join(wt, 'tools', 'guard', 'hooks').replace(/\\/g, '/');
const hookConfigured = () => /(^|\/)tools\/guard\/hooks\/?$/.test((gitWt('config', '--get', 'core.hooksPath') || '').replace(/\\/g, '/'));
const hookAvailable = () => fs.existsSync(path.join(HOOKS_DIR, 'pre-commit'));

// The prompt library (tools/orchestrate/prompts/README.md). Read once at start, so a running loop sends one known
// version; relaunch.sh picks up a new one. Each prompt is pinned by its `prompt_version` and its git blob sha, which
// every tasks.jsonl line records, so a change in agent behaviour can be checked against a change in the prompt.
function loadPrompt(name) {
  const file = path.join(here, 'prompts', name + '.md');
  const text = fs.readFileSync(file, 'utf8').replace(/\r/g, '');
  const m = text.match(/^---\n([\s\S]*?)\n---\n?/);
  const get = (k) => m && (m[1].match(new RegExp(`^${k}:[ \\t]*(\\S.*?)[ \\t]*$`, 'm')) || [])[1];
  if (!m || get('prompt') !== name || !/^\d+$/.test(get('prompt_version') || ''))
    throw new Error(`${file}: front matter needs "prompt: ${name}" and an integer "prompt_version" (guard rule P1)`);
  const h = spawnSync('git', ['-C', main, 'hash-object', file], { encoding: 'utf8', windowsHide: true });
  return { name, version: Number(get('prompt_version')), blob: h.status === 0 ? h.stdout.trim() : null,
    body: text.slice(m[0].length).replace(/^\n+/, '').replace(/\s+$/, '') };
}
// Fills {{name}} placeholders; a placeholder left unfilled is a bug in the loop or the prompt, so it refuses.
function render(p, vars) {
  const out = p.body.replace(/\{\{(\w+)\}\}/g, (all, k) => (Object.prototype.hasOwnProperty.call(vars, k) ? String(vars[k]) : all));
  const left = out.match(/\{\{\w+\}\}/);
  if (left) throw new Error(`prompt ${p.name}@v${p.version}: unfilled placeholder ${left[0]}`);
  return out;
}
const PROMPTS = { task: loadPrompt('agent-task'), continue: loadPrompt('agent-continue') };
const promptPins = () => Object.fromEntries(Object.entries(PROMPTS).map(([k, p]) => [k, { version: p.version, blob: p.blob }]));

const front = (txt) => {
  const m = txt.match(/^---\r?\n([\s\S]*?)\r?\n---/); const f = {};
  if (m) for (const l of m[1].split(/\r?\n/)) { const k = l.match(/^(\w+):\s*(.*)$/); if (k) f[k[1]] = k[2]; }
  f.depends = (f.depends || '').replace(/[\[\]]/g, '').split(',').map((s) => s.trim()).filter(Boolean);
  return f;
};
const ids = (dir) => new Set(fs.readdirSync(O(dir)).map((n) => (n.match(/^(T-[A-Z]+-\d+)/) || [])[1]).filter(Boolean));
// A dependency on this agent's own task is met when it is done (the code is already on this branch). A dependency on
// the OTHER agent's task is met only once the host has merged it to main and moved it to merged/ - the code must exist
// here before work that builds on it starts.
const PREFIX = { terrarium: 'T-TER-', sundial: 'T-SUN-' }[app];
const depMet = (d, done, merged) => merged.has(d) || (d.startsWith(PREFIX) && done.has(d));

function nextTask() {
  const done = ids('done'), merged = ids('merged');
  const tasks = fs.readdirSync(O('queue', app)).filter((n) => n.endsWith('.md'))
    .map((n) => ({ file: n, ...front(fs.readFileSync(O('queue', app, n), 'utf8')) }))
    .sort((a, b) => (Number(a.priority ?? 5) - Number(b.priority ?? 5)) || a.file.localeCompare(b.file));
  for (const t of tasks) if (t.depends.every((d) => depMet(d, done, merged))) return t;
  return null;
}

async function runGrok(task) {
  const id = task.id || task.file.replace(/\.md$/, '');
  const runDir = O('runs', app, id); fs.mkdirSync(runDir, { recursive: true });
  const taskPath = O('running', task.file);
  // tools/orchestrate/prompts/agent-task.md; the exact text sent is kept beside the engine output.
  const prompt = render(PROMPTS.task, { app, wt, taskPath, id });
  const continuePrompt = render(PROMPTS.continue, {});
  fs.writeFileSync(path.join(runDir, 'prompt.txt'), prompt + '\n');
  const report = path.join(wt, 'orchestration', 'runs', app, id, 'REPORT.md');
  const AGY = process.env.AGY || path.join(process.env.LOCALAPPDATA, 'agy', 'bin', 'agy.exe');
  // codex: OpenAI Codex CLI, spawned as node + codex.js (the npm shim is a .ps1/.cmd that spawn cannot run without a
  // shell). It reads extra input from stdin unless stdin is closed (measured 2026-10-12), so stdin is 'ignore'.
  const CODEX_JS = process.env.CODEX_JS || path.join(path.dirname(process.execPath), 'node_modules', '@openai', 'codex', 'bin', 'codex.js');
  // One engine call. grok: Grok Build CLI. agy: Antigravity CLI (Gemini), which must skip permissions headless or every
  // write_file is auto-denied (measured 2026-10-04: T-TER-042/043 "SUCCESS" with denied write_file and no output).
  const once = (eng, text, resumeId, tag) => new Promise((resolve) => {
    const args = eng === 'codex'
      ? [CODEX_JS, 'exec', ...(resumeId ? ['resume', resumeId] : []), '-m', codexModel, '-c', `model_reasoning_effort="${codexEffort}"`,
         '--dangerously-bypass-approvals-and-sandbox', '--json', ...(resumeId ? [] : ['-C', wt]), text]
      : eng === 'agy'
      ? ['-p', text, '--model', agyModel, '--output-format', 'json', '--dangerously-skip-permissions', ...(resumeId ? ['--conversation', resumeId] : [])]
      : eng === 'claude'
      ? [...(resumeId ? ['-r', resumeId] : []), '-p', text, '--model', claudeModel, '--effort', claudeEffort, '--output-format', 'json',
         '--permission-mode', 'bypassPermissions']
      : [...(resumeId ? ['-r', resumeId] : []), '-p', text, '-m', model, '--reasoning-effort', effort, '--output-format', 'json',
         '--always-approve', '--permission-mode', 'bypassPermissions', '--cwd', wt];
    const bin = eng === 'codex' ? process.execPath : eng === 'agy' ? AGY : eng === 'claude' ? 'claude' : 'grok';
    // A Claude child must not inherit the parent Claude session's CLAUDECODE / CLAUDE_CODE_* variables, and must bill the
    // subscription, never a leaked ANTHROPIC_API_KEY (both from this machine's own measured failures).
    const env = { ...process.env, GROK_AGENT_DASHBOARD: '0' };
    if (eng === 'claude') for (const k of Object.keys(env)) if (k === 'CLAUDECODE' || k.startsWith('CLAUDE_CODE_') || k === 'ANTHROPIC_API_KEY') delete env[k];
    if (eng === 'claude') env.CLAUDE_CODE_PRINT_BG_WAIT_CEILING_MS = '0';
    // Every commit the agent makes runs the guard's pre-commit hook (AGENTS.md "How to work a task"). An instruction
    // alone did not hold a runner-level habit (docs/knowledge/lessons.md, 2026-10-03), so the runner wires it, scoped to
    // this process tree; a commit made with --no-verify is still caught by the --range run after the task.
    if (hookAvailable()) {
      const n = Number(env.GIT_CONFIG_COUNT || 0);
      env[`GIT_CONFIG_KEY_${n}`] = 'core.hooksPath'; env[`GIT_CONFIG_VALUE_${n}`] = HOOKS_DIR; env.GIT_CONFIG_COUNT = String(n + 1);
    }
    const child = spawn(bin, args, { cwd: wt, env, shell: false, windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'] });
    child.stdout.pipe(fs.createWriteStream(path.join(runDir, `${eng}${tag}.json`)));
    child.stderr.pipe(fs.createWriteStream(path.join(runDir, `${eng}${tag}.stderr.log`)));
    // SIGTERM does not reach the process tree on Windows (measured: a 150-min timeout fired 8.7 h late and left children);
    // taskkill /T /F does.
    const timer = setTimeout(() => { log(`${id}: timeout after ${timeoutMin} min, killing tree`); spawn('taskkill', ['/PID', String(child.pid), '/T', '/F'], { windowsHide: true }); }, timeoutMin * 60000);
    child.on('close', (code) => { clearTimeout(timer); resolve(code); });
    child.on('error', (e) => { clearTimeout(timer); log(`${id}: spawn error ${e.message}`); resolve(-1); });
  });
  // codex writes JSONL; its first event is {"type":"thread.started","thread_id":...}.
  const sessionOf = (eng, tag) => { try { const s = fs.readFileSync(path.join(runDir, `${eng}${tag}.json`), 'utf8');
    if (eng === 'codex') return (s.match(/"thread_id"\s*:\s*"([^"]+)"/) || [])[1] || null; const d = JSON.parse(s.slice(s.indexOf('{'))); return d.sessionId || d.session_id || d.conversation_id || null; } catch { return null; } };
  const limited = (eng) => { const b = ['', '.cont1', '.cont2'].map((tag) => { try { return fs.readFileSync(path.join(runDir, `${eng}${tag}.json`), 'utf8') + fs.readFileSync(path.join(runDir, `${eng}${tag}.stderr.log`), 'utf8'); } catch { return ''; } }).join(' '); return /rate.?limit|quota|usage limit|429|402|insufficient|exhausted|too many requests|balance/i.test(b); };
  const t0 = Date.now();
  // Run one engine with up to two same-session continuations when it exits without REPORT.md (headless agents end their
  // run when they end a turn, e.g. while "waiting" for Unity: measured on both Grok and Gemini).
  const attempt = async (eng) => {
    let code = await once(eng, prompt, null, '');
    let sid = sessionOf(eng, '');
    for (let n = 1; n <= 2 && code === 0 && !fs.existsSync(report) && sid && !limited(eng); n++) {
      log(`${id}: ${eng} exited without REPORT.md, resuming (continuation ${n})`);
      code = await once(eng, continuePrompt, sid, `.cont${n}`);
      sid = sessionOf(eng, `.cont${n}`) || sid;
    }
    return code;
  };
  let code;
  const primary = engineState.primary;
  code = await attempt(primary);
  // Codex runs do not fall back to agy: the owner allowed agy for image generation only (2026-10-12).
  if (primary !== 'agy' && primary !== 'codex' && !fs.existsSync(report) && limited(primary)) {
    engineState.primary = 'agy';
    log(`${id}: ${primary} usage limit/balance hit - switching this loop to agy ${agyModel}`);
    code = await attempt('agy');
  }
  return { id, code, wallS: Math.round((Date.now() - t0) / 1000) };
}

// Runs the AGENTS.md rule guard over the worktree after a task (docs/AI_HARNESS.md, Definition of Done step 4): `--all`
// over every tracked file, and `--range <base>..<head>` over the files this task's commits changed, which adds the W1
// ownership warnings and catches a commit that skipped the hook. Advisory: it never changes done/failed; the host reads
// guard.txt when verifying. Returns both exit codes, null for one that did not run.
function runGuard(id, base, head) {
  const script = path.join(wt, 'tools', 'guard', 'check.mjs');
  if (!fs.existsSync(script)) { log(`${id}: guard not run (no tools/guard/check.mjs on this branch yet)`); return { guard: null, guardRange: null }; }
  const run = (args) => {
    const g = spawnSync(process.execPath, [script, ...args], { cwd: wt, encoding: 'utf8', windowsHide: true, timeout: 120000 });
    const out = `${g.stdout || ''}${g.stderr || ''}${g.error ? `guard: could not run: ${g.error.message}\n` : ''}`;
    return { status: g.status, out, summary: (out.trim().split(/\r?\n/).filter((l) => l.startsWith('guard:'))[0] || '').trim() };
  };
  const a = run(['--all']);
  let text = `$ node tools/guard/check.mjs --all\n${a.out}`;
  let r = null;
  if (base && head && base !== head && fs.readFileSync(script, 'utf8').includes("'--range'")) {
    r = run(['--range', `${base}..${head}`]);
    text += `\n$ node tools/guard/check.mjs --range ${base}..${head}\n${r.out}`;
  }
  fs.mkdirSync(O('runs', app, id), { recursive: true });
  fs.writeFileSync(O('runs', app, id, 'guard.txt'), text);
  log(`${id}: guard exit=${a.status} ${a.summary}${r ? ` | range ${base.slice(0, 10)}..${head.slice(0, 10)} exit=${r.status} ${r.summary}` : ' | range not run'}`);
  return { guard: a.status, guardRange: r ? r.status : null };
}

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
log(`loop start: engine=${engineState.primary} codex=${codexModel}@${codexEffort} claude=${claudeModel}@${claudeEffort} grok=${model} agy=${agyModel} worktree=${wt}`);
log(`prompts: ${Object.values(PROMPTS).map((p) => `${p.name}@v${p.version} ${p.blob ? p.blob.slice(0, 10) : 'blob?'}`).join(', ')}`);
if (!hookAvailable()) log('WARNING: no tools/guard/hooks/pre-commit on this branch; agent commits are not gated until main is merged in');
if (!hookConfigured()) log(`commit hooks: agent commits run ${HOOKS_DIR}/pre-commit through GIT_CONFIG_* env; host commits and merges skip the guard until the owner runs: git config core.hooksPath tools/guard/hooks`);
while (true) {
  if (fs.existsSync(O('STOP-' + app))) { log('STOP file present, exiting'); break; }
  const task = nextTask();
  if (!task) { await sleep(120000); continue; }
  // Fail closed: hundreds of commits ahead of main means main was rewritten under this branch.
  const div = divergence(wt);
  if (div.over) { log(`${div.ahead} commits ahead of main (limit ${div.max}): not merging main, task stays queued; see tools/orchestrate/REANCHOR.md`); break; }
  // Pull in whatever the host merged since the last task (shared packages, the other agent's work).
  await new Promise((res) => { const g = spawn('git', ['-C', wt, 'merge', '--no-edit', 'main'], { windowsHide: true });
    let o = ''; g.stdout.on('data', (d) => (o += d)); g.stderr.on('data', (d) => (o += d));
    g.on('close', (code) => { if (code !== 0) { log(`merge main failed, aborting merge: ${o.trim().slice(0, 300)}`); spawn('git', ['-C', wt, 'merge', '--abort']); } else log(`merged main: ${o.trim().split(/\r?\n/)[0]}`); res(); }); });
  // The dispatch base (PLAN section 5): the host grades `git diff <base>..agent/<app>` and copies base and head into the
  // verification ledger (docs/knowledge/verifications.jsonl).
  const base = gitWt('rev-parse', 'HEAD');
  const hook = hookAvailable() ? (hookConfigured() ? 'config' : 'env') : 'none';
  fs.renameSync(O('queue', app, task.file), O('running', task.file));
  log(`start ${task.file} base=${base} hook=${hook}`);
  const r = await runGrok(task);
  const head = gitWt('rev-parse', 'HEAD');
  const report = path.join(wt, 'orchestration', 'runs', app, r.id, 'REPORT.md');
  const ok = r.code === 0 && fs.existsSync(report);
  // A REPORT that declares itself blocked is not done: it goes to hold/ so nothing that depends on it starts (measured
  // 2026-10-12: T-SUN-063 reported "Status: blocked" and the loop filed it as done, which unblocked B10).
  const blocked = ok && /^\W*status\W*blocked/im.test(fs.readFileSync(report, 'utf8'));
  fs.renameSync(O('running', task.file), O(blocked ? 'hold' : ok ? 'done' : 'failed', task.file));
  let guard = null, guardRange = null;
  try { ({ guard, guardRange } = runGuard(r.id, base, head)); } catch (e) { log(`${r.id}: guard crashed: ${e.message}`); }
  fs.appendFileSync(O('runs', app, 'tasks.jsonl'), JSON.stringify({ ts: new Date().toISOString(), ...r, ok: ok && !blocked, blocked, report: fs.existsSync(report), base, head, hook, guard, guardRange, prompts: promptPins() }) + '\n');
  log(`${blocked ? 'BLOCKED' : ok ? 'done' : 'FAILED'} ${task.file} exit=${r.code} wall=${r.wallS}s report=${fs.existsSync(report)} guard=${guard} guardRange=${guardRange}`);
  // The loop never certifies; it only names the command that writes the host's verdict (docs/AI_HARNESS.md, step 9).
  log(`${r.id}: record the host verdict with: node tools/orchestrate/verdict.mjs --app ${app} --task ${r.id} (--merge <sha> | --reject "<reason>") ` +
    `plus --oracles --acceptance --core --evidence --report --postMerge pass|fail|n/a for each check re-run; base and head are read from tasks.jsonl`);
}
