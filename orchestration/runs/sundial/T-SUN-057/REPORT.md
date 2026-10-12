# T-SUN-057: B0 compile debt verification

Completed 2026-10-12 in `C:/Users/kazda/kiro/gvr-sundial`, on `agent/sundial`.

Both apps compile against the upgrade core with zero compiler errors. All four existing Unity suites pass, all 505 .NET tests pass, and the Sundial Windows player builds. No application, core, test, metadata, or behavior changes were necessary. This commit records verification evidence only.

## Scope and starting revision

- Read `AGENTS.md`, the read-only task at `C:/Users/kazda/kiro/garden-vr/orchestration/running/T-SUN-057-b0-compile-debt.md`, upgrade-step-b's common instructions and B0, upgrade context, and decisions 0015/0016.
- Starting `HEAD` and local `main`: `8fb98ff`. The upgrade branch tip `origin/claude/laughing-dijkstra-aaicvq` (`97801dc`) was already merged by `adf75a0`. `git merge-base --is-ancestor origin/claude/laughing-dijkstra-aaicvq HEAD` returned 0; `git log --oneline HEAD..main` was empty. No additional merge was needed.
- The tracked worktree was initially clean. Pre-existing untracked plant candidates, root comparison images, and `scratch/` were left untouched.
- Used Unity `6000.6.4f1`, URP `17.6.0`, and Input System `1.20.0` as configured. No package/API-level changes.
- All Unity invocations ran sequentially through foreground PowerShell `Start-Process -Wait`; each process finished and its log was read before the next invocation. No Unity project editor remains running.

## Results

| Check | Total | Passed | Failed | Skipped | Evidence |
|---|---:|---:|---:|---:|---|
| .NET core | 442 | 442 | 0 | 0 | `dotnet.log` |
| .NET Sundial model | 38 | 38 | 0 | 0 | `dotnet.log` |
| .NET Terrarium model | 14 | 14 | 0 | 0 | `dotnet.log` |
| .NET Coach CLI | 11 | 11 | 0 | 0 | `dotnet.log` |
| Sundial EditMode | 75 | 75 | 0 | 0 | `sundial-editmode.xml`, `.log` |
| Sundial PlayMode | 49 | 49 | 0 | 0 | `sundial-playmode.xml`, `.log` |
| Terrarium EditMode, graphics enabled | 55 | 55 | 0 | 0 | `terrarium-editmode.xml`, `.log` |
| Terrarium PlayMode | 49 | 49 | 0 | 0 | `terrarium-playmode.xml`, `.log` |

The four final XML roots were parsed and checked for `result=Passed`, `total>0`, and `failed=0`. Their combined total is 228 passing Unity tests. Both dedicated compile passes exited 0; compiler/shader error scans of `sundial-compile.log`, `terrarium-compile.log`, and `build.log` returned zero entries. `verification.txt` records these checks and player hashes.

All 42 core runtime C# files have accompanying `.meta` files. Unity did not rewrite core metadata. Both projects' generated `GardenVR.Core.rsp` files include `Runtime/Coach/CoachFlow.cs`, reference `NetStandard/ref/2.1.0/netstandard.dll`, and include the `System.Threading.Tasks` shims. Both actual Unity compiles succeeded, confirming the async coach code compiles at the existing API level. Shared packages remain unchanged and backward compatible.

## Commands and real output

Working directory for all commands: `C:/Users/kazda/kiro/gvr-sundial`. Absolute Unity output paths avoided project-relative log/result placement.

```powershell
$env:DOTNET_ROLL_FORWARD='Major'
dotnet test shared/core-dotnet/GardenVR.sln 2>&1 |
    Tee-Object -FilePath orchestration/runs/sundial/T-SUN-057/dotnet.log
```

Trimmed actual output, exit 0:

```text
Passed!  - Failed:     0, Passed:    14, Skipped:     0, Total:    14, Duration: 31 ms - GardenVR.TerrariumModel.Tests.dll (net9.0)
Passed!  - Failed:     0, Passed:    11, Skipped:     0, Total:    11, Duration: 75 ms - GardenVR.CoachCli.Tests.dll (net9.0)
Passed!  - Failed:     0, Passed:    38, Skipped:     0, Total:    38, Duration: 309 ms - GardenVR.SundialModel.Tests.dll (net9.0)
Passed!  - Failed:     0, Passed:   442, Skipped:     0, Total:   442, Duration: 928 ms - GardenVR.Core.Tests.dll (net9.0)
```

Unity command arguments below are exactly those run, with the common paths factored into variables for readability. Each invocation also used `-PassThru`; after waiting, its exit code, log tail, and (for tests) parsed XML root were printed.

```powershell
$unity = 'C:/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Unity.exe'
$repo = 'C:/Users/kazda/kiro/gvr-sundial'
$runDir = "$repo/orchestration/runs/sundial/T-SUN-057"

Start-Process -Wait -PassThru -WindowStyle Hidden -FilePath $unity -ArgumentList "-batchmode -nographics -quit -projectPath $repo/apps/sundial -logFile $runDir/sundial-compile.log"
Start-Process -Wait -PassThru -WindowStyle Hidden -FilePath $unity -ArgumentList "-batchmode -nographics -projectPath $repo/apps/sundial -runTests -testPlatform EditMode -testResults $runDir/sundial-editmode.xml -logFile $runDir/sundial-editmode.log"
Start-Process -Wait -PassThru -WindowStyle Hidden -FilePath $unity -ArgumentList "-batchmode -projectPath $repo/apps/sundial -runTests -testPlatform PlayMode -testResults $runDir/sundial-playmode.xml -logFile $runDir/sundial-playmode.log"

Start-Process -Wait -PassThru -WindowStyle Hidden -FilePath $unity -ArgumentList "-batchmode -nographics -quit -projectPath $repo/apps/terrarium -logFile $runDir/terrarium-compile.log"
# Initial attempt; preserved afterward under terrarium-editmode-nographics.*:
Start-Process -Wait -PassThru -WindowStyle Hidden -FilePath $unity -ArgumentList "-batchmode -nographics -projectPath $repo/apps/terrarium -runTests -testPlatform EditMode -testResults $runDir/terrarium-editmode.xml -logFile $runDir/terrarium-editmode.log"
# Full rerun with graphics for the rendered contrast test:
Start-Process -Wait -PassThru -WindowStyle Hidden -FilePath $unity -ArgumentList "-batchmode -projectPath $repo/apps/terrarium -runTests -testPlatform EditMode -testResults $runDir/terrarium-editmode.xml -logFile $runDir/terrarium-editmode.log"
Start-Process -Wait -PassThru -WindowStyle Hidden -FilePath $unity -ArgumentList "-batchmode -projectPath $repo/apps/terrarium -runTests -testPlatform PlayMode -testResults $runDir/terrarium-playmode.xml -logFile $runDir/terrarium-playmode.log"

Start-Process -Wait -PassThru -WindowStyle Hidden -FilePath $unity -ArgumentList "-batchmode -nographics -quit -projectPath $repo/apps/sundial -executeMethod GardenVR.Sundial.Editor.Build.Windows -logFile $runDir/build.log"
```

Compile output excerpts:

```text
# Sundial
*** Tundra build success (39.40 seconds), 358 items updated, 785 evaluated
Exiting batchmode successfully now!
# Terrarium
*** Tundra build success (44.30 seconds), 391 items updated, 851 evaluated
Exiting batchmode successfully now!
```

Final XML verification output:

```text
sundial-editmode result=Passed total=75 passed=75 failed=0 skipped=0
sundial-playmode result=Passed total=49 passed=49 failed=0 skipped=0
terrarium-editmode result=Passed total=55 passed=55 failed=0 skipped=0
terrarium-playmode result=Passed total=49 passed=49 failed=0 skipped=0
sundial-compile compiler/shader error entries=0
terrarium-compile compiler/shader error entries=0
build compiler/shader error entries=0
CoreSourceFiles=42 MissingMetadata=0
```

Build output, exit 0:

```text
Build Succeeded C:\Users\kazda\kiro\gvr-sundial\apps\sundial\Build\sundial\Sundial.exe
```

The player and its data are retained locally at `apps/sundial/Build/sundial/`, excluded from Git. The executable is 667136 bytes. SHA256 for the executable, `GardenVR.Core.dll`, and `GardenVR.Sundial.dll` is in `verification.txt`. Player launch/manual play was not run; acceptance requires a successful build, which was run.

## Failure, warnings, and cleanup

The first Terrarium EditMode attempt used the common `-nographics` command. It exited 2 with 54 passed and one failed test. Preserved unmodified in `terrarium-editmode-nographics.xml` and `.log`:

```text
GardenVR.Terrarium.Tests.EtchContrastTests.Etch_RenderedContrast_AtLeastFourPointFive
  the etched line did not render any glyph pixels
  Expected: greater than 40
  But was:  0
at GardenVR.Terrarium.Tests.EtchContrastTests.Etch_RenderedContrast_AtLeastFourPointFive () [0x0017a] in C:\Users\kazda\kiro\gvr-sundial\apps\terrarium\Assets\Tests\EditMode\EtchContrastTests.cs:40
```

The test calls a camera-based render measurement, so graphics are required. Rerunning the full suite with graphics, without changing any test or runtime code, passed 55/55. Its existing contrast evidence reported `contrast=10.47`, `hits=1865`, `minimum=4.5`, `font=CormorantGaramond SDF`.

Non-blocking diagnostics remain in the full logs: unused events (CS0067), inherited-member hiding (CS0108), Sundial's obsolete FindObjectsByType overloads (CS0618), shader `pow` warnings, Unity licensing access-token refresh diagnostics despite successful entitlement resolution, and build-shutdown JobTempAlloc warnings. None caused a compile/build error or a final test failure. This task does not expand into warning cleanup or behavior changes.

Unity test/build serialization dirtied materials and rendering settings. Inspected and restored only those run-generated changes: ten Sundial materials; sixteen Terrarium materials and its GraphicsSettings/QualitySettings; Sundial's generated default volume profile, URP prefilter settings, and global runtime settings. No `.meta` changes occurred. Before staging evidence, `git diff --exit-code` returned 0. The initial untracked files remain unmodified.

## Evidence and next step

All compile/test/build logs, all five test result XML files (including the failed invocation), `dotnet.log`, and `verification.txt` are beside this report. They are explicitly included despite the repository's normal ignore rules for orchestration evidence. No `Library/`, `Temp/`, `Logs/`, build binaries, secrets, or unrelated files are committed.

B0 names no visual capture or comparison frame, and there is no visual change. No new app captures, side-by-sides, generated art, or PNG files are committed. The existing Terrarium contrast test also wrote its customary local sample to `orchestration/runs/terrarium/T-TER-024/contrast-sample.png`; that incidental test artifact is not part of this commit.

User-facing strings added for B12's census: **none**.

No blockers remain. Next: B1 save-state adoption, or the independently scheduled B3 speech seam. Future Terrarium EditMode runs must enable graphics for the contrast test.
