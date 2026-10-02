---
id: T-SUN-008
app: sundial
title: Wire glance-and-pinch tending end to end - look halo, pinch tend with deferred undo, tiles, waiting glow and the shadow clock - with persistence and playback tests
depends: [T-SUN-003, T-SUN-005, T-TER-001]
estimate_min: 120
touches: [apps/sundial/Assets/Scripts/**, apps/sundial/Assets/Tests/**, apps/sundial/Assets/Scenes/**, orchestration/runs/sundial/T-SUN-008/**]
---
## Goal
The Sundial vertical slice (M1): look at a plant (PC: hover, Tab) and an inked halo answers; pinch (click, Enter) and the
plant tends with a bloom pulse, today's tile fills with ink and an undo mark sits at the tile for 6 s; the gnomon's
shadow follows the clock; the due plant breathes a waiting glow; everything persists through the Common save store.
PlayMode tests drive it with scripted intents by target id.

## Read first
- `docs/plans/sundial.md` sections 2 (items 2-3, 6, 12), 3 (normal day), 4 (bindings and target ids), 5 (rules).
- `docs/art/sundial-style.md` section 4 (motion timings: halo dwell, bloom pulse 700 ms, waiting glow 2.6 s, tile fill
  300 ms).
- `shared/packages/com.gardenvr.input/README.md` (Look dwell, Tab focus, JSONL playback);
  `shared/packages/com.gardenvr.core/Runtime/Common/README.md` (`DeferredTend`, `SaveStore`).

## Steps
0. `git merge --no-edit main`. Create `orchestration/runs/sundial/T-SUN-008/`.
1. `SundialService`: holds `SundialSave` (habits, tends, settings) via `SaveStore` at
   `Application.persistentDataPath/sundial/save.json` (tests: a fresh temp path); an `IClock` (system; tests and dev keys
   inject); computes `SundialState` every frame it changes. Until the first-run task lands, a dev seed creates one habit
   per arc (`water`, `top3`, `breaths`).
2. `SundialController`: on `LookTarget` change to a plant -> `DialView.halo` on that plant (dwell is the provider's 150 ms);
   on `Pinch` at a plant -> `DeferredTend` (visual immediately: `tend.tock` cue id, bloom pulse, tile ink fill), commit
   after 6 s, on `PalmOpen`, `SystemPause` and quit; `Pinch` on the undo mark (`undo.<arc>`) inside 6 s cancels with
   `tend.undo`. A second tend the same day does nothing visible except the halo.
3. Gnomon: `DialView.gnomonDeg = GnomonAngleDeg(nowMin)`; on scene load, sweep from 06:00 to now in 1.5 s (reduced motion:
   in place). Waiting glow on `DueNow` plants (2.6 s opacity breath).
4. Development keys through the provider's dev commands: `]` / `[` day, `T` clock x60, `F1` state overlay with
   `SundialState.ToJson()`.
5. PlayMode tests in `Assets/Tests/PlayMode/` (new asmdef `GardenVR.Sundial.Tests.PlayMode`; fixed step,
   `ScriptedIntentSource`, injected clock 14:20):
   `Tend_LookPinch_ByPlayback` (look `plant.midday` 0.3 s, pinch; after 6.5 s the ledger has 1 live event and the midday
   window's last tile is `Kept`); `Tend_Undo_InsideWindow` (pinch, then pinch `undo.midday` at 3 s: no live event);
   `Tend_FlushOnPause` (pinch, PalmOpen at 1 s: committed); `Tend_SecondSameDay_NoOp`; `Gnomon_At1420_Is125`;
   `Halo_OnlyAfterDwell` (a 0.1 s pass over a plant shows no halo); `Save_SurvivesRelaunch`; `Response_TwoFrames` (pinch to
   first visual change <= 2 frames).
6. Captures at `DialG1`: `tend-before.png`, `tend-after.png` (clock 14:20), and a 4 s sequence of a tend (`tend.gif`).
7. Commit per sub-step.

## Acceptance (each check runnable on Windows without a headset; say the command)
```bash
U="C:/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Unity.exe"; R=orchestration/runs/sundial/T-SUN-008
"$U" -batchmode -projectPath apps/sundial -runTests -testPlatform PlayMode -testResults "$(pwd)/$R/playmode.xml" -logFile "$R/playmode.log"
grep -o '<test-run[^>]*>' "$R/playmode.xml"                                       # total >= 8, failed="0"
grep -o 'name="Tend_LookPinch_ByPlayback"[^>]*result="[A-Za-z]*"' "$R/playmode.xml"   # Passed
grep -rnE "Keyboard\.current|Mouse\.current|Input\.Get" apps/sundial/Assets --include=*.cs   # nothing (S1)
dotnet test shared/core-dotnet 2>&1 | tail -1                                       # green
cat "$R/tend-after.png.check.json"                                                  # magentaFrac < 0.001
```

## Evidence to leave (paths under orchestration/runs/sundial/T-SUN-008/)
`REPORT.md` (state JSON before and after a tend), `playmode.xml`, `tend-before.png`, `tend-after.png`, `tend.gif`.

## Out of scope
Backfill and tile art states beyond kept/today (T-SUN-010), the dusk ritual (T-SUN-009), first run (T-SUN-011), audio
generation (cue ids are called; missing clips are harmless).
