---
id: T-SUN-007
app: sundial
title: Draw the three plant species as a coherent set of inked, cel-shaded cards for five growth stages plus bud and bloom, with derived halos
depends: [T-SUN-005]
estimate_min: 120
touches: [apps/sundial/Art/Source/**, apps/sundial/Art/Scripts/**, apps/sundial/Assets/Art/**, apps/sundial/Assets/Scripts/Dial/**, orchestration/runs/sundial/T-SUN-007/**]
---
## Goal
The plants are the round-3 gap ("the plants are the owner's drawings, keyed": art debt). Make our own: 3 species (a
sunrise herb sprig, a midday coral flower, a dusk lavender) x 5 stages (Seed, Sprout, Young, Leafy, Full) plus a bud and
an open-bloom overlay per species = 21 drawings in the anime-pushed linework (clean confident ink contours of varying
weight, flat cel colour with one soft shade step, a light wash), consistent as a set, keyed to alpha with a stable base
pivot, each with a halo derived from its silhouette. `DialView` shows the card for each plant's `Stage` and `Bloom`.
Budget: up to 30 image generations (an exception to the 24 default, because the set is 21 drawings).

## Read first
- `docs/art/sundial-style.md` sections 1 (palette), 2 (plants: `Card`, alpha-to-coverage, UV boil), 5-7 (rubric:
  "a constant-width vector contour on all plants" is a disqualifier).
- `docs/plans/sundial.md` section 5 (`Stage` thresholds, `Bloom`).
- `OWNER-CHOICE.md` A/2-05-F1 "Style:" block (reuse it verbatim as the style prefix of every plant prompt) and the extra
  negative; `docs/knowledge/registry-consult.md` (sprite-and-atlas-production: the set is the deliverable).

## Steps
0. `git merge --no-edit main`. Create `orchestration/runs/sundial/T-SUN-007/`.
1. Prompts: one template per species and stage (same style prefix, same framing: single plant, centred, base at the
   bottom centre, on plain white paper, no shadow, no text); save each kept image with `.prompt.txt` under
   `apps/sundial/Art/Source/plants/`. Generate the Full stage of each species first, gate it at headset scale (a 128 px
   tall preview), then derive the other stages from it with `image_edit` so the species stays one plant.
2. `apps/sundial/Art/Scripts/plants_key.py`: key white to alpha (ink threshold, keep wash transparency), trim to a fixed
   canvas (256 x 512) with the base pivot at a fixed pixel, write `plant_<arc>_<stage>.png` and `bloom_<arc>_<bud|open>.png`;
   a coherence report per species (mean hue and saturation of the wash, mean ink line width in px, pivot offset) with the
   tolerance: hue within 8 deg, line width within 25%, pivot within 2 px.
3. `apps/sundial/Art/Scripts/plants_halo.py`: per card, dilate the silhouette, trace a soft gold line (`halo.gold`) and a
   faint glow, `halo_<arc>_<stage>.png` (the round-3 approach, cleaner double-bubble shape).
4. Pack into one atlas per species (gutters from filter width + mips, as the registry says) or keep separate textures if
   draws stay inside the cap; import with alpha-to-coverage; check ASTC preview for haloing on ink lines (compare the
   compressed preview with the source at framing size).
5. `DialView`: map `Stage` -> card, `Bloom` -> overlay, halo per stage; the UV boil stays on.
6. Captures: G1 and a "stage strip" capture (all 5 stages + bud + open for each species side by side on the dial, a dev
   state `stages=strip`), side-by-side G1 vs the reference; `Measure`.
7. `PROVENANCE.md`: the interim IWSDK plant rows marked `removed`; new rows `generated`, with prompt file paths.
8. Commit per sub-step.

## Acceptance (each check runnable on Windows without a headset; say the command)
```bash
U="C:/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Unity.exe"; R=orchestration/runs/sundial/T-SUN-007
ls apps/sundial/Assets/Art/Textures/plant_*_*.png | wc -l                          # 15
ls apps/sundial/Assets/Art/Textures/bloom_*_*.png | wc -l                          # 6
python apps/sundial/Art/Scripts/plants_key.py --report-only > "$R/coherence.txt"; cat "$R/coherence.txt"   # every species within tolerance
"$U" -batchmode -quit -projectPath apps/sundial -executeMethod GardenVR.Capture.Editor.CaptureCli.Shot -scene Assets/Scenes/Main.unity -framing DialG1 -target dial -state "halo=1,haloTarget=midday,gnomonDeg=105,time=1" -overlay "$(pwd)/shared/assets/room-plates/dial-hand-matte.png" -reference "$(pwd)/shared/assets/art-reference/A2-05-field-notebook-1.png" -out "$(pwd)/$R/dial-g1.png" -logFile "$R/shot.log"; cat "$R/dial-g1.png.check.json"
"$U" -batchmode -quit -projectPath apps/sundial -executeMethod GardenVR.Capture.Editor.CaptureCli.Measure -scene Assets/Scenes/Main.unity -target dial -out "$(pwd)/$R/measure.json" -logFile "$R/measure.log"; cat "$R/measure.json"   # drawsEst <= 30 (hard), tris <= 30000
```

## Evidence to leave (paths under orchestration/runs/sundial/T-SUN-007/)
`REPORT.md` (generation count; rejected candidates and why; coherence table), `plants-contact.png` (all 21 cards on one
sheet), `stage-strip.png`, `dial-g1.sbs.png`, `coherence.txt`, `measure.json`.

## Out of scope
Habits per arc beyond one; hand-lettered labels; animation beyond the UV boil and the bloom pulse (T-SUN-008).
