Status: complete

# T-SUN-066: B9 monthly record

Completed 2026-10-12 in `C:/Users/kazda/kiro/gvr-sundial`, branch `agent/sundial`. All task acceptance checks pass: **547 .NET tests, 98 Sundial EditMode tests, 94 Sundial PlayMode tests**, the Windows player build, and the nine-plant record capture with its artboard comparison. The five named B9 tests pass, including U5. Terrarium also compiles against the shared-core changes.

## Changes

- The four onboarding zone tabs now belong to the dial's monthly record view. They appear with the first plant and use `ZoneRecord.Fill`, `GardenMonth.Of`, the existing ink-paper material, and the Field Notebook zone palette. Pencil strokes cover the core-provided share. A zone with no planned days has plain paper and no number. The tabs clear the Settings ear and are hidden on the week page.
- Plant look notes show `MonthlyRecord.KeptDays` beside the name and zone. Monthly totals update after tending, undo and month rollover. The gratitude invitation yields while a profile note is visible, as the focus invitation already did.
- A quantity plant or its current-day tile accepts a `Pinch` intent through the existing input source. `Quantity.Add` records each unit; partial progress uses the existing tile shader's ink-fill channel. Reaching the target keeps the day once; extra units do not create duplicate tends. Partial counts and the target tend save together. The latest quantity pinch owns the existing undo mark for six seconds, with `Quantity.Undo` reversing the count and, when applicable, its tend. Pause/quit retain already saved counts. Historical partial tiles retain their fill and count on look.
- Onboarding recognizes an immediately kept quantity day as well as a deferred ordinary tend. It does not finish on a partial count. The existing speech/privacy test now completes all eight water units, checks that the first unit is not a kept day, and retains its reload/privacy assertions.
- Core adds read-only `QuantityTally.CanUndo`, sharing the existing inclusive six-second rule with `Undo`. Reload continues the count event sequence, preventing duplicate event IDs at an unchanged clock instant. Both have regression tests. The shared changes are backward compatible: no member was removed, no save schema changed, and old count/tend rows remain readable. Approved surfaces were regenerated and reviewed.

Implementation commits: `59687e5` (core undo availability), `d52a4e1` (count ID reload fix), `fa2e6bc` (app, model and PlayMode tests). No push or history rewrite.

Read AGENTS.md, the read-only task, upgrade-step-b common section/B9, T-SUN-059's report, upgrade context, decisions 0015/0016, and the art/style references. The task file was not edited or moved. No direct keyboard/mouse reads, image generation, mesh generation, audio generation, credentials, or external services were needed.

## Verification commands and actual output

All long processes ran in the foreground. Each Unity invocation used `Start-Process -Wait -WindowStyle Hidden`, sequentially, and its log was read before the next Unity launch. XML counts establish test success. No Unity Editor remained running afterward; the pre-existing Unity Hub process was left alone.

```powershell
$env:DOTNET_ROLL_FORWARD='Major'
dotnet test shared/core-dotnet/GardenVR.sln --logger trx --results-directory orchestration/runs/sundial/T-SUN-066/dotnet-verified 2>&1 | Tee-Object orchestration/runs/sundial/T-SUN-066/dotnet-verified.log
```

Actual final output:

```text
Passed!  - Failed: 0, Passed: 14, Skipped: 0, Total: 14, Duration: 28 ms - GardenVR.TerrariumModel.Tests.dll (net9.0)
Passed!  - Failed: 0, Passed: 11, Skipped: 0, Total: 11, Duration: 65 ms - GardenVR.CoachCli.Tests.dll (net9.0)
Passed!  - Failed: 0, Passed: 60, Skipped: 0, Total: 60, Duration: 451 ms - GardenVR.SundialModel.Tests.dll (net9.0)
Passed!  - Failed: 0, Passed: 462, Skipped: 0, Total: 462, Duration: 900 ms - GardenVR.Core.Tests.dll (net9.0)
```

The four final TRX files are copied beside this report under their assembly names. `GARDENVR_APPROVE_SURFACE=1` was used only for the earlier filtered `PublicSurfaceTests` regeneration, then unset for the full suites.

```powershell
$unity='C:/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Unity.exe'
$repo='C:/Users/kazda/kiro/gvr-sundial'
$runDir="$repo/orchestration/runs/sundial/T-SUN-066"
Start-Process -Wait -WindowStyle Hidden -FilePath $unity -ArgumentList "-batchmode -nographics -projectPath $repo/apps/sundial -runTests -testPlatform EditMode -testResults $runDir/editmode.xml -logFile $runDir/editmode.log"
Start-Process -Wait -WindowStyle Hidden -FilePath $unity -ArgumentList "-batchmode -projectPath $repo/apps/sundial -runTests -testPlatform PlayMode -testResults $runDir/playmode.xml -logFile $runDir/playmode.log"
Start-Process -Wait -WindowStyle Hidden -FilePath $unity -ArgumentList "-batchmode -nographics -quit -projectPath $repo/apps/sundial -executeMethod GardenVR.Sundial.Editor.Build.Windows -logFile $runDir/build.log"
Start-Process -Wait -WindowStyle Hidden -FilePath $unity -ArgumentList "-batchmode -nographics -quit -projectPath $repo/apps/terrarium -logFile $runDir/terrarium-compile.log"
```

Two earlier filtered PlayMode runs used `-testFilter GardenVR.Sundial.Tests.PlayMode.MonthlyRecordTests`, producing `monthly.xml` and `monthly-final.xml`: both 5 passed, 0 failed. The final full suites ran after the integration fixes; EditMode was rerun after the final code changes. Parsed output in `verification.txt`:

```text
editmode result=Passed total=98 passed=98 failed=0 skipped=0
playmode result=Passed total=94 passed=94 failed=0 skipped=0
MonthTotal_OnLook Passed
NoConsecutiveCountString Passed
Quantity_ReachTarget_TendsOnce Passed
Quantity_Undo_ReversesTend Passed
ZoneTabs_FillFromLedger Passed
editmode compiler/shader/build errors=0
playmode compiler/shader/build errors=0
build compiler/shader/build errors=0
Build Succeeded C:\Users\kazda\kiro\gvr-sundial\apps\sundial\Build\sundial\Sundial.exe
terrarium-compile compiler/shader/build errors=0
Exiting batchmode successfully now!
```

Terrarium also logged `AssetDatabase: script compilation time: 3.475044s`. The Windows executable stays ignored on disk; its hash is in `verification.txt`. Manual standalone interaction was not run. Runtime interactions, saves, reloads, target crossing, extra counts, undo, undo expiry, first run, month rollover and empty-zone shading were verified through tests.

## Visual evidence

- [DialG1-monthly-record-nine-plants.png](DialG1-monthly-record-nine-plants.png): nine plants, four shaded zone tabs, and `Plan tomorrow's top 3`, `kept 18 days this month`.
- [DialG1-quantity-three-of-eight.png](DialG1-quantity-three-of-eight.png): the same garden, looking at Water with three units recorded toward eight.
- [DialG1-record-side-by-side.jpg](DialG1-record-side-by-side.jpg): record artboard left, actual capture right.
- [DialG1-reference-side-by-side.jpg](DialG1-reference-side-by-side.jpg): Field Notebook G1 style reference left, actual capture right.
- `capture-save.json` and `capture-state.json`: isolated fixture state. The fixture uses October 20, 2026, includes September 30 to test month exclusion, and gives the selected Work habit 18 kept October days. It never uses the real player save.
- Both PNG check files show `magentaFrac: 0`; they retain the raw capture hash and add the final post-ingest hash. `evidence.py` rebuilds the comparisons and verification summary.

Both comparison sources and captures use the same 1824 x 1024 DialG1 framing, with no crop, stretching or retouching. A caption strip labels the two sources. The current inherited plants, dial/gnomon layout, and larger notes remain visibly different from the concept/reference. This task does **not** claim an A3 art-quality pass. The frame is registered in `apps/sundial/GATE.md` for B12.

Lossless PNG ingest before staging:

```powershell
python tools/assets/lossless.py --ingest --apply orchestration/runs/sundial/T-SUN-066/DialG1-monthly-record-nine-plants.png orchestration/runs/sundial/T-SUN-066/DialG1-quantity-three-of-eight.png
python orchestration/runs/sundial/T-SUN-066/evidence.py
```

Actual ingest output, exit 0 (`lossless.txt`):

```text
DialG1-monthly-record-nine-plants.png 2952227 -> 2490002 pillow-optimize proof ok (mode, size, palette, transparency, pixel sha256 f0302b4b6ce0) written
DialG1-quantity-three-of-eight.png 2947010 -> 2482180 pillow-optimize proof ok (mode, size, palette, transparency, pixel sha256 4a5e71770627) written
total 5899237 -> 4972182, saved 927055 bytes; proof failures 0; applied
```

## Failures resolved and cleanup

1. Initial .NET test fixture used target 1, which `HabitProfiles.CleanTarget` intentionally treats as a non-quantity habit. `SundialQuantityTests.Quantity_respects_reading_pending_tends_and_expired_undo` failed with `Assert.NotNull() Failure: Value is null`, line 49. Corrected the fixture to target 2, then verified expiry after reaching it. Raw output: `dotnet.log`; final .NET suite is green.
2. Initial full PlayMode run: 92 passed, 2 failed (`playmode-initial.xml`, raw `playmode-initial.log`). `Interview_Speech_UnknownCue_ArcChoice_PauseAndPrivacy`, line 238: `Expected: greater than 0.0f / But was: -1.0f`. The wizard only observed deferred tends, and the test assumed water completed with one pinch. Updated the wizard and strengthened the test to require all eight units. `Backfill_NotAfterBoundary`, line 111: `Expected: True / But was: False`. This fixture implicitly depended on a previous fixture enabling dev seeding. It now explicitly enables its own dev habits; the boundary assertions are unchanged. Final full PlayMode is 94/94.
3. The first capture exposed duplicate onboarding/monthly zone tabs and an overlapping gratitude invitation. Both flows now share one row of four tabs; the invitation yields while reading the note. Final captures were inspected.

Unity logs contain inherited obsolete-member warnings and build-shutdown JobTempAlloc diagnostics, but no compiler, shader or build errors. Restored only generated material/URP/default-volume changes and prior tasks' tracked captures regenerated by the full suite. No Terrarium files changed. Initial untracked plant candidates, root comparison PNGs and scratch files remain untouched.

Raw `.log` files stay on disk, untracked, per AGENTS.md; relevant output is quoted above. Results XML/TRX, captures, comparisons, summaries and this report are committed. No Library, Temp, Logs, build files or secrets are committed. `git diff --check` passed; trailing whitespace in result stdout was normalized without changing results.

## User-facing strings for B12

New templates, also listed in `user-facing-strings.txt`:

- `kept {N} days this month`
- `{count} of {target}`

Reused tab labels: `Body`, `Mind`, `Work`, `Connection`. Habit names remain profile data. There are no percentage strings or numbers on zone tabs. The two templates and four reused labels have zero em-dash or CoachGuards medical-stem matches (`verification.txt`). `NoConsecutiveCountString` scans shipped script string literals and instantiated dial labels for streak, in-a-row, consecutive, straight-day, fail, shame and wilt terms: **0 hits** (`grep-u5.txt`).

No blockers remain. Next: B12 can collect the record frame, results and string census; owner art scoring remains separate.
