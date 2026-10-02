# Jar textures

Round-3 night-jar maps copied from `shared/assets/seed-textures/`. Origins follow `tools/blender/paint_textures.py` and the round-3 report (gate A4): a crop from the owner's frame is a stand-in, not authored art.

The T-TER-013 generated maps stay in this folder. `JarView` binds the round-3 names, not those.

| File | Origin | Source | Status |
|---|---|---|---|
| condensation.png | painted-by-code | `paint_textures.py` droplet field, via `shared/assets/seed-textures/condensation.png`. Replaces the T-TER-013 generated file of the same name. | bound on the glass |
| cork.png | generated | T-TER-013 `image_gen` + `terrarium_textures.py` | kept, not bound by JarView |
| cork_side.png | crop-from-reference | `paint_textures.py` crop of the cork band in the A1-03 night frame | stand-in, art debt (gate A4). Bound on the cork side |
| cork_top.png | painted-by-code | `paint_textures.py` fbm cork top, via `shared/assets/seed-textures/cork_top.png` | bound as the cork cap |
| fern_a_albedo.png | generated | T-TER-013 `image_gen` + `terrarium_textures.py` | kept, not bound by JarView |
| fern_a_emission.png | generated | T-TER-013 `image_gen` + `terrarium_textures.py` | kept, not bound by JarView |
| fern_albedo.png | generated | T-TER-006 `image_gen` atlas of `fern_v0`, `fern_v1`, `fern_v2` in `Assets/Art/Source/`, keyed by `apps/terrarium/Art/Scripts/key_alpha.py`. Each mesh column uses one variant. | bound on the fronds |
| fern_b_albedo.png | generated | T-TER-013 `image_gen` + `terrarium_textures.py` | kept, not bound by JarView |
| fern_b_emission.png | generated | T-TER-013 `image_gen` + `terrarium_textures.py` | kept, not bound by JarView |
| fern_emission.png | generated | T-TER-006 distance-field edge mask of the fern atlas alpha (`key_alpha.py`). Grayscale mask, mint comes from the shader. | bound on the fronds |
| halo.png | painted-by-code | `paint_textures.py` radial falloff, via `shared/assets/seed-textures/halo.png` | bound on the spill and halos |
| mist.png | painted-by-code | `paint_textures.py` noise wisp, via `shared/assets/seed-textures/mist.png` | bound on the five mist cards |
| moss_band.png | retired | Earlier stand-in of the moss cap. Not bound. The mound uses `moss_macro`. | removed from the jar |
| moss_card.png | generated | T-TER-006 4x4 tuft atlas. Sources `moss_tuft_0`, `moss_tuft_1`, `moss_tuft_2` in `Assets/Art/Source/`. The fourth tuft is a horizontal flip of `moss_tuft_2` (that generation was rate-limited). Keyed by `key_alpha.py`. | bound on MossSkirt |
| moss_fuzz.png | retired | Earlier stand-in tuft tile. Not bound. | removed from the jar |
| moss_macro.png | generated | T-TER-006 plates `moss_albedo.png` and `moss_cushion.png`. `key_alpha.py` quilts the cushion plate, raises crevice contrast, and grades the mean to measured moss `#376222`. Triplanar on the clump mound. | bound on the moss mound |
| moss_tile.png | retired | Earlier stand-in moss tile. Not bound. | removed from the jar |
| moss_top.png | painted-by-code | `paint_textures.py` fbm moss cap, via `shared/assets/seed-textures/moss_top.png` | not bound. The mound uses `moss_macro`. |
| moss_tuft.png | generated | T-TER-013 `image_gen` + `terrarium_textures.py` | kept, not bound by JarView |
| ring.png | painted-by-code | `paint_textures.py` analytic ring and pool, via `shared/assets/seed-textures/ring.png` | bound on the breath ring |
| soil.png | generated | T-TER-013 `image_gen` + `terrarium_textures.py` | kept, not bound by JarView |
| soil_band.png | crop-from-reference | `paint_textures.py` crop of the soil band in the A1-03 night frame | stand-in, art debt (gate A4). Bound on the soil |
| spore.png | painted-by-code | `paint_textures.py` radial sprite, via `shared/assets/seed-textures/spore.png` | bound on the spore particles |
