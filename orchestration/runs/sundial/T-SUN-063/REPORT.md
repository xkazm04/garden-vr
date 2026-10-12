# T-SUN-063: B6 break session

Status: **blocked on input-package scope; B6 is not complete**. Work performed on 2026-10-12 only in `C:/Users/kazda/kiro/gvr-sundial`, branch `agent/sundial`. The task in the main checkout was read without modification. No push or history rewrite.

## Exact blocker

B6 requires a held open palm, release detection, and pause/resume without counting time after letting go. The existing PC provider does not expose that information:

- `shared/packages/com.gardenvr.input/Runtime/Mapping/KbmIntentMapper.cs:124` resets its private palm state silently on release, and emits one `PalmOpen` at 0.6 seconds. `_palmFired` then suppresses further events.
- `IHandIntentSource` exposes pinch and tracking state, but no palm state. `Release` describes the pinch, not the palm.
- A 0.7-second palm press and a four-second palm hold produce **identical public intent streams**. An app consumer cannot correctly distinguish them.

This is reproduced by the passing EditMode diagnostic `BreakInputContractTests.PcPalm_CurrentContractCannotDistinguishReleaseFromContinuedHold`, which compares both full 240-frame streams. Real output from `editmode.log`:

```text
[B6 input blocker] P held for 0.7s then released and P held for 4s produce identical intent streams: one PalmOpen, no Release. IHandIntentSource exposes no palm state.
```

There is no compiler error behind this blocker. It is missing input information plus a scope restriction. The task's touches list names only the Sundial app, core-dotnet, com.gardenvr.core and this report area. AGENTS.md says: "Work only inside your app folder and the shared paths the task names." It also forbids direct keyboard/mouse reads in app code. I requested approval to add a backward-compatible palm-state interface/provider implementation and provider tests under `com.gardenvr.input`. Approval has not arrived. That package remains unchanged.

Next required action: authorize that input-package addition. Preserve existing single-commit PalmOpen behavior for all consumers; expose held palm state through an optional interface. Cover release and focus loss, then integrate BreakController and run the four B6 tests. Replace the diagnostic expectation once the public input contract gains the needed state.

## Completed independent work

1. `SundialService.EnsureBreakHabit()` admits one named Body-zone `InAppRitual` using `BreakSession.PresetKey`, `LeastPopulatedArc`, `HabitProfiles.CanAdd` and `SundialRules.Admit`. It reuses an existing live break habit and respects the existing load/read-only/scrub guards. No core rule is duplicated. Two .NET tests cover admission into a free arc, persistence/reload, idempotent Ritual tends, and refusal without save changes when all nine spaces are full. Approved public surface updated by one additive method. Commit: `2f1e734`.
2. `BreakView` is an unattached presentation component using existing InkLetter paper/font rendering and hand-authored ink paths for open-palm/pinch figures. It renders the countdown from `EyeRest.SecondsLeft`. The mote is placed on the window at `EyeRest.TargetDistance(null)` (3 m). It is not added to runtime bootstrap or any scene; its action target IDs have no controller yet.
3. `BreakViewTests` captures three presentation previews using core samples. It checks 13 seconds remaining after seven seconds of eye rest, the actual three-metre camera-to-target distance, and that a ray can hit `break.eye`. This is a presentation test, not an input-driven break test.
4. The input-contract diagnostic above gives a repeatable reproduction for the scope extension. Presentation and diagnostic code committed as `8d09b98`.

Shared runtime packages are unchanged. Sundial model changes are additive and backward compatible. No image generation, audio generation, mesh-generation model, network service, or ADB device was used.

## Acceptance status

| Required check | Result |
|---|---|
| `Break_FullRun_TendsBreakHabitOnce` | Not implemented/run; controller depends on missing palm input state |
| `Break_LookAway_PausesEyeRest` | Not implemented/run |
| `Break_PoseLetGo_KeepsDonePoses` | Blocked by indistinguishable palm hold/release |
| `Break_PauseResume_AtEachPart` (S4) | Not implemented/run; not a pass |
| Actual break moment gate capture | Not available; presentation-only previews supplied |
| Side-by-side against artboard | Three labeled preview comparisons supplied |
| .NET suite | Passed: 539, failed: 0 |
| Sundial EditMode | Passed: 98, failed: 0 |
| Sundial PlayMode | Passed: 74, failed: 0 |
| Windows player | Build succeeded |

Morning stretch behavior is unchanged. Full break input routing, suppression of other ritual prompts, first-use controller admission, ConsumeTend routing, nudge entry seam, and pause/resume remain to implement. No B6 completion claim is made.

## Commands and actual output

All commands ran in the assigned worktree. Unity invocations run sequentially in the foreground with `Start-Process -Wait`, and logs are read after exit. No two Unity editors use this project concurrently. The separately observed Unity Hub `unity.exe serve` process is not a project editor.

```powershell
$env:DOTNET_ROLL_FORWARD='Major'
dotnet test shared/core-dotnet/GardenVR.sln 2>&1 |
    Tee-Object orchestration/runs/sundial/T-SUN-063/dotnet.log
```

```text
Passed!  - Failed: 0, Passed: 14, Skipped: 0, Total: 14 - GardenVR.TerrariumModel.Tests.dll
Passed!  - Failed: 0, Passed: 11, Skipped: 0, Total: 11 - GardenVR.CoachCli.Tests.dll
Passed!  - Failed: 0, Passed: 55, Skipped: 0, Total: 55 - GardenVR.SundialModel.Tests.dll
Passed!  - Failed: 0, Passed: 459, Skipped: 0, Total: 459 - GardenVR.Core.Tests.dll
```

The first run deliberately encountered the approved-surface check after adding the method (`dotnet-initial.log`, initial TRX files):

```text
public surface changed.
added:
  GardenVR.Sundial.SundialService method public GardenVR.Core.HabitDef EnsureBreakHabit()
removed:
Failed! - Failed: 1, Passed: 54, Total: 55 - GardenVR.SundialModel.Tests.dll
```

Approved and reviewed that single additive line through the existing mechanism, then unset the approval flag before the final green suite:

```powershell
$env:GARDENVR_APPROVE_SURFACE='1'
dotnet test shared/core-dotnet/GardenVR.SundialModel.Tests --filter FullyQualifiedName~PublicSurfaceTests
Remove-Item Env:GARDENVR_APPROVE_SURFACE
# Passed: 1, Failed: 0
```

Unity commands (absolute paths used in the actual calls):

```powershell
$unity='C:/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Unity.exe'
$repo='C:/Users/kazda/kiro/gvr-sundial'
$runDir="$repo/orchestration/runs/sundial/T-SUN-063"
Start-Process -Wait -PassThru -WindowStyle Hidden -FilePath $unity -ArgumentList "-batchmode -nographics -projectPath $repo/apps/sundial -runTests -testPlatform EditMode -testResults $runDir/editmode.xml -logFile $runDir/editmode.log"
Start-Process -Wait -PassThru -WindowStyle Hidden -FilePath $unity -ArgumentList "-batchmode -projectPath $repo/apps/sundial -runTests -testPlatform PlayMode -testFilter BreakViewTests -testResults $runDir/view-preview.xml -logFile $runDir/view-preview.log"
Start-Process -Wait -PassThru -WindowStyle Hidden -FilePath $unity -ArgumentList "-batchmode -projectPath $repo/apps/sundial -runTests -testPlatform PlayMode -testResults $runDir/playmode.xml -logFile $runDir/playmode.log"
Start-Process -Wait -PassThru -WindowStyle Hidden -FilePath $unity -ArgumentList "-batchmode -nographics -quit -projectPath $repo/apps/sundial -executeMethod GardenVR.Sundial.Editor.Build.Windows -logFile $runDir/build.log"
```

The isolated presentation test passed 1/1. Initial EditMode before the diagnostic passed 97/97; final diagnostic-inclusive EditMode passed 98/98. XML, not exit code alone, is used to assess test runs. The presentation test was then rerun in the full PlayMode suite after fitting the slips to the viewport. The final six PNGs come from that full run.

```text
editmode result=Passed total=98 passed=98 failed=0 skipped=0
playmode result=Passed total=74 passed=74 failed=0 skipped=0
view-preview result=Passed total=1 passed=1 failed=0 skipped=0
Build Succeeded C:\Users\kazda\kiro\gvr-sundial\apps\sundial\Build\sundial\Sundial.exe
```

All Unity invocations returned exit 0. The pre-existing `Stretch_ThreeReaches_TendsMorning` passed in the full suite. No compiler/shader/build errors were found. Build shutdown retains the known `Internal: There are remaining Allocations on the JobTempAlloc` diagnostics. Manual standalone interaction was not run; the new view is deliberately unattached and the player does not yet offer B6.

## Visual evidence and limits

`DialG1-break-{palm,pinch,eye}-preview.png` are genuine Unity renders at the 1824x1024 DialG1 framing. `break-{palm,pinch,eye}-side-by-side.png` puts the artboard at left and the corresponding preview at right, without resizing either panel. `compare.py` reproduces the boards. `.check.json` files are the capture-time image checks; `view-preview.txt` records distance/countdown/raycast assertions.

These are explicitly labeled presentation previews. They do not prove the four acceptance interactions or qualify as the final gate frame. Visual inspection found legacy ritual offer cards still overlap the eye-rest slip and dominate the scene. The future controller needs modal prompt ownership. The existing tiny seed-stage plants and gnomon also differ from the grown artboard composition. No owner art score or art-parity pass is claimed.

New PNG files passed `python tools/assets/lossless.py --ingest --apply <all six PNG paths>` before commit; pixel proof output is retained in `lossless.log`:

```text
total 20302598 -> 18413867, saved 1888731 bytes; proof failures 0; applied
```

Raw Unity/build/.NET logs remain on disk, untracked, per AGENTS.md; report excerpts, results XML, captures, comparisons and small summaries are committed. Capture-time `.check.json` byte hashes precede lossless recompression; decoded pixels were proved unchanged.

## User-facing strings for B12

`Break` is the new habit name. The unattached view adds the following draft strings (not yet reachable in the player):

- `Take a break`
- `Pause break`
- `Continue break`
- `Take your time` / `Pinch continue when ready`
- `Reach to each mark` / `stretch {N} of 3`
- `Hold an open palm`
- `Hold a pinch`
- `{provider binding hint}   {N} of 3`
- `A moment by the window` / `Looking away holds your place`
- `Rest your eyes here` / `{N} s`

Binding hints come from the existing input provider. None of the new strings contains an em dash, medical claim, guilt phrase or consecutive-day counter.

## Final repository state

Restored only Unity-generated material and URP/volume serialization changes and regenerated older-task evidence introduced by the verification runs. Initial untracked plant candidates, root comparison images and scratch files remain untouched. No build output, Library/Temp/Logs directory or secret is committed. Source `git diff --check` passed. The committed raw PlayMode XML retains one trailing space in the existing `[Review] hints` test output (line 1398); its staged whitespace check reports that evidence line. The verification summary was normalized to UTF-8 after PowerShell's mixed output encoding was detected. All foreground processes finished and their logs were read before reporting. Final report/evidence are separate commits. No outstanding implementation error was hidden by weakening a test; the B6 scope/input blocker remains open.
