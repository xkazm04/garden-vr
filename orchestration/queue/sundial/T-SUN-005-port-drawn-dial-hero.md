---
id: T-SUN-005
app: sundial
title: Port the round-3 drawn dial into the Sundial app as a state-driven DialView with toon ramp, ink hull, boil clock and pinch halo
depends: [T-SUN-004]
estimate_min: 120
touches: [tools/blender/sundial_dial.py, apps/sundial/Assets/Art/**, apps/sundial/Assets/Scripts/**, apps/sundial/Assets/Editor/**, apps/sundial/Assets/Scenes/**, shared/packages/com.gardenvr.fx/Runtime/Shaders/FToon.shader, shared/packages/com.gardenvr.fx/Runtime/Shaders/FCard.shader, shared/packages/com.gardenvr.fx/Runtime/Shaders/FShadowCatcher.shader, orchestration/runs/sundial/T-SUN-005/**]
---
## Goal
The drawn dial from round 3 lives in the app: Blender meshes (disc, bevelled rim, paper-stack edge, soil mound, gnomon,
one raised tile instanced 21 times), the toon ramp with an inverted-hull ink outline on smoothed normals, a 10 fps boil
clock, three plant cards and a pinch halo, all driven by a `DialView` component from state. It renders at `DialG1`
beside the owner's frame with no borrowed plant pixels (use the IWSDK seat's own SVG plant drawings as interim cards).

## Read first
- `docs/art/sundial-style.md` (all); `docs/plans/sundial.md` section 6.
- `docs/contest/seed-unity/FidelityHero.cs` (DrawnDial state) and `FidelityTools.cs` (`BuildDial`, `DialCamera`);
  `tools/blender/blender_heroes.py` (the dial part, `drawn_dial.fbx`).
- `shared/assets/seed-textures/` (round-3 Unity textures: `plant_*.png` there are **keyed from the reference: do not use**)
  and `shared/assets/seed-textures/sundial-drawn/` (IWSDK seat: `dial_face.png`, `plant_<arc>_<0..2>.png` are its own
  SVG drawings, usable as interim art; `halo_*.png`).
- Round-3 Unity section 02 and IWSDK "How each effect is drawn" (cost notes).

## Steps
0. `git merge --no-edit main`. Create `orchestration/runs/sundial/T-SUN-005/`.
1. `tools/blender/sundial_dial.py` (argv `-- --out <dir>`, default `apps/sundial/Assets/Art/Models`): the dial part of
   `blender_heroes.py` plus a raised tile (12 x 9 x 2 mm, bevelled) and a low soil mound; bake **smoothed normals into
   vertex colour RGB** for the outline hull (probe the bake ops in `-b` mode and log the result); print a triangle census.
2. Textures to `apps/sundial/Assets/Art/Textures/` with `PROVENANCE.md` (`dial_face.png` and plants from `sundial-drawn/`:
   "painted by code (IWSDK seat SVG), interim"; nothing from the reference frames).
3. `FToon`: two-step ramp (lit 1.0, shade 0.78 warm), paper grain in object space, an outline pass pushing back faces
   along the vertex-colour normals by a width in pixels (2.5-3.5 px at framing), and a `_BoilTime` jitter (10 fps held
   clock, amplitude <= 1.2 px) on the outline; `FCard`: alpha-to-coverage cards with the same boil jitter in UV; keep
   both shaders backward compatible for Terrarium's use of `FCard`.
4. `Assets/Scripts/Dial/DialView.cs` (implements `ICaptureState`): `halo` (0..1) and `haloTarget` (arc), `gnomonDeg`,
   plant `stage` / `bloom` per arc (cards chosen by state; interim cards map stage 0..2), tile states (an array of 21
   values drawn by an instanced tile with a per-instance state), `boil` on/off, `time`. A `ShadowCatcher` under the dial;
   the gnomon's shadow as a painted decal rotated by `gnomonDeg` (a simple soft wash texture for now).
5. `Assets/Prefabs/Dial.prefab` via `GardenVR.Sundial.Editor.DialSetup.Run` on `DialRoot`; `IntentTarget` ids
   `plant.morning`, `plant.midday`, `plant.winddown`, `tile.<arc>.<0..6>`.
6. Captures: G1 (`DialG1`, `-overlay shared/assets/room-plates/dial-hand-matte.png`, state `halo=1,haloTarget=midday,
   gnomonDeg=105`) and G2 (no overlay, `halo=0,gnomonDeg=125`), side-by-sides vs `A2-05-field-notebook-1.png`; a 3 s
   boil sequence (30 frames) at `DialG1`; `Measure`.
7. Commit per sub-step.

## Acceptance (each check runnable on Windows without a headset; say the command)
```bash
U="C:/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Unity.exe"; R=orchestration/runs/sundial/T-SUN-005
"/c/Program Files/Blender Foundation/Blender 4.2/blender.exe" -b -P tools/blender/sundial_dial.py -- --out apps/sundial/Assets/Art/Models > "$R/blender.log" 2>&1; grep -iE "tris|probe" "$R/blender.log" | tail -6
"$U" -batchmode -nographics -quit -projectPath apps/sundial -executeMethod GardenVR.Sundial.Editor.DialSetup.Run -logFile "$R/setup.log"; grep -c "error CS" "$R/setup.log"   # 0
"$U" -batchmode -quit -projectPath apps/sundial -executeMethod GardenVR.Capture.Editor.CaptureCli.Shot -scene Assets/Scenes/Main.unity -framing DialG1 -target dial -state "halo=1,haloTarget=midday,gnomonDeg=105,time=1" -overlay "$(pwd)/shared/assets/room-plates/dial-hand-matte.png" -reference "$(pwd)/shared/assets/art-reference/A2-05-field-notebook-1.png" -out "$(pwd)/$R/dial-g1.png" -logFile "$R/shot.log"
cat "$R/dial-g1.png.check.json"; ls "$R/dial-g1.sbs.png"        # magentaFrac < 0.001
"$U" -batchmode -quit -projectPath apps/sundial -executeMethod GardenVR.Capture.Editor.CaptureCli.Measure -scene Assets/Scenes/Main.unity -target dial -out "$(pwd)/$R/measure.json" -logFile "$R/measure.log"; cat "$R/measure.json"   # drawsEst <= 24, tris <= 24000 (soft)
grep -c "crop\|keyed" apps/sundial/Assets/Art/Textures/PROVENANCE.md   # 0 rows in use
"$U" -batchmode -nographics -quit -projectPath apps/terrarium -logFile "$R/compile-terrarium.log"; grep -c "error CS" "$R/compile-terrarium.log"   # 0 (FCard change is compatible)
```

## Evidence to leave (paths under orchestration/runs/sundial/T-SUN-005/)
`REPORT.md` (three located findings vs the reference), `blender.log`, `dial-g1.png` + `.sbs.png`, `dial-g2.png`,
`boil.gif` (ffmpeg from the 30 frames), `measure.json`.

## Out of scope
New plant art (T-SUN-007), the dial face pipeline (T-SUN-006), interaction (T-SUN-008), post-processing.
