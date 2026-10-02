---
id: T-TER-002
app: terrarium
title: Build the seated PC rig, room plate and desk anchor in com.gardenvr.room and set up the Terrarium main scene
depends: [T-TER-001, T-SUN-001]
estimate_min: 90
touches: [shared/packages/com.gardenvr.room/**, apps/terrarium/Assets/Scenes/**, apps/terrarium/Assets/Scripts/**, apps/terrarium/Assets/Editor/**, apps/terrarium/Assets/Tests/EditMode/**, orchestration/runs/terrarium/T-TER-002/**]
---
## Goal
A seated rig that stands in for the headset on PC: an eye camera at seated height that turns with the head-pose source
(right-drag), a room plate behind the scene that stands in for passthrough, a desk anchor where the jar will sit, and
the provider interfaces that the Quest phase will implement (`docs/plans/terrarium.md` section 10). You own
`com.gardenvr.room`; Sundial consumes it in T-SUN-004.

## Read first
- `docs/plans/terrarium.md` sections 3 (first-run timings 0-4 s) and 10 (seams); `docs/art/terrarium-style.md`
  sections 3 and 5.
- `apps/terrarium/Assets/Editor/GardenBootstrap.cs` (the existing scene: rig at origin, camera at 1.15 m, DeskAnchor at
  (0, 0.75, 0.5)).
- `shared/packages/com.gardenvr.fx/Runtime/Shaders/FPlate.shader`; `shared/assets/room-plates/` (`plate-jar.png` is the
  night room with the jar inpainted out).
- `shared/packages/com.gardenvr.capture/README.md` (T-SUN-001: `CaptureCli`, framing presets, `Check`).

## Steps
0. `git merge --no-edit main`. Create `orchestration/runs/terrarium/T-TER-002/`.
1. `com.gardenvr.room/Runtime/`: `IRoomProvider { void Show(float fadeSeconds); void Dim(float amount); }`,
   `IPlacementProvider { Pose DeskPose { get; } Plane DeskPlane { get; } event Action Placed; }`.
2. `PcRoomPlate` (implements `IRoomProvider`): a large camera-centred curved card at 4 m using `FPlate`, a
   `Texture2D plate`, `exposure` and a fade from black over `fadeSeconds` (default 2.0). It never renders in an XR build
   (guard with a serialized `pcOnly` flag the Quest phase will turn off).
3. `PcDeskAnchor` (implements `IPlacementProvider`): desk height 0.75 m, distance and lateral offset serialized
   (Terrarium: 0.40 m ahead, 0.30 m below eye with eye at 1.05 m; Sundial will use 0.55 m), a thin mint edge-trace
   card that animates along the desk's near edge in 0.8 s (`docs/plans/terrarium.md` t = 2.5 s), `Placed` raised after it.
4. `SeatedRig` (MonoBehaviour): eye height (default 1.05 m), reads `IHeadPoseSource` (from `com.gardenvr.input`) for
   rotation only, clamps yaw +/- 40 deg and pitch +/- 25 deg, camera FOV 60 (PC), near clip 0.02. An editor menu and a
   batch method `GardenVR.Room.Editor.RoomSetup.BuildRigPrefab` create `Packages/com.gardenvr.room/Runtime/Prefabs/SeatedRig.prefab`.
5. Terrarium scene: an editor batch method `GardenVR.Terrarium.Editor.SceneSetup.Run` (in
   `apps/terrarium/Assets/Editor/`) that makes `Assets/Scenes/Main.unity` use the prefab, `KeyboardMouseIntentSource`,
   `PcRoomPlate` with `plate-jar.png` (copy it to `apps/terrarium/Assets/Art/Plates/` with a `PROVENANCE.md` row:
   "room plate, inpainted from the owner's reference frame, development only, never shipped"), and `PcDeskAnchor`.
   Idempotent (safe to re-run).
6. EditMode tests (in the Terrarium test asmdef from T-TER-001): clamp limits, recentre, desk pose from settings,
   plate fade reaches full exposure at `fadeSeconds`.
7. Captures with the capture package: the seated POV (`-framing SeatedPOV`) and the jar reference framing
   (`-framing JarG1`, plate only, no jar yet); run `Check` on both.
8. Commit per sub-step.

## Acceptance (each check runnable on Windows without a headset; say the command)
```bash
U="C:/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Unity.exe"; R=orchestration/runs/terrarium/T-TER-002
"$U" -batchmode -nographics -quit -projectPath apps/terrarium -executeMethod GardenVR.Terrarium.Editor.SceneSetup.Run -logFile "$R/setup.log"; echo "exit=$?"   # 0
grep -c "error CS" "$R/setup.log"   # 0
"$U" -batchmode -nographics -projectPath apps/terrarium -runTests -testPlatform EditMode -testResults "$(pwd)/$R/editmode.xml" -logFile "$R/editmode.log"; grep -o '<test-run[^>]*>' "$R/editmode.xml"   # failed="0", total grew by >= 4
"$U" -batchmode -quit -projectPath apps/terrarium -executeMethod GardenVR.Capture.Editor.CaptureCli.Shot -scene Assets/Scenes/Main.unity -framing SeatedPOV -out "$(pwd)/$R/seated-pov.png" -logFile "$R/cap1.log"
"$U" -batchmode -quit -projectPath apps/terrarium -executeMethod GardenVR.Capture.Editor.CaptureCli.Shot -scene Assets/Scenes/Main.unity -framing JarG1 -out "$(pwd)/$R/jar-g1-plate.png" -logFile "$R/cap2.log"
cat "$R/seated-pov.png.check.json" "$R/jar-g1-plate.png.check.json"   # meanLuma 0.03..0.6, magentaFrac < 0.001, blackFrac < 0.5
```
(If `CaptureCli` flags differ from the above, use the ones in the capture package README and say so in the report.)

## Evidence to leave (paths under orchestration/runs/terrarium/T-TER-002/)
`REPORT.md`, `setup.log`, `editmode.xml`, `seated-pov.png`, `jar-g1-plate.png` and their `.check.json`.

## Out of scope
Passthrough, MRUK, anchors, any Meta package; the jar itself (T-TER-004); framing presets (they live in the capture
package, owned by sundial; request changes via `REQUEST-capture.md`).
