---
id: T-TER-006
app: terrarium
title: Replace the borrowed moss and the regular frond with authored moss and a frond atlas, and generate candidate reference frames for the sixth breath and day 7
depends: [T-TER-004]
estimate_min: 120
touches: [tools/blender/terrarium_moss.py, tools/blender/terrarium_jar.py, apps/terrarium/Art/Source/**, apps/terrarium/Art/Scripts/**, apps/terrarium/Assets/Art/**, apps/terrarium/Assets/Scripts/Jar/**, shared/packages/com.gardenvr.fx/Runtime/Shaders/FGlow.shader, orchestration/runs/terrarium/T-TER-006/**]
---
## Goal
Close the two biggest round-3 gaps at the G1 framing: the moss reads as a textured dome (and borrows reference pixels),
and the frond lace is regular. Also produce candidate frames for the two states the owner's references do not show
(G3 the sixth breath, G4 day 7) so the owner can pick parity anchors at R2. Bounded loop: at most 3 render-compare
rounds, at most 24 image generations; keep the best attempt with its residuals, never silently the last.

## Read first
- `docs/art/terrarium-style.md` (palette, materials, sections 5-7: framing, spec anchor, rubric and disqualifiers).
- `docs/plans/terrarium.md` section 6 (the element table) and `docs/PLAN.md` section 7 (image generation rules).
- Round-3 gap ledger and "What failed" (fuzz shells, keyed tuft cards): `C:\Users\kazda\kiro\personas\.contest\arena\habit-garden-r2-r3\entries\claude-claude-opus-5-5_high-v1\variant-1\index.html` sections 03, 05.
- `C:\Users\kazda\kiro\personas\.contest\staging\habit-garden\OWNER-CHOICE.md` (A1-03-F1, A1-03-F2 prompts and the extra
  negative) and `ART-STYLE-STORYBOARD.md` (the shared negative prompt).
- `tools/blender/paint_textures.py` (round-3 distance-field edge glow you can reuse).

## Steps
0. `git merge --no-edit main`. Create `orchestration/runs/terrarium/T-TER-006/`.
1. **Reference candidates**: with `image_gen`, 4 images for A1-03-F1 and 4 for A1-03-F2, using the exact prompts and
   negatives (16:9). Save to `apps/terrarium/Art/Source/ref-candidates/` with `.prompt.txt` sidecars and write
   `CANDIDATES.md` (a contact sheet image + one line per candidate). Do not treat any as an anchor: the owner picks at R2.
2. **Moss**: `tools/blender/terrarium_moss.py` builds a sculpted mound (displaced dome with clumped noise, a lower
   lip), and a skirt ring of 24-40 cards at the silhouette, exported into the jar FBX pipeline (extend
   `terrarium_jar.py` to call it). With `image_gen`, a 4x4 moss tuft atlas (top-down tufts on pure black) and a moss
   albedo; key alpha from luminance with a script in `apps/terrarium/Art/Scripts/`; check tiling with a wrap-around
   edge diff for the albedo. Probe each `bpy` op you rely on in `-b` mode and log the result.
3. **Fronds**: with `image_gen`, a bipinnate fern frond atlas, 3 variants, front-lit on black, irregular lace; key,
   generate the edge-glow distance field (reuse `paint_textures.py` logic), replace `fern_albedo` / `fern_emission`;
   the jar's fronds pick a variant by `ShapeSeed`.
4. Update `PROVENANCE.md`: the moss and frond rows become `generated` with their prompt files; the old crop rows are
   removed or marked `removed`.
5. **Compare loop** (max 3 rounds): capture G1 (`JarG1`, the G1 state) and G2 (`JarG1` framing, state `answer=1,time=4`,
   compared with `A1-03-night-moss-2.png` for look, not composition: style bible section 5), compose side-by-sides and same-pixel crops of moss, fronds and glass rim; write findings as
   located corrections (region, what differs, by how much, proposed fix); apply; keep each round's images.
   Pick the best round by the bible's rubric applied region by region (minimum counts) and say why.
6. `Measure` after the final round: within the soft caps (32 draws, 48k tris, mean transparent layers <= 1.2 over the
   jar) or explain which part exceeds and the cheaper swap.
7. Commit per sub-step.

## Acceptance (each check runnable on Windows without a headset; say the command)
```bash
U="C:/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Unity.exe"; R=orchestration/runs/terrarium/T-TER-006
ls apps/terrarium/Art/Source/ref-candidates/*.png | wc -l             # 8
ls apps/terrarium/Art/Source/ref-candidates/*.prompt.txt | wc -l      # 8
"/c/Program Files/Blender Foundation/Blender 4.2/blender.exe" -b -P tools/blender/terrarium_jar.py -- --out apps/terrarium/Assets/Art/Models > "$R/blender.log" 2>&1; grep -iE "tris|probe" "$R/blender.log" | tail -8
"$U" -batchmode -quit -projectPath apps/terrarium -executeMethod GardenVR.Capture.Editor.CaptureCli.Shot -scene Assets/Scenes/Main.unity -framing JarG1 -state "breath=0.5,uncoil=0.3,fog=0.45,time=3" -reference "$(pwd)/shared/assets/art-reference/A1-03-night-moss-1.png" -out "$(pwd)/$R/final/jar-g1.png" -logFile "$R/cap.log"
cat "$R/final/jar-g1.png.check.json"                                 # magentaFrac < 0.001
grep -iE "moss|fern|frond" apps/terrarium/Assets/Art/Textures/PROVENANCE.md   # no row in use says crop-from-reference
"$U" -batchmode -quit -projectPath apps/terrarium -executeMethod GardenVR.Capture.Editor.CaptureCli.Measure -scene Assets/Scenes/Main.unity -target jar -out "$(pwd)/$R/measure.json" -logFile "$R/measure.log"; cat "$R/measure.json"
```

## Evidence to leave (paths under orchestration/runs/terrarium/T-TER-006/)
`REPORT.md` (rounds table: round, changes, findings, region scores; the chosen best and why; image generation count),
`round1/ round2/ round3/` captures and crops, `final/jar-g1.sbs.png`, `final/jar-g2.sbs.png`, `blender.log`,
`measure.json`, the candidates contact sheet.

## Out of scope
Glass, steam, cork, spores (T-TER-007); choosing the G3/G4 anchors (owner); post-processing.
