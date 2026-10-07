# Sundial - game-design conformance read

Milestone 3, goal "Game-design conformance read for both apps". A reading of `apps/sundial` and the shared core it uses
against the registry game-production subjects that govern teaching, seated comfort and honest habits.

- **Repo commit read:** `e5c0aa6` (branch base, a `main` commit).
- **Registry read:** `C:\Users\kazda\kiro\ai-registry` at `10e643f`, read-only. Every subject resolved through
  `knowledge/game-production/index.json` -> `subjects[slug].file`; digests and revisions below are from that index.
- **What this is:** a reading of code, tests and plans. Unity cannot run here (no licence, decision 0004), so every
  statement about runtime behaviour is read from code and tests and says so. No play test was run. PlayMode tests cited
  as evidence are cited for what they assert, not as having passed here.
- `apps/sundial/Assets/Scripts/Tend/SundialSave.cs` is being refactored by another run this hour; rows citing it cite
  its behaviour (the saved settings and their defaults) at `e5c0aa6`, not its private members.

## Counts

| Rows | Conformant | Deviation | Not applicable | Not covered by the registry |
|---|---|---|---|---|
| **42** | **14** | **10** | **6** | **12** (9 meet the repo rule, 3 deviate) |

Rows = 30 registry technique rows (5 subjects) + 12 rows not covered by the registry. The conformant, deviation and
not-applicable counts are over the 30 technique rows; the 12 not-covered rows carry their own verdict against the repo's
rule. 13 deviations (10 technique rows + 3 not-covered rows) are filed as 14 ideas; S9 covers two rows and LC6 and DC1
each produce two ideas. See "Ideas filed".

## Subjects used

| Subject | Digest / rev | Trigger that fits (one line) |
|---|---|---|
| `learning-curve-and-teaching-design` | `sha256-b2:ad86d31cbebbbb39` / 1 | "deciding where a mechanic is introduced practised and tested": look + pinch and the dusk hold must be learned on the first run (docs/plans/sundial.md:84-95) |
| `design-canon-as-executable-law` | `sha256-b2:398ce42cd0bdc279` / 5 | "turning a written design bible into automated checks": growth only rises (PLAN D2, PLAN.md:276) is meant to be law the tests read |
| `speech-synthesis-script-writing` | `sha256-b2:d288645c0b915230` / 1 | "writing dialogue or barks that a synthetic voice will perform": 8 ElevenLabs lines (`Audio/Voice/CAST.md`) |
| `short-form-cards-and-barks` | `sha256-b2:ad67b1491a2e4063` / 1 | "writing reactive barks ... that will fire many times": the in/out lines fire every dusk; ink words are short cards on the page |
| `playtest-signal-to-defect` | `sha256-b2:3ecfd50d6322e70f` / 1 | "designing a session or bug-report schema": owner sessions R1, R2 and the U2 first-run witness (PLAN.md:59-61, :88) |

Weighed and not kept: `difficulty-design-and-adaptation` (no trigger fits: the dusk ritual has a fixed three breaths
and no difficulty setting, `apps/sundial/Assets/Scripts/Ritual/DuskRitualController.cs:21-22`, and nothing adapts);
`spatial-audio-scene-authoring` (audio mix craft, applied 10-02, registry-consult.md:29, not teaching, comfort or habits);
`motion-quality-gating` (animation-asset linting and motion critics; latency is gate S3, PLAN.md:99).

**Registry gap.** No game-production subject has a `use_when` trigger for seated or comfort-bounded play, VR/XR
viewpoint comfort, or habit honesty (streaks, shaming, loss on a missed day). A search of all 525 technique triggers for
comfort, seated, VR, XR, habit, streak, shame and wellness found none. Seated comfort (AGENTS.md rule 2) and honest
habits (rule 3) are read below as rows **not covered by the registry**, against the repo's own rules. These rows are the
registry gap that milestone 5's VR/XR coverage goal can cite.

## Rows

Verdicts: **C** conformant, **D** deviation, **N/A** not applicable. Paths: `Tend/`, `Ritual/`, `Dial/` =
`apps/sundial/Assets/Scripts/<dir>/`, `core/` = `shared/packages/com.gardenvr.core/Runtime/`, `tests/` =
`shared/core-dotnet/GardenVR.Core.Tests/`, `PlayMode/` = `apps/sundial/Assets/Tests/PlayMode/`.

### learning-curve-and-teaching-design

| # | Technique | Rule (one line) | Verdict | Evidence | Consequence and smallest fix |
|---|---|---|---|---|---|
| LC1 | skill-atom-inventory | Keep a prerequisite-linked list of every atom the player must learn, each bound to its introduce, practise and test sites. | D | No atom list exists: the plan has a first-minute timeline (docs/plans/sundial.md:84-95) and registry-consult.md:34 names "a glance selects" and "yesterday can be logged" with no sites. The wizard's step ids (`Tend/SeedCatalog.cs:148-165`) are steps, not atoms. | Nobody can say which of look, pinch, hold, backfill, dismiss or settings the dial ever teaches (most it does not, LC2, LC6). Fix: a table of atoms keyed to the `fr.*` step ids with their sites; an atom with no site is listed as untaught. Idea **S1**. |
| LC2 | introduce-practise-test-spacing | Introduce one atom alone at no cost, practise it, then require it later. | D | Two atoms are required with no introduction. Look then pinch: `fr.firsttend` waits for a pinch on the due plant (`Tend/FirstRunWizard.cs:259-264`) with only a gold glow under it (:560-576); no words, no demonstration. The breath hold: the offer "Try three breaths?" (`Ritual/DuskRitualController.cs:30`) says nothing about holding, and "Breathe in." shows only after a hold starts (`Ritual/SundialVoice.cs:84-98`). The binding hints exist (`shared/packages/com.gardenvr.input/Runtime/HandIntent.cs:64-71`) and only a test reads them (`PlayMode/ReviewPackPlaybackTests.cs:47-50`). The 60 s first tend passes on the script (`PlayMode/FirstRunTests.cs:57`), which knows both gestures. | A first-time player sees a glowing plant and a question and may not know to hover and click, or to hold for a breath; U2 (PLAN.md:88) is the first place this can show. Fix: at `fr.firsttend` a short ink line with the binding ("look, then click"), and under "Try three breaths?" the PinchHold hint, both fading after first success. Idea **S2**. |
| LC3 | flow-corridor-as-two-sided-envelope | State a ceiling (nothing beyond what was taught) and a floor (something new to learn), and check both. | D | The ceiling is breached by LC2. The floor is unstated: after the first week the day is the same three tends and three breaths; novelty is stage growth to Full at 14 kept days (`core/Sundial/Growth.cs:39-46`). No plan line states what is new in week 2 or 3; the only witness is U4 (PLAN.md:90). | A player with nothing new to notice in week 2 drifts away silently. Fix: write the novelty floor per week (stages at 3, 7, 14 kept days, bloom) and add "anything new today?" to the U4 notes. Idea **S3**. |
| LC4 | teaching-escalation-ladder | Teach from the cheapest rung (affordance, situation) and climb to prompts only after a measured failure. | C | The dial stays on the lowest rungs: an inked halo after a 0.15 s look (`Tend/SundialController.cs:26`, :387-413), a breathing glow on the due plant (`Tend/FirstRunWizard.cs:560-576`), no modal anywhere. Climbing is warranted only by a measured failure; none is measured yet (LC5). | - |
| LC5 | time-to-competence-measurement | Measure per atom how long a stated population takes to meet a stated criterion; tutorial completion is not the metric. | D | `FirstTendAt` and `RitualDoneAt` are measured but only logged (`Tend/FirstRunWizard.cs:261-262`, :333-334) and only asserted on the scripted run (`PlayMode/FirstRunTests.cs:57`, :59); `FirstRunRecorder` records video only (`Tend/FirstRunRecorder.cs:13-14`). No atom has a criterion. | The gate cannot tell where a first-time player stopped or which gesture they never found. Fix: a criterion per atom (for example the first tend by look + pinch with no help, within N s) and keep the existing timestamps in a per-session file for U2. Idea **S4**. |
| LC6 | unused-mechanic-detection | A mechanic nobody uses is a teaching defect until shown otherwise; look for mechanics with no test site. | D | Settings: a click on the tab does nothing; only F (Poke) opens it (`Tend/SundialController.cs:415-418`; `OnPinch`, :436-477, never routes to settings; no Sundial target sets `PokeOnly`). Backfill: yesterday's ask is "No glyph" (`Dial/InkLetter.cs:120`), where the plan has an ink "?" (docs/plans/sundial.md:109). Dismiss (hold P), rim scrub (`Ritual/RimScrubController.cs:10`), the week dial and the arc-time drag have no teaching site; the stretch, gratitude and focus offers do not name their gesture (`Ritual/StretchRitualController.cs:23`, `Ritual/GratitudeRitualController.cs:21`, `Ritual/FocusBlockController.cs:21`). Read from code. | A player who clicks "Settings" gets nothing and may conclude there are none; backfill, the one honest-habit recovery path, is likely never found. Fix: make the tab a poke-only target so a click pokes it (idea **S5**); give backfill, dismiss and the post-MVP rituals one teaching site each (idea **S6**). |

### design-canon-as-executable-law

| # | Technique | Rule (one line) | Verdict | Evidence | Consequence and smallest fix |
|---|---|---|---|---|---|
| DC1 | canon-as-single-source-of-thresholds | Each design number has one home, and every check and view reads it from there. | D | Growth thresholds have one home in core (`core/Sundial/Growth.cs:39-53`) read by tests (`tests/PlantMonotoneTests.cs:35`). Breaks: the dusk thresholds live in app code (`Ritual/DuskRitualController.cs:21-22`, 3 breaths, 1.5 s), against AGENTS.md rule 4, and the core test retypes them as literals (`tests/TerrariumCoreTests.cs:96-104`). The honest-copy word ban exists as separate, different lists in PlayMode tests (`PlayMode/FocusBlockTests.cs:53`, `PlayMode/GratitudeRitualTests.cs:56`, `PlayMode/JourneyTests.cs:716`), none read by a dotnet test. | If the app's 1.5 s changes, the core test still passes on its own copy, and a hold that counts in the jar may not count at the dial with nobody noticing. A new ritual's copy is checked only if its test copied the list. Fix: dusk thresholds as a core `BreathConfig` the test reads (idea **S7**); one banned-word list in core (idea **S8**). |
| DC2 | parse-thresholds-from-prose | When a check's threshold comes from a written rule, extract it by parser so prose and check cannot drift. | N/A | The canon is code, not prose: `core/Sundial/Growth.cs:39-53`, `core/Sundial/SundialRules.cs:11-14`; the plan's code block (docs/plans/sundial.md:140-185) is a copy for readers. | - |
| DC3 | shape-check-vs-content-invariant | Label each check by what it can conclude, and never report a shape check as proof of the content rule. | D | The content invariant is proven in core: stage never decreases over 1,000 lives with a negative control (`tests/SundialTests.cs:175`, :185) and `PlantMonotone` (`core/Sundial/SundialRules.cs:157-170`, `tests/PlantMonotoneTests.cs`). The app does not draw from it: the live state uses `SundialRules.Plant` (`core/Sundial/SundialState.cs:39`), and the app hides a clock set-back with a per-session stage floor (`Tend/SundialController.cs:87`, :631-634) that forgets on restart. Decision 0007 records this, with step B staged for a Unity session. | The tested rule is not the shipped path: set the clock back, restart, and a plant's drawing can shrink. Fix: build `SundialState` plants from `PlantMonotone` (decision 0007 step B). Idea **S9**. |
| DC4 | flag-your-own-shipped-defaults | Prove a checker fires on a known-bad input before trusting its passes. | C | `A_window_derived_stage_is_flagged_by_the_property_checker` (`tests/SundialTests.cs:185`). | - |
| DC5 | self-declared-budget-enforcement | An artifact that states its own limit is checked against its own measured value. | C | Every voice line states its window and measured length and fits (`apps/sundial/Assets/Audio/Voice/CAST.md:36-43`); the windows are pinned by test (`apps/sundial/Assets/Tests/EditMode/SundialAudioTests.cs:217-221`). | - |
| DC6 | archetype-aware-envelopes | Use one band per kind of thing graded, not one band for all. | N/A | One habit kind per arc in the MVP, one stage and bloom band for all (docs/plans/sundial.md:36, :168-169); no content kinds are graded. | - |

### speech-synthesis-script-writing

| # | Technique | Rule (one line) | Verdict | Evidence | Consequence and smallest fix |
|---|---|---|---|---|---|
| SS1 | change-the-text-before-the-settings | Fix a bad render by re-roll or rewording before touching global voice settings. | C | One global setting for every line (`Audio/Voice/CAST.md:8`); no line needed a fix (all fit, :36-43), and none was fixed by settings. | - |
| SS2 | hesitation-marks-can-silence-a-line | Avoid ellipses, fillers and pause tags. | C | None in any line (`Ritual/SundialVoice.cs:161-168`). | - |
| SS3 | length-and-breath-budget | One idea per line, about one breath, timed against a stated budget. | C | Windows by class (in 1.8 s, out 2.5 s, other 4 s; `Ritual/SundialVoice.cs:173`, pinned by `SundialAudioTests.cs:217-221`); every take fits (`Audio/Voice/CAST.md:36-43`). | - |
| SS4 | punctuation-as-direction | Punctuation is the performance; confirm each mark rendered as meant by listening. | D | Marks are sound (full stops; "One more." shaped), but "Nobody listened to the takes" (`Audio/Voice/CAST.md:25-27`). | A take could render "One more." as a question or "That's the day. Goodnight." flat, and nobody has heard it. Fix: an owner listening pass with a note per line id. Idea **S10**. |
| SS5 | tags-depend-on-the-model-generation | Pin the model and do not rely on tags. | C | No tags; `eleven_multilingual_v2` pinned (`Audio/Voice/CAST.md:7`). | - |
| SS6 | text-carries-the-emotion-not-the-settings | Put the feeling in the words; settings are global. | C | The close is in the words ("That's the day. Goodnight.", `Ritual/SundialVoice.cs:167`); one moderate global setting (`Audio/Voice/CAST.md:8`). | - |

### short-form-cards-and-barks

| # | Technique | Rule (one line) | Verdict | Evidence | Consequence and smallest fix |
|---|---|---|---|---|---|
| SB1 | bark-carries-character-in-one-breath | A reactive line carries one fact the player cannot see and one point of view. | C | "One more." carries the count, correct for the fixed three breaths (`Ritual/DuskRitualController.cs:21`); "That's the day. Goodnight." carries the close (`Ritual/SundialVoice.cs:161-168`). Note: the first-run voice says "Here is your day." (:168) where the ink says "This is your day." (`Tend/SeedCatalog.cs:25`); both are in the plan (docs/plans/sundial.md:88, :239), so it is recorded, not filed. | - |
| SB2 | barks-that-survive-repetition | The more often a line fires, the plainer it must be; vary the angle; prefer silence to a repeat. | C | The every-dusk lines are plain, two variants each, round-robin (`Ritual/SundialVoice.cs:147-154`); open and close fire once per ritual (:79, :102). No fires-per-session estimate is written down. | - |
| SB3 | callback-pays-a-seed | A later line returns to a planted detail and changes its meaning. | C | First run plants "This is your day." (`Tend/SeedCatalog.cs:25`); every dusk pays it with "That's the day. Goodnight." (`Ritual/SundialVoice.cs:167`), which fires only after a day exists. | - |
| SB4 | picture-carries-the-card | Text beside an image says what the image cannot. | C | "This is your day." types out while the shadow sweeps 06:00 to now (`Tend/FirstRunWizard.cs:309-312`, :533-542): the words give the sweep its meaning. The missed day has no words (`PlayMode/JourneyTests.cs:179-221`). | - |
| SB5 | television-read-budget | State the text budget for the viewing surface, in units with their basis, before writing. | D | Ink words have no stated size or line budget in degrees. Their reading distance depends on a PC scene choice: the dial sits 0.26 m ahead in the scene (`apps/sundial/Assets/Editor/SceneSetup.cs:38-44`), the plan says ~0.55 m (docs/plans/sundial.md:43; `shared/packages/com.gardenvr.room/Runtime/PcDeskAnchor.cs:18`). | Words that read at 26 cm on PC may be half the angular size at 55 cm on Quest. Fix: state a minimum glyph height in degrees at 0.55 m and a words-per-line cap; assert it in an EditMode test. Idea **S11**. |
| SB6 | three-line-card-image-trade-turn | A story card is one image, one trade, one turn. | N/A | No story cards; no narrative (docs/plans/sundial.md:73-76). | - |

### playtest-signal-to-defect

| # | Technique | Rule (one line) | Verdict | Evidence | Consequence and smallest fix |
|---|---|---|---|---|---|
| PS1 | session-instrumentation-contract | Decide what a session must record before the first session. | D | R1 is 2026-10-09 (PLAN.md:59). No session record schema is in the repo; registry-consult.md:35 plans a notes template that does not exist. The app logs `first-interactive` (`Tend/SundialController.cs:278-296`) and the wizard's times to Player.log only (`Tend/FirstRunWizard.cs:261-262`). | R1 and U2 findings arrive without build, save or step times and cannot be placed or reproduced. Fix: a one-page session record (build sha, save file, Player.log, observations kept apart from fixes) used from R1. Idea **S12**. |
| PS2 | observation-before-interpretation | Keep what happened apart from why someone thinks it happened. | C | Required for U2: "observation notes separate from fixes" (PLAN.md:88). | - |
| PS3 | frequency-and-severity-as-separate-axes | Report how often and how bad as two numbers. | N/A | No finding queue exists yet; first owner session R1 (PLAN.md:59). | - |
| PS4 | repro-minimization-protocol | Shrink a session to the smallest reliable trigger. | C | Playback drives intents by stable target id (docs/plans/sundial.md:131-132; `Tests/Playback/first-run.jsonl` used by `PlayMode/FirstRunTests.cs:36`). | - |
| PS5 | complaint-to-owning-subject-routing | Route a finding to the owner of its defect class. | N/A | No finding queue yet (PLAN.md:59). | - |
| PS6 | unreproducible-is-a-state-not-a-dismissal | Keep unreproduced findings open as a state. | N/A | No finding queue yet (PLAN.md:59). | - |

### Not covered by the registry: seated comfort (AGENTS.md rule 2) and honest habits (rule 3)

No registry subject governs these; each row cites the repo's own rule. Verdicts are against that rule.

| # | Repo rule | Verdict | Evidence (how the app meets it) | Consequence and smallest fix |
|---|---|---|---|---|
| NC1 | Seated: the view rotates, never translates (AGENTS.md:19; docs/plans/sundial.md:128) | meets | Rotation only, fixed eye position (`shared/packages/com.gardenvr.room/Runtime/SeatedRig.cs:7`, :19-20); yaw 40 / pitch 25 and R recentre (`shared/packages/com.gardenvr.input/Runtime/Mapping/KbmIntentMapper.cs:17-18`, :105-110). No camera writes in app code. | - |
| NC2 | Two-foot radius, the dial ~55 cm ahead (AGENTS.md:19; docs/plans/sundial.md:43) | deviation | Within two feet, but the scene puts the dial 0.26 m ahead, 0.15 m below the eye, to fill the PC frame (`apps/sundial/Assets/Editor/SceneSetup.cs:38-44`, :147-148); the anchor's own value stays 0.55 m (`shared/packages/com.gardenvr.room/Runtime/PcDeskAnchor.cs:18`). Read from code; no headset. | On a headset a dial at 26 cm sits closer than comfortable focus, and the art, text and halo are tuned at that distance. Fix: frame the PC view with the camera's field of view, not by moving the dial, and keep 0.55 m as the placement the Quest provider uses. Idea **S13**. |
| NC3 | Under ten minutes to a complete moment (AGENTS.md:19; PLAN.md:87-88) | meets (scripted) | First tend <= 60 s and ritual <= 180 s asserted on the scripted run (`PlayMode/FirstRunTests.cs:57`, :59). U2 not measured. | - |
| NC4 | Fast start (AGENTS.md:19) | meets | The timed draw-in steps total 5.3 s, 0 s under reduced motion (`Tend/FirstRunWizard.cs:21-25`, :289-299); a `first-interactive` mark is logged (`Tend/SundialController.cs:278-296`). S5 not measured here. | - |
| NC5 | Clean pause/resume; nothing lost (AGENTS.md:19; PLAN.md:100) | meets | A waiting tend is saved on pause and quit (`Tend/SundialController.cs:502-507`, :264-268; `PlayMode/TendPlaybackTests.cs:78`); the ritual resumes on a fresh pinch and asks "Continue?" after 60 s (`Ritual/DuskRitualController.cs:167-175`, :189-194); `PauseAt_*` tests (`PlayMode/JourneyTests.cs:270`, :310, :366, :420, :478). A ritual cut off by quitting is offered again only through the normal dusk offer (:355-363), not by the core's offer-back (decision 0005 records the drift). Not run here. | - |
| NC6 | Comfort: boiling line off by a setting (docs/plans/sundial.md:57-58, :283) | deviation | Reduced motion stops the boil (`Tend/SundialController.cs:661-662`), but the planned boil toggle has no Settings row: the tabs are Mute, Beds, Voice, Reduced motion (`Tend/SettingsTabs.cs:24-28`); the saved boil flag defaults on and nothing sets it (SundialSave behaviour at `e5c0aa6`). | A player who finds the 10 fps line flicker uncomfortable in stereo (PLAN.md:314) must give up all motion to stop it. Fix: a "Boil" tab that writes the existing flag. Idea **S14**. |
| NC7 | Growth only rises (AGENTS.md:20; PLAN.md:276) | deviation | Core proves it (`tests/SundialTests.cs:175`, :185); the app draws from the non-monotone path with a session floor (`core/Sundial/SundialState.cs:39`, `Tend/SundialController.cs:87`, :631-634). See DC3. | A clock set back plus a restart can shrink a plant. Fix: decision 0007 step B. Idea **S9**. |
| NC8 | A missed day is quiet (AGENTS.md:20) | meets | `MissedDay_IsQuiet` (`PlayMode/JourneyTests.cs:179-221`); no missed-day count anywhere (searched); the tile is pale (`shared/packages/com.gardenvr.fx/Runtime/Shaders/FToon.shader:186`, :198). | - |
| NC9 | A missed day is recoverable (AGENTS.md:20) | meets | Yesterday can be logged once, marked late with its real time (`core/Sundial/SundialRules.cs:141-149`; `tests/SundialTests.cs:196`); a bloom reopens as the week fills (`core/Sundial/Growth.cs:48-53`). Discoverability of backfill is LC6. | - |
| NC10 | Nothing wilts to death (AGENTS.md:20) | meets | No wilt state: bloom is None, Bud or Open (`core/Sundial/Growth.cs:48-53`); a bloom folds back to a bud (`Dial/DialView.cs:1696-1720`). | - |
| NC11 | Nothing turns red (AGENTS.md:20) | meets (code) | Missed is pale paper (`FToon.shader:186`, :198); the midday arc's coral marks a kept day (:219); the week page carries a red detector (`Ritual/WeekPageInk.cs:11`, :152-166). Read from code, not measured in a render. | - |
| NC12 | Nothing shames; no streak counters (AGENTS.md:20-21) | meets | No user-facing streak or failure string (grep over `apps/sundial/Assets`); tile labels such as "Missed" appear only in the dev overlay (`Tend/SundialController.cs:331-366`); cue names are checked for streak, fail, shame, wilt (`PlayMode/JourneyTests.cs:716`). The enforcement gap is DC1. | - |

## Ideas filed

The app is closed, so these are filed as ideas (also listed in the run's `result.json` questions).

| Id | Title | Text |
|---|---|---|
| S1 | Sundial: a skill-atom table bound to the first-run step ids | Write the atoms (look selects, pinch tends, the tile fills, undo, the shadow is the clock, hold = in, release = out, backfill yesterday, dismiss, settings poke, bloom follows the week) with introduce, practise and test sites keyed to the `fr.*` ids; list an atom with no site as untaught. Row LC1. |
| S2 | Sundial: introduce look-and-pinch and the breath hold before they are required | The first tend and the dusk ritual require gestures nothing introduces. Add a short ink line with the binding at `fr.firsttend` and the PinchHold hint under "Try three breaths?", both fading after the first success. Row LC2. |
| S3 | Sundial: state the novelty floor for weeks 2 to 4 | Write what is new each week (stages at 3, 7, 14 kept days, bloom) as the corridor's floor, and add "anything new today?" to the U4 day notes. Row LC3. |
| S4 | Sundial: per-atom competence criteria and a kept first-run timeline | Give each atom an observable criterion and write `FirstTendAt`, `RitualDoneAt` and the step times to a per-session file the U2 run keeps. Row LC5. |
| S5 | Sundial: a click on the Settings tab opens it | Only F opens Settings; a click on the tab is ignored. Mark the tab poke-only so the provider turns a click into a Poke, as Terrarium's pebble does. Row LC6. |
| S6 | Sundial: teaching sites for backfill, dismiss and the post-MVP rituals | Yesterday's ask has no glyph (the plan has an ink "?"), dismiss is never named, and the stretch, gratitude, focus, rim scrub, week dial and arc-time offers do not name their gesture. Give each one teaching site. Row LC6. |
| S7 | Sundial: dusk breath thresholds move to core | `BreathsRequired = 3` and `MinInhaleSeconds = 1.5f` live in `DuskRitualController`; move them to a core config the core test reads instead of retyping them. Row DC1. |
| S8 | Sundial: one honest-copy word list that a dotnet test reads | The banned words are copied, differently, into three PlayMode tests. Keep one list in core; a dotnet test scans `Audio/Voice/lines.json` and the string constants. Row DC1. |
| S9 | Sundial: draw plants from PlantMonotone | The app draws from `SundialRules.Plant` with a per-session floor, so a clock set-back and a restart can shrink a plant. Build plants from `PlantMonotone`. This is decision 0007 step B, already staged; file only if the staged step is not already tracked. Rows DC3, NC7. |
| S10 | Sundial: an owner listening pass over the 8 voice takes | Takes were chosen by duration and loudness only (CAST.md:25-27). The owner listens once with a note per line id. Row SS4. |
| S11 | Sundial: a read budget for ink words in degrees | State a minimum glyph height in degrees at the 0.55 m placement and a words-per-line cap; assert it in an EditMode test. Row SB5. |
| S12 | Sundial: a session record template for R1, R2 and U2 | One page: build sha, save file, Player.log, step times, observations kept apart from proposed fixes. Used from R1 on 10-09. Row PS1. |
| S13 | Sundial: the Quest placement keeps the 55 cm reach | The PC scene moves the dial to 0.26 m to fill the frame. Frame the PC view by field of view instead, and keep 0.55 m as the placement for the Quest provider. Row NC2. |
| S14 | Sundial: a boil toggle in Settings | The plan's boil on/off setting has no tab; only reduced motion stops the boil. Add a "Boil" tab that writes the saved flag. Row NC6. |
