---
id: T-SUN-001
app: sundial
title: Build com.gardenvr.capture - batchmode shots at reference framings, side-by-sides, image checks, budget measurement and sequence recording
depends: []
estimate_min: 120
touches: [shared/packages/com.gardenvr.capture/**, apps/sundial/Assets/Tests/EditMode/**, apps/sundial/Assets/Capture/**, orchestration/runs/sundial/T-SUN-001/**]
---
## Goal
Every art and journey task in both apps proves itself with this package: a batchmode shot of a scene at a named
framing with a state applied, an automatic image check (not black, not magenta), a labelled side-by-side against a
reference frame, a budget measurement (draws, triangles, textures, transparent overdraw), an image diff under a mask,
and a frame-sequence recorder for PlayMode tests. You own this package (`docs/PLAN.md` section 5); Terrarium consumes it
from T-TER-002 on, so its command-line contract below is binding.

## Read first
- `docs/PLAN.md` section 4 (gate items A1-A5 use this package); both art bibles, section 5 (framings).
- `docs/contest/seed-unity/FidelityTools.cs`: `Render`, `Settle`, `MakeCamera`, `JarCamera`, `DialCamera`, `Measure`
  (port these; the overdraw method swaps transparent materials for `Fidelity/Overdraw`).
- Round-2 limitation 8 (async shader compilation poisons captures: magenta / placeholder first frames):
  `C:\Users\kazda\kiro\personas\.contest\arena\habit-garden-r2\entries\claude-claude-opus-5-5_high-v1\variant-1\index.html` section 04.
- `shared/packages/com.gardenvr.fx/Runtime/Shaders/FOverdraw.shader`, `FPlate.shader`; `shared/assets/room-plates/`.

## Steps
0. `git merge --no-edit main`. Create `orchestration/runs/sundial/T-SUN-001/`.
1. Runtime assembly `GardenVR.Capture` (no editor code): `ICaptureState { void ApplyCaptureState(IReadOnlyDictionary<string,string> s); }`;
   `Framing` (name, eye position and look-at relative to a target root, vertical FOV, lens shift, optional plate file,
   optional overlay file, resolution); `Framings` with built-in presets: `JarG1` (from `FidelityTools.JarCamera`, plate
   `plate-jar.png`), `DialG1` (from `DialCamera`, plate `plate-dial.png`), `SeatedPOV` (the rig's eye camera, 90 deg FOV,
   no screen plate); overrides and additions read from `Assets/Capture/framings.json` in the app;
   `SequenceRecorder` (PlayMode: fixed fps, writes `f0000.png...` to a directory from a parameter or env `GARDEN_SEQ_DIR`);
   `PlaybackHarness` (sets `Time.captureDeltaTime`, `RunUntil(predicate, timeoutSeconds)`, writes a state JSON).
2. Editor assembly `GardenVR.Capture.Editor`, class `CaptureCli` with these batch entry points and flags (binding):
   - `Shot`: `-scene <asset path> -framing <name> [-target <IntentTarget id or object name>] [-state "k=v,k=v"]
     [-reference <png>] [-overlay <png>] [-w 1824] [-h 1024] [-msaa 8] -out <absolute png>`. Before rendering:
     `ShaderUtil.allowAsyncCompilation = false` and one warm-up render. For a framing with a plate, render the plate
     screen-aligned behind the scene and disable any world `PcRoomPlate`. Writes `<out>`, `<out>.check.json`
     (`meanLuma`, `blackFrac`, `whiteFrac`, `magentaFrac`, `width`, `height`, `sha256`), and with `-reference` a labelled
     `<stem>.sbs.png` (REFERENCE name | RENDER name + "Unity URP, batchmode, <framing>, <state>").
   - `Crops`: `-a <png> -b <png> -regions "x,y,w,h;..." -out <png>` same-pixel crops a|b per row.
   - `Diff`: `-a <png> -b <png> [-mask <png>] -out <json>` mean absolute difference (0..1) outside / inside the mask.
   - `Measure`: `-scene -target [-state] -out <json>`: renderers, `drawsEst` (materials x passes + shadow casters),
     `shadowCasters`, `transparentDraws`, `tris`, textures with sizes, `texMemAstc6x6MB`, `transparentMeanLayers` and
     `transparentCoverage` over the target's screen area at its framing, plus `<stem>.overdraw.png`.
   - Every entry point logs `[Capture] <entry> OK <out>` or `[Capture] <entry> FAIL <reason>` and exits non-zero on failure.
3. `shared/packages/com.gardenvr.capture/README.md`: the contract above with one example per entry point, the
   `framings.json` format, and how `ICaptureState` is used.
4. EditMode tests in `apps/sundial/Assets/Tests/EditMode/` (asmdef `GardenVR.Sundial.Tests.EditMode`): image check on
   synthetic textures (all black, all magenta, a gradient); side-by-side size and label strip; `Diff` with and without a
   mask; framings JSON override parsing; `Measure` on a test scene with 2 opaque + 1 transparent known renderers.
5. Smoke runs on the Sundial project's current `Main.unity`: `Shot` at `SeatedPOV` and at `DialG1` with
   `-reference shared/assets/art-reference/A2-05-field-notebook-1.png`; `Diff` between the `DialG1` plate-only shot and the
   reference with `-mask shared/assets/room-plates/dial-mask.png` (outside the dial the plate IS the reference, so the
   difference must be small: this proves the plate is pixel-aligned).
6. Commit per sub-step.

## Acceptance (each check runnable on Windows without a headset; say the command)
```bash
U="C:/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Unity.exe"; R=orchestration/runs/sundial/T-SUN-001
"$U" -batchmode -nographics -projectPath apps/sundial -runTests -testPlatform EditMode -testResults "$(pwd)/$R/editmode.xml" -logFile "$R/editmode.log"; grep -o '<test-run[^>]*>' "$R/editmode.xml"   # total >= 8, failed="0"
"$U" -batchmode -quit -projectPath apps/sundial -executeMethod GardenVR.Capture.Editor.CaptureCli.Shot -scene Assets/Scenes/Main.unity -framing DialG1 -reference "$(pwd)/shared/assets/art-reference/A2-05-field-notebook-1.png" -out "$(pwd)/$R/dial-g1-plate.png" -logFile "$R/shot.log"; grep "\[Capture\]" "$R/shot.log"   # Shot OK
cat "$R/dial-g1-plate.png.check.json"; ls "$R/dial-g1-plate.sbs.png"
"$U" -batchmode -quit -projectPath apps/sundial -executeMethod GardenVR.Capture.Editor.CaptureCli.Diff -a "$(pwd)/$R/dial-g1-plate.png" -b "$(pwd)/shared/assets/art-reference/A2-05-field-notebook-1.png" -mask "$(pwd)/shared/assets/room-plates/dial-mask.png" -out "$(pwd)/$R/plate-align.json" -logFile "$R/diff.log"; cat "$R/plate-align.json"   # outsideMask mean < 0.03
"$U" -batchmode -nographics -quit -projectPath apps/terrarium -logFile "$R/compile-terrarium.log"; grep -c "error CS" "$R/compile-terrarium.log"   # 0 (consumer compiles)
```

## Evidence to leave (paths under orchestration/runs/sundial/T-SUN-001/)
`REPORT.md` (the CLI contract as implemented, any deviation from the flags above), `editmode.xml`, `dial-g1-plate.png`
+ `.check.json` + `.sbs.png`, `seated-pov.png`, `plate-align.json`, `compile-terrarium.log`.

## Out of scope
Hero objects, gameplay, audio, video encoding inside Unity (ffmpeg assembles sequences outside), any Quest capture.
