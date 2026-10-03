# Locked looks

A later task may change a locked value only when its task file says so. The jar silhouette is not locked. T-TER-023 replaced the cylinder with a symmetric mason profile. Glass clarity stays at the T-TER-019 values. Cork, jar shape, and fronds stay locked. T-TER-032 replaces the moss and holds the glow between the floor and ceiling below.

## Approving frame

Host review of T-TER-020 (2026-10-03): glass and inner light from the T-TER-019 frame. Clear glass, contents visible, the jar brighter than the desk lamp. Moss and fronds from T-TER-017 and T-TER-019. Cork from T-TER-007 onward.

| Frame | Path | sha256 |
| --- | --- | --- |
| Side-by-side the host named | `orchestration/runs/terrarium/T-TER-019/jar-g1.sbs.png` | `4d55d221f26a799ad5d1e590971f224ec481814d1db4b84ac92ef9c1266b135e` |
| G1 render (T-TER-019 report hash) | `orchestration/runs/terrarium/T-TER-019/jar-g1.png` | `55f73259b952d53c4855f24b97657edd6de8d01a8a540e7a98e4970a787e4c7a` |

State for that frame: `breath=0.5,uncoil=0.3,fog=0.45,time=3`, framing `JarG1`. Source commit `2b286e7`.

## Glow frame (T-TER-030)

T-TER-030 measured the glow on this frame. The calibrated judge scored it 2, 2, 2, median 2, weakest moss, and did not name neon or harsh bloom.

| Frame | Path | sha256 |
| --- | --- | --- |
| G1 render | `orchestration/runs/terrarium/T-TER-030/jar-g1.png` | `6a6e28466bb01ec474d308d0c641fd3229c27d99c118cda923f82c99d0da674d` |
| Side-by-side | `orchestration/runs/terrarium/T-TER-030/jar-g1.sbs.png` | `bb0b7e3c46e72e030779cb55b10d14936e87f4a2d38bdd2ef5575466d2ab8259` |

State for that frame: `breath=0.5,uncoil=0.3,fog=0.45,time=3`, framing `JarG1`.

## Glow floor and ceiling (T-TER-032)

Relative luminance, IEC sRGB decoded to CIE Y, from 0 to 1. `spec_anchor.py` reports the floor and the ceiling. A sample is inside when the printed 4-decimal value, clamped to 1, sits on the closed interval. The reference passes all three. T-TER-030 is under the glass floor and the ring floor. T-TER-028 is over the glass ceiling.

| Measure | Rect (x, y, w, h) | Floor | Ceiling | Reference | T-TER-032 |
| --- | --- | --- | --- | --- | --- |
| Crozier peak | (860, 420, 100, 100) | 0.70 | 1.0 | 0.9878 | 0.9743 |
| Glass mean | (760, 368, 28, 36) | 0.145 | 0.280 | 0.1834 | 0.1804 |
| Ring peak | (700, 850, 420, 140) | 0.60 | 1.0 | 1.0 | 0.8385 |

## Glow frame (T-TER-032)

| Frame | Path | sha256 |
| --- | --- | --- |
| G1 render | `orchestration/runs/terrarium/T-TER-032/jar-g1.png` | `6a4387b56290eca6be06afaa6bd4ad84d37d8802117eabc85830e2b97ddde05a` |
| Side-by-side | `orchestration/runs/terrarium/T-TER-032/jar-g1.sbs.png` | `9bc75370a809c4548bd1559a487e8984cc5ebb3fe0faceffe5f8f899e767d71f` |

State for that frame: `breath=0.5,uncoil=0.3,fog=0.45,time=3`, framing `JarG1`.

## Glass (`Fidelity/JarGlass` and `Fidelity/Glass`)

`JarView.Apply` and `JarSetup` write the same colours. Tint, volume, drops, beads, the fog cap, and the back pass are the T-TER-019 clarity lock. Rim, inner light, and inner height are the T-TER-032 glow. The streak stays at the T-TER-030 value.

| Property | Value |
| --- | --- |
| `_Tint` | (0.75, 0.94, 0.84, 0.018) |
| `_Rim` | (0.48, 0.72, 0.78, 0.32), `_RimPower` 2.40 |
| `_Inner` | (0.34, 0.86, 0.48, 1) |
| `_InnerY` | (0.078, 0.052, 0, 0) full through the moss and the crozier, gone by about 13 cm |
| `_Volume` | (0.30, 0.78, 0.52, 0.10) wall tint, not a cavity fill |
| `_VolumeY` | (0.038, 0.072, 0, 0) |
| `_Drops` | 1.15 |
| `_Streak` | (0.50, 0.66, 0.74, 0.12) |
| Back pass | off (`SRPDefaultUnlit` disabled) |
| Bead window | object y 0.094 to 0.124, in both glass shaders |
| Air light | `_Inner.rgb * column * lerp(0.35, 1.0, cavity) * 0.22` |
| Emission cap | rim, inner, and volume rgb are min'd with linear `#E8FFF4` (0.804, 1.0, 0.903) before they are added |
| Wall opacity | `wall * column * _Volume.a` on `JarGlass`. On shared `FGlass`, `wall * column * 0.10` |
| Fog cap | 0.30 |

## Moss and glow emission

The mound is one low sheet, not a field of round cushions. `MossSkirt` is 980 small tuft cards welded into one mesh of its own. `BudgetMeasure` counts a renderer as a draw, so the cards stay one draw and their triangles stay in the budget. The card material still has GPU instancing on. The mound maps one carpet photo: `_Tri` 0 and `_TopAmount` 0. A repeating tile put a cross of cells through the sheet.

The mound and the moss cards use `Fidelity/MossVelvet`. On the JarG1 plate (28.2 deg) that shader is the Glow equation, and the values below stay. `Fidelity/Moss` is not assigned. `JarView.Apply` still multiplies emission by the breath pulse. At rest the colours are these. The seated rig is a 60 deg lens. That is not part of this table. Both moss materials use `_FarStart` 0.36 and `_FarEnd` 0.46, which are tan of half the vertical field of view. `Jar_Moss` adds `_FarMip` 3.6, `_Velvet` 1, `_TopEmit` 0.32. `Jar_MossCard` adds `_FarMip` 4.5, `_Velvet` 0.85, `_TopEmit` 0.40, and keeps its cutout alpha at mip 0. The seated lens also keeps 0.72 of the emission. Those terms are off at the plate.

| Material | Emission | Tip (y0, y1, floor, peak) above the jar | Other |
| --- | --- | --- | --- |
| Moss | (0.22, 0.62, 0.18) | 0.018, 0.048, 0.55, 1 | `_Tri` 0, `_TopTile` 1, `_TopAmount` 0, `_GradY` (0.016, 0.032), `_GradBottom` 0.58, `_GradTop` 1.02, rim (0.20, 0.48, 0.18) power 3.2 |
| Moss cards | (0.12, 0.42, 0.11) | 0.020, 0.052, 0.28, 1 | rim (0.18, 0.44, 0.16). Card emission samples luma, not the red channel. |
| Fern | (0.16, 0.54, 0.30) | 0.030, 0.110, 0.30, 1 | `_Edge` 0.16, `_Trans` 0.75, rim (0.32, 0.50, 0.42) |
| Newest frond | fern emission times life | same tip window | `_Edge` 0.22, `_Trans` 0.75 |
| Fiddle | (0.46, 0.95, 0.55) | 0.034, 0.096, 0.10, 1 | `_Edge` 0.10, rim (0.48, 0.70, 0.52) |
| Dew | (0.70, 0.98, 0.84) | | tint (0.78, 1, 0.92) |
| Crozier light | mint (0.32, 0.66, 0.40), radius 0.08 | | soil `_LightPos` stays zero. Day 7 uses warm (0.55, 0.32, 0.12) |
| Coil halo | (0.42, 0.78, 0.50) | | `JarView` scales it with the breath |
| Jar halo | (0.16, 0.40, 0.24) | | |
| Desk spill | (0.20, 0.48, 0.28) | | |
| Ring | mint (0.46, 0.82, 0.54) | | answer (0.50, 0.88, 0.58), pool (0.12, 0.32, 0.18), gold (0.92, 0.70, 0.32). `ring.png` peaks at r=0.92 |

`Fidelity/Glow` and `Fidelity/MossVelvet` run green-dominant pixels that are both hot and saturated toward linear `#E8FFF4`, then shoulder anything still over that cap. Warm pixels and anything under the cap stay as authored. The defaults that keep older materials flat stay locked: `_Tip` (0, 1, 1, 1), `_Edge` 0. Do not retune those defaults to chase a frame.

`ring.png` is painted in place. Its ridge is wider than the old razor (`fwhmFractionOfHalfExtent` 0.035, peak still 0.9175). Running `tools/blender/paint_textures.py` restores the razor. Do not run that script to refresh this texture.
