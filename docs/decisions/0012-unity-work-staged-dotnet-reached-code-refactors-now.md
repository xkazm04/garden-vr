# 0012 - Unity work waits for the first Unity session; milestone 3 refactors what dotnet test reaches

Date: 2026-10-07. Status: accepted. Parts (a) and (b) come from the owner (2026-10-07). Parts (c) and (d) are the App
Master's, made under that answer on 2026-10-07; the owner may overrule them.
Amended by 0013: it adds item 6 (Android ASTC 6x6 overrides) to (a), a Phase 2 exit for the plates to item 3, and a SundialSave Settings and Focus step B to item 4.

Source: the owner's answer of 2026-10-07 at 11:17Z to the App Master's ask 109debf1, "Can Unity run before 2026-10-16,
here or anywhere?": "No Unity before the gate", and his note, "no Unity session before the 10-16 gate". And the base
commit `aa9419c`, from which every number and path below is quoted. The sources are `docs/architecture/review-2026-10.md`
(sections 1, 4, 5), `docs/assets/M2-READING-2026-10-07.md` (sections 1, 2, "What stays open"), `docs/assets/duplicates.json`
and `docs/assets/DUPLICATES.md`, `docs/health/BASELINE.md`, the two Model csproj files under `shared/core-dotnet/` and
`docs/PLAN.md` section 8.

**Constraint.** This machine has no Unity licence. The owner's brief says the gate is `dotnet test shared/core-dotnet`,
and no claim of a Unity build or PlayMode pass is made. The owner rules out a Unity session before 2026-10-16, here or
elsewhere. No date is set for the first Unity session. The latest it can come is Phase 2 Quest integration from
2026-10-24, which `docs/PLAN.md:63` states as "Sat 10-24 to Wed 11-11 | Only apps that passed: XR hands provider,
passthrough, table + anchor, Operator e2e in XR Simulator, device perf; headset sessions H1-H3". Decision 0004 froze every
step B and the six in-place app files until a session that can compile Unity exists. That freeze now has no end date
before the 2026-10-16 target.

## Decision

### (a) The first Unity session: one ordered checklist

Each item names the evidence it must leave. Evidence goes under `docs/` or `orchestration/runs/`, and a claim without it
is "not run".

1. **Compile both apps over every core change since `89971d1`, run the EditMode and PlayMode suites, record the result.**
   This is the debt in 0004 and `docs/PLAN.md:184`. It also covers every edit made under (c) below. Evidence: the Unity
   log of each compile and each suite run, with the pass and fail counts, committed under `orchestration/runs/`.
2. **Cut the 9 duplicate groups decided as cut and not yet cut** (9,613,545 bytes, M2-READING section 1; per group, the
   blob plus its `.meta`, from `docs/assets/DUPLICATES.md`). Each is a `visible-change`, so each needs item 5. Paths are
   from `docs/assets/duplicates.json`; the editor step is the one its `reason` states, and where the json names none,
   none is named here.
   - `plate-dial` (2,289,519 bytes). Keep `shared/assets/room-plates/plate-dial.png`; cut
     `apps/sundial/Assets/Art/Plates/plate-dial.png`. Run `SceneSetup.Run`; the app copy becomes generated and untracked
     (new `.gitignore` line `apps/*/Assets/Art/Plates/plate-*.png`).
   - `plate-dial-seated` (1,574,842). Keep `shared/assets/room-plates/plate-dial-seated.png`; cut
     `apps/sundial/Assets/Art/Plates/plate-dial-seated.png`. Run `SceneSetup.Run`; `Assets/Scenes/Main.unity` and
     `PcRoomPlate.mat` bind it by guid and `SceneSetup.Run` regenerates both. Same `.gitignore` line.
   - `plate-jar` (1,302,962). Keep `shared/assets/room-plates/plate-jar.png`; cut
     `apps/terrarium/Assets/Art/Plates/plate-jar.png`. Run `SceneSetup.Run` (Terrarium's); generated and untracked.
   - `plate-jar-seated` (1,275,021). Keep `shared/assets/room-plates/plate-jar-seated.png`; cut
     `apps/terrarium/Assets/Art/Plates/plate-jar-seated.png`. Run `SceneSetup.Run`; `Main.unity` and `PcRoomPlate.mat`
     bind it by guid. Generated and untracked.
   - `moss_fuzz` (1,221,160). Keep `shared/assets/seed-textures/moss_fuzz.png`; cut
     `apps/terrarium/Assets/Art/Textures/moss_fuzz.png` and its `.meta`. In the same change: the `JarSetup.cs` table
     entry (`:36`) and the alpha rule (`:74`), and the `PROVENANCE.md` row (`:26`). `JarSetup.cs` throws on a missing
     listed texture.
   - `moss_tile` (721,516). Keep `shared/assets/seed-textures/moss_tile.png`; cut
     `apps/terrarium/Assets/Art/Textures/moss_tile.png` and its `.meta`. Same change: `JarSetup.cs:36` table entry and
     `PROVENANCE.md:29`.
   - `moss_band` (636,722). Keep `shared/assets/seed-textures/moss_band.png`; cut
     `apps/terrarium/Assets/Art/Textures/moss_band.png` and its `.meta`. Same change: `JarSetup.cs:36` and
     `PROVENANCE.md:24`.
   - `moss_top` (574,008). Keep `shared/assets/seed-textures/moss_top.png`; cut
     `apps/terrarium/Assets/Art/Textures/moss_top.png` and its `.meta`. Same change: `JarSetup.cs:36` and
     `PROVENANCE.md:30`.
   - `sprig.fbx` (17,795). Keep `apps/terrarium/Assets/Resources/Companions/sprig.fbx`; cut
     `apps/terrarium/Assets/Art/Models/sprig.fbx`. The json names no editor script. Its `reason` says the Blender script
     should export straight to Resources, so the repoints are `tools/blender/terrarium_companions.py:117`, `:118`, `:123`
     and `apps/terrarium/Assets/Editor/CompanionImport.cs:15`.

   The full repoint list of every group is the `repoints` field in `duplicates.json`.
3. **The 7 images still over 1024 px** (M2-READING section 2): the 4 plates, `apps/sundial/Assets/Art/Plates/plate-dial.png`
   and `plate-dial-seated.png`, `apps/terrarium/Assets/Art/Plates/plate-jar.png` and `plate-jar-seated.png` (1824x1024,
   effective import cap 2048), and the 3 script inputs, `apps/sundial/Assets/Art/Textures/dial_face.png` and
   `dial_face_s1.png` (2048x2048, cap 1024) and `apps/terrarium/Assets/Art/Textures/condensation.png` (1024x1536, cap
   1024). The plates are the same four paths as the plate cuts in item 2, so one render compare serves both. The 3 script
   inputs are read by scripts and Unity already imports them at 1024. Evidence: a census rerun
   (`node tools/assets/census.mjs`) showing the over-1024 count, and the render compare of item 5.
4. **Every step B of review proposals 1 to 6** (`docs/architecture/review-2026-10.md` section 4), one line per edited
   file. Line ranges are the review's, at its base `ce81b8f`, and move with any edit before them. If a change here
   qualifies under (c), it may land earlier and then leaves this list for item 1.
   - Proposal 1, `apps/terrarium/Assets/Scripts/Ritual/JarRitualController.cs`: fields `:43-51`, then `:247-255`,
     `:330-346`, `:488-534`, `:755-776`, `:853-875` (about 106 lines become about 25). It overlaps the stranded
     `agent/terrarium` change to the same file. Commit it with proposal 5's part of this file.
   - Proposal 1, `apps/sundial/Assets/Scripts/Ritual/DuskRitualController.cs`: fields `:44-50`, then `:167-198`,
     `:231-236`, `:273-279` (about 45 lines become about 12). Its check is `DuskRitualTests.cs:130`.
   - Proposal 2, `apps/sundial/Assets/Scripts/Tend/SundialService.cs`: `TryRestoreBackup` `:400-410` calls
     `_store.Restore()` and adopts the document in place; the constructor binding `:135-175` becomes one method that
     both use.
   - Proposal 2, `apps/sundial/Assets/Scripts/Tend/FirstRunWizard.cs`: `:218-225` acts on the result, hides the prompt
     and runs `ShouldStart` (`:267`) again.
   - Proposal 2, `apps/terrarium/Assets/Scripts/Ritual/GardenService.cs`: `:299-312` calls `_store.Restore()` and keeps
     its adopt (`:314-327`); `FirstBackup` (`:347-356`) goes.
   - Proposal 3, `apps/sundial/Assets/Scripts/Tend/SundialController.cs`: delete the stage floor, `:87` and `:632-634`.
   - Proposal 4, `git mv` of the six in-place files with their `.meta` files, keeping their namespaces:
     `apps/sundial/Assets/Scripts/Tend/SundialService.cs`, `SundialSave.cs` and `SeedCatalog.cs` into
     `shared/packages/com.gardenvr.core/Runtime/Sundial`; `apps/terrarium/Assets/Scripts/Ritual/CompanionHabits.cs` and
     `FirstRunSteps.cs` and `apps/terrarium/Assets/Scripts/Jar/GardenJourney.cs` into
     `shared/packages/com.gardenvr.core/Runtime/Terrarium`. Then `GardenVR.SundialModel.csproj` and
     `GardenVR.TerrariumModel.csproj` point at the new paths. The review also has `GardenService.cs` follow once its one
     engine line (`:31`) moves to the app.
   - Proposal 5, `apps/terrarium/Assets/Scripts/Ritual/JarRitualController.cs`: the pieces at `:588-607`, `:877-895`
     (auto-pace hand), `:619-630`, `:1407-1418` (ring and box), `:1433-1462` (settings to `BreathConfig`), `:969`,
     `:1366` (ritual still due), `:1265` (drooping), `:931-941`, `:964-970`, `:1182-1199` (side-ritual gates),
     `:1281-1287` (voice offer). It overlaps the stranded change, and goes in one commit with proposal 1's part.
   - Proposal 5, `apps/terrarium/Assets/Scripts/Jar/JarView.cs`: `:587-588` and `:1020`. It overlaps the stranded
     change; batch it with proposal 6.
   - Proposal 5, `apps/terrarium/Assets/Scripts/Ritual/SettingsPebbles.cs`: `:26-28`.
   - Proposal 6 (after the owner's look lock, `docs/plans/art-fidelity-sprint.md:16`), `apps/sundial/Assets/Scripts/Dial/DialView.cs`:
     variant grammar `:844`, `HasVariant` `:865`, sprint parts `:805`, capture-state parsing `:1502-1552`, `Build`
     `:1225`, `PlaceTiles` `:2117`, halo-mask baking `:1878-1998`.
   - Proposal 6, `apps/terrarium/Assets/Scripts/Jar/JarView.cs`: variants `:276-300`, implications `:493-508`, capture
     parsing `:370-542`, `Build` `:1206`.
   - Proposal 6, `apps/sundial/Assets/Editor/DialSetup.cs`: the capture helpers `:880-957`, and `BoilSequence` `:163`
     with `HaloReel` `:208`.
   - Proposal 6, `apps/terrarium/Assets/Editor/JarSetup.cs`: the locked colours `:331`, `:335`, `:345`, `:346`, against
     `JarView.cs:361`, `:363`, `:75`, `:76`.
   - Proposal 6, `shared/packages/com.gardenvr.capture/Editor/CaptureCli.cs`: `DisableWorldPlates` `:432-451`,
     `AttachPlate` `:279-302`, `LoadPlate` `:326-336`, camera set-up `:202`, made public.
   - Proposal 6, the nine Sundial PlayMode files that each declare `AttachPlate(Camera)`: `ArcTimesTests`,
     `DuskRitualTests`, `FocusBlockTests`, `GratitudeRitualTests`, `JourneyTests`, `RecordTilesTests`, `RimScrubTests`,
     `StretchRitualTests`, `WeekDialTests` (no line range in the review).
5. **The render compare that every visible-change item above needs.** Each plate cut, each moss texture cut, the
   `sprig.fbx` cut and each proposal 6 edit is compared against the art reference at the same framing, and the
   side-by-side is saved (`AGENTS.md` rule 5). Evidence: the saved side-by-side image and the capture check json per
   item.

### (b) Two milestone-2 goals read as staged

"Duplicate image blobs removed" and "Shipped textures inside the 1024 px cap at source" are reported as **staged**: not
open, and not met. Their measures do not change. The first stays at 9 cut groups owed (9,613,545 bytes, M2-READING
"What stays open"); the second stays at 7 images over 1024 px, of which 4 plates are over after import (M2-READING
section 2). Milestone 2 misses its 2026-10-16 target because of these two goals alone; M2-READING records the other two
(images under `Art/Source` inside `apps/*/Assets`: 48 to 0; tracked png footprint) as met.

### (c) Amendment to 0004: a file that dotnet test reaches may change on dotnet test alone

A file may change on `dotnet test shared/core-dotnet` alone if it is one of the six app files that a dotnet project
compiles in place, or a file in `com.gardenvr.core`, and all four of these hold.

The six files are the `Compile` items of the two Model csproj files:

- `GardenVR.SundialModel.csproj`: `apps/sundial/Assets/Scripts/Tend/SundialService.cs`, `SundialSave.cs`, `SeedCatalog.cs`.
- `GardenVR.TerrariumModel.csproj`: `apps/terrarium/Assets/Scripts/Ritual/CompanionHabits.cs`,
  `apps/terrarium/Assets/Scripts/Jar/GardenJourney.cs`, `apps/terrarium/Assets/Scripts/Ritual/FirstRunSteps.cs`.

1. **Every non-private type and member signature in its assembly stays the same.** A committed reflection test in its
   test project proves this by comparing the surface to an approved list.
2. **No value that a Unity-only caller reads in the same session changes.** The change lists each Unity-only caller of
   every changed member, with the reading that shows this. An effect on disk that only the next launch reads is
   allowed when a dotnet test pins it.
3. **`dotnet test shared/core-dotnet` passes.**
4. **The edit joins item 1's compile debt.**

A change that alters a signature, or that needs a Unity-only file to change, stays a step B and is staged in (a).

### (d) Milestone 3's target

Milestone 3's goal "Top hotspots refactored under tests" now targets the top of the M1 ranking that dotnet test reaches
(`docs/health/BASELINE.md`, ranked table).

- **#7 `SundialService.cs`** comes first (score 7,440; finding 7, and the restore inside the service from proposal 2).
- The next two by M1 score are **#12 `TerrariumSave.cs`** (2,254) and **#14 `SundialSave.cs`** (2,065).
- Each is taken only after a finding is written by reading the file. A file whose reading finds nothing worth changing
  is passed over for the next one in the ranking, and the reason is recorded.
- **#1 `DialView.cs`, #2 `JarView.cs` and #3 `JarRitualController.cs`** leave the milestone-3 target and wait in (a).
- Each refactor reports code lines and duplicate rule sites beside the score, as 0004 requires.

## Alternatives that lost

- Activate Unity here, and run Unity elsewhere from a runbook in `orchestration/queue`: the owner declined both.
- Keep 0004's freeze on the six in-place files: milestone 3 would be left with additive step As only, although a39cbdf
  gave those files a dotnet net of 22 Sundial and 14 Terrarium tests.
- Refactor app files that dotnet does not compile: no net, as in 0004's second alternative.

## Consequences

The goals in (b) read as staged. The compile debt of item 1 grows with every change made under (c). The first Unity
session now has one ordered list, and no date is invented for it. `docs/PLAN.md` gains row D11 and leaves `docs/PLAN.md:184`
unedited: its Unity compile is deferred, not dropped. 0004 gains one line under its Status. The `dotnet test
shared/core-dotnet` gate at the base commit counts 287 tests (251 core, 22 Sundial model, 14 Terrarium model), all passing.
