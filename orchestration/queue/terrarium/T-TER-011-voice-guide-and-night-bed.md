---
id: T-TER-011
app: terrarium
title: Render the breathing voice guide from a line catalog timed to the breath, add the night bed, and check the full-ritual loudness
depends: [T-TER-010]
estimate_min: 100
touches: [apps/terrarium/Assets/Audio/**, apps/terrarium/Assets/Scripts/**, apps/terrarium/Assets/Tests/**, tools/audio/ledger.jsonl, orchestration/runs/terrarium/T-TER-011/**]
---
> **HOST OVERRIDE (2026-10-02): audio ASSETS are produced by the dedicated audio agent** (branch `agent/audio`, merged
> to main by the host). **Do not call `tools/audio/elevenlabs.mjs` in this task.** Build/consume the code and wiring
> only, reading `apps/<app>/Assets/Audio/cues.json` and `Voice/lines.json` in the format fixed in
> `docs/audio/CHOICES.md` ("Manifest contract"). Owner choices in `docs/audio/CHOICES.md` outrank the bible. If an
> asset is not merged yet when you run, wire the cue as a placeholder (bible section 2) and list it in REPORT.md.

## Goal
An optional voice guide that follows the user's breath (lines start on the intent, never before it), rendered as baked
clips whose measured durations fit their windows, plus one unpulsed 90 s night bed, both off until chosen. The
full-ritual mixdown with voice and bed meets -20 LUFS +/- 1 and true peak <= -1.5 dBTP.

## Read first
- `docs/audio/AUDIO-BIBLE.md` sections 4 (voice direction, windows, copy rules), 5, 6, 7; `docs/plans/terrarium.md`
  section 7 (the 14-line outline) and `docs/PLAN.md` D6 (the first ritual is voiceless; the guide is offered after it).
- `apps/terrarium/Assets/Audio/Voice/CAST.md` (written by the host after the owner picks a voice on 10-03).

## Steps
0. `git merge --no-edit main`. Create `orchestration/runs/terrarium/T-TER-011/`. Check credits.
1. **If `CAST.md` is missing or has no `voice_id`: do steps 2, 5 (with placeholders), 6 and 7 only, and report "blocked on
   voice pick".** Never pick a voice yourself.
2. `Assets/Audio/Voice/lines.json`: the 14 lines from the plan, ids `vo.ter.*`, each with `text`, `window_s`
   (in-lines 1.8, out-lines 2.5, others 4.0), `script_rev: 1`. Scan every text: no em dash, none of the banned words in
   AUDIO-BIBLE 4. Freeze the text (commit) before rendering.
3. Render 3 takes per line: `node tools/audio/elevenlabs.mjs tts --voice <voice_id> --text "<line>" --stability 0.65
   --similarity 0.75 --style 0.1 --out apps/terrarium/Assets/Audio/Voice/<id>.t<N>.mp3` (about 600 characters x 3 =
   1,800 credits; stop at 3,500). Trim silence (50 ms head, 250 ms tail) and normalise each kept take to -20 LUFS with
   ffmpeg (`silenceremove`, `loudnorm`); measure each with `ffprobe`; keep the best take that fits its window (reword and
   re-render rather than speed up beyond 5%). Fill `measured_s`, `take`, `voice_id`, `model` in `lines.json`.
4. **Night bed**: `node tools/audio/elevenlabs.mjs music --prompt "very quiet ambient pad at night, warm low strings and
   distant soft glass tones, slowly evolving, no percussion, no melody, seamless" --seconds 90 --out
   apps/terrarium/Assets/Audio/Music/bed.night.mp3` (~5,400). Make the loop seamless (fold the tail into the head with an
   ffmpeg crossfade script); verify 3 consecutive loops have no seam click (sample jump < -60 dB) and record integrated
   loudness (target -30 LUFS).
5. `VoiceGuide` (app): plays `vo.ter.in.*` on PinchHold start (rotating variants), `vo.ter.out.*` on Release, `mid` after
   breath 3, `close` on the answer, `return` on the first ritual after a gap; cuts a line with a 150 ms fade if the user is
   faster; never queues. Settings `VoiceGuide`, `NightBed` (both default off); the offer shell after the first answer
   (target `shell.voice`) toggles the guide on and remembers it.
6. Update `cues.json` (voice and music entries no longer placeholders). Tests: `VoiceGuide_NeverLeadsTheHold` (no in-line
   starts before a PinchHold intent), `VoiceGuide_CutsWhenUserIsFaster`, `Bed_DucksUnderVoice`.
7. Full-ritual mixdown with guide and bed on (run the six-breath PlayMode test with both settings on and
   `GARDEN_CUE_LOG="$(pwd)/$R/ritual-cues.jsonl"`); measure loudness.
8. Commit per sub-step.

## Acceptance (each check runnable on Windows without a headset; say the command)
```bash
U="C:/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Unity.exe"; R=orchestration/runs/terrarium/T-TER-011
grep -nP "\x{2014}" apps/terrarium/Assets/Audio/Voice/lines.json || echo "no em dash"
grep -niE "stress|anxiety|heal|therap|cure|medical|nervous system" apps/terrarium/Assets/Audio/Voice/lines.json || echo "no banned words"
node -e "const l=require('./apps/terrarium/Assets/Audio/Voice/lines.json');const bad=l.filter(x=>x.measured_s&&x.measured_s>x.window_s);console.log('over-window',bad.length)"   # 0 (or blocked on voice pick)
"$U" -batchmode -projectPath apps/terrarium -runTests -testPlatform PlayMode -testResults "$(pwd)/$R/playmode.xml" -logFile "$R/playmode.log"; grep -o '<test-run[^>]*>' "$R/playmode.xml"   # failed="0"
node tools/audio/mixdown.mjs --log "$R/ritual-cues.jsonl" --manifest apps/terrarium/Assets/Audio/cues.json --out "$R/mix-full.wav"
ffmpeg -hide_banner -i "$R/mix-full.wav" -af ebur128=peak=true -f null - 2>&1 | tail -12 | tee "$R/loudness.txt"   # I = -20 +/- 1 LUFS, TP <= -1.5 dBTP
```

## Evidence to leave (paths under orchestration/runs/terrarium/T-TER-011/)
`REPORT.md` (line table: id, window, measured, take, rewordings; credits estimate vs billed), `lines.json` copy,
`mix-full.wav`, `loudness.txt`, `loop-seam.txt`, `playmode.xml`.

## Out of scope
Choosing the voice (owner), Sundial narration, localisation, any music beyond the night bed.
