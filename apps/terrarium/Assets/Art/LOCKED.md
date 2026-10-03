# Locked looks

A later task may change a locked value only when its task file says so. The jar silhouette is not locked. T-TER-023 replaced the cylinder with a symmetric mason profile. Glass, inner light, and moss emission stay at the values below.

## Approving frame

Host review of T-TER-020 (2026-10-03): glass and inner light from the T-TER-019 frame. Clear glass, contents visible, the jar brighter than the desk lamp. Moss and fronds from T-TER-017 and T-TER-019. Cork from T-TER-007 onward.

| Frame | Path | sha256 |
| --- | --- | --- |
| Side-by-side the host named | `orchestration/runs/terrarium/T-TER-019/jar-g1.sbs.png` | `4d55d221f26a799ad5d1e590971f224ec481814d1db4b84ac92ef9c1266b135e` |
| G1 render (T-TER-019 report hash) | `orchestration/runs/terrarium/T-TER-019/jar-g1.png` | `55f73259b952d53c4855f24b97657edd6de8d01a8a540e7a98e4970a787e4c7a` |

State for that frame: `breath=0.5,uncoil=0.3,fog=0.45,time=3`, framing `JarG1`. Source commit `2b286e7`.

## Glass (`Fidelity/JarGlass` and `Fidelity/Glass`)

`JarView.Apply` and `JarSetup` write the same colours. Shaders are the T-TER-019 text.

| Property | Value |
| --- | --- |
| `_Tint` | (0.75, 0.94, 0.84, 0.018) |
| `_Rim` | (0.90, 1.12, 1.00, 0.90), `_RimPower` 2.35 |
| `_Inner` | (0.40, 1.15, 0.68, 1) |
| `_InnerY` | (0.038, 0.072, 0, 0) full beside the moss, gone by the shoulder |
| `_Volume` | (0.30, 0.78, 0.52, 0.10) wall tint, not a cavity fill |
| `_VolumeY` | (0.038, 0.072, 0, 0) |
| `_Drops` | 1.15 |
| `_Streak` | (0.90, 1, 0.96, 0.46) |
| Back pass | off (`SRPDefaultUnlit` disabled) |
| Bead window | object y 0.094 to 0.124, in both glass shaders |
| Air light | `_Inner.rgb * column * lerp(0.35, 1.0, cavity) * 0.22` |
| Wall opacity | `wall * column * _Volume.a` on `JarGlass`. On shared `FGlass`, `wall * column * 0.10` |
| Fog cap | 0.30 |

## Moss and glow emission

The mound and the moss cards use `Fidelity/MossVelvet`. On the JarG1 plate (28.2 deg) that shader is the Glow equation, and the values below stay. `Fidelity/Moss` is not assigned. `JarView.Apply` still multiplies emission by the breath pulse. At rest the colours are these. The seated rig is a 60 deg lens. That is not part of this table. Both moss materials use `_FarStart` 0.36 and `_FarEnd` 0.46, which are tan of half the vertical field of view. `Jar_Moss` adds `_FarMip` 3.6, `_Velvet` 1, `_TopEmit` 0.32. `Jar_MossCard` adds `_FarMip` 4.5, `_Velvet` 0.85, `_TopEmit` 0.40, and keeps its cutout alpha at mip 0. The seated lens also keeps 0.72 of the emission. Those terms are off at the plate.

| Material | Emission | Tip (y0, y1, floor, peak) above the jar | Other |
| --- | --- | --- | --- |
| Moss | (0.16, 0.48, 0.26) | 0.018, 0.048, 0.18, 1 | `_Tri` 52, `_TopTile` 52, `_TopAmount` 0.92, `_GradY` (0.016, 0.032), `_GradBottom` 0.58, `_GradTop` 1.02, rim (0.42, 0.90, 0.55) power 3.2 |
| Moss cards | (0.12, 0.36, 0.20) | 0.020, 0.052, 0.22, 1 | rim (0.40, 0.88, 0.52) |
| Fern | (0.22, 0.62, 0.36) | 0.030, 0.110, 0.38, 1 | `_Edge` 0.32 |
| Newest frond | fern emission times life | same tip window | `_Edge` 0.42 |
| Fiddle | (0.55, 1.12, 0.40) | 0.034, 0.096, 0.16, 1 | `_Edge` 0.18 |
| Crozier light | mint (0.55, 1.20, 0.70), radius 0.12 | | soil `_LightPos` stays zero |

`Fidelity/Glow` defaults that keep older materials flat: `_Tip` (0, 1, 1, 1), `_Edge` 0. Those defaults are part of the lock. Do not retune them to chase a frame.
