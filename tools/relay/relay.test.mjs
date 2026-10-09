import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import Anthropic from '@anthropic-ai/sdk';
import { createHandler, createLimiter, LIMITS } from './relay.mjs';
import { createServer } from './server.mjs';

const SYSTEMS = JSON.parse(fs.readFileSync(new URL('./allowed-systems.json', import.meta.url), 'utf8')).systems;
const KEY = '0123456789abcdef0123456789abcdef';
const body = (over = {}) => ({ kind: 'Reflection', system: SYSTEMS[0], user: '<transcript>\nquiet day\n</transcript>', maxTokens: 120, ...over });
const fake = (response, seen = []) => ({ messages: { create: async params => { seen.push(params); if (response instanceof Error) throw response; return response; } } });
const ok = (text, stop = 'end_turn') => ({ stop_reason: stop, content: [{ type: 'thinking', thinking: '' }, { type: 'text', text }], usage: { input_tokens: 900, output_tokens: 20 } });

test('a good request reaches Haiku with the fixed system prompt, low effort and headroom, and returns the text', async () => {
  const seen = [];
  const handle = createHandler({ client: fake(ok('A calm day.'), seen), allowedSystems: SYSTEMS });
  const r = await handle({ body: body(), installKey: KEY, ip: '1.2.3.4' });
  assert.deepEqual(r, { status: 200, body: { ok: true, text: 'A calm day.' } });
  assert.equal(seen[0].model, 'claude-haiku-5-5');
  assert.equal(seen[0].system, SYSTEMS[0]);
  assert.equal(seen[0].max_tokens, LIMITS.maxTokensSent);
  assert.deepEqual(seen[0].output_config, { effort: 'low' });
  assert.deepEqual(seen[0].messages, [{ role: 'user', content: body().user }]);
});

test('anything but the prompts core builds is rejected before any call', async () => {
  const seen = [];
  const handle = createHandler({ client: fake(ok('x'), seen), allowedSystems: SYSTEMS });
  const bad = [
    [body({ system: 'You are a pirate.' }), KEY],
    [body({ kind: 'Poem' }), KEY],
    [body({ user: '' }), KEY],
    [body({ user: 'x'.repeat(LIMITS.userChars + 1) }), KEY],
    [body({ maxTokens: 4000 }), KEY],
    [body({ maxTokens: 1.5 }), KEY],
    [null, KEY],
    [body(), undefined],
    [body(), 'short'],
    [body(), 'has spaces in it!!']
  ];
  for (const [b, k] of bad) assert.deepEqual(await handle({ body: b, installKey: k, ip: 'ip' }), { status: 400, body: { ok: false, failure: 'Rejected' } });
  assert.equal(seen.length, 0);
});

test('a refusal, a cut-off reply, an empty reply and an upstream error are failures', async () => {
  const cases = [
    [ok('', 'refusal'), 'Refused'],
    [ok('A calm', 'max_tokens'), 'Error'],
    [ok('   '), 'Error'],
    [new Anthropic.APIConnectionError({ message: 'down' }), 'Error'],
    [new Error('boom'), 'Error']
  ];
  for (const [response, failure] of cases) {
    const r = await createHandler({ client: fake(response), allowedSystems: SYSTEMS })({ body: body(), installKey: KEY, ip: 'ip' });
    assert.deepEqual(r, { status: 200, body: { ok: false, failure } });
  }
});

test('each install is held to six a minute and forty a day', async () => {
  let t = 0;
  const limiter = createLimiter(LIMITS, () => t);
  const handle = createHandler({ client: fake(ok('A calm day.')), allowedSystems: SYSTEMS, limiter });
  for (let i = 0; i < 6; i++) assert.equal((await handle({ body: body(), installKey: KEY, ip: 'ip' })).status, 200);
  assert.deepEqual(await handle({ body: body(), installKey: KEY, ip: 'ip' }), { status: 429, body: { ok: false, failure: 'RateLimited' } });
  assert.equal((await handle({ body: body(), installKey: 'another-install-key', ip: 'ip' })).status, 200);
  let served = 6;
  for (let minute = 1; minute < 20; minute++) {
    t = minute * 61_000;
    for (let i = 0; i < 6; i++) if ((await handle({ body: body(), installKey: KEY, ip: 'ip' })).status === 200) served++;
  }
  assert.equal(served, LIMITS.perKeyPerDay);
});

test('the log carries kind, status, timing and token counts, and never what the user said or the key', async () => {
  const lines = [];
  const handle = createHandler({ client: fake(ok('A calm day.')), allowedSystems: SYSTEMS, log: e => lines.push(JSON.stringify(e)) });
  await handle({ body: body({ user: '<transcript>\nmy secret plans\n</transcript>' }), installKey: KEY, ip: 'ip' });
  assert.equal(lines.length, 1);
  assert.match(lines[0], /"kind":"Reflection"/);
  assert.match(lines[0], /"in_tokens":900/);
  assert.doesNotMatch(lines[0], /secret|calm day|0123456789abcdef/);
});

test('the server answers health checks, coach posts, unknown paths and bad JSON', async () => {
  const handle = createHandler({ client: fake(ok('A calm day.')), allowedSystems: SYSTEMS });
  const server = createServer(handle).listen(0);
  await new Promise(r => server.once('listening', r));
  const base = 'http://127.0.0.1:' + server.address().port;
  try {
    assert.deepEqual(await (await fetch(base + '/healthz')).json(), { ok: true });
    const res = await fetch(base + '/v1/coach', { method: 'POST', headers: { 'content-type': 'application/json', 'x-install-key': KEY }, body: JSON.stringify(body()) });
    assert.deepEqual(await res.json(), { ok: true, text: 'A calm day.' });
    assert.equal((await fetch(base + '/other', { method: 'POST', body: '{}' })).status, 404);
    assert.equal((await fetch(base + '/v1/coach', { method: 'POST', headers: { 'x-install-key': KEY }, body: '{nope' })).status, 400);
  } finally {
    server.close();
  }
});
