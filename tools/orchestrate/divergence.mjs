#!/usr/bin/env node
// How far a worktree's HEAD is ahead of main. The agent loop calls this before `git merge --no-edit main`.
//
//   node tools/orchestrate/divergence.mjs --worktree C:/Users/kazda/kiro/gvr-terrarium
//
// Prints the count and the verdict. Exit 0 when under the limit, 3 when over, 1 when the count cannot be read.
import { execFileSync } from 'node:child_process';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

// Between host merges an agent branch carries a few dozen commits at most, so hundreds mean main was rewritten under it.
export const MAX_AHEAD = 100;

export function divergence(worktree) {
  const out = execFileSync('git', ['-C', worktree, 'rev-list', '--count', 'main..HEAD'], { encoding: 'utf8', windowsHide: true });
  const ahead = Number(out.trim());
  if (!Number.isFinite(ahead)) throw new Error(`unreadable rev-list count: ${out.trim()}`);
  return { ahead, max: MAX_AHEAD, over: ahead > MAX_AHEAD, verdict: ahead > MAX_AHEAD ? 'over the limit' : 'under the limit' };
}

if (process.argv[1] && path.resolve(process.argv[1]) === path.resolve(fileURLToPath(import.meta.url))) {
  const argv = process.argv.slice(2);
  const i = argv.indexOf('--worktree');
  if (i < 0 || !argv[i + 1]) { console.error('need --worktree <path>'); process.exit(1); }
  try {
    const r = divergence(argv[i + 1]);
    console.log(`${r.ahead} commits ahead of main (MAX_AHEAD ${r.max}): ${r.verdict}`);
    process.exit(r.over ? 3 : 0);
  } catch (e) { console.error(`divergence failed: ${e.message}`); process.exit(1); }
}
