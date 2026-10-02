---
id: T-TER-003
app: terrarium
title: Move the breath and garden rules into their owned folders, apply the flower rule D5 and add the missing invariant tests
depends: []
estimate_min: 75
touches: [shared/packages/com.gardenvr.core/Runtime/Breath/**, shared/packages/com.gardenvr.core/Runtime/Terrarium/**, shared/packages/com.gardenvr.core/Runtime/*.cs, shared/packages/com.gardenvr.core/Runtime/*.cs.meta, shared/core-dotnet/GardenVR.Core.Tests/CoreTests.cs, shared/core-dotnet/GardenVR.Core.Tests/Terrarium*.cs, orchestration/runs/terrarium/T-TER-003/**]
---
## Goal
The seeded spike core becomes the Terrarium-owned part of `com.gardenvr.core`: breath rules in `Runtime/Breath/`,
garden rules in `Runtime/Terrarium/`, the flower rule changed to "first at 6, then every 6" (`docs/PLAN.md` D5), and the
invariants in `docs/plans/terrarium.md` section 5 each covered by a test, including a negative control. The sundial
agent owns `Runtime/Common/` and `Runtime/Sundial/`; do not create or edit those.

## Read first
- `docs/plans/terrarium.md` section 5 (the data model and the invariant list); `docs/PLAN.md` D5.
- `shared/packages/com.gardenvr.core/Runtime/{BreathSession,PinchDetector,SimulatedHand,Garden}.cs` and
  `shared/core-dotnet/GardenVR.Core.Tests/CoreTests.cs` (16 tests from the round-2 spike).
- The spike's evidence for the rules: `C:\Users\kazda\kiro\personas\.contest\arena\habit-garden-r2\entries\claude-claude-opus-5-5_high-v1\variant-1\index.html` section 01.

## Steps
0. `git merge --no-edit main`. Create `orchestration/runs/terrarium/T-TER-003/`. Run `dotnet test shared/core-dotnet`
   first and save the baseline output (`baseline.txt`).
1. `git mv` `BreathSession.cs`, `PinchDetector.cs`, `SimulatedHand.cs` (and their `.meta` files) into
   `Runtime/Breath/`, and `Garden.cs` (+ `.meta`) into `Runtime/Terrarium/`. Keep the namespace `GardenVR.Core` (no
   consumer breaks). Create folder `.meta` files by copying the pattern of `Runtime.meta` with fresh GUIDs.
2. Apply D5 in `Garden`: `FirstFlowerAt = 6`, `FlowerEvery = 6`, `Flowers` as in the plan, `GrowthAnswer.Flower` true when
   the new frond count is 6, 12, 18 ...; keep every other rule unchanged.
3. Add `Runtime/Terrarium/TerrariumState.cs`: a pure snapshot `{phase, breaths, uncoil, fog, fronds, flowers, dewToday,
   vitality, rituals}` with `ToJson()` (hand-written, invariant culture). It is the state oracle tests and, later, the
   Operator tool return.
4. Rename `CoreTests.cs` to `TerrariumCoreTests.cs` (`git mv`) and update the "every seventh frond" test to D5. Add tests:
   first flower at 6 and second at 12; a 7-day week with one missed day flowers on day 7; vitality floor reached after a
   long gap and restored by one ritual; a `TerrariumState.ToJson()` golden string; a property test over 1,000 random lives
   (seeded) that fronds never decrease; a **negative control**: the same property checker run against a deliberately
   broken test double (a garden that drops a frond on a long gap) must report a violation; `BreathConfig` with
   `TargetBreaths = 3` and `MinInhaleSeconds = 1.5` completes in three calm breaths (Sundial uses this).
5. Commit: moves, D5 change, state oracle, tests.

## Acceptance (each check runnable on Windows without a headset; say the command)
```bash
R=orchestration/runs/terrarium/T-TER-003
dotnet test shared/core-dotnet 2>&1 | tee "$R/dotnet-test.txt" | tail -3     # Passed! Failed: 0, Total >= 22
ls shared/packages/com.gardenvr.core/Runtime/Breath shared/packages/com.gardenvr.core/Runtime/Terrarium   # the moved files + .meta
git log --follow --oneline -- shared/packages/com.gardenvr.core/Runtime/Terrarium/Garden.cs | tail -1    # history kept
grep -rn "UnityEngine" shared/packages/com.gardenvr.core/Runtime                                          # nothing
U="C:/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Unity.exe"
"$U" -batchmode -nographics -quit -projectPath apps/terrarium -logFile "$R/compile.log"; grep -c "error CS" "$R/compile.log"   # 0
```

## Evidence to leave (paths under orchestration/runs/terrarium/T-TER-003/)
`REPORT.md` (list each new test and the invariant it guards), `baseline.txt`, `dotnet-test.txt`, `compile.log`.

## Out of scope
`Runtime/Common/` and `Runtime/Sundial/` (sundial agent), persistence format changes (T-TER-008), the `.csproj` files.
