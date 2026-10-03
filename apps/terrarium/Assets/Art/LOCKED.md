# Locked looks

A later task may change a locked value only when its task file says so. The jar silhouette is not locked. T-TER-023 replaced the cylinder with a symmetric mason profile. Glass clarity stays at the T-TER-019 values. T-TER-030 replaced the glow, the inner light, and the moss emission. Those stay at the values below.

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

## Glass (`Fidelity/JarGlass` and `Fidelity/Glass`)

`JarView.Apply` and `JarSetup` write the same colours. Tint, volume, drops, beads, the fog cap, and the back pass are the T-TER-019 clarity lock. Rim, inner light, inner height, and the streak are the T-TER-030 glow.

| Property | Value |
| --- | --- |
| `_Tint` | (0.75, 0.94, 0.84, 0.018) |
| `_Rim` | (0.38, 0.58, 0.66, 0.26), `_RimPower` 2.40 |
| `_Inner` | (0.18, 0.40, 0.26, 1) |
| `_InnerY` | (0.030, 0.040, 0, 0) full beside the moss, gone by about 7 cm |
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

The mound and the moss cards use `Fidelity/MossVelvet`. On the JarG1 plate (28.2 deg) that shader is the Glow equation, and the values below stay. `Fidelity/Moss` is not assigned. `JarView.Apply` still multiplies emission by the breath pulse. At rest the colours are these. The seated rig is a 60 deg lens. That is not part of this table. Both moss materials use `_FarStart` 0.36 and `_FarEnd` 0.46, which are tan of half the vertical field of view. `Jar_Moss` adds `_FarMip` 3.6, `_Velvet` 1, `_TopEmit` 0.32. `Jar_MossCard` adds `_FarMip` 4.5, `_Velvet` 0.85, `_TopEmit` 0.40, and keeps its cutout alpha at mip 0. The seated lens also keeps 0.72 of the emission. Those terms are off at the plate.

| Material | Emission | Tip (y0, y1, floor, peak) above the jar | Other |
| --- | --- | --- | --- |
| Moss | (0.13, 0.40, 0.11) | 0.018, 0.048, 0.18, 1 | `_Tri` 52, `_TopTile` 52, `_TopAmount` 0.92, `_GradY` (0.016, 0.032), `_GradBottom` 0.58, `_GradTop` 1.02, rim (0.18, 0.42, 0.16) power 3.2 |
| Moss cards | (0.10, 0.30, 0.10) | 0.020, 0.052, 0.22, 1 | rim (0.16, 0.36, 0.16) |
| Fern | (0.16, 0.54, 0.30) | 0.030, 0.110, 0.30, 1 | `_Edge` 0.16, `_Trans` 0.75, rim (0.32, 0.50, 0.42) |
| Newest frond | fern emission times life | same tip window | `_Edge` 0.22, `_Trans` 0.75 |
| Fiddle | (0.46, 0.95, 0.55) | 0.034, 0.096, 0.10, 1 | `_Edge` 0.10, rim (0.48, 0.70, 0.52) |
| Dew | (0.70, 0.98, 0.84) | | tint (0.78, 1, 0.92) |
| Crozier light | mint (0.24, 0.52, 0.32), radius 0.08 | | soil `_LightPos` stays zero. Day 7 uses warm (0.55, 0.32, 0.12) |
| Coil halo | (0.42, 0.78, 0.50) | | `JarView` scales it with the breath |
| Jar halo | (0.10, 0.26, 0.16) | | |
| Desk spill | (0.12, 0.30, 0.18) | | |
| Ring | mint (0.16, 0.28, 0.20) | | answer (0.20, 0.34, 0.24), gold (0.92, 0.70, 0.32). `ring.png` peaks at r=0.92 |

`Fidelity/Glow` and `Fidelity/MossVelvet` run green-dominant pixels that are both hot and saturated toward linear `#E8FFF4`, then shoulder anything still over that cap. Warm pixels and anything under the cap stay as authored. The defaults that keep older materials flat stay locked: `_Tip` (0, 1, 1, 1), `_Edge` 0. Do not retune those defaults to chase a frame.

`ring.png` is painted in place. Its ridge is wider than the old razor (`fwhmFractionOfHalfExtent` 0.035, peak still 0.9175). Running `tools/blender/paint_textures.py` restores the razor. Do not run that script to refresh this texture.
