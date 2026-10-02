---
id: T-SUN-009
app: sundial
title: Add the three-breath dusk ritual at the wind-down plant using the shared breath rules, with pause, resume and reduced motion
depends: [T-SUN-008, T-TER-003]
estimate_min: 90
touches: [apps/sundial/Assets/Scripts/**, apps/sundial/Assets/Tests/**, apps/sundial/Art/Source/**, apps/sundial/Assets/Art/**, orchestration/runs/sundial/T-SUN-009/**]
---
## Goal
The in-app habit of the Sundial: at the dusk plant, PinchHold breathes in (an ink circle swells around the plant),
Release breathes out (it shrinks), three self-paced breaths, then the plant answers (a leaf opens, a hand-drawn sparkle
ring, the `dusk.chime` close) and the wind-down habit is kept with source `Ritual`. It uses the Terrarium-owned
`BreathSession` with `TargetBreaths = 3`, `MinInhaleSeconds = 1.5`.

## Read first
- `docs/plans/sundial.md` sections 2 (item 5), 3 (t = 70-165 s), 7 (narration outline: voice comes in T-SUN-012).
- `shared/packages/com.gardenvr.core/Runtime/Breath/BreathSession.cs` (consume; do not edit: owned by terrarium).
- `docs/art/sundial-style.md` section 4 (breath circle, reduced motion).

## Steps
0. `git merge --no-edit main`. Create `orchestration/runs/sundial/T-SUN-009/`.
1. `DuskRitualController`: starts when the user pinch-holds the wind-down plant while the ritual prompt is shown (from
   18:00, or any time on day 1; the arc is a cue, never a gate) or via `prompt.breaths`; feeds `PinchSample` from the
   provider into `BreathSession` (3 breaths, 1.5 s minimum inhale); the ink circle radius follows the inhale fraction and
   shrinks over the exhale; never auto-paced.
2. Answer: a generated hand-drawn sparkle ring (2-3 `image_gen` sprites, kept with prompts), the plant's next leaf, the
   `dusk.chime` cue id, and `Ledger.Tend(source: Ritual)` (no deferred undo for a ritual completion).
3. Pause / resume: `PalmOpen` or `SystemPause` holds the circle; after 60 s paused, offer "Continue?" (`prompt.continue`).
4. Reduced motion: the circle becomes a fill ring; breath timing unchanged.
5. PlayMode tests: `Dusk_ThreeBreaths` (complete in <= 40 s app time; the wind-down window's last tile `Kept`, source
   `Ritual`); `Dusk_FidgetsIgnored` (1.0 s holds do not count); `Dusk_PauseAndResume_NoLoss`; `Dusk_ReducedMotion_SameTiming`.
6. Captures at `DialG1`: mid-inhale and the answer; a sequence `dusk.mp4`.
7. Commit per sub-step.

## Acceptance (each check runnable on Windows without a headset; say the command)
```bash
U="C:/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Unity.exe"; R=orchestration/runs/sundial/T-SUN-009
"$U" -batchmode -projectPath apps/sundial -runTests -testPlatform PlayMode -testResults "$(pwd)/$R/playmode.xml" -logFile "$R/playmode.log"
grep -o '<test-run[^>]*>' "$R/playmode.xml"                                     # failed="0", the 4 Dusk_* tests present
ffprobe -v error -show_entries format=duration -of csv=p=0 "$R/dusk.mp4"         # > 15
git diff --stat main -- shared/packages/com.gardenvr.core/Runtime/Breath | tail -1   # no changes (not owned)
```

## Evidence to leave (paths under orchestration/runs/sundial/T-SUN-009/)
`REPORT.md`, `playmode.xml`, `dusk-inhale.png`, `dusk-answer.png`, `dusk.mp4`.

## Out of scope
Narration and the chime clip (T-SUN-012); the stretch-reach ritual (post-MVP).
