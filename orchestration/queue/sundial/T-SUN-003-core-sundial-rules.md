---
id: T-SUN-003
app: sundial
title: Port the sundial rules to C# - arcs, the 7-tile record, monotone plant growth, bloom from the week, backfill and the shadow clock
depends: [T-SUN-002]
estimate_min: 90
touches: [shared/packages/com.gardenvr.core/Runtime/Sundial/**, shared/core-dotnet/GardenVR.Core.Tests/Sundial*.cs, orchestration/runs/sundial/T-SUN-003/**]
---
## Goal
`Runtime/Sundial/` decides everything the dial shows: which arc the clock is in, each plant's 7 tiles, its stage from
lifetime kept days (only rises), its bloom from the last 7 days (PLAN D2), whether it is due now, whether yesterday may
be logged, and the gnomon angle. Pure C#, tested, with a state oracle.

## Read first
- `docs/plans/sundial.md` sections 1 (what changed: D2, D4, D7) and 5 (the model and invariants are the spec).
- `C:\Users\kazda\kiro\personas\.contest\arena\habit-garden-r2\entries\claude-claude-opus-5-5_high-v2\spike\src\core\rules.ts`
  and `model.ts` (round-1 rule 1-5; note D2 changes rule 1).
- `shared/packages/com.gardenvr.core/Runtime/Common/README.md` (T-SUN-002).

## Steps
0. `git merge --no-edit main`. Create `orchestration/runs/sundial/T-SUN-003/`.
1. `ArcId`, `ArcDef` with defaults 360-660, 660-1080, 1080-1620 (wind-down runs to 03:00 the next calendar day);
   `ArcAt(nowMin)` handles minutes after midnight before the boundary as wind-down.
2. `TileState { Kept, Late, Missed, Today, Before }` (closed), `Stage { Seed, Sprout, Young, Leafy, Full }` from
   `LifetimeKept` 0, 1-2, 3-6, 7-13, 14+; `Bloom { None, Bud, Open }` from `WindowKept` < 3, 3-4, >= 5.
3. `SundialRules.Plant(h, ledger, today, nowMin)` -> `PlantState` (window oldest first, today last; `Before` for days
   before `CreatedDay`; `Today` while today is open; `DueNow` only inside the arc and while today is not kept;
   `CanBackfillYesterday`); `GnomonAngleDeg(nowMin)` = 15 deg per hour from 06:00 (wrapping); `Backfill` delegating to
   the Common ledger.
4. `SundialState` oracle: `{now, arc, gnomonDeg, plants:[{habit, window, windowKept, lifetimeKept, stage, bloom, dueNow,
   canBackfill}]}` with `ToJson()`.
5. Tests `SundialTests.cs` (>= 15): arc boundaries at 05:59, 06:00, 10:59, 11:00, 17:59, 18:00, 02:59 (wind-down), 03:00
   (new day, before morning); the window across a missed day (subtracts nothing); a kept day ageing out lowers
   `WindowKept` and can close a bloom but never lowers `Stage`; property test over 1,000 seeded random lives:
   `LifetimeKept` and `Stage` never decrease; negative control (a broken double that derives stage from the window must be
   flagged by the property checker); late tiles count as kept for `WindowKept` and render `Late`; `DueNow` false after the
   arc and after keeping; gnomon angle at 14:20 = 125 deg; a habit created today shows six `Before` tiles; `SundialState`
   golden JSON.
6. Commit per sub-step.

## Acceptance (each check runnable on Windows without a headset; say the command)
```bash
R=orchestration/runs/sundial/T-SUN-003
dotnet test shared/core-dotnet 2>&1 | tee "$R/dotnet-test.txt" | tail -3     # Passed, Failed: 0; >= 15 new Sundial tests
dotnet test shared/core-dotnet --filter "FullyQualifiedName~Sundial" 2>&1 | tail -3   # the Sundial suite alone passes (not 0 tests)
grep -rn "UnityEngine" shared/packages/com.gardenvr.core/Runtime/Sundial   # nothing
```

## Evidence to leave (paths under orchestration/runs/sundial/T-SUN-003/)
`REPORT.md` (rule -> test table; where D2 differs from round 1 and how the tests show it), `dotnet-test.txt`.

## Out of scope
Rendering, input, persistence wiring (T-SUN-008), the dusk ritual (uses `Runtime/Breath/`, T-SUN-009).
