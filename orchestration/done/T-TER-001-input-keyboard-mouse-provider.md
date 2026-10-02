---
id: T-TER-001
app: terrarium
title: Build the keyboard/mouse hand-intent provider and scripted intent playback in com.gardenvr.input
depends: []
estimate_min: 100
touches: [shared/packages/com.gardenvr.input/**, apps/terrarium/Assets/Tests/EditMode/**, orchestration/runs/terrarium/T-TER-001/**]
---
## Goal
`com.gardenvr.input` becomes the only place that reads keyboard and mouse, and turns them into the six hand intents
with the feel described in `docs/plans/terrarium.md` section 4 (hold stays a hold; strength ramps through the core's
pinch hysteresis). It also plays scripted intents from a JSONL file by target id, which is how every later journey test
drives the apps without a human. You own this package (`docs/PLAN.md` section 5); Sundial consumes it.

## Read first
- `AGENTS.md`; `docs/PLAN.md` sections 4 (S1) and 5; `docs/plans/terrarium.md` section 4 (the binding table is the spec);
  `docs/plans/sundial.md` section 4 (what Sundial needs: Look dwell, Tab focus, target ids).
- `shared/packages/com.gardenvr.input/Runtime/HandIntent.cs` (existing interface; extend it backward compatibly).
- `shared/packages/com.gardenvr.core/Runtime/PinchDetector.cs` (engage 0.8, release 0.5, lost grace 0.4 s: your
  strength ramp must exercise it).
- `apps/terrarium/Packages/manifest.json` (Input System 1.17.0 is installed; do not change the manifest).

## Steps
0. `git merge --no-edit main` in your worktree. Create `orchestration/runs/terrarium/T-TER-001/`.
1. Extend `IHandIntentSource` (additive only): `float PinchStrength { get; }`, `bool IsTracked { get; }`,
   `string BindingHint(HandIntentKind kind)` (PC: "Space or mouse" for PinchHold, "click" for Pinch, "F" for Poke,
   "hold P" for PalmOpen), `event Action SystemPause`. Add `IHeadPoseSource { Quaternion LocalRotation { get; } event Action Recentred; }`.
2. `Runtime/Mapping/KbmIntentMapper.cs`: a pure class (no device reads) that takes a per-frame `RawKbm` struct
   (space, left/middle/right mouse, F, P, Enter, Tab, Shift, Esc, R, F1, F2, `[`, `]`, mouse position, mouse delta,
   unscaled dt) plus a ray function, and emits intents with exactly these rules:
   - `Pinch` on press of left mouse or Enter (ray = look ray); `PinchHold` every frame while Space or left mouse is held
     for >= 0.30 s, with `Held` seconds; `Release` on release with `Held` = total hold seconds;
   - `PinchStrength` ramps 0 -> 1 over 0.12 s (smoothstep) on press and 1 -> 0 over 0.12 s on release;
   - `holdMode = Toggle`: a Space tap toggles held / released (accessibility);
   - `Look` every frame with the mouse ray; a `LookTarget` changes only after 0.15 s dwell on a new `IntentTarget`;
     Tab / Shift+Tab step focus through registered targets in a stable order (by id);
   - `Poke` on F press at the look ray; `PalmOpen` once when P (or middle mouse) has been held 0.6 s;
   - head: right-drag yaw +/- 40 deg, pitch +/- 25 deg (0.15 deg per pixel), R recentres; Esc raises `SystemPause`;
   - `DevCommands` (F1, F2, `[`, `]`) raised only when `Debug.isDebugBuild || Application.isEditor`.
3. `Runtime/Providers/KeyboardMouseIntentSource.cs` (MonoBehaviour): the ONLY file that touches
   `Keyboard.current` / `Mouse.current`; fills `RawKbm` each frame and forwards to the mapper. Same folder:
   `KeyboardMouseHeadPose.cs`. Also forward `OnApplicationFocus(false)` as `SystemPause`.
4. `Runtime/Targets/IntentTarget.cs` (MonoBehaviour with `string id`, optional collider) and `IntentTargetRegistry`
   (static lookup by id, raycast helper). Hidden or disabled targets are not hit and are skipped by Tab.
5. `Runtime/Playback/ScriptedIntentSource.cs`: implements `IHandIntentSource` from a JSONL script, one object per line:
   `{"t":10.0,"intent":"PinchHold","target":"jar","dur":4.2}`, `{"t":14.2,"intent":"Release"}`,
   `{"t":20,"intent":"Look","target":"seed.walk","dur":0.4}`, `{"t":21,"intent":"Pinch","target":"seed.walk"}`,
   `{"t":30,"intent":"PalmOpen"}`, `{"t":31,"intent":"Lost","dur":2.0}` (tracking loss). It resolves targets through
   the registry, ramps strength like the mapper, advances on `Tick(dt)` (tests) or `Update` with `Time.deltaTime`, and
   exposes `bool Finished`. Write a tiny line parser (no Newtonsoft).
6. EditMode tests in `apps/terrarium/Assets/Tests/EditMode/` with asmdef `GardenVR.Terrarium.Tests.EditMode`
   (references `GardenVR.Input`, `UnityEngine.TestRunner`, `UnityEditor.TestRunner`; `nunit.framework.dll`;
   Editor platform; `UNITY_INCLUDE_TESTS`). At least 14 tests covering: click vs hold threshold; strength ramp passes
   0.8 within 0.12 s and drops under 0.5 within 0.12 s of release; toggle mode; Release carries total hold; PalmOpen at
   0.6 s and not at 0.5 s; Look dwell 0.15 s; Tab order stable; dev commands absent when not debug (inject the flag);
   playback parses all line kinds, reports a bad line with its line number, holds strength for `dur`, `Lost` reports
   `IsTracked == false`, and `Finished` after the last event.
7. Update `shared/packages/com.gardenvr.input/README.md` with the binding table and the JSONL format.
8. Commit per sub-step (interface, mapper + tests, provider, playback + tests, README).

## Acceptance (each check runnable on Windows without a headset; say the command)
```bash
U="C:/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Unity.exe"; R=orchestration/runs/terrarium/T-TER-001
"$U" -batchmode -nographics -quit -projectPath apps/terrarium -logFile "$R/compile.log"; echo "exit=$?"   # exit=0
grep -c "error CS" "$R/compile.log"                                                                    # 0
"$U" -batchmode -nographics -projectPath apps/terrarium -runTests -testPlatform EditMode -testResults "$(pwd)/$R/editmode.xml" -logFile "$R/editmode.log"
grep -o '<test-run[^>]*>' "$R/editmode.xml"     # total >= 14, failed="0"
grep -rnE "Keyboard\.current|Mouse\.current|Input\.Get" shared/packages apps/terrarium/Assets --include=*.cs   # only .../com.gardenvr.input/Runtime/Providers/
"$U" -batchmode -nographics -quit -projectPath apps/sundial -logFile "$R/compile-sundial.log"; grep -c "error CS" "$R/compile-sundial.log"   # 0 (consumer still compiles)
```

## Evidence to leave (paths under orchestration/runs/terrarium/T-TER-001/)
`REPORT.md` (what changed, the commands above with trimmed real output, the binding table as implemented),
`compile.log`, `compile-sundial.log`, `editmode.xml`, `editmode.log`, `grep-s1.txt` (output of the S1 grep).

## Out of scope
Any XR / Meta package; scene objects; app gameplay; changes to `Packages/manifest.json` or the core package.
