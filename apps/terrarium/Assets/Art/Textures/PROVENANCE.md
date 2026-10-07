# Jar textures

Origins for the maps `JarView` binds. A generated plate is an `image_gen` or `image_edit` file under `Assets/Art/Source/` with a `.prompt.txt` sidecar. Code in `apps/terrarium/Art/Scripts/` only composes, keys, or grades those plates. Nothing bound here is sampled from the A1-03 reference frames.

The T-TER-013 generated maps stay in this folder. `JarView` binds the names in the status column.

| File | Origin | Source | Status |
|---|---|---|---|
| condensation.png | generated | T-TER-007 macro beads `condensation_beads.png`. `compose_jar_detail.py` packs R as the droplet mask (denser in the lower half of the jar), B as haze, and G as a highlight. G is a normal estimate: Sobel of luminance height, not a measured normal. | bound on the glass, haze only |
| droplet_normal.png | painted-by-code | T-TER-033 `droplet_normal.py`. Sphere-cap beads. RGB is a tangent normal, A is a hard cut. Not sampled from the reference frames. | bound on the glass as `_Bead` |
| s1_drops.png | painted-by-code | T-TER-040 `s1_structured_glass.py`. Sphere caps, larger toward the top. RG is a tangent normal, B is height (the fog wipe), A is the bead mask. Not sampled from the reference frames. | bound on the glass as `_DropN`. Sampled only by variant `s1` |
| s1_studio.png | painted-by-code | T-TER-040 `s1_structured_glass.py`. Six 128 px faces in one strip (+X -X +Y -Y +Z -Z): two cool vertical softboxes, a warm upper blob, a mint foot. Not sampled from the reference frames. | bound on the glass as `_Studio`. Sampled only by variant `s1` |
| cork.png | generated | T-TER-013 `image_gen` + `terrarium_textures.py` | kept, not bound by JarView |
| cork_side.png | generated | T-TER-007 macro `cork_macro.png`. `compose_jar_detail.py` makes it tileable and grades the mean to a light warm tan. Wrap-around edge diff is in `orchestration/runs/terrarium/T-TER-007/edge-diff.txt`. | bound on the cork side |
| cork_top.png | generated | T-TER-007 top-down edit `cork_top_macro.png`. Same tile and grade as the side. | bound as the cork cap |
| fern_a_albedo.png | generated | T-TER-013 `image_gen` + `terrarium_textures.py` | kept, not bound by JarView |
| fern_a_emission.png | generated | T-TER-013 `image_gen` + `terrarium_textures.py` | kept, not bound by JarView |
| fern_albedo.png | generated | T-TER-017 Nano Banana atlas, 1024 square, two columns. `fern_nb_a.png` and `fern_nb_b.png` in `Art/Source/`, keyed by `compose_nb.py`. FrondV2 reuses the first painting with a different curl. | bound on the fronds |
| fern_b_albedo.png | generated | T-TER-013 `image_gen` + `terrarium_textures.py` | kept, not bound by JarView |
| fern_b_emission.png | generated | T-TER-013 `image_gen` + `terrarium_textures.py` | kept, not bound by JarView |
| fern_emission.png | generated | T-TER-017 vein mask of the fern atlas (`compose_nb.py`). Bright between the veins, darker on them, so the backlight is a leaf and not a flat card. | bound on the fronds |
| halo.png | painted-by-code | `paint_textures.py` radial falloff, via `shared/assets/seed-textures/halo.png` | bound on the spill and halos |
| mist.png | generated | T-TER-007 eight wisp plates `mist_wisp_0` through `mist_wisp_7`. `compose_mist.py` builds one 8x8 flipbook. T-TER-015 reshapes each cell into one soft plume, wide at the cork and thin as it rises. One Card material steps the UV. | bound on the single mist card |
| moss_band.png | retired | Earlier stand-in of the moss cap. Not bound. The mound uses `moss_macro`. | removed from the jar |
| moss_card.png | generated | T-TER-032 8x8 tuft atlas. Six Nano Banana sprigs `moss_sprig_0` through `moss_sprig_5` in `Art/Source/`, keyed and ragged by `compose_moss.py`. Flips fill the spare cells. The silhouette is the sprig, not a round plate. | bound on MossSkirt |
| moss_fuzz.png | retired | Earlier stand-in tuft tile. Not bound. | removed from the jar |
| moss_macro.png | generated | T-TER-032 Nano Banana carpet `moss_carpet_nb.png`. `compose_moss.py` grades it and does not run a seamless blend, because that blend painted a cross. The mound uses one photo in mesh UV (`_Tri` 0, `_TopAmount` 0), not a tiled cell pattern. | bound on the moss mound |
| moss_macro_b.png | generated | Copy of T-TER-017 Nano Banana `Art/Source/moss_albedo.png` (the uniform cushion plate). Not regenerated. The T-TER-032 mound does not sample it. | not sampled |
| moss_tile.png | retired | Earlier stand-in moss tile. Not bound. | removed from the jar |
| moss_top.png | painted-by-code | `paint_textures.py` fbm moss cap, via `shared/assets/seed-textures/moss_top.png` | not bound. The mound uses `moss_macro`. |
| moss_tuft.png | generated | T-TER-013 `image_gen` + `terrarium_textures.py` | kept, not bound by JarView |
| petal.png | generated | T-TER-007 plate `petal_macro.png`. `compose_jar_detail.py` keeps the amber petal and fills the background so the glow cutoff can drop it. | bound on the first flower |
| ring.png | painted-by-code | `paint_textures.py` analytic ring and pool, via `shared/assets/seed-textures/ring.png` | bound on the breath ring |
| soil.png | generated | T-TER-013 `image_gen` + `terrarium_textures.py` | kept, not bound by JarView |
| soil_band.png | generated | T-TER-017 Nano Banana potting soil `soil_nb.png`, then `refine_soil.py` (T-TER-018) crushes the pale perlite and shifts the mean toward measured loam `#0D231D`. The tile stays seamless. Triplanar and fine, on a thin bed flush with the glass. | bound on the soil |
| spore.png | painted-by-code | `paint_textures.py` radial sprite, via `shared/assets/seed-textures/spore.png` | bound on the spore particles |
| fiddle_hairs.png | generated | T-TER-017 Nano Banana fiddlehead `fiddle_nb.png`. `compose_nb.py` turns the coloured stem into a repeating hair albedo for the crozier tube. | bound on the fiddlehead |
| s3_strata.png | generated | T-TER-042 `s3_moss_textures.py`. A 1024x256 crop of the generated `soil_band.png` crumb, regraded per layer (substrate with green specks, pale grit with stones, dark humus, root zone, a thin green line). Not sampled from the reference frames. | bound on `SoilS3`. Sampled only by variant `s3` |
| s3_strand.png | painted-by-code | T-TER-042 `s3_moss_textures.py`. Linear, tiling. R is strand height (a cone per strand, strands in Voronoi clumps), G is clump shade, B is strand tint. Not sampled from the reference frames. | bound on the shell moss as `_StrandTex`. Sampled only by variant `s3` |
| s4_frond_albedo.png | derived | T-TER-043 `s4_frond_textures.py`. The bound `fern_albedo.png` atlas (same alpha, so the frond silhouette is unchanged) regraded from the saturated yellow-green photo to a pale mint ramp (shadow, `#9FD7A8` mid, light), lighter at the pinna margins, with the rachis kept dark. No pixels from the reference frames. | bound on the fronds only while variant `s4` or `s4f` is on |
| s4_frond_thick.png | derived | T-TER-043 `s4_frond_textures.py`. Linear. R is thickness: thin at the pinna margins (distance to the silhouette), thick on the rachis and veins. Drives `(1 - thickness)` in the `Fidelity/FrondBacklit` transmission term. | sampled by variant `s4` and `s4f` |

| s2_refract_strip.png | baked | T-TER-044 `S2Bake.Bake` (Editor, `s2_shots.py bake`). Six 160 px faces rendered from the cavity centre (jar origin + 6 cm) with the glass hidden, the JarG1 plate in place and the JarG1 state, laid out +X, -X, +Y, -Y, +Z, -Z for `SampleRefractStrip` in `GlassCommon.hlsl`. It is a render of the project scene, not a photo and not sampled from the reference frames. | bound on `Jar_Glass` as `_RefractStrip`. Sampled only by variant `s2b1` |

mist.png: box-downscaled 2048 to 1024 to the import cap on 2026-10-07 (exact 2x2 box average, engine-equivalent, not pixel-identical); a rerun of apps/terrarium/Art/Scripts/compose_mist.py (or tools/blender/paint_textures.py) re-inflates it to 2048.
