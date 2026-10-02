# Terrarium - plan (MVP = the competition scope)

App: `apps/terrarium` (Unity 6.6 URP). Agent: Grok "terrarium" on `agent/terrarium`. Art direction: **A/1-03 Night
Moss** - extend reality, add glow and magic on top. Programme, gates and dates: `docs/PLAN.md`. Style bible:
`docs/art/terrarium-style.md`. Audio: `docs/audio/AUDIO-BIBLE.md`.

Sources (relative to `C:\Users\kazda\kiro\personas\.contest\`):

- Round 1 design: `arena/habit-garden/entries/claude-claude-opus-5-5_high/variant-1/index.html` (+ `NOTES.md`): scope,
  journey, hand vocabulary, growth rules, data model, audio table.
- Round 2 spike: `arena/habit-garden-r2/entries/claude-claude-opus-5-5_high-v1/variant-1/` (`index.html`, `RUNBOOK.md`,
  `evidence/`): the pure C# core (`spike/core/Runtime/*.cs`, 16 xUnit tests), Operator e2e 6/6, 12 Windows workarounds.
- Round 3 fidelity gate: `arena/habit-garden-r2-r3/entries/claude-claude-opus-5-5_high-v1/variant-1/` (`index.html`,
  `RUNBOOK.md`): the night jar in URP at the owner's framing, gap ledger, cost table, Quest techniques; assets built by
  `spike/fidelity/scripts/{blender_heroes.py,paint_textures.py,make_plates.py}` (now `tools/blender/`), shaders
  `spike/unity/Assets/Fidelity/Shaders/F*.shader`, driver `FidelityHero.cs` (now `docs/contest/seed-unity/`).
- Art target: `staging/habit-garden/reference/A1-03-night-moss-{1,2}.png` (now `shared/assets/art-reference/`),
  follow-up prompts in `staging/habit-garden/OWNER-CHOICE.md`.

## 1. What changes from the contest reports

| Topic | Contest said | This plan | Why |
|---|---|---|---|
| Daily moment | Round 1: morning, warm botanical glass | **Wind-down**, Night Moss glow; usable any time | Owner chose A1-03; the storyboard ties it to evening (`OWNER-CHOICE.md`) |
| Looks | Round 2: A Morning Glasshouse by day, C Night Lantern by night | **One look**, Night Moss, perfected | Round 3 rendered Night Moss only; two looks halve the art time |
| Missed day | Round 1: plants droop 10 / 22 degrees | **Glow dims to the 0.6 floor; lean at most 6 degrees**; the waiting fiddlehead stays coiled and dim | Night Moss expresses vitality as light; F2 prompt: "one quieter frond still healthy but unlit" |
| Flowers | Spike: every 7th frond | **First flower at 6 fronds, then every 6** (PLAN D5) | Round 1: one miss in a week still flowers on day 7 |
| Day boundary | Round 1: 04:00; spike: midnight | **03:00**, a setting (PLAN D3) | One rule for both apps |
| Voice | Not in round 1 (no music bed, no lyrics) | Optional breathing narration, offered after the first answer (PLAN D6); a very low night bed, off by default | Owner asked for narration and calm music; registry `voice-ux-integration` |
| Stack | Unity + Meta XR v207 + XR Simulator from day 1 | **PC-first**, no Meta packages until the gate | Decision 0002 |
| Glass | Round 3: fresnel rim, fog, droplets; no refraction | Same; refraction of the room is a platform limit, excluded from parity | Round 3 gap ledger: the compositor owns passthrough pixels |

## 2. Feature scope and the MVP cut line

### In (MVP, the competition scope)

1. **The jar on the desk.** PC: a seated rig over a room plate, the jar on `DeskAnchor` 40 cm ahead, 30 cm below eye.
   Quest (Phase 2): passthrough, the scene table, a spatial anchor, place-by-pinch fallback.
2. **The six-breath ritual.** PinchHold = inhale, Release = exhale; breath counts after a hold >= 1.2 s and a release
   >= 1.0 s (`BreathConfig`); fidgets ignored, never punished; tracking drops bridged (0.4 s) or paused. The pace ring
   suggests 4 s in / 6 s out and re-syncs on every pinch; it never marks a breath wrong.
3. **The garden answers.** The fiddlehead uncoils with each hold; the glass fogs bottom-up on each release; mist and
   gold spores drift; after breath six the frond stays, a dew bead rolls off its tip, a ripple of light runs through the
   moss, the breath ring closes gold, one chime. Growth only rises.
4. **Auto-pace fallback** (the jar breathes, the user follows; it counts) and a hold/toggle setting for motor access.
5. **Garden over days.** One permanent frond per practice day; extra sessions the same day add dew; glow vitality
   floor 0.6; the next ritual restores everything; first flower at 6 fronds; past 12 fronds an inner layer.
6. **Life habits.** Up to 3 from 6 presets (Walk, Water, Read, Stretch, Journal, Early night), each a small glowing
   companion plant in the moss; checked in by Poke or Look + Pinch on its seed label with Today / Yesterday; a 6 s undo.
7. **Pause and resume.** PalmOpen (PC: hold P) or focus loss: the jar holds its breath; nothing is lost; a ritual cut
   off is offered back ("Continue breathing?"), never auto-restarted.
8. **First run** with no menus: the jar arrives, a ghost hand and etched words teach the hold; seed packets only after
   the first answer.
9. **Day-7 look-back**: pinch-hold the cork for 2 s and the week replays in the glass, one frond lighting per kept day;
   a missed day is a held frame.
10. **Settings pebble** (poke): pace (3/5, 4/6, 5/7 s), breaths (3, 4, 6, 8), voice guide, night bed, reduced motion,
    hold or toggle.
11. **Audio**: room tone, glass cues, chime, plucks, optional narration and night bed (AUDIO-BIBLE section 3.1).
12. **Local persistence**: one JSON save with schema version, atomic replace, a rotated backup; no network.

### Cut line (dropped first, in this order, if M5 slips more than 2 days)

1. Day-7 look-back replay -> a still frame where the fronds glow in birth order once.
2. Companion species variety -> one companion sprig shape, three tints.
3. Music bed -> room tone only.
4. Narration -> etched words only.

Never cut: the breath ritual, the answer, growth rules, life check-ins, pause/resume, persistence, first run.

### Out (and why)

Palm-flip menu (weakest gesture, round 1 cut #1), two-hand rotate (cut #2), the "three good things" ritual (post-MVP),
streaks, scores, notifications, accounts, cloud sync, free-text habits (no keyboard in a hands-only evening), health
data (no API, TOOLING.md B [U]), glasses layout (post-MVP, simulator only).

## 3. The customer journey

All times are design targets, measured by the PlayMode playback test `FirstRun_SixBreaths` (gate U1) and by the owner
(U2).

### First launch to the first complete ritual (PC build; Quest notes in brackets)

| t | What the user sees and hears | Implementation note |
|---|---|---|
| 0.0 s | Launch, seated. No splash text, no menu. | Unity splash off; `Main.unity` only |
| 0.0-2.0 s | The room plate fades up from black; a soft room tone rises from where the jar will be. [Quest: passthrough fades in] | `RoomPlate` fade; cue `amb.room` |
| 2.5 s | A thin mint line traces the desk edge nearest the user. [Quest: the scene table is found; fallback "Pinch where your table is"] | `DeskAnchor` trace card |
| 4.0 s | The jar drops the last 8 cm and settles with one glass tink, 40 cm ahead, 30 cm below the eye. | cue `jar.land`; save created only now |
| 6.0 s | The cork breathes once (a small mist puff). One coiled fiddlehead glows faintly on the moss. | cue `jar.lid` |
| 8.0 s | Etched into the glass: "Hold to breathe in" with the provider's binding ("Space or mouse" on PC, "Pinch and hold" on Quest). A ghost hand beside the jar demonstrates. | `IHandIntentSource.BindingHint(PinchHold)`; anchors by id |
| ~10 s | First hold: the ring swells over ~4 s; the fiddlehead uncoils its first sixth while the hold lasts. | `BreathSession.Uncoil` |
| ~14 s | Release: the glass fogs from the bottom up over ~6 s. Etched: "and let go." | `Fog`; cue `breath.exhale.end` at the end |
| ~20 s | Breath 2. The words fade for good; they return only after 8 s idle. The ghost hand leaves after the first counted breath (it advances on the real intent, never on a timer). | tour step ids `tour.hold`, `tour.release` |
| 20-60 s | Breaths 3-5. Six dots on the cork fill one by one (none of them is a score). | `Breaths` |
| ~70 s | Breath 6 ends: the frond stays, a dew bead rolls off its tip, light ripples through the moss, the ring closes gold, one three-partial chime. | `GrowthAnswer.NewFrond`; cue `answer.chime` |
| ~80 s | Quiet for 4 s. Then three seed packets glow at the desk edge; a small shell glows beside the jar (the voice guide offer). | packets are pokeable / pinchable |
| 80-150 s | Poke up to three habits (or none). Each packet plants a companion sprig in the moss with a soft pluck. | cue `habit.plant` |
| <= 180 s | First complete moment done. Nothing else is asked. | gate U1 |

### A normal evening (day 2-6, 2-5 min)

The jar is where it was. Today's fiddlehead waits, coiled and glowing. The user holds, six breaths (with the voice guide
if chosen), the frond stays. Poke "Yesterday" on the Read label for last night's reading. Rest as long as they like; the
spores keep drifting. No notification brought them here (round 1: "the return reason is visual").

### A missed day (came back after a gap)

The jar is dimmer: every frond glows at the vitality value (one missed day 0.85, two 0.70, floor 0.60) and leans at most
6 degrees; the waiting fiddlehead is coiled and quiet. No text, no sound about it, no count of days away. One ritual and
the light runs back through the moss to full (`GrowthAnswer.Recovered`); every frond that ever grew is still there. A
companion habit missed shows nothing at all; it only did not add a leaf.

### Day 7

Six or seven fronds of slightly different sizes, the newest brightest; the first flower opened at the sixth frond (so
a week with one miss still flowers); companion sprigs with their leaves. Pinch-hold the cork for 2 s: the week replays in
the glass, each kept day's frond lighting in order; a missed day is a held frame, never a red gap.

## 4. Hand vocabulary and the PC input mapping

The mapping lives in `com.gardenvr.input` (owner: terrarium; built in T-TER-001). App code reacts only to intents.

| Intent | Meaning in Terrarium | Hand on Quest (Phase 2) | PC binding | Why the feel survives |
|---|---|---|---|---|
| `PinchHold` | inhale (while held); pinch-hold the cork 2 s = look-back | thumb-index pinch held | **hold Space** or **hold left mouse** | A hold stays a hold: duration, not a click, carries the breath. The provider ramps strength 0 -> 1 over 120 ms on press and back on release, so the core `PinchDetector` hysteresis (engage 0.8, release 0.5) runs on the same kind of signal a hand gives |
| `Release` | exhale | open the pinch | release Space / mouse | Released means released; no auto-release |
| `Pinch` | select (a seed label, a packet, the shell, "Continue breathing?") | quick pinch at the looked-at target | **left click** (press < 300 ms) at the cursor, or **Enter** on the focused target | Same select-at-target semantics as look-and-pinch on Quest |
| `Look` | which label or object is targeted (soft look ring) | head/hand ray (Quest 3 has no eye tracking), eye gaze on Pro or glasses | **mouse cursor ray**, ring after 150 ms dwell; **Tab / Shift+Tab** cycles targets for keyboard-only use | The cursor is the gaze point; hover never acts on its own |
| `Poke` | check in Today / Yesterday, the settings pebble | index fingertip touch on a flat target >= 3 cm | **F** at the cursor, or a click on a poke-only target | A distinct key keeps poke and pinch separable in tests |
| `PalmOpen` | pause: the jar holds its breath | open palm toward the jar for 0.6 s | **hold P for 0.6 s** (or hold middle mouse) | The same 0.6 s commitment as the palm gesture |
| head pose | look around while seated | HMD | **right-drag**: yaw +/- 40 deg, pitch +/- 25 deg, no translation; **R** recentres | Seated: rotation only |
| system pause | app loses focus | headset off / system menu | **Esc** or window focus loss | Lifecycle, not an intent; same code path as Quest focus loss |
| accessibility | hold or toggle | - | setting `holdMode = toggle`: Space toggles inhale / exhale | Registry accessibility: no operation reachable by one input only |
| dev only | time travel, overlay | - | development builds only: **F1** state overlay, **]** next day, **[** previous day (refused by the core if it rewrites history), **F2** auto-pace | via `DevCommands` in the provider, stripped from release builds |

Scripted playback (`ScriptedIntentSource`, JSONL; T-TER-001) drives the same intents by **target id**, never by pixel:
`{"t":10.0,"intent":"PinchHold","target":"jar","dur":4.2}` then `{"t":14.2,"intent":"Release"}`. Every pokeable or
lookable object carries an `IntentTarget` with a stable id (`jar`, `jar.cork`, `seed.walk`, `label.walk.today`,
`pebble.settings`, `shell.voice`, `prompt.continue`).

## 5. Growth and record rules (code-like data models)

Rules live in `com.gardenvr.core` (pure C#, tested by `dotnet test shared/core-dotnet`). App code renders state; it
never decides it. Terrarium-owned paths: `Runtime/Breath/` (BreathSession, PinchDetector, SimulatedHand) and
`Runtime/Terrarium/` (Garden and companions). Common types (`Runtime/Common/`) are owned by the sundial agent
(T-SUN-002) and consumed here.

```csharp
// Runtime/Breath/BreathSession.cs - seeded from the spike, unchanged in behaviour
public sealed class BreathConfig {
    public int   TargetBreaths      = 6;    // setting: 3, 4, 6, 8
    public float MinInhaleSeconds   = 1.2f; // shorter holds are fidgets: ignored, never penalised
    public float IdealInhaleSeconds = 4.0f; // this breath's share of uncoil completes here (setting: 3 / 4 / 5)
    public float MinExhaleSeconds   = 1.0f; // the breath counts once the release has lasted this long
    public float FogDecaySeconds    = 2.5f;
}
public enum BreathPhase { Waiting, Inhaling, Exhaling, Paused, Complete }
// Update(dt, PinchSample) -> Phase, Breaths, Uncoil (0..1, never falls below earned), Fog (0..1), Events

// Runtime/Terrarium/Garden.cs - seeded from the spike; D5 changes the flower rule
public sealed class Garden {
    public const float VitalityFloor = 0.6f;     // glow floor: never dark, never dead
    public const int   FirstFlowerAt = 6;        // CHANGED (was every 7th): one miss in a week still flowers on day 7
    public const int   FlowerEvery   = 6;
    public const int   FrondsBeforeInnerLayer = 12;
    public IReadOnlyList<int> FrondDays { get; } // one entry per practice day, append-only
    public int  Fronds => FrondDays.Count;       // monotone: nothing removes a frond
    public int  Flowers => Fronds < FirstFlowerAt ? 0 : 1 + (Fronds - FirstFlowerAt) / FlowerEvery;
    public int  DewToday { get; }                // extra rituals the same day add dew, never fronds
    public int? LastRitualDay { get; }
    public int  Returns { get; }                 // comebacks after a gap; never shown as a number
    public float Vitality(int today);            // gap <= 1 -> 1.0; else max(0.6, 1 - 0.15 * (gap - 1))
    public GrowthAnswer CompleteRitual(int today); // throws if today < LastRitualDay (clock set back)
}
public readonly struct GrowthAnswer { bool NewFrond; bool Recovered; bool Flower; int DewBeads; }

// Runtime/Terrarium/Companions.cs - life habits as companion plants (new, T-TER-009)
public enum CompanionSpecies { GlowSprig, MoonMoss, StarFern }      // art: 1 mesh, 3 tints at the cut line
public static class Companions {
    public const int MaxHabits = 3;
    // Leaves = days with a live (not undone) TendEvent in the Common ledger: monotone except a same-day undo
    public static int  Leaves(Ledger l, string habitId);
    public static float Vitality(Ledger l, string habitId, GardenDay today); // same curve and floor as Garden
}

// Runtime/Terrarium/TerrariumSave.cs - the save document (schemaVersion, append-only migrations: T-TER-008)
public sealed class TerrariumSave {
    public int SchemaVersion = 1;
    public int[] FrondDays; public int DewToday; public int? LastRitualDay; public int Returns; public int RitualsCompleted;
    public List<HabitDef> Habits;          // Common: Id, Kind, PresetKey, Species, Slot, CreatedDay, ArchivedDay
    public List<TendEvent> Tends;          // Common: append-only; undo marks, never deletes
    public RitualSettings Settings;        // only values that differ from defaults are written
    public string FirstRunStep;            // tour progress by step id ("tour.hold", ...), resumable
}
public sealed class RitualSettings {
    public int Breaths = 6; public float InhaleSec = 4f, ExhaleSec = 6f;
    public bool AutoPace, VoiceGuide, NightBed, ReducedMotion; public HoldMode HoldMode = HoldMode.Hold;
}
```

Invariants with a test each (existing spike tests carry over; new ones marked +):

- fronds never decrease over 1,000 random lives (property; + negative control that fails when a removal is injected);
- one frond per day; extra rituals add dew; missed days lower vitality to the floor; one ritual restores;
- + first flower at 6, next at 12; a 7-day week with one miss flowers;
- clock going backwards is refused; persistence round-trips exactly; + a newer-schema save is opened read-only;
- + a truncated or corrupt save never starts the first run: the backup is offered instead (registry `failure-states`);
- + companion leaves rise by one per kept day, yesterday check-in dates to yesterday and only until the 03:00 boundary,
  undo inside 6 s restores the exact prior value, and the undo window flushes on pause/teardown.

## 6. Art pipeline to the reference frames

Target and tolerances: `docs/art/terrarium-style.md`. Starting point: round 3 "about halfway" (owner 5/10, Gemini 4/10)
with these named gaps (round-3 gap ledger): moss reads as a textured dome; frond lace too regular; steam faint and blocky;
cork, droplets coarse; reference pixels cropped into moss, soil and cork bands (art debt, gate A4).

| Element | Blender (scripted, `tools/blender/terrarium_*.py`, Blender 4.2 `-b -P`) | image_gen / image_edit (kept in `apps/terrarium/Art/Source/` + `.prompt.txt`) | Shader / Unity |
|---|---|---|---|
| Jar + glass | Lathe from `blender_heroes.py` (rounded shoulder, lip, 2 mm wall), proportions tuned to ref-1 (height : width 1.35) | droplet / condensation map (macro water beads on glass, black background) | `Glass` 2-pass: back wall first, fresnel rim `#6FB7C9`, mint inner scatter, droplet map, breath fog driven by `Fog`; no grab pass |
| Cork | tapered cylinder, bevel, slight dome | macro cork texture, tileable (edge-diffed) | lit by the jar's self-light gradient only |
| Moss mound | sculpted dome by displacement + clumped silhouette; a skirt ring of 24-40 moss cards at the silhouette (the mobile technique the round-3 ledger names) | moss tuft atlas (4x4, top-down tufts on black, alpha from luminance), moss albedo for the dome | `Glow` triplanar with tip brightening, alpha-to-coverage on cards (4x MSAA) |
| Soil band | lathe band inside the glass base | dark loam texture, tileable | unlit, very dark `#0D231D` |
| Fronds | V-folded arched cards (existing), 3 variants, instanced per frond day | bipinnate fern frond atlas, 3 variants, irregular lace, front-lit on black | `Glow`: emissive mint `#8FF0C8` edge glow from a distance field; vitality scales emission |
| Fiddlehead | swept tube along a log spiral, 5 authored uncoil states (existing), add fine hairs as a fin card | - | blended on the CPU by `Uncoil`; coil halo card |
| Breath ring | analytic quad (no texture): core line + soft pool + progress arc | - | additive; gold `#F2D27A` on completion |
| Mist / steam | 5 loop cards (existing) | 8x8 smoke flipbook composed from generated wisps | `Card` premultiplied, flipbook UV; one draw |
| Spores | - | 64 px spore sprite | one particle system, additive, one draw |
| Companions | 3 low-poly sprig cards | sprig drawings (3), glowing tips | `Glow`, tinted per species |
| First flower | small bell bloom, 6 petals | petal texture | `Glow`, amber-gold |
| Halos | - | - | halo cards at coil, jar, flower: no post-processing anywhere |

The side-by-side check (every art task, gate A1-A3): capture at the reference framing (style bible, "Reference frames
and framing"), compose `reference | render` with `GardenVR.Capture` (T-SUN-001), crop 3 same-pixel regions (glass rim,
fronds, moss), write findings as located corrections ("moss silhouette 30% too smooth on the left third"), keep the best
attempt, never just the last. Budgets are handed to the art task up front: scene hard caps 40 draws / 60k tris / mean
transparent layers <= 1.5 over the jar; soft caps 32 / 48k / 1.2.

Gate frames: **G1** ref-1 mid-breath; **G2** ref-2 (centred jar, gold ring: "the answer" composition); **G3** the sixth
breath (F1 prompt, generated by T-TER-006 step 1, owner-approved before it becomes an anchor); **G4** day 7 (F2 prompt,
same rule). Until the owner approves G3/G4 they are provisional and do not count for A1.

## 7. Audio plan

Full cue list, voice direction, loudness and credits: `docs/audio/AUDIO-BIBLE.md` sections 3.1, 4, 5, 7. Summary:

- **SFX (about 14 cues, 2-3 variants each)**: room tone loop, jar land tink, cork breath puff, exhale glass resonance
  (one quiet partial at the end of each exhale), fog hiss (barely audible), answer chime (three glass partials, 2.5 s
  decay, the only reward sound), dew drop, moss ripple shimmer, seed packet rustle, check-in pluck (3 pitches rising with
  plant size), undo (soft reverse pluck), pause (held hum), look-back shimmer, pebble tap. Missed day: nothing, ever.
- **Narration** (optional voice guide, 14 lines, ids `vo.ter.*`): baked clips, never timed by script length; each line
  fits its breath window measured from the take; the voice never paces faster than the user's hold (it waits for the
  intent). Outline:
  - `vo.ter.open.1` "Let's take six slow breaths together." / `.2` "Settle in. The jar will follow you."
  - `vo.ter.in.1..3` "Breathe in." / "And in, slowly." / "Fill up gently." (played on PinchHold start, never before)
  - `vo.ter.out.1..3` "And let go." / "Let it out, softly." / "All the way out." (on Release)
  - `vo.ter.mid.1` (after breath 3) "Halfway. The fern is listening." 
  - `vo.ter.close.1` (on the answer) "Six breaths. This frond stays." / `.2` "Rest here as long as you like."
  - `vo.ter.return.1` (first ritual after a gap) "Welcome back. Everything you grew is still here."
  No medical or therapeutic words (`AGENTS.md` rule 6); no em dash in any line (rule 7).
- **Music**: one unpulsed night bed (90 s seamless loop, low pads + distant glass), off by default, -30 LUFS, ducks
  under narration by 6 dB.
- **Credits** (pot 18,000; tool estimates): SFX ~14 cues x 2.5 variants x ~140 = ~5,000; narration ~600 characters x
  3 takes + 2 retakes of long lines = ~3,500; night bed 90 s x 60 = 5,400 + one retry of 60 s = 3,600 (cap 6,500 by
  trimming the retry); slack 3,000.

## 8. Milestones and acceptance

Commands run from the agent's worktree in Git Bash. `U="C:/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Unity.exe"`.
`R=orchestration/runs/terrarium/<task-id>`. "Owner" rows need the owner's eyes; everything else an agent can verify and
the host re-runs.

| Milestone | Window | Tasks | Acceptance (agent-verifiable) | Owner's eyes |
|---|---|---|---|---|
| **M0 Foundations** | 10-03 to 10-05 | T-TER-001 input, 002 rig + plate, 003 core restructure | `dotnet test shared/core-dotnet` all green with >= 20 tests; `"$U" -batchmode -nographics -quit -projectPath apps/terrarium -logFile $R/compile.log` exit 0 and `grep -c "error CS" $R/compile.log` = 0; input EditMode tests pass (results XML total > 0, failed 0); `Main.unity` capture at the jar framing shows the room plate | - |
| **M1 Vertical slice** | 10-05 to 10-08 | 004 hero port, 005 ritual wired | PlayMode test `Ritual_SixBreaths_ByPlayback` passes: 6 breaths counted, `Fronds == 1`, `Phase == Complete`, in <= 90 s app time at a fixed 1/60 step; capture sequence `ritual/f####.png` -> `ritual.mp4`; S1 grep clean | **R1 Fri 10-09**: plays the Windows build for 5 min, rates the hold-to-breathe feel |
| **M2 Art pass** | 10-08 to 10-13 | 006 moss + fronds (+ G3/G4 candidates), 007 glass, steam, cork, spores | side-by-sides for G1, G2 (and provisional G3, G4) in `$R/sbs/`; `Measure` within soft caps; provenance census: 0 textures from reference pixels; judge record (host) | **R2 Fri 10-16**: rates G1-G4 1-5; picks G3/G4 anchors |
| **M3 Garden over days** | 10-12 to 10-15 | 008 persistence + days, 009 life habits | PlayMode `Days_MissedThenRecovered` and `Days_WeekFlowers` pass; captures `day1`, `missed2`, `recovered`, `day7`; core property tests green | (R2) the missed-day frame reads "resting, not dying" |
| **M4 Audio** | 10-14 to 10-17 | 010 cue service + SFX + mixdown, 011 narration + bed | every cue id in AUDIO-BIBLE 3.1 resolves to a clip (EditMode manifest test); `node tools/audio/mixdown.mjs --log $R/ritual-cues.jsonl --out $R/mix.wav` then `ffmpeg -i $R/mix.wav -af ebur128 -f null -` reports -20 +/- 1 LUFS, TP <= -1.5; credits spent within pot (ledger) | (R2) listens to the full-ritual mixdown |
| **M5 Journey** | 10-16 to 10-20 | 012 first run; then (queued later) look-back, settings, pause/resume polish | PlayMode `FirstRun_SixBreaths` <= 180 s to the answer from a fresh save path; `PauseAt_*` (5 points) pass; a load failure never replays the first run (test); 90 s first-run capture `first-run.mp4` | Owner runs a fresh-profile first run (U2) at G |
| **M6 Polish + gate pack** | 10-20 to 10-22 | queued later: reduced motion, budgets, string census, Windows player build, gate pack | `GATE.md` lists every U/S/A item with status and evidence; Windows player zip; budgets; S2 10/10 playback runs | **G Fri 10-23** |

## 9. Post-MVP backlog (more habits, more in-app rituals)

Ordered by value to the daily ritual; each is one or two tasks once the gate is passed.

| Item | What | Hand gesture | Notes |
|---|---|---|---|
| Three good things | Pinch three dew drops onto three leaves, one per thing you are glad of; nothing typed or stored but "done" | Pinch x3 | round-1 cut #3 (sundew species) |
| Box breathing | 4-4-4-4 pattern as a second pace preset | PinchHold with a held-full beat | same BreathSession, a hold-at-top phase |
| Stretch with the jar | 3 slow seated reaches; the fern sways after your hand | Look + PalmOpen hold at three points | seated, two-foot radius |
| Mindful sip | Water habit done in-app: lift the real glass, the jar's mist rises | Pinch to start, Pinch to end (30 s) | life habit made ritual |
| Evening "one word" | Pick one of 6 word stones for the day (no typing) | Pinch | mood without a mood tracker |
| Look-back timelapse | The full replay if cut | PinchHold cork | cut-line item 1 |
| Seasons | The jar's light warms over weeks (lifetime fronds) | - | growth only rises |
| More companions | 6 species, 2 shapes each | - | art time |
| Glasses form | Smaller jar, ring on the cork, breath only | Look + Pinch | Meta VR Glasses simulator profile only (TOOLING.md B) |
| Export | Local JSON export after a confirm | Poke | no network |
| Localisation | Czech, German, Japanese strings and narration | - | registry `localization` bundle; narration re-render cost in credits |

## 10. Quest-readiness notes (what stays an abstraction)

| Seam | PC (now) | Quest (Phase 2) | Rule now |
|---|---|---|---|
| Hand intents | `KeyboardMouseIntentSource` | `XrHandsIntentSource`: pinch strength from the Meta Interaction SDK / OpenXR hand tracking feeds `PinchSample` unchanged; Poke from the index tip collider; PalmOpen from a palm-toward-head pose held 0.6 s | App code only sees `IHandIntentSource`; S1 grep enforces it |
| Head | right-drag | HMD pose | `IHeadPoseSource` |
| Room | `RoomPlate` (image behind the scene) | passthrough underlay (`OVRPassthroughLayer`), alpha-0 clear | `IRoomProvider`: `Show()`, `Dim(float)` |
| Desk and placement | fixed `DeskAnchor` | MRUK scene table + spatial anchor; place-by-pinch fallback | `IPlacementProvider`; the save never depends on the anchor (it stores where, never what grew) |
| Lifecycle | Esc / focus loss | headset off, system menu, `OnApplicationPause` | one merged pause signal; the undo window flushes on pause |
| Testing | `ScriptedIntentSource` playback in PlayMode | Meta XR Simulator + Operator MCP (round 2 proved 6/6 breaths, `terrarium_get_state` tool) | keep a state oracle (`TerrariumState.ToJson()`) the Operator tool can return |
| Manifest | - | hand tracking permission and feature, passthrough (round 2 limitation 12; round 3 fixed it) | Phase 2 task checks `aapt dump badging` |
| Rendering | URP, no post, halo cards, 4x MSAA, ASTC 6x6 caps | same + multiview, fixed foveation, possibly SpaceWarp (needs motion vectors in custom shaders) | every custom shader carries `UNITY_VERTEX_OUTPUT_STEREO` and instancing (round 3 section 06) |
| Windows workarounds | - | the 12 in round 2 section 04 (MSI extract, embedded packages, Vulkan dzn filter, single Operator layer, JDK 17) | reuse `RUNBOOK.md`; do not rediscover |

Platform limits accepted now: the glass cannot refract the real room; real hands cannot be lit by the jar (a mint rim on
the tracked hand mesh is a Phase 2 experiment).
