# Locked looks

A later task may change a locked value only when its task file says so. The pinch halo stroke and the dial-face rim ink are the T-SUN-031 lock. Wash grades, plant cards, and the slim nib stay at the frames named below. The live tile mesh is 63 slabs. This task did not edit it. The outer ring is still the T-SUN-028 slab.

## Halo (T-SUN-031, look from T-SUN-017)

Host review of T-SUN-028 (2026-10-03): the gold pinch halo was gone on the brighter plants. T-SUN-017 had a thin glowing outline. The stroke below is that outline, retuned so it reads on the current cards. The core gold is the T-SUN-017 colour. The falloff is stronger, the card is no longer scaled past the plant, and the close is short so the flowers stay separate leaves.

| Frame | Path | sha256 |
| --- | --- | --- |
| Pinch render | `orchestration/runs/sundial/T-SUN-031/pinch-halo.png` | `09298a2379b2c53ab8c58c01f194a57057b9eae51e5a7cb86527d8b16d42765f` |
| Side-by-side | `orchestration/runs/sundial/T-SUN-031/pinch-halo.sbs.png` | `37729f96b44ecef4fff605f525ec3608b4a75651ccaf8d5f3395d8c8b28f412a` |
| T-SUN-017 source | `orchestration/runs/sundial/T-SUN-017/pinch-halo.sbs.png` | `220eab1459465f2ce023f4c5fd5753e9debbea3287be5eca8165ce15e1d83446` |

State: `halo=1,haloTarget=midday,gnomonDeg=105,time=1`, framing `DialG1`, 1824x1024, hand matte on the pinch plate. `DialView.Apply` writes the colours. At rest, `amount` is 1.

| Property | Value |
| --- | --- |
| Core `_Color` | (1.15, 0.86, 0.32) |
| Falloff `_Color2` | (0.58, 0.38, 0.12) |
| `_Silhouette` | 2.15 |
| `_Fit` | `DialView.HaloFit` 1 |
| `HaloClosePx` | 5 |
| `HaloCorePx` | 3 |
| `HaloGlowPx` | 8 |
| `HaloPush` | 0.0035 m along the face normal |
| `BloomCardLift` | 0.0015 m, shifted up in the mask by `round(lift / cardHeight * texHeight)` px |
| Queue | 3012, additive, `_ZWrite` 0 |
| Mask | R is the core, G is the falloff. Chamfer step 3, diagonal 4 |

Glow on `dial-g1.png` against `dial-g1-nohalo.png`: peak excess 104.55, tenth falloff 5 px. The gate needs a peak of at least 4 and a tenth falloff of at least 2 px.

## Rim ink (T-SUN-031)

The painted ring cannot hold 3 px after the 1024 import. `Fidelity/Toon` draws it when `_InkRingR` is above 0.001. Only `Dial_Face` sets that, to 0.12685 m (`882/1023.5 * 0.1472`). Rim, soil, gnomon, tiles, and every other Toon user stay at 0, so the shared shader change stays off for them. The width is a finite 0.001 m step through `TransformWorldToHClip`, then `smoothstep(1.60, 2.15, dpx)`. `grade_a2.py` `clear_rim_specks` removes the old wobbling stroke (luma under 90 and chroma under 40, radius 0.1248 to 0.1292) and leaves the dusk wash.

| Frame | Path | sha256 |
| --- | --- | --- |
| G1, halo off | `orchestration/runs/sundial/T-SUN-031/dial-g1-nohalo.png` | `7fecea657902ce51b9fe3cf72a684547cc4c3f670c6220fa213ca597dc286375` |
| Side-by-side | `orchestration/runs/sundial/T-SUN-031/dial-g1-nohalo.sbs.png` | `9160530eaf5ad78614ada52414765802f4b0c82cf9296f4bf853c11f8a2b387d` |

State: `halo=0,waiting=0,gnomonDeg=105,time=1`, framing `DialG1`.

| Property | Value |
| --- | --- |
| `_InkRingR` | 0.12685 on `Dial_Face` only |
| `_Ink` | (0.165, 0.149, 0.133) |
| Half-height width | median 3.0 px, p10 2.75, p90 3.75, n 77. The gate is the median, band 2.5 to 3.5 |
| Ink mean | RGB about (44.3, 36.4, 26.6), delta-E of the mean 3.782 vs `#2A2622` |
| Hull fringe | 0 samples. `Dial_Rim` stays `_OutlinePx` 1.6, `_BoilPx` 1.0 |

## Dial face and washes (T-SUN-028)

Host review of T-SUN-028 accepted the dusk wash (delta-E 2.4), the tile thickness, and the fresher plants. T-SUN-031 remeasured the same three washes on the new nohalo frame. The wash masks against the T-SUN-028 nohalo frame are byte-identical.

| Frame | Path | sha256 |
| --- | --- | --- |
| Grade frame | `orchestration/runs/sundial/T-SUN-028/dial-g1-nohalo.png` | `5c1c728eea17042fc0c6b6bb5ea2e4019e6d4d9b9777d54f2dba0c5517274e31` |
| Remeasure | `orchestration/runs/sundial/T-SUN-031/dial-g1-nohalo.png` | `7fecea657902ce51b9fe3cf72a684547cc4c3f670c6220fa213ca597dc286375` |

| Property | Value |
| --- | --- |
| `Dial_Face` `_Lit` | (1.03, 1, 0.97) |
| `Dial_Face` `_Shade` | (0.97, 0.94, 0.90) |
| `Dial_Face` `_Grain` | 0.045 |
| `Dial_Face` `_Step` | -0.45 |
| Morning wash mean delta-E | 5.737 vs ref-1. Cap is 8 |
| Midday wash mean delta-E | 7.884 |
| Dusk wash mean delta-E | 2.396 |
| `Dial_Tile` `_WashMorning` | (0.965, 0.871, 0.718) |
| `Dial_Tile` `_WashMidday` | (0.957, 0.714, 0.631) |
| `Dial_Tile` `_WashDusk` | (0.731, 0.668, 0.898) |
| Import cap | `dial_face.png` max 1024. Do not raise it |

## Tiles

`TilesLive.asset` is the nine-plant mesh (`badf0b4`), 1260 verts, 63 slabs of 20. This task did not edit the mesh. Every slab is raised 2.0 mm and is 1.8 mm thick. Seven days sit on each of nine plants.

| Ring | Count | Footprint |
| --- | --- | --- |
| Outer | 21 | 12 x 9 mm, the T-SUN-028 slab |
| Mid | 21 | 9.2 x 6.8 mm |
| Inner | 21 | 7.2 x 5.6 mm |

## Plant cards (T-SUN-028)

The card textures were not edited. Sizes are `DialView.PlantSize`. Contours on the nohalo frame: morning 1.776 px, midday 1.981 px, dusk 1.654 px. No plant has one 0.5 px bin over 60% of the stroke.

| Arc | Card | Metres |
| --- | --- | --- |
| Morning | `plant_sunrise_full.png` | 0.064 x 0.096 |
| Midday | `plant_midday_full.png` | 0.072 x 0.112 |
| Dusk | `plant_dusk_full.png` | 0.0593 x 0.0935 |

## Slim nib (T-SUN-016)

`Dial_Gnomon` was not edited. The approving frame is the T-SUN-016 dial. On the T-SUN-031 nohalo frame the pencil ticks stay median 0.75 px, and the pixels outside the new ink ring match T-SUN-028.

| Frame | Path | sha256 |
| --- | --- | --- |
| G1 | `orchestration/runs/sundial/T-SUN-016/dial-g1.png` | `c82ea5054d6b5f983e7f8f83e49ea5a4374903ac1a9cbf47dcffb9615ccaf1b5` |

| Property | Value |
| --- | --- |
| `_OutlinePx` | 1.15 |
| `_Lit` | (1.02, 1.03, 1.06) |
| `_Shade` | (0.78, 0.80, 0.84) |
| `_Spec` | (0.55, 0.57, 0.60), `_SpecStep` 0.84 |
| Length | 0.11 m |

## Ink lettering and tags (T-SUN-020)

`InkLetter` was not edited. Patrick Hand (`Resources/FirstRunHand`), cream notes, white tint so the ink stays ink. Yesterday's ask is a pale square, an ink outline, and a soft gold glow, with no glyph.

| Frame | Path | sha256 |
| --- | --- | --- |
| Settings | `orchestration/runs/sundial/T-SUN-020/settings.png` | `a7b28ba5448fc2d4fafe61acf043a12d15d1995851b56cf2060c9070f4a587b0` |
| First run | `orchestration/runs/sundial/T-SUN-020/firstrun.png` | `7d7624a15ca4515d77c190dc1469926508e46efb1f8ae0867af5366448669e5a` |
| Day 7 | `orchestration/runs/sundial/T-SUN-020/day7.png` | `55c01f6624065f823b737ef91be883db866bfe9546bfd6dccdaf2af09834e44b` |
| Missed day | `orchestration/runs/sundial/T-SUN-020/missed.png` | `a14e2d957034070d9e84faba48d83d92b733a478b926f8b6e0007503a630326b` |

| Property | Value |
| --- | --- |
| `Ink` | (0.165, 0.149, 0.133) |
| `Paper` | (0.953, 0.933, 0.886) |
| Ask `Glow` | (0.961, 0.780, 0.416) |
| Ask fill | (0.937, 0.910, 0.855) |
| Note stroke | `clamp(min(width, height) * 0.018, 1.7, 2.8)` px |
| Card | `Fidelity/Card`, tint white, render queue 3100 on the TextMesh |

## Layout (T-SUN-052, variant=layout)

A variant, not a replacement: look A and every number above are unchanged and stay the default. `variant=layout` (alone, or joined as `layout+soilmound+leafplant`) is what the host compared against `T-SUN-047/pinch-b.sbs.png`. Adopting it is an owner call; if adopted, the table below is the lock. Measured at DialG1 (1824x1024) against `A2-05-field-notebook-1.png`. The habit record (63 tiles, their ids and angles, the three arcs, the nine plant slots) is untouched.

| Proportion | Look A (old) | Layout (new) | Reference |
| --- | --- | --- | --- |
| Dial scale about its centre (`DialLayout.Scale`) | 1.0 | 0.8843 | - |
| Dial silhouette major axis, px | 1148.9 | 1011.2 | 1008.3 |
| Major axis over hand scale (sqrt of the hand matte area, 577 px) | 1.991 | 1.753 | 1.748 |
| Silhouette IoU against the reference | 0.759 | 0.970 | - |
| Wash disc ends at, of the face radius | 0.731 | 0.82 (`InnerRing`) | 0.79 to 0.83 |
| Rim band width, of the face radius | 0.269 (washes to edge) | 0.18 | 0.17 to 0.25 (left to right) |
| Shader ink ring `_InkRingR` | 0.12685 m (0.862) | 0.12100 m (0.822), `Dial_Face_Layout.mat` | - |
| Tile row radii (rows 0, 1, 2) | 0.82, 0.70, 0.58 | 0.885, 0.775, 0.70 | - |
| Plant spots (x, z in face radii) | (-0.30,-0.08) (0.16,0.18) (0.26,-0.16) | (-0.46,-0.20) (0.26,-0.02) (0.46,-0.36) | - |
| Soil | mound circle 0.50 at the centre | bed ellipse 0.70 x 0.41, centre z -0.39 (`BedHalfX`, `BedHalfZ`, `BedCentreZ`) | 0.75 x 0.48 |
| Soil area over dial silhouette | 0.185 (disc), 0.223 (S4 mound) | 0.328 | 0.318 |
| Soil bed IoU against the reference bed | 0.214 (disc), 0.254 (mound) | 0.806 | - |
| Halo close radius, card texels | 5 (`HaloClosePx`) | 14 (`DialLayout.HaloClosePx`) | - |

The face is `Resources/Layout/dial_face_layout.png`: `layout_t052_face.py` remaps `dial_face.png` radially (no new paint, wash colours untouched), mirrors the wash into the blank paper core, keeps half the contrast of the ticks and adds a warm tone and a seeded pencil hatch to the band. The halo stroke (3 px core, 8 px glow, gold, `_Silhouette` 2.15, queue 3012) is the T-SUN-031 lock, unchanged; only the close radius and, for a drawn-leaf plant, a canvas wider than the card with a soil patch under the base change. The midday plant stands outside its arc by 26 degrees (at -4 degrees, the arc starts at 22) because the bed's far edge is at z +0.02; its tiles and wash are unchanged. `Dial_Face`, `Dial_Rim`, the gnomon and every frame hashed above are not edited.

## Sprint composite (T-SUN-050, variant=sprint)

A variant, not a replacement: look A and every number above are unchanged and stay the default. `variant=sprint` stands for `layout+watercolour+roomlight+soilmound+sprintleaf+halo2` (`DialView.SprintParts`); naming more tokens adds them (`sprint+leafplant` also draws the morning plant as an assembly). Adopting it is an owner call; nothing here is a lock until then.

| Part | Spike | In the stack | Why |
| --- | --- | --- | --- |
| Face, washes, paper | S1 T-SUN-041 B | yes, on the layout face (`Resources/Layout/Dial_Face_Layout_S1.mat`, `layout_t052_face.py --s1`) | judge 6/6, wash and paper hp inside the dossier bands |
| Room light | S2 T-SUN-044 B | yes, including the `Fidelity/Leaf` path and the leaf plants' wash wedges | judge 6/6 closer |
| Drawn leaves | S3b T-SUN-049 B | midday and evening only | the morning assembly lost to its card (2 of 6), so the morning plant stays a card |
| Soil | S4 T-SUN-046 B | yes (the layout stretches it to the bed) | judge 6/6, soil gate met |
| Halo | S5 T-SUN-047 B | for the card plant only | it has no mask for a drawn-leaf plant; those two keep the layout's closed stroke (`HaloClosePx` 14, canvas and soil patch) |
| Proportions | T-SUN-052 | yes | IoU 0.759 to 0.970 |

Seams fixed in this pass: the S1 face and control map are remapped with the layout's radial knots (the washes keep their ids and grain on the wider disc); `Fidelity/Leaf` takes the room-light cookie once per vertex at the plant's base point (0 fragment samples); a hero plant drawn as an assembly keeps its wash wedge; the cookie is read through the dial's look A transform, so the layout scale (0.8843) does not stretch the room's light by 1 / 0.8843 against the plate. Frames: `orchestration/runs/sundial/T-SUN-050/nohalo-s.png` (sha256 `1e1d4ecd49efe35bbbf093dcfad17b17e5e318c677cbcd7cd90322e9ef81745d`) and `pinch-s.png`, state `halo=0,waiting=0,gnomonDeg=105,time=1` and `halo=1,haloTarget=midday,gnomonDeg=105,time=1`, framing `DialG1`.
