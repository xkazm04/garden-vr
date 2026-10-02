---
id: T-TER-009
app: terrarium
title: Add up to three life habits as glowing companion plants, checked in by poke or look-and-pinch with Today / Yesterday and a 6-second undo
depends: [T-TER-008]
estimate_min: 120
touches: [shared/packages/com.gardenvr.core/Runtime/Terrarium/**, shared/core-dotnet/GardenVR.Core.Tests/Terrarium*.cs, tools/blender/terrarium_companions.py, apps/terrarium/Art/Source/**, apps/terrarium/Assets/Art/**, apps/terrarium/Assets/Scripts/**, apps/terrarium/Assets/Tests/**, orchestration/runs/terrarium/T-TER-009/**]
---
## Goal
The habit half of the app: seed packets for 6 presets (Walk, Water, Read, Stretch, Journal, Early night), up to three
chosen, each a small glowing companion in the moss that grows one leaf per kept day; labels at the desk edge with
Today and Yesterday targets (>= 3 cm) checked in by Poke (F) or Look + Pinch; a 6 s deferred-write undo that cannot
fail and flushes on pause.

## Read first
- `docs/plans/terrarium.md` sections 2 (item 6), 3 (normal evening, missed day), 5 (`Companions`); section 4 (Poke and
  Pinch bindings, target ids `seed.<preset>`, `label.<preset>.today`, `label.<preset>.yesterday`).
- Common `Ledger` (`Tend`, `Undo`, backfill semantics) in `shared/packages/com.gardenvr.core/Runtime/Common/README.md`.
- `docs/audio/AUDIO-BIBLE.md` 3.1 (`seed.appear`, `habit.pluck`, `habit.undo`: call the cue ids even if the audio
  service is not in yet; a missing cue must be harmless).

## Steps
0. `git merge --no-edit main`. Create `orchestration/runs/terrarium/T-TER-009/`.
1. Core `Runtime/Terrarium/Companions.cs` as in the plan (`MaxHabits = 3`, `Leaves`, `Vitality` with the same curve and
   floor as `Garden`); tests: leaves rise one per kept day; a second check-in the same day adds nothing; yesterday dates to
   yesterday only before today's boundary; undo inside 6 s restores the exact prior state; leaves never decrease over 1,000
   random lives except a same-day undo.
2. Art: `tools/blender/terrarium_companions.py` one sprig card mesh (under 600 tris) + 3 generated sprig drawings
   (glowing tips, `image_gen`, kept with prompts); tints per species from the bible (mint, moon-white `#DDF3FF`, pale
   gold). Each leaf added is a small card on the sprig.
3. Seed packets: after the first ritual's answer (a hook T-TER-012 will call; for now a dev key and a test), 6 packets
   glow along the desk's near edge; Poke or Look + Pinch plants it (max 3; a fourth shows nothing new); habits stored as
   Common `HabitDef` in the save.
4. Labels: per habit a small etched label with two targets, Today and Yesterday; Yesterday is shown only while a backfill
   is allowed; a check-in plays `habit.pluck` (pitch by size) and grows a leaf at once; the undo mark sits at the label
   for 6 s (deferred write: the ledger commits at expiry, on `SystemPause`, on PalmOpen and on quit).
5. PlayMode tests: `Habits_PlantThree`, `Checkin_Today_GrowsLeaf`, `Checkin_Yesterday_DatedYesterday`,
   `Undo_InsideWindow_Restores`, `Undo_FlushOnPause_Commits`, `Habits_FourthIgnored`.
6. Captures at `JarG1` and `SeatedPOV`: `habits-day1.png`, `habits-day7.png` (injected clock).
7. Commit per sub-step.

## Acceptance (each check runnable on Windows without a headset; say the command)
```bash
U="C:/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Unity.exe"; R=orchestration/runs/terrarium/T-TER-009
dotnet test shared/core-dotnet 2>&1 | tail -1                     # Passed, Failed: 0
"$U" -batchmode -projectPath apps/terrarium -runTests -testPlatform PlayMode -testResults "$(pwd)/$R/playmode.xml" -logFile "$R/playmode.log"; grep -o '<test-run[^>]*>' "$R/playmode.xml"   # failed="0", 6 new tests present
cat "$R/habits-day7.png.check.json"                               # magentaFrac < 0.001
"$U" -batchmode -quit -projectPath apps/terrarium -executeMethod GardenVR.Capture.Editor.CaptureCli.Measure -scene Assets/Scenes/Main.unity -target jar -state "companions=3,leaves=7" -out "$(pwd)/$R/measure.json" -logFile "$R/measure.log"; cat "$R/measure.json"   # scene drawsEst <= 40 (hard), tris <= 60000
```

## Evidence to leave (paths under orchestration/runs/terrarium/T-TER-009/)
`REPORT.md`, `playmode.xml`, `habits-day1.png`, `habits-day7.png`, `measure.json`, sprig drawings contact sheet.

## Out of scope
Free-text habits, more than 3 habits, notifications, audio generation (T-TER-010), the first-run choreography (T-TER-012).
