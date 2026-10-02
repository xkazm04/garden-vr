---
id: T-TER-008
app: terrarium
title: Persist the garden across launches on the shared day clock, render vitality and accumulated fronds, and prove the missed-day and day-7 journeys by playback
depends: [T-TER-005, T-SUN-002]
estimate_min: 120
touches: [shared/packages/com.gardenvr.core/Runtime/Terrarium/**, shared/core-dotnet/GardenVR.Core.Tests/Terrarium*.cs, apps/terrarium/Assets/Scripts/**, apps/terrarium/Assets/Tests/**, orchestration/runs/terrarium/T-TER-008/**]
---
## Goal
The jar remembers. A save document with a schema version, atomic replace and a rotated backup (through the Common
store, owned by sundial); the garden day from the shared `GardenDay` with the 03:00 boundary; fronds accumulating on
the moss, vitality as glow and a small lean, flowers from D5, dew for extra sessions; and the journeys "missed day then
recovered" and "a week flowers" proven by playback with an injected clock.

## Read first
- `docs/plans/terrarium.md` sections 3 (normal evening, missed day, day 7) and 5 (`TerrariumSave`, invariants);
  `docs/art/terrarium-style.md` section 4 (missed-day values).
- `shared/packages/com.gardenvr.core/Runtime/Common/` and its README (T-SUN-002: `GardenDay`, `IClock`, `ISaveStore`,
  `LoadOutcome`, mini JSON).
- `docs/PLAN.md` D3 (03:00) and D5.

## Steps
0. `git merge --no-edit main`. Create `orchestration/runs/terrarium/T-TER-008/`.
1. Core (`Runtime/Terrarium/`): `TerrariumSave` (as in the plan) with `ToJson`/`FromJson` via the Common JSON;
   `TerrariumSaveMigrations` (v1 only now, append-only list); `Garden.FromSave` / `ToSave`. Tests in
   `TerrariumSaveTests.cs`: round trip; unknown members preserved; a newer `SchemaVersion` opens read-only (no write);
   default settings are not written.
2. App: `GardenService` owns the `Garden`, an `IClock` (system clock; tests inject a fixed clock), and the store at
   `Application.persistentDataPath/terrarium/save.json` (tests use a fresh temp path per test). Load outcome `Failed`
   shows a quiet "restore the last copy?" prompt (target id `prompt.restore`) and never starts the first run.
   Save after every state change that matters (ritual complete, settings) and on `SystemPause`.
3. Render the garden: fronds placed by `FrondDays` index on deterministic slots around the moss (inner layer after 12);
   each frond's emission x vitality; lean <= 6 deg at the floor; the waiting fiddlehead dim and not pulsing on a gap;
   flowers per `Flowers`; dew beads for `DewToday`. On `GrowthAnswer.Recovered` the moss ripple runs brighter and longer
   (2.5 s) from the new frond outward.
4. Development time travel: `]` / `[` move the injected clock by one day (refused by the core if it would rewrite history;
   show nothing in release builds).
5. PlayMode tests (fixed step, `ScriptedIntentSource`, injected clock):
   `Days_MissedThenRecovered` (ritual day 1, skip 2 days, vitality 0.70 then a ritual -> 1.0, `Recovered` true, fronds 2);
   `Days_WeekFlowers` (rituals on 6 of 7 days -> one flower on day 7); `Save_SurvivesRelaunch` (unload and reload the scene:
   same fronds); `Save_CorruptOffersBackup` (truncate the save to 0 bytes -> restore prompt, first run not started);
   `Day_BoundaryAt0300` (a ritual at 02:30 counts for the previous day).
6. Captures at `JarG1` with the injected clock: `day1.png`, `missed2.png` (two days missed), `recovered.png`, `day7.png`.
7. Commit per sub-step.

## Acceptance (each check runnable on Windows without a headset; say the command)
```bash
U="C:/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Unity.exe"; R=orchestration/runs/terrarium/T-TER-008
dotnet test shared/core-dotnet 2>&1 | tail -1                     # Passed, Failed: 0
"$U" -batchmode -projectPath apps/terrarium -runTests -testPlatform PlayMode -testResults "$(pwd)/$R/playmode.xml" -logFile "$R/playmode.log"
grep -o '<test-run[^>]*>' "$R/playmode.xml"                       # failed="0"; includes the 5 new tests
for f in day1 missed2 recovered day7; do cat "$R/$f.png.check.json"; done   # all exist, magentaFrac < 0.001
```

## Evidence to leave (paths under orchestration/runs/terrarium/T-TER-008/)
`REPORT.md`, `playmode.xml`, the four captures and a 2x2 contact sheet `days.png`, a sample `save.json` from a test run.

## Out of scope
Life habits (T-TER-009); the look-back replay; changes inside `Runtime/Common/` (request them via `REQUEST-core.md`).
