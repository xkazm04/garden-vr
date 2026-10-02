# Sundial - plan (MVP = the competition scope)

App: `apps/sundial` (Unity 6.6 URP). Agent: Grok "sundial" on `agent/sundial`. Art direction: **A/2-05 Field Notebook,
pushed to hand-drawn / anime linework living in the real room** - mix hand-drawn art and reality. Programme, gates and
dates: `docs/PLAN.md`. Style bible: `docs/art/sundial-style.md`. Audio: `docs/audio/AUDIO-BIBLE.md`.

Sources (relative to `C:\Users\kazda\kiro\personas\.contest\`):

- Round 1 design: `arena/habit-garden/entries/claude-claude-opus-5-5_high/variant-2/index.html` (+ `NOTES.md`): the bet,
  scope, first-minute timeline (FIG 3.1), normal day (FIG 3.2), rolling-window rules, gestures, data model
  (`core/model.ts`, `core/rules.ts`), arcs table, motion table, audio.
- Round 2 spike (IWSDK): `arena/habit-garden-r2/entries/claude-claude-opus-5-5_high-v2/variant-2/` and
  `.../spike/src/core/{model,rules,ledger,days,breath}.ts`: the TypeScript core this plan ports to C#; 24/24 IWER checks.
- Round 3 fidelity gate: the Unity seat rendered the drawn dial at the owner's framing
  (`arena/habit-garden-r2-r3/entries/claude-claude-opus-5-5_high-v1/variant-1/index.html` section 02: toon ramp, ink
  hull, boil at 10 fps, pinch halo, shadow catcher; 15 draws, 10.4k tris) and the IWSDK seat built the SVG dial-face
  pipeline (`.../claude-claude-opus-5-5_high-v2/variant-2/index.html` "Asset pipeline"; script now
  `tools/blender/from-iwsdk/dial_svg.mjs`; outputs now `shared/assets/seed-textures/sundial-drawn/`).
- Art target: `staging/habit-garden/reference/A2-05-field-notebook-{1,2}.png` (now `shared/assets/art-reference/`) and
  the anime push prompts A/2-05-F1..F3 in `staging/habit-garden/OWNER-CHOICE.md` (never generated: `recent.json` shows no
  images for them).

## 1. What changes from the contest reports (Sundial was designed for IWSDK)

| Topic | Round 1 / 2 (IWSDK) | This plan (Unity) | Why |
|---|---|---|---|
| Engine and ship form | IWSDK + IWER, hosted URL | Unity 6.6 URP, APK on the Competition channel | Decision 0001 |
| Core | `core/*.ts`, Vitest | C# in `com.gardenvr.core/Runtime/{Common,Sundial}`, `dotnet test` | Rules in the shared core (`AGENTS.md` rule 4) |
| Persistence | IndexedDB, eviction risk R5 | one JSON save in the app's private storage, atomic replace + backup | No browser storage on a native build |
| Device first | Meta VR Glasses first, Quest certified | **Quest first** (Phase 2); glasses = post-MVP simulator profile | Glasses ship spring 2027 (TOOLING.md B); Unity APK targets Quest |
| Look | eye gaze (glasses), head-gaze reticle fallback | `Look` intent: **mouse hover on PC**; on Quest 3 a hand ray or head reticle (no eye tracking), eye gaze on Pro | Intents abstraction (decision 0002) |
| Placement | summoned with palm-up, never anchored (WebXR anchors [U]) | **on the desk**, anchored like the jar in Phase 2; PalmOpen dismisses / brings back | Unity has spatial anchors [V]; the reference frame shows it on the table |
| Tests | Playwright + IWER, action capture | PlayMode playback of intents (`ScriptedIntentSource`), batchmode captures; XR Simulator + Operator in Phase 2 | Same seam: tests inject intents, not pixels |
| Art | Daylight instrument (terracotta) in round 1; Field Notebook chosen in round 2 | Field Notebook pushed to anime linework: clean confident ink contours, flat cel with one shade step, watercolour only inside the dial, the room stays photographic | Owner: "its potential to combine hand-drawn/anime style into reality" |
| Growth | size = days kept in the last 7 (can fall as days age out) | **record = 7 tiles; plant size = lifetime kept days (only rises); bloom = the week** (PLAN D2) | `AGENTS.md` rule 3: growth only rises |
| Habits per arc | 1-3 | **1 per arc** in MVP (PLAN D7) | Draw budget and reference composition |
| Arcs | round 1: 06-11, 11-18, 18-24; spike moved dusk to 17:00 | 06:00-11:00, 11:00-18:00, 18:00-03:00 (PLAN D4) | Wind-down runs to the day boundary |

## 2. Feature scope and the MVP cut line

### In (MVP, the competition scope)

1. **The dial on the desk.** A plate-sized (30 cm) hand-drawn sundial bed, ~55 cm ahead, tilted 12 degrees toward the
   user; three watercolour arcs (morning ochre, midday coral, dusk lilac); a fountain-pen gnomon whose painted shadow
   follows the real clock at 15 degrees per hour.
2. **Read in two seconds.** Where the shadow lies says where you are in the day; the plant due now has a soft waiting
   glow; each plant's 7 tiles say how the week went (kept, late, missed, today).
3. **Glance and pinch to tend** (in twenty seconds). Look at a plant (PC: hover) -> an inked halo; Pinch -> a wooden
   tock, a bloom pulse, the tile fills with ink; a 6 s undo at the tile.
4. **One habit per arc** from presets with a cue chip ("After I wake, I drink a glass of water."): no free text.
5. **The three-breath dusk ritual** (the in-app habit; default wind-down habit): PinchHold on the dusk plant = in,
   Release = out, three times, self-paced; the plant answers and one chime closes the day (a close, not a reward).
6. **Honest record**: a missed day is a pale tile; yesterday can be logged once, during today, drawn hatched forever;
   nothing resets; the plant never shrinks.
7. **First run**: the dial appears, the shadow sweeps 06:00 -> now, three seed packets rise; first tend in <= 60 s.
8. **Pause / dismiss**: PalmOpen (PC: hold P) puts the dial away and brings it back; focus loss pauses; nothing lost.
9. **Boiling line** at 10 fps on plants and rim (a setting turns it off; reduced motion turns it off).
10. **Settings** (poke the corner tab): sound, voice guide, ambient beds, reduced motion, boil on/off, day boundary.
11. **Audio**: tock, page and pencil sounds, dusk chime, optional voice for the three breaths, three short arc beds.
12. **Local persistence**: append-only tend events, one JSON save, schema version, atomic replace, rotated backup.

### Cut line (dropped first, in this order)

1. Arc ambient beds -> one shared room tone.
2. Boiling line on the rim -> plants only.
3. Hand-lettered arc labels -> none (ref-1 has none).
4. Voice for the dusk ritual -> ink words on the page only.

Never cut: the shadow clock, look-and-pinch tending, tiles, backfill, undo, dusk ritual, first run, persistence.

### Out (and why)

2-3 habits per arc (post-MVP, draw budget), rim scrub through time (round-1 "should"), stretch-reach ritual
(post-MVP), week-view dial, palm-up summon as the only way in (it is now an extra, the dial lives on the desk),
notifications (the return comes from the dial; TOOLING.md B notification limits), streaks, accounts, sync, voice input,
custom species.

## 3. The customer journey

Design targets; U1 is measured by PlayMode `FirstRun_FirstTendAndDusk`, U2 by the owner.

### First launch to the first complete moment (adapted from round-1 FIG 3.1)

| t | What the user sees and hears | Implementation note |
|---|---|---|
| 0 s | Launch, seated. No splash, no menu. The kitchen plate fades in (Quest: passthrough). | `RoomPlate` |
| 3 s | The dial draws itself onto the desk: pencil construction lines first, then ink, then washes bleed into the arcs (1.2 s). A soft page sound. | cue `dial.appear`; reduced motion: appears at once |
| 8 s | The shadow sweeps from 06:00 to now in 1.5 s and settles. Ink words on the page, in the dial's hand: "This is your day." | gnomon decal angle from `SundialRules.GnomonAngleDeg(nowMin)` |
| 14 s | Three seed packets rise at the dial's near edge, one per arc. | `seed.morning`, `seed.midday`, `seed.winddown` |
| 21 s | Look at a packet (halo), pinch: it opens into three preset habits with their cue chips; pinch one. | wizard steps with ids, resumable |
| 40 s | The three seeds drop into their arcs; the arc under the shadow glows softly. | |
| **52 s** | **First tend**: look at the due plant, pinch: a tock, a bloom pulse, the first leaf, today's tile fills with ink; the undo mark sits at the tile for 6 s. | gate: first tend <= 60 s |
| 70 s | Ink words beside the dusk plant: "Try three breaths?" (on day 1 the ritual is allowed at any hour; the arc is a cue, never a gate). | |
| 95 s | Three breaths: hold = in (an ink circle swells around the plant), release = out; self-paced. | `BreathSession` with `TargetBreaths=3`, `MinInhale=1.5s` |
| ~165 s | The dusk plant answers: its first leaf opens, a hand-drawn sparkle ring, one chime. Done. | gate: <= 180 s |

### A normal day (round-1 FIG 3.2, three micro-moments, ~2.5 min in total)

- **07:10 morning, 45 s**: the shadow is in the ochre arc; the Water plant has its waiting glow; look, pinch, tock.
- **12:30 midday, 20 s**: a glance reads "morning kept, midday waiting"; pinch Top-3 plan. (The commute glance on Meta
  VR Glasses is post-MVP and simulator-only.)
- **22:10 wind-down, 90 s**: three breaths at the dusk plant; the dial now shows the whole day.
No notification is required: the shadow has moved every time the user looks.

### A missed day

Yesterday's tile is pale (missed). Nothing else changed: the plant has every leaf it ever grew (D2). If the week now has
fewer than five kept days, its flower closes back into a bud, the way a flower closes at evening; nothing wilts. A tiny
ink "?" sits on yesterday's pale tile until the day boundary: pinch it -> "Did it happen?" -> "Yes, it happened" draws the
tile hatched (late, forever, with its real timestamp). It can be logged once and never claims to be on time.

### Day 7 at 21:30

Plants kept every day are in full bloom; others are smaller in the week (buds), never dead. Each tile row reads as a week:
oldest first, today's tile last. The dial at a glance is the week.

## 4. Hand vocabulary and the PC input mapping

The bindings are the shared provider's (`docs/plans/terrarium.md` section 4, owner: terrarium). What Sundial uses:

| Intent | Meaning in Sundial | Quest (Phase 2) | PC binding | Feel preserved because |
|---|---|---|---|---|
| `Look` | which plant / tile / packet is targeted: an inked halo after 150 ms dwell; never acts alone | head or hand ray (Quest 3), eye gaze (Pro, glasses sim) | **mouse hover** (cursor hidden over the dial; the halo is the cursor); **Tab / arrow keys** step between plants | Glance-to-target stays separate from select, like "look to target, tap to select" (TOOLING.md B [V]) |
| `Pinch` | tend the looked-at plant; open a packet; confirm "Yes, it happened"; undo mark | quick pinch | **left click**, or **Enter / Space tap** on the focused plant | One gesture, one tend; undo within 6 s insures false pinches |
| `PinchHold` / `Release` | the dusk ritual's in / out breath | pinch held | **hold Space** or **hold left mouse** on the dusk plant | Duration carries the breath, as in Terrarium |
| `Poke` | tiles' "?" and the settings tab (Quest: near poke, the dial is within reach) | index fingertip | **F** at the cursor | Distinct from pinch for tests |
| `PalmOpen` | put the dial away / bring it back (pause) | palm toward the head, 0.6 s, not during a pinch | **hold P 0.6 s** | Same commitment time as the palm pose (round-1 gesture card: 0.6 s) |
| head pose | look around while seated | HMD | right-drag, R recentres | Seated rotation only |
| dev only | clock and day travel | - | **F1** overlay, **]** / **[** day, **T** clock x60, **Shift+T** set clock | development builds only |

Playback targets (stable ids): `dial`, `plant.morning`, `plant.midday`, `plant.winddown`, `tile.<arc>.<0..6>`,
`tile.<arc>.yesterday.ask`, `seed.<arc>`, `seed.<arc>.<preset>`, `undo.<arc>`, `tab.settings`, `prompt.breaths`.

## 5. Growth and record rules (code-like data models)

Common types are owned by the sundial agent (`Runtime/Common/`, T-SUN-002) and consumed by Terrarium; the Sundial rules
are `Runtime/Sundial/` (T-SUN-003). Pure C#, no UnityEngine, no packages (a minimal JSON reader/writer lives in
`Common/Json`).

```csharp
// Runtime/Common - shared by both apps
public readonly struct GardenDay : IComparable<GardenDay> {
    public readonly int Index;                                  // days since 2000-01-01 of the garden day
    public static GardenDay From(DateTimeOffset local, TimeSpan boundary); // default boundary 03:00 (PLAN D3)
    public DateTimeOffset EndsAt(TimeZoneInfo tz, TimeSpan boundary);      // DST-aware
}
public interface IClock { DateTimeOffset Now { get; } TimeZoneInfo Zone { get; } }      // injected; tests fake it
public enum HabitKind { InAppRitual, LifeCheckIn }
public sealed class HabitDef { public string Id, PresetKey, Group; public HabitKind Kind; public int Slot, CreatedDay; public int? ArchivedDay; }
public enum TendSource { Pinch, Poke, Ritual, Backfill }
public sealed class TendEvent {                                  // append-only; undo marks, never deletes
    public string Id, HabitId, Tz; public int Day; public long AtUtcMs; public TendSource Source;
    public bool Late;                                            // true only for Backfill; drawn hatched forever
    public long? UndoneAtUtcMs;
}
public sealed class Ledger {
    public TendResult Tend(string habitId, GardenDay day, TendSource src, IClock clock); // idempotent per habit-day
    public bool Undo(string tendId, IClock clock, double windowSeconds = 6);           // only inside the window
    public bool IsKept(string habitId, int day); public int KeptDays(string habitId);   // live events only
}
public enum LoadOutcome { Fresh, Loaded, Migrated, Failed }      // Failed never routes to first run
public interface ISaveStore<T> { LoadResult<T> Load(); void Save(T doc); }   // tmp + replace, rotate .prev, refuse newer schema

// Runtime/Sundial - the dial's rules
public enum ArcId { Morning, Midday, WindDown }
public sealed class ArcDef { public ArcId Id; public int StartMin, EndMin; }   // 360-660, 660-1080, 1080-1620 (03:00 next day)
public enum TileState { Kept, Late, Missed, Today, Before }        // closed vocabulary; labels from a catalog, never raw
public enum Stage { Seed, Sprout, Young, Leafy, Full }            // from LifetimeKept: 0, 1-2, 3-6, 7-13, 14+ (only rises)
public enum Bloom { None, Bud, Open }                             // from WindowKept: <3, 3-4, >=5 (PLAN D2)
public sealed class PlantState {
    public string HabitId; public TileState[] Window;              // length 7, oldest first, today last
    public int WindowKept, LifetimeKept; public Stage Stage; public Bloom Bloom;
    public bool DueNow;                                            // clock inside this arc and today not yet kept
    public bool CanBackfillYesterday;                              // yesterday missed, not yet logged, before today's boundary
}
public static class SundialRules {
    public static PlantState Plant(HabitDef h, Ledger l, GardenDay today, int nowMin);
    public static ArcId? ArcAt(int nowMin);
    public static float GnomonAngleDeg(int nowMin);                // 15 deg per hour, 0 at 06:00
    public static TendResult Backfill(HabitDef h, Ledger l, GardenDay today, IClock c); // once, Late = true
}
public sealed class SundialSave { public int SchemaVersion = 1; public List<HabitDef> Habits; public List<TendEvent> Tends;
    public SundialSettings Settings; public string FirstRunStep; }
public sealed class SundialSettings { public int BoundaryMin = 180; public bool Voice, Beds, ReducedMotion; public bool Boil = true; }
```

Invariants, each a test (ported from the round-1 rules and the spike's Vitest suite, plus D2):

- `LifetimeKept` and `Stage` never decrease over 1,000 random lives (property) - with a negative control;
- a miss subtracts nothing; a tended day ageing out of the window lowers `WindowKept` and may close a bloom, never `Stage`;
- backfill: only yesterday, only once, only before today's boundary, `Late` forever, real timestamp;
- undo: only inside 6 s; marks, never deletes; flushes on pause / teardown (deferred write);
- the day ends at 03:00 local; a 01:30 tend counts for the previous calendar day; DST spring and autumn weeks; a time
  zone change mid-week; a clock set back never creates a future tile;
- `DueNow` only inside the habit's arc and only while today is not kept;
- a corrupt or truncated save opens the backup offer, never the first run.

## 6. Art pipeline to the reference frames

Target and tolerances: `docs/art/sundial-style.md`. Starting point: round 3 Unity "closer" (owner 6/10, Gemini 4/10):
paper, washes, ink outline, pencil ticks, toon shadow, boil and halo work; gaps: the plants are the owner's drawings
keyed from the frame (art debt), soil spill and raised tiles plainer, the gnomon shadow thin and hard, less pencil
construction and looser line weight in the reference.

| Element | Blender (`tools/blender/sundial_*.py`) | SVG / image_gen (kept in `apps/sundial/Art/Source/`) | Shader / Unity |
|---|---|---|---|
| Dial body | disc + bevelled rim + paper-stack edge (from `blender_heroes.py` `drawn_dial`) | paper-stack edge texture | `Toon` 2-step ramp + paper grain; inverted-hull ink outline (outline normals smoothed into a vertex colour channel) |
| Dial face | - | **SVG pipeline** (port `tools/blender/from-iwsdk/dial_svg.mjs`): 2048 px face with paper fibre, pencil ruler, construction lines, 120 ticks, wobbling ink circles, three turbulence-displaced watercolour arcs with edge pooling and granulation, soil wash | unlit painted albedo, ASTC 6x6 at 1024 for Quest |
| Soil mound | low sculpted mound inside the bed, stippled | soil spill texture (ink stipple + wash) | `Toon` |
| Gnomon | lathe nib + gold collar (existing) | lacquer strip | `Toon` with one specular step |
| Gnomon shadow | - | painted soft wash shadow decal | rotated by `GnomonAngleDeg`; replaces the realtime shadow pass (saves 4 shadow casters) |
| Plants | slightly curved card quads | **image_gen**: 3 species x 5 stages (Stage) + bud and open bloom overlays = 21 drawings, anime linework: confident ink contour of varying weight, flat cel colour + one shade step, light wash; on white, keyed by ink threshold | `Card` alpha-to-coverage; **boil = UV jitter at 10 fps** (no extra frames, round-3 IWSDK suggestion) |
| Tiles | one raised tile mesh, instanced 21x | states drawn by shader: kept (ink fill in the arc colour), late (pencil hatch), missed (pale paper), today (dashed outline), before (blank) | one instanced draw |
| Halo | - | derived from each plant's silhouette (dilate, line, glow) by script | additive, breathing, boiling |
| Sparkles, breath circle | - | hand-drawn sparkle sprites (image_gen); ink circle analytic | `Card` |
| Table contact | - | - | `ShadowCatcher` darkening the real desk under the dial |

Reference framing (round-3 `RUNBOOK.md`): dial ellipse centre at pixel (785, 615) of 1824 x 1024, about 37 degrees
elevation, 30 degrees vertical FOV, over `shared/assets/room-plates/plate-dial.png`, hand matte
`dial-hand-matte.png` re-laid on top for parity captures only (dev).

Gate frames: **G1** ref A2-05-1 with the pinch halo on the midday plant; **G2** the same framing, idle (no halo, shadow
at 14:20); **G3** A/2-05-F1 "the drawn dial in a real kitchen" (generated by T-SUN-006 step 1 from the exact prompt in
`OWNER-CHOICE.md`; owner-approved before it anchors anything); **G4** A/2-05-F2 day 7 (same rule). Ref A2-05-2 (the
dial held upright, labelled arcs) is a palette and tile-layout reference only; the app keeps the dial on the desk.

Budgets handed up front: scene hard caps 30 draws / 30k tris / textures <= 1024 px (2048 source); soft caps 24 / 24k.

## 7. Audio plan

Full list and credits: `docs/audio/AUDIO-BIBLE.md` sections 3.2, 4, 5, 7. Summary:

- **SFX (about 11 cues)**: the tock (two decaying wooden partials with a pitch drop, round 1), dial appear (page and
  pencil), tile ink fill (nib scratch, very short), undo (eraser brush), backfill (pencil hatch), packet open (paper),
  seed drop (soft soil), bloom pulse (tiny paper flutter), dusk chime (one close, ~2 s decay), breath in / out (none:
  the ink circle is silent; the voice is optional), settings tab (paper tap).
- **Narration** (optional voice for the dusk ritual, 8 lines, `vo.sun.*`): "Three breaths to close the day." /
  "Breathe in." / "And out." (two variants each) / "One more." / "That's the day. Goodnight." / first-run line
  "Here is your day." (only if the voice guide is on). No medical words, no em dash.
- **Music**: three unpulsed arc beds (sunrise air, midday room, dusk pad), 30 s seamless loops, off by default,
  crossfading 4 s when the shadow enters an arc; -30 LUFS; duck 6 dB under the voice.
- **Credits** (pot 14,000): SFX ~11 cues x 2.5 variants x ~140 = ~4,000 (plus retakes, cap 4,500); narration ~350
  characters x 3 takes = ~1,100 (cap 1,500); beds 3 x 30 s x 60 = 5,400 + one 10 s fix = 6,000; slack 2,000.

## 8. Milestones and acceptance

`U="C:/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Unity.exe"`, `R=orchestration/runs/sundial/<task-id>`.

| Milestone | Window | Tasks | Acceptance (agent-verifiable) | Owner's eyes |
|---|---|---|---|---|
| **M0 Foundations** | 10-03 to 10-05 | T-SUN-001 capture package, 002 core common, 003 Sundial rules | `dotnet test shared/core-dotnet` green with the new Common and Sundial suites (>= 25 new tests incl. DST and property tests); capture package compiles in both apps; a test capture PNG and a side-by-side exist | - |
| **M1 Vertical slice** | 10-05 to 10-09 | 004 rig + kitchen plate, 005 hero port, 008 glance-and-tend | PlayMode `Tend_LookPinch_ByPlayback` passes (ledger has 1 live event, tile `Kept`, tock cue logged); G1/G2 captures at reference framing; `Measure` within hard caps | **R1 Fri 10-09**: hover and click to tend for 5 min; is the glance readable in 2 s? |
| **M2 Art pass** | 10-09 to 10-14 | 006 SVG dial face (+ G3/G4 candidates), 007 plant cards | side-by-sides G1-G4 in `$R/sbs/`; provenance census: 0 textures keyed from reference frames; boil on plants at 10 fps in a 3 s capture; judge record (host) | **R2 Fri 10-16**: rates G1-G4; picks G3/G4 anchors; boil: charm or noise? |
| **M3 Record + ritual** | 10-12 to 10-15 | 009 dusk ritual, 010 tiles + backfill + undo | PlayMode `Dusk_ThreeBreaths`, `Backfill_Once_Late`, `Undo_InsideWindow`, `Undo_FlushOnPause` pass; captures `missed`, `backfilled`, `day7` | (R2) the missed tile reads quiet |
| **M4 Audio** | 10-14 to 10-17 | 012 audio (after T-TER-010) | every cue id in AUDIO-BIBLE 3.2 resolves; mixdown of the dusk ritual at -20 +/- 1 LUFS; within pot | (R2) listens |
| **M5 Journey** | 10-16 to 10-20 | 011 first run + seed packets; then (queued later) settings, PalmOpen dismiss, day-7 polish | PlayMode `FirstRun_FirstTendAndDusk`: first tend <= 60 s, ritual done <= 180 s from a fresh save path; `PauseAt_*` pass; corrupt save never replays first run | owner fresh-profile run (U2) at G |
| **M6 Polish + gate pack** | 10-20 to 10-22 | queued later: reduced motion, budgets, string census, player build, gate pack | `GATE.md` complete; 10/10 playback runs identical | **G Fri 10-23** |

## 9. Post-MVP backlog (more habits, more in-app rituals)

| Item | What | Gesture | Notes |
|---|---|---|---|
| 2-3 habits per arc | Up to 9 plants, 63 tiles in one instanced draw | Look + Pinch | round-1 scope; art: more species |
| Stretch reach (morning ritual, 20 s) | Reach to three inked marks around the dial; the morning plant stretches with you | Look + PalmOpen hold at each mark | round-1 "should" |
| Rim scrub | Pinch-drag on the rim to read earlier today or this week; snaps back on release; read-only | Pinch-drag | round-1 gesture 3 |
| Glasses glance | Commute moment: palm up, read in 2 s, pinch once, palm up to dismiss | PalmOpen, Look + Pinch | simulator profile only; ~70 deg FOV framing (round-1 FIG 7.2) |
| Gratitude at midday | Ink one of five small symbols into the midday arc | Pinch | no typing |
| Focus block | A 25-minute "shadow hour": the gnomon's shadow is drawn slowly while you work | Pinch to start / end | productivity track fit |
| Week dial | A second dial page for the last 4 weeks | PinchHold the page corner | record only, never a streak |
| Arc times | Move arc boundaries to the user's day | Pinch-drag on arc edges | settings |
| Hand-lettered labels | Ink arc names (ref-2 look) | - | cut-line item 3 |
| Localisation | Czech, German, Japanese | - | registry `localization` |

## 10. Quest-readiness notes

| Seam | PC (now) | Quest (Phase 2) | Rule now |
|---|---|---|---|
| Look | mouse hover ray | Quest 3: hand ray or head reticle; Pro / glasses sim: eye gaze; dwell 150 ms unchanged | `Look` is an intent with a ray; nothing assumes eyes |
| Pinch / poke | click / F | Interaction SDK pinch; near poke on the dial (within reach) | intents only (S1) |
| Dial placement | `DeskAnchor` | MRUK table + spatial anchor; PalmOpen dismiss / return | `IPlacementProvider`; save never stores the anchor's content |
| Hand over the dial | hand matte composite (dev captures only) | Depth API occlusion or a hand-mesh occluder (round-3 gap ledger: platform limit) | the dial shader keeps a depth-tested path |
| Table shadow | `ShadowCatcher` on the plate's desk plane | `ShadowCatcher` on the scene desk plane | plane comes from `IPlacementProvider` |
| Boil comfort | 10 fps jitter | check in stereo at H2 (round-3 IWSDK checklist item 10) | a setting turns it off |
| Glasses profile | - | XR Simulator glasses profile only | post-MVP |
| Tests | PlayMode playback | XR Simulator + Operator gestures (look + pinch), a `sundial_get_state` tool | keep `SundialState.ToJson()` as the oracle |
