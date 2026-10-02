---
id: T-SUN-002
app: sundial
title: Build the shared core - garden day with a 03:00 boundary, injected clock, habit ledger with undo and backfill, and a safe versioned save store
depends: []
estimate_min: 120
touches: [shared/packages/com.gardenvr.core/Runtime/Common/**, shared/core-dotnet/GardenVR.Core.Tests/Common*.cs, orchestration/runs/sundial/T-SUN-002/**]
---
## Goal
`com.gardenvr.core/Runtime/Common/` holds the engine-free primitives both apps build on: `GardenDay`, `IClock`,
`HabitDef`, `TendEvent`, `Ledger`, a dependency-free JSON reader/writer and `SaveStore` with atomic replace, rotated
backups and schema migrations. You own `Runtime/Common/` and `Runtime/Sundial/`; Terrarium consumes Common from
T-TER-008 on, so the API in `docs/plans/sundial.md` section 5 is binding (additions are fine; renames are not).

## Read first
- `docs/plans/sundial.md` section 5 (the data model and invariants); `docs/PLAN.md` D3 (03:00 boundary).
- The round-1 rules and the IWSDK core you are porting from:
  `C:\Users\kazda\kiro\personas\.contest\arena\habit-garden-r2\entries\claude-claude-opus-5-5_high-v2\spike\src\core\{model,ledger,days,rules}.ts`.
- `docs/knowledge/registry-consult.md` (settings, embedded-db, migrations, undo-history rows).
- `shared/core-dotnet/` (netstandard2.1 library + net9 xUnit tests; `dotnet test shared/core-dotnet`).

## Steps
0. `git merge --no-edit main`. Create `orchestration/runs/sundial/T-SUN-002/`. Run `dotnet test shared/core-dotnet` and
   save `baseline.txt`.
1. `GardenDay` (index = days since 2000-01-01 of the garden day; `From(DateTimeOffset local, TimeSpan boundary)`;
   `EndsAt(TimeZoneInfo, TimeSpan)` DST-aware) and `IClock` (+ `FixedClock` and `SystemClock`). Do not depend on IANA ids
   resolving on Windows/Mono: tests build zones with `TimeZoneInfo.CreateCustomTimeZone` and explicit DST rules.
2. `HabitDef`, `TendSource`, `TendEvent`, `Ledger` (append-only list; `Tend` idempotent per habit-day; `Undo` only inside
   the window, marks `UndoneAtUtcMs`, never deletes; `Backfill` only yesterday, only once, only before today's boundary,
   `Late = true`, real timestamp; `IsKept`, `KeptDays`, `KeptDaysInWindow(habit, today, 7)`); a `DeferredTend` helper
   that holds a tend for 6 s and commits on expiry, on `Flush()` (pause / teardown) and never fails on undo.
3. `Json` (minimal reader/writer: objects, arrays, strings with escapes, numbers invariant culture, bools, null; unknown
   members kept in a passthrough dictionary and written back).
4. `SaveStore<T>`: `Load()` -> `LoadResult { Outcome: Fresh | Loaded | Migrated | Failed, Doc, FailedStep, BackupAvailable }`;
   `Save(doc)` writes `save.json.tmp`, flushes, rotates `save.json` -> `save.prev1.json` -> `save.prev2.json`, then
   replaces; migrations are an append-only list of steps keyed by version, a snapshot copy is written before the first
   pending step; a file with a newer `SchemaVersion` loads read-only (`Save` refuses); `Failed` never returns `Fresh`.
5. Settings registry helper: a closed list of keys with typed accessors; values equal to defaults are not written.
6. Tests `CommonTests.cs` (xUnit, >= 20): 01:30 counts for the previous day; 03:00 exactly starts the new day; DST spring
   and autumn weeks (custom zone); a time-zone change mid-week; a clock set back never creates a future day; tend
   idempotent; undo at 5.9 s works, at 6.1 s refused; `DeferredTend.Flush` commits; backfill yesterday once, refused the
   second time, refused for two days ago, refused after the boundary, marked late; `KeptDaysInWindow`; JSON round trip with
   unknown members; truncated file -> `Failed` with `BackupAvailable`; newer schema read-only; migration snapshot written;
   defaults not written; a negative control (a deliberately broken store that writes in place is detected by the
   "never a half-written file" test using an injected failing stream).
7. A short `Runtime/Common/README.md` (API, invariants, how to add a migration step).
8. Commit per sub-step.

## Acceptance (each check runnable on Windows without a headset; say the command)
```bash
R=orchestration/runs/sundial/T-SUN-002
dotnet test shared/core-dotnet 2>&1 | tee "$R/dotnet-test.txt" | tail -3        # Passed, Failed: 0, total >= baseline + 20
grep -rn "UnityEngine\|System.Text.Json\|Newtonsoft" shared/packages/com.gardenvr.core/Runtime/Common   # nothing
U="C:/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Unity.exe"
"$U" -batchmode -nographics -quit -projectPath apps/sundial -logFile "$R/compile.log"; grep -c "error CS" "$R/compile.log"   # 0
```

## Evidence to leave (paths under orchestration/runs/sundial/T-SUN-002/)
`REPORT.md` (each test name -> the invariant it guards), `baseline.txt`, `dotnet-test.txt`, `compile.log`.

## Out of scope
Sundial-specific rules (T-SUN-003), anything in `Runtime/Breath/` or `Runtime/Terrarium/`, the `.csproj` files, Unity code.
