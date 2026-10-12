# T-SUN-068: B11 relay live check on CLI

Status: complete

Executed on 2026-10-12 in `C:/Users/kazda/kiro/gvr-sundial`, branch `agent/sundial`. The assigned task in the main worktree was read only. No push, API backend, API-key setup, shared-package change, image generation, or headset/device operation was performed.

## Live player evidence

The Windows development player checked `http://localhost:8787/healthz`: `{"ok":true,"backend":"cli"}`. Its transient development `RelayConfig` used localhost and a 15-second budget. The normal committed player resource still has a blank URL. The relay used the installed Claude Code subscription CLI with model `claude-haiku-5-5`.

The probe used a new isolated temporary save and synthetic speech delivered through `ISpeechToText`, with `IHandIntentSource` Pinch/PinchHold/Release intents. It selected the real online-coach consent choice, answered all four interview questions, received three model proposals, checked the rendered packet text against those proposals, planted one and completed the first tend. It then opened the existing reflection controller, delivered a synthetic reflection, and checked that the model line appeared on the card and matched the saved journal. This is an automated rendered Windows-player check, not a manual microphone/dictation check or a full dusk ritual test.

Content-free player evidence:

```text
[RelayLive] backend=cli budget_ms=15000 speech=synthetic transport=UnityWebRequest
[RelayLive] live_onboarding from_model=true packet_text_matches=true proposal_count=3
[RelayLive] live_reflection from_model=true card_text_matches=true journal_matches=true
```

Both captures were opened and visually inspected. `live-onboarding.png` shows the model's three packets, including “Short walk after breakfast” and “One page of reading”. `live-reflection.png` shows “You walked after breakfast and read a page this evening, a quiet day well kept.” The synthetic input is a test fixture; no owner's spoken text was captured. These screenshots document existing UI, with no art/layout changes, so an art-reference comparison was not applicable. No video was requested or recorded.

## Timing and privacy

**backend=cli: p50 4.659 s, p95 6.334 s.** Onboarding: p50 4.667 s, p95 5.201 s; reflection: p50 4.600 s, p95 7.168 s. All 20 requests returned HTTP 200 with no relay failure; 18 model replies passed the app guards. Samples 10 and 12 were reflection guard rejections and used the scripted fallback (`FromModel=false`, `CoachFailure.Error`). Their relay responses were successful, so the rejection is at `CoachPrompts.ParseReflection`/`CoachGuards`; the specific guard condition cannot be recovered without retaining reply text, which this run did not do. No timeout or rate limit occurred.

See `timing-table.md` for all 20 timed calls (10 onboarding, 10 reflection), per-kind statistics, model/fallback outcomes, and matching relay durations. `timings.csv` is written by the Windows player. The two preceding live UI calls are excluded from this sample. All calls share one install key, with starts at least 11 seconds apart to respect the relay limiter.

The player timer covers SundialCoach/CoachFlow through completion observation, including UnityWebRequest, model time, guards and frame granularity. p50 is the median (mean of the middle two values); p95 is nearest rank (19th of 20). All outcomes remain in the sample. This is a repeated synthetic-fixture local CLI measurement, not a diverse prompt evaluation or an API gate pass.

`relay-lines.jsonl` contains only startup metadata plus request time, kind, HTTP status, failure enumeration, duration and token counts. `verify.py` checks allowed fields, expected call counts, backend, empty relay stderr, player evidence and test XML. No prompts, user/reply text, install keys or credentials appear in the relay logs. Raw logs remain local and untracked.

The API p95 <= 4 seconds gate is **needs-owner**, as required: it has not been measured and awaits the owner's switch to `api`. No API key was requested or configured.

## Changes

- `RelayLiveCheck.cs`: an explicitly invoked development-player probe (`--relay-live-check <directory>`), compiled out of release players. It uses a transient localhost config and a separate temporary save, asserts both live UI results, captures them, writes 20 timed outcomes, and exits with a status code. It does not read keyboard/mouse APIs or change core rules.
- `FirstRunWizard.InterviewFromModel`: exposes the existing core result's provenance, reset before a new request, to verify the displayed proposal source. No user-facing strings or rendering behavior changed.
- `run-live.ps1`: starts the CLI relay with `Start-Process`, checks health, waits for the graphical player in the foreground and stops the relay in `finally`. It refuses to replace an existing listener on port 8787.
- `verify.py`, the timing/evidence files, `docs/research/coach-eval/README.md`, and updated local-run instructions in `tools/relay/README.md`.

## Commands and verification

Commands ran in this worktree. Every Unity invocation used `Start-Process -Wait`; no two editor instances opened this project together. Logs were read after completion and test XML was inspected.

```powershell
# WorkingDirectory: tools/relay
npm ci
npm test
# added 7 packages, audited 8 packages; found 0 vulnerabilities
# tests 14; pass 14; fail 0; skipped 0; exit 0

$env:DOTNET_ROLL_FORWARD='Major'
dotnet test shared/core-dotnet/GardenVR.sln --logger trx --results-directory orchestration/runs/sundial/T-SUN-068/dotnet-results
# Core 462, SundialModel 60, TerrariumModel 14, CoachCli 11
# 547 passed, 0 failed; exit 0

$repo='C:/Users/kazda/kiro/gvr-sundial'
$run="$repo/orchestration/runs/sundial/T-SUN-068"
$unity='C:/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Unity.exe'
Start-Process -Wait -PassThru -WindowStyle Hidden -FilePath $unity -ArgumentList "-batchmode -nographics -projectPath $repo/apps/sundial -runTests -testPlatform EditMode -testResults $run/editmode.xml -logFile $run/editmode.log"
# Passed: total 98, passed 98, failed 0; Unity exit 0

Start-Process -Wait -PassThru -WindowStyle Hidden -FilePath $unity -ArgumentList "-batchmode -projectPath $repo/apps/sundial -runTests -testPlatform PlayMode -testResults $run/playmode.xml -logFile $run/playmode.log"
# Initial: total 94, passed 93, failed 1; Unity exit 2
# Preserved as playmode-initial.xml and playmode-initial.log

Start-Process -Wait -PassThru -WindowStyle Hidden -FilePath $unity -ArgumentList "-batchmode -projectPath $repo/apps/sundial -runTests -testPlatform PlayMode -testFilter GardenVR.Sundial.Tests.PlayMode.MonthlyRecordTests.NoConsecutiveCountString -testResults $run/playmode-recheck.xml -logFile $run/playmode-recheck.log"
# Passed: total 1, passed 1, failed 0; Unity exit 0

Start-Process -Wait -PassThru -WindowStyle Hidden -FilePath $unity -ArgumentList "-batchmode -nographics -quit -projectPath $repo/apps/sundial -executeMethod GardenVR.Sundial.Editor.Build.WindowsDevelopment -logFile $run/build.log"
# Unity exit 0; Build Succeeded .../Build/sundial-development/Sundial.exe

& orchestration/runs/sundial/T-SUN-068/run-live.ps1
# PlayerExit=0; complete samples=20 backend=cli; RelayStopped=True
python orchestration/runs/sundial/T-SUN-068/verify.py
# PASS relay log schema, live UI model assertions, backend and 20-call sample
# p50_ms=4658.616 p95_ms=6333.906
python tools/assets/lossless.py --ingest --apply orchestration/runs/sundial/T-SUN-068/live-onboarding.png orchestration/runs/sundial/T-SUN-068/live-reflection.png
# onboarding 2287067 -> 1864032; reflection 2196958 -> 1766865
# proof failures 0; exit 0
```

`commands-summary.txt` records the command outcomes and player executable/application assembly SHA256 hashes. The standalone build stays ignored on disk.

Committed evidence: this report, `health.json`, `timings.csv`, `timing-table.md`, `relay-lines.jsonl`, `player-excerpts.txt`, `verification.txt`, `commands-summary.txt`, the two PNG captures, both runner/verifier scripts, all three Unity results XML files (including the initial failure), and four .NET TRX files. Raw `build.log`, `dotnet.log`, `editmode.log`, `playmode-initial.log`, `playmode-recheck.log`, `player.log`, `relay.log` and `relay-stderr.log` remain untracked on disk. Final process checks found no task player/editor process and no listener on port 8787.

## Failures and limits

The initial PlayMode run caught diagnostic literals in the new probe: the broad U5 census also scans development-only script strings. Its exact error was `Expected: <empty> But was: < ... elapsed_ms,from_model,failure ... " failure=" ... "[RelayLive] FAIL " ... "failure.txt" >`. Renamed the diagnostics to `outcome`, `ERROR` and `probe-error.txt`; the unchanged census test then passed. The other 93 PlayMode cases passed in the initial full run. Only the failed case was rerun after this string-only correction; a second full suite was not run.

The build retained existing obsolete-API/hiding warnings and Unity shutdown `JobTempAlloc` warnings. No compiler/build error occurred. Generated materials, URP/volume serialization changes and older tasks' recaptured evidence were restored to their starting state. Initial untracked art candidates and scratch images/directories were left untouched.

Next work is the owner's eventual API measurement and a broader coach quality evaluation, especially guard-rejected reflection responses. No guard was weakened and no fallback was relabelled as a model reply.

Implementation commit: `cb159d3` (development-player probe and onboarding provenance). This report and measured evidence are committed separately. A trailing space in an existing review-test diagnostic inside `playmode-initial.xml` was trimmed for the commit; the XML was re-parsed with the same 93 passed / 1 failed result. Raw logs were not altered.
