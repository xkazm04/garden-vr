// The coach relay's logic, kept apart from HTTP so it can be tested with a fake client.
// Upgrade plan section 6.5 and 7: the API key lives only here; the app sends the prompt core built; the relay checks it,
// rate-limits per install, calls Claude Haiku, and returns one line of text or a failure. Nothing the user said is logged.

import Anthropic from '@anthropic-ai/sdk';

export const DEFAULT_MODEL = 'claude-haiku-5-5';
export const KINDS = ['Onboarding', 'Reflection'];
export const LIMITS = {
  userChars: 8000,          // the core prompts are under 3,000 characters with the longest answers
  maxTokensAsked: 600,      // what CoachRequest.MaxTokens may say
  maxTokensSent: 2048,      // room for adaptive thinking on top of a short reply
  perKeyPerMinute: 6,
  perKeyPerDay: 40,
  perIpPerDay: 300
};
const KEY_PATTERN = /^[A-Za-z0-9-]{8,64}$/;

/** In-memory counters. One process, so a restart forgets them; that only ever lets more through, never blocks. */
export function createLimiter(limits = LIMITS, now = () => Date.now()) {
  const hits = new Map();
  function count(id, windowMs) {
    const t = now();
    const list = (hits.get(id) || []).filter(x => t - x < windowMs);
    hits.set(id, list);
    return list.length;
  }
  return {
    allow(key, ip) {
      const minute = count('k:' + key, 60_000), day = count('d:' + key, 86_400_000), ipDay = count('i:' + ip, 86_400_000);
      if (minute >= limits.perKeyPerMinute || day >= limits.perKeyPerDay || ipDay >= limits.perIpPerDay) return false;
      const t = now();
      for (const id of ['k:' + key, 'd:' + key, 'i:' + ip]) hits.get(id).push(t);
      return true;
    }
  };
}

const reply = (status, body) => ({ status, body });
const failed = (status, failure) => reply(status, { ok: false, failure });

/**
 * handle({ body, installKey, ip }) -> { status, body }
 * body: { kind, system, user, maxTokens } as RelayProtocol.Encode writes it.
 * client: an Anthropic client (or a fake with messages.create). allowedSystems: the system prompts core can build.
 */
export function createHandler({ client, model = DEFAULT_MODEL, allowedSystems, limiter = createLimiter(), log = () => {}, now = () => Date.now() }) {
  const allowed = new Set(allowedSystems || []);
  return async function handle({ body, installKey, ip }) {
    const started = now();
    const done = (result, extra = {}) => {
      log({ t: new Date(started).toISOString(), kind: body && KINDS.includes(body.kind) ? body.kind : 'invalid', status: result.status,
        failure: result.body.ok ? null : result.body.failure, ms: now() - started, ...extra });
      return result;
    };
    if (!installKey || !KEY_PATTERN.test(installKey)) return done(failed(400, 'Rejected'));
    if (!body || typeof body !== 'object' || !KINDS.includes(body.kind)) return done(failed(400, 'Rejected'));
    if (typeof body.system !== 'string' || !allowed.has(body.system)) return done(failed(400, 'Rejected'));
    if (typeof body.user !== 'string' || body.user.length === 0 || body.user.length > LIMITS.userChars) return done(failed(400, 'Rejected'));
    if (!Number.isInteger(body.maxTokens) || body.maxTokens < 1 || body.maxTokens > LIMITS.maxTokensAsked) return done(failed(400, 'Rejected'));
    if (!limiter.allow(installKey, ip || 'unknown')) return done(failed(429, 'RateLimited'));

    let response;
    try {
      response = await client.messages.create({
        model,
        max_tokens: LIMITS.maxTokensSent,
        output_config: { effort: 'low' },
        system: body.system,
        messages: [{ role: 'user', content: body.user }]
      });
    } catch (error) {
      // Most specific first; APIConnectionError is a subclass of APIError in the TypeScript SDK.
      let upstream = 'error';
      if (error instanceof Anthropic.RateLimitError) upstream = 'upstream-rate-limit';
      else if (error instanceof Anthropic.APIConnectionError) upstream = 'upstream-connection';
      else if (error instanceof Anthropic.APIError) upstream = 'upstream-' + (error.status ?? 'error');
      return done(failed(200, 'Error'), { upstream });
    }
    const usage = { in_tokens: response.usage?.input_tokens ?? null, out_tokens: response.usage?.output_tokens ?? null };
    if (response.stop_reason === 'refusal') return done(failed(200, 'Refused'), usage);
    if (response.stop_reason === 'max_tokens') return done(failed(200, 'Error'), { ...usage, upstream: 'max-tokens' });
    const text = (response.content || []).filter(b => b.type === 'text').map(b => b.text).join('').trim();
    if (!text) return done(failed(200, 'Error'), { ...usage, upstream: 'empty' });
    return done(reply(200, { ok: true, text }), usage);
  };
}
