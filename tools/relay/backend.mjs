// Which model path the relay uses, chosen by one flag so local work never waits on an API key (owner, 2026-10-12).
//   COACH_BACKEND=cli  (default) the Claude Code CLI on this machine, billed to the owner's subscription. Local only.
//   COACH_BACKEND=api  the Anthropic API with ANTHROPIC_API_KEY. The path for the cloud relay once the product is proven.
// Both return the same client shape (messages.create -> { stop_reason, content, usage }), so relay.mjs, its checks, its
// limits and its failure mapping do not know which one runs.

import fs from 'node:fs';
import path from 'node:path';
import { spawn } from 'node:child_process';
import { fileURLToPath } from 'node:url';

export const BACKENDS = ['cli', 'api'];
const here = path.dirname(fileURLToPath(import.meta.url));
export const ROOT_ENV = path.resolve(here, '..', '..', '.env');

/** Reads KEY=value lines from the git-ignored .env at the repo root. Values already in the environment win. */
export function loadEnv(file = ROOT_ENV, env = process.env) {
  let text = '';
  try { text = fs.readFileSync(file, 'utf8'); } catch { return env; }
  for (const line of text.split(/\r?\n/)) {
    const m = line.match(/^\s*([A-Z0-9_]+)\s*=\s*(.*?)\s*$/);
    if (m && env[m[1]] === undefined) env[m[1]] = m[2].replace(/^(['"])(.*)\1$/, '$2');
  }
  return env;
}

export function backendOf(env = process.env) {
  const b = (env.COACH_BACKEND || 'cli').trim().toLowerCase();
  if (!BACKENDS.includes(b)) throw new Error(`COACH_BACKEND must be one of ${BACKENDS.join(', ')}, not "${b}"`);
  return b;
}

/** The environment a CLI child gets: never an API key (it must bill the subscription) and no parent Claude session. */
export function childEnv(env = process.env) {
  const out = { ...env };
  for (const k of Object.keys(out)) if (k === 'ANTHROPIC_API_KEY' || k === 'CLAUDECODE' || k.startsWith('CLAUDE_CODE_')) delete out[k];
  return out;
}

/** The same flags as GardenVR.CoachCli's CliCoachClient: the request's system prompt replaces Claude Code's, no tools,
 *  no session kept, no settings files. The user text goes in on stdin, so it is never parsed as a flag. */
export function cliArgs({ model, system, effort }) {
  return ['-p', '--model', model, '--output-format', 'json', '--system-prompt', system ?? '',
    ...(effort ? ['--effort', effort] : []), '--tools', '', '--no-session-persistence', '--setting-sources', ''];
}

/** Maps the CLI's JSON result to the Messages API shape relay.mjs reads. Throws on anything it cannot read. */
export function fromCliResult(stdout) {
  const d = JSON.parse(String(stdout).trim());
  if (d.stop_reason === 'refusal') return { stop_reason: 'refusal', content: [], usage: d.usage };
  if (d.is_error === true || typeof d.result !== 'string') throw new Error('cli-error');
  return { stop_reason: d.stop_reason || 'end_turn', content: [{ type: 'text', text: d.result }],
    usage: { input_tokens: d.usage?.input_tokens ?? null, output_tokens: d.usage?.output_tokens ?? null } };
}

/** run(bin, args, stdin, env, timeoutMs) -> Promise<{ code, stdout, stderr }>; replaced in tests. */
export function runProcess(bin, args, stdin, env, timeoutMs) {
  return new Promise((resolve, reject) => {
    const child = spawn(bin, args, { env, shell: false, windowsHide: true });
    let stdout = '', stderr = '';
    child.stdout.on('data', d => (stdout += d));
    child.stderr.on('data', d => (stderr += d));
    // taskkill /T reaches the process tree on Windows; child.kill does not (measured in agent-loop.mjs).
    const timer = setTimeout(() => {
      if (process.platform === 'win32') spawn('taskkill', ['/PID', String(child.pid), '/T', '/F'], { windowsHide: true });
      else child.kill('SIGKILL');
      reject(new Error('cli-timeout'));
    }, timeoutMs);
    child.on('error', e => { clearTimeout(timer); reject(e); });
    child.on('close', code => { clearTimeout(timer); resolve({ code, stdout, stderr }); });
    child.stdin.end(stdin);
  });
}

export function createCliClient({ claudePath = 'claude', timeoutMs = 30_000, effort = 'low', env = process.env, run = runProcess } = {}) {
  return {
    messages: {
      async create(params) {
        const user = (params.messages || []).map(m => m.content).join('\n');
        const r = await run(claudePath, cliArgs({ model: params.model, system: params.system, effort }), user, childEnv(env), timeoutMs);
        if (r.code !== 0 && !r.stdout.trim()) throw new Error('cli-exit-' + r.code);
        return fromCliResult(r.stdout);
      }
    }
  };
}

/** The relay's client for the configured backend. */
export async function createCoachClient(env = process.env) {
  const backend = backendOf(env);
  if (backend === 'api') {
    if (!env.ANTHROPIC_API_KEY) throw new Error('COACH_BACKEND=api needs ANTHROPIC_API_KEY (in .env or the environment)');
    const { default: Anthropic } = await import('@anthropic-ai/sdk');
    return { backend, client: new Anthropic({ apiKey: env.ANTHROPIC_API_KEY }) };
  }
  return { backend, client: createCliClient({ claudePath: env.COACH_CLAUDE_PATH || 'claude', timeoutMs: Number(env.COACH_CLI_TIMEOUT_MS || 30_000), env }) };
}
