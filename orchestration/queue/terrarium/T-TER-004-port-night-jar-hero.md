---
id: T-TER-004
app: terrarium
title: Port the round-3 night jar into the Terrarium app as a state-driven JarView and capture it at the reference framing
depends: [T-TER-002, T-SUN-001]
estimate_min: 120
touches: [tools/blender/terrarium_jar.py, apps/terrarium/Assets/Art/**, apps/terrarium/Assets/Scripts/**, apps/terrarium/Assets/Editor/**, apps/terrarium/Assets/Scenes/**, shared/packages/com.gardenvr.fx/Runtime/Shaders/FGlass.shader, shared/packages/com.gardenvr.fx/Runtime/Shaders/FGlow.shader, orchestration/runs/terrarium/T-TER-004/**]
---
## Goal
The jar from the round-3 fidelity gate (Blender meshes, painted textures, fx shaders) lives in the app as a prefab with
a `JarView` component whose look is driven only by state parameters (`breath`, `uncoil`, `fog`, `answer`, `vitality`,
`time`), placed on the desk anchor, and captured at the G1 reference framing beside the owner's frame. This is the
starting point of every art task; it must render without placeholder materials.

## Read first
- `docs/art/terrarium-style.md` (all); `docs/plans/terrarium.md` section 6.
- `docs/contest/seed-unity/FidelityHero.cs` (the state parameters) and `FidelityTools.cs` (`BuildJar`, materials,
  `JarCamera`, `Settle`, `Measure`); `tools/blender/blender_heroes.py` (the jar part, exported as `night_jar.fbx`).
- `shared/packages/com.gardenvr.fx/Runtime/Shaders/F*.shader`; `shared/assets/seed-textures/` (round-3 textures).
- Round-3 report section 01 and 04: `C:\Users\kazda\kiro\personas\.contest\arena\habit-garden-r2-r3\entries\claude-claude-opus-5-5_high-v1\variant-1\index.html`.

## Steps
0. `git merge --no-edit main`. Create `orchestration/runs/terrarium/T-TER-004/`.
1. `tools/blender/terrarium_jar.py`: copy the jar part of `blender_heroes.py` (Jar, Cork, Soil, Moss, Fiddle0..100,
   Frond, Seedling, SeedStem) with `OUT` taken from argv (`-- --out <dir>`), default
   `apps/terrarium/Assets/Art/Models`; keep the triangle census print. Run it and keep the log.
2. Copy the jar textures from `shared/assets/seed-textures/` to `apps/terrarium/Assets/Art/Textures/` and write
   `apps/terrarium/Assets/Art/Textures/PROVENANCE.md` with one row per file: `file | origin (painted-by-code /
   crop-from-reference / generated) | source | status`. Per the round-3 report, `moss_band`, `soil_band` and the cork
   bands are **crop-from-reference: stand-in, art debt** (gate A4); mark them so.
3. `Assets/Scripts/Jar/JarView.cs` (namespace `GardenVR.Terrarium`): builds or binds the jar hierarchy from the FBX,
   creates materials from the fx shaders (`Fidelity/Glass`, `Fidelity/Glow`, `Fidelity/Card`), and maps state to look
   exactly as `FidelityHero` did (uncoil blends the five coil meshes on the CPU; fog drives the glass; answer shows the
   frond-stays state, dew bead, gold ring; breath fills the ring; time drives mist and spores). Add `vitality` (0.6..1)
   scaling every frond emission. Use the lean variant from round 3 (smaller desk spill and jar halo, no glass back pass
   if it is invisible at framing) as the default. Implement the capture package's `ICaptureState` so `-state` strings
   set these fields by name.
4. `Assets/Prefabs/Jar.prefab` built by `GardenVR.Terrarium.Editor.JarSetup.Run` (batch method, idempotent); the
   Main scene gets the jar on `PcDeskAnchor`; `IntentTarget` ids `jar` and `jar.cork`.
5. Capture with the capture package at framing `JarG1` (the round-3 `JarCamera`), state = G1 (breath 0.5, uncoil 0.3,
   fog 0.45, time 3), and at `SeatedPOV`; compose the side-by-side against
   `shared/assets/art-reference/A1-03-night-moss-1.png`; run `Measure`.
6. If `FGlass`/`FGlow` need a fix to render (you own these two shaders), keep the property names unchanged.
7. Commit per sub-step.

## Acceptance (each check runnable on Windows without a headset; say the command)
```bash
U="C:/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Unity.exe"; R=orchestration/runs/terrarium/T-TER-004
"/c/Program Files/Blender Foundation/Blender 4.2/blender.exe" -b -P tools/blender/terrarium_jar.py -- --out apps/terrarium/Assets/Art/Models > "$R/blender.log" 2>&1; grep -i "tris" "$R/blender.log" | tail -3
"$U" -batchmode -nographics -quit -projectPath apps/terrarium -executeMethod GardenVR.Terrarium.Editor.JarSetup.Run -logFile "$R/setup.log"; grep -c "error CS" "$R/setup.log"   # 0
"$U" -batchmode -quit -projectPath apps/terrarium -executeMethod GardenVR.Capture.Editor.CaptureCli.Shot -scene Assets/Scenes/Main.unity -framing JarG1 -state "breath=0.5,uncoil=0.3,fog=0.45,time=3" -reference "$(pwd)/shared/assets/art-reference/A1-03-night-moss-1.png" -out "$(pwd)/$R/jar-g1.png" -logFile "$R/cap.log"
cat "$R/jar-g1.png.check.json"      # magentaFrac < 0.001, blackFrac < 0.6, meanLuma 0.03..0.5
ls "$R/jar-g1.sbs.png"              # side-by-side exists
"$U" -batchmode -quit -projectPath apps/terrarium -executeMethod GardenVR.Capture.Editor.CaptureCli.Measure -scene Assets/Scenes/Main.unity -target jar -out "$(pwd)/$R/measure.json" -logFile "$R/measure.log"
cat "$R/measure.json"               # drawsEst <= 24, tris <= 30000, transparentMeanLayers reported
```
(The `-state` string sets `JarView` fields by name through the capture package's state hook; if T-SUN-001 named it
differently, follow its README and say so.)

## Evidence to leave (paths under orchestration/runs/terrarium/T-TER-004/)
`REPORT.md` (include the side-by-side image link and three located findings: where the render differs most and what
would fix each), `blender.log`, `setup.log`, `jar-g1.png`, `jar-g1.sbs.png`, `seated-pov.png`, `measure.json`.

## Out of scope
Breath input wiring (T-TER-005); new art (T-TER-006/007); post-processing of any kind.
