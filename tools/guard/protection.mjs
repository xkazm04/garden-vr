#!/usr/bin/env node
// Merge-gate probe: does a red guard check block a merge into main? The workflow in .github/workflows/guard.yml
// re-checks every push, but its red only blocks a merge where main requires its jobs as status checks, which is a
// GitHub setting no clone can read (tools/guard/README.md, "Making CI block a merge"). This asks the GitHub API.
//
//   node tools/guard/protection.mjs              in CI (GITHUB_TOKEN, GITHUB_REPOSITORY): report, per gate job of
//                                                guard.yml, whether main requires it: required, not required, unknown
//   node tools/guard/protection.mjs --self-test  the parsing and the verdict against in-memory fixtures; no network
//
// Advisory: the report always exits 0. A red probe would block nothing either, and it would turn every push red over a
// setting no contributor can change (README, H1). Anything short of "every gate job required" prints a ::warning::
// annotation and a line in the step summary. A setting it cannot read is "unknown", never a pass. Plain Node 20, no
// dependencies.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const WORKFLOW = '.github/workflows/guard.yml';
const BRANCH = 'main';
// The jobs of guard.yml whose red should block a merge. GitHub names a job's check after its `name:`, or its id.
const GATE_JOBS = ['guard', 'core'];

// The check name of each job under `jobs:` in a workflow, as a Map of job id to name. A text read; no YAML parser.
function jobNames(text) {
  const out = new Map();
  let inJobs = false;
  let job = null;
  for (const l of (text || '').replace(/\r/g, '').split('\n')) {
    if (/^\s*#/.test(l) || !l.trim()) continue;
    if (/^\S/.test(l)) { inJobs = /^jobs:\s*(#.*)?$/.test(l); job = null; continue; }
    if (!inJobs) continue;
    const id = /^ {2}([A-Za-z_][\w-]*):\s*(#.*)?$/.exec(l);
    if (id) { job = id[1]; out.set(job, job); continue; }
    const n = job ? /^ {4}name:\s*(.+?)\s*$/.exec(l) : null;
    if (n) out.set(job, n[1].replace(/\s+#.*$/, '').replace(/^(['"])(.*)\1$/, '$2'));
  }
  return out;
}

// The status checks main requires, from the two places GitHub keeps them. `rules` is the body of
// GET /repos/{repo}/rules/branches/main (rulesets), `branch` the body of GET /repos/{repo}/branches/main (classic
// protection); null when the call failed. `known` says which of the two could be read; `bypass` notes a classic
// protection that lets admins merge past the checks.
function requiredContexts(rules, branch) {
  const contexts = new Set();
  const known = { rules: Array.isArray(rules), classic: false };
  let bypass = false;
  if (known.rules) {
    for (const r of rules) {
      if (!r || r.type !== 'required_status_checks') continue;
      const list = (r.parameters && r.parameters.required_status_checks) || [];
      for (const c of list) if (c && typeof c.context === 'string') contexts.add(c.context);
    }
  }
  if (branch && typeof branch === 'object' && typeof branch.protected === 'boolean') {
    if (!branch.protected) known.classic = true;
    else if (branch.protection && typeof branch.protection === 'object') {
      known.classic = true;
      const rsc = branch.protection.required_status_checks;
      if (rsc && rsc.enforcement_level !== 'off') {
        for (const c of rsc.contexts || []) if (typeof c === 'string') contexts.add(c);
        for (const c of rsc.checks || []) if (c && typeof c.context === 'string') contexts.add(c.context);
        if (rsc.enforcement_level === 'non_admins') bypass = true;
      }
    }
  }
  return { contexts, known, bypass };
}

// One row per gate job: required, not required, unknown (a source could not be read), or missing (no such job).
function gate(names, req) {
  const allKnown = req.known.rules && req.known.classic;
  return GATE_JOBS.map((id) => {
    const name = names.get(id);
    if (!name) return { id, name: null, status: 'missing' };
    if (req.contexts.has(name)) return { id, name, status: 'required' };
    return { id, name, status: allKnown ? 'not required' : 'unknown' };
  });
}

// The report lines, and whether a red guard check blocks a merge into main.
function verdict(rows, req, note = '') {
  const blocks = rows.length > 0 && rows.every((r) => r.status === 'required');
  const lines = rows.map((r) => (r.name ? `${r.status}: "${r.name}" (job ${r.id})` : `missing: job ${r.id} is not in ${WORKFLOW}`));
  if (note) lines.push(note);
  let summary;
  if (blocks && req.bypass)
    summary = `a red guard check blocks a merge into ${BRANCH}, except for admins: classic protection enforces it for non-admins only`;
  else if (blocks) summary = `a red guard check blocks a merge into ${BRANCH}`;
  else if (rows.some((r) => r.status === 'unknown'))
    summary = `unknown whether a red guard check blocks a merge into ${BRANCH}; the required-check setting could not be read`;
  else summary = `a red guard check does NOT block a merge into ${BRANCH}: the owner has not made every gate job a required status check (tools/guard/README.md, "Making CI block a merge")`;
  return { blocks, warn: !blocks || req.bypass, lines, summary };
}

async function getJson(url, token) {
  try {
    const res = await fetch(url, {
      headers: {
        Accept: 'application/vnd.github+json',
        Authorization: `Bearer ${token}`,
        'X-GitHub-Api-Version': '2022-11-28',
        'User-Agent': 'garden-vr-merge-gate-probe',
      },
    });
    if (!res.ok) return { body: null, note: `${url} answered HTTP ${res.status}` };
    return { body: await res.json(), note: '' };
  } catch (e) {
    return { body: null, note: `${url} failed: ${e && e.message ? e.message : e}` };
  }
}

function publish(v) {
  for (const l of v.lines) console.log(`merge-gate: ${l}`);
  console.log(`merge-gate: ${v.summary}`);
  if (v.warn) console.log(`::warning title=Merge gate::${v.summary}`);
  const file = process.env.GITHUB_STEP_SUMMARY;
  if (!file) return;
  const md = `### Merge gate\n\n**${v.summary}**\n\n${v.lines.map((l) => `- ${l}`).join('\n')}\n`;
  try { fs.appendFileSync(file, md); } catch { /* the log above already carries it */ }
}

async function probe() {
  let text = '';
  try { text = fs.readFileSync(path.join(root, WORKFLOW), 'utf8'); } catch { text = ''; }
  const names = jobNames(text);
  const token = process.env.GITHUB_TOKEN;
  const repo = process.env.GITHUB_REPOSITORY;
  const api = (process.env.GITHUB_API_URL || 'https://api.github.com').replace(/\/+$/, '');
  if (!token || !repo) {
    const req = requiredContexts(null, null);
    publish(verdict(gate(names, req), req, 'the required-check setting cannot be read from a clone; this runs in CI with GITHUB_TOKEN and GITHUB_REPOSITORY'));
    return;
  }
  const rules = await getJson(`${api}/repos/${repo}/rules/branches/${BRANCH}`, token);
  const branch = await getJson(`${api}/repos/${repo}/branches/${BRANCH}`, token);
  const req = requiredContexts(rules.body, branch.body);
  publish(verdict(gate(names, req), req, [rules.note, branch.note].filter(Boolean).join('; ')));
}

function selfTest() {
  const wf = 'name: guard\n# comment\non:\n  push:\njobs:\n  guard:\n    name: guard (AGENTS.md rules)\n    runs-on: ubuntu-latest\n' +
    '    steps:\n      - name: Guard self-test\n        run: node x\n\n  core:\n    name: "core tests" # the core\n    steps:\n      - name: t\n';
  const names = jobNames(wf);
  const both = new Map([['guard', 'guard (AGENTS.md rules)'], ['core', 'core tests']]);
  const ruleset = (ctx) => [{ type: 'pull_request', parameters: {} }, { type: 'required_status_checks', parameters: { required_status_checks: ctx.map((context) => ({ context, integration_id: 15368 })), strict_required_status_checks_policy: false } }];
  const classic = (ctx, level = 'everyone') => ({ name: 'main', protected: true, protection: { enabled: true, required_status_checks: { enforcement_level: level, contexts: ctx, checks: ctx.map((context) => ({ context, app_id: null })) } } });
  const open = { name: 'main', protected: false, protection: { enabled: false, required_status_checks: { enforcement_level: 'off', contexts: [], checks: [] } } };
  const run = (rules, branch) => { const req = requiredContexts(rules, branch); return verdict(gate(both, req), req); };
  const statuses = (rules, branch) => gate(both, requiredContexts(rules, branch)).map((r) => r.status).join(',');
  let real = '';
  try { real = fs.readFileSync(path.join(root, WORKFLOW), 'utf8'); } catch { real = ''; }
  const realNames = jobNames(real);
  // [name, actual, expected]
  const cases = [
    ['job names read from the jobs block', JSON.stringify([...names]), JSON.stringify([...both])],
    ['a step name is not a job name', names.has('Guard self-test') || [...names.values()].includes('Guard self-test'), false],
    ['CRLF workflow', JSON.stringify([...jobNames(wf.replace(/\n/g, '\r\n'))]), JSON.stringify([...both])],
    ['a job without name: is checked by its id', jobNames('jobs:\n  core:\n    runs-on: x\n').get('core'), 'core'],
    ['the real guard.yml names both gate jobs', GATE_JOBS.every((id) => typeof realNames.get(id) === 'string' && realNames.get(id).length > 0), true],
    ['nothing readable is unknown, not a pass', statuses(null, null), 'unknown,unknown'],
    ['no protection anywhere', statuses([], open), 'not required,not required'],
    ['a ruleset requires both', statuses(ruleset([...both.values()]), open), 'required,required'],
    ['classic protection requires both', statuses([], classic([...both.values()])), 'required,required'],
    ['a ruleset requires one', statuses(ruleset(['guard (AGENTS.md rules)']), open), 'required,not required'],
    ['required by ruleset while classic is unreadable', statuses(ruleset([...both.values()]), { protected: true }), 'required,required'],
    ['not in the ruleset and classic unreadable', statuses([], { protected: true }), 'unknown,unknown'],
    ['a check turned off in classic does not count', statuses([], classic([...both.values()], 'off')), 'not required,not required'],
    ['a renamed job no longer matches its required check', statuses(ruleset(['guard']), open), 'not required,not required'],
    ['a gate job missing from the workflow', gate(new Map([['guard', 'g']]), requiredContexts([], open)).map((r) => r.status).join(','), 'not required,missing'],
    ['blocks when both are required', run(ruleset([...both.values()]), open).blocks, true],
    ['no warning when both are required for everyone', run([], classic([...both.values()])).warn, false],
    ['admin bypass still warns', run([], classic([...both.values()], 'non_admins')).warn, true],
    ['does not block when one is missing', run(ruleset(['core tests']), open).blocks, false],
    ['does not block when unknown', run(null, null).blocks, false],
    ['an unknown verdict says unknown', /^unknown/.test(run(null, null).summary), true],
  ];
  let failed = 0;
  for (const [name, actual, expected] of cases) {
    if (actual !== expected) { failed++; console.log(`FAIL ${name}: expected ${JSON.stringify(expected)}, got ${JSON.stringify(actual)}`); }
  }
  console.log(`merge-gate self-test: ${cases.length - failed} passed, ${failed} failed`);
  return failed;
}

const mode = process.argv[2];
if (mode === '--self-test') {
  process.exitCode = selfTest() ? 1 : 0;
} else if (mode === undefined) {
  try { await probe(); } catch (e) {
    console.log(`::warning title=Merge gate::probe crashed, so the setting is unknown: ${e && e.message ? e.message : e}`);
  }
  process.exitCode = 0;
} else {
  console.error('usage: node tools/guard/protection.mjs [--self-test]');
  process.exitCode = 2;
}
