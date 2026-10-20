#!/usr/bin/env node
// Claude Code PreToolUse hook: a Claude session's `git commit` and `git merge` run the guard, in any clone, without
// anyone setting core.hooksPath (tools/guard/README.md, "Where it runs").
//
//   node tools/guard/claude-hook.mjs              read the PreToolUse JSON on stdin (.claude/settings.json wires it)
//   node tools/guard/claude-hook.mjs --self-test  each decision against command fixtures; needs no git and no stdin
//
// The pre-commit and pre-merge-commit hooks run only where core.hooksPath points at tools/guard/hooks. That setting is
// shared by the main checkout and every agent worktree, so it is the owner's call. The agent loop wires it for the
// agent's process tree through GIT_CONFIG_* env. This hook does the same for a Claude session (the host, an automation
// lane) without touching any config: when a Bash or PowerShell command would commit or merge with the guard's hooks off,
// it refuses the command (exit 2; Claude reads the reason on stderr) and names the fix, the per-command override
//   git -c core.hooksPath=tools/guard/hooks commit ...
// which makes git run the same hooks for that one command. `--no-verify` is always refused. Anything it cannot read
// (bad stdin, no git, another repo) is let through: a hook that guesses must not block unrelated work, and CI
// (.github/workflows/guard.yml) still re-checks every pushed commit. Plain Node, no dependencies.
import fs from 'node:fs';
import path from 'node:path';
import { spawnSync } from 'node:child_process';

const HOOKS = 'tools/guard/hooks';
const OVERRIDE = `-c core.hooksPath=${HOOKS}`;

// Same comparison as guard rule H1 (checkHook in tools/guard/check.mjs) and install.mjs: relative or absolute, either slash.
const normalise = (v) => (v || '').trim().replace(/\\/g, '/').replace(/\/+$/, '');
export const isOurs = (v) => { const n = normalise(v); return n === HOOKS || n.endsWith('/' + HOOKS); };

// Split a shell line into simple commands at unquoted ; & | newlines and brackets, so `a && git commit`,
// `if ($?) { git commit }` and `$(git commit)` each yield a segment that starts with git. Bash and PowerShell both.
export function segments(command) {
  const out = [];
  let cur = '', quote = '';
  for (let i = 0; i < command.length; i++) {
    const ch = command[i];
    if (quote) {
      if (ch === quote) quote = '';
      else if (ch === '\\' && quote === '"' && i + 1 < command.length) { cur += ch + command[++i]; continue; }
      cur += ch;
      continue;
    }
    if (ch === '"' || ch === "'") { quote = ch; cur += ch; continue; }
    if (ch === '\\' && i + 1 < command.length && command[i + 1] !== '\n') { cur += ch + command[++i]; continue; }
    if (';&|\n\r{}()'.includes(ch)) { if (cur.trim()) out.push(cur); cur = ''; continue; }
    cur += ch;
  }
  if (cur.trim()) out.push(cur);
  return out;
}

// Whitespace-separated words with the quotes removed: `-m "a b"` is two words, `-m` and `a b`.
export function words(segment) {
  const out = [];
  let cur = '', quote = '', has = false;
  for (let i = 0; i < segment.length; i++) {
    const ch = segment[i];
    if (quote) {
      if (ch === quote) quote = '';
      else cur += ch;
      continue;
    }
    if (ch === '"' || ch === "'") { quote = ch; has = true; continue; }
    if (/\s/.test(ch)) { if (has) out.push(cur); cur = ''; has = false; continue; }
    cur += ch; has = true;
  }
  if (has) out.push(cur);
  return out;
}

// Global options git reads before the subcommand; these take the next word as their value.
const GIT_OPT_WITH_VALUE = new Set(['-c', '-C', '--git-dir', '--work-tree', '--namespace', '--super-prefix', '--config-env', '--attr-source']);
const PREFIX_WORDS = new Set(['command', 'exec', 'time', 'sudo', 'nohup', '!', 'then', 'do', 'else']);

// One simple command, if it runs git: its env assignments, -c values, -C directories, subcommand and arguments.
export function gitCall(segment) {
  const w = words(segment);
  const env = {};
  let i = 0;
  for (; i < w.length; i++) {
    const m = /^([A-Za-z_][A-Za-z0-9_]*)=(.*)$/s.exec(w[i]);
    if (m) env[m[1]] = m[2];
    else if (!PREFIX_WORDS.has(w[i])) break;
  }
  if (i >= w.length) return null;
  const exe = w[i].replace(/\\/g, '/').split('/').pop().toLowerCase();
  if (exe !== 'git' && exe !== 'git.exe') return null;
  const configs = [], dirs = [];
  for (i++; i < w.length; i++) {
    const a = w[i];
    if (a === '-c') { if (i + 1 < w.length) configs.push(w[++i]); continue; }
    if (a === '-C') { if (i + 1 < w.length) dirs.push(w[++i]); continue; }
    if (GIT_OPT_WITH_VALUE.has(a)) { i++; continue; }
    if (a.startsWith('-')) continue;
    return { env, configs, dirs, sub: a, args: w.slice(i + 1) };
  }
  return null;
}

// `git commit -n`, `-an`, `-anm msg` and `--no-verify` skip pre-commit; a short-flag cluster stops at the first flag
// that takes a value (-m, -F, -c, -C, -t, -S, -u), because the rest of the word is that value.
export function skipsHooks(call) {
  for (const a of call.args) {
    if (a === '--') break;
    if (a === '--no-verify') return true;
    if (call.sub === 'commit' && /^-[A-Za-z]+$/.test(a)) {
      for (const ch of a.slice(1)) {
        if (ch === 'n') return true;
        if ('mFcCtSu'.includes(ch)) break;
      }
    }
  }
  return false;
}

// The hooks path git would use for this call from its own -c values and GIT_CONFIG_* env (the agent loop's
// mechanism), or null when neither sets it and the repo's config decides.
export function overridden(call, env) {
  let found = null;
  const n = Number(env.GIT_CONFIG_COUNT || 0);
  for (let k = 0; k < n; k++)
    if (String(env[`GIT_CONFIG_KEY_${k}`] || '').toLowerCase() === 'core.hookspath') found = env[`GIT_CONFIG_VALUE_${k}`] || '';
  for (const c of call.configs) {
    const eq = c.indexOf('=');
    if (eq > 0 && c.slice(0, eq).toLowerCase() === 'core.hookspath') found = c.slice(eq + 1);
  }
  return found;
}

// The decision for one command line. `repo(dirs)` answers for the repo the call runs in: `guarded` when it has the
// guard's hook files (otherwise this is not garden-vr, and the call is let through), `hooksPath` from its config.
// `env` is the session's environment. Returns null to allow, or the reason to refuse.
export function decide(command, env, repo) {
  for (const seg of segments(String(command || ''))) {
    const call = gitCall(seg);
    if (!call || (call.sub !== 'commit' && call.sub !== 'merge')) continue;
    if (call.sub === 'merge' && call.args.some((a) => a === '--abort' || a === '--quit')) continue;
    const r = repo(call.dirs);
    if (!r || !r.guarded) continue;
    const callEnv = { ...env, ...call.env };
    if (skipsHooks(call))
      return `guard: refused "git ${call.sub}" with --no-verify. The guard's hook must run on every commit (AGENTS.md, "Before each commit"); fix what it reports instead of bypassing it.`;
    const over = overridden(call, callEnv);
    if (over !== null ? isOurs(over) : isOurs(r.hooksPath)) continue;
    const was = over !== null ? `overridden to "${normalise(over)}"` : r.hooksPath ? `"${normalise(r.hooksPath)}"` : 'unset';
    return `guard: refused "git ${call.sub}": this checkout's core.hooksPath is ${was}, so git would skip tools/guard/hooks/pre-${call.sub === 'merge' ? 'merge-' : ''}commit and the guard would not run. Re-run the same command with the hooks switched on for that one call, without changing any config:\n  git ${OVERRIDE} ${call.sub} ...\n(tools/guard/README.md, "Where it runs").`;
  }
  return null;
}

// The repo a call runs in: cwd, then each -C in turn. Any git failure means "unknown", which is let through.
function repoAt(cwd) {
  return (dirs) => {
    let dir = cwd;
    for (const d of dirs) dir = path.resolve(dir, d);
    const run = (...a) => spawnSync('git', ['-C', dir, ...a], { encoding: 'utf8', windowsHide: true, timeout: 5000 });
    const top = run('rev-parse', '--show-toplevel');
    if (top.error || top.status !== 0) return null;
    const root = top.stdout.trim();
    const guarded = ['pre-commit', 'pre-merge-commit'].every((f) => fs.existsSync(path.join(root, HOOKS, f)));
    const cfg = run('config', '--get', 'core.hooksPath');
    return { guarded, hooksPath: cfg.status === 0 ? cfg.stdout : '' };
  };
}

function hook() {
  let input;
  try { input = JSON.parse(fs.readFileSync(0, 'utf8')); } catch { process.exit(0); }
  const command = input && input.tool_input && input.tool_input.command;
  if (typeof command !== 'string' || !command.trim()) process.exit(0);
  const cwd = (input && typeof input.cwd === 'string' && input.cwd) || process.cwd();
  let reason = null;
  try { reason = decide(command, process.env, repoAt(cwd)); } catch { process.exit(0); }
  if (!reason) process.exit(0);
  process.stderr.write(reason + '\n');
  process.exit(2);
}

function selfTest() {
  const plain = () => ({ guarded: true, hooksPath: '' });
  const installed = () => ({ guarded: true, hooksPath: 'tools/guard/hooks' });
  const installedAbs = () => ({ guarded: true, hooksPath: 'C:\\repo\\tools\\guard\\hooks\\' });
  const husky = () => ({ guarded: true, hooksPath: '.husky' });
  const otherRepo = () => ({ guarded: false, hooksPath: '' });
  const unknown = () => null;
  const loopEnv = { GIT_CONFIG_COUNT: '1', GIT_CONFIG_KEY_0: 'core.hooksPath', GIT_CONFIG_VALUE_0: 'C:/wt/tools/guard/hooks' };
  // [name, command, env, repo, expected: 'allow' | 'refuse' | 'no-verify']
  const cases = [
    ['commit, hooks unset', 'git commit -m "fix: x"', {}, plain, 'refuse'],
    ['commit, hooks installed', 'git commit -m "fix: x"', {}, installed, 'allow'],
    ['commit, hooks installed by absolute path', 'git commit -m x', {}, installedAbs, 'allow'],
    ['commit, another hooks path', 'git commit -m x', {}, husky, 'refuse'],
    ['commit with the -c override', `git ${OVERRIDE} commit -m "fix: x"`, {}, plain, 'allow'],
    ['-c override with a backslash path', 'git -c core.hooksPath=tools\\guard\\hooks commit -m x', {}, plain, 'allow'],
    ['-c override key in another case', 'git -c core.hookspath=tools/guard/hooks commit -m x', {}, plain, 'allow'],
    ['-c override to another path', 'git -c core.hooksPath=/dev/null commit -m x', {}, installed, 'refuse'],
    ['-c override to empty disables the hooks', 'git -c core.hooksPath= commit -m x', {}, installed, 'refuse'],
    ['merge, hooks unset', 'git merge --no-ff agent/sundial -m "merge agent/sundial: x"', {}, plain, 'refuse'],
    ['merge with the -c override', `git ${OVERRIDE} merge --no-ff agent/sundial`, {}, plain, 'allow'],
    ['merge --abort is not a commit', 'git merge --abort', {}, plain, 'allow'],
    ['compound bash line', 'git add -A && git commit -m "fix: y"', {}, plain, 'refuse'],
    ['compound bash line with the override', `git add -A && git ${OVERRIDE} commit -m "fix: y"`, {}, plain, 'allow'],
    ['PowerShell statement chain', "git add -A; if ($?) { git commit -m 'fix: y' }", {}, plain, 'refuse'],
    ['PowerShell call operator', "& git commit -m 'x'", {}, plain, 'refuse'],
    ['PowerShell git.exe by path', '& "C:/Program Files/Git/cmd/git.exe" commit -m x', {}, plain, 'refuse'],
    ['subshell', 'echo $(git commit -m x)', {}, plain, 'refuse'],
    ['git -C another folder', 'git -C ../other commit -m x', {}, plain, 'refuse'],
    ['git -C a repo without the guard', 'git -C ../other commit -m x', {}, otherRepo, 'allow'],
    ['repo cannot be read', 'git commit -m x', {}, unknown, 'allow'],
    ['the agent loop env sets the hooks', 'git commit -m x', loopEnv, plain, 'allow'],
    ['inline env assignment sets the hooks', 'GIT_CONFIG_COUNT=1 GIT_CONFIG_KEY_0=core.hooksPath GIT_CONFIG_VALUE_0=tools/guard/hooks git commit -m x', {}, plain, 'allow'],
    ['log mentioning commit', 'git log --grep commit --oneline', {}, plain, 'allow'],
    ['status', 'git status', {}, plain, 'allow'],
    ['a quoted git commit is text, not a call', 'gh pr create --title "git commit hooks"', {}, plain, 'allow'],
    ['commit message that mentions merge', 'git log -1 --format=%s merge', {}, plain, 'allow'],
    ['not git at all', 'node tools/guard/check.mjs --staged', {}, plain, 'allow'],
    ['empty command', '', {}, plain, 'allow'],
    ['--no-verify with hooks installed', 'git commit --no-verify -m x', {}, installed, 'no-verify'],
    ['-n with hooks installed', 'git commit -n -m x', {}, installed, 'no-verify'],
    ['-anm cluster', 'git commit -anm x', {}, installed, 'no-verify'],
    ['-am cluster is not -n', 'git commit -am "no verify"', {}, installed, 'allow'],
    ['-m value holding n is not -n', 'git commit -mn', {}, installed, 'allow'],
    ['merge --no-verify with the override', `git ${OVERRIDE} merge --no-verify agent/terrarium`, {}, plain, 'no-verify'],
    ['merge -n is --no-stat, not --no-verify', 'git merge -n agent/terrarium', {}, installed, 'allow'],
  ];
  let failed = 0;
  for (const [name, command, env, repo, want] of cases) {
    let got;
    try {
      const r = decide(command, env, repo);
      got = r === null ? 'allow' : /--no-verify/.test(r) ? 'no-verify' : 'refuse';
    } catch (e) { got = `threw ${e.message}`; }
    if (got !== want) { failed++; console.log(`FAIL ${name}: expected ${want}, got ${got}`); }
  }
  const reason = decide('git commit -m x', {}, plain) || '';
  if (!reason.includes(`git ${OVERRIDE} commit`)) { failed++; console.log('FAIL the refusal must name the -c override to re-run with'); }
  console.log(`claude-hook self-test: ${cases.length + 1 - failed} passed, ${failed} failed`);
  process.exit(failed ? 1 : 0);
}

const mode = process.argv[2];
if (mode === '--self-test') selfTest();
else if (mode === undefined) hook();
else { console.error('usage: node tools/guard/claude-hook.mjs [--self-test]   (without arguments it reads a Claude Code PreToolUse event on stdin)'); process.exit(2); }
