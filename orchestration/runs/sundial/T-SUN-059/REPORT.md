# T-SUN-059: B2 habit profiles on the dial

Completed 2026-10-12 in `C:/Users/kazda/kiro/gvr-sundial`, branch `agent/sundial`.

All acceptance checks pass: **522 .NET tests, 75 Sundial EditMode tests, 55 Sundial PlayMode tests**, and the Windows player build. All four named B2 tests pass. The nine-plant DialG1 gate frame and its record-artboard comparison are saved and registered in `apps/sundial/GATE.md`. Terrarium also compiles against the shared-core additions.

## Changes

- `SundialService.PlantProposal(p, arcKey)` converts through `CoachProposal.ToHabit`, cleans the name/target through core, owns a copy of the schedule, and preserves the zone and quantity unit. A chosen arc is respected; no choice uses core's least-populated arc, with stable morning/midday/wind-down tie order. A full chosen arc refuses without silently redirecting. A shared admission path uses `HabitProfiles.CanAdd` and `SundialRules.Admit` for presets and proposals, preserving three per arc and nine live habits. Preset zones use `TryPresetZone`. Preset first-run repeat taps still return the existing habit.
- Core supplies two appended tile states: `NotPlanned` and `Flexible`. Weekday off days are blank paper and read ?not planned? on look. Unkept past days of a weekly habit are blank and never missed. Kept/late events remain visible even on an off day. Daily/default schedules retain their existing behavior and enum values. The yesterday ??? is offered only for a missed planned day. Core also supplies the current Monday-to-today distinct kept count, excluding undone events.
- `DialView` draws an ink-edged dot under each plant using the exact Field Notebook FN palette: Body `#6f9e6a`, Mind `#5f84b3`, Work `#b88440`, Connection `#c9707f`. Dots render above foliage so dense nine-plant arrangements remain readable. Looking at a plant or its seven-day tile row shows its own cleaned name and zone on an `InkLetter` note; a weekly habit adds ?2 of 3 this week? from core's count. Names render as literal text. Week-page names also prefer the profile name, with the existing preset fallback.
- The existing focus offer yields while a profile note is being read, avoiding the large overlapping prompt seen in the first capture. No direct keyboard/mouse access was added: the controller receives `Look` through the input intent source.
- Added three core tests, two service tests and the four requested PlayMode tests. Tests cover off-day tending, weekly count reset/undo/late history, least-populated placement with archived habits, profile disk round-trip, independent schedule ownership, preset compatibility, no-write refusal, rendered label/dot color, all nine slots, and refusal of a tenth. Updated both approved public surfaces. Existing tests remain present.

Shared-package changes are backward compatible: no public member was removed, existing enum numeric values are unchanged, and daily/default schedules behave as before. Profile schedules opt into the new record behavior. Both apps compile; Terrarium's 14 model tests pass. Shared changes are in `41ec416` (`feat(core): respect habit schedules in sundial records`); app/service/tests are in `e0ac006` (`feat(sundial): plant profiles and show names zones and schedules`). No push or history rewrite.

Read AGENTS.md, the read-only task, upgrade-step-b's common section/B2, the upgrade context, decisions 0015/0016, the record artboard, and T-SUN-058's report. The task file was not edited or moved. All work stayed in this worktree. Terrarium was opened only for the required shared-package compilation check; no Terrarium files changed.

## Verification commands and actual output

Each Unity invocation ran sequentially in the foreground with `Start-Process -Wait -PassThru -WindowStyle Hidden`; its log was read before continuing. XML counts, not just process exit codes, establish test success.

```powershell
$env:DOTNET_ROLL_FORWARD='Major'
dotnet test shared/core-dotnet/GardenVR.sln --logger trx --results-directory orchestration/runs/sundial/T-SUN-059/dotnet-results 2>&1 | Tee-Object orchestration/runs/sundial/T-SUN-059/dotnet.log
```

Exit 0, actual final output:

```text
Passed!  - Failed: 0, Passed: 14, Skipped: 0, Total: 14, Duration: 27 ms - GardenVR.TerrariumModel.Tests.dll (net9.0)
Passed!  - Failed: 0, Passed: 11, Skipped: 0, Total: 11, Duration: 65 ms - GardenVR.CoachCli.Tests.dll (net9.0)
Passed!  - Failed: 0, Passed: 52, Skipped: 0, Total: 52, Duration: 305 ms - GardenVR.SundialModel.Tests.dll (net9.0)
Passed!  - Failed: 0, Passed: 445, Skipped: 0, Total: 445, Duration: 999 ms - GardenVR.Core.Tests.dll (net9.0)
```

Surface snapshots were regenerated with the repository's `GARDENVR_APPROVE_SURFACE=1` mechanism and `--filter FullyQualifiedName~PublicSurfaceTests`, then reviewed. The environment variable was unset for the final suite. Final XML TRX results are copied beside this report under the four assembly names.

```powershell
$unity='C:/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Unity.exe'
$repo='C:/Users/kazda/kiro/gvr-sundial'
$runDir="$repo/orchestration/runs/sundial/T-SUN-059"
Start-Process -Wait -PassThru -WindowStyle Hidden -FilePath $unity -ArgumentList "-batchmode -nographics -projectPath $repo/apps/sundial -runTests -testPlatform EditMode -testResults $runDir/editmode.xml -logFile $runDir/editmode.log"
Start-Process -Wait -PassThru -WindowStyle Hidden -FilePath $unity -ArgumentList "-batchmode -projectPath $repo/apps/sundial -runTests -testPlatform PlayMode -testResults $runDir/playmode.xml -logFile $runDir/playmode.log"
Start-Process -Wait -PassThru -WindowStyle Hidden -FilePath $unity -ArgumentList "-batchmode -nographics -quit -projectPath $repo/apps/sundial -executeMethod GardenVR.Sundial.Editor.Build.Windows -logFile $runDir/build.log"
Start-Process -Wait -PassThru -WindowStyle Hidden -FilePath $unity -ArgumentList "-batchmode -nographics -quit -projectPath $repo/apps/terrarium -logFile $runDir/terrarium-compile.log"
```

Each returned Unity exit 0. Parsed XML and log scan (`verification.txt`):

```text
editmode result=Passed total=75 passed=75 failed=0 skipped=0
playmode result=Passed total=55 passed=55 failed=0 skipped=0
NinePlants_ThreeArcsFull_AdmitRefusesTenth Passed
PerWeek_NeverMarksADayMissed Passed
Profile_NameAndZoneShowOnLook Passed
Weekdays_OffDayTileIsBlank Passed
editmode compiler/shader/build error entries=0
playmode compiler/shader/build error entries=0
build compiler/shader/build error entries=0
terrarium-compile compiler/shader/build error entries=0
Build Succeeded C:\Users\kazda\kiro\gvr-sundial\apps\sundial\Build\sundial\Sundial.exe
```

Terrarium's log additionally reports `AssetDatabase: script compilation time: 2.323111s` and `Exiting batchmode successfully now!`. No editor processes remained after verification. The ignored Windows build stays on disk; `verification.txt` records its executable hash. Manual standalone interaction was not run; behaviors were exercised through PlayMode intent playback.

## Gate frame and visual evidence

- [DialG1-nine-plants.png](DialG1-nine-plants.png): **B2 gate frame**, nine live plants across three full arcs and four zones, looking at ?Plan tomorrow's top 3?.
- [DialG1-record-side-by-side.jpg](DialG1-record-side-by-side.jpg): record artboard on the left, actual capture on the right. Both source frames are 1824 x 1024 at DialG1, with no crop or stretching. Pillow composed the two unchanged frames plus a caption strip.
- [DialG1-weekly-look.png](DialG1-weekly-look.png): the same garden looking at the weekly Connection habit.
- `capture-save.json` and `capture-state.json`: isolated fixture save and actual state oracle. The fixture uses 2026-10-10 and eighteen previous days of varied tends. It never opens or writes the user's real save.
- Both `.png.check.json` files show `magentaFrac: 0`; they include the original capture PNG hash and the final post-ingest PNG hash.

The four-test PlayMode fixture produces these captures through the existing DialG1 camera/plate helpers and `FrameGrab`, including shader warmup. The artboard remains a composited concept; the inherited current plant assets and dial layout are visibly denser and differ from it. This evidence does **not** claim an A3 art-quality pass. Monthly totals and shaded rim tabs are B9; this task establishes the profile/zone/schedule frame for B12.

Lossless ingest ran before committing either PNG:

```powershell
python tools/assets/lossless.py --ingest --apply orchestration/runs/sundial/T-SUN-059/DialG1-nine-plants.png orchestration/runs/sundial/T-SUN-059/DialG1-weekly-look.png
```

Actual output, exit 0 (`lossless.txt`):

```text
DialG1-nine-plants.png 2913128 -> 2448602 pillow-optimize proof ok (mode, size, palette, transparency, pixel sha256 a6bb9962361f) written
DialG1-weekly-look.png 2904630 -> 2439111 pillow-optimize proof ok (mode, size, palette, transparency, pixel sha256 63526d829d3b) written
total 5817758 -> 4887713, saved 930045 bytes; proof failures 0; applied
```

No image generation, mesh generation or audio generation was needed. All plants/textures are existing assets; the small zone mark uses the existing code-drawn ink/card system.

## Development failures and cleanup

1. Initial .NET run: the expected public-surface mismatches, plus `SundialServiceTests.A_failed_load_writes_nothing` reported `Assert.NotNull() Failure: Value is null`. An over-broad new admission guard had changed the legacy transient preset behavior. Restored that behavior while retaining existing disk-write protection. The final suite passes the original test unchanged. Output is in `dotnet-initial.log`.
2. New core test initially omitted GardenDay.From's boundary: `error CS7036: There is no argument given that corresponds to the required parameter 'boundary'`. Added `GardenDay.DefaultBoundary`; output is retained in `surface.log`.
3. First Unity invocation: `HabitProfileTests.cs(99,35): error CS0117: 'WeekDial' does not contain a definition for 'Capture'`. Corrected the test to the actual `WeekDial.Read` API. Original output: `profiles.log`.
4. Filtered retry: 3 passed, 1 failed. Exact Color equality reported visually identical `RGBA(0.788, 0.439, 0.498, 1.000)` values because of float roundoff through the material property block. The assertion now uses a 0.0001 distance tolerance. Failed XML is preserved as `profiles-initial.xml`, with `profiles-retry.log`. Final full-suite XML shows all four passing.

The first capture also exposed an oversized label, foliage hiding dots, and the existing focus prompt covering plants. The final capture verifies the smaller note, visible dots and yielding focus offer. Final Unity logs contain existing obsolete/inherited-member warnings and build-shutdown JobTempAlloc diagnostics; no compiler/shader/build errors were found.

Inspected and restored only Unity-generated changes to ten Sundial materials, DefaultVolumeProfile, URP prefilter settings and the global URP settings asset. The initial untracked plant candidates, root comparison PNGs and scratch files remain untouched. Raw `.log` files stay local and untracked per AGENTS.md; the report quotes the relevant output, and XML/TRX, captures and small summaries are committed. No Library, Temp, Logs, build files, credentials or unrelated files are committed. Trailing whitespace in captured XML stdout and the lossless summary was normalized; test results were not changed. `git diff --check` passed.

## User-facing strings for B12

The complete new catalog is also in `user-facing-strings.txt`:

- `Body`
- `Mind`
- `Work`
- `Connection`
- `not planned`
- `Any day this week`
- `{kept} of {target} this week`

Habit names come from the saved profile through `CleanName`; existing preset titles are reused. Capture/test names are fixture data, not additional shipped catalog strings. The seven entries were checked against CoachGuards' medical stems and consecutive-count phrases, and for an em dash: zero matches (`verification.txt`).

No blockers remain. Next: B5 can plant accepted coach proposals through the new service method; B9 adds monthly totals and rim tabs. Termination: finished.
