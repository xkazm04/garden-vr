---
id: T-SUN-004
app: sundial
title: Set up the Sundial main scene on the shared seated rig with the kitchen plate and desk anchor, aligned to the DialG1 framing
depends: [T-TER-002, T-SUN-001]
estimate_min: 75
touches: [apps/sundial/Assets/Scenes/**, apps/sundial/Assets/Editor/**, apps/sundial/Assets/Scripts/**, apps/sundial/Assets/Art/Plates/**, apps/sundial/Assets/Capture/**, orchestration/runs/sundial/T-SUN-004/**]
---
## Goal
The Sundial app gets the same PC stand-in for the headset as Terrarium (seated rig, room plate, desk anchor from
`com.gardenvr.room`, keyboard/mouse provider from `com.gardenvr.input`), set for daylight at a kitchen table, with the
dial's anchor 0.55 m ahead and a `DialG1` capture that lines up pixel-for-pixel with the owner's frame outside the dial.

## Read first
- `docs/plans/sundial.md` sections 2 (item 1: size, distance, tilt) and 10; `docs/art/sundial-style.md` sections 3, 5.
- `shared/packages/com.gardenvr.room/README.md` and `shared/packages/com.gardenvr.input/README.md` (owned by terrarium:
  consume, do not edit; request changes via `REQUEST-room.md` / `REQUEST-input.md` in your run folder).
- `shared/packages/com.gardenvr.capture/README.md` (yours).

## Steps
0. `git merge --no-edit main`. Create `orchestration/runs/sundial/T-SUN-004/`.
1. `apps/sundial/Assets/Editor/SceneSetup.cs` batch method `GardenVR.Sundial.Editor.SceneSetup.Run` (idempotent):
   `Main.unity` with the `SeatedRig` prefab, `KeyboardMouseIntentSource`, `PcRoomPlate` with `plate-dial.png` (copied to
   `Assets/Art/Plates/` with a `PROVENANCE.md` row: development only, never shipped), `PcDeskAnchor` at 0.55 m ahead,
   and an empty `DialRoot` on the anchor tilted 12 deg toward the user with `IntentTarget` id `dial`.
2. Daylight: one directional light whose direction matches the plate's window (upper left in ref-1), used only by the toon
   ramp later; ambient flat.
3. `Assets/Capture/framings.json`: confirm `DialG1` places the camera relative to `DialRoot` the way round 3 did (dial
   centre at pixel (785, 615)); add `DialSeated` (the rig's eye, 90 deg).
4. Captures: `DialG1` plate-only and `SeatedPOV`; `Diff` against the reference outside `dial-mask.png`.
5. Commit per sub-step.

## Acceptance (each check runnable on Windows without a headset; say the command)
```bash
U="C:/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Unity.exe"; R=orchestration/runs/sundial/T-SUN-004
"$U" -batchmode -nographics -quit -projectPath apps/sundial -executeMethod GardenVR.Sundial.Editor.SceneSetup.Run -logFile "$R/setup.log"; grep -c "error CS" "$R/setup.log"   # 0
"$U" -batchmode -quit -projectPath apps/sundial -executeMethod GardenVR.Capture.Editor.CaptureCli.Shot -scene Assets/Scenes/Main.unity -framing DialG1 -target dial -out "$(pwd)/$R/dial-g1-plate.png" -logFile "$R/shot.log"; cat "$R/dial-g1-plate.png.check.json"
"$U" -batchmode -quit -projectPath apps/sundial -executeMethod GardenVR.Capture.Editor.CaptureCli.Diff -a "$(pwd)/$R/dial-g1-plate.png" -b "$(pwd)/shared/assets/art-reference/A2-05-field-notebook-1.png" -mask "$(pwd)/shared/assets/room-plates/dial-mask.png" -out "$(pwd)/$R/plate-align.json" -logFile "$R/diff.log"; cat "$R/plate-align.json"   # outsideMask < 0.03
"$U" -batchmode -quit -projectPath apps/sundial -executeMethod GardenVR.Capture.Editor.CaptureCli.Shot -scene Assets/Scenes/Main.unity -framing SeatedPOV -out "$(pwd)/$R/seated-pov.png" -logFile "$R/shot2.log"; cat "$R/seated-pov.png.check.json"   # magentaFrac < 0.001
grep -rnE "Keyboard\.current|Mouse\.current|Input\.Get" apps/sundial/Assets --include=*.cs   # nothing (S1)
```

## Evidence to leave (paths under orchestration/runs/sundial/T-SUN-004/)
`REPORT.md`, `setup.log`, `dial-g1-plate.png`, `seated-pov.png`, `plate-align.json`.

## Out of scope
The dial itself (T-SUN-005); changes to the room or input packages; passthrough or anchors.
