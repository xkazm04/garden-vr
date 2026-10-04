#!/usr/bin/env node
// Runs one Grok agent against its task queue until the queue is empty or a STOP file appears.
//
//   node tools/orchestrate/agent-loop.mjs --app terrarium --worktree C:/Users/kazda/kiro/gvr-terrarium [--model grok-4.7]
//
// Queue lives in the MAIN checkout: orchestration/queue/<app>/*.md (sorted by name). A task whose `depends:` ids are
// not all in orchestration/done/ is skipped until they are. Running a task: the file moves to orchestration/running/,
// Grok runs headless in the agent's worktree, and the task moves to done/ when the agent left
// <worktree>/orchestration/runs/<app>/<id>/REPORT.md, else to failed/. Raw Grok output goes to
// orchestration/runs/<app>/<id>/ in the main checkout (host-side log). Idle: polls every 2 min for new tasks.
// Stop: create orchestration/STOP-<app> (finishes the current task first).
import fs from 'node:fs';
import path from 'node:path';
import { spawn } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const main = path.resolve(here, '..', '..');
const argv = process.argv.slice(2);
const opt = (k, d) => { const i = argv.indexOf('--' + k); return i >= 0 ? argv[i + 1] : d; };
const app = opt('app'); const wt = opt('worktree'); const model = opt('model', 'grok-4.7');
const effort = opt('effort', 'high'); const timeoutMin = Number(opt('timeout-min', 150));
const agyModel = opt('agy-model', 'gemini-3.8-flash-high');
const claudeModel = opt('claude-model', 'claude-sonnet-5-5'); const claudeEffort = opt('claude-effort', 'high');
const engineState = { primary: opt('engine', 'grok') }; // grok | agy
if (!app || !wt) { console.error('need --app and --worktree'); process.exit(1); }

const O = (...p) => path.join(main, 'orchestration', ...p);
for (const d of ['queue/' + app, 'running', 'done', 'merged', 'failed', 'runs/' + app]) fs.mkdirSync(O(d), { recursive: true });
const log = (m) => { const line = `[${new Date().toISOString()}] [${app}] ${m}`; console.log(line); fs.appendFileSync(O('runs', app, 'loop.log'), line + '\n'); };

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
  const prompt = [
    `You are the Garden VR ${app} agent. Your git worktree (work ONLY here): ${wt} on branch agent/${app}.`,
    `First read ${wt}/AGENTS.md, then your task file ${taskPath} (read-only; do not move or edit it).`,
    `Execute the task completely and verify it. Commit atomically on agent/${app} in ${wt} (never push).`,
    `Finish by writing ${wt}/orchestration/runs/${app}/${id}/REPORT.md with the evidence the task asks for, and commit it.`,
    `If you are blocked, still write REPORT.md explaining exactly what blocked you, with the error output.`,
  ].join('\n');
  const report = path.join(wt, 'orchestration', 'runs', app, id, 'REPORT.md');
  const AGY = process.env.AGY || path.join(process.env.LOCALAPPDATA, 'agy', 'bin', 'agy.exe');
  const extra = 'Run every long process (Unity, Blender) in the FOREGROUND and read its log before you continue. Do not end your turn while a process runs.';
  // One engine call. grok: Grok Build CLI. agy: Antigravity CLI (Gemini), which must skip permissions headless or every
  // write_file is auto-denied (measured 2026-10-04: T-TER-042/043 "SUCCESS" with denied write_file and no output).
  const once = (eng, text, resumeId, tag) => new Promise((resolve) => {
    const args = eng === 'agy'
      ? ['-p', text, '--model', agyModel, '--output-format', 'json', '--dangerously-skip-permissions', ...(resumeId ? ['--conversation', resumeId] : [])]
      : eng === 'claude'
      ? [...(resumeId ? ['-r', resumeId] : []), '-p', text, '--model', claudeModel, '--effort', claudeEffort, '--output-format', 'json',
         '--permission-mode', 'bypassPermissions']
      : [...(resumeId ? ['-r', resumeId] : []), '-p', text, '-m', model, '--reasoning-effort', effort, '--output-format', 'json',
         '--always-approve', '--permission-mode', 'bypassPermissions', '--cwd', wt];
    const bin = eng === 'agy' ? AGY : eng === 'claude' ? 'claude' : 'grok';
    // A Claude child must not inherit the parent Claude session's CLAUDECODE / CLAUDE_CODE_* variables, and must bill the
    // subscription, never a leaked ANTHROPIC_API_KEY (both from this machine's own measured failures).
    const env = { ...process.env, GROK_AGENT_DASHBOARD: '0' };
    if (eng === 'claude') for (const k of Object.keys(env)) if (k === 'CLAUDECODE' || k.startsWith('CLAUDE_CODE_') || k === 'ANTHROPIC_API_KEY') delete env[k];
    if (eng === 'claude') env.CLAUDE_CODE_PRINT_BG_WAIT_CEILING_MS = '0';
    const child = spawn(bin, args, { cwd: wt, env, shell: false, windowsHide: true });
    child.stdout.pipe(fs.createWriteStream(path.join(runDir, `${eng}${tag}.json`)));
    child.stderr.pipe(fs.createWriteStream(path.join(runDir, `${eng}${tag}.stderr.log`)));
    // SIGTERM does not reach the process tree on Windows (measured: a 150-min timeout fired 8.7 h late and left children);
    // taskkill /T /F does.
    const timer = setTimeout(() => { log(`${id}: timeout after ${timeoutMin} min, killing tree`); spawn('taskkill', ['/PID', String(child.pid), '/T', '/F'], { windowsHide: true }); }, timeoutMin * 60000);
    child.on('close', (code) => { clearTimeout(timer); resolve(code); });
    child.on('error', (e) => { clearTimeout(timer); log(`${id}: spawn error ${e.message}`); resolve(-1); });
  });
  const sessionOf = (eng, tag) => { try { const s = fs.readFileSync(path.join(runDir, `${eng}${tag}.json`), 'utf8'); const d = JSON.parse(s.slice(s.indexOf('{'))); return d.sessionId || d.session_id || d.conversation_id || null; } catch { return null; } };
  const limited = (eng) => { const b = ['', '.cont1', '.cont2'].map((tag) => { try { return fs.readFileSync(path.join(runDir, `${eng}${tag}.json`), 'utf8') + fs.readFileSync(path.join(runDir, `${eng}${tag}.stderr.log`), 'utf8'); } catch { return ''; } }).join(' '); return /rate.?limit|quota|usage limit|429|402|insufficient|exhausted|too many requests|balance/i.test(b); };
  const t0 = Date.now();
  // Run one engine with up to two same-session continuations when it exits without REPORT.md (headless agents end their
  // run when they end a turn, e.g. while "waiting" for Unity: measured on both Grok and Gemini).
  const attempt = async (eng) => {
    let code = await once(eng, prompt + '\n' + extra, null, '');
    let sid = sessionOf(eng, '');
    for (let n = 1; n <= 2 && code === 0 && !fs.existsSync(report) && sid && !limited(eng); n++) {
      log(`${id}: ${eng} exited without REPORT.md, resuming (continuation ${n})`);
      code = await once(eng, 'Continue the task. Your previous turn ended while you were waiting on a process (likely Unity), so nothing after that ran. Check whether it finished (read its log; if still running, wait in the FOREGROUND), then finish the remaining steps, verify, commit, and write REPORT.md. Do not end your turn until REPORT.md is committed.', sid, `.cont${n}`);
      sid = sessionOf(eng, `.cont${n}`) || sid;
    }
    return code;
  };
  let code;
  const primary = engineState.primary;
  code = await attempt(primary);
  if (primary !== 'agy' && !fs.existsSync(report) && limited(primary)) {
    engineState.primary = 'agy';
    log(`${id}: ${primary} usage limit/balance hit - switching this loop to agy ${agyModel}`);
    code = await attempt('agy');
  }
  return { id, code, wallS: Math.round((Date.now() - t0) / 1000) };
}

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
log(`loop start: engine=${engineState.primary} claude=${claudeModel}@${claudeEffort} grok=${model} agy=${agyModel} worktree=${wt}`);
while (true) {
  if (fs.existsSync(O('STOP-' + app))) { log('STOP file present, exiting'); break; }
  const task = nextTask();
  if (!task) { await sleep(120000); continue; }
  // Pull in whatever the host merged since the last task (shared packages, the other agent's work).
  await new Promise((res) => { const g = spawn('git', ['-C', wt, 'merge', '--no-edit', 'main'], { windowsHide: true });
    let o = ''; g.stdout.on('data', (d) => (o += d)); g.stderr.on('data', (d) => (o += d));
    g.on('close', (code) => { if (code !== 0) { log(`merge main failed, aborting merge: ${o.trim().slice(0, 300)}`); spawn('git', ['-C', wt, 'merge', '--abort']); } else log(`merged main: ${o.trim().split(/\r?\n/)[0]}`); res(); }); });
  fs.renameSync(O('queue', app, task.file), O('running', task.file));
  log(`start ${task.file}`);
  const r = await runGrok(task);
  const report = path.join(wt, 'orchestration', 'runs', app, r.id, 'REPORT.md');
  const ok = r.code === 0 && fs.existsSync(report);
  fs.renameSync(O('running', task.file), O(ok ? 'done' : 'failed', task.file));
  fs.appendFileSync(O('runs', app, 'tasks.jsonl'), JSON.stringify({ ts: new Date().toISOString(), ...r, ok, report: fs.existsSync(report) }) + '\n');
  log(`${ok ? 'done' : 'FAILED'} ${task.file} exit=${r.code} wall=${r.wallS}s report=${fs.existsSync(report)}`);
}
