# Garden VR Room (`com.gardenvr.room`)

PC stand-in for the headset: a seated eye, a curved room plate, and a desk anchor. Quest replaces the providers later. App code talks to `IRoomProvider` and `IPlacementProvider`.

Owner: terrarium (`docs/PLAN.md`). Sundial consumes the prefab. Do not edit this package from Sundial; ask for changes with `REQUEST-room.md`.

Depends on `com.gardenvr.input` for `IHeadPoseSource` and `KeyboardMouseIntentSource`. The plate and the desk trace use `Fidelity/Plate` and `Fidelity/Card` from `com.gardenvr.fx` (both apps already reference that package). This package does not reference `com.gardenvr.fx` in `package.json`, so `packages-lock.json` stays as it is.

## Types

| Type | Role |
|---|---|
| `IRoomProvider` | `Show(fadeSeconds)` fades the room in from black. `Dim(amount)` is 0 at the faded exposure and 1 at black |
| `PcRoomPlate` | Flat card in front of the eye, on `RoomPlateAnchor` (not parented to the camera), sized to fill the 60 deg seated lens. Terrarium parks it at 4 m. Sundial's scene card is at 2.2 m. Each app assigns its own seated plate (a near desk, empty room). `Fidelity/Plate`, `exposure`. Default fade is 2.0 s. `pcOnly` defaults on. Turn it off for Quest so the card never renders. G1 captures disable this object and draw `plate-jar.png` / `plate-dial.png` themselves |
| `IPlacementProvider` | `DeskPose`, `DeskPlane`, and `Placed` |
| `PcDeskAnchor` | Local pose `(lateral, height, distance)` on the rig. Height defaults to 0.75 m (0.30 m below a 1.05 m eye). Distance defaults to 0.40 m (Terrarium) and the shared sundial constant is 0.55 m. The terrarium scene sits the jar at 0.27 m on a 0.85 m desk so the 14 cm glass is about a third of the 60 deg frame. The sundial scene sits the dial at 0.26 m on a 0.90 m desk so a 30 cm dial fills about half the frame width. A mint card (`#8FF0C8`) traces the near edge only while the intro is drawing, for 0.8 s starting at t = 2.5 s. It stays hidden before that, after `Placed`, and in edit-mode captures. `Placed` fires once when that trace finishes |
| `SeatedRig` | Eye height 1.05 m. Reads `IHeadPoseSource` for rotation only and clamps yaw to +/- 40 deg and pitch to +/- 25 deg. That rotation is an offset on the rest pitch, which aims the eye at the desk anchor, then subtracts `LookAboveDesk` (0 aims at the desk; the terrarium scene uses 16 deg so the jar sits lower). Camera FOV 60, near clip 0.02. R on the keyboard/mouse provider recentres the offset. Edit mode applies the rest pose in `OnEnable`, because captures do not enter play mode |

`Show` is called from `Start`. Edit-mode captures do not enter play mode, so the saved plate material stays at full exposure and the desk trace stays hidden. In play mode the plate fades up from black and the trace plays at 2.5 s, then hides.

## Prefab

`Packages/com.gardenvr.room/Runtime/Prefabs/SeatedRig.prefab`

```
SeatedRig          SeatedRig, KeyboardMouseIntentSource
  HeadPose         KeyboardMouseHeadPose (the IHeadPoseSource; not the camera)
  HeadPivot        eye height, rest pitch aimed at the desk
    EyeCamera      Camera, AudioListener, FOV 60, near 0.02, solid night clear
  RoomPlateAnchor  same rest pose as the head, does not follow the look offset
    PcRoomPlate    card filling the 60 deg seated lens, Fidelity/Plate
  PcDeskAnchor     desk pose. Parent the hero here (DialRoot, the jar)
    DeskTrace      mint edge card
```

Menu: **Garden VR / Room / Build Seated Rig Prefab**.

Batch, from a project that references this package:

```bash
"C:/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Unity.exe" -batchmode -nographics -quit -projectPath apps/terrarium -executeMethod GardenVR.Room.Editor.RoomSetup.BuildRigPrefab
```

Terrarium's scene builder is `GardenVR.Terrarium.Editor.SceneSetup.Run`. It instances this prefab, copies `plate-jar.png` and `plate-jar-seated.png` to `Assets/Art/Plates/`, assigns the seated plate on the scene `PcRoomPlate`, sets the scene desk to 0.27 m ahead at height 0.85 m, and looks 16 deg above that desk. The shared `TerrariumDistance` constant stays 0.40 m. Sundial should instance the same prefab, assign `plate-dial-seated.png` on the scene `PcRoomPlate` (do not write the dial texture into the shared material), and set the scene desk to 0.26 m ahead at height 0.90 m. The shared `SundialDistance` constant stays 0.55 m. The rig then aims the eye at that desk. `plate-jar.png` and `plate-dial.png` stay the G1 capture plates.

The plate texture on the shared prefab is empty. Each app assigns its own material on the scene instance.
