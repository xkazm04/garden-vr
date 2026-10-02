---
id: T-TER-012
app: terrarium
title: Build the first-run journey - room fade, jar arrival, ghost hand and etched words, seeds after the first answer - and prove it lands in under 3 minutes
depends: [T-TER-009, T-TER-011]
estimate_min: 120
touches: [apps/terrarium/Art/Source/**, apps/terrarium/Assets/Art/**, apps/terrarium/Assets/Scripts/**, apps/terrarium/Assets/Tests/**, apps/terrarium/Assets/Scenes/**, orchestration/runs/terrarium/T-TER-012/**]
---
## Goal
A first-time user, seated, with no instructions, goes from launch to the garden's answer and the seed packets inside
three minutes (gate U1), taught only by the jar: the room fades up, the jar arrives with a tink, a ghost hand and etched
words teach the hold, the words fade after the second breath, seeds come only after the answer, the voice guide is
offered then (D6). Tour steps advance on real intents, never on timers, and resume after a quit.

## Read first
- `docs/plans/terrarium.md` section 3 (the second-by-second table is the spec) and section 4 (`BindingHint`).
- `docs/art/terrarium-style.md` sections 1 (`etch.text`, contrast) and 4 (jar arrival, reduced motion).
- `docs/knowledge/registry-consult.md` (guided-tours and async-ui-states rows: action-driven advancement, a failed load is
  not a first run).

## Steps
0. `git merge --no-edit main`. Create `orchestration/runs/terrarium/T-TER-012/`.
1. `FirstRunDirector` with step ids `tour.room`, `tour.desk`, `tour.arrive`, `tour.hold`, `tour.release`, `tour.breathe`,
   `tour.answer`, `tour.seeds`, `tour.voice`, `tour.done`; progress saved in `TerrariumSave.FirstRunStep`; a relaunch
   resumes at the step (the jar is already placed if `tour.arrive` passed). It runs only when the load outcome is `Fresh`.
2. Timeline per the plan: room plate fade 0-2 s with `amb.room`; desk trace at 2.5 s; jar drop (8 cm, 0.6 s eased) and
   `jar.land` at 4 s; cork puff 6 s; etched text at 8 s on the glass (TextMeshPro, `etch.text` colour, contrast >= 4.5:1
   checked by an EditMode test that samples the rendered text against its background in a capture), with the binding
   from `IHandIntentSource.BindingHint(PinchHold)`.
3. Ghost hand: two generated translucent mint hand silhouettes (open, pinch; `image_gen`, kept with prompts) on a card
   beside the jar, cycling every 1.5 s; it leaves after the first counted breath (on the event, not a timer).
4. Words: "Hold to breathe in." then "and let go." on the first release; both fade for good after breath 2 and return only
   after 8 s idle while the ritual waits.
5. After the answer: 4 s of quiet, then seed packets (T-TER-009 hook) and the voice shell offer (T-TER-011). Reduced
   motion: everything appears in place; breath timing unchanged.
6. PlayMode tests: `FirstRun_SixBreaths` (fresh temp save path; script `first-run.jsonl` waits for the words, then six
   breaths, then pokes two seeds): answer <= 180 s app time, seeds planted, `FirstRunStep == tour.done`;
   `FirstRun_ResumeAfterQuit` (quit during breath 3, relaunch: no arrival animation again, ritual offered back);
   `FirstRun_NotOnFailedLoad` (corrupt save: restore prompt, no tour); `FirstRun_IdleHintReturns` (8 s idle);
   `FirstRun_ReducedMotion_EndStates`.
7. Record the canonical first run at `SeatedPOV`, 24 fps, 0-100 s, to `first-run.mp4`.
8. Build a Windows player for the owner: `GardenVR.Terrarium.Editor.Build.Windows` -> `Builds/terrarium/Terrarium.exe`
   (git-ignored); time 3 cold starts to the first interactive frame from the player log (gate S5).
9. Commit per sub-step.

## Acceptance (each check runnable on Windows without a headset; say the command)
```bash
U="C:/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Unity.exe"; R=orchestration/runs/terrarium/T-TER-012
"$U" -batchmode -projectPath apps/terrarium -runTests -testPlatform PlayMode -testResults "$(pwd)/$R/playmode.xml" -logFile "$R/playmode.log"
grep -o '<test-run[^>]*>' "$R/playmode.xml"                                         # failed="0", 5 FirstRun_* present
grep -o 'name="FirstRun_SixBreaths"[^>]*' "$R/playmode.xml"                          # Passed
grep -E "answerAt=" "$R/playmode.log" | tail -1                                     # answerAt <= 180 s
ffprobe -v error -show_entries format=duration -of csv=p=0 "$R/first-run.mp4"       # >= 90
"$U" -batchmode -quit -projectPath apps/terrarium -executeMethod GardenVR.Terrarium.Editor.Build.Windows -logFile "$R/build.log"; grep -E "Build (Succeeded|Failed)" "$R/build.log"
cat "$R/cold-start.txt"                                                              # 3 runs, median <= 4 s
```

## Evidence to leave (paths under orchestration/runs/terrarium/T-TER-012/)
`REPORT.md` (the timeline as measured: each step id with its time), `playmode.xml`, `first-run.mp4`, `cold-start.txt`,
`ghost-hand.png`, `etched-text.png` with the measured contrast.

## Out of scope
The day-7 look-back, the settings pebble, Quest placement; publishing the build anywhere.
