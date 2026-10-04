# Garden VR Capture (`com.gardenvr.capture`)

Batchmode shots, image checks, labelled side-by-sides, pixel diffs, budget measurement, one-launch variant sweeps, and PlayMode sequence recording. Terrarium and Sundial both call this package. The command line below is the contract.

Owner: Sundial (`docs/PLAN.md`). Shaders `Fidelity/Plate` and `Fidelity/Overdraw` live in `com.gardenvr.fx`, which both apps already reference.

## Assemblies

| Assembly | Where | What |
|---|---|---|
| `GardenVR.Capture` | `Runtime/` | Framings, image check, crops, diff, side-by-side, budget counts, overdraw, `SequenceRecorder`, `PlaybackHarness`. No `UnityEditor`. |
| `GardenVR.Capture.Editor` | `Editor/` | `GardenVR.Capture.Editor.CaptureCli` batch entry points. |

## ICaptureState

```csharp
public interface ICaptureState
{
    void ApplyCaptureState(IReadOnlyDictionary<string, string> state);
}
```

`-state "k=v,k=v"` is parsed into that dictionary (the value may contain `=`) and passed to every `ICaptureState` in the open scene. Unknown keys are the component's problem. A non-empty `-state` with no host fails the entry. The side-by-side caption prints the raw state string, or `none`.

## Framings

A framing is a camera pose relative to `-target` (an `IntentTarget` id, else an object name). With no `-target`, positions are world space. `SeatedPOV` with no `-target` copies the scene `EyeCamera` pose and forces the framing field of view.

`lensShift` is the round-3 screen offset in degrees, applied after LookAt as `Quaternion.Euler(-y, x, 0)`. It is not `Camera.lensShift`. The built-in numbers are `FidelityTools.JarCamera` and `DialCamera`.

| Name | Eye | Look-at | FOV | Lens shift (deg) | Plate |
|---|---|---|---|---|---|
| `JarG1` | (0, 0.175, -0.44) | (0, 0.072, 0) | 28.2 | (0, -0.4) | `plate-jar.png` |
| `DialG1` | (0, 0.322, -0.411) | (0, 0.012, 0) | 30 | (3.6, 0.5) | `plate-dial.png` |
| `SeatedPOV` | (0, 1.15, 0), pitch 28 deg down | along that view | 90 | (0, 0) | none |

Default resolution is 1824 x 1024. A plate, overlay, reference, or mask path is used as given when it is absolute and the file exists. Otherwise the name is looked up in `Assets/Capture/`, then `shared/assets/room-plates/`, then `shared/assets/art-reference/`, then as a path from the repo root. A framing with a plate is drawn as a screen-aligned quad that fills the view (point sampled), and any world object named `PcRoomPlate` or `PassthroughPlate` is disabled for that shot.

`Assets/Capture/framings.json` (missing file = built-ins only):

```json
{
  "overrides": {
    "DialG1": { "fov": 30, "lensShift": [3.6, 0.5] }
  },
  "additions": [
    {
      "name": "DeskClose",
      "eye": [0, 1.2, -0.4],
      "lookAt": [0, 0.7, 0.2],
      "fov": 55,
      "lensShift": [0, 0],
      "plate": null,
      "overlay": null,
      "width": 1824,
      "height": 1024
    }
  ]
}
```

An override replaces only the fields it sets. `plate: null` clears a plate. A new name needs `eye`, `lookAt`, and `fov`.

## Batch entry points

Unity: `C:/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Unity.exe`

Every entry logs `[Capture] <entry> OK <out>` or `[Capture] <entry> FAIL <reason>` and exits non-zero on failure. Shot and Measure need a GPU (do not pass `-nographics`). Before a shot or a measure, async shader compilation is turned off and one warm-up render is discarded.

Paths below assume the repo root is the working directory. `R` is your run folder.

### Shot

```bash
"$U" -batchmode -quit -projectPath apps/sundial -executeMethod GardenVR.Capture.Editor.CaptureCli.Shot \
  -scene Assets/Scenes/Main.unity -framing DialG1 \
  -reference "$(pwd)/shared/assets/art-reference/A2-05-field-notebook-1.png" \
  -out "$(pwd)/$R/dial-g1-plate.png" -logFile "$R/shot.log"
```

Flags: `-scene <asset path> -framing <name> [-target <id or name>] [-state "k=v,k=v"] [-reference <png>] [-overlay <png>] [-w 1824] [-h 1024] [-msaa 8] -out <absolute png>`.

Writes `<out>`, `<out>.check.json` (`meanLuma`, `blackFrac`, `whiteFrac`, `magentaFrac`, `width`, `height`, `sha256` of the PNG), and, with `-reference`, `<stem>.sbs.png`. The label strip reads `REFERENCE <file> | RENDER <file>` and `Unity URP, batchmode, <framing>, <state>`.

`-overlay`: a PNG with partial alpha is composited over the shot. An opaque PNG is a matte: white pixels are replaced from `-reference` (the hand, laid back on).

Image check thresholds: black is luma < 0.02, white is luma >= 0.98, magenta is the Unity error colour (R and B high, G low). Luma is Rec.709.

### Crops

```bash
"$U" -batchmode -quit -projectPath apps/sundial -executeMethod GardenVR.Capture.Editor.CaptureCli.Crops \
  -a "$(pwd)/$R/ref.png" -b "$(pwd)/$R/shot.png" \
  -regions "700,180,440,200;742,175,340,80" \
  -out "$(pwd)/$R/crops.png" -logFile "$R/crops.log"
```

Regions are top-left pixel coordinates, `x,y,w,h` separated by `;`. Each region is one row: crop of A, then the same pixels of B.

### Diff

```bash
"$U" -batchmode -quit -projectPath apps/sundial -executeMethod GardenVR.Capture.Editor.CaptureCli.Diff \
  -a "$(pwd)/$R/dial-g1-plate.png" \
  -b "$(pwd)/shared/assets/art-reference/A2-05-field-notebook-1.png" \
  -mask "$(pwd)/shared/assets/room-plates/dial-mask.png" \
  -out "$(pwd)/$R/plate-align.json" -logFile "$R/diff.log"
```

JSON fields: `meanAbs`, `outsideMask`, `insideMask`, `width`, `height`. Difference is the mean absolute per-channel delta of the raw bytes, 0..1. A mask pixel with red >= 128 is inside. With no mask, every pixel is outside and `insideMask` is 0.

### Measure

```bash
"$U" -batchmode -quit -projectPath apps/sundial -executeMethod GardenVR.Capture.Editor.CaptureCli.Measure \
  -scene Assets/Scenes/Main.unity -target DeskAnchor -framing DialG1 \
  -out "$(pwd)/$R/budget.json" -logFile "$R/measure.log"
```

Required: `-scene -target [-state] -out`. Optional: `-framing` (default `SeatedPOV`), `-w`, `-h`.

The dev plate is not included. World objects named `PcRoomPlate` are disabled. Counts:

- `renderers`, `shadowCasters` (`shadowCastingMode` not Off)
- `drawsEst`: material pass count, plus one per shadow caster. `Fidelity/Glass` and `Fidelity/Toon` (unless `_Outline` is 0) count as two passes. Other shaders count as one.
- `transparentDraws`: passes with `renderQueue` >= 2500, or a shader name containing `Glass`
- `tris`: mesh triangles times pass count (a particle renderer uses `particleCount * 2`)
- `textures`: name, width, height (excludes `Texture2D.whiteTexture`)
- `texMemAstc6x6MB`: `ceil(w/6) * ceil(h/6) * 16 * 4/3` bytes, summed, in MiB, from the texture's current size
- `transparentMeanLayers`, `transparentCoverage`: overdraw inside the target's screen rectangle at the framing. Coverage is the fraction of that rectangle with at least one transparent layer. Mean layers is the average over those covered pixels. The counting shader is `Fidelity/Overdraw` (1/32 red per layer). Also writes `<stem>.overdraw.png`.

### Sweep

One Unity launch, one scene open, many variants. `CaptureCli` is a partial class; the entry lives in `Editor/CaptureCli.Sweep.cs`.

```bash
"$U" -batchmode -quit -projectPath apps/terrarium -executeMethod GardenVR.Capture.Editor.CaptureCli.Sweep   -sweep "$(pwd)/tools/fidelity/sweeps/jar-moss.json" -out "$(pwd)/$R/jar-moss" -logFile "$R/jar-moss/sweep.log"
```

`tools/fidelity/fid.py sweep <spec> --out <dir>` wraps this (foreground), reads the log, and checks drift and `git status`. Needs a GPU (no `-nographics`).

Spec (JSON). `scene`, `framing` and `variants` are required; `state`, `target`, `reference`, `w`, `h`, `msaa` default as for `Shot`. `-scene`, `-framing`, `-state`, `-target` on the command line fill in what the spec leaves out.

```json
{ "app": "apps/terrarium", "scene": "Assets/Scenes/Main.unity", "framing": "JarG1",
  "state": "breath=0.5,uncoil=0.3,fog=0.45,time=3", "base": "jar-base", "focusRegion": "moss",
  "variants": [
    { "id": "jar-moss-tint-1.10", "axis": "moss _Tint", "value": "1.10",
      "materials": { "Jar_Moss": { "_Tint": [1.1, 1.1, 1.1, 1] } } },
    { "id": "jar-moss-tex-repaint", "axis": "moss texture", "value": "repaint",
      "textures": { "Jar_Moss": { "_MainTex,_EmissionTex": "Assets/Art/Textures/moss-repaint.png" } } },
    { "id": "dial-variant-watercolour", "state": "variant=watercolour" }
  ] }
```

* The base variant (no overrides, id `base` unless `"base"` names it) is rendered **first and last**; the last one is `<base>-last`. Their difference is the drift inside the launch.
* `state` on a variant is merged over the sweep state, and the whole state is applied again before every variant, so a delta cannot leak into the next one. `ICaptureState` hosts see it exactly as with `Shot`.
* `materials`: key is the material asset name (a trailing ` (Instance)` is ignored). Every renderer slot that uses it gets one runtime copy (`new Material(shared)`) for that variant. A float takes a number, a colour or vector takes 3 or 4 numbers (a colour also takes `"#rrggbb"`). An unknown material or property fails the sweep.
* `textures`: property to file. A key may list several properties, comma separated. The file is read from disk (`Assets/...` is project relative; otherwise the usual `ResolveFile` search) into a runtime `Texture2D` with mips. Wrap mode, filter, aniso and colour space follow the texture being replaced. The file is uncompressed, so add the current texture's own file as a control variant (`jar-moss-tex-reload`) to see the load path's effect.
* Copies are put back and destroyed after each variant. Before any state is applied the sweep snapshots every material under `Assets/` and restores them at the end, because scene hosts such as `JarView` write into shared material assets and Unity would save that on exit. Any other asset still dirty is listed in `sweep.json` (`dirtyAssets`) with a warning.
* Output in `-out`: `<id>.png` + `<id>.png.check.json` per variant (same render path as `Shot`: async compilation off, one discarded render after every change, then the frame) and `sweep.json` (ids, labels, state, sha256, mean luma, per-variant millis, override list, hashes of texture files). `[Capture] Sweep OK <dir>` on success. A variant id is a file name; ids that begin with `jar` or `dial` are picked up by `fid metrics`.
* Base-first is byte-identical to a plain `Shot` of the same framing and state.

## PlayMode

`SequenceRecorder` on a camera writes `f0000.png`, `f0001.png`, ... at `fps` into `outputDirectory`, or into `GARDEN_SEQ_DIR` when that field is empty. It sets `Time.captureDeltaTime` and renders one discarded warm-up from `Start`. Turn off async shader compilation before entering play mode.

`PlaybackHarness` sets `Time.captureDeltaTime` from its fps, exposes `RunUntil(predicate, timeoutSeconds)` as a coroutine, and `WriteState(path, dictionary)` writes a JSON object of strings. Call `Restore()` to put the previous capture delta back.

## Notes for plate alignment

Dial and jar plates are loaded as sRGB, point filtered, and drawn by `Fidelity/Plate` on a quad that matches the vertical FOV. The render target is sRGB. Outside `dial-mask.png`, `plate-dial.png` is the reference photo, so a plate-only `DialG1` shot should diff under 0.03 outside the mask.
