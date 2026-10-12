Status: complete

# T-SUN-065: B8 catch-up card

Completed 2026-10-12 in `C:/Users/kazda/kiro/gvr-sundial`, branch `agent/sundial`.
Implementation commit: `1ece81b` (`feat(sundial): offer launch catch-up through shared late-log rules`). No push.

The launch card offers core's pending habit-days, with two days as the default depth. Each pinch logs a late tend;
Done or PalmOpen closes the card without logging the remaining rows. All 543 .NET tests, 98 Sundial EditMode tests,
and 89 Sundial PlayMode tests pass. The Windows player builds successfully.

## Changes

- `CatchUpCard` uses `SundialService.PendingCatchUp()`, which delegates directly to `CatchUp.Pending`. It lists dated
  habit names on the existing ink-and-paper material, six rows per page, with Previous/Next for larger gardens.
  It refreshes after logging, when a weekly target becomes satisfied, and when the garden day changes. Empty
  completion reads "All noted" until dismissed. A first visit has no card.
- The controller's default launch presenter now opens this card after drawing the dial. The existing
  `LaunchCatchUp` override remains usable. `CompleteLaunch` is called after dismissal, or immediately when there
  is nothing to offer. Logging a row saves its tend while retaining the previous visit until dismissal.
- Both entry points use `SundialService.LogCatchUp` -> `CatchUp.Log` -> `Ledger.BackfillWithin`. The yesterday
  question remains available, including at depth 1. Before writing, the service re-queries core eligibility so
  stale rows cannot log a satisfied weekly schedule, a day before creation, or a day outside the window.
  Duplicate logging is refused by core. Failed/read-only saves and history scrubbing remain protected.
- Both paths now pass the saved garden-day boundary to core. Previously the question used 03:00 even when the
  save used 06:00. Updated the existing .NET boundary regression to verify that the question is still offered at
  03:00 with a 06:00 boundary, accepts the late log, and then stops offering the kept day. Approved public surface
  changes are the two additive service methods only.
- The card consumes Pinch/PalmOpen through the existing input source. Competing intent handlers are blocked while
  it is open and during the closing frame, so the closing palm does not also dismiss the dial. Dial redraws keep
  plant and tile colliders disabled beneath the card. The existing modal restores competing invitations on exit.
- Shared runtime packages are unchanged. No core rule, input provider, package version, generated art asset,
  audio, or scene changed. No direct keyboard/mouse reads were added.

Read the worktree AGENTS.md, the read-only task from the main checkout, upgrade-step-b's common section and B8,
T-SUN-058's report, upgrade context, decisions 0015/0016, Sundial style framing, and the art references. The task
file was not edited or moved. All work ran inside this worktree.

## Acceptance and evidence

The final authoritative Unity results are `editmode.xml` and `playmode.xml`. XML totals were checked, not just
process exit codes. All seven new PlayMode cases passed:

```text
CatchUp_TwoDaysAway_OffersTwoDays             Passed
CatchUp_Logged_IsHatchedLate                 Passed
CatchUp_FirstVisit_ShowsNothing              Passed
CatchUp_DepthOne_QuestionAlsoLogsLate         Passed
CatchUp_Pages_Pause_AndBackgroundIntents      Passed
CatchUp_WeeklyTarget_RefreshesRemainingRows   Passed
CatchUp_DayRollover_RefreshesStaleRows         Passed
```

These check both logging paths, shader tile state 2 (late hatch), disk persistence and reload, duplicate pinches,
deferred visit persistence, same-day relaunch, first-visit suppression, Done/PalmOpen without adding tends,
18 offers across nine habits, next/previous navigation, pause preservation, blocked underlying interactions,
weekly-target refresh, and stale rows after rollover. Capture tests raycast every visible row/navigation target
from the DialG1 camera and require the intended collider to be hit. Existing backfill tests also pass.

Captures, each at the existing 1824 x 1024 DialG1 framing:

- `DialG1-catch-up.png`: two days, three habits, six pinchable rows.
- `DialG1-catch-up-nine-habits.png`: the middle page of an 18-row offer, with Previous, Next and Done.
- `DialG1-catch-up-late.png`: card dismissed after one late log; the morning tile is visibly hatched.
- The three corresponding `*-reference.jpg` files compare untouched full-frame captures with
  `shared/assets/art-reference/A2-05-field-notebook-1.png`.
- `catch-up-artboard.jpg` compares the paper presentation with the Field Notebook reflection artboard. There is
  no separate B8 catch-up artboard; this is the existing paper-card style reference, not an exact B8 layout target.
- `make-comparisons.py` reproduces the four boards without crops or reframing.

Visually inspected the card, navigation, late-tile frame and references. The final card fits the frame and its ink
stays inside the paper. It uses the established paper texture/font and leaves the room photographic. The garden's
existing plant/soil fidelity gap and the competing invitations visible after card dismissal remain; this task
does not claim a new art-gate score. No standalone manual mouse session or video was run; interactions and captures
were verified in PlayMode and the release Windows player was built.

## Commands and real output

All Unity calls ran sequentially in the foreground with `Start-Process -Wait`; each process finished and its log
was read before the next invocation. There are no Unity Editor processes left (`process-check.txt`).

```powershell
$env:DOTNET_ROLL_FORWARD='Major'
# Approve the two additive service methods once, then run without approval enabled.
$env:GARDENVR_APPROVE_SURFACE='1'
dotnet test shared/core-dotnet/GardenVR.SundialModel.Tests --filter FullyQualifiedName~PublicSurfaceTests
Remove-Item Env:GARDENVR_APPROVE_SURFACE
dotnet test shared/core-dotnet/GardenVR.sln --logger trx --results-directory orchestration/runs/sundial/T-SUN-065/dotnet-results 2>&1 | Tee-Object orchestration/runs/sundial/T-SUN-065/dotnet.log
```

Approval test: Passed 1, Failed 0. Final solution run, exit 0:

```text
Passed!  - Failed: 0, Passed: 14, Skipped: 0, Total: 14, Duration: 26 ms - GardenVR.TerrariumModel.Tests.dll (net9.0)
Passed!  - Failed: 0, Passed: 11, Skipped: 0, Total: 11, Duration: 58 ms - GardenVR.CoachCli.Tests.dll (net9.0)
Passed!  - Failed: 0, Passed: 58, Skipped: 0, Total: 58, Duration: 321 ms - GardenVR.SundialModel.Tests.dll (net9.0)
Passed!  - Failed: 0, Passed: 460, Skipped: 0, Total: 460, Duration: 828 ms - GardenVR.Core.Tests.dll (net9.0)
```

Four TRX XML files are committed in `dotnet-results/`.

```powershell
$unity='C:/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Unity.exe'
$repo='C:/Users/kazda/kiro/gvr-sundial'
$runDir="$repo/orchestration/runs/sundial/T-SUN-065"
Start-Process -Wait -PassThru -WindowStyle Hidden -FilePath $unity -ArgumentList "-batchmode -nographics -projectPath $repo/apps/sundial -runTests -testPlatform EditMode -testResults $runDir/editmode.xml -logFile $runDir/editmode.log"
Start-Process -Wait -PassThru -WindowStyle Hidden -FilePath $unity -ArgumentList "-batchmode -projectPath $repo/apps/sundial -runTests -testPlatform PlayMode -testResults $runDir/playmode.xml -logFile $runDir/playmode.log"
Start-Process -Wait -PassThru -WindowStyle Hidden -FilePath $unity -ArgumentList "-batchmode -nographics -quit -projectPath $repo/apps/sundial -executeMethod GardenVR.Sundial.Editor.Build.Windows -logFile $runDir/build.log"
```

Each returned exit 0. Parsed XML and log checks (`verification.txt`):

```text
editmode result=Passed total=98 passed=98 failed=0 skipped=0
playmode result=Passed total=89 passed=89 failed=0 skipped=0
editmode compiler/shader/build error entries=0
playmode compiler/shader/build error entries=0
build compiler/shader/build error entries=0
Build Succeeded C:\Users\kazda\kiro\gvr-sundial\apps\sundial\Build\sundial\Sundial.exe
```

The player stays at the ignored build path. `verification.txt` includes its size and SHA-256.

```powershell
python orchestration/runs/sundial/T-SUN-065/make-comparisons.py
python tools/assets/lossless.py --ingest --apply orchestration/runs/sundial/T-SUN-065/DialG1-catch-up.png orchestration/runs/sundial/T-SUN-065/DialG1-catch-up-late.png orchestration/runs/sundial/T-SUN-065/DialG1-catch-up-nine-habits.png
git diff --check
```

```text
Wrote four side-by-sides; full 1824 x 1024 panels, no crop or reframing.
total 7911704 -> 6527834, saved 1383870 bytes; proof failures 0; applied
```

All three PNG proofs passed (full per-image output in `lossless.txt`). Image check JSON preserves the original
capture PNG hash and records the final ingested hash; image statistics and pixels are unchanged. All three report
magenta fraction 0. The code diff passed `git diff --check`. The evidence staging check found duplicated carriage
returns in two captured text summaries and one trailing space in a PlayMode output line; these were normalized
in a follow-up evidence commit, with test outcomes and raw logs unchanged. The final staged diff check passed.

## Development failures and cleanup

- Initial .NET run (`dotnet-initial.log`): 2 failures in 58 Sundial model tests. One was the expected public surface
  mismatch for `LogCatchUp`/`PendingCatchUp`; the other was `Backfill_closes_at_the_default_boundary_instant`,
  `Assert.False() Failure; Expected: False; Actual: True`. The latter pinned the old 03:00/saved-boundary mismatch.
  The replacement test verifies logging against the saved boundary and duplicate prevention as described above.
- Initial filtered Unity invocation (`catchup-compile-failure.log`) exited 1:
  `CatchUpTests.cs(45,66): error CS7036: There is no argument given that corresponds to the required parameter 'boundary' of 'GardenDay.From(DateTimeOffset, TimeSpan)'`.
  Fixed the fixture to pass `GardenDay.DefaultBoundary`.
- First filtered PlayMode result: 6 passed, 1 failed (`catchup-layout-failure.xml`/`.log`):
  `catchup.previous; Expected: in range (0.01,0.99); But was: -0.11404103f`. Repositioned/scaled the card and enlarged
  its paper backing so all content and navigation fit.
- Second filtered result: 5 passed, 2 failed (`catchup-occlusion-failure.xml`/`.log`):
  `Expected: "catchup.row.9770.habit0"; But was: "plant.morning"` and
  `Expected: "catchup.row.9771.habit1"; But was: "plant.morning.r2"`. Rendering re-enabled underlying plant
  colliders. Fixed the dial's collider update, then the full PlayMode suite passed, including both raycast checks.
- Filtered invocations used the PlayMode command above with
  `-testFilter GardenVR.Sundial.Tests.PlayMode.CatchUpTests -testResults $runDir/catchup.xml -logFile $runDir/catchup.log`.
  Failed XML files are explicitly named; `playmode.xml` is the final acceptance run.
- Final logs retain pre-existing obsolete/inherited API and licensing diagnostics, plus build-shutdown
  `Internal: There are remaining Allocations on the JobTempAlloc.` No compiler, shader or build errors were found.
- Restored 50 tracked Unity/test-generated material, URP setting and older evidence files that were clean at task
  start. The initially untracked plant candidates, root comparison images and scratch directory remain untouched.
  Build binaries, Library/Temp, secrets and raw logs are not committed. Per AGENTS.md, all raw `.log` files remain
  here on disk untracked; relevant errors/output are quoted above.

## User-facing strings for B12

Also recorded in `user-facing-strings.txt`:

```text
What happened since?
Pinch what happened. Leave the rest.
All noted
Previous
Next
Done
```

Dynamic row format: `{date: ddd d MMM}   {existing habit display name}`, for example `Thu 1 Oct   Drink water`.
Dates use invariant English weekday/month names; habit names use the existing `WeekPageInk.PlantLine` formatter.
The static census has zero matches against CoachGuards' medical, consecutive-count and guilt lists, and no em dash
(`strings-check.txt`). No medical, guilt, or consecutive-day-count copy was added.

No blockers remain. Next: B10 can expose the persisted depth setting; B12 can consume this string census and these
captures. Manual PC usability review and owner art scoring remain broader gate work, not claimed as run here.
