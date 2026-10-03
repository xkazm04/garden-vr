# Dial textures

Interim art for the drawn dial. Nothing in this folder that the dial uses is taken from the owner's reference frames.

| File | Note |
|---|---|
| dial_face.png | fitted by fit_notebook.py fit_face from Assets/Art/Source/dial_face_nb.png (prompt dial_face_nb.png.prompt.txt), then grade_a2.py: dusk wash shifted for the warm key light, painted rim cleared. Dial_Face draws the ink ring |
| soil.png | painted by code (IWSDK seat SVG), interim |
| plant_sunrise_0.png | removed. Interim IWSDK seat card, replaced by plant_sunrise_seed/sprout/young/leafy/full |
| plant_sunrise_1.png | removed. Interim IWSDK seat card |
| plant_sunrise_2.png | removed. Interim IWSDK seat card |
| plant_sunrise_bloom.png | removed. Interim IWSDK bloom, replaced by bloom_sunrise_bud and bloom_sunrise_open |
| plant_midday_0.png | removed. Interim IWSDK seat card |
| plant_midday_1.png | removed. Interim IWSDK seat card |
| plant_midday_2.png | removed. Interim IWSDK seat card |
| plant_midday_bloom.png | removed. Interim IWSDK bloom, replaced by bloom_midday_bud and bloom_midday_open |
| plant_dusk_0.png | removed. Interim IWSDK seat card |
| plant_dusk_1.png | removed. Interim IWSDK seat card |
| plant_dusk_2.png | removed. Interim IWSDK seat card |
| plant_dusk_bloom.png | removed. Interim IWSDK bloom, replaced by bloom_dusk_bud and bloom_dusk_open |
| halo_sunrise.png | removed. Interim IWSDK halo, replaced by halo_sunrise_seed/sprout/young/leafy/full |
| halo_midday.png | removed. Interim IWSDK halo |
| halo_dusk.png | removed. Interim IWSDK halo |
| plant_sunrise_{seed,sprout,young,leafy,full}.png | generated. Prompt apps/sundial/Art/Source/plants/src_sunrise_{stage}.prompt.txt. Keyed by plants_key.py. Pale paper crust is dropped. Brown-grey sunrise leaves are graded back to sage so a quiet stage stays green. |
| plant_midday_{seed,sprout,young,leafy,full}.png | generated. Prompt apps/sundial/Art/Source/plants/src_midday_{stage}.prompt.txt |
| plant_dusk_{seed,sprout,young,leafy,full}.png | generated. Prompt apps/sundial/Art/Source/plants/src_dusk_{stage}.prompt.txt |
| bloom_{sunrise,midday,dusk}_{bud,open}.png | generated. Overlay only. Prompt apps/sundial/Art/Source/plants/src_{arc}_{bud|open}.prompt.txt |
| halo_{sunrise,midday,dusk}_{seed,sprout,young,leafy,full}.png | painted by plants_halo.py from that card's silhouette. The png is an R/G mask |
| sparkle_ring_{1,2,3}.png | dusk ritual answer. image_gen, prompts in apps/sundial/Art/Source/sparkle/. Paper keyed to alpha. Not from a reference frame |
| dial_paper.png | painted by code, paper grain for the rim, interim |
| gnomon.png | painted by code, apps/sundial/Art/Scripts/dial_face.mjs, ink shaft, gold collar, nib |
| gnomon_shadow.png | painted by code. fit_notebook.py paint_shadow and compose_paint.py both write this path. Soft brown wash, straight alpha |
| soil_bed.png | fitted by fit_notebook.py fit_soil from Assets/Art/Source/soil_bed_nb.png (prompt soil_bed_nb.png.prompt.txt) |
| tiles_atlas.png | fitted by fit_notebook.py fit_tiles from Assets/Art/Source/tiles_nb.png (prompt tiles_nb.png.prompt.txt), then grade_a2.py on the dusk column |
| packet-{morning,midday,winddown}.png | generated. Crop of Art/Source/packets/packet-sheet.png, white keyed out. Prompts in Art/Source/packets/ |

Reference candidates for A/2-05-F1 and A/2-05-F2 are generated images under `apps/sundial/Art/Source/ref-candidates/`. They are not anchors. The owner picks anchors at R2.
