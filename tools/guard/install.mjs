#!/usr/bin/env node
// One-time setup: point this clone's commit hooks at tools/guard/hooks, so host and human commits and merges run the
// guard at commit time, as agent commits already do (tools/guard/README.md, "Installing the hooks").
//
//   node tools/guard/install.mjs           set core.hooksPath to tools/guard/hooks, unless another hooks path is set
//   node tools/guard/install.mjs --force   replace a different core.hooksPath (for example a leftover .husky)
//   node tools/guard/install.mjs --check   change nothing; exit 1 when the hooks are not installed
//
// Git has no post-clone hook, so this cannot run by itself on clone; run it once per clone. core.hooksPath is shared by
// the main checkout and every agent worktree (each worktree resolves the relative path against its own root, so it
// runs its own branch's hooks), which is why it stays the owner's call. Plain Node, no dependencies.
import fs from 'node:fs';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const HOOKS = 'tools/guard/hooks';
const HOOK_FILES = ['pre-commit', 'pre-merge-commit'];
const args = process.argv.slice(2);
const force = args.includes('--force');
const checkOnly = args.includes('--check');
const unknown = args.filter((a) => !['--force', '--check'].includes(a));
if (unknown.length) {
  console.error(`usage: node tools/guard/install.mjs [--force | --check] (unknown: ${unknown.join(' ')})`);
  process.exit(2);
}

const here = path.dirname(fileURLToPath(import.meta.url));
const git = (...a) => spawnSync('git', a, { cwd: here, encoding: 'utf8', windowsHide: true });
const fail = (msg) => { console.error(`install: ${msg}`); process.exit(1); };

const top = git('rev-parse', '--show-toplevel');
if (top.status !== 0) fail(`not inside a git checkout (${(top.stderr || '').trim()})`);
const root = top.stdout.trim();

// The guard is plain Node; CONTRIBUTING.md asks for Node 20 or newer.
const major = Number(process.versions.node.split('.')[0]);
if (!(major >= 20)) fail(`Node ${process.versions.node} is too old; the guard needs Node 20 or newer`);

const missing = HOOK_FILES.filter((f) => !fs.existsSync(path.join(root, HOOKS, f)));
if (missing.length) fail(`${HOOKS}/${missing.join(', ')} not found in ${root}; is this the garden-vr repo at a commit that has the guard?`);

// Same comparison as guard rule H1 (checkHook in tools/guard/check.mjs): relative or absolute, either slash.
const normalise = (v) => (v || '').trim().replace(/\\/g, '/').replace(/\/+$/, '');
const isOurs = (v) => { const n = normalise(v); return n === HOOKS || n.endsWith('/' + HOOKS); };
const current = () => { const r = git('config', '--get', 'core.hooksPath'); return r.status === 0 ? r.stdout : ''; };

// On Linux and macOS a hook runs only when its file is executable. Git for Windows reads the shebang instead.
function ensureExecutable() {
  if (process.platform === 'win32') return;
  for (const f of HOOK_FILES) {
    const p = path.join(root, HOOKS, f);
    const mode = fs.statSync(p).mode;
    if ((mode & 0o111) === 0) {
      if (checkOnly) { console.log(`install: ${HOOKS}/${f} is not executable, so git skips it here`); continue; }
      fs.chmodSync(p, mode | 0o755);
      console.log(`install: made ${HOOKS}/${f} executable (git update-index --chmod=+x records it for every clone)`);
    }
  }
}

const before = normalise(current());
if (isOurs(before)) {
  ensureExecutable();
  console.log(`install: hooks already installed (core.hooksPath is "${before}"); commits and merges here run the guard`);
  process.exit(0);
}
if (checkOnly) {
  console.log(`install: hooks not installed (core.hooksPath is ${before ? `"${before}"` : 'unset'}); run: node tools/guard/install.mjs`);
  process.exit(1);
}
if (before && !force)
  fail(`core.hooksPath is already "${before}", which is not ${HOOKS}. Re-run with --force to replace it, after checking that nothing else needs it`);

const set = git('config', 'core.hooksPath', HOOKS);
if (set.status !== 0) fail(`git config failed: ${(set.stderr || '').trim()}`);
ensureExecutable();
const after = normalise(current());
if (!isOurs(after)) fail(`core.hooksPath reads "${after}" after setting it; check git config --show-origin core.hooksPath`);
console.log(`install: core.hooksPath ${before ? `changed from "${before}" ` : ''}set to ${HOOKS}`);
console.log('install: every commit now runs pre-commit and every merge runs pre-merge-commit (node tools/guard/check.mjs --staged),');
console.log('install: and guard rule H1 stops failing on main. Check it with: node tools/guard/check.mjs --all');
