# Jar textures

Round-3 night-jar maps copied from `shared/assets/seed-textures/`. Origins follow `tools/blender/paint_textures.py` and the round-3 report (gate A4): a crop from the owner's frame is a stand-in, not authored art.

The T-TER-013 generated maps stay in this folder. `JarView` binds the round-3 names, not those.

| File | Origin | Source | Status |
|---|---|---|---|
| condensation.png | painted-by-code | `paint_textures.py` droplet field, via `shared/assets/seed-textures/condensation.png`. Replaces the T-TER-013 generated file of the same name. | bound on the glass |
| cork.png | generated | T-TER-013 `image_gen` + `terrarium_textures.py` | kept, not bound by JarView |
| cork_side.png | crop-from-reference | `paint_textures.py` crop of the cork band in `A1-03-night-moss-1` | stand-in, art debt (gate A4). Bound on the cork side |
| cork_top.png | painted-by-code | `paint_textures.py` fbm cork top, via `shared/assets/seed-textures/cork_top.png` | bound as the cork cap |
| fern_a_albedo.png | generated | T-TER-013 `image_gen` + `terrarium_textures.py` | kept, not bound by JarView |
| fern_a_emission.png | generated | T-TER-013 `image_gen` + `terrarium_textures.py` | kept, not bound by JarView |
| fern_albedo.png | painted-by-code | `paint_textures.py` pinnate frond, via `shared/assets/seed-textures/fern_albedo.png` | bound on the fronds |
| fern_b_albedo.png | generated | T-TER-013 `image_gen` + `terrarium_textures.py` | kept, not bound by JarView |
| fern_b_emission.png | generated | T-TER-013 `image_gen` + `terrarium_textures.py` | kept, not bound by JarView |
| fern_emission.png | painted-by-code | `paint_textures.py` edge distance field, via `shared/assets/seed-textures/fern_emission.png` | bound on the fronds |
| halo.png | painted-by-code | `paint_textures.py` radial falloff, via `shared/assets/seed-textures/halo.png` | bound on the spill and halos |
| mist.png | painted-by-code | `paint_textures.py` noise wisp, via `shared/assets/seed-textures/mist.png` | bound on the five mist cards |
| moss_band.png | crop-from-reference | `paint_textures.py` crop of the moss cap in `A1-03-night-moss-1` | stand-in, art debt (gate A4). Bound on the moss mound |
| moss_card.png | crop-from-reference | `paint_textures.py` keyed crop of the moss ridge in `A1-03-night-moss-1` | stand-in, art debt. Not bound. Round 3 built the tuft material and did not place the cards |
| moss_fuzz.png | crop-from-reference | `paint_textures.py` cropped moss tile tinted by painted tuft noise | stand-in, art debt where the colour is the owner's frame. Not bound |
| moss_macro.png | generated | T-TER-013 `image_gen` + `terrarium_textures.py` | kept, not bound by JarView |
| moss_tile.png | crop-from-reference | `paint_textures.py` mirrored crop of `A1-03-night-moss-1` | stand-in, art debt. Not bound |
| moss_top.png | painted-by-code | `paint_textures.py` fbm moss cap, via `shared/assets/seed-textures/moss_top.png` | not bound. The mound uses `moss_band` for the cap, as round 3 did |
| moss_tuft.png | generated | T-TER-013 `image_gen` + `terrarium_textures.py` | kept, not bound by JarView |
| ring.png | painted-by-code | `paint_textures.py` analytic ring and pool, via `shared/assets/seed-textures/ring.png` | bound on the breath ring |
| soil.png | generated | T-TER-013 `image_gen` + `terrarium_textures.py` | kept, not bound by JarView |
| soil_band.png | crop-from-reference | `paint_textures.py` crop of the soil band in `A1-03-night-moss-1` | stand-in, art debt (gate A4). Bound on the soil |
| spore.png | painted-by-code | `paint_textures.py` radial sprite, via `shared/assets/seed-textures/spore.png` | bound on the spore particles |
