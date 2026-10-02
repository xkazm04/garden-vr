---
id: T-SUN-010
app: sundial
title: Draw the honest record - five tile states on raised tiles, the once-only late log for yesterday, and the missed-day and day-7 journeys
depends: [T-SUN-008]
estimate_min: 90
touches: [apps/sundial/Assets/Scripts/**, apps/sundial/Assets/Tests/**, apps/sundial/Assets/Art/**, shared/packages/com.gardenvr.fx/Runtime/Shaders/FToon.shader, orchestration/runs/sundial/T-SUN-010/**]
---
## Goal
The 7-tile row under each plant tells the week truthfully: kept (ink fill in the arc's wash), late (pencil hatch, forever),
missed (pale paper, quiet), today (dashed outline), before (blank). Yesterday's pale tile carries a small ink "?" until
today's boundary; pinch or poke it, confirm "Yes, it happened", and it becomes hatched. A missed day never shrinks a
plant (D2); a week under five kept days closes the bloom to a bud. The missed-day and day-7 journeys are proven by
playback with an injected clock.

## Read first
- `docs/plans/sundial.md` sections 3 (missed day, day 7) and 5 (tile vocabulary, backfill invariants);
  `docs/art/sundial-style.md` section 1 (tile colours) and 7 (disqualifiers).
- `docs/knowledge/registry-consult.md` (status-vocabulary: one closed enum, labels from a catalog, shape as well as colour).

## Steps
0. `git merge --no-edit main`. Create `orchestration/runs/sundial/T-SUN-010/`.
1. Tile instancing: per-instance state (0-4) drives fill / hatch / pale / dashed / blank in the tile shader (shape as well
   as colour: hatch lines, dashed outline), still one draw for 21 tiles; ink-fill animation 300 ms from the nib side
   (reduced motion: instant).
2. `TileLabels` catalog (`Assets/Scripts/Dial/TileLabels.cs`): one label per `TileState` for the dev overlay and future
   accessibility text; an unknown state falls back to neutral and logs once.
3. Backfill: when `CanBackfillYesterday`, an ink "?" on yesterday's tile (`tile.<arc>.yesterday.ask`); Poke or Look +
   Pinch opens a small paper chip "Did it happen?" with "Yes, it happened" (`prompt.backfill.yes`) and "Not this time"
   (`prompt.backfill.no`, closes, records nothing); yes -> `SundialRules.Backfill`, `tile.hatch` cue id, hatched tile.
4. Bloom closing: when `Bloom` drops from Open to Bud, the bloom overlay closes over 1.2 s like evening (reduced motion:
   swap); `Stage` cards never step back.
5. PlayMode tests (injected clock): `Backfill_Once_Late` (yes once -> `Late`; the "?" disappears; a second attempt is not
   offered); `Backfill_NotAfterBoundary`; `Missed_NoShrink` (miss 3 days: `LifetimeKept` and the stage card unchanged);
   `Week_BloomOpensAt5_ClosesBelow`; `Day7_TilesReadOldestFirst` (window order on the dial matches the rules).
6. Captures at `DialG1` with the injected clock: `missed.png` (a pale tile, plant unchanged), `backfilled.png`,
   `day7.png` (a mostly kept week, one pale tile, dusk plant in bloom), and a contact sheet `record.png`.
7. Commit per sub-step.

## Acceptance (each check runnable on Windows without a headset; say the command)
```bash
U="C:/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Unity.exe"; R=orchestration/runs/sundial/T-SUN-010
"$U" -batchmode -projectPath apps/sundial -runTests -testPlatform PlayMode -testResults "$(pwd)/$R/playmode.xml" -logFile "$R/playmode.log"; grep -o '<test-run[^>]*>' "$R/playmode.xml"   # failed="0", 5 new tests
for f in missed backfilled day7; do cat "$R/$f.png.check.json"; done          # all present, magentaFrac < 0.001
"$U" -batchmode -quit -projectPath apps/sundial -executeMethod GardenVR.Capture.Editor.CaptureCli.Measure -scene Assets/Scenes/Main.unity -target dial -out "$(pwd)/$R/measure.json" -logFile "$R/measure.log"; cat "$R/measure.json"   # drawsEst <= 30, tiles still one draw
grep -rniE "streak|failed|broke|lost" apps/sundial/Assets/Scripts --include=*.cs | grep -v "//" || echo "no shaming strings"
```

## Evidence to leave (paths under orchestration/runs/sundial/T-SUN-010/)
`REPORT.md`, `playmode.xml`, `missed.png`, `backfilled.png`, `day7.png`, `record.png`, `measure.json`.

## Out of scope
Week view, rim scrub, more than one habit per arc, notifications.
