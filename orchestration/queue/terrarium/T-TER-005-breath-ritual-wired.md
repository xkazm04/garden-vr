---
id: T-TER-005
app: terrarium
title: Wire pinch-hold breathing end to end - intents to BreathSession to JarView to the garden's answer - and prove six breaths by playback
depends: [T-TER-001, T-TER-003, T-TER-004]
estimate_min: 120
touches: [apps/terrarium/Assets/Scripts/**, apps/terrarium/Assets/Scenes/**, apps/terrarium/Assets/Editor/**, apps/terrarium/Assets/Tests/PlayMode/**, apps/terrarium/Assets/Tests/Playback/**, orchestration/runs/terrarium/T-TER-005/**]
---
## Goal
The vertical slice (milestone M1): on PC, holding Space or the mouse breathes in, releasing breathes out, the fiddlehead
uncoils with each hold, the glass fogs on each release, and after six breaths the frond stays with the full answer
sequence. A PlayMode test drives it from a JSONL intent script at a fixed timestep and asserts behaviour, not just a
final enum.

## Read first
- `docs/plans/terrarium.md` sections 2 (items 2-4, 7), 3 (t = 10 s to 70 s), 4, 5; `docs/art/terrarium-style.md`
  section 4 (motion, the answer timeline).
- `shared/packages/com.gardenvr.core/Runtime/Breath/BreathSession.cs`, `Runtime/Terrarium/Garden.cs`,
  `TerrariumState.cs` (T-TER-003); `shared/packages/com.gardenvr.input/README.md` (T-TER-001: `PinchStrength`,
  `IsTracked`, JSONL); `docs/contest/seed-unity/TerrariumDriver.cs` (the spike's driver, for reference only).

## Steps
0. `git merge --no-edit main`. Create `orchestration/runs/terrarium/T-TER-005/`.
1. `Assets/Scripts/Ritual/JarRitualController.cs`: each frame build `PinchSample(source.PinchStrength,
   source.IsTracked)`, call `BreathSession.Update(dt, sample)`, and push `Uncoil`, `Fog`, breath-ring progress
   (`Breaths + current inhale fraction`) and phase to `JarView`. On `Complete`: call `Garden.CompleteRitual(today)`
   (today from `Garden.DayNumber(DateTime.Now)` for now; T-TER-008 moves this to the Common day) and play the answer
   sequence with the bible's timings (frond settles 0.8 s, dew bead 1.2 s from 0.4 s, moss ripple 1.5 s from 0.6 s, ring
   gold). Expose `TerrariumState Snapshot()`.
2. Six breath dots on the cork (emissive dots filling one by one). The pace ring suggests 4 s in / 6 s out and re-syncs on
   every pinch start; it never shows a breath as wrong.
3. Pause: `PalmOpen` intent, `SystemPause` or tracking `Lost` -> the jar holds its breath (mist and spores slow to 20%,
   ring freezes); resume continues; a ritual paused for more than 60 s shows a "Continue breathing?" prompt
   (`IntentTarget` id `prompt.continue`) instead of auto-resuming. Nothing earned is lost.
4. Auto-pace fallback (dev F2 now; a setting later): a synthetic `PinchSample` stream at 4 s / 6 s; it counts.
5. A development overlay (F1) printing `TerrariumState.ToJson()`; every state change appends a JSONL line
   `{t, event, state}` to `Application.persistentDataPath/logs/ritual.jsonl` in development builds and tests.
6. PlayMode tests in `Assets/Tests/PlayMode/` (asmdef `GardenVR.Terrarium.Tests.PlayMode`), fixed step
   `Time.captureDeltaTime = 1f/60f`, scene `Main.unity`, provider swapped for `ScriptedIntentSource`:
   - `Ritual_SixBreaths_ByPlayback` with `Assets/Tests/Playback/six-breaths.jsonl` (holds 4.0-4.5 s, releases 5-6 s):
     breaths == 6, phase Complete, fronds == 1, app time <= 90 s; the sampled uncoil never decreases and ends at 1;
     fog rises above 0.2 within 0.5 s after every release (behavioural discriminators);
   - `Ritual_FidgetsIgnored`: three 0.4 s taps then six breaths -> still exactly 6;
   - `Ritual_PauseAtBreath3_Resumes`: PalmOpen during breath 3, wait 10 s, resume -> completes with 6, none lost;
   - `Ritual_TrackingLost2s_Bridged_Or_Paused`: a 0.3 s loss mid-inhale is bridged, a 2 s loss pauses;
   - `Ritual_Response_TwoFrames`: from the first PinchHold frame to the first uncoil change <= 2 frames (gate S3).
7. Record the six-breath run as 24 fps PNGs with the capture package's sequence recorder at the `JarG1` framing and
   assemble `ritual.mp4` with ffmpeg.
8. Commit per sub-step.

## Acceptance (each check runnable on Windows without a headset; say the command)
```bash
U="C:/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Unity.exe"; R=orchestration/runs/terrarium/T-TER-005
"$U" -batchmode -projectPath apps/terrarium -runTests -testPlatform PlayMode -testResults "$(pwd)/$R/playmode.xml" -logFile "$R/playmode.log"
grep -o '<test-run[^>]*>' "$R/playmode.xml"                         # total >= 5, failed="0"
grep -o 'name="Ritual_[A-Za-z0-9_]*"[^>]*result="[A-Za-z]*"' "$R/playmode.xml"   # each Passed
ffmpeg -y -framerate 24 -i "$R/seq/f%04d.png" -c:v libx264 -pix_fmt yuv420p -crf 22 "$R/ritual.mp4" && ffprobe -v error -show_entries format=duration -of csv=p=0 "$R/ritual.mp4"   # > 40 s
grep -rnE "Keyboard\.current|Mouse\.current|Input\.Get" apps/terrarium/Assets --include=*.cs   # nothing (S1)
dotnet test shared/core-dotnet 2>&1 | tail -1                         # still green
```

## Evidence to leave (paths under orchestration/runs/terrarium/T-TER-005/)
`REPORT.md` (state timeline excerpt from `ritual.jsonl`, the five test results), `playmode.xml`, `ritual.mp4`, three
stills (`mid-inhale.png`, `exhale-fog.png`, `answer.png`), `six-breaths.jsonl` copy.

## Out of scope
Persistence across launches (T-TER-008), audio (T-TER-010), first-run teaching (T-TER-012), new art.
