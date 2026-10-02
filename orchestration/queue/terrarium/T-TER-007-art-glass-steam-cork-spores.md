---
id: T-TER-007
app: terrarium
title: Author the glass droplets, steam flipbook, cork, spores and the first flower, and bring the jar's transparent fill under budget
depends: [T-TER-006]
estimate_min: 120
touches: [tools/blender/terrarium_jar.py, tools/blender/terrarium_flower.py, apps/terrarium/Art/Source/**, apps/terrarium/Art/Scripts/**, apps/terrarium/Assets/Art/**, apps/terrarium/Assets/Scripts/Jar/**, shared/packages/com.gardenvr.fx/Runtime/Shaders/FGlass.shader, shared/packages/com.gardenvr.fx/Runtime/Shaders/FGlow.shader, orchestration/runs/terrarium/T-TER-007/**]
---
## Goal
Close the remaining round-3 jar gaps ("steam, cork, droplets": Gemini's top three) and the borrowed cork pixels, add the
first-flower asset that day 7 needs, and measure the jar's transparent overdraw to the hard cap (mean <= 1.5 layers,
soft 1.2). Bounded: max 3 compare rounds, max 24 image generations.

## Read first
- `docs/art/terrarium-style.md` sections 1, 2 (overdraw rule), 4 (mist, spores, the answer), 6, 7.
- Round-3 cost table and overdraw maps: `C:\Users\kazda\kiro\personas\.contest\arena\habit-garden-r2-r3\entries\claude-claude-opus-5-5_high-v1\variant-1\index.html` section 04 (full jar 1.85 mean layers over 72% of the frame; lean 2.25 over 30%).
- `orchestration/runs/terrarium/T-TER-006/REPORT.md` (what is already fixed; open findings).

## Steps
0. `git merge --no-edit main`. Create `orchestration/runs/terrarium/T-TER-007/`.
1. **Steam / mist**: generate 6-8 soft smoke wisps (`image_gen`, on black), compose an 8x8 flipbook with a script in
   `apps/terrarium/Art/Scripts/`; one `Card` material with flipbook UV; replace the five loop cards with at most 3.
2. **Glass droplets**: a condensation droplet map (generated macro beads on black -> height/normal estimate, labelled an
   estimate in `PROVENANCE.md`), finer than round 3, denser low on the glass; the breath fog keeps its bottom-up sweep.
3. **Cork**: a macro cork texture, tileable (wrap-around edge diff logged), top and side; replace the cropped cork bands.
4. **Spores**: one particle system, 20-40 points, gold `#F2D27A`, drifting up 1-3 cm/s, brightening 1 s on the answer.
5. **First flower**: `tools/blender/terrarium_flower.py` (a small 6-petal bell bloom, under 1.5k tris) + a petal
   texture (generated); `Glow` amber-gold; `JarView` shows `Flowers` count blooms at deterministic positions.
6. **Fill budget**: run `Measure`; if mean transparent layers over the jar > 1.2, shrink the desk spill and jar halo,
   merge mist cards, and drop the glass back pass where invisible; report each change with its before/after numbers.
7. Compare loop at G1 and G2 (side-by-sides, same-pixel crops of cork, glass rim, mist); findings as located corrections;
   keep the best round.
8. Commit per sub-step.

## Acceptance (each check runnable on Windows without a headset; say the command)
```bash
U="C:/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Unity.exe"; R=orchestration/runs/terrarium/T-TER-007
"$U" -batchmode -quit -projectPath apps/terrarium -executeMethod GardenVR.Capture.Editor.CaptureCli.Measure -scene Assets/Scenes/Main.unity -target jar -out "$(pwd)/$R/measure.json" -logFile "$R/measure.log"
cat "$R/measure.json"     # transparentMeanLayers <= 1.5 (target 1.2), drawsEst <= 32, tris <= 48000
"$U" -batchmode -quit -projectPath apps/terrarium -executeMethod GardenVR.Capture.Editor.CaptureCli.Shot -scene Assets/Scenes/Main.unity -framing JarG1 -state "answer=1,time=4" -reference "$(pwd)/shared/assets/art-reference/A1-03-night-moss-2.png" -out "$(pwd)/$R/final/jar-g2.png" -logFile "$R/cap.log"
cat "$R/final/jar-g2.png.check.json"   # magentaFrac < 0.001
grep -c "crop-from-reference" apps/terrarium/Assets/Art/Textures/PROVENANCE.md   # 0 rows still in use (gate A4)
grep -iE "edge.?diff" "$R/REPORT.md"   # the cork tiling result is reported
```

## Evidence to leave (paths under orchestration/runs/terrarium/T-TER-007/)
`REPORT.md` (rounds table, overdraw before/after, image generation count), `final/jar-g1.sbs.png`, `final/jar-g2.sbs.png`,
crops, `overdraw-jar.png` from `Measure`, `measure.json`, `flower.png` (the bloom close-up).

## Out of scope
Companion plants (T-TER-009), new shaders beyond FGlass/FGlow/Card usage, post-processing, any Quest setting.
