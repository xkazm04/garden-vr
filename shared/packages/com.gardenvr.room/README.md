# Garden VR Room (`com.gardenvr.room`)

PC stand-in for the headset: a seated eye, a curved room plate, and a desk anchor. Quest replaces the providers later. App code talks to `IRoomProvider` and `IPlacementProvider`.

Owner: terrarium (`docs/PLAN.md`). Sundial consumes the prefab. Do not edit this package from Sundial; ask for changes with `REQUEST-room.md`.

Depends on `com.gardenvr.input` for `IHeadPoseSource` and `KeyboardMouseIntentSource`. The plate and the desk trace use `Fidelity/Plate` and `Fidelity/Card` from `com.gardenvr.fx` (both apps already reference that package). This package does not reference `com.gardenvr.fx` in `package.json`, so `packages-lock.json` stays as it is.

## Types

| Type | Role |
|---|---|
| `IRoomProvider` | `Show(fadeSeconds)` fades the room in from black. `Dim(amount)` is 0 at the faded exposure and 1 at black |
| `PcRoomPlate` | Curved card at 4 m, parented to the eye so it stays camera-centred. `Fidelity/Plate`, a `Texture2D`, and `exposure`. Default fade is 2.0 s. `pcOnly` defaults on. Turn it off for Quest so the card never renders |
| `IPlacementProvider` | `DeskPose`, `DeskPlane`, and `Placed` |
| `PcDeskAnchor` | Local pose `(lateral, height, distance)` on the rig. Height defaults to 0.75 m (0.30 m below a 1.05 m eye). Distance defaults to 0.40 m (Terrarium). Sundial sets 0.55 m on the scene instance. A mint card (`#8FF0C8`) traces the near edge, toward the user, for 0.8 s starting at t = 2.5 s. `Placed` fires once when that trace finishes |
| `SeatedRig` | Eye height 1.05 m. Reads `IHeadPoseSource` for rotation only and clamps yaw to +/- 40 deg and pitch to +/- 25 deg. That rotation is an offset on the seated look-down (the desk in the centre of the view). Camera FOV 60, near clip 0.02. R on the keyboard/mouse provider recentres the offset |

`Show` is called from `Start`. Edit-mode captures do not enter play mode, so the saved plate material stays at full exposure and the desk trace stays drawn. In play mode the plate fades up from black and the trace plays at 2.5 s.

## Prefab

`Packages/com.gardenvr.room/Runtime/Prefabs/SeatedRig.prefab`

```
SeatedRig          SeatedRig, KeyboardMouseIntentSource
  HeadPose         KeyboardMouseHeadPose (the IHeadPoseSource; not the camera)
  HeadPivot        eye height, seated pitch
    EyeCamera      Camera, AudioListener, FOV 60, near 0.02, solid night clear
      PcRoomPlate  curved card, Fidelity/Plate
  PcDeskAnchor     desk pose. Parent the hero here (DialRoot, the jar)
    DeskTrace      mint edge card
```

Menu: **Garden VR / Room / Build Seated Rig Prefab**.

Batch, from a project that references this package:

```bash
"C:/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Unity.exe" -batchmode -nographics -quit -projectPath apps/terrarium -executeMethod GardenVR.Room.Editor.RoomSetup.BuildRigPrefab
```

Terrarium's scene builder is `GardenVR.Terrarium.Editor.SceneSetup.Run`. It instances this prefab, copies `plate-jar.png` to `Assets/Art/Plates/`, and sets the desk to 0.40 m. Sundial should instance the same prefab, assign `plate-dial.png` on the scene `PcRoomPlate` (do not write the dial texture into the shared material), and set `PcDeskAnchor.Distance` to 0.55 on the instance.

The plate texture on the shared prefab is empty. Each app assigns its own material on the scene instance.
