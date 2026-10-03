#!/usr/bin/env node
// Offline mix of a cue log. No Unity audio required.
//
//   node tools/audio/mixdown.mjs --log cues.jsonl --manifest apps/terrarium/Assets/Audio/cues.json --out mix.wav [--duration s]
//
// Each played line is delayed with adelay, gained with volume, and summed with amix.
// A 3D cue whose file is mono is duplicated to stereo. Looping cues (room, bed) hold until the mix ends.
import { spawnSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';

function args(argv) {
  const out = { _: [] };
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    if (a.startsWith('--')) {
      const k = a.slice(2);
      const v = argv[i + 1];
      if (v === undefined || v.startsWith('--')) out[k] = true;
      else { out[k] = v; i++; }
    } else out._.push(a);
  }
  return out;
}

function die(msg) {
  console.error(msg);
  process.exit(1);
}

function run(cmd, cmdArgs) {
  const r = spawnSync(cmd, cmdArgs, { encoding: 'utf8' });
  if (r.error) die(`${cmd} failed: ${r.error.message}`);
  if (r.status !== 0) die(`${cmd} exited ${r.status}\n${(r.stderr || '').slice(-2000)}`);
  return r.stdout || '';
}

function probe(file) {
  const out = run('ffprobe', ['-v', 'error', '-print_format', 'json', '-show_streams', '-show_format', file]);
  const data = JSON.parse(out);
  const stream = (data.streams || []).find((s) => s.codec_type === 'audio') || (data.streams || [])[0] || {};
  const duration = Number(stream.duration || (data.format && data.format.duration) || 0);
  const channels = Number(stream.channels || 1);
  if (!Number.isFinite(duration) || duration <= 0) die(`no duration for ${file}`);
  return { duration, channels };
}

const opt = args(process.argv.slice(2));
if (!opt.log || !opt.manifest || !opt.out) die('usage: mixdown.mjs --log <cues.jsonl> --manifest <cues.json> --out <mix.wav> [--duration s]');

const manifest = JSON.parse(fs.readFileSync(opt.manifest, 'utf8'));
const byId = new Map();
for (const cue of manifest.cues || []) byId.set(cue.id, cue);
const audioRoot = path.dirname(path.resolve(opt.manifest));

let logText = fs.readFileSync(opt.log, 'utf8');
if (logText.charCodeAt(0) === 0xFEFF) logText = logText.slice(1);
const events = [];
for (const line of logText.split(/\r?\n/)) {
  const trimmed = line.trim();
  if (!trimmed) continue;
  const row = JSON.parse(trimmed);
  const cue = byId.get(row.cue) || {};
  const clip = row.clip || '';
  const file = path.isAbsolute(clip) ? clip : path.join(audioRoot, clip);
  if (!fs.existsSync(file)) die(`missing clip for ${row.cue}: ${file}`);
  events.push({
    t: Number(row.t) || 0,
    gain: Number(row.gainDb) || 0,
    space: cue.space || '2d',
    loop: Boolean(cue.loop),
    file,
  });
}

const outFile = path.resolve(opt.out);
fs.mkdirSync(path.dirname(outFile), { recursive: true });

if (events.length === 0) {
  const seconds = Number(opt.duration) > 0 ? Number(opt.duration) : 1;
  run('ffmpeg', ['-y', '-hide_banner', '-f', 'lavfi', '-i', 'anullsrc=r=48000:cl=stereo', '-t', String(seconds), '-c:a', 'pcm_s16le', outFile]);
  console.log(JSON.stringify({ ok: true, events: 0, out: outFile, seconds }));
  process.exit(0);
}

const probed = events.map((ev) => ({ ...ev, ...probe(ev.file) }));
let duration = Number(opt.duration);
if (!Number.isFinite(duration) || duration <= 0) {
  duration = 0;
  for (const ev of probed) {
    if (ev.loop) continue;
    duration = Math.max(duration, ev.t + ev.duration);
  }
  if (duration <= 0) duration = Math.max(...probed.map((ev) => ev.duration));
}

const ffArgs = ['-y', '-hide_banner', '-loglevel', 'error'];
for (const ev of probed) {
  if (ev.loop) ffArgs.push('-stream_loop', '-1');
  ffArgs.push('-i', ev.file);
}

const chains = [];
const labels = [];
probed.forEach((ev, i) => {
  const delay = Math.max(0, Math.round(ev.t * 1000));
  const hold = Math.max(0.05, duration - ev.t);
  const parts = [];
  if (ev.space === '3d' && ev.channels < 2) parts.push('pan=stereo|c0=c0|c1=c0');
  parts.push('aformat=sample_fmts=fltp:sample_rates=48000:channel_layouts=stereo');
  if (ev.loop) parts.push(`atrim=0:${hold.toFixed(3)}`, 'asetpts=PTS-STARTPTS');
  parts.push(`adelay=${delay}|${delay}`, `volume=${ev.gain}dB`);
  chains.push(`[${i}:a]${parts.join(',')}[a${i}]`);
  labels.push(`[a${i}]`);
});
chains.push(`${labels.join('')}amix=inputs=${probed.length}:duration=longest:dropout_transition=0:normalize=0,atrim=0:${duration.toFixed(3)}[mix]`);

ffArgs.push('-filter_complex', chains.join(';'), '-map', '[mix]', '-c:a', 'pcm_s16le', '-ar', '48000', '-ac', '2', outFile);
run('ffmpeg', ffArgs);
console.log(JSON.stringify({ ok: true, events: probed.length, out: outFile, seconds: Number(duration.toFixed(3)) }));
