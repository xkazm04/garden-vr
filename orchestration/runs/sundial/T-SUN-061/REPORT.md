# T-SUN-061: B4 coach clients

Completed 2026-10-12 in `C:/Users/kazda/kiro/gvr-sundial`, branch `agent/sundial`. The assigned task in the main worktree was read only.

The relay client, Windows editor CLI client and save-backed opt-in entry point are implemented. All 522 .NET tests, 97 Sundial EditMode tests and 68 Sundial PlayMode tests pass. The Windows player builds, and a metadata audit confirms that its six Garden VR assemblies contain no CLI client types or process-spawning references. No blockers remain.

## Implementation and integration

- `RelayCoachClient` POSTs UTF-8 `RelayProtocol.Encode` to `RelayProtocol.Path`, with JSON content type and the save's `InstallKeyHeader`. Replies go through `RelayProtocol.Decode`. Cancellation aborts and disposes the request on Unity's thread, including cancellation initiated by CoachFlow's timeout continuation. Redirects are disabled. HTTPS and loopback HTTP are supported; no prompt, reply or install key is logged.
- `RelayConfig` is a ScriptableObject with a blank URL and a four-second default budget. Invalid budgets fall back to `CoachFlow.Budget`. The committed player resource has no endpoint. `Assets/Editor/Coach/RelayConfigLocal.asset` has only `http://localhost:8787` and a 15-second budget for the local CLI-backed relay. The Windows editor loads that local asset by default; players load `Resources/RelayConfig`.
- `EditorCliCoachClient` ports the existing CoachCli process path to Unity's Mono APIs, entirely inside `#if UNITY_EDITOR_WIN`. It launches native `claude.exe`, uses Haiku and the core system prompt, disables tools/settings/session persistence, passes user text only through stdin, drains stdout/stderr, and kills the process on cancellation. It uses Windows argument quoting because Unity's Mono needs the `Arguments` path. It strips `ANTHROPIC_API_KEY` from the child environment and never logs content. Tests substitute its process runner; an inert PowerShell child separately verifies real process I/O and cancellation.
- `SundialController.Coach` exposes `SundialCoach.OnboardAsync` and `ReflectAsync` for B5/B7. It reads the current service save at each call. With `CoachOptIn == false`, no client is constructed and CoachFlow receives null. After save replacement/reset it reads the replacement's opt-in and install key. Core still owns guards, proposals, scripted lines and timeout behavior. No shared package or .NET model code changed.
- Set `UseEditorCli` on the local editor config to use the direct CLI instead of the relay. Calls must originate on Unity's main thread. `CoachConfig` can override the controller config before its first coach access. The transport returns the core result; B5/B7 add its presentation and moment lifecycle.
- Unity's insecure HTTP setting is now Development Only so localhost tests and development players can reach the local relay. Release builds should use HTTPS. For B11's localhost player check, configure the player resource locally with the localhost URL/15-second budget and use the existing `Build.WindowsDevelopment` entry point. No production URL is committed.
- The player audit uncovered the pre-existing FFmpeg `FirstRunRecorder`, documented as editor-only but compiled into players. Added its missing `UNITY_EDITOR` guard. Editor recording behavior is unchanged; the recorder is excluded from players.

Implementation commits: `94f2fc9` (coach clients, gate, configs and tests), `1b99a63` (exclude the recorder from players). This report and evidence are committed separately. Nothing was pushed.

## Commands and real results

All commands ran in this worktree. Unity ran sequentially in the foreground with `Start-Process -Wait`; each process finished and its log was read. XML totals and failures were checked independently of exit codes.

```powershell
$env:DOTNET_ROLL_FORWARD='Major'
dotnet test shared/core-dotnet/GardenVR.sln 2>&1 |
    Tee-Object orchestration/runs/sundial/T-SUN-061/dotnet.log
```

Exit 0, trimmed output:

```text
Passed!  - Failed:     0, Passed:    14, Skipped:     0, Total:    14, Duration: 21 ms - GardenVR.TerrariumModel.Tests.dll (net9.0)
Passed!  - Failed:     0, Passed:    52, Skipped:     0, Total:    52, Duration: 327 ms - GardenVR.SundialModel.Tests.dll (net9.0)
Passed!  - Failed:     0, Passed:    11, Skipped:     0, Total:    11, Duration: 70 ms - GardenVR.CoachCli.Tests.dll (net9.0)
Passed!  - Failed:     0, Passed:   445, Skipped:     0, Total:   445, Duration: 983 ms - GardenVR.Core.Tests.dll (net9.0)
```

```powershell
$repo = 'C:/Users/kazda/kiro/gvr-sundial'
$runDir = "$repo/orchestration/runs/sundial/T-SUN-061"
$unity = 'C:/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Unity.exe'
Start-Process -Wait -PassThru -WindowStyle Hidden -FilePath $unity -ArgumentList "-batchmode -nographics -projectPath $repo/apps/sundial -runTests -testPlatform EditMode -testResults $runDir/editmode.xml -logFile $runDir/editmode.log"
Start-Process -Wait -PassThru -WindowStyle Hidden -FilePath $unity -ArgumentList "-batchmode -projectPath $repo/apps/sundial -runTests -testPlatform PlayMode -testResults $runDir/playmode.xml -logFile $runDir/playmode.log"
Start-Process -Wait -PassThru -WindowStyle Hidden -FilePath $unity -ArgumentList "-batchmode -nographics -quit -projectPath $repo/apps/sundial -executeMethod GardenVR.Sundial.Editor.Build.Windows -logFile $runDir/build.log"
```

All returned exit 0. The build command ran twice, before and after the recorder guard. The full editor suites ran before that guard; it leaves editor code unchanged. The final build compiled both final editor and player code.

```text
editmode result=Passed total=97 passed=97 failed=0 skipped=0
playmode result=Passed total=68 passed=68 failed=0 skipped=0
editmode compiler/shader/build errors=0
playmode compiler/shader/build errors=0
build compiler/shader/build errors=0
UnityProjectProcesses=0
Build Succeeded C:\Users\kazda\kiro\gvr-sundial\apps\sundial\Build\sundial\Sundial.exe
```

Required PlayMode cases:

```text
Coach_OffByDefault_ScriptedPathCompletes   Passed  0.008241 s
Coach_RelayDown_FallsBackWithinBudget     Passed  0.207606 s
Coach_Refused_ShowsScriptedLine           Passed  0.001333 s
```

The off/default test completes both onboarding and reflection and asserts zero client construction. The refusal test checks the exact core scripted line and onboarding proposals. The budget test uses a fake client that never answers, a 0.2-second configured budget, and asserts cancellation and completion within a scheduler allowance. This is deterministic timeout coverage, not a live latency measurement.

Five additional PlayMode cases verify opt-in/key re-reading, the exact HTTP path/header/UTF-8 body and successful decoding, refusal/malformed replies, pre-cancellation, and a loopback server that accepts a request but never replies. That last test observes socket disconnection after the 0.5-second budget (`0.521581 s` test duration), proving abort rather than just abandoning a task. Servers use ephemeral loopback ports. No PlayMode test calls the live relay or Claude.

Thirteen new EditMode cases cover config defaults/local budgets, CLI isolation/stdin, Windows quoting, failure decoding, pre-cancellation, and process I/O/cancellation. All pass.

```powershell
dotnet run --project orchestration/runs/sundial/T-SUN-061/PlayerAudit -- apps/sundial/Build/sundial/Sundial_Data/Managed 2>&1 |
    Tee-Object orchestration/runs/sundial/T-SUN-061/player-audit.txt
```

Final exit 0, trimmed output:

```text
PASS GardenVR.Sundial.dll: no CLI type or Process/ProcessStartInfo reference; SHA256=058468971edda59025fa5dabdfdc9bb0ab8ff1969416975b2e3c107ab2067c3e
PASS audited 6 application/package assemblies; RelayCoachClient, SundialCoach, RelayConfig present.
```

The audit reads managed assembly metadata, asserts the runtime coach types exist, rejects CLI types and `System.Diagnostics.Process`/`ProcessStartInfo` references, and records every inspected assembly's hash. It checks app/package code, not the .NET runtime's own library definitions.

## Failure found and fixed

The first audit returned exit 1 after the successful initial player build:

```text
Unhandled exception. System.Exception: Process-spawning reference in GardenVR.Sundial.dll: System.Diagnostics.Process
at Program.<Main>$(String[] args) in .../PlayerAudit/Program.cs:line 29
```

`rg -n 'Process|Diagnostics' apps/sundial/Assets/Scripts` traced this to the pre-existing `FirstRunRecorder` calling `Process.Start(psi)` for FFmpeg. Its editor guard fixed the audit without weakening it. The final build and audit then passed. The initial output remains in `player-audit-initial.log`, and the initial build log in `build-initial.log`.

No test failed. Logs retain nonblocking Unity licensing access-token diagnostics and the existing build-shutdown `JobTempAlloc` allocation warnings. Compiler, shader and build error scans found zero errors. Staging whitespace checks found a trailing space after the blank YAML URL and in an existing test's diagnostic output inside PlayMode XML; trimmed those spaces before committing. XML was re-parsed afterward: Passed, total 68, failed 0. Raw logs remain unchanged. Final whitespace checks passed.

## Evidence, scope and next work

- Committed here: `editmode.xml`, `playmode.xml`, `verification.txt`, `player-audit.txt`, and the reproducible `PlayerAudit/Program.cs` and `.csproj`.
- Raw logs remain locally, untracked: `dotnet.log`, `editmode.log`, `playmode.log`, `build.log`, `build-initial.log`, `player-audit-initial.log`. Relevant lines are quoted above as required by AGENTS.md.
- The ignored Windows build remains at `apps/sundial/Build/sundial/Sundial.exe`. `verification.txt` records its size/hash; `player-audit.txt` records the application assembly hashes.
- Restored only generated Unity material/URP/volume changes and the older B2/B3 evidence files regenerated by their existing PlayMode tests. Initial untracked plant candidates, root comparison images and scratch files were left untouched. No build products, audit `bin/obj`, raw logs, secrets or unrelated files were committed.
- Captures, side-by-sides and videos: not applicable. B4 specifies no capture and changes no visuals. No PNG or generated art was added or changed.
- User-facing strings added for B12's census: **none**. Inspector labels, transport protocol text and test fixtures are not player copy. Scripted lines remain the existing core lines.
- Live Claude/relay calls, manual player interaction and latency sampling: **not run**. B11 owns those checks. B5/B7 can now call `controller.Coach` and render the returned core results; no new UI or opt-in interaction was part of B4.

Termination: finished, with no outstanding B4 acceptance checks.
