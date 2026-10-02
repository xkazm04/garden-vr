---
id: T-SUN-012
app: sundial
title: Generate and wire the Sundial sound - paper, ink and wood cues, the dusk chime, three arc beds and the optional dusk voice - within the Sundial credit pot
depends: [T-TER-010, T-SUN-009]
estimate_min: 100
touches: [apps/sundial/Assets/Audio/**, apps/sundial/Assets/Scripts/**, apps/sundial/Assets/Tests/**, tools/audio/ledger.jsonl, orchestration/runs/sundial/T-SUN-012/**]
---
> **HOST OVERRIDE (2026-10-02): audio ASSETS are produced by the dedicated audio agent** (branch `agent/audio`, merged
> to main by the host). **Do not call `tools/audio/elevenlabs.mjs` in this task.** Build/consume the code and wiring
> only, reading `apps/<app>/Assets/Audio/cues.json` and `Voice/lines.json` in the format fixed in
> `docs/audio/CHOICES.md` ("Manifest contract"). Owner choices in `docs/audio/CHOICES.md` outrank the bible. If an
> asset is not merged yet when you run, wire the cue as a placeholder (bible section 2) and list it in REPORT.md.

## Goal
The Sundial sounds like paper, ink and wood in a daylit kitchen, never like a game: the cue set in AUDIO-BIBLE 3.2
generated and wired through the shared cue service (owned by terrarium: consume it), three unpulsed 30 s arc beds that
crossfade as the shadow moves (off by default), and the optional voice for the three breaths if the owner has picked a
voice. The dusk-ritual mixdown meets -20 LUFS +/- 1, true peak <= -1.5 dBTP. Spend stays inside the Sundial pot (14,000).

## Read first
- `docs/audio/AUDIO-BIBLE.md` (sections 2, 3.2, 4, 5, 6, 7, 8); `docs/plans/sundial.md` section 7.
- `shared/packages/com.gardenvr.audio/README.md` and `tools/audio/mixdown.mjs` (T-TER-010).
- `apps/sundial/Assets/Audio/Voice/CAST.md` (written by the host after the owner's pick; if missing, skip the voice).

## Steps
0. `git merge --no-edit main`. Create `orchestration/runs/sundial/T-SUN-012/`. `node tools/audio/elevenlabs.mjs credits`;
   write the batch estimate (SFX ~4,000; beds ~6,000; voice ~1,100) in the report before spending.
1. SFX: each cue in 3.2 via `node tools/audio/elevenlabs.mjs sfx --text "<prompt>, very quiet, close, dry, no music, no
   voice" --seconds <s> --out apps/sundial/Assets/Audio/Sfx/<cue-id>.v<N>.mp3`; check each with `ffmpeg -af astats` (no
   clipping, no long silence); stop SFX at 4,500 credits.
2. Beds: `bed.morning`, `bed.midday`, `bed.dusk` via `elevenlabs.mjs music --seconds 30` with the prompts in section 6;
   fold tails into heads for seamless loops; verify 3 loops without a seam click; integrated -30 LUFS; stop at 6,000.
3. Voice (only if `CAST.md` has a `voice_id`): `Assets/Audio/Voice/lines.json` with the 8 lines from the plan (windows:
   in 1.8 s, out 2.5 s, others 4.0 s), 3 takes each, trimmed and normalised, measured, best fitting take kept; no em dash,
   no banned words. Otherwise write the catalog and mark the voice cues `placeholder: true`.
4. `apps/sundial/Assets/Audio/cues.json` covering every id in 3.2 plus voice and beds; wire: `dial.appear`, `tend.tock`,
   `tile.ink`, `tend.undo`, `tile.hatch`, `packet.open`, `seed.drop`, `bloom.flutter`, `dusk.chime`, `tab.tap`,
   `amb.kitchen`; beds crossfade 4 s when `ArcAt` changes (setting `Beds`, default off); voice follows the breath intents
   (never leads the hold).
5. Tests: every cue id called in Sundial scripts exists in the manifest; `Beds_CrossfadeOnArcChange`;
   `Voice_NeverLeadsTheHold` (if voice present).
6. Mixdown: run `Dusk_ThreeBreaths` with beds and voice on and `GARDEN_CUE_LOG` set; render and measure.
7. Commit per sub-step.

## Acceptance (each check runnable on Windows without a headset; say the command)
```bash
U="C:/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Unity.exe"; R=orchestration/runs/sundial/T-SUN-012
"$U" -batchmode -nographics -projectPath apps/sundial -runTests -testPlatform EditMode -testResults "$(pwd)/$R/editmode.xml" -logFile "$R/editmode.log"; grep -o '<test-run[^>]*>' "$R/editmode.xml"   # failed="0"
GARDEN_CUE_LOG="$(pwd)/$R/dusk-cues.jsonl" "$U" -batchmode -projectPath apps/sundial -runTests -testPlatform PlayMode -testFilter Dusk_ThreeBreaths -testResults "$(pwd)/$R/playmode.xml" -logFile "$R/playmode.log"; grep -o '<test-run[^>]*>' "$R/playmode.xml"   # failed="0"
node tools/audio/mixdown.mjs --log "$R/dusk-cues.jsonl" --manifest apps/sundial/Assets/Audio/cues.json --out "$R/mix-dusk.wav"
ffmpeg -hide_banner -i "$R/mix-dusk.wav" -af ebur128=peak=true -f null - 2>&1 | tail -12 | tee "$R/loudness.txt"   # I = -20 +/- 1 LUFS (with voice) or reported; TP <= -1.5 dBTP
node tools/audio/elevenlabs.mjs credits                                           # Sundial spend from the ledger <= 14,000 in total
```

## Evidence to leave (paths under orchestration/runs/sundial/T-SUN-012/)
`REPORT.md` (estimate vs billed per kind from the ledger; rejected takes and why), `editmode.xml`, `playmode.xml`,
`dusk-cues.jsonl`, `mix-dusk.wav`, `loudness.txt`, `loop-seams.txt`.

## Out of scope
Changes to `com.gardenvr.audio` (request via `REQUEST-audio.md`), Terrarium cues, picking the voice, localisation.
