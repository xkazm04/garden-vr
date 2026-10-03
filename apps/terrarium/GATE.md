# Terrarium gate pack

Read against `docs/PLAN.md` section 4. This file records what was run in T-TER-022. It does not certify an owner item. Not measured is not a pass. The gate is not passed: owner rows and the unrun judge stay open.

Evidence lives in `orchestration/runs/terrarium/T-TER-022/`. Logs (`*.log`) are gitignored. The numbers below are copied from the files that are kept.

| Item | Status | Witness | Evidence |
|---|---|---|---|
| U1 | pass | Host. `FirstRun_SixBreaths` in the PlayMode suite. The largest `answerAt` in that log is 63.866 s, and the test asserts `<= 180`. | `playmode.xml` (43 passed, 2026-10-03 14:09:21Z). Log line `answerAt=63.866` next to `[FirstRun] step=tour.answer`. |
| U2 | needs-owner | A first-time person, stopwatch, fresh save. Not run. | none |
| U3 | pass | Host. `dotnet test shared/core-dotnet/GardenVR.Core.Tests/GardenVR.Core.Tests.csproj` | `core-tests.txt`: Passed 96, Failed 0, 99 ms. |
| U4 | needs-owner | Owner, four days on the PC build between 10-15 and 10-21. Not run. | none |
| U5 | needs-owner | Host half is done. Property tests passed inside the 96. Case-insensitive grep of `apps/*/Assets` for `streak`, `failed`, `broke`, `lost your` finds no sentence the player shows. The owner has not read every string. | `string-census.txt`. `TerrariumInvariantTests.Property_fronds_never_decrease_over_1000_random_lives`. |
| S1 | pass | Host grep of `*.cs` for `Input.Get`, `Keyboard.current`, `Mouse.current`. | `s1-device-reads.txt`. The only package hit is `KeyboardMouseIntentSource.cs` lines 93-94. No hit under `apps/terrarium/Assets`. |
| S2 | pass | Host. `Canonical_EndState` in ten separate Unity processes. | `playback-10.txt`: 10 exits of 0, one hash `ce41afb66dcf948eed8ea88ba92d0dcbdc2d5eeb0366b8f44bf77ad1ae389bcb`. Per-run JSON in `playback/run-01` through `run-10`. |
| S3 | pass | Host. `Response_WithinTwoFrames`. Logged `response-dt=0`, under 2/60 s. | `playmode.log` line `response-dt=0`. Same test in `playmode.xml`. |
| S4 | pass | Host. `PauseAt_Waiting`, `PauseAt_Inhale1`, `PauseAt_Exhale1`, `PauseAt_Inhale3`, `PauseAt_Inhale6` inside the 43 PlayMode tests. | `playmode.xml`. |
| S5 | pass | Host. One warmup, then three windowed launches of `apps/terrarium/Build/Terrarium.exe`, killed on the first interactive line. Median of the three. | `cold-start.txt`: 0.349, 0.354, 0.390 s. Median 0.354 s. Gate is `<= 4` s. |
| S6 | needs-owner | Owner, at R2 and G. Not run. | none |
| A1 | needs-owner | Owner scores each gate frame `>= 4`. Side-by-sides from earlier tasks are not this pack's certification. | none for this pack |
| A2 | not measured | Host palette delta-E, line width, and size tool was not run. One manual check: `ring.png` stroke peaks at 0.922 of the card half-extent, so the 0.152 m card puts the stroke at 7.0 cm. The bible asks 6.0 cm, plus or minus 8 percent. | note only. No delta-E file. |
| A3 | not measured | Outside judge was not run. | none |
| A4 | pass | Host census. Every png under `apps/terrarium/Assets/Art/Textures` is named in `PROVENANCE.md` (26 files). The file says none are sampled from the A1-03 frames. | `apps/terrarium/Assets/Art/Textures/PROVENANCE.md` |
| A5 | pass | Host `CaptureCli.Measure` on `Main.unity`, target `jar`, framing `JarG1`. | `measure.json`: drawsEst 18 (`<= 40`), tris 14502 (`<= 60000`), transparentMeanLayers 1.440039 (`<= 1.5`), every listed texture `<= 1024`. `GardenRenderer` has no features and `postProcessData` 0. `GardenURP` volume profile is 0. `SeatedRig` `m_RenderPostProcessing` is 0. Room plates `plate-jar.png` and `plate-jar-seated.png` are 1824 wide and their importer max is 2048. Measure turns world plates off, so they are not in `measure.json`. |
| A6 | needs-owner | Host loudness passes. Owner has not listened. | `loudness.txt`: mix I -20.6 LUFS, true peak -1.6 dBFS, narration 10.0 dB above `bed.night`. No cue id or clip repeats inside 3 s (`fog.hiss` gap 9.2 s). Wav: `mix-full.wav`. The app does not apply `masterGainDb`. The wav does. |
| A7 | needs-owner | Host `ReducedMotion_ShowsEndStates` passed in the PlayMode suite. A 60 s owner watch of the hero was not recorded for this pack. | `playmode.xml`. |

JarG1 before the crozier card was tightened: `measure-before-halo.json` mean 1.528229. The card on `Jar.prefab` is now 0.025 m (`JarView.CoilHaloSize`).
