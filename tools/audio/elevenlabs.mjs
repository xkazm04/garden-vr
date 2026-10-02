#!/usr/bin/env node
// ElevenLabs generation for Garden VR, with a credit guard and a ledger.
//
//   node tools/audio/elevenlabs.mjs credits
//   node tools/audio/elevenlabs.mjs voices [--filter calm]
//   node tools/audio/elevenlabs.mjs sfx   --text "soft glass tink" --seconds 1.5 --out apps/terrarium/Assets/Audio/Sfx/jar-tink.mp3
//   node tools/audio/elevenlabs.mjs tts   --voice <voice_id> --text-file narration.txt --out .../breath-01.mp3 [--stability 0.6]
//   node tools/audio/elevenlabs.mjs music --prompt "..." --seconds 90 --out .../night-bed.mp3
//
// Every call checks remaining credits first and refuses when the call would dip below RESERVE (default 8000), so
// two agents cannot drain the plan. Each successful call appends a line to tools/audio/ledger.jsonl (committed) with
// the prompt, output path and credits before/after; keep a sidecar so any asset can be regenerated or replaced.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(here, '..', '..');
const API = 'https://api.elevenlabs.io';
const RESERVE = Number(process.env.GARDEN_AUDIO_RESERVE || 8000);

function loadEnv() {
  if (process.env.ELEVENLABS_API_KEY) return;
  for (const f of [path.join(root, '.env'), path.resolve(root, '..', 'pof', '.env')]) {
    if (!fs.existsSync(f)) continue;
    for (const line of fs.readFileSync(f, 'utf8').split(/\r?\n/)) {
      const m = line.match(/^ELEVENLABS_API_KEY=(.*)$/);
      if (m) { process.env.ELEVENLABS_API_KEY = m[1].trim().replace(/^["']|["']$/g, ''); return; }
    }
  }
}

function args(argv) {
  const out = { _: [] };
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    if (a.startsWith('--')) { const k = a.slice(2); const v = argv[i + 1]; if (v === undefined || v.startsWith('--')) out[k] = true; else { out[k] = v; i++; } }
    else out._.push(a);
  }
  return out;
}

const key = () => { loadEnv(); if (!process.env.ELEVENLABS_API_KEY) die('ELEVENLABS_API_KEY not found (.env)'); return process.env.ELEVENLABS_API_KEY; };
function die(msg, extra) { console.error(JSON.stringify({ ok: false, error: msg, ...extra }, null, 1)); process.exit(1); }

async function credits() {
  const r = await fetch(`${API}/v1/user/subscription`, { headers: { 'xi-api-key': key() } });
  if (!r.ok) die('subscription lookup failed', { status: r.status, body: await r.text() });
  const d = await r.json();
  return { tier: d.tier, used: d.character_count, limit: d.character_limit, remaining: d.character_limit - d.character_count, resetsAt: new Date(d.next_character_count_reset_unix * 1000).toISOString() };
}

async function guard(estimate, label) {
  const c = await credits();
  if (c.remaining - estimate < RESERVE) die(`refused: ${label} would leave ${c.remaining - estimate} credits, reserve is ${RESERVE}`, { credits: c, estimate });
  return c;
}

async function postAudio(url, body, out) {
  const r = await fetch(url, { method: 'POST', headers: { 'xi-api-key': key(), 'content-type': 'application/json', accept: 'audio/mpeg' }, body: JSON.stringify(body) });
  if (!r.ok) die('generation failed', { status: r.status, body: (await r.text()).slice(0, 600) });
  const buf = Buffer.from(await r.arrayBuffer());
  fs.mkdirSync(path.dirname(path.resolve(out)), { recursive: true });
  fs.writeFileSync(out, buf);
  return buf.length;
}

function ledger(entry) {
  fs.appendFileSync(path.join(here, 'ledger.jsonl'), JSON.stringify({ ts: new Date().toISOString(), ...entry }) + '\n');
  if (entry.out) fs.writeFileSync(entry.out + '.json', JSON.stringify(entry, null, 1));
}

const a = args(process.argv.slice(2));
const cmd = a._[0];
const need = (k) => a[k] ?? die(`--${k} is required`);

if (cmd === 'credits') {
  console.log(JSON.stringify(await credits(), null, 1));
} else if (cmd === 'voices') {
  const r = await fetch(`${API}/v1/voices`, { headers: { 'xi-api-key': key() } });
  const d = await r.json();
  const f = (a.filter || '').toLowerCase();
  for (const v of d.voices || []) {
    const desc = [v.name, v.labels && Object.values(v.labels).join(' '), v.description].filter(Boolean).join(' | ');
    if (!f || desc.toLowerCase().includes(f)) console.log(`${v.voice_id}  ${desc}`);
  }
} else if (cmd === 'sfx') {
  const text = need('text'), out = need('out'), seconds = Number(a.seconds || 2);
  // Sound effects bill per generation; budget generously (about 40 credits per second, minimum 100).
  const est = Math.max(100, Math.ceil(seconds * 40));
  const before = await guard(est, 'sfx');
  const bytes = await postAudio(`${API}/v1/sound-generation?output_format=mp3_44100_128`, { text, duration_seconds: seconds, prompt_influence: Number(a.influence || 0.5) }, out);
  const after = await credits();
  ledger({ kind: 'sfx', text, seconds, out, bytes, creditsBefore: before.remaining, creditsAfter: after.remaining });
  console.log(JSON.stringify({ ok: true, out, bytes, spent: before.remaining - after.remaining, remaining: after.remaining }));
} else if (cmd === 'tts') {
  const voice = need('voice'), out = need('out');
  const text = a['text-file'] ? fs.readFileSync(a['text-file'], 'utf8') : need('text');
  const before = await guard(text.length, 'tts');
  const body = { text, model_id: a.model || 'eleven_multilingual_v2', voice_settings: { stability: Number(a.stability || 0.6), similarity_boost: Number(a.similarity || 0.75), style: Number(a.style || 0.1), use_speaker_boost: true } };
  const bytes = await postAudio(`${API}/v1/text-to-speech/${voice}?output_format=mp3_44100_128`, body, out);
  const after = await credits();
  ledger({ kind: 'tts', voice, model: body.model_id, chars: text.length, text, out, bytes, creditsBefore: before.remaining, creditsAfter: after.remaining });
  console.log(JSON.stringify({ ok: true, out, bytes, spent: before.remaining - after.remaining, remaining: after.remaining }));
} else if (cmd === 'music') {
  const prompt = need('prompt'), out = need('out'), seconds = Number(a.seconds || 60);
  // Music is the most expensive call; refuse long tracks outright and budget conservatively.
  if (seconds > 180) die('music longer than 180 s is refused; loop a shorter bed instead');
  const est = Math.ceil(seconds * 60);
  const before = await guard(est, 'music');
  const bytes = await postAudio(`${API}/v1/music?output_format=mp3_44100_128`, { prompt, music_length_ms: Math.round(seconds * 1000), force_instrumental: a.vocals ? false : true }, out);
  const after = await credits();
  ledger({ kind: 'music', prompt, seconds, out, bytes, creditsBefore: before.remaining, creditsAfter: after.remaining });
  console.log(JSON.stringify({ ok: true, out, bytes, spent: before.remaining - after.remaining, remaining: after.remaining }));
} else {
  console.log('usage: elevenlabs.mjs credits | voices [--filter x] | sfx --text --seconds --out | tts --voice --text|--text-file --out | music --prompt --seconds --out');
  process.exit(cmd ? 1 : 0);
}
