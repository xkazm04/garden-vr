# Terrarium - game-design conformance read

Milestone 3, goal "Game-design conformance read for both apps". A reading of `apps/terrarium` and the shared core it
uses against the registry game-production subjects that govern teaching, seated comfort and honest habits.

- **Repo commit read:** `e5c0aa6` (branch base, a `main` commit).
- **Registry read:** `C:\Users\kazda\kiro\ai-registry` at `10e643f`, read-only. Every subject resolved through
  `knowledge/game-production/index.json` -> `subjects[slug].file`; digests and revisions below are from that index.
- **What this is:** a reading of code, tests and plans. Unity cannot run here (no licence, decision 0004), so every
  statement about runtime behaviour is read from code and tests and says so. No play test was run. PlayMode tests cited
  as evidence are cited for what they assert, not as having passed here.

## Counts

| Rows | Conformant | Deviation | Not applicable | Not covered by the registry |
|---|---|---|---|---|
| **50** | **14** | **12** | **12** | **12** (12 meet the repo rule, 0 deviate) |

Rows = 38 registry technique rows (6 subjects) + 12 rows not covered by the registry. The conformant, deviation and
not-applicable counts are over the 38 technique rows; the 12 not-covered rows carry their own verdict against the repo's
rule. 12 deviations are filed as 11 ideas (T6 covers two rows); see "Ideas filed".

## Subjects used

| Subject | Digest / rev | Trigger that fits (one line) |
|---|---|---|
| `learning-curve-and-teaching-design` | `sha256-b2:ad86d31cbebbbb39` / 1 | "deciding where a mechanic is introduced practised and tested": the jar teaches the hold with no menus (terrarium.md:52-53) |
| `design-canon-as-executable-law` | `sha256-b2:398ce42cd0bdc279` / 5 | "turning a written design bible into automated checks": the honest-habit rules (AGENTS.md:20-21) are meant to be law that tests read |
| `difficulty-design-and-adaptation` | `sha256-b2:d760d5742a4fa29e` / 5 | "adding difficulty settings or an adaptive system": pace, breath count, hold/toggle and auto-pace (terrarium.md:45, :56) |
| `speech-synthesis-script-writing` | `sha256-b2:d288645c0b915230` / 1 | "writing dialogue or barks that a synthetic voice will perform": 14 ElevenLabs narration lines (`Audio/Voice/CAST.md`) |
| `short-form-cards-and-barks` | `sha256-b2:ad67b1491a2e4063` / 1 | "writing reactive barks ... that will fire many times": the in/out lines fire on every breath, every evening |
| `playtest-signal-to-defect` | `sha256-b2:3ecfd50d6322e70f` / 1 | "designing a session or bug-report schema": owner sessions R1, R2 and the U2 first-run witness (PLAN.md:59-61, :88) |

Weighed and not kept: `spatial-audio-scene-authoring` (its trigger, an audio event catalog, fits the AUDIO-BIBLE, but it
governs mix craft, not teaching, comfort or habits, and it was applied on 10-02, registry-consult.md:29);
`motion-quality-gating` (its triggers are animation-asset linting and machine critics of motion; response latency is
gate S3, PLAN.md:99, a seamlessness item, not a design one).

**Registry gap.** No game-production subject has a `use_when` trigger for seated or comfort-bounded play, VR/XR
locomotion or viewpoint comfort, or habit honesty (streaks, shaming, loss on a missed day). A search of all 525
technique triggers for comfort, seated, VR, XR, habit, streak, shame and wellness found none. Seated comfort
(AGENTS.md rule 2) and honest habits (rule 3) are therefore read as rows **not covered by the registry**, against the
repo's own rules. These rows are the registry gap that milestone 5's VR/XR coverage goal can cite.

## Rows

Verdicts: **C** conformant, **D** deviation, **N/A** not applicable. Paths: `Ritual/` = `apps/terrarium/Assets/Scripts/Ritual/`,
`Jar/` = `apps/terrarium/Assets/Scripts/Jar/`, `core/` = `shared/packages/com.gardenvr.core/Runtime/`, `tests/` =
`shared/core-dotnet/GardenVR.Core.Tests/`, `PlayMode/` = `apps/terrarium/Assets/Tests/PlayMode/`.

### learning-curve-and-teaching-design

| # | Technique | Rule (one line) | Verdict | Evidence | Consequence and smallest fix |
|---|---|---|---|---|---|
| LC1 | skill-atom-inventory | Keep a prerequisite-linked list of every atom the player must learn, each bound to its introduce, practise and test sites. | D | No atom list exists: the plan has a journey timeline (docs/plans/terrarium.md:81-98) and registry-consult.md:34 names four atoms with no sites. Atoms with no site at all: palm pause, cork look-back, settings poke, auto-pace (see LC6). | The team cannot answer "was this taught?" for the day-7 look-back or the pause; untaught features read as done. Fix: a table of atoms keyed to the tour step ids in `Ritual/FirstRunSteps.cs:8-17`, each with its sites; an atom with no site is listed as untaught. Idea **T1**. |
| LC2 | introduce-practise-test-spacing | Introduce one atom alone at no cost, practise it, then require it later; the spacing is the design. | C | Hold = inhale is introduced alone at `tour.hold` with words and the ghost hand (`Ritual/FirstRunDirector.cs:179-184`, :287-309), practised on breaths 2-5 after the words fade (:217-219), and required on later evenings with the words back only after 8 s idle (:28, :225-238). Fidgets cost nothing (`core/Breath/BreathSession.cs:23`, :150-155). Habits come only after the first answer (`Ritual/FirstRunDirector.cs:329-333`). | - |
| LC3 | flow-corridor-as-two-sided-envelope | State a ceiling (nothing demanded beyond what was taught) and a floor (something new to learn), and check both. | D | Ceiling holds: nothing beyond the hold is demanded. Floor is unstated: the evening asks the same six breaths forever; novelty comes only from growth cadence (`core/Terrarium/Garden.cs:30-32`, `core/Terrarium/Season.cs`). No plan line states what is new on evening 10 or 20, and the only witness is U4 (PLAN.md:90). | A player who has nothing new to notice by week 2 drifts away silently, and nothing reports it. Fix: write the novelty floor (which evening brings what: flower at 6, inner layer at 12, season weeks) and add "anything new tonight?" to the U4 notes. Idea **T2**. |
| LC4 | teaching-escalation-ladder | Teach from the cheapest rung (affordance, situation) and climb to prompts only after a measured failure. | D | The tour uses demonstration and a contextual prompt that fade on success (`Ritual/FirstRunDirector.cs:217-219`). But with the voice guide off (the default, `core/Terrarium/TerrariumSave.cs` settings defaults) every hold and release still shows a caption ("Breathe in.", "And let go.") because `Speak` falls back to text (`Ritual/VoiceGuide.cs:131-136`; the guide always exists, `Ritual/JarRitualController.cs:1161`). Plan: "The words fade for good; they return only after 8 s idle" (docs/plans/terrarium.md:93). Read from code; not seen in a run. | Every breath of every evening carries a text prompt the player no longer needs; the jar stops being the teacher (PLAN.md:280, D6). Fix: with the voice off, show captions only where the tour or the idle return would show words. Idea **T3**. |
| LC5 | time-to-competence-measurement | Measure per atom how long a stated population takes to meet a stated criterion; tutorial completion is not the metric. | D | The only timing is the scripted run (`PlayMode/FirstRunPlaybackTests.cs:82`, `answerAt <= 180`) and the owner stopwatch for the whole moment (PLAN.md:88). No atom has a criterion. The save keeps only the step id (`core/Terrarium/TerrariumSave.cs:47`); step times go to Player.log only (`Ritual/FirstRunDirector.cs:348`). | The gate cannot tell a player who learned the hold from one carried by the ghost hand, or where a stuck player stopped. Fix: one criterion per atom (for example "first counted breath after the ghost hand leaves, unprompted") and keep the existing step timestamps in a per-session file for U2. Idea **T4**. |
| LC6 | unused-mechanic-detection | A mechanic nobody uses is a teaching defect until shown otherwise; look for mechanics with no test site. | D | No teaching site: cork pinch-hold 2 s look-back (`core/Terrarium/LookBack.cs:32`, `Ritual/JarRitualController.cs:1247`); palm pause (the "hold P" hint, `shared/packages/com.gardenvr.input/Runtime/HandIntent.cs:69`, is never shown; only the PinchHold hint is, `Ritual/JarRitualController.cs:104-112`); settings poke F (`Ritual/SettingsPebbles.cs:229`, :235 `PokeOnly`); auto-pace (row DD2). Usage is counted only in memory (`Ritual/JarRitualController.cs:73`, :1166-1174). | The day-7 look-back, the plan's week payoff (docs/plans/terrarium.md:54, :116-117), is likely never found, and nobody would know. Fix: an affordance on the cork once 6 fronds exist (a glint under Look) and a soft "hold P to rest" at the first Continue breathing? prompt. Idea **T5**. |

### design-canon-as-executable-law

| # | Technique | Rule (one line) | Verdict | Evidence | Consequence and smallest fix |
|---|---|---|---|---|---|
| DC1 | canon-as-single-source-of-thresholds | Each design number has one home, and every check and view reads it from there. | D | Mostly holds: `core/Terrarium/Garden.cs:29-32` constants are read by tests (`tests/TerrariumInvariantTests.cs:19-21`, :58-67) and by the lean (`Jar/JarView.cs:660`). Breaks: `Jar/JarView.cs:1049` clamps with a bare `0.6f`, pinned only by a test whose comment cites a stale line (`tests/BreathRitualRulesTests.cs:146`, "JarView.cs:1020"); the cork has six dots hard-coded (`Ritual/JarRitualController.cs:1543-1547`) while the breath count is 3, 4, 6 or 8 (`Ritual/SettingsPebbles.cs:28`); `MinInhaleSeconds`/`MinExhaleSeconds` (`core/Breath/BreathSession.cs:23`, :25) are asserted by no test. | With 3 breaths the ritual completes with half the dots empty; with 8 the dots are full two breaths early. Fix: draw the dots from `BreathConfig.TargetBreaths` and read `Garden.VitalityFloor` in JarView. Idea **T6**. |
| DC2 | parse-thresholds-from-prose | When a check's threshold comes from a written rule, extract it by parser so prose and check cannot drift. | N/A | The canon is code, not prose: thresholds live in `core/Terrarium/Garden.cs:29-32` and `core/Breath/BreathSession.cs:22-26`; the plan's code block (docs/plans/terrarium.md:150-198) is a copy for readers, and no checker reads prose. | - |
| DC3 | shape-check-vs-content-invariant | Label each check by what it can conclude, and never report a shape check as proof of the content rule. | D | Growth is a true content invariant: property test plus negative control (`tests/TerrariumInvariantTests.cs:98`, :105-108). The honest-copy rule is not: the gate is a grep (PLAN.md:91) that also matches non-user identifiers (`Jar/JarView.cs:33`, :168 `DeskStreak`), so it can never come back empty; the word ban is one PlayMode helper (`PlayMode/JourneyTests.cs:421-434`) with its own list that no dotnet test reads, and Sundial keeps different lists. | A shaming or "missed" string added outside the strings that helper scans would ship, and the grep would not tell it from shader names. Fix: one banned-word list in core, read by a dotnet test over the voice `lines.json` and the string constants, and by the PlayMode helpers. Idea **T7**. |
| DC4 | flag-your-own-shipped-defaults | Prove a checker fires on a known-bad input before trusting its passes. | C | Negative controls: `tests/TerrariumInvariantTests.cs:105` (a dropped frond is reported, :108), `tests/SeasonTests.cs:153`. | - |
| DC5 | self-declared-budget-enforcement | An artifact that states its own limit is checked against its own measured value. | C | Every voice line states `window_s` and `measured_s` and each take fits (`apps/terrarium/Assets/Audio/Voice/CAST.md:36-49`); two lines that overran were reworded (`Audio/Voice/lines.json:47-49`, :105-107). No test re-checks it after a re-render; the render tool did. | - |
| DC6 | archetype-aware-envelopes | Use one band per kind of thing graded, not one band for all. | N/A | The app grades no content kinds against bands; the jar and companions share one vitality curve on purpose (docs/plans/terrarium.md:183; `tests/VitalityParityTests.cs:23`). | - |

### difficulty-design-and-adaptation

| # | Technique | Rule (one line) | Verdict | Evidence | Consequence and smallest fix |
|---|---|---|---|---|---|
| DD1 | four-term-difficulty-decomposition | Hold difficulty as four separable terms and state the assumed skill. | N/A | No opposition and no failure: a short hold is "ignored, never penalised" (`core/Breath/BreathSession.cs:23`); the ring never marks a breath wrong (docs/plans/terrarium.md:39-41). | - |
| DD2 | player-chosen-challenge-and-adjustment-hazards | The player declares the challenge first, in a setting they can revise; a system adjusts only where that cannot cover the spread. | D | Pace (3/5, 4/6, 5/7 s), breath count and hold/toggle are player settings, revisable any evening (`Ritual/SettingsPebbles.cs:26-28`, :180, :203-208); nothing adjusts on performance (none found). But the plan's auto-pace fallback (docs/plans/terrarium.md:45) has no player path: `AutoPace` is read once at start (`Ritual/JarRitualController.cs:386`) and toggled only by dev F2 (:952; gated at `shared/packages/com.gardenvr.input/Runtime/Mapping/KbmIntentMapper.cs:114`). | A player who cannot pace a hold has no "the jar breathes, you follow" option in a release build. Fix: an auto-pace pebble that writes the existing `RitualSettings.AutoPace`. Idea **T8**. |
| DD3 | reward-cadence-first-diagnosis | Audit how rewards are spaced before reshaping a challenge curve. | C | The cadence is stated and tested: one frond a day, dew for extra rituals, first flower at 6 then every 6, inner layer at 12 (`core/Terrarium/Garden.cs:30-32`, `tests/TerrariumInvariantTests.cs:19-21`); a week with one miss still flowers (`tests/LookBackTests.cs:7`). | - |
| DD4 | setting-bounded-overlapping-bands | Live adaptation stays inside a band the player's setting locks. | N/A | No live adaptation: `BreathConfig.From` is a pure function of the settings (`core/Breath/BreathSession.cs:35-45`). | - |
| DD5 | skill-scaling-versus-power-scaling | Choose between raising the opposition's numbers and improving its play. | N/A | No opposition (docs/PLAN.md:42, the core gesture is a breath). | - |
| DD6 | skill-swap-cross-tier-test | Prove a skill tier beats a stat advantage. | N/A | No competitors or tiers (docs/PLAN.md:42). | - |
| DD7 | equal-skill-straight-time-loss | Hold skill equal and prove a capped competitor loses time. | N/A | No competitors (docs/PLAN.md:42). | - |
| DD8 | tier-changes-decisions-not-specs-test | A difficulty tier changes opposition decisions, not specs. | N/A | No opposition tiers (docs/PLAN.md:42). | - |

### speech-synthesis-script-writing

| # | Technique | Rule (one line) | Verdict | Evidence | Consequence and smallest fix |
|---|---|---|---|---|---|
| SS1 | change-the-text-before-the-settings | Fix a bad render by re-roll or rewording before touching global voice settings. | C | Two overlong lines were reworded, settings untouched: "And in, slowly." -> "In, slowly." and "One more. The last one." -> "One last breath." (`apps/terrarium/Assets/Audio/Voice/lines.json:47-49`, :105-107); one global setting set (`Audio/Voice/CAST.md:8`). | - |
| SS2 | hesitation-marks-can-silence-a-line | Avoid ellipses, fillers and pause tags, which a model may render as dead air. | C | No ellipsis, filler or pause tag in any line (`Ritual/VoiceGuide.cs:173-186`). | - |
| SS3 | length-and-breath-budget | One idea per line, about one breath, timed against a stated budget. | C | Windows per line class (in 1.8 s, out 2.5 s, other 4 s; `Ritual/VoiceGuide.cs:191-196`), and every kept take fits its window (`Audio/Voice/CAST.md:36-49`). | - |
| SS4 | punctuation-as-direction | Punctuation is the performance; confirm each mark rendered as meant by listening. | D | The marks are sound (every line ends in a full stop, short lines shaped: "In, slowly.", `Ritual/VoiceGuide.cs:176`), but "Nobody listened to the takes: selection is by measured duration and loudness only" (`Audio/Voice/CAST.md:25-27`). | A take that renders "Whenever you are ready." as a question, or flat, plays at the first pause and nobody has heard it. Fix: an owner listening pass with a note per line id, as CAST.md:26-27 already asks. Idea **T9**. |
| SS5 | tags-depend-on-the-model-generation | Bracketed tags and markup depend on the model; pin the model and do not rely on tags. | C | No tags in any line; model pinned `eleven_multilingual_v2` (`Audio/Voice/CAST.md:7`, `Audio/Voice/lines.json`). | - |
| SS6 | text-carries-the-emotion-not-the-settings | Put the feeling in the words; global settings apply to every line. | C | Calm is in the words ("Rest here as long as you like.", `Ritual/VoiceGuide.cs:184`); settings are one moderate global set (stability 0.65, style 0.1, `Audio/Voice/CAST.md:8`). | - |

### short-form-cards-and-barks

| # | Technique | Rule (one line) | Verdict | Evidence | Consequence and smallest fix |
|---|---|---|---|---|---|
| SB1 | bark-carries-character-in-one-breath | A reactive line carries one fact the player cannot see and one point of view. | D | The event lines carry an unseen fact: the count ("Halfway.", "Six breaths.", "One last breath.") and permanence ("Everything you grew is still here."; `Ritual/VoiceGuide.cs:173-185`). The count fact is fixed to six: "Halfway" fires on breath 3 whatever the target (:99-103), and the open and close lines say "six" (:173, :182) while the player may choose 3, 4 or 8 (`Ritual/SettingsPebbles.cs:28`). Read from code. | With 3 breaths the player hears "Halfway" on the last breath and then "Six breaths. This frond stays."; the jar miscounts the player's own ritual. Fix: count lines follow `TargetBreaths` (no "six" in open/close, mid line at half). Idea **T6**. |
| SB2 | barks-that-survive-repetition | The more often a line fires, the plainer it must be; vary the angle; prefer silence to a repeat. | C | The every-breath lines are plain and attitudinal, three variants each, rotated round-robin with cursors kept across evenings (`Ritual/VoiceGuide.cs:57`, :159-166); the specific lines fire rarely (mid once per ritual, :101-102; return once after a gap, :114-118). No fires-per-session estimate is written down. | - |
| SB3 | callback-pays-a-seed | A later line returns to a planted detail and changes its meaning, and only fires after the seed was seen. | C | "This frond stays." at the answer (`Ritual/EtchContrast.cs:21`) is paid after a gap by "Welcome back. Everything you grew is still here." (`Ritual/VoiceGuide.cs:185`), which fires only when a past ritual exists (:114-118). | - |
| SB4 | picture-carries-the-card | Text beside an image says what the image cannot. | C | The answer text names permanence ("This frond stays.", `Ritual/EtchContrast.cs:21`) while the picture shows the frond settling (docs/plans/terrarium.md:95); the missed day has no text at all (`Ritual/JarRitualController.cs:315`). | - |
| SB5 | television-read-budget | State the text budget for the viewing surface, in units with their basis, before writing. | D | Etched words are sized by hand per call (`Ritual/JarRitualController.cs:1615`, 0.16 / 0.012; `Ritual/SettingsPebbles.cs:229`, 0.046) and have a contrast floor (`Ritual/EtchContrast.cs:18`, ratio 4.5) but no stated size or line budget in degrees at the 40 cm desk distance. | Words sized on a PC monitor may be too small or too long to read in a headset; nobody can check it before H1. Fix: state a minimum glyph height in degrees and a words-per-line cap, and assert it in an EditMode test. Idea **T10**. |
| SB6 | three-line-card-image-trade-turn | A story card is one image, one trade, one turn. | N/A | No story cards; the app has no narrative (docs/plans/terrarium.md:72-74). | - |

### playtest-signal-to-defect

| # | Technique | Rule (one line) | Verdict | Evidence | Consequence and smallest fix |
|---|---|---|---|---|---|
| PS1 | session-instrumentation-contract | Decide what a session must record before the first session. | D | R1 is 2026-10-09 (PLAN.md:59). No session record schema is in the repo; registry-consult.md:35 plans an "owner review notes template" that does not exist. The app's own logs are Player.log lines (`Ritual/FirstRunDirector.cs:348`) and a ritual log written only in debug or editor builds (`Ritual/JarRitualController.cs:273-276`, :1827-1830). | R1 and U2 findings become rumours: no build id, no save, no step times to place where the player stopped. Fix: a one-page session record (build sha, save file, Player.log, observations kept apart from fixes) used from R1. Idea **T11**. |
| PS2 | observation-before-interpretation | Keep what happened apart from why someone thinks it happened. | C | The plan requires it for U2: "observation notes separate from fixes" (PLAN.md:88). | - |
| PS3 | frequency-and-severity-as-separate-axes | Report how often and how bad as two numbers. | N/A | No finding queue exists yet; the first owner session is R1 (PLAN.md:59). | - |
| PS4 | repro-minimization-protocol | Shrink a session to the smallest reliable trigger. | C | Scripted playback drives the same intents by target id (docs/plans/terrarium.md:136-139), so a session can be replayed as JSONL (`PlayMode/FirstRunPlaybackTests.cs:39`). | - |
| PS5 | complaint-to-owning-subject-routing | Route a finding to the owner of its defect class. | N/A | No finding queue yet (PLAN.md:59). | - |
| PS6 | unreproducible-is-a-state-not-a-dismissal | Keep unreproduced findings open as a state. | N/A | No finding queue yet (PLAN.md:59). | - |

### Not covered by the registry: seated comfort (AGENTS.md rule 2) and honest habits (rule 3)

No registry subject governs these; each row cites the repo's own rule. Verdicts are against that rule.

| # | Repo rule | Verdict | Evidence (how the app meets it) |
|---|---|---|---|
| NC1 | Seated: the view rotates, never translates (AGENTS.md:19; docs/plans/terrarium.md:131) | meets | Position fixed at eye height, rotation only (`shared/packages/com.gardenvr.room/Runtime/SeatedRig.cs:7`, :150); yaw 40 / pitch 25 limits and R recentre (`shared/packages/com.gardenvr.input/Runtime/Mapping/KbmIntentMapper.cs:17-18`, :97-110). No camera writes in app code (searched `apps/terrarium/Assets/Scripts`). |
| NC2 | Two-foot radius (AGENTS.md:19) | meets | The jar sits 0.40 m ahead and 0.30 m below the eye (`shared/packages/com.gardenvr.room/Runtime/PcDeskAnchor.cs:16-17`, :88). |
| NC3 | Under ten minutes to a complete, satisfying moment (AGENTS.md:19; PLAN.md:87-88) | meets (scripted) | `PlayMode/FirstRunPlaybackTests.cs:82` asserts the answer by 180 s on the scripted run; `tests/TerrariumCoreTests.cs:281-290` asserts the cold start to answer is under 10 min. The human witness (U2) is not measured. |
| NC4 | Fast start (AGENTS.md:19) | meets | No splash or menu; the hold words appear at 8 s (`Ritual/FirstRunDirector.cs:26`, :179-184). S5 cold start is not measured here. |
| NC5 | Clean pause/resume; a cut-off ritual is offered back, never auto-restarted (AGENTS.md:19; PLAN.md:100) | meets | Pause latch and fresh pinch (`Ritual/JarRitualController.cs:494-503`, :904-926), offer back on relaunch (:247-255, :430-431); tests assert no self-restart (`PlayMode/FirstRunPlaybackTests.cs:142`, `PlayMode/JourneyTests.cs:89`, :159) and five `PauseAt_*` points (:49-81). Not run here. |
| NC6 | Comfort under reduced motion; breath timing survives (docs/plans/terrarium.md:56; registry-consult.md:47) | meets | Reduced motion steps the uncoil, fog and ring and shows end states (`Ritual/JarRitualController.cs:640-648`, :1744-1765); test asserts timing did not collapse (`PlayMode/FirstRunPlaybackTests.cs:273`). |
| NC7 | Growth only rises (AGENTS.md:20) | meets | Fronds only accumulate (`core/Terrarium/Garden.cs:20`, :98); property test and negative control (`tests/TerrariumInvariantTests.cs:98`, :105). |
| NC8 | A missed day is quiet (AGENTS.md:20) | meets | "A missed day is quiet. The gap itself never plays a cue." (`Ritual/JarRitualController.cs:315`); `PlayMode/JourneyTests.cs:169`, :213-219 assert no cue and quiet copy. |
| NC9 | A missed day is recoverable (AGENTS.md:20) | meets | One ritual restores full glow (`Recovered`, `tests/BreathRitualRulesTests.cs:28`); the look-back holds a missed day as a frame, nothing marked wrong (`core/Terrarium/LookBack.cs:7`, `tests/LookBackTests.cs:7`). |
| NC10 | Nothing wilts to death (AGENTS.md:20) | meets | Glow floor 0.6 (`core/Terrarium/Garden.cs:29`, :71-75); lean at most 6 degrees (`Jar/JarView.cs:657-661`); companions only dim (`Jar/CompanionGarden.cs:174`). |
| NC11 | Nothing turns red (AGENTS.md:20) | meets (code) | No red in app colours; the warm tones are amber (`Jar/JarView.cs:80`, :1182). Read from code, not measured in a render. |
| NC12 | Nothing shames; no streak counters (AGENTS.md:20-21) | meets | No user-facing streak or failure string (grep over `apps/terrarium/Assets`, hits are shader names and logs); `Returns` is counted but never shown (`core/Terrarium/Garden.cs:39-40`, `Ritual/GardenService.cs:365`). The enforcement gap is DC3. |

## Ideas filed

The app is closed, so these are filed as ideas (also listed in the run's `result.json` questions).

| Id | Title | Text |
|---|---|---|
| T1 | Terrarium: a skill-atom table bound to the tour step ids | Write the atoms the player must learn (hold = inhale, release = exhale, the fern answers, six breaths complete, check in Today, Yesterday, undo, palm pause, Continue breathing?, settings poke, cork look-back, voice shell, auto-pace) with introduce, practise and test sites keyed to `FirstRunSteps` ids; list an atom with no site as untaught. Row LC1. |
| T2 | Terrarium: state the novelty floor for evenings 7 to 30 | Write which evening brings what is new (flower at 6, inner layer at 12, season weeks) as the floor of the learning corridor, and add "anything new tonight?" to the U4 day notes. Row LC3. |
| T3 | Terrarium: captions with the voice off follow the tour's fade rule | With the voice guide off, `VoiceGuide.Speak` shows a caption on every hold and release, every evening. Show captions only where the tour or the 8 s idle return would show words, as docs/plans/terrarium.md:93 says. Row LC4. |
| T4 | Terrarium: per-atom competence criteria and a kept first-run timeline | Give each atom an observable criterion (for example the first counted breath after the ghost hand leaves, unprompted) and write the director's step timestamps to a per-session file the U2 run keeps. Row LC5. |
| T5 | Terrarium: teach the cork look-back, the palm pause and the settings poke | These have no teaching site. Add a cork glint under Look once 6 fronds exist and a quiet "hold P to rest" beside the first Continue breathing? prompt; show the poke binding on the settings pebble. Row LC6. |
| T6 | Terrarium: the breath count in dots, captions and voice follows the Breaths setting | The cork has six dots and the voice says "six" and "Halfway" at breath 3 whatever the setting (3, 4, 6, 8). Size the dots from `TargetBreaths`, drop "six" from open/close lines or pick lines by target, fire the mid line at half; read `Garden.VitalityFloor` instead of the bare 0.6f in JarView. Rows DC1, SB1. |
| T7 | Terrarium: one honest-copy word list that a dotnet test reads | Move the banned words (streak, fail, shame, wilt, missed, behind, em dash) to one list in core; a dotnet test scans `Audio/Voice/lines.json` and the string constants, and the PlayMode helper reads the same list. Row DC3. |
| T8 | Terrarium: an auto-pace pebble in settings | `RitualSettings.AutoPace` exists but only dev F2 sets it. Add a settings pebble so a player who cannot pace a hold can let the jar breathe. Row DD2. |
| T9 | Terrarium: an owner listening pass over the 14 voice takes | Takes were chosen by duration and loudness only. The owner listens once with a note per line id, starting with vo.ter.in.02 (CAST.md:26-27). Row SS4. |
| T10 | Terrarium: a read budget for etched words in degrees | State a minimum glyph height in degrees and a words-per-line cap for etched text at the 40 cm desk distance, and assert it in an EditMode test. Row SB5. |
| T11 | Terrarium: a session record template for R1, R2 and U2 | One page: build sha, save file, Player.log, step times, observations kept apart from proposed fixes. Used from R1 on 10-09. Row PS1. |
