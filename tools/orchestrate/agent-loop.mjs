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
  for (const n of fs.readdirSync(O('queue', app)).filter((n) => n.endsWith('.md')).sort()) {
    const f = front(fs.readFileSync(O('queue', app, n), 'utf8'));
    if (f.depends.every((d) => depMet(d, done, merged))) return { file: n, ...f };
  }
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
  const flags = ['-m', model, '--reasoning-effort', effort, '--output-format', 'json',
    '--always-approve', '--permission-mode', 'bypassPermissions', '--cwd', wt];
  const once = (args, tag) => new Promise((resolve) => {
    const child = spawn('grok', args, { cwd: wt, env: { ...process.env, GROK_AGENT_DASHBOARD: '0' }, shell: false, windowsHide: true });
    const out = fs.createWriteStream(path.join(runDir, `grok${tag}.json`));
    const err = fs.createWriteStream(path.join(runDir, `grok${tag}.stderr.log`));
    child.stdout.pipe(out); child.stderr.pipe(err);
    const timer = setTimeout(() => { log(`${id}: timeout after ${timeoutMin} min, killing`); child.kill('SIGTERM'); }, timeoutMin * 60000);
    child.on('close', (code) => { clearTimeout(timer); resolve(code); });
    child.on('error', (e) => { clearTimeout(timer); log(`${id}: spawn error ${e.message}`); resolve(-1); });
  });
  const sessionOf = (tag) => { try { const t = fs.readFileSync(path.join(runDir, `grok${tag}.json`), 'utf8'); return JSON.parse(t.slice(t.indexOf('{'))).sessionId; } catch { return null; } };
  const report = path.join(wt, 'orchestration', 'runs', app, id, 'REPORT.md');
  const t0 = Date.now();
  let code = await once(['-p', prompt, ...flags], '');
  // Headless Grok ends its run whenever it ends a turn, including "I'll read the log when Unity finishes" (measured
  // twice: T-SUN-001, T-TER-005). A clean exit with no REPORT.md is resumed in the same session, at most twice.
  let session = sessionOf('');
  for (let n = 1; n <= 2 && code === 0 && !fs.existsSync(report) && session; n++) {
    log(`${id}: exited without REPORT.md, resuming session (continuation ${n})`);
    code = await once(['-r', session, '-p', [
      'Continue the task. Your previous turn ended while you were waiting on a process (likely Unity), so nothing after that ran.',
      'Check whether that process finished (read its log; if it is still running, wait for it in the FOREGROUND, e.g. poll in one blocking command).',
      'Then finish the remaining steps, verify, commit, and write the REPORT.md. Do not end your turn until REPORT.md is committed.',
    ].join('\n'), ...flags], `.cont${n}`);
    session = sessionOf(`.cont${n}`) || session;
  }
  return { id, code, wallS: Math.round((Date.now() - t0) / 1000) };
}

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
log(`loop start: model=${model} effort=${effort} worktree=${wt}`);
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
