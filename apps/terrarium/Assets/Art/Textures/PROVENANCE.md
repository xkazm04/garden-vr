# Jar textures

Origins for the maps `JarView` binds. A generated plate is an `image_gen` or `image_edit` file under `Assets/Art/Source/` with a `.prompt.txt` sidecar. Code in `apps/terrarium/Art/Scripts/` only composes, keys, or grades those plates. Nothing bound here is sampled from the A1-03 reference frames.

The T-TER-013 generated maps stay in this folder. `JarView` binds the names in the status column.

| File | Origin | Source | Status |
|---|---|---|---|
| condensation.png | generated | T-TER-007 macro beads `condensation_beads.png`. `compose_jar_detail.py` packs R as the droplet mask (denser in the lower half of the jar), B as haze, and G as a highlight. G is a normal estimate: Sobel of luminance height, not a measured normal. | bound on the glass |
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
| moss_card.png | generated | T-TER-017 4x4 tuft atlas. Six Nano Banana clumps `moss_nb_tuft_0` through `moss_nb_tuft_5` in `Art/Source/`, keyed and graded by `compose_nb.py`. Flips fill the spare cells. | bound on MossSkirt |
| moss_fuzz.png | retired | Earlier stand-in tuft tile. Not bound. | removed from the jar |
| moss_macro.png | generated | T-TER-017 Nano Banana cushion tile `moss_nb_tile.png`. `compose_nb.py` makes it seamless and grades the mean toward measured moss `#376222` while keeping the dark gaps. Triplanar on the low mound, and per-clump UVs. | bound on the moss mound |
| moss_tile.png | retired | Earlier stand-in moss tile. Not bound. | removed from the jar |
| moss_top.png | painted-by-code | `paint_textures.py` fbm moss cap, via `shared/assets/seed-textures/moss_top.png` | not bound. The mound uses `moss_macro`. |
| moss_tuft.png | generated | T-TER-013 `image_gen` + `terrarium_textures.py` | kept, not bound by JarView |
| petal.png | generated | T-TER-007 plate `petal_macro.png`. `compose_jar_detail.py` keeps the amber petal and fills the background so the glow cutoff can drop it. | bound on the first flower |
| ring.png | painted-by-code | `paint_textures.py` analytic ring and pool, via `shared/assets/seed-textures/ring.png` | bound on the breath ring |
| soil.png | generated | T-TER-013 `image_gen` + `terrarium_textures.py` | kept, not bound by JarView |
| soil_band.png | generated | T-TER-017 Nano Banana potting soil `soil_nb.png`. `compose_nb.py` keeps the crumbs and perlite, pulls the mean down to a dark loam, and makes the tile seamless. Triplanar, with a gradient that darkens into the glass base. | bound on the soil |
| spore.png | painted-by-code | `paint_textures.py` radial sprite, via `shared/assets/seed-textures/spore.png` | bound on the spore particles |
| fiddle_hairs.png | generated | T-TER-017 Nano Banana fiddlehead `fiddle_nb.png`. `compose_nb.py` turns the coloured stem into a repeating hair albedo for the crozier tube. | bound on the fiddlehead |
