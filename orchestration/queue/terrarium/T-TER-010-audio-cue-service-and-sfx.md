---
id: T-TER-010
app: terrarium
title: Build the cue-based audio service and offline mixdown, generate the Terrarium SFX set, and wire it to the ritual
depends: [T-TER-005]
estimate_min: 120
touches: [shared/packages/com.gardenvr.audio/**, tools/audio/mixdown.mjs, tools/audio/ledger.jsonl, apps/terrarium/Assets/Audio/**, apps/terrarium/Assets/Scripts/**, apps/terrarium/Assets/Tests/**, orchestration/runs/terrarium/T-TER-010/**]
---
> **HOST OVERRIDE (2026-10-02): audio ASSETS are produced by the dedicated audio agent** (branch `agent/audio`, merged
> to main by the host). **Do not call `tools/audio/elevenlabs.mjs` in this task.** Build/consume the code and wiring
> only, reading `apps/<app>/Assets/Audio/cues.json` and `Voice/lines.json` in the format fixed in
> `docs/audio/CHOICES.md` ("Manifest contract"). Owner choices in `docs/audio/CHOICES.md` outrank the bible. If an
> asset is not merged yet when you run, wire the cue as a placeholder (bible section 2) and list it in REPORT.md.

## Goal
`com.gardenvr.audio` (you own it; Sundial consumes it in T-SUN-012) plays named cues from a manifest with buses,
priority bands, ducking, cooldowns and variant rotation exactly as `docs/audio/AUDIO-BIBLE.md` section 2 says, logs every
played cue, and an offline mixdown script turns a cue log into a WAV whose loudness ffmpeg can measure. Then the
Terrarium SFX set (section 3.1) is generated within its share of the pot and wired to the ritual.

## Read first
- `docs/audio/AUDIO-BIBLE.md` (all; sections 2, 3.1, 5, 7, 8 are the spec); `docs/PLAN.md` section 7 (credit rules).
- `tools/audio/elevenlabs.mjs` (usage line at the top; it refuses calls below the 8,000 reserve and writes
  `tools/audio/ledger.jsonl` and a `.json` sidecar per file).
- `shared/packages/com.gardenvr.audio/README.md`.

## Steps
0. `git merge --no-edit main`. Create `orchestration/runs/terrarium/T-TER-010/`. Run
   `node tools/audio/elevenlabs.mjs credits` and record the result.
1. Package `Runtime/`: `CueManifest` (loads `Assets/Audio/cues.json`: id -> clips, bus, `space` 2D/3D, `band` P0-P4,
   `cooldownMs`, `pitchJitter`, `placeholder`), `AudioCueService` (`Play(id, Transform at = null, float pitchSemitones = 0)`,
   `Stop(id)`, max 8 voices, lowest band yields first, round-robin variants, no identical clip within 3 s, cooldown drops,
   music ducks 6 dB under `voice` with 300 ms attack / 800 ms release, ambient ducks 3 dB, one voice line at a time, a
   new voice line replaces the old, `Mute` persistent and outranking all), `CueLog` (development builds and tests: JSONL
   `{t, cue, clip, bus, gainDb, pos}`), placeholder behaviour (missing clip: silent, logs `PLACEHOLDER cue <id>` once).
2. `tools/audio/mixdown.mjs`: `--log <cues.jsonl> --manifest <cues.json> --out mix.wav [--duration s]` renders the cue log
   offline with ffmpeg (`adelay` + `volume` + `amix`, mono to stereo for 3D cues at their logged gain), no Unity audio needed.
3. Write the batch estimate (AUDIO-BIBLE 3.1: about 37 generations, ~3,700 credits before retakes; Terrarium pot 18,000,
   of which ~5,000 for SFX) in the report, then generate each cue with
   `node tools/audio/elevenlabs.mjs sfx --text "<prompt>, very quiet, close, dry, no music, no voice" --seconds <s> --out apps/terrarium/Assets/Audio/Sfx/<cue-id>.v<N>.mp3`.
   Listen-check each file you keep by its waveform stats (`ffmpeg -af astats`): no clipping, no long silence; delete
   rejects from `Assets/` (the ledger keeps them). Stop SFX generation at 5,500 credits spent and report.
4. `apps/terrarium/Assets/Audio/cues.json` with every Terrarium cue id from 3.1 (the voice and music ids too, marked
   `placeholder: true` until T-TER-011). Import settings: Vorbis 0.6; SFX decompress on load.
5. Wire: `jar.land`, `jar.lid`, `breath.exhale.end` (end of each counted exhale only), `fog.hiss`, `answer.chime`,
   `dew.drop`, `moss.ripple`, `habit.pluck` (pitch 0/+3/+5 by plant size), `habit.undo`, `pause.hold`, `pebble.tap`,
   `amb.room` (loop). Missed day: nothing.
6. EditMode tests: every id used in code exists in the manifest (scan the scripts for `Play("...")`); cooldown drops;
   duck applies and releases; variant rotation never repeats within 3 s; mute outranks.
7. Mixdown of the six-breath playback: run the PlayMode test from T-TER-005 with the cue log on (the test writes the log
   to the path in env var `GARDEN_CUE_LOG`, default `Application.persistentDataPath/logs/cues.jsonl`; copy it to
   `$R/ritual-cues.jsonl`), then mixdown and measure.
8. Commit per sub-step.

## Acceptance (each check runnable on Windows without a headset; say the command)
```bash
U="C:/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Unity.exe"; R=orchestration/runs/terrarium/T-TER-010
"$U" -batchmode -nographics -projectPath apps/terrarium -runTests -testPlatform EditMode -testResults "$(pwd)/$R/editmode.xml" -logFile "$R/editmode.log"; grep -o '<test-run[^>]*>' "$R/editmode.xml"   # failed="0"
GARDEN_CUE_LOG="$(pwd)/$R/ritual-cues.jsonl" "$U" -batchmode -projectPath apps/terrarium -runTests -testPlatform PlayMode -testFilter Ritual_SixBreaths_ByPlayback -testResults "$(pwd)/$R/playmode.xml" -logFile "$R/playmode.log"; grep -o '<test-run[^>]*>' "$R/playmode.xml"   # failed="0"
node tools/audio/mixdown.mjs --log "$R/ritual-cues.jsonl" --manifest apps/terrarium/Assets/Audio/cues.json --out "$R/mix.wav"
ffmpeg -hide_banner -i "$R/mix.wav" -af ebur128=peak=true -f null - 2>&1 | tail -12 > "$R/loudness.txt"; cat "$R/loudness.txt"   # report I (LUFS) and true peak; SFX-only mix expected below -20 LUFS, TP <= -1.5 dBTP
grep -c '"kind": "sfx"\|"kind":"sfx"' tools/audio/ledger.jsonl; node tools/audio/elevenlabs.mjs credits   # spend within ~5,500 for this task
grep -c "PLACEHOLDER" "$R/playmode.log"   # only the voice / music ids
```

## Evidence to leave (paths under orchestration/runs/terrarium/T-TER-010/)
`REPORT.md` (estimate vs billed per cue from the ledger; credits before/after; which cues were regenerated and why),
`editmode.xml`, `playmode.xml`, `ritual-cues.jsonl`, `mix.wav`, `loudness.txt`.

## Out of scope
Narration and music (T-TER-011); Sundial cues (T-SUN-012); spatializer plugins; any change to `elevenlabs.mjs`.
