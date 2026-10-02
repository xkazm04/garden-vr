---
id: T-SUN-011
app: sundial
title: Build the Sundial first run - the dial draws itself, the shadow sweeps to now, three seed packets plant one habit per arc - first tend under a minute, dusk ritual under three
depends: [T-SUN-009, T-SUN-010]
estimate_min: 120
touches: [apps/sundial/Art/Source/**, apps/sundial/Assets/Art/**, apps/sundial/Assets/Scripts/**, apps/sundial/Assets/Tests/**, apps/sundial/Assets/Scenes/**, orchestration/runs/sundial/T-SUN-011/**]
---
## Goal
A first-time user goes from launch to the first tend in <= 60 s and to the completed dusk ritual in <= 180 s (gate U1)
with no menu: the dial draws itself (pencil, ink, washes), the shadow sweeps from 06:00 to now, "This is your day." is
inked on the page, three seed packets rise, each opens to three presets with a cue chip, one habit per arc is planted,
the due plant is tended, and "Try three breaths?" leads to the dusk ritual. It is a wizard with step state (it collects
choices), resumable after a quit, and it never starts after a failed load.

## Read first
- `docs/plans/sundial.md` section 3 (the first-launch table is the spec) and section 4 (target ids `seed.<arc>`,
  `seed.<arc>.<preset>`, `prompt.breaths`).
- `docs/art/sundial-style.md` section 4 (dial appears, shadow sweep, reduced motion).
- `docs/knowledge/registry-consult.md` (guided-tours: advance on real intents; the habit picker is a wizard).

## Steps
0. `git merge --no-edit main`. Create `orchestration/runs/sundial/T-SUN-011/`.
1. Presets (no free text), each with a cue chip: Morning: Water ("After I wake, I drink a glass of water."), Stretch
   ("After coffee, I stretch for a minute."), Make the bed ("After I get up, I make the bed."); Midday: Top-3 plan ("When I
   sit down to work, I write my top three."), Walk a stop ("After work, I walk one stop."), Screen-free lunch ("At lunch,
   the screen stays closed."); Wind-down: Three breaths (in-app ritual, "After I brush my teeth, three breaths."), Phone
   away ("At 22:30 the phone goes on the shelf."), Read ("In bed, I read ten pages."). Strings live in one catalog file;
   no em dash; nothing about streaks or health outcomes.
2. `FirstRunWizard` with step ids `fr.appear`, `fr.sweep`, `fr.caption`, `fr.packets`, `fr.pick.morning`,
   `fr.pick.midday`, `fr.pick.winddown`, `fr.drop`, `fr.firsttend`, `fr.breaths.offer`, `fr.breaths`, `fr.done`, saved in
   `SundialSave.FirstRunStep`; runs only on load outcome `Fresh`; a relaunch resumes at the saved step with choices kept.
3. Visuals: the dial draw-in (construction 0.4 s, ink 0.4 s, washes 0.4 s; reduced motion: drawn); the caption inked with
   a handwritten-style font (an OFL-licensed font file in `Assets/Art/Fonts/` with its licence text; record it in
   `PROVENANCE.md`); three seed packets (generated paper packet drawings, `image_gen`, kept with prompts) that rise at the
   near edge; seeds drop into their arcs; the arc under the shadow glows; the ink caption "Try three breaths?" beside the
   dusk plant (on day 1 allowed at any hour).
4. PlayMode tests (fresh temp save path, injected clock 14:20, script `first-run.jsonl`):
   `FirstRun_FirstTendAndDusk` (first tend <= 60 s, ritual done <= 180 s, three habits saved, `FirstRunStep == fr.done`);
   `FirstRun_ResumeAfterQuit` (quit after picking two: relaunch resumes at the third pick with the two kept);
   `FirstRun_NotOnFailedLoad` (corrupt save: restore prompt, no wizard); `FirstRun_ReducedMotion`.
5. Record the canonical first run at `SeatedPOV` (24 fps, 0-180 s, `first-run.mp4`).
6. Windows player: `GardenVR.Sundial.Editor.Build.Windows` -> `Builds/sundial/Sundial.exe` (git-ignored); 3 cold starts
   timed from the player log (gate S5).
7. Commit per sub-step.

## Acceptance (each check runnable on Windows without a headset; say the command)
```bash
U="C:/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Unity.exe"; R=orchestration/runs/sundial/T-SUN-011
"$U" -batchmode -projectPath apps/sundial -runTests -testPlatform PlayMode -testResults "$(pwd)/$R/playmode.xml" -logFile "$R/playmode.log"; grep -o '<test-run[^>]*>' "$R/playmode.xml"   # failed="0", 4 FirstRun_* present
grep -E "firstTendAt=|ritualDoneAt=" "$R/playmode.log" | tail -2                    # <= 60 s and <= 180 s
grep -nP "\x{2014}" apps/sundial/Assets/Scripts -r || echo "no em dash"
ffprobe -v error -show_entries format=duration -of csv=p=0 "$R/first-run.mp4"      # >= 150
"$U" -batchmode -quit -projectPath apps/sundial -executeMethod GardenVR.Sundial.Editor.Build.Windows -logFile "$R/build.log"; grep -E "Build (Succeeded|Failed)" "$R/build.log"
cat "$R/cold-start.txt"                                                             # median <= 4 s
```

## Evidence to leave (paths under orchestration/runs/sundial/T-SUN-011/)
`REPORT.md` (measured timeline per step id), `playmode.xml`, `first-run.mp4`, `cold-start.txt`, `packets.png`, the font
licence path.

## Out of scope
Settings UI, PalmOpen dismiss polish, audio generation (T-SUN-012), more than one habit per arc.
