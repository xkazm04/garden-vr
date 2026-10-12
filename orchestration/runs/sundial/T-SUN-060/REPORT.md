# T-SUN-060: B3 speech seam

2026-10-12, `C:/Users/kazda/kiro/gvr-sundial`, branch `agent/sundial`, starting revision `54c44dd`.

Implementation commit: `f27f1cb` (`Add Sundial hold-to-speak seam with Windows and scripted providers`). This report and the evidence are committed separately after verification. Nothing was pushed.

Implemented the speech seam, scripted playback, hold/release adapter, transient transcript lifetime, and ink listening ripple. The actual standalone Windows recognizer **does not work on this machine's current configuration**: it returns `0x80045509`, dictation permission is disabled. The prescribed fallback is present: an empty scripted source, speech unavailable, and the existing complete preset first run. Windows settings were not changed.

The manual human Space/speak/release transcription check is **not run / blocked by Windows dictation permission**. The device check really ran in the standalone development player with scripted hand intents and the real Windows recognizer; it is not presented as a successful spoken transcription. No microphone speech was fabricated or saved.

## Changes

- Added `ISpeechToText` in `com.gardenvr.input`: `Begin`, `End`, `Final`, `Partial`, `Available`. Optional `ISpeechCaptureState.Listening` lets the view reflect device failure/completion.
- Added a lazy, Windows-guarded `WindowsSpeechSource`. Construction does not create a recognizer or open the microphone. Begin starts capture; End stops it while allowing the final native completion callback. Cancellation of a pending start and disposal release the device. Startup is asynchronous, polled each frame and bounded to three seconds. Failure diagnostics contain device errors, not transcripts.
- Added `ScriptedSpeechSource`: one utterance per hold from a playback file, timed partial/final events, repeated-Begin protection, ordered validation, end cancellation, and no invented final or repeated exhausted playback. `Assets/Tests/Playback/speech.json` is synthetic fixture text.
- `SpeechController` starts dormant. Interview/reflection consumers call `Arm(targetId)` in an exclusive speech context, and `CompleteCoachCall()` on completion/cancellation. All capture control comes from input intents/lifecycle. Release stops capture; pause, focus loss, tracking loss, disable and source replacement also stop and clear text. Re-arming during an existing hold waits for release. An unavailable device selects an empty script and reports `UsingPresetFallback=true`, `Available=false`.
- The existing preset wizard remains available, independent of speech. B5/B7 integration is not part of this step. No new business rule or save schema was needed; core remains unchanged.
- A small irregular ink ring and `listening` label appear on the front of the dial only during actual capture. Reduced motion stops the pulse. Uses the app's existing included ink shader.
- Added `Build.WindowsDevelopment` and development-only `SpeechPlayerCheck`. `--speech-check` accepts real Space/voice/release for 30 seconds; `--speech-probe` uses scripted hold/release for a bounded real-device check. Both isolate their save under the temporary cache and exit themselves. Only this explicitly invoked development harness logs final text. Normal runtime and release builds do not log transcripts.
- The shared input change is **additive and backward compatible**: no existing hand interface, provider, binding, or enum changed. Terrarium compatibility is checked below. No edits to core, Terrarium sources, package versions, input key readers, or the read-only task file.

## Verification

Final verification results are recorded in `verification.txt`; Unity XML roots must have `total > 0`, `failed = 0`.

| Check | Result | Evidence |
|---|---|---|
| .NET solution | 522 passed, 0 failed | `dotnet.log` (local), excerpt below |
| Sundial EditMode | 84 passed, 0 failed | `editmode.xml` |
| Sundial PlayMode, initial full run | 60 passed, 0 failed | `playmode-initial.xml` |
| Speech PlayMode after indicator placement/fallback update | 5 passed, 0 failed | `speech-playmode.xml` |
| Final full PlayMode | 60 passed, 0 failed | `playmode.xml` |
| Windows development build | Succeeded | `build-development.log` (local) |
| Windows release build | Succeeded | `build.log` (local) |
| Terrarium compile with shared input additions | Succeeded, 0 compiler errors | `terrarium-compile.log` (local) |
| Real Windows recognizer | Unavailable, HRESULT `0x80045509` | `player-speech.log` (local), committed excerpt below |
| Human spoken final | Not run: device permission prevents dictation | No success claim |

The nine new EditMode cases cover playback file timing/order, repeated Begin, early release, next utterance, exhaustion, invalid input/ordering, invalid time and reload. Five new PlayMode tests cover opt-in/target routing, release and delayed final, cleanup rejecting late text, pause/re-arm, tracking loss/disable, asynchronous failure/fallback, actual scripted hand+speech playback, screenshot visibility and save privacy. The privacy test reads **every file** in its isolated save directory after Flush and rejects the synthetic spoken marker. The existing first-run and ten-run journey tests remain in the full suite.

No raw `Keyboard.current`, `Mouse.current`, `UnityEngine.Input.*` or `Input.GetKey/GetButton/GetMouse` reads were added in app code. `verify.py` checks the app scripts and the compiled DLLs: `SpeechPlayerCheck` must exist in development and be absent from release.

## Commands and real output

All commands ran in this worktree. Every Unity/player invocation used foreground `Start-Process -Wait -PassThru -WindowStyle Hidden`, completed, and had its log read before proceeding. Unity runs were sequential, including the compatibility compile. No ADB commands or device operations were issued.

```powershell
$env:DOTNET_ROLL_FORWARD='Major'
dotnet test shared/core-dotnet/GardenVR.sln 2>&1 |
    Tee-Object -FilePath orchestration/runs/sundial/T-SUN-060/dotnet.log
```

Actual output, exit 0:

```text
Passed!  - Failed:     0, Passed:    14, Skipped:     0, Total:    14, Duration: 41 ms - GardenVR.TerrariumModel.Tests.dll (net9.0)
Passed!  - Failed:     0, Passed:    11, Skipped:     0, Total:    11, Duration: 59 ms - GardenVR.CoachCli.Tests.dll (net9.0)
Passed!  - Failed:     0, Passed:   445, Skipped:     0, Total:   445, Duration: 1 s - GardenVR.Core.Tests.dll (net9.0)
Passed!  - Failed:     0, Passed:    52, Skipped:     0, Total:    52, Duration: 342 ms - GardenVR.SundialModel.Tests.dll (net9.0)
```

Unity arguments (absolute paths factored here for readability):

```powershell
$repo='C:/Users/kazda/kiro/gvr-sundial'
$runDir="$repo/orchestration/runs/sundial/T-SUN-060"
$unity='C:/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Unity.exe'
Start-Process -Wait -PassThru -WindowStyle Hidden -FilePath $unity -ArgumentList "-batchmode -nographics -projectPath $repo/apps/sundial -runTests -testPlatform EditMode -testResults $runDir/editmode.xml -logFile $runDir/editmode.log"
Start-Process -Wait -PassThru -WindowStyle Hidden -FilePath $unity -ArgumentList "-batchmode -projectPath $repo/apps/sundial -runTests -testPlatform PlayMode -testResults $runDir/playmode.xml -logFile $runDir/playmode.log"
# Focused intermediate rerun:
Start-Process -Wait -PassThru -WindowStyle Hidden -FilePath $unity -ArgumentList "-batchmode -projectPath $repo/apps/sundial -runTests -testPlatform PlayMode -testFilter GardenVR.Sundial.Tests.PlayMode.SpeechTests -testResults $runDir/speech-playmode.xml -logFile $runDir/speech-playmode.log"
Start-Process -Wait -PassThru -WindowStyle Hidden -FilePath $unity -ArgumentList "-batchmode -nographics -quit -projectPath $repo/apps/sundial -executeMethod GardenVR.Sundial.Editor.Build.WindowsDevelopment -logFile $runDir/build-development.log"
Start-Process -Wait -PassThru -WindowStyle Hidden -FilePath "$repo/apps/sundial/Build/sundial-development/Sundial.exe" -ArgumentList "--speech-probe -screen-fullscreen 0 -screen-width 960 -screen-height 540 -logFile $runDir/player-speech.log"
Start-Process -Wait -PassThru -WindowStyle Hidden -FilePath $unity -ArgumentList "-batchmode -nographics -quit -projectPath $repo/apps/sundial -executeMethod GardenVR.Sundial.Editor.Build.Windows -logFile $runDir/build.log"
Start-Process -Wait -PassThru -WindowStyle Hidden -FilePath $unity -ArgumentList "-batchmode -nographics -quit -projectPath $repo/apps/terrarium -logFile $runDir/terrarium-compile.log"
```

Final standalone device check, player exit 0 (this exit is **not** evidence of working recognition):

```text
[SpeechCheck] Automated device probe, no human speech claimed
[Speech] Windows begin: Stopped
ERROR:  Dictation support is not enabled on this device (see 'Get to know me' in Settings > Privacy > Speech, inking, & typing) [Operation has failed with error 0x80045509: (null)]
[Speech] Windows unavailable: HRESULT=0x80045509 ERROR:  Dictation support is not enabled on this device (see 'Get to know me' in Settings > Privacy > Speech, inking, & typing)
[SpeechCheck] Available=False Listening=False Finals=0
[SpeechCheck] Transient text cleared=True
```

Unity documents this HRESULT as the speech privacy policy not being accepted: [DictationRecognizer.Start](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/windows/speech/dictationrecognizer/start). The adapter does not enable permissions or establish an offline guarantee for the Windows speech service. The broader on-device speech requirement remains unverified; do not describe this Windows service as proven offline.

Final test/build output (all process exits 0):

```text
editmode result=Passed total=84 passed=84 failed=0 skipped=0
playmode result=Passed total=60 passed=60 failed=0 skipped=0
speech-playmode result=Passed total=5 passed=5 failed=0 skipped=0
Build Succeeded C:\Users\kazda\kiro\gvr-sundial\apps\sundial\Build\sundial-development\Sundial.exe
Build Succeeded C:\Users\kazda\kiro\gvr-sundial\apps\sundial\Build\sundial\Sundial.exe
*** Tundra build success (5.27 seconds), 35 items updated, 851 evaluated
Exiting batchmode successfully now!
App raw keyboard/mouse reads=0
sundial SpeechPlayerCheck metadata present=False
sundial-development SpeechPlayerCheck metadata present=True
```

`verify.py` found zero compiler/shader error entries in both final test logs, both final build logs and the Terrarium compile log. `verification.txt` includes the actual compiled app DLL SHA256 values. The final ten-run journey reached run 10 with identical `4d993985b44e1ed46492b269bdf3dd4ed011f8ed0d7e028e130a8bd079f1881d` state hashes.

## Captures and visual check

- `DialG1-listening.png` / `DialG1-released.png`: real PlayMode renders, same fixed 1824x1024 DialG1 framing, scripted speech source, existing seeded preset dial. They are not screenshots of successful Windows recognition.
- `reference-side-by-side.jpg`: original Field Notebook reference alongside the listening capture, full frames at identical size.
- `artboard-side-by-side.jpg`: reflection artboard alongside the same capture. This compares the small listener's scale/ink treatment, not completion of the later reflection UI.
- `ripple-pixels.json`: 1002 rendered pixels change between listening and release, bounds `(684, 808)` to `(865, 846)`. The test also asserts more than 100 changed pixels, so a hidden/offscreen indicator cannot pass on state alone.
- `compare.py`: reproducible comparison/measurement script. Comparison panels are joined without rescaling or recolouring the source frames. The current dial's existing preset offers and plant presentation differ visibly from the reference; this is not a full-art-fidelity pass or an owner rubric score.
- Both PNGs must pass `python tools/assets/lossless.py --ingest --apply <paths>` before staging. Final output is in `lossless.txt`; raw original-size `.check.json` statistics are produced by the capture test before PNG recompression.

Final lossless ingest completed, exit 0: `total 5446969 -> 4511459, saved 935510 bytes; proof failures 0; applied`. Both images passed mode, dimensions, palette, transparency and decoded pixel SHA256 proofs.

## Failures found and corrected

1. The initial full suite passed, but visual inspection showed the marker was offscreen. Its position was corrected using the dial's face radius/height, and the pixel comparison assertion was added. The initial XML is retained as `playmode-initial.xml`; it is not evidence of the final marker placement.
2. The first development player exposed shader stripping, despite passing editor tests. `player-speech-initial.log` contains:

   ```text
   ArgumentNullException: Value cannot be null.
   Parameter name: shader
   at GardenVR.Sundial.ListeningRipple.Create ... ListeningRipple.cs:21
   at GardenVR.Sundial.SpeechController.Awake ... SpeechController.cs:29
   ```

   Switched from an unreferenced URP shader name to the already included `InkLetter.Card()` shader. The final player has no such exception.
3. An intermediate check rejected the immediate native `Stopped` status too early (`player-speech-start-status.log`). Allowing asynchronous startup exposed the actual Windows permission error above. This is why the final device result uses the last run, not the intermediate status heuristic.

Raw `.log` files stay locally beside this report and are untracked, per AGENTS.md. The report and `verification.txt` preserve material output. No build binaries, Library/Temp/Logs directories, secrets, or unrelated existing untracked files are committed.

Incidental serialization changes to ten Sundial materials, its volume/URP settings, and the prior T-SUN-059 captures generated by existing tests were inspected and restored. `restored-generated-files.txt` lists the paths. The initial untracked plant candidates, root comparison images and `scratch/` remain untouched. No Unity project editor or Sundial player remains running; the pre-existing Unity Hub `serve` process was left alone.

Non-blocking log diagnostics include the existing unused events, inherited member hiding, obsolete FindObjectsByType overloads, shader warnings and build-shutdown JobTempAlloc allocation warnings. None failed the final suites or builds. No Terrarium source, asset or setting change remains.

## Strings and next step

User-facing strings added for B12's census: **`listening`**. No em dash, medical claim, streak counter or guilt copy. Playback fixture phrases and development diagnostics are not app UI strings.

B5/B7 can inject scripted speech and consume `Final`/`Partial` through the new adapter, calling `CompleteCoachCall` when the coach request ends or is cancelled. Keep preset onboarding available. A successful human transcription and latency check still require a machine with permitted dictation; then run the development player with `--speech-check`, hold Space, speak a non-sensitive test phrase, release, and inspect its explicit final-text log. This run makes no successful-recognition or speech-latency claim.
