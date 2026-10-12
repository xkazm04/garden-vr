# T-SUN-062: B5 first-run onboarding interview

2026-10-12, `C:/Users/kazda/kiro/gvr-sundial`, branch `agent/sundial`. The assigned task in the main worktree was read only. Nothing was pushed.

Completed and verified: **537 .NET tests, 97 Sundial EditMode tests and 73 Sundial PlayMode tests pass**, including all three named B5 tests. The Windows player builds and Terrarium compiles against the shared additions. Coach-off first tend is **4.25 simulated seconds** in the new packet journey. The onboarding gate capture, comparisons and string census are committed with this report. No B5 acceptance blocker remains.

## What changed

- The shadow sweep now ends at a one-line online-coach opt-in, with separate pinchable off/on choices. Fresh saves remain off. The four exact `CoachPrompts.OnboardingQuestions` appear one at a time. Speech uses the existing `SpeechController` hold/release seam; a next choice confirms an answer, and skip discards it. When speech is unavailable, the card offers the complete starter-habit path.
- `SundialCoach.OnboardAsync` still owns the opt-in gate and delegates to `CoachFlow`. Its guarded proposals become labelled seed envelopes, with name, zone, schedule and cue. The envelopes use existing authored textures, retained in player builds by a resource asset. Clear time cues select morning, midday or evening through core; an ambiguous cue asks the player to pinch an arc. Each offer can be declined. All-declined also leads to the existing presets.
- Accepting a packet plants it through `SundialService.PlantProposal`, plays the seed-drop cue/animation and reveals the four unshaded zone tabs. Monthly fill and totals remain B9. A ready choice proceeds to the first planted habit, even when the current time arc is empty. That tend completes the interview. Legacy preset journeys still offer their three-breath ritual.
- Core supplies additive interview step/routing helpers and `OnboardingDraft`. The optional schema-2 save member stores proposals, selection and decisions, **not answers or transcripts**. Accepted/declined packets and an unfinished arc choice survive reload. Acceptance and its packet decision share one save write, preventing duplicate planting after restart. Legacy saved preset steps continue from their original positions.
- Spoken answers stay in memory until the coach call, with speech text cleared at each question transition and call completion. Pause, focus loss, disable and destruction clear transient speech; coach requests are cancelled on interruption. A resumed question retains its index but deliberately has no previous spoken answer. A resumed in-flight request starts with no saved answers.
- A deliberate off-day check-in is now accepted by the app's tend handler through `SundialRules.CanTendToday`; core already records that check-in as kept. This avoids trapping a first-run weekend walker on a blank off-day tile. `IsBreathHabit` prevents the dusk breath ritual from crediting a custom evening habit such as Read.
- The paper controls are camera-facing and use `IntentTarget` colliders. Tests raycast from the actual DialG1 camera to every visible interview target. The gnomon's focus target yields while the interview card is open. No app code reads raw keyboard or mouse state.

Shared-core changes are **additive and backward compatible**: existing types/members and enum values remain. Both public-surface snapshots were updated and reviewed. Core implementation commits: `d0374c5` and `492e72b`. App/save integration commit: `db7e10a`. This report, gate registration and evidence form the final commit. Nothing was pushed.

## Verification and commands

Every Unity run used foreground `Start-Process -Wait -PassThru -WindowStyle Hidden`. Runs were sequential, and each log was read before the next Unity command. Test success is based on XML totals/failures, not process exit alone. No ADB/device operations, paid API calls, image generation or mesh generation were used.

```powershell
$env:DOTNET_ROLL_FORWARD='Major'
dotnet test shared/core-dotnet/GardenVR.sln --logger "trx;LogFilePrefix=complete" `
  --results-directory orchestration/runs/sundial/T-SUN-062/dotnet-results 2>&1 |
  Tee-Object orchestration/runs/sundial/T-SUN-062/dotnet.log
```

Actual final .NET output, exit 0:

```text
Passed!  - Failed: 0, Passed: 14, Skipped: 0, Total: 14, Duration: 27 ms - GardenVR.TerrariumModel.Tests.dll (net9.0)
Passed!  - Failed: 0, Passed: 11, Skipped: 0, Total: 11, Duration: 65 ms - GardenVR.CoachCli.Tests.dll (net9.0)
Passed!  - Failed: 0, Passed: 53, Skipped: 0, Total: 53, Duration: 426 ms - GardenVR.SundialModel.Tests.dll (net9.0)
Passed!  - Failed: 0, Passed: 459, Skipped: 0, Total: 459, Duration: 1 s - GardenVR.Core.Tests.dll (net9.0)
```

537 tests passed. The additions cover cue ambiguity, question progression, draft round-trip, off-day tending, breath-habit isolation, selecting a planted first-tend arc and atomic acceptance/reload. Final TRX files are committed with the evidence. Surface regeneration used `GARDENVR_APPROVE_SURFACE=1` and the `FullyQualifiedName~PublicSurfaceTests` filter; the variable was removed before the final full run.

Unity commands (absolute common paths factored for readability):

```powershell
$repo='C:/Users/kazda/kiro/gvr-sundial'
$runDir="$repo/orchestration/runs/sundial/T-SUN-062"
$unity='C:/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Unity.exe'
Start-Process -Wait -PassThru -WindowStyle Hidden -FilePath $unity -ArgumentList "-batchmode -nographics -projectPath $repo/apps/sundial -runTests -testPlatform EditMode -testResults $runDir/editmode.xml -logFile $runDir/editmode.log"
Start-Process -Wait -PassThru -WindowStyle Hidden -FilePath $unity -ArgumentList "-batchmode -projectPath $repo/apps/sundial -runTests -testPlatform PlayMode -testResults $runDir/playmode.xml -logFile $runDir/playmode.log"
Start-Process -Wait -PassThru -WindowStyle Hidden -FilePath $unity -ArgumentList "-batchmode -nographics -quit -projectPath $repo/apps/sundial -executeMethod GardenVR.Sundial.Editor.Build.Windows -logFile $runDir/build.log"
Start-Process -Wait -PassThru -WindowStyle Hidden -FilePath $unity -ArgumentList "-batchmode -nographics -quit -projectPath $repo/apps/terrarium -logFile $runDir/terrarium-compile.log"
```

Focused development runs added `-testFilter GardenVR.Sundial.Tests.PlayMode.InterviewTests` to the PlayMode command. The final full XML is authoritative for the final code. `verification.txt` records parsed results, named tests, log checks and image hashes.

Actual final Unity output, every process exit 0:

```text
editmode: result=Passed total=97 passed=97 failed=0
playmode: result=Passed total=73 passed=73 failed=0
FirstRun_Interview_Resumes_AtSavedStep: Passed (0.455408 s)
FirstRun_Interview_ScriptedCoach_FirstTendUnder60s: Passed (0.946942 s)
FirstRun_Interview_Skips_AllFourQuestions: Passed (0.283783 s)
Interview_ConsentAndQuestion_AreVisible_OptInPersists: Passed (0.715127 s)
Interview_Speech_UnknownCue_ArcChoice_PauseAndPrivacy: Passed (0.492265 s)
Build Succeeded C:\Users\kazda\kiro\gvr-sundial\apps\sundial\Build\sundial\Sundial.exe
AssetDatabase: script compilation time: 6.901255s
Exiting batchmode successfully now!
```

The last two lines are Terrarium's compatibility compile. Final compiler/shader/build-error scans found zero matches in all four logs. The Windows build remains locally at the path above, untracked. The existing build-shutdown `JobTempAlloc` warnings remain; they did not fail compilation, tests or the build.

The final full suite's ten canonical journeys all produced state hash `4d993985b44e1ed46492b269bdf3dd4ed011f8ed0d7e028e130a8bd079f1881d`, with first tend at 52.02 simulated seconds. The new, shorter packet journey records:

```text
CoachOptIn=False; speech unavailable; questions skipped=4
FirstTendAt=4.25 simulated seconds
WallSeconds=0.940478 (includes scene load and two captures)
```

The five new PlayMode cases cover:

- `FirstRun_Interview_ScriptedCoach_FirstTendUnder60s`: coach off, an unavailable speech fixture, four skips, three scripted suggestions, acceptance/decline, visible zone tabs, target raycasts, first tend and wizard completion. `first-tend.txt` records simulated timing and wall time. This is automated gate U1 evidence, not the owner's first-time usability test U2.
- `FirstRun_Interview_Skips_AllFourQuestions`: every exact question, declining every suggestion, legacy presets and first tend within 60 seconds.
- `FirstRun_Interview_Resumes_AtSavedStep`: reload at question three, then reload after an accepted and declined packet, rejecting duplicate acceptance.
- `Interview_Speech_UnknownCue_ArcChoice_PauseAndPrivacy`: scripted speech, a cue without a single time, a persisted pending arc choice, pause, explicit arc placement, first tend with an empty current arc, completed-wizard reload, and searching every save/backup file for a synthetic transcript marker.
- `Interview_ConsentAndQuestion_AreVisible_OptInPersists`: default off, visible/raycastable choices, explicit opt-in persistence and question capture. It stops before any model request; tests never contact the live coach.

The canonical journey now explicitly chooses coach off, skips four questions and selects presets before its existing tending/ritual steps. Legacy resume and reduced-motion expectations were updated to distinguish old saved steps from the new first-run entry.

## Captures and string census

- `DialG1-onboarding.png`: onboarding gate frame, one accepted suggestion and two still offered, four zone tabs visible.
- `DialG1-onboarding-packets.png`: all three scripted offers before planting.
- `DialG1-onboarding-consent.png` and `DialG1-onboarding-question.png`: opt-in and the no-speech question path.
- `DialG1-onboarding-side-by-side.jpg`: the requested `docs/art/artboards/field-notebook/1-onboarding-zones.jpg` on the left and actual Unity capture on the right.
- `DialG1-reference-side-by-side.jpg`: original Field Notebook reference versus the same capture.
- `compare_verify.py`: reproducible full-frame comparisons and verification summary. Both source panels remain 1824 x 1024, uncropped and unscaled, with a caption strip above them.
- `user-facing-strings.txt`: complete B5 census, including the existing core questions/catalog newly exposed by the interview. No em dash, medical claim, guilt copy or consecutive-day counter was added.

The gate frame is registered in `apps/sundial/GATE.md`. The current dial/plant art visibly differs from the composited artboard; this is **not an owner A3 art-quality pass**. New visuals reuse the established paper/ink system and authored envelopes. No generated image or new PNG asset was added to the app.

Before staging captures:

```powershell
python tools/assets/lossless.py --ingest --apply <the four T-SUN-062 capture PNG paths>
python orchestration/runs/sundial/T-SUN-062/compare_verify.py
```

`lossless.txt` records the actual pixel-proof output. Original render statistics are in the PNG `.check.json` files; final encoded-file hashes are in `verification.txt`.

Actual ingest output, exit 0:

```text
DialG1-onboarding-consent.png 2687785 -> 2234699 zlib9-default proof ok
DialG1-onboarding-packets.png 2829660 -> 2375883 pillow-optimize proof ok
DialG1-onboarding-question.png 2700141 -> 2247691 zlib9-default proof ok
DialG1-onboarding.png 2810494 -> 2348099 pillow-optimize proof ok
total 11028080 -> 9206372, saved 1821708 bytes; proof failures 0; applied
```

All proofs include mode, dimensions, palette, transparency and decoded pixel SHA256. The census scan found zero em dashes and zero matches from core's `MedicalStems`, `CountPhrases` and `GuiltStems`; app raw keyboard/mouse reads are zero.

## Failures found and resolved

1. Initial .NET compile: `Onboarding.cs(111,30): error CS0103: The name 'SundialArcs' does not exist in the current context`. That normalizer lives in the app; removed the dependency so core stays engine/app independent. `dotnet-initial.log` retains the output.
2. An intermediate surface-approval run raced another test reading `PublicSurface.approved.txt`: `System.IO.IOException: The process cannot access the file ... because it is being used by another process`. Regeneration was then scoped to surface tests, followed by the full suite with approval disabled. `dotnet-surface-final.log` preserves the failed run; final .NET tests pass.
3. Initial focused Unity run: 1 passed, 3 failed. Decline/selection used `Flush`, which only persists a pending tend; switched to the save-step write. The app rejected the weekend walk's off-day tile; added the core-backed tend eligibility check. The scripted speech test advanced simulated frames faster than its unscaled speech clock; it now waits for the actual short speech event. `interview-initial.xml` preserves these failures.
4. Visual inspection found oversized/offscreen cards, clipped envelopes and small text. Resized/repositioned them and added real-camera target checks. Those checks then found an envelope collider with excessive depth and the gnomon hitbox intercepting a skip choice. Corrected the envelope collider and modal target routing. `interview-layout.xml` and `interview-gnomon-failed.xml` preserve the failed checks.
5. Initial full suite: 72 passed, 1 failed. `Record_JourneyFrames` still expected `fr.pick.morning` where the new entry correctly showed `fr.interview.optin`. Updated it to assert/capture the actual consent card, including its font and paper. `playmode-initial.xml` preserves the failure.
6. Final review found that a custom-habit first tend could leave the wizard waiting for an absent dusk ritual, and that a custom evening habit could receive breath credit. Added a distinct saved first-tend step that completes the interview, the core breath-habit guard and regression assertions. Legacy preset ritual behavior is retained.
7. The next full suite passed all interview/journey tests but the unchanged B4 `Relay_NeverAnsweringSocket_IsAbortedAfterFlowBudget` case reported `Task did not complete within 5 seconds` (72 passed, 1 failed). `playmode-retry.xml` and its local log retain the failure. The unchanged full suite then passed all 73 tests, including that relay test. This records the transient test failure without weakening its deadline or assertions.

## Scope, limitations and next work

Raw `.log` files remain locally in this evidence directory, untracked as required by AGENTS.md. Material output is quoted here and summarized in `verification.txt`; XML/TRX, captures, comparisons and small reproduction scripts are committed. No build products, Library/Temp/Logs directories, secrets or unrelated initial scratch files are committed.

Restored 23 incidental generated changes: Sundial material/volume/URP serialization and old B2/B3 capture fixtures rewritten by their existing tests. `restored-generated-files.txt` lists every path. No Terrarium source or asset change remains. The initial untracked plant candidates, root comparison PNGs and `scratch/` remain untouched. XML trailing whitespace was normalized before staging and final counts re-parsed. No Unity editor or player run from this task remains active; the pre-existing Unity Hub service was left alone.

Windows native dictation remains unavailable on this machine as established by T-SUN-060 (`0x80045509`). This run uses unavailable/scripted speech fixtures and makes no successful human-transcription claim. Live relay/CLI calls and owner usability/art scoring were not run; those remain B11/U2/A3 work. The no-speech path is fully covered. B9 adds monthly tab fills and totals; B12 consumes this string census and frame.
