# Garden VR Input (`com.gardenvr.input`)

Hand intents for both apps. App code subscribes to `IHandIntentSource` and never reads a keyboard, mouse, or (later) a headset. This package is the only place that does.

Owner: terrarium (`docs/PLAN.md`). Sundial consumes the same assembly.

## Types

| Type | Role |
|---|---|
| `HandIntent` / `IHandIntentSource` | The six intents, analog `PinchStrength`, `IsTracked`, `BindingHint`, and `SystemPause` |
| `IHeadPoseSource` | Seated head rotation and `Recentred` |
| `KbmIntentMapper` | Pure mapping from a `RawKbm` frame plus a look-ray function. No device reads |
| `KeyboardMouseIntentSource` | The only behaviour that touches `Keyboard.current` and `Mouse.current` |
| `KeyboardMouseHeadPose` | Applies the mapper's head offset on a pivot above the eye camera |
| `IntentTarget` / `IntentTargetRegistry` | Stable ids (`jar`, `seed.walk`). Raycast and Tab skip inactive or disabled targets |
| `ScriptedIntentSource` | JSONL playback by target id. `Tick(dt)` for tests, `Update` uses `Time.deltaTime` |

`HandIntent.TargetId` is optional and defaults to null, so older constructors still compile.

## PC bindings

`holdMode` defaults to `Hold`. Set `KeyboardMouseIntentSource.HoldMode` (or the mapper) to `Toggle` for the accessibility latch.

| Input | Result |
|---|---|
| Left mouse released before 0.30 s | `Pinch` on the cursor ray (a `Poke` instead when that target is `pokeOnly`) |
| Left mouse or Space held for 0.30 s or more | `PinchHold` every frame. `Held` is seconds since the press began |
| Release of that hold | `Release`. `Held` is the total hold, including the time before 0.30 s |
| Space tap shorter than 0.30 s, hold mode | Nothing. Space is the breath key, not a click |
| Enter | `Pinch` on the focused target (or `Poke` if that target is `pokeOnly`) |
| Strength | Smoothstep 0 to 1 over 0.12 s on press, and 1 to 0 over 0.12 s on release. 0.8 and 0.5 both fall inside that window, so `PinchDetector` engages and releases on it |
| `holdMode = Toggle` | A Space press latches the breath on. The next Space press releases it. `Held` is the latched time. Left mouse still clicks and holds |
| Mouse ray | `Look` every frame |
| Dwell 0.15 s on a new `IntentTarget` | The focused id changes. Looking at empty space does not clear it |
| Tab / Shift+Tab | Next / previous target by ordinal id. Wraps. Skips hidden and disabled targets |
| F | `Poke` once per press, at the cursor |
| P or middle mouse held 0.6 s | `PalmOpen` once. Holding longer does not repeat it. Releasing arms it again |
| Right-drag | Yaw +/- 40 deg, pitch +/- 25 deg, 0.15 deg per pixel. Mouse up looks up. No translation |
| R | Recentre the head offset and raise `Recentred` |
| Esc, or `OnApplicationFocus(false)` | `SystemPause`. Not an intent |
| F1, F2, `[`, `]`, `T` | `DevCommand`: `StateOverlay`, `AutoPace`, `PreviousDay`, `NextDay`, `ClockFast` (T toggles the clock at sixty times). Raised only when `Debug.isDebugBuild` or `Application.isEditor` (tests inject the flag on the mapper). `ClockFast` is additive: older readers of the first four values are unchanged |

`BindingHint` on PC: PinchHold `"Space or mouse"`, Pinch `"click"`, Poke `"F"`, PalmOpen `"hold P"`.

Put `KeyboardMouseHeadPose` on a pivot above the eye camera. `LocalRotation` is the drag offset (identity after R). The pivot keeps the rotation it was given and multiplies the offset on top, so the camera can keep a seated pitch.

The project must enable the Input System in Player Settings (Active Input Handling: Input System Package, or Both). While it is set to the old Input Manager, `Keyboard.current` stays null and the provider does nothing.

## JSONL playback

One JSON object per line. Blank lines are skipped. A line the parser cannot read throws `ScriptedIntentParseException`. The message starts with `line N:` and `LineNumber` is 1-based. A failed load leaves the previous script in place. There is no Newtonsoft dependency.

```json
{"t":10.0,"intent":"PinchHold","target":"jar","dur":4.2}
{"t":14.2,"intent":"Release"}
{"t":20,"intent":"Look","target":"seed.walk","dur":0.4}
{"t":21,"intent":"Pinch","target":"seed.walk"}
{"t":30,"intent":"PalmOpen"}
{"t":31,"intent":"Lost","dur":2.0}
```

| Field | Meaning |
|---|---|
| `t` | Seconds from the start of the script. Required, zero or greater |
| `intent` | `Look`, `Pinch`, `PinchHold`, `Release`, `Poke`, `PalmOpen`, or `Lost`. Required |
| `target` | `IntentTarget` id. Optional. The ray aims at that object's position when it is registered |
| `dur` | Seconds the event stays active. Optional |

`PinchHold` keeps `PinchStrength` ramped up for `dur` (smoothstep in over 0.12 s, then held at 1) and raises `PinchHold` each tick. `Release` raises `Release` and ramps strength back down. `Look` with `dur` raises `Look` for that long. `Lost` is not a `HandIntent`: `IsTracked` is false until `dur` elapses, then tracking returns. `Finished` becomes true once every event has started and every `dur` has elapsed.

`Poke` is accepted on the same line shape. Lines should be in time order; the player sorts them by `t` and keeps file order for equal times.
