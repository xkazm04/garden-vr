# T-SUN-058: B1 save schema 2

Completed 2026-10-12 in `C:/Users/kazda/kiro/gvr-sundial`, branch `agent/sundial`.

Schema-1 saves migrate to schema 2 through the existing SaveStore snapshot and atomic-write path. Upgrade state round-trips, the install key persists, and launch writes the garden day after the catch-up opportunity. All 517 .NET tests, 75 Sundial EditMode tests and 51 Sundial PlayMode tests pass. The Windows player builds.

## Changes and scope

- Added `Counts`, `Journal`, `LastVisitDay`, `InstallKey`, and the five new settings. Counts and journal use `QuantityJson` and `CoachJournal`; catch-up depth uses `CatchUp.Depth`; defaults come from core. Nudge windows serialize exact `TimeSpan` ticks as `StartTicks`/`EndTicks`. Each settings instance owns its list, and an explicitly empty list remains empty.
- `SundialService.SchemaVersion` is 2. The 1-to-2 migration fills missing upgrade fields without replacing existing values. SaveStore retains the original snapshot. Unknown root, habit and tend data continue to survive; unknown settings now survive too. Explicit schema-1 codec writes retain the original golden format; the service migrates and writes schema 2.
- Fresh saves, codec reads of missing keys, and writes of uninitialized documents create the install key through `RelayProtocol.NewInstallKey`. Existing keys remain stable across writes, migration and relaunch.
- `SundialService.CompleteLaunch()` records the garden day once, respecting the configured boundary and the existing failed-load/future-schema write guards. Loading leaves the prior visit available. `SundialController.LaunchCatchUp` lets B8 subscribe before Start and receive a completion callback to invoke after the offer closes. Without a subscriber, Start completes the launch immediately. No catch-up UI is added in B1.
- Added 12 .NET cases, including a schema-1-to-2 golden, every new member set in a codec/disk round trip, copied player-save migration, missing defaults, empty journal/windows, invalid depth, key stability, delayed/idempotent launch and protected saves. Added two PlayMode launch tests. Updated the approved public surface and the existing FocusBlock test's expected schema from 1 to 2; no test was removed, skipped or weakened.
- Shared runtime packages are unchanged. The additions consume their existing APIs and remain backward compatible. Only Sundial app code and the authorized Sundial model test paths changed.

Read the worktree AGENTS.md, the read-only task in the main checkout, upgrade-step-b's common section/B1, upgrade context, decisions 0015/0016, and T-SUN-057's report. The task file was not edited or moved. `git merge --no-edit main` completed without conflicts as `f83bd2f` (no file changes). Implementation and tests are committed as `812e45b` (`feat(sundial): migrate upgrade state to save schema 2`). No push or history rewrite.

## Real player-save evidence

Copied the existing 161-byte schema-1 file from `C:/Users/kazda/AppData/LocalLow/DefaultCompany/sundial/sundial/save.json`. It contains empty habit/tend lists, the original settings and `FirstRunStep: fr.appear`. Its bytes are preserved in `player-schema1-save.json` and the committed test fixture `shared/core-dotnet/GardenVR.SundialModel.Tests/Fixtures/player-schema1-save.json`.

`The_current_players_schema1_save_loads_with_nothing_lost` opens that fixture in a temporary directory through the real service/SaveStore. It checks the snapshot and previous-save bytes, compares every original root/settings member except the intended schema version change, completes launch, and reloads the result. `player-schema2-save.json` is the actual migrated and launch-saved output. The populated golden separately covers habits, tends, gratitude, focus and unknown members, because this real player's save has no habits yet.

The original player save was never written. Its final SHA-256 still equals the fixture:

```text
D07E3AE7860B3301A4279A75B59E81D06D434CDF43936A4D4331713C918D3DAC
```

## Verification commands and actual output

All commands ran in this worktree. Unity ran sequentially in the foreground using `Start-Process -Wait`; each invocation finished and its log was read before the next. Test success was checked from XML, not just exit codes.

```powershell
$env:DOTNET_ROLL_FORWARD='Major'
$env:GARDENVR_B1_EVIDENCE=(Resolve-Path 'orchestration/runs/sundial/T-SUN-058').Path
dotnet test shared/core-dotnet/GardenVR.sln 2>&1 |
    Tee-Object orchestration/runs/sundial/T-SUN-058/dotnet.log
```

Final output, exit 0 (`dotnet.log`):

```text
Passed!  - Failed:     0, Passed:    14, Skipped:     0, Total:    14, Duration: 25 ms - GardenVR.TerrariumModel.Tests.dll (net9.0)
Passed!  - Failed:     0, Passed:    11, Skipped:     0, Total:    11, Duration: 53 ms - GardenVR.CoachCli.Tests.dll (net9.0)
Passed!  - Failed:     0, Passed:    50, Skipped:     0, Total:    50, Duration: 270 ms - GardenVR.SundialModel.Tests.dll (net9.0)
Passed!  - Failed:     0, Passed:   442, Skipped:     0, Total:   442, Duration: 885 ms - GardenVR.Core.Tests.dll (net9.0)
```

This includes all `SundialProfileSaveTests` and `SundialCodecGoldenTests`. The task-required public surface update used the existing approval mechanism once, then the final suite ran with it unset:

```powershell
$env:GARDENVR_APPROVE_SURFACE='1'
dotnet test shared/core-dotnet/GardenVR.SundialModel.Tests --filter FullyQualifiedName~PublicSurfaceTests
Remove-Item Env:GARDENVR_APPROVE_SURFACE
# Passed: 1, Failed: 0
```

Unity invocations (each also printed the returned exit code and log tail):

```powershell
$unity = 'C:/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Unity.exe'
$repo = 'C:/Users/kazda/kiro/gvr-sundial'
$runDir = "$repo/orchestration/runs/sundial/T-SUN-058"

Start-Process -Wait -PassThru -WindowStyle Hidden -FilePath $unity -ArgumentList "-batchmode -nographics -projectPath $repo/apps/sundial -runTests -testPlatform EditMode -testResults $runDir/editmode.xml -logFile $runDir/editmode.log"
Start-Process -Wait -PassThru -WindowStyle Hidden -FilePath $unity -ArgumentList "-batchmode -projectPath $repo/apps/sundial -runTests -testPlatform PlayMode -testResults $runDir/playmode.xml -logFile $runDir/playmode.log"
Start-Process -Wait -PassThru -WindowStyle Hidden -FilePath $unity -ArgumentList "-batchmode -nographics -quit -projectPath $repo/apps/sundial -executeMethod GardenVR.Sundial.Editor.Build.Windows -logFile $runDir/build.log"
```

Each returned exit 0. Parsed XML and error-scan output (`verification.txt`):

```text
editmode result=Passed total=75 passed=75 failed=0 skipped=0
playmode result=Passed total=51 passed=51 failed=0 skipped=0
editmode compiler/shader/build error entries=0
playmode compiler/shader/build error entries=0
build compiler/shader/build error entries=0
UnityEditorProcesses=0
```

Both new PlayMode cases passed:

```text
UpgradeSaveTests.LaunchCatchUp_SeesPreviousVisit_AndDefersWritingUntilClosed  Passed
UpgradeSaveTests.Schema1_LaunchMigratesAndPersistsVisitAndKey              Passed
```

The existing ten-run journey passed with the same state SHA-256 in all ten runs:
`4d993985b44e1ed46492b269bdf3dd4ed011f8ed0d7e028e130a8bd079f1881d`.

Build output (`build.log`):

```text
Build Succeeded C:\Users\kazda\kiro\gvr-sundial\apps\sundial\Build\sundial\Sundial.exe
```

The player remains locally at that ignored build path. `verification.txt` records executable and managed assembly sizes/SHA-256. Manual standalone player interaction was not run; launch behavior was verified in PlayMode and the build completed.

## Failures, cleanup and evidence

Two development-stage .NET failures were fixed before the final run, with their original logs preserved:

1. `dotnet-initial.log`: the expected approved-surface mismatch after adding the fields and changing the schema constant (`Failed: 1, Passed: 37`). Updated the approved file as required by B1 and reviewed the additive surface.
2. `dotnet-fixture-failure.log`: the new migration golden initially reused the codec's intentionally id-less habit fixture through a running service (`Failed: 1, Passed: 49`). Actual error:

```text
System.ArgumentException : habit id
at GardenVR.Core.SundialRules.Plant(...)
at GardenVR.Core.SundialState.Capture(...)
at GardenVR.Sundial.SundialService.Recompute()
```

The new service migration fixture now supplies that habit's ID and pins the matching expected output. The original codec golden and its id-less-row checks remain unchanged. No production rule was relaxed.

All Unity suites passed on their first invocation. Logs retain existing nonblocking licensing token diagnostics, inherited-member/obsolete API warnings, and build shutdown JobTempAlloc diagnostics. No compiler, shader or build errors were found.

Inspected and restored only Unity's generated changes to ten Sundial materials, DefaultVolumeProfile, GardenURP prefilter settings and global URP runtime settings. Temporary test scenes/resources were removed by Unity. `git diff --check` passed. The initially untracked plant candidates, root comparison PNGs and scratch directory remain untouched. No build binaries, Library/Temp/Logs directories, secrets or unrelated files are committed.

Evidence beside this report: `dotnet.log`, both development failure logs, `editmode.xml`/`.log`, `playmode.xml`/`.log`, `build.log`, the before/after player save JSON files, and `verification.txt`. Evidence is force-added only within this task's report directory because orchestration is normally ignored.

B1 specifies no capture framing and changes no visuals. Captures and side-by-sides: not applicable. Image/audio generation: none. No PNG files added or changed.

User-facing strings added for B12's census: **none**. Journal sentences in tests are fixtures, not app copy.

No blockers remain. Next: B2 can consume the persisted profile/upgrade state. When B8 adds its card, subscribe to `LaunchCatchUp` before Start, read `Service.Save.LastVisitDay`, and invoke the supplied completion after dismissal or when no offer is needed. Termination: finished.
