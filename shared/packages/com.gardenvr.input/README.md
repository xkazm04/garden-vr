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

## Speech input

`ISpeechToText` supplies `Begin()`, `End()`, `Available`, and `Final` / `Partial` string events.
It is additive: no existing hand interface or mapping changes. Consumers start from `PinchHold` and stop on
`Release`, lifecycle pause, tracking loss, or shutdown. Providers never write transcripts to disk.
`ISpeechCaptureState.Listening` optionally exposes actual capture state for the listening indicator.

`WindowsSpeechSource` lazily creates `DictationRecognizer` on Windows, only when `Begin` is called. `End` stops
capture but leaves the result callbacks connected for native completion. `Dispose` releases the native resource.
`Available` is optimistic before the first attempt; a start/error failure makes it false for that instance.
Call `Poll()` each frame (Sundial does) to observe asynchronous startup and bound a stalled start to three seconds.
Non-Windows builds report unavailable. The provider does not enable Windows permissions or change speech settings.
Windows dictation depends on the machine's speech service/privacy configuration; this adapter does not establish
an offline guarantee. Keep the preset path available. Diagnostics include device state and HRESULT, never text.

`ScriptedSpeechSource.Load(json)` accepts playback file contents. Each `Begin` selects the next utterance;
repeated `Begin` during a hold is harmless. `Tick(seconds)` advances events during capture only. `End` discards
unplayed events and never promotes a partial to a final. Exhausted/empty scripts emit nothing. Invalid loads
leave the previous playback intact. A consumer drives Tick (Sundial's SpeechController does so in Update).

```json
{"utterances":[{"events":[{"t":0.1,"kind":"partial","text":"read"},{"t":0.5,"kind":"final","text":"read a little"}]}]}
```

Sundial keeps the adapter dormant until a speech context calls `Arm(targetId)`. Consumers must call
`CompleteCoachCall()` when the request completes or is cancelled. This clears accumulated final and partial
text and rejects late callbacks. No text enters SundialSave. Preset onboarding remains the default in B3.
On device failure it selects an empty scripted source and reports `UsingPresetFallback=true`, `Available=false`;
it never fabricates a player answer. Tests inject a loaded scripted source explicitly using `SetSpeech`.
The development player alone accepts `--speech-check` (manual Space, speak, release; 30-second check) or
`--speech-probe` (scripted hold/release, real Windows provider; no human speech claim). Only this explicitly
requested development harness logs final text; do not use sensitive phrases in the check. Both modes use an
isolated temporary save and exit themselves. Build with `GardenVR.Sundial.Editor.Build.WindowsDevelopment`.

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
# Optional continuous palm state

`IPalmStateSource` adds `PalmHeld` and `PalmReleased` without changing `IHandIntentSource`, intent kinds or bindings.
Keyboard/mouse reports the hold from the initial press; the existing single `PalmOpen` still commits at 0.6 seconds.
Release, focus loss, provider disable or unavailable input ends the hold. After loss, release and begin a fresh gesture.
Future tracked-hand providers can implement the same optional interface and end the hold on tracking loss.
Consumers that need sustained poses check for this capability; a one-shot `PalmOpen` must never imply an indefinite hold.
