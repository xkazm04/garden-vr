import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { loadEnv, backendOf, childEnv, cliArgs, fromCliResult, createCliClient, createCoachClient } from './backend.mjs';
import { createHandler } from './relay.mjs';

const SYSTEMS = JSON.parse(fs.readFileSync(new URL('./allowed-systems.json', import.meta.url), 'utf8')).systems;
const KEY = '0123456789abcdef0123456789abcdef';
const cliJson = (over = {}) => JSON.stringify({ type: 'result', is_error: false, result: 'A calm day.', stop_reason: 'end_turn', usage: { input_tokens: 900, output_tokens: 7 }, ...over });
const fakeRun = (out, seen = []) => async (bin, args, stdin, env, ms) => { seen.push({ bin, args, stdin, env, ms }); if (out instanceof Error) throw out; return out; };

test('the backend defaults to cli and refuses an unknown value', () => {
  assert.equal(backendOf({}), 'cli');
  assert.equal(backendOf({ COACH_BACKEND: ' API ' }), 'api');
  assert.throws(() => backendOf({ COACH_BACKEND: 'cloud' }), /COACH_BACKEND/);
});

test('.env fills what the environment lacks and never overrides it', () => {
  const f = path.join(fs.mkdtempSync(path.join(os.tmpdir(), 'relay-')), '.env');
  fs.writeFileSync(f, 'COACH_BACKEND=api\r\nPORT="9000"\n# comment\nRELAY_MODEL=x\n');
  const env = loadEnv(f, { RELAY_MODEL: 'kept' });
  assert.deepEqual(env, { RELAY_MODEL: 'kept', COACH_BACKEND: 'api', PORT: '9000' });
  assert.deepEqual(loadEnv(f + '.missing', { A: '1' }), { A: '1' });
});

test('a CLI child never sees an API key or the parent Claude session', () => {
  const env = childEnv({ PATH: 'p', ANTHROPIC_API_KEY: 'sk', CLAUDECODE: '1', CLAUDE_CODE_ENTRYPOINT: 'cli' });
  assert.deepEqual(env, { PATH: 'p' });
});

test('the CLI flags match CliCoachClient: own system prompt, no tools, no session, no settings', () => {
  assert.deepEqual(cliArgs({ model: 'claude-haiku-5-5', system: 'S', effort: 'low' }),
    ['-p', '--model', 'claude-haiku-5-5', '--output-format', 'json', '--system-prompt', 'S', '--effort', 'low',
      '--tools', '', '--no-session-persistence', '--setting-sources', '']);
});

test('the CLI result maps to the Messages shape; refusal and errors are told apart', () => {
  assert.deepEqual(fromCliResult(cliJson()), { stop_reason: 'end_turn', content: [{ type: 'text', text: 'A calm day.' }], usage: { input_tokens: 900, output_tokens: 7 } });
  assert.equal(fromCliResult(cliJson({ stop_reason: 'refusal' })).stop_reason, 'refusal');
  assert.throws(() => fromCliResult(cliJson({ is_error: true })), /cli-error/);
  assert.throws(() => fromCliResult('not json'));
});

test('through the relay, the CLI client sends the user text on stdin and returns the line', async () => {
  const seen = [];
  const client = createCliClient({ env: { ANTHROPIC_API_KEY: 'sk', PATH: 'p' }, run: fakeRun({ code: 0, stdout: cliJson(), stderr: '' }, seen) });
  const handle = createHandler({ client, allowedSystems: SYSTEMS });
  const r = await handle({ body: { kind: 'Reflection', system: SYSTEMS[0], user: 'quiet day', maxTokens: 120 }, installKey: KEY, ip: 'i' });
  assert.deepEqual(r, { status: 200, body: { ok: true, text: 'A calm day.' } });
  assert.equal(seen[0].stdin, 'quiet day');
  assert.equal(seen[0].args[seen[0].args.indexOf('--system-prompt') + 1], SYSTEMS[0]);
  assert.equal(seen[0].env.ANTHROPIC_API_KEY, undefined);
});

test('a CLI timeout or a dead exit is an Error failure with the reason in the log, never the user text', async () => {
  for (const out of [new Error('cli-timeout'), { code: 1, stdout: '', stderr: 'boom' }]) {
    const logs = [];
    const handle = createHandler({ client: createCliClient({ run: fakeRun(out) }), allowedSystems: SYSTEMS, log: e => logs.push(e) });
    const r = await handle({ body: { kind: 'Reflection', system: SYSTEMS[0], user: 'secret words', maxTokens: 120 }, installKey: KEY, ip: 'i' });
    assert.deepEqual(r.body, { ok: false, failure: 'Error' });
    assert.match(logs[0].upstream, /^cli-/);
    assert.doesNotMatch(JSON.stringify(logs), /secret words/);
  }
});

test('api needs a key; cli needs none', async () => {
  await assert.rejects(createCoachClient({ COACH_BACKEND: 'api' }), /ANTHROPIC_API_KEY/);
  assert.equal((await createCoachClient({ COACH_BACKEND: 'api', ANTHROPIC_API_KEY: 'sk-test' })).backend, 'api');
  assert.equal((await createCoachClient({})).backend, 'cli');
});
