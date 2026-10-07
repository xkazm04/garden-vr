#!/usr/bin/env node
// Code-health hotspots for Garden VR: the baseline every refactor is measured against.
//
//   node tools/health/hotspots.mjs                  measure HEAD, write docs/health/baseline.json and BASELINE.md
//   node tools/health/hotspots.mjs --rev <commit>   measure another commit
//   node tools/health/hotspots.mjs --out <dir>      write the two files somewhere else
//   node tools/health/hotspots.mjs --run-tests      run `dotnet test shared/core-dotnet` on a clean HEAD and write the
//                                                  counts to docs/health/dotnet-test.json, for the next commit to carry
//   node tools/health/hotspots.mjs --explain <path> [--rev <commit>]   list the methods the parser found in one file
//
// Measuring reads only blobs and history reachable from the commit (git ls-tree, git cat-file --batch, git log
// --numstat), never the working tree, and changes no file but its own two outputs. Output is deterministic: two runs on
// the same commit are byte-identical. Two hand-kept inputs are read from the commit too, never from disk:
//   docs/health/dotnet-test.json   the last `--run-tests` record (its commit, counts, and the trees it tested)
//   docs/health/findings.json      one finding per hotspot, written by reading the code; each names the blob it read,
//                                  so a finding goes stale on its own when the file changes
//
// Scope: every tracked .cs file under apps/*/Assets, shared/packages and shared/core-dotnet. For each file: total and
// code lines, churn since 2026-10-02 by author date (non-merge commits reachable from the commit, no rename following),
// methods with a body and the longest of them, the deepest brace nesting inside a member, and whether a test file names
// its main type. Score = code lines x (1 + churn commits). AGENTS.md rules 1 and 4 are counted by regex.
//
// The C# reading is a lexer (comments, strings, char literals, directives) and a brace-structure parser, not a compiler:
// it is exact for the code style of this repo and approximate for constructs the repo does not use.
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(here, '..', '..');
const SINCE = '2026-10-02';
const TEST_RECORD = 'docs/health/dotnet-test.json';
const FINDINGS = 'docs/health/findings.json';
const TEST_TREES = ['shared/core-dotnet', 'shared/packages/com.gardenvr.core'];
const TOP = 10;
const SCOPE = [/^apps\/[^/]+\/Assets\/.+\.cs$/, /^shared\/packages\/.+\.cs$/, /^shared\/core-dotnet\/.+\.cs$/];
const PATHSPEC = [':(glob)apps/*/Assets/**/*.cs', ':(glob)shared/packages/**/*.cs', ':(glob)shared/core-dotnet/**/*.cs'];
const PROVIDERS = 'shared/packages/com.gardenvr.input/Runtime/Providers/';
const RULE1 = /(?<![\w.])Input\s*\.|\bUnityEngine\s*\.\s*Input\s*\.|\bKeyboard\s*\.\s*current\b|\bMouse\s*\.\s*current\b/g;
const RULE4_USING = /^[ \t]*(?:global[ \t]+)?using[ \t]+(?:static[ \t]+)?(?:[A-Za-z_]\w*[ \t]*=[ \t]*)?(?:global::)?UnityEngine\b/gm;
const RULE4_QUALIFIED = /(?<![\w.])(?:global::)?UnityEngine\s*\./g;
const RULE4_SCOPE = p => p.startsWith('shared/packages/com.gardenvr.core/') || p.startsWith('shared/core-dotnet/');

function args(argv) {
  const out = { _: [] };
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    if (a.startsWith('--')) { const k = a.slice(2); const v = argv[i + 1]; if (v === undefined || v.startsWith('--')) out[k] = true; else { out[k] = v; i++; } }
    else out._.push(a);
  }
  return out;
}

function git(argv, input) {
  const r = spawnSync('git', ['-c', 'core.quotepath=off', ...argv], { cwd: root, input, maxBuffer: 1 << 30 });
  if (r.error) throw r.error;
  if (r.status !== 0) throw new Error(`git ${argv.join(' ')} failed: ${r.stderr.toString().trim()}`);
  return r.stdout;
}

// One `git cat-file --batch` round trip for a list of blob shas.
function readBlobs(shas) {
  const unique = [...new Set(shas)];
  const blobs = new Map();
  if (!unique.length) return blobs;
  const buf = git(['cat-file', '--batch'], unique.join('\n') + '\n');
  let pos = 0;
  while (pos < buf.length) {
    const nl = buf.indexOf(10, pos);
    const [sha, type, size] = buf.subarray(pos, nl).toString('latin1').split(' ');
    if (type === 'missing') throw new Error(`blob ${sha} missing`);
    const n = Number(size);
    blobs.set(sha, buf.subarray(nl + 1, nl + 1 + n));
    pos = nl + 1 + n + 1;
  }
  return blobs;
}

const byPath = (a, b) => (a.path < b.path ? -1 : a.path > b.path ? 1 : 0);
const sorted = it => [...it].sort();
const roleOf = p => (p.startsWith('shared/core-dotnet/') || /^apps\/[^/]+\/Assets\/Tests\//.test(p) ? 'test' : /\/Editor\//.test(p) ? 'editor' : 'runtime');
const appOf = p => (p.match(/^apps\/([^/]+)\//) || [])[1] || null;

// ---------------------------------------------------------------------------------------------------------------------
// Lexer: a class for every character. 0 whitespace, 1 code, 2 comment, 3 string or char literal, 4 directive.

const BOM = String.fromCharCode(0xfeff);
const WS = new Set([' ', '\t', '\r', '\n', '\f', '\v', BOM]);

function lex(src) {
  const n = src.length;
  const cls = new Uint8Array(n);
  const fill = (from, to, k) => { for (let j = from; j < to; j++) if (src[j] !== '\n') cls[j] = k; };
  const isStringStart = i => src[i] === '"' || ((src[i] === '$' || src[i] === '@') && /^(?:\$+@?|@\$*)"/.test(src.slice(i, i + 8)));

  function charEnd(i) {
    let j = i + 1;
    j += src[j] === '\\' ? 2 : 1;
    while (j < n && src[j] !== '\'' && src[j] !== '\n') j++;
    return src[j] === '\'' ? j + 1 : j;
  }
  function holeEnd(j) {
    let depth = 1;
    while (j < n) {
      const c = src[j];
      if (isStringStart(j)) { j = stringEnd(j); continue; }
      if (c === '\'') { j = charEnd(j); continue; }
      if (c === '{') depth++;
      else if (c === '}' && --depth === 0) return j + 1;
      j++;
    }
    return n;
  }
  function stringEnd(i) {
    let j = i, interp = 0, verbatim = false;
    while (src[j] === '$') { interp++; j++; }
    if (src[j] === '@') { verbatim = true; j++; while (src[j] === '$') { interp++; j++; } }
    let q = 0;
    while (src[j + q] === '"') q++;
    if (q >= 3) { const k = src.indexOf('"'.repeat(q), j + q); return k < 0 ? n : k + q; }
    j++;
    while (j < n) {
      const c = src[j];
      if (verbatim) { if (c === '"') { if (src[j + 1] === '"') { j += 2; continue; } return j + 1; } }
      else { if (c === '\\') { j += 2; continue; } if (c === '"') return j + 1; if (c === '\n') return j; }
      if (interp && c === '{') { if (src[j + 1] === '{') { j += 2; continue; } j = holeEnd(j + 1); continue; }
      j++;
    }
    return n;
  }

  let i = 0;
  let lineStart = true;
  while (i < n) {
    const c = src[i];
    if (c === '\n') { lineStart = true; i++; continue; }
    if (WS.has(c)) { i++; continue; }
    if (lineStart && c === '#') { let j = src.indexOf('\n', i); if (j < 0) j = n; fill(i, j, 4); i = j; continue; }
    lineStart = false;
    if (c === '/' && src[i + 1] === '/') { let j = src.indexOf('\n', i); if (j < 0) j = n; fill(i, j, 2); i = j; continue; }
    if (c === '/' && src[i + 1] === '*') { let j = src.indexOf('*/', i + 2); j = j < 0 ? n : j + 2; fill(i, j, 2); i = j; continue; }
    if (isStringStart(i)) { const j = stringEnd(i); fill(i, j, 3); i = j; continue; }
    if (c === '\'') { const j = charEnd(i); fill(i, j, 3); i = j; continue; }
    cls[i] = 1;
    i++;
  }
  // Whitespace inside a literal is part of the literal; everywhere else it stays 0.
  return cls;
}

// Lines, code lines and a masked copy of the source: comments and directives blanked, every literal turned into a run
// of underscores, newlines kept, so the structure parser sees one placeholder token per literal and the right lines.
function measureText(src) {
  const cls = lex(src);
  const lines = src.split('\n');
  if (lines.length && lines[lines.length - 1] === '') lines.pop();
  let code = 0, comment = 0, blank = 0, pos = 0;
  for (const line of lines) {
    let hasCode = false, hasComment = false;
    for (let j = 0; j < line.length; j++) {
      const k = cls[pos + j];
      if (k === 1 || k === 3 || k === 4) hasCode = true;
      else if (k === 2) hasComment = true;
    }
    if (hasCode) code++; else if (hasComment) comment++; else blank++;
    pos += line.length + 1;
  }
  let masked = '';
  for (let j = 0; j < src.length; j++) {
    const ch = src[j];
    const k = cls[j];
    masked += ch === '\n' ? '\n' : k === 2 || k === 4 ? ' ' : k === 3 ? '_' : k === 0 && WS.has(ch) ? ' ' : ch;
  }
  return { cls, totalLines: lines.length, codeLines: code, commentLines: comment, blankLines: blank, masked };
}

// ---------------------------------------------------------------------------------------------------------------------
// Tokens and structure.

const TOKEN = /(?:[A-Za-z_@]|[^\x00-\x7f])(?:\w|[^\x00-\x7f])*|\d[\w.]*|=>|==|!=|<=|>=|&&|\|\||\?\?=?|\?\.|<<=|>>=|[+\-*/%&|^]=|\+\+|--|->|::|\S/g;
const KEYWORDS = new Set(('abstract as base bool break byte case catch char checked class const continue decimal default delegate do double '
  + 'else enum event explicit extern false finally fixed float for foreach goto if implicit in int interface internal is lock long '
  + 'namespace new null object operator out override params private protected public readonly ref return sbyte sealed short sizeof '
  + 'stackalloc static string struct switch this throw true try typeof uint ulong unchecked unsafe ushort using virtual void volatile '
  + 'while async await partial record var when where yield nameof get set init add remove value global').split(' '));
const TYPE_KW = new Set(['class', 'struct', 'interface', 'enum', 'record']);
const CONTROL = new Set(['if', 'else', 'for', 'foreach', 'while', 'do', 'switch', 'try', 'catch', 'finally', 'using', 'lock', 'fixed',
  'checked', 'unchecked', 'unsafe', 'case', 'default']);
const ASSIGN = new Set(['=', '+=', '-=', '*=', '/=', '%=', '&=', '|=', '^=', '<<=', '>>=', '??=']);
const EXPR_MARK = new Set(['new', 'return', 'switch', 'with', 'is', 'yield', 'throw', 'await', ',', '?', ':', '??']);
const isIdent = t => /^(?:[A-Za-z_@]|[^\x00-\x7f])/.test(t);
const isName = t => isIdent(t) && !KEYWORDS.has(t);

function tokenize(masked) {
  const out = [];
  const lines = masked.split('\n');
  for (let l = 0; l < lines.length; l++) for (const m of lines[l].matchAll(TOKEN)) out.push({ t: m[0], line: l + 1 });
  return out;
}

function stripAttributes(h) {
  let s = 0;
  while (s < h.length && h[s].t === '[') {
    let depth = 0, j = s;
    for (; j < h.length; j++) { if (h[j].t === '[') depth++; else if (h[j].t === ']' && --depth === 0) break; }
    s = j + 1;
  }
  return h.slice(s);
}

// A statement head at depth 0 of its own brackets: the indices of its top-level '(' and whether it assigns.
function scanHead(h) {
  let depth = 0, typeKw = null, assign = false, operator = false;
  const parens = [];
  for (let i = 0; i < h.length; i++) {
    const t = h[i].t;
    if (t === '(' || t === '[') { if (depth === 0 && t === '(') parens.push(i); depth++; continue; }
    if (t === ')' || t === ']') { depth--; continue; }
    if (depth) continue;
    if (TYPE_KW.has(t) && !parens.length && !typeKw && i + 1 < h.length && isIdent(h[i + 1].t)) typeKw = { kw: t, name: h[i + 1].t === 'struct' || h[i + 1].t === 'class' ? h[i + 2]?.t : h[i + 1].t };
    if (ASSIGN.has(t)) assign = true;
    if (t === 'operator') operator = true;
  }
  return { parens, typeKw, assign, operator };
}

// The name in front of a parameter list, if the head declares a method: `F(`, `F<T>(`, `~F(`, `operator +(`.
function methodName(h, head) {
  if (head.operator) {
    const i = h.findIndex(x => x.t === 'operator');
    return `operator ${h.slice(i + 1, head.parens.find(p => p > i) ?? i + 2).map(x => x.t).join('')}`;
  }
  for (const p of head.parens) {
    const prev = h[p - 1];
    if (!prev) continue;
    if (isName(prev.t)) return prev.t;
    if (prev.t === '>') {
      let depth = 0;
      for (let j = p - 1; j >= 0; j--) {
        if (h[j].t === '>') depth++;
        else if (h[j].t === '<' && --depth === 0) { if (j > 0 && isName(h[j - 1].t)) return h[j - 1].t; break; }
      }
    }
  }
  return null;
}

// Classify the block a '{' opens, from the tokens since the last ';', '{' or '}' of the enclosing block.
function classify(header, parent) {
  const h = stripAttributes(header);
  const last = h.length ? h[h.length - 1].t : null;
  const lambda = last === '=>' || last === 'delegate' || (last === ')' && h.some(x => x.t === 'delegate'));
  if (parent.kind === 'namespace' || parent.kind === 'type') {
    if (!h.length) return { kind: 'expr' };
    if (h[0].t === 'namespace') return { kind: 'namespace' };
    const head = scanHead(h);
    if (head.typeKw) return { kind: 'type', name: head.typeKw.name, kw: head.typeKw.kw };
    if (lambda) return { kind: 'body', member: 'field-lambda' };
    if (head.assign) return { kind: 'init' };
    const name = methodName(h, head);
    if (name) return { kind: 'body', member: 'method', name, start: h[0].line };
    return { kind: 'property' };
  }
  if (parent.kind === 'property') return { kind: 'body', member: 'accessor' };
  if (parent.paren > 0 || parent.kind === 'expr' || parent.kind === 'init') return { kind: lambda ? 'lambda' : 'expr' };
  if (!h.length) return { kind: 'control' };
  if (lambda) return { kind: 'lambda' };
  const first = h[0].t;
  if (CONTROL.has(first) || (first === 'await' && h[1] && (h[1].t === 'foreach' || h[1].t === 'using'))) return { kind: 'control' };
  const head = scanHead(h);
  if (head.assign || h.some(x => EXPR_MARK.has(x.t))) return { kind: 'expr' };
  if (last === ')' && methodName(h, head)) return { kind: 'local' };
  return { kind: 'control', unknown: h.map(x => x.t).join(' ') };
}

const NESTS = new Set(['control', 'lambda', 'local']);

// Walk the tokens once: methods with their span, the deepest nesting inside any member, declared type names.
function structure(tokens) {
  const methods = [];
  const types = [];
  const unknown = [];
  let deepest = { depth: 0, line: null };
  const stack = [{ kind: 'namespace', paren: 0, stmt: 0 }];
  const top = () => stack[stack.length - 1];
  const close = (f, line) => {
    if (f.member === 'method') methods.push({ name: f.name, line: f.start, endLine: line, lines: line - f.start + 1, nesting: f.maxNest });
  };
  const open = (f, parent, line) => {
    // A member body, or a field initializer, owns the nesting counted inside it.
    if (f.kind === 'body' || (f.kind === 'init' && !parent.owner)) { f.nest = 0; f.maxNest = 0; f.owner = f; }
    else if (parent.owner) {
      f.owner = parent.owner;
      f.nest = parent.nest + (NESTS.has(f.kind) ? 1 : 0);
      if (f.nest > f.owner.maxNest) f.owner.maxNest = f.nest;
      if (f.nest > deepest.depth) deepest = { depth: f.nest, line };
    }
    f.paren = 0;
    stack.push(f);
  };
  for (let k = 0; k < tokens.length; k++) {
    const { t, line } = tokens[k];
    const f = top();
    if (t === '(' || t === '[') { f.paren++; continue; }
    if (t === ')' || t === ']') { if (f.paren > 0) f.paren--; continue; }
    if (t === ';') {
      if (f.paren > 0) continue;
      if (f.pseudo) { close(f, line); stack.pop(); }
      top().stmt = k + 1;
      continue;
    }
    if (t === '=>' && f.kind === 'type' && f.paren === 0 && !(tokens[k + 1] && tokens[k + 1].t === '{')) {
      const h = stripAttributes(tokens.slice(f.stmt, k));
      const head = scanHead(h);
      if (!h.length) continue;
      const name = head.assign ? null : methodName(h, head);
      const g = name ? { kind: 'body', member: 'method', name, start: h[0].line } : { kind: 'body', member: head.assign ? 'field-lambda' : 'accessor' };
      g.pseudo = true;
      open(g, f, line);
      g.stmt = k + 1;
      continue;
    }
    if (t === '{') {
      const header = tokens.slice(f.stmt, k);
      const g = classify(header, f);
      if (g.unknown) unknown.push({ line, head: g.unknown });
      if (g.kind === 'type') types.push({ name: g.name, kw: g.kw, line, nested: stack.some(x => x.kind === 'type') });
      const start = stripAttributes(header)[0];
      open(g, f, start ? start.line : line);
      g.stmt = k + 1;
      continue;
    }
    if (t === '}') {
      while (stack.length > 1 && top().pseudo) { close(top(), line); stack.pop(); }
      if (stack.length > 1) { close(top(), line); stack.pop(); }
      const p = top();
      if (p.paren === 0) p.stmt = k + 1;
      continue;
    }
  }
  // Types declared without a body (`record R(int A);`) still name the file's type.
  return { methods, types, unknown, deepest, unclosed: stack.length - 1 };
}

function bodilessTypes(masked) {
  const out = [];
  for (const m of masked.matchAll(/\b(class|struct|interface|enum|record)\s+(?:struct\s+|class\s+)?([A-Za-z_]\w*)/g)) out.push(m[2]);
  return out;
}

// ---------------------------------------------------------------------------------------------------------------------

function readJsonAt(sha, p) {
  const line = git(['ls-tree', sha, '--', p]).toString().trim();
  if (!line) return null;
  const blob = line.split(/\s+/)[2];
  return JSON.parse(readBlobs([blob]).get(blob).toString('utf8'));
}

function treeAt(sha, p) {
  const r = spawnSync('git', ['rev-parse', '--verify', '--quiet', `${sha}:${p}`], { cwd: root });
  return r.status === 0 ? r.stdout.toString().trim() : null;
}

function churnOf(sha, scope) {
  const out = git(['log', '--no-merges', '--no-renames', '--numstat', '--format=%x00%H %aI', sha, '--', ...PATHSPEC]).toString('utf8');
  const churn = new Map();
  const window = { commits: 0, firstAuthorDate: null, lastAuthorDate: null };
  for (const rec of out.split('\0')) {
    if (!rec.trim()) continue;
    const lines = rec.split('\n');
    const [, date] = lines[0].split(' ');
    if (date.slice(0, 10) < SINCE) continue;
    let touched = false;
    for (const l of lines.slice(1)) {
      const m = l.match(/^(\d+|-)\t(\d+|-)\t(.+)$/);
      if (!m || !scope.has(m[3])) continue;
      const c = churn.get(m[3]) || { commits: 0, added: 0, deleted: 0 };
      c.commits++;
      c.added += m[1] === '-' ? 0 : Number(m[1]);
      c.deleted += m[2] === '-' ? 0 : Number(m[2]);
      churn.set(m[3], c);
      touched = true;
    }
    if (touched) {
      window.commits++;
      if (!window.firstAuthorDate || date < window.firstAuthorDate) window.firstAuthorDate = date;
      if (!window.lastAuthorDate || date > window.lastAuthorDate) window.lastAuthorDate = date;
    }
  }
  return { churn, window };
}

function hotspots(rev) {
  const sha = git(['rev-parse', '--verify', `${rev}^{commit}`]).toString().trim();
  const subject = git(['log', '-1', '--format=%s', sha]).toString().trim();

  const entries = [];
  for (const rec of git(['ls-tree', '-r', '-z', sha]).toString('utf8').split('\0')) {
    if (!rec) continue;
    const tab = rec.indexOf('\t');
    const [, type, blob] = rec.slice(0, tab).split(/ +/);
    const p = rec.slice(tab + 1);
    if (type === 'blob' && SCOPE.some(re => re.test(p))) entries.push({ path: p, blob });
  }
  entries.sort(byPath);
  const blobs = readBlobs(entries.map(e => e.blob));
  const { churn, window } = churnOf(sha, new Set(entries.map(e => e.path)));

  const files = entries.map(e => {
    const text = blobs.get(e.blob).toString('utf8');
    const src = text.startsWith(BOM) ? text.slice(1) : text;
    const m = measureText(src);
    const s = structure(tokenize(m.masked));
    const stem = path.posix.basename(e.path, '.cs');
    const declared = [...s.types.filter(t => !t.nested).map(t => t.name), ...bodilessTypes(m.masked)];
    const mainType = declared.find(n => n === stem) || declared.find(n => stem.startsWith(`${n}.`)) || declared[0] || null;
    const longest = s.methods.reduce((a, b) => (b.lines > a.lines ? b : a), { name: null, line: null, lines: 0 });
    const c = churn.get(e.path) || { commits: 0, added: 0, deleted: 0 };
    return {
      path: e.path, blob: e.blob, role: roleOf(e.path), src, cls: m.cls, masked: m.masked,
      totalLines: m.totalLines, codeLines: m.codeLines, commentLines: m.commentLines, blankLines: m.blankLines,
      churn: { commits: c.commits, added: c.added, deleted: c.deleted, lines: c.added + c.deleted },
      methods: s.methods.length,
      longestMethod: longest.name ? { name: longest.name, line: longest.line, lines: longest.lines } : null,
      deepestNesting: { depth: s.deepest.depth, line: s.deepest.line },
      mainType, parseNotes: [...(s.unclosed ? [`${s.unclosed} block(s) left open`] : []), ...s.unknown.map(u => `line ${u.line}: unclassified block head "${u.head}"`)],
      score: m.codeLines * (1 + c.commits),
    };
  });

  // Tested: a test file names the main type as a whole word in its code (comments and literals masked). App code is
  // matched against its own app's tests; shared code against every test file.
  const tests = files.filter(f => f.role === 'test');
  for (const f of files) {
    if (f.role === 'test') { f.tested = null; f.testedBy = []; continue; }
    const app = appOf(f.path);
    const pool = tests.filter(t => (app ? appOf(t.path) === app : true));
    const re = f.mainType ? new RegExp(`(?<![\\w@])${f.mainType}(?!\\w)`) : null;
    f.testedBy = re ? pool.filter(t => re.test(t.masked)).map(t => t.path).sort() : [];
    f.tested = f.testedBy.length > 0;
  }

  const rank = list => list.sort((a, b) => b.score - a.score || (a.path < b.path ? -1 : 1)).forEach((f, i) => { f.rank = i + 1; });
  const code = files.filter(f => f.role !== 'test');
  rank(code);
  rank(tests);

  // Rule breaks, read from the raw text and kept only where the match sits in code (not a comment or a literal).
  const where = (f, i) => ({ 1: 'code', 2: 'comment', 3: 'literal', 4: 'directive' }[f.cls[i]] || 'code');
  const lineAt = (src, i) => src.slice(0, i).split('\n').length;
  const scan = (list, re) => {
    const hits = [];
    for (const f of list) for (const m of f.src.matchAll(re)) {
      const at = m.index + m[0].length - m[0].trimStart().length;
      hits.push({ path: f.path, line: lineAt(f.src, at), in: where(f, at), text: f.src.split('\n')[lineAt(f.src, at) - 1].trim() });
    }
    return hits;
  };
  const r1 = scan(files, RULE1);
  const r4u = scan(files.filter(f => RULE4_SCOPE(f.path)), RULE4_USING);
  const r4q = scan(files.filter(f => RULE4_SCOPE(f.path)), RULE4_QUALIFIED).filter(h => !r4u.some(u => u.path === h.path && u.line === h.line));
  const rules = {
    rule1: {
      rule: 'AGENTS.md rule 1: no direct Input / Keyboard.current / Mouse.current read outside the keyboard/mouse provider',
      pattern: RULE1.source, allowedUnder: PROVIDERS,
      breaks: r1.filter(h => h.in === 'code' && !h.path.startsWith(PROVIDERS)),
      allowed: r1.filter(h => h.in === 'code' && h.path.startsWith(PROVIDERS)),
      mentions: r1.filter(h => h.in !== 'code'),
    },
    rule4: {
      rule: 'AGENTS.md rule 4: no `using UnityEngine` in com.gardenvr.core or shared/core-dotnet',
      pattern: RULE4_USING.source, scope: 'shared/packages/com.gardenvr.core/, shared/core-dotnet/',
      breaks: r4u.filter(h => h.in === 'code'),
      qualified: r4q.filter(h => h.in === 'code'),
      mentions: [...r4u, ...r4q].filter(h => h.in !== 'code').sort((a, b) => byPath(a, b) || a.line - b.line),
    },
  };

  // Hand-kept inputs, read from the commit.
  const record = readJsonAt(sha, TEST_RECORD);
  let testRun = null;
  if (record) {
    const trees = TEST_TREES.map(p => ({ path: p, tested: record.trees ? record.trees[p] || null : null, measured: treeAt(sha, p) }));
    testRun = { ...record, sameTreesAsMeasured: trees.every(t => t.tested && t.tested === t.measured), treeCheck: trees };
  }
  const findingsDoc = readJsonAt(sha, FINDINGS);
  const byFile = new Map();
  for (const f of (findingsDoc && findingsDoc.findings) || []) byFile.set(f.path, f);
  const findings = [];
  for (const f of code) {
    const g = byFile.get(f.path);
    if (!g && f.rank > TOP) continue;
    findings.push(g ? {
      rank: f.rank, path: f.path, line: g.line, kind: g.kind, finding: g.finding, backlog: g.backlog || null,
      readAtBlob: g.blob, stale: g.blob !== f.blob,
    } : { rank: f.rank, path: f.path, line: null, kind: null, finding: null, backlog: null, readAtBlob: null, stale: false });
  }
  const orphans = [...byFile.keys()].filter(p => !code.some(f => f.path === p)).sort();

  const sum = (list, k) => list.reduce((s, f) => s + f[k], 0);
  const roleTotals = ['runtime', 'editor', 'test'].map(r => {
    const l = files.filter(f => f.role === r);
    return { role: r, files: l.length, totalLines: sum(l, 'totalLines'), codeLines: sum(l, 'codeLines'), methods: sum(l, 'methods') };
  });
  const record1 = f => ({
    rank: f.rank, path: f.path, role: f.role, blob: f.blob, totalLines: f.totalLines, codeLines: f.codeLines, commentLines: f.commentLines,
    blankLines: f.blankLines, churn: f.churn, score: f.score, methods: f.methods, longestMethod: f.longestMethod,
    deepestNesting: f.deepestNesting, mainType: f.mainType, tested: f.tested, testedBy: f.testedBy, parseNotes: f.parseNotes,
  });
  return {
    schema: 'garden-vr/code-health/1',
    commit: { sha, subject },
    definitions: {
      scope: 'tracked .cs under apps/*/Assets, shared/packages, shared/core-dotnet',
      codeLine: 'a line with any character outside a comment (code, a literal or a directive)',
      churn: `non-merge commits reachable from the commit with an author date on or after ${SINCE} (author's own calendar day), git log --no-merges --no-renames --numstat; lines = added + deleted`,
      method: 'a method, constructor, finalizer or operator with a block or expression body; local functions and lambdas count inside the method that holds them; accessors are not methods',
      methodLines: 'from the first line of the signature (attributes excluded) to the line of its closing brace or semicolon',
      nesting: 'the deepest stack of control blocks, lambdas and local functions inside one member body, counted in braces; the body itself is 0, initializer and pattern braces add nothing, a brace-less if adds nothing',
      tested: 'a test file (shared/core-dotnet, or apps/<app>/Assets/Tests of the same app for app code, any for shared code) names the main type as a whole word in its code',
      mainType: 'the top-level type named like the file, else the first top-level type declared',
      score: 'codeLines * (1 + churn.commits)',
      ranking: 'runtime and editor files ranked by score, then path; test files ranked the same way in their own list',
    },
    window: { since: SINCE, ...window },
    testRun,
    totals: {
      files: files.length, totalLines: sum(files, 'totalLines'), codeLines: sum(files, 'codeLines'), methods: sum(files, 'methods'),
      byRole: roleTotals,
      tested: code.filter(f => f.tested).length, untested: code.filter(f => !f.tested).length,
      rule1Breaks: rules.rule1.breaks.length, rule4Breaks: rules.rule4.breaks.length,
      parseNotes: files.reduce((s, f) => s + f.parseNotes.length, 0),
    },
    rules,
    findings,
    findingsWithoutHotspot: orphans,
    files: code.map(record1),
    testFiles: tests.map(record1),
  };
}

// Custom JSON layout: the top two levels pretty, every record below on one line, so diffs stay one line per record.
function layout(v, depth = 0) {
  if (depth >= 2 || v === null || typeof v !== 'object') return JSON.stringify(v);
  const pad = '  '.repeat(depth + 1);
  const end = '  '.repeat(depth);
  if (Array.isArray(v)) return v.length ? `[\n${v.map(x => pad + layout(x, depth + 1)).join(',\n')}\n${end}]` : '[]';
  const keys = Object.keys(v);
  return keys.length ? `{\n${keys.map(k => `${pad}${JSON.stringify(k)}: ${layout(v[k], depth + 1)}`).join(',\n')}\n${end}}` : '{}';
}

function markdown(r) {
  const t = r.totals;
  const row = cells => `| ${cells.join(' | ')} |`;
  const cell = s => String(s).replace(/\|/g, '\\|');
  const yn = f => (f.tested === null ? 'test' : f.tested ? 'yes' : 'no');
  const longest = f => (f.longestMethod ? `${f.longestMethod.lines} (\`${f.longestMethod.name}\` :${f.longestMethod.line})` : '-');
  const nest = f => (f.deepestNesting.line ? `${f.deepestNesting.depth} (:${f.deepestNesting.line})` : '0');
  const out = [];
  out.push('# Code-health baseline', '');
  out.push(`Generated by \`node tools/health/hotspots.mjs --rev ${r.commit.sha}\`. Do not edit by hand; rerun the tool. `
    + `The findings come from \`${FINDINGS}\` and the test counts from \`${TEST_RECORD}\`, both read from the same commit.`, '');
  out.push(`Commit measured: \`${r.commit.sha}\` (${r.commit.subject}).`, '');

  out.push('## dotnet test', '');
  if (!r.testRun) out.push(`No \`${TEST_RECORD}\` at this commit: not run, not a pass.`, '');
  else {
    const x = r.testRun;
    out.push(row(['Command', 'Commit tested', 'Passed', 'Failed', 'Skipped', 'Total', 'Outcome']), row(['---', '---', '---:', '---:', '---:', '---:', '---']));
    out.push(row([`\`${x.command}\``, `\`${x.commit}\``, x.passed, x.failed, x.skipped, x.total, x.outcome]));
    out.push('');
    out.push(`Run on ${x.date} with .NET SDK ${x.sdk}. `
      + (x.sameTreesAsMeasured
        ? `The trees the tests compile (${x.treeCheck.map(c => `\`${c.path}\` ${c.measured.slice(0, 10)}`).join(', ')}) are identical at the commit tested and the commit measured, so the counts hold for the measured commit.`
        : `The trees the tests compile differ between the commit tested and the commit measured (${x.treeCheck.map(c => `\`${c.path}\` tested ${String(c.tested).slice(0, 10)}, measured ${String(c.measured).slice(0, 10)}`).join('; ')}): these counts do not certify the measured commit.`), '');
    out.push('The Unity EditMode and PlayMode tests under `apps/*/Assets/Tests` need a Unity licence this machine lacks: not run.', '');
  }

  out.push('## How to read it', '');
  for (const [k, v] of Object.entries(r.definitions)) out.push(`- **${k}**: ${v}.`);
  out.push(`- **window**: ${r.window.commits} non-merge commits touched a scoped file, author dates ${r.window.firstAuthorDate} to ${r.window.lastAuthorDate}.`);
  out.push('- **tested** says a test file names the type; it does not say the test runs or what it covers.');
  out.push(`- **parse notes**: ${t.parseNotes} (blocks the parser could not classify or left open; listed per file in \`baseline.json\`).`, '');

  out.push('## Totals', '');
  out.push(row(['Role', 'Files', 'Total lines', 'Code lines', 'Methods']), row(['---', '---:', '---:', '---:', '---:']));
  for (const g of t.byRole) out.push(row([g.role, g.files, g.totalLines, g.codeLines, g.methods]));
  out.push(row(['all', t.files, t.totalLines, t.codeLines, t.methods]));
  out.push('');
  out.push(`Runtime and editor files whose main type a test file names: ${t.tested} of ${t.tested + t.untested}.`, '');

  out.push('## AGENTS.md rule breaks', '');
  const hitList = list => list.map(h => `- \`${h.path}:${h.line}\` (${h.in}): \`${cell(h.text)}\``);
  const r1 = r.rules.rule1;
  out.push(`**Rule 1** (direct \`Input.\`, \`Keyboard.current\`, \`Mouse.current\` outside \`${r1.allowedUnder}\`): **${r1.breaks.length}** in code.`, '');
  out.push(`Regex \`${r1.pattern}\` over the raw text of every scoped file; a match counts when it sits in code, not in a comment or a literal. \`GardenVR.Input.\` and \`StandardInput.\` are not reads.`, '');
  if (r1.breaks.length) out.push(...hitList(r1.breaks), '');
  out.push(`Allowed reads inside the provider: ${r1.allowed.length}.`, ...hitList(r1.allowed), '');
  out.push(`Mentions in comments or literals (not reads): ${r1.mentions.length}.`, ...hitList(r1.mentions), '');
  const r4 = r.rules.rule4;
  out.push(`**Rule 4** (\`using UnityEngine\` in ${r4.scope}): **${r4.breaks.length}**.`, '');
  out.push(`Regex \`${r4.pattern}\` (multiline), code only. A fully qualified \`UnityEngine.\` in code breaks the same rule without a using: ${r4.qualified.length}. Mentions in comments or literals: ${r4.mentions.length}.`, '');
  if (r4.breaks.length) out.push(...hitList(r4.breaks), '');
  if (r4.qualified.length) out.push(...hitList(r4.qualified), '');
  if (r4.mentions.length) out.push(...hitList(r4.mentions), '');

  out.push(`## Top ${TOP} hotspots`, '');
  out.push('Each finding was written by reading the file, not from its numbers; `findings.json` names the blob it read. '
    + 'A finding marked *backlogged* is already open in the scan-sweep ledger of 2026-10-05 and is not raised twice.', '');
  for (const f of r.findings) {
    const where = `\`${f.path}${f.line ? `:${f.line}` : ''}\``;
    const text = f.finding ? `${f.kind}: ${f.finding}` : 'no finding recorded';
    const flags = [f.backlog ? `*backlogged*: ${f.backlog}` : null, f.stale ? `*stale*: read at blob ${f.readAtBlob.slice(0, 10)}, the file has changed since` : null].filter(Boolean);
    out.push(`${f.rank}. ${where} ${cell(text)}${flags.length ? ` (${flags.join('; ')})` : ''}`);
  }
  out.push('');
  if (r.findingsWithoutHotspot.length) out.push(`Findings for files no longer in scope: ${r.findingsWithoutHotspot.map(p => `\`${p}\``).join(', ')}.`, '');

  const table = (list, withFinding) => {
    const head = ['#', 'File', 'Total lines', 'Code lines', 'Churn commits', 'Churn lines', 'Score', 'Tested', 'Methods', 'Longest method', 'Deepest nesting'];
    const align = ['---:', '---', '---:', '---:', '---:', '---:', '---:', '---', '---:', '---', '---'];
    out.push(row(withFinding ? [...head, 'Finding'] : head), row(withFinding ? [...align, '---'] : align));
    const byPathFinding = new Map(r.findings.map(x => [x.path, x]));
    for (const f of list) {
      const cells = [f.rank, `\`${f.path}\``, f.totalLines, f.codeLines, f.churn.commits, f.churn.lines, f.score, yn(f), f.methods, longest(f), nest(f)];
      if (withFinding) {
        const x = byPathFinding.get(f.path);
        cells.push(x && x.finding ? cell(`${x.line ? `:${x.line} ` : ''}${x.kind}${x.backlog ? ' (backlogged)' : ''}`) : '');
      }
      out.push(row(cells));
    }
    out.push('');
  };
  out.push('## Ranked: runtime and editor code', '');
  out.push(`Score = code lines x (1 + churn commits since ${SINCE}). Longest method is in lines, with its name and first line; deepest nesting is in blocks, with the line of the deepest block.`, '');
  table(r.files, true);
  out.push('## Ranked: test files', '');
  out.push('Same measures. Tests are the net the top hotspots get refactored under, so they rank apart and get no finding here.', '');
  table(r.testFiles, false);
  return out.join('\n');
}

// `--run-tests`: the test record the next commit carries. Refuses a working tree with changes under the tested trees,
// so the counts always belong to a commit.
function runTests() {
  const sha = git(['rev-parse', '--verify', 'HEAD^{commit}']).toString().trim();
  const dirty = git(['status', '--porcelain', '--', ...TEST_TREES]).toString().trim();
  if (dirty) throw new Error(`uncommitted changes under ${TEST_TREES.join(', ')}; commit them first:\n${dirty}`);
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'gvr-health-'));
  const command = 'dotnet test shared/core-dotnet';
  const r = spawnSync('dotnet', ['test', 'shared/core-dotnet', '--logger', 'trx;LogFileName=run.trx', '--results-directory', dir], { cwd: root, stdio: ['ignore', 'inherit', 'inherit'] });
  const trx = path.join(dir, 'run.trx');
  if (!fs.existsSync(trx)) throw new Error(`dotnet test left no trx (exit ${r.status})`);
  const xml = fs.readFileSync(trx, 'utf8');
  const counters = Object.fromEntries([...(xml.match(/<Counters\b[^>]*>/) || [''])[0].matchAll(/(\w+)="(\d+)"/g)].map(m => [m[1], Number(m[2])]));
  const outcome = (xml.match(/<ResultSummary\s+outcome="([^"]+)"/) || [])[1] || 'unknown';
  const sdk = spawnSync('dotnet', ['--version'], { cwd: root }).stdout.toString().trim();
  const d = new Date();
  const record = {
    schema: 'garden-vr/dotnet-test-record/1',
    command, commit: sha, subject: git(['log', '-1', '--format=%s', sha]).toString().trim(),
    date: `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`, sdk,
    exitCode: r.status, outcome,
    total: counters.total, executed: counters.executed, passed: counters.passed, failed: counters.failed + (counters.error || 0),
    skipped: counters.notExecuted || 0,
    trees: Object.fromEntries(TEST_TREES.map(p => [p, treeAt(sha, p)])),
  };
  fs.rmSync(dir, { recursive: true, force: true });
  const out = path.join(root, TEST_RECORD);
  fs.mkdirSync(path.dirname(out), { recursive: true });
  fs.writeFileSync(out, layout(record) + '\n');
  console.log(`dotnet test at ${sha}: ${record.passed} passed, ${record.failed} failed, ${record.skipped} skipped of ${record.total} (exit ${r.status})`);
  console.log(`wrote ${TEST_RECORD}`);
  if (r.status !== 0) process.exitCode = 1;
}

// `--explain <path>`: every method the parser found in one file at the commit, to check a number by eye.
function explain(rev, p) {
  const sha = git(['rev-parse', '--verify', `${rev}^{commit}`]).toString().trim();
  const text = git(['cat-file', 'blob', `${sha}:${p}`]).toString('utf8');
  const src = text.startsWith(BOM) ? text.slice(1) : text;
  const m = measureText(src);
  const s = structure(tokenize(m.masked));
  console.log(`${p} at ${sha}: ${m.totalLines} lines, ${m.codeLines} code, ${m.commentLines} comment, ${m.blankLines} blank`);
  console.log(`types: ${s.types.map(t => `${t.kw} ${t.name}:${t.line}${t.nested ? ' (nested)' : ''}`).join(', ')}`);
  for (const x of s.methods) console.log(`  ${String(x.line).padStart(5)}-${String(x.endLine).padEnd(5)} ${String(x.lines).padStart(4)} lines  nesting ${x.nesting}  ${x.name}`);
  console.log(`deepest nesting ${s.deepest.depth} at line ${s.deepest.line}; unclosed ${s.unclosed}; unclassified ${s.unknown.length}`);
}

const a = args(process.argv.slice(2));
if (a['run-tests']) runTests();
else if (typeof a.explain === 'string') explain(typeof a.rev === 'string' ? a.rev : 'HEAD', a.explain);
else {
  const result = hotspots(typeof a.rev === 'string' ? a.rev : 'HEAD');
  const outDir = path.resolve(root, typeof a.out === 'string' ? a.out : 'docs/health');
  fs.mkdirSync(outDir, { recursive: true });
  fs.writeFileSync(path.join(outDir, 'baseline.json'), layout(result) + '\n');
  fs.writeFileSync(path.join(outDir, 'BASELINE.md'), markdown(result));
  const t = result.totals;
  console.log(`commit ${result.commit.sha}`);
  console.log(`${t.files} files, ${t.totalLines} lines, ${t.codeLines} code lines, ${t.methods} methods; rule 1 breaks ${t.rule1Breaks}, rule 4 breaks ${t.rule4Breaks}; parse notes ${t.parseNotes}`);
  console.log(`dotnet test: ${result.testRun ? `${result.testRun.passed}/${result.testRun.failed}/${result.testRun.skipped} at ${result.testRun.commit.slice(0, 10)}${result.testRun.sameTreesAsMeasured ? '' : ' (trees differ)'}` : 'no record'}`);
  for (const f of result.findings) if (!f.finding || f.stale) console.log(`finding ${f.finding ? 'stale' : 'missing'}: #${f.rank} ${f.path}`);
  console.log(`wrote ${path.relative(root, outDir).replace(/\\/g, '/')}/baseline.json and BASELINE.md`);
}
