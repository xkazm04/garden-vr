# Audio bible - Terrarium and Sundial

Sound is quiet company. It marks what the user did, never what they failed to do; it never counts, scores or
celebrates loudly; it is natural material (glass, moss, water, paper, wood, ink, air) recorded close and kept small.

Sources: round-1 audio tables (`arena/habit-garden/entries/claude-claude-opus-5-5_high/variant-1/index.html` section 06:
"no music bed, no lyrics", "the only reward sound", "no sad sound, ever"; `.../variant-2/index.html` section 06: the tock,
the dusk chime "a close, not a reward", ambient beds per arc), the owner's request (subtle SFX, breathing narration, calm
background music), `tools/audio/elevenlabs.mjs` (rates and guard), and the registry subjects in
`docs/knowledge/registry-consult.md` (spatial-audio-scene-authoring, adaptive-music-authoring,
game-dialogue-voice-pipeline, voice-io).

## 1. Direction (both apps)

1. **Subtle.** Every cue is shorter and quieter than the first instinct. If a cue is noticed before the visual it
   belongs to, it is too loud.
2. **Natural.** Real materials, close-miked, dry; no synth bleeps, no UI clicks, no coins, no fanfares, no whooshes
   with a gamey swell.
3. **Never gamified.** One reward sound per app (Terrarium's answer chime; Sundial's dusk chime is a close, not a
   reward). No sound for a missed day, ever. No count-up, no level-up, no "combo".
4. **Marks the end, not the start.** The exhale resonance plays at the end of an exhale; the voice speaks after the
   user's intent, never before it (it never paces the user faster than their hold).
5. **Silence is designed.** Returning after a gap is silent apart from the room. Pause is near-silent.

## 2. Engine rules (`com.gardenvr.audio`, owner: terrarium, T-TER-010)

- **Cue service**: app code plays cue ids (`audio.Play("answer.chime", at: jarTransform)`); the manifest
  `apps/<app>/Assets/Audio/cues.json` maps each id to clips, bus, 2D/3D, priority band, concurrency, cooldown, variant
  rotation and pitch jitter. App code never references an `AudioClip` directly.
- **Buses and priority bands**: `voice` (P0) > `reward` (P1) > `interaction` (P2) > `ambient` (P3) > `music` (P4).
  Maximum 8 simultaneous voices; the lowest band yields first.
- **Narration is protected by ducking, not by reservation** (one mechanism, written down): while `voice` plays,
  `music` ducks 6 dB (attack 300 ms, release 800 ms) and `ambient` ducks 3 dB. At most one voice line audible; a new
  line replaces the old one; stop is immediate.
- **2D or 3D, decided per cue**: narration, music, room tone are 2D; everything that comes from an object (jar, dial,
  plant, tile, seed) is 3D at that object (logarithmic rolloff, min 0.2 m, max 3 m). PC uses Unity's panner; Quest
  (Phase 2) uses the Meta XR Audio spatializer behind the same cue ids.
- **Repetition**: variants rotate round-robin with +/- 3% pitch jitter; no identical clip twice within 3 s.
- **Cooldowns**: listed per cue below; a cue inside its cooldown is dropped, not queued.
- **Mute and preferences**: global mute outranks everything and persists; "voice guide" and "beds" are separate
  settings; muted and unavailable are different states in the settings view.
- **Placeholders**: a cue whose clip is missing plays nothing and logs `PLACEHOLDER cue <id>` once; the manifest marks it
  `"placeholder": true`. A placeholder never carries a real-looking path.
- **Cue log**: in development builds and tests every played cue is appended to a JSONL log
  (`{t, cue, clip, bus, gainDb, pos}`) so a mixdown and loudness check can be made without real-time audio
  (`tools/audio/mixdown.mjs`, T-TER-010).

## 3. Cue lists

Seconds are generation lengths (the tool bills SFX at ~40 credits per second, minimum 100). Prompts are starting
points for `node tools/audio/elevenlabs.mjs sfx --text "<prompt>" --seconds <s> --out <path>`; every prompt ends with
", very quiet, close, dry, no music, no voice".

### 3.1 Terrarium (night, glass, moss, breath)

| Cue id | Trigger | Prompt | Bus / space | Cooldown | Variants x s |
|---|---|---|---|---|---|
| `amb.room` | session start, loops | quiet room tone at night, soft air, faint distant hum, no clicks | ambient / 2D, -40 LUFS | - | 1 x 20 (crossfaded loop) |
| `jar.land` | the jar settles | a single soft tink of a small glass jar set down on a wooden desk, short decay | interaction / 3D jar | once per placement | 3 x 1 |
| `jar.lid` | cork breathes (first run, session start) | a whisper-quiet puff of air from a cork lid | ambient / 3D cork | 10 s | 2 x 1.5 |
| `breath.exhale.end` | end of each counted exhale | one quiet glass resonance, a single soft ringing partial from a glass jar | interaction / 3D jar | once per breath | 3 x 2 |
| `fog.hiss` | release (fog starts) | barely audible soft hiss of condensation on glass | ambient / 3D jar, -36 dB | 2 s | 1 x 3 |
| `answer.chime` | the frond stays (the only reward sound) | three delicate soft glass chime notes, warm, 2.5 second decay | reward / 3D new frond | once per answer | 3 x 3 |
| `dew.drop` | dew bead leaves the frond tip | a tiny water droplet falling onto moss | interaction / 3D frond tip | 1 s | 3 x 0.6 |
| `moss.ripple` | light ripples through the moss | a soft airy shimmer rising, like light through leaves | reward / 3D moss | once per answer | 2 x 1.5 |
| `seed.appear` | seed packets appear | soft rustle of small paper seed packets | interaction / 3D desk edge | once | 2 x 1 |
| `habit.pluck` | check-in, companion grows a leaf | a short soft wooden pluck, muted kalimba-like single note | interaction / 3D companion; pitch +0 / +3 / +5 semitones by plant size | 120 ms | 2 x 1 |
| `habit.undo` | check-in undone | a soft reversed wooden pluck | interaction / 3D label | 300 ms | 2 x 1 |
| `pause.hold` | pause (the jar holds its breath) | a low soft sustained hum fading out | ambient / 2D | 2 s | 1 x 2 |
| `lookback.shimmer` | day-7 look-back | slow gentle sparkling glass shimmer | reward / 3D jar | once per look-back | 2 x 3 |
| `pebble.tap` | settings pebble poked | a small smooth pebble tapped on wood | interaction / 3D pebble | 200 ms | 2 x 0.5 |
| missed day | - | **nothing** | - | - | - |

### 3.2 Sundial (daylight, paper, ink, wood)

| Cue id | Trigger | Prompt | Bus / space | Cooldown | Variants x s |
|---|---|---|---|---|---|
| `amb.kitchen` | session start, loops | quiet daytime kitchen room tone, faint window air, no voices, no clatter | ambient / 2D, -40 LUFS | - | 1 x 20 |
| `dial.appear` | the dial draws itself | a soft page turn and a light pencil sketching on paper | interaction / 3D dial | once | 2 x 2 |
| `tend.tock` | a tend (pinch) | a single soft wooden tock with a slight downward pitch, two warm partials | interaction / 3D plant | 80 ms | 3 x 0.5 |
| `tile.ink` | a tile fills | a fountain-pen nib making one short stroke on paper | interaction / 3D tile | 150 ms | 3 x 0.6 |
| `tend.undo` | undo inside 6 s | a soft eraser brushing paper | interaction / 3D tile | 300 ms | 2 x 0.8 |
| `tile.hatch` | yesterday logged late | a pencil hatching four quick light strokes | interaction / 3D tile | 300 ms | 2 x 1 |
| `packet.open` | seed packet opens | a small paper envelope opening | interaction / 3D packet | 300 ms | 2 x 1 |
| `seed.drop` | seeds drop into arcs | soft seeds falling onto dry soil | interaction / 3D arc | 500 ms | 2 x 0.6 |
| `bloom.flutter` | bloom pulse | a tiny paper flutter, like a petal | interaction / 3D plant | 500 ms | 2 x 0.8 |
| `dusk.chime` | the dusk ritual completes (a close) | one soft low chime, warm, gentle 2 second decay | reward / 3D dusk plant | once per ritual | 3 x 2.5 |
| `tab.tap` | settings tab poked | a light tap on a paper card | interaction / 3D tab | 200 ms | 2 x 0.4 |
| missed day | - | **nothing** | - | - | - |

## 4. Voice direction (narration)

- **One voice per app**, chosen by the owner from the host's audition (Sat 10-03: four calm library voices from
  `node tools/audio/elevenlabs.mjs voices --filter calm`, each reading the same 300-character passage per app). Record the
  chosen `voice_id`, the model (`eleven_multilingual_v2`, pinned) and the voice's terms in
  `apps/<app>/Assets/Audio/Voice/CAST.md` before any line is rendered.
- **Character**: warm, low-mid, unhurried, close but not whispered; a friend sitting beside you, not a coach, not a
  meditation teacher, not ASMR. Terrarium: softer and slower; Sundial: lighter, a little brighter.
- **Settings** (tool flags): `--stability 0.65 --similarity 0.75 --style 0.1`; render each line with its neighbours in
  mind (same session, same settings); trim to 50 ms head and 250 ms tail of silence; normalise to -20 LUFS.
- **Timing is measured, never assumed**: every line has a window; the take's measured duration must fit it. Breath-in
  lines play at the start of a hold and must end within 1.8 s (the first half of a 4 s inhale); breath-out lines within
  2.5 s. An overrun is fixed by rewording first; a speed change only within +/- 5%.
- **Triggered by intents, not timers**: `vo.*.in.*` on PinchHold start, `vo.*.out.*` on Release; if the user is faster
  than the line, the line is cut with a 150 ms fade, never queued.
- **Line catalog**: `apps/<app>/Assets/Audio/Voice/lines.json` with `{id, text, window_s, take, measured_s, voice_id,
  model, settings, script_rev}`. Freeze the text before the bulk render; every re-render is logged (it costs credits).
- **Copy rules**: no medical or therapeutic words ("stress", "anxiety", "heal", "therapy", "calm your nervous system"
  and the like); no promises; no em dash; every spoken line also exists as on-screen (etched or inked) text when the
  voice is off.
- **Script outlines**: Terrarium `docs/plans/terrarium.md` section 7 (14 lines, ~600 characters); Sundial
  `docs/plans/sundial.md` section 7 (8 lines, ~350 characters).

## 5. Loudness targets and how they are measured (gate A6)

| Stem | Target | Notes |
|---|---|---|
| Full-session mixdown (one complete ritual) | **-20 LUFS integrated +/- 1**, true peak <= **-1.5 dBTP** | between the console (-24) and handheld (-18) conventions: a quiet app on near-ear speakers; recorded here as this project's choice |
| Narration | -20 LUFS per line (+/- 1) | always >= 10 dB above the music bed |
| Music beds | -30 LUFS integrated | ducks 6 dB under narration |
| Room tones | -40 LUFS | felt more than heard |
| SFX | peaks <= -12 dBFS; the reward chime short-term <= -18 LUFS | no SFX louder than narration |

Measurement (no real-time audio needed): the PlayMode test writes the cue log; `node tools/audio/mixdown.mjs --log
<cues.jsonl> --manifest apps/<app>/Assets/Audio/cues.json --out mix.wav` renders it offline with ffmpeg; then
`ffmpeg -hide_banner -i mix.wav -af ebur128=peak=true -f null - 2>&1 | tail -12` reports integrated loudness and true
peak. The owner listens to the same `mix.wav` at R2. (Unity audio output in batchmode may be inert; nothing here depends
on it.)

## 6. Music direction

- **Unpulsed beds, not songs**: no beat, no melody hook, no lyrics, no build-up, no ending. Declared unpulsed in the
  manifest (no tempo, no bar grid). Generated with the music endpoint only (`elevenlabs.mjs music`, instrumental).
- **Terrarium "night bed"** (90 s seamless loop, off by default): very quiet ambient pad at night, warm low strings and
  distant soft glass tones, slowly evolving, no percussion, no melody.
- **Sundial arc beds** (3 x 30 s loops, off by default; crossfade 4 s when the shadow enters an arc): `bed.morning`
  faint morning air, light high pad; `bed.midday` warm soft room, gentle sustained tones; `bed.dusk` low lilac pad, slow.
- **Acceptance** (beyond "it decoded"): 3 consecutive loops play with no click (sample discontinuity under -60 dB at the
  seam) and no audible hole (the decay tail is folded into the loop head); integrated -30 LUFS; the owner hears it as
  background at R2.

## 7. Credit budget

Guard: the tool refuses a call that would leave fewer than 8,000 credits. Estimates use the tool's rates; the ledger
`tools/audio/ledger.jsonl` is the authority on what was billed.

| Pot | Credits | Breakdown |
|---|---|---|
| Host audition (Sat 10-03) | 4,000 | 4 voices x (300 + 300 characters) = 2,400; credit probe and one SFX test per kind; slack |
| **Terrarium** | **18,000** | SFX: 15 cues, ~37 generations, ~3,700 + 35% retakes = 5,000; narration: ~600 characters x 3 takes + retakes = 2,500-3,500; night bed: 90 s x 60 = 5,400 + one 20 s fix = 6,600; slack 2,900 |
| **Sundial** | **14,000** | SFX: 12 cues, ~26 generations, ~3,100 + retakes = 4,000-4,500; narration: ~350 x 3 = 1,100 (cap 1,500); arc beds: 3 x 30 s x 60 = 5,400 + one 10 s fix = 6,000; slack 2,000 |
| Rework reserve (host) | 8,000 | released per request after R2 |
| Guard reserve | 8,000 | never spent |
| **Total** | **52,000** | |

Each audio task writes its batch estimate before spending, checks `credits`, and reports billed versus estimated from
the ledger. Quota or refusal: stop audio work, record the provider's reset time, finish the rest, report.

## 8. Files

`apps/<app>/Assets/Audio/Sfx/<cue-id>.v<N>.mp3`, `.../Voice/<line-id>.t<N>.mp3`, `.../Music/<bed-id>.mp3`, each with
the tool's `.json` sidecar; `cues.json` (manifest), `Voice/lines.json`, `Voice/CAST.md`. Rejected takes are deleted
from `Assets/` but stay in the ledger. Import: Vorbis, quality 0.6 (SFX: decompress on load; beds and voice:
streaming).
