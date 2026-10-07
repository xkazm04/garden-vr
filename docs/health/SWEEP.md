# Lean sweep, code half

Milestone 3, goal "Static-analysis findings classified to zero". This is the code half of the sweep, and the first sweep
run. The asset half (the census classes in `docs/assets`) adds its rows to `docs/health/sweep.json` later, with `half`
set to `assets`. The record of every finding is `docs/health/sweep.json` (one record per finding: id, tool, rule id,
file:line, the blob it was read at, the finding, the class and the commit or the reason).

- **Base commit:** `00c2e4cee61e9cb55b8779a5f39729b75fdbbb2b` (design: game-design conformance read for Terrarium and
  Sundial). Every finding was read at its blob in that commit.
- **Fix commit:** `1a990718f551c88d34db7f963f55f59fb883c8e7` (test projects only).
- **Unclassified findings: 0 in each half.** The asset half is the last section of this file. The line below is the code half.
- **Unclassified findings (code half): 0.** 867 records after the second closure re-run: 69 fixed, 0 idea, and 798
  deliberate (24 of those staged for the first Unity session, 0012 (a)). The base run had 842 records (51 fixed, 15
  idea, 776 deliberate); I1 to I4 are closed (a576eb7, c2e05b6, 37541f0, 85973ea), 25 records were added at 37541f0, and
  I6 and I7 are closed (e02ce5e, 8a875b6). See "Closure re-run (37541f0)" and "Second closure re-run (8a875b6)" at the end of this file.

## Commands and tool versions

.NET SDK 9.0.308 (commit 46e170c517), runtime 9.0.11, Windows 10.0.26200 win-x64. The .NET analyzers are the ones the
SDK ships (AnalysisLevel 9 globalconfigs). xunit 2.9.2 brings xunit.analyzers 1.16.0. jscpd 4.3.0 was run through
`npx --yes jscpd@4` from a temp directory, so it went into the npm cache and nothing was added to the repo. Python 3.12
parsed the outputs. Every tool wrote its output to `%TEMP%/sweep95/`, outside the repo. No analyzer config, no
`.editorconfig`, no `Directory.Build.props` and no csproj change was made.

| # | Command | Exit | Raw output |
| --- | --- | ---: | --- |
| 1a | `dotnet build shared/core-dotnet/GardenVR.sln --no-incremental` | 0 | `build-default.txt` |
| 1b | `dotnet build shared/core-dotnet/GardenVR.sln --no-incremental -p:AnalysisLevel=latest-recommended` | 0 | `build-recommended.txt` |
| 2a | `dotnet format analyzers shared/core-dotnet/GardenVR.sln --verify-no-changes --severity info --report <temp>/fmt-analyzers` | 2 | `fmt-analyzers/format-report.json` |
| 2b | `dotnet format style shared/core-dotnet/GardenVR.sln --verify-no-changes --severity info --report <temp>/fmt-style` | 2 | `fmt-style/format-report.json` |
| 2c | Name grep: each non-private member in `GardenVR.Core.Tests/PublicSurface.approved.txt` (656 after constructors, operators, indexers, `value__` and object overrides are left out), counted as a whole word in every tracked `.cs` under `apps/` and `shared/` (`git ls-files`), with comments stripped and string literals kept (a string can be a reflection or serialization name). A second pass for types leaves out the declaring file. | - | `refs.tsv` |
| 3 | `npx --yes jscpd@4 --min-tokens 70 --format csharp --reporters json,console --output <temp>/jscpd-out --absolute shared/packages/com.gardenvr.core/Runtime` plus the six in-place files | 0 | `jscpd-out/jscpd-report.json` |

Exit 2 from `dotnet format --verify-no-changes` means it found changes it would make. It made none.

## Counts

This table and the sections down to "Duplication" are the base run, kept as they were read at 00c2e4c. The counts after the
closure re-run are in "Closure re-run (37541f0)" at the end of this file. Per tool. "Reported" counts unique sites (rule, file, line, column). A file compiled by two projects would count once,
but none is: every `Compile` item belongs to one project, and the Model projects reference `GardenVR.Core` rather than
compiling its sources. A site that two tools report is one record, with both tools in `seenBy`.

| Tool | Reported | Records | Fixed | Idea | Deliberate | of which staged |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| dotnet build, default level | 2 | (inside 1b) | 2 | 0 | 0 | 0 |
| dotnet build, latest-recommended | 560 | 560 | 50 | 12 | 498 | 0 |
| dotnet format analyzers | 57 (56 also in 1b) | 1 | 1 | 0 | 0 | 0 |
| dotnet format style | 244 | 244 | 0 | 0 | 244 | 1 |
| name grep | 656 members | 32 | 0 | 0 | 32 | 22 |
| jscpd | 4 clones | 4 | 0 | 3 | 1 | 0 |
| scope (Unity-only C#) | - | 1 | 0 | 0 | 1 | 1 |
| **Total** | | **842** | **51** | **15** | **776** | **24** |

Per area: test code has 461 records (51 fixed, 410 deliberate). Core has 306 (9 idea, 297 deliberate, 23 of them
staged). The six in-place app files have 74 (6 idea, 68 deliberate). The scope line is 1 more.

**What the stricter level adds.** The default level reports two warnings, both xUnit2013 (`GratitudeTests.cs:43`, `:62`).
`latest-recommended` adds 558: CA1707 302, CA1051 161, CA1050 40, CA2208 11, CA1825 10, CA1305 6, CA1861 6, CA1822 5,
CA1805 3, CA1720 3, CA2249 3, CA1859 3, CA1716 2, CA2211 2, CA1036 1. The default level reports no compiler (CS)
warning.

**After the fix (1a99071).** The default-level build has **0 warnings**. At `latest-recommended` it has 510: 308 in test
code (CA1707 302, CA1861 6, both deliberate below) and the 202 production findings, which this run does not edit.
`dotnet test shared/core-dotnet` passes 309 of 309 (Core 264, SundialModel 31, TerrariumModel 14), which is the base
count. The diff adds and removes no `[Fact]`, `[Theory]` or `InlineData`.

## Fixed (1a99071, test projects only)

| Rule | Sites | Change |
| --- | ---: | --- |
| xUnit2013 | 2 | `GratitudeTests.cs:43` and `:62`: `Assert.Equal(1, record.Marks.Count)` becomes `Assert.Single(record.Marks)`. |
| xUnit2024 | 1 | `TerrariumSaveTests.cs:172`: `Assert.False(loaded.Doc == null)` becomes `Assert.NotNull(loaded.Doc)`. |
| CA1825 | 5 | `new T[0]` becomes `Array.Empty<T>()` (RimScrubTests :144, SaveCrashWindowTests :71, SaveRestoreTests :239, SundialTests :30, WeekDialTests :113). |
| CA1859 | 3 | `SaveRestoreTests.cs` `Put`, `Text` and `Snapshot` take the `SaveCrashWindowTests.MemoryIo` that every caller passes. |
| CA1050 | 40 (39 files) | A file-scoped namespace per project: `GardenVR.Core.Tests` (as `MultiHabitTests.cs` already has), `GardenVR.SundialModel.Tests`, `GardenVR.TerrariumModel.Tests`. Inside `GardenVR.Core.Tests`, `GardenVR.Core` now comes before the using directives in name lookup. A name in both `GardenVR.Core` and an imported namespace was already ambiguous, so it could not have compiled. The only Core.Tests file without `using GardenVR.Core` is `PublicSurfaceTests.cs`, which names no core type except the fully qualified `GardenVR.Core.TerrariumSave`. |

## Ideas (the app and core are closed to this run)

**I1 to I4, I6 and I7 are closed.** Each delivery is on main and its analyzer site is gone at 37541f0 (I1 to I4) or at 8a875b6 (I6, I7); see the closure sections.

None of these repeats T1 to T11 (`docs/design/CONFORMANCE-terrarium.md`) or S1 to S14 (`CONFORMANCE-sundial.md`), and
none repeats a hotspot finding in `docs/health/findings.json`.

- **I1. Core: argument checks name the parameter.** *Closed by a576eb729a95b8e628a1d2c954d55be5d5f383b1.* Eight core guards throw `new ArgumentException("habitId")` (also
  "directory" and "plant"). The parameter name becomes the message and ParamName stays null (CA2208; `Ledger.cs:149`,
  `:162`, `:182`, `:220`, `:252`, `SaveStore.cs:68`, `SundialState.cs:62`, `Companions.cs:46`). Throw
  `new ArgumentException("<what is wrong>", nameof(habitId))` and keep the exception type. No signature changes, so it
  qualifies under 0012 (c), with a dotnet test that pins ParamName.
- **I2. Sundial: SundialService argument checks name the parameter.** *Closed by c2e05b649c8eeb6455b4d1345c20014d402968ca.* The same CA2208 shape is in the in-place app file
  `SundialService.cs:124` (directory) and `:333` and `:452` (habitId). The fix is the same as I1. The file is one of the
  six that 0012 (c) lets change on dotnet test alone, with a test that pins ParamName and the Unity-only callers listed.
- **I3. Core: GardenDay gets <= and >= beside < and >.** *Closed by 37541f067357d462af87ed3376173e500e38befb.* `GardenDay` implements `IComparable<GardenDay>` and defines `==`,
  `!=`, `<` and `>` (`GardenDay.cs:55-58`), but not `<=` and `>=` (CA1036). A caller has to write `a.Index <= b.Index` or
  `!(a > b)`. Adding the two operators is purely additive (step A, 0004), with a dotnet test.
- **I4. Core: LedgerJson reads and writes the whole Habits and Tends arrays.** *Closed by 85973eaa972e4996ca4b12958e41a75dc79d7de9.* After b7ed44f the row codec has one
  owner, but the loops around it are still typed twice. `SundialSave.cs:69-87` and `TerrariumSave.cs:83-98` read the
  Habits and Tends arrays row by row, and `SundialSave.cs:101-116` and `TerrariumSave.cs:121-136` write them (jscpd,
  118 to 201 tokens each). A pair of LedgerJson methods, `ReadHabits`/`ReadTends` and `WriteHabits`/`WriteTends`, would
  be a step A that lets both saves drop the loops. The SundialSave half then lands under 0012 (c), with the golden row
  tests (f11db8f). It continues findings #12 and #14.
- **I6. Tests: constructor-guard tests discard the new object.** *Closed by e02ce5e534c4fda233e31a0d06fe305f1c986590.* `ArgumentGuardParamNameTests.cs:39`
  (`new DiskSaveIo(directory)`) and `SundialServiceGuardTests.cs:32` (`new SundialService(_clock, directory)`) build an
  object only to see its constructor throw, so CA1806 flags the unused instance (SW-C0854, SW-C0855). Assign each to a
  discard (`_ = new ...`). Test code only. Both files are the guard tests that I1 and I2 added.
- **I7. Core: KeptDays and KeptDaysFrom share one guard-and-loop.** *Closed by 8a875b6e868133030925e63b5b63ce072fbf06fb.* `Ledger.cs:147-154` against
  `:180-187` (jscpd, 8 lines, 113 tokens; SW-C0866). The two methods differ by one condition (`e.Day >= firstDay`). It is
  not in the base run. The guard text is the one line I1 (a576eb7) changed in these two blocks, and it added
  `nameof(habitId)` to each, which plausibly lifted the stretch over the 70-token minimum (not run at a576eb7). A private
  helper removes it; no signature changes, so it qualifies under 0012 (c) with the existing Ledger tests.

## Deliberate, by rule

The reason for each record is in `sweep.json`. In summary:

| Rule | Area | Sites | Reason |
| --- | --- | ---: | --- |
| CA1707 | test | 302 | Tests are named as sentences with underscores, and the name states the rule the test checks. CA1707 targets library API names. Renaming would change every test id and read worse. |
| CA1861 | test | 6 | The array is the expected value of one assertion that runs once. Hoisting it would move the expectation away from the assertion it explains. |
| CA1051 | core 130, app 31 | 161 | Public fields are the plain data shape that the approved surface lists and that Unity-compiled callers read and write. Changing them to properties would change every signature (0012 (c) 1). |
| CA2211 | app | 2 | `SundialService.DevSeedOnFresh` and `FreshReducedMotion` are documented static test seams. `FirstRunRecorder.cs:40-41` and the PlayMode tests set them. |
| CA1305 | core | 6 | They format non-negative schema numbers and counters, where culture changes only the minus sign. `StretchEvent.ToString` is diagnostic. |
| CA1716 | core | 2 | `to` in `ISaveIo.Move` and `Copy` is a keyword only in VB, and nothing here calls them from VB. |
| CA1720 | core | 3 | `JsonKind.String` and `Object` name the JSON types. |
| CA1805 | core | 3 | The explicit 0 sits beside the doc comment that says what 0 means. |
| CA1822 | core | 5 | `TendsArc` and `TendsAs` give three rituals one instance shape. |
| CA1825 | core | 5 | Each runs once per load or per look-back open, not per frame. A commit to TerrariumSave would raise #12's hotspot score (0004). |
| CA2249 | core | 3 | Readability only, and the behaviour is the same. |
| IDE style (0016 to 0305 except 0059 and 0060) | core 101, app 32, test 96 | 229 | Info-level style with no behaviour change. No `.editorconfig` sets a house style, and the core compiles at LangVersion 9.0, the Unity level. Several rewrites (IDE0300, IDE0305, IDE0290) need C# 12. A style-only commit adds churn to the hotspot score (0004). |
| IDE0059 | core 6, app 2, test 6 | 14 | An `out` value the call requires but this caller does not need. `out _` would be cosmetic. In tests, the named local says what the call returns. |
| IDE0060 | core | 1 | **Staged.** `RitualPause.Continue(sessionPaused, ...)` documents that the parameter is not consulted (`RitualPause.cs:63-68`). Removing it changes the surface used by the step B callers of proposal 1. |
| clone | app/core | 1 | The four one-line wrappers that b7ed44f and d46adb2 left in each save. Each passes its own `RowChoices` to LedgerJson. This was covered by findings #12 and #14. |

**Unused code.** No IDE0051 (unused private member) or IDE0052 (unread private member) fired. The unused-code ids that
fired are IDE0059 (14) and IDE0060 (1), above.

## Name grep: members with no reference found, and members only tests name

A name is not proof of use, and the absence of a name is not proof of disuse. Unity reaches code through serialization,
reflection and scene bindings. Core has no MonoBehaviour and no field that Unity binds, and its Json codec reads by
explicit key, not by reflection. So a core member is reached only through compiled calls, which the grep sees in
`apps/`. Common names (Id, Count) always match something, so the grep can only find too few unused members, never too
many. Nothing below is called unused.

No reference found beyond the declaration (3):

- `Garden.DayNumber(DateTime)` (`Garden.cs:161`): **staged.** Removing it changes the approved surface, a step B.
- `ThreeGoodThings.HasDrop(int)` (`ThreeGoodThings.cs:20`): **staged**, the same reason.
- `Season.LockedWeek` (`Season.cs:15`): deliberate. It is a documented constant that names the locked look of week 1.

Referenced only from test code (29):

- **Staged.** These are step A core waiting for its step B (0012 (a) item 4):
  - Proposal 1 (9d09227): `RitualPause` with `Latch`, `OfferBack`, `SourceSwapped` and `RitualPauseEvent`.
  - Proposal 5 (fd85965): `PacedHand` with `Duty`, `BreathProgress` and `Garden.RitualDoneOn`.
  - The closed `SettingsRegistry` with `Define`, `IsStored` and `ReadJson` (c7c555b). No app settings code adopts it yet.
- **Staged.** These have no app caller, and removing them changes the surface:
  - `Garden.Serialize` and `Deserialize`. The findings.json notes already record these from the scan-sweep round notes;
    they are cited here, not raised again.
  - The five `Garden.Season*` convenience properties. App code calls `Season` directly.
- **Deliberate.** These are test drivers and rule witnesses that document the rule:
  - `SimulatedHand` and `SimulatedHand.Drive`.
  - `Season.FrondsAtWeek` and `FrondsAtEndOfWeek`. The latter is also used by the PlayMode `SeasonPlaybackTests.cs:41`.
  - `RimScrub.DegreesPerHour` and `AllowsWrite`.
  - `SundialRules.MaxTiles`.
  - `LedgerJson.HabitKnown` and `TendKnown`, the known-key contract of the codec (4f6ba63).

## Duplication

jscpd analysed 39 files: the 33 core Runtime files and the six in-place files. It counted 6,203 lines and 63,980
tokens, and found 4 clones: 51 duplicated lines (0.82%) and 683 tokens (1.07%). Every clone pair is `SundialSave.cs`
against `TerrariumSave.cs`. Three are the array loops (idea I4). One is the LedgerJson wrappers (deliberate, covered by
b7ed44f and findings #12 and #14). No clone is inside core alone.

## Not analysed: Unity-only C#

The Unity-only C# under `apps/*/Assets` (all but the six in-place files) is outside `GardenVR.sln`, and so are
`shared/packages/com.gardenvr.*/Editor` and `com.gardenvr.capture`. An analyzer, format or clone run cannot read that
code without a Unity compile, so it was not analysed. It is **staged for the first Unity session, 0012 (a)**, as one
ledger line (`SW-C0842`), not one finding per file. No Unity run was made, and no Unity compile is claimed.

Two bare-name `ArgumentException` guards (the CA2208 shape of I1 and I2) sit in this unanalysed code and are listed in
`SW-C0842` as `knownSites`. Both are staged as step B under 0012 (a):

- `apps/terrarium/Assets/Scripts/Ritual/GardenService.cs:69`
- `shared/packages/com.gardenvr.capture/Runtime/PlaybackHarness.cs:45`

## Asset half

The census findings of the milestone-3 lean sweep, classified into the same ledger (`docs/health/sweep.json`, records
`SW-A0001` to `SW-A0057`, `half` "assets"). **Unclassified: 0 in the asset half, 0 in the code half, 0 overall** (924
records after the closure re-run: 867 code, 57 assets).

- **Measured at:** `8670dd1ab3ee88b5cdccf5509272c16725bc0406` (decisions: 0013), with `node tools/assets/census.mjs --rev 8670dd1 --out <temp dir outside the repo>`.
  The run printed `tracked 2378 files 251368912 bytes; png 410 files 222942403 bytes` and `unclassified duplicate groups: 0`.
  `git status` showed nothing under `docs/assets/` or `docs/budgets/` afterwards.
- **Same as M2-READING.** Every class count equals the "After, `c3b8e3e`" table of `docs/assets/M2-READING-2026-10-07.md`
  (duplicate-blob 25, over-1024 7, png-16-bit 2, no-reference-found 21, source-in-assets 0, resources-ballast 0), and the
  bytes equal too. No difference to report. The census also printed 8 groups whose hand verdict differs from its keep rule
  and 2 whose hand risk differs from the census; both are existing census notes, and the hand verdict in
  `docs/assets/duplicates.json` is the one used here.
- **Not re-counted.** The lossless ledger (`LOSSLESS.md`, 0011) and A4 provenance (`A4-PROVENANCE.md`) are separate
  ledgers. Each has one scope record (`SW-A0056`, `SW-A0057`), like `SW-C0842` in the code half.

### Per class

All 55 findings come from `node tools/assets/census.mjs --rev 8670dd1`. A file can carry several classes, so the rows
overlap (the 4 plates are in three classes).

| Class | Findings | Bytes (census "saved") | Fixed | Idea | Deliberate | of which staged |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| duplicate-blob | 25 | 12655460 (12.07 MiB) | 0 | 0 | 25 (16 keep, 9 cut) | 9 |
| over-1024 | 7 | 9859922 (9.40 MiB) | 0 | 0 | 7 (4 plates, 3 script inputs) | 4 |
| png-16-bit | 2 | 2098328 (2.00 MiB) | 0 | 2 | 0 | 0 |
| no-reference-found | 21 | 17079142 (16.29 MiB) | 0 | 0 | 21 | 6 |
| source-in-assets | 0 | 0 | 0 | 0 | 0 | 0 |
| resources-ballast | 0 | 0 | 0 | 0 | 0 | 0 |
| census total | 55 | 41692792 (39.76 MiB) | 0 | 2 | 53 | 19 |
| scope (lossless, A4) | - | - | 0 | 0 | 2 | 0 |
| **Asset half** | **57** | | **0** | **2** | **55** | **19** |

Nothing was fixed: this run writes only `docs/health/`, and every change to an asset is a `visible-change` that needs a
render compare, which needs a Unity licence this machine lacks.

### Deliberate, by rule

- **duplicate-blob, keep (16 groups, 4625470 bytes).** Each has a keep reason in `docs/assets/duplicates.json`
  (source pairs under `Art/Source`, seed pairs under `shared/assets/seed-textures`, halo masks that are pixel-equal by
  accident). The record cites that reason.
- **duplicate-blob, cut (9 groups, 9613545 bytes), staged.** Decided as cut and not cut: the 4 plates, `moss_fuzz`,
  `moss_tile`, `moss_band`, `moss_top` and `sprig.fbx`. Cited: 0012 (a) item 2 (the cut) and item 5 (the render
  compare).
- **over-1024, the 4 plates, staged.** They import at 1824 px under an effective cap of 2048. 0012 (a) item 3 and 0013 (c):
  they leave the Quest build in Phase 2 and are not downscaled.
- **over-1024, the 3 script inputs.** `dial_face`, `dial_face_s1` (cap 1024) and `condensation` (cap 1024) are read by
  scripts at full size, and the census reads the effective import cap 1024 from their `.meta`.
- **no-reference-found, all 21.** A search of `.cs`, `.py`, `.mjs`, `.shader`, `.json` and `.txt` found, for every one, a
  script that loads or composes the file by name. The census guid scan cannot see that. The file:line is in each record:
  `tools/blender/terrarium_hero.py:1097-1100, :1235-1241` (6 terrarium textures: both fern albedos and emissions,
  `moss_tuft`, `cork`, `soil`), `JarSetup.cs:36` (`moss_fuzz`, `moss_tile`, `moss_band`, `moss_top`), `DialSetup.cs:39`
  (sundial `soil`), `DuskRitualController.cs:538` (3 sparkle rings), `FirstRunWizard.cs:425` (3 packets),
  `SceneSetup.cs` (`plate-dial`, `plate-jar`) and `tools/fidelity/sweeps/jar-moss.json:11` (`moss-repaint`, a variant
  that a sweep binds by name). The 6 that 0012 (a) item 2 cuts (the two plates and four moss textures) are staged. None is
  left for an owner call, so no idea is filed for this class. The owner may still judge a file unwanted (for example the
  `moss-repaint` variant); nothing was deleted or moved.

### Ideas

- **I5. Assets: two 16-bit pngs, an owner call on requantising.** *Closed by decision 0014 (c) and (d).* `SW-A0033`
  (`moss-repaint.png`) is deliberate and staged by 0014 (d): item 8 of 0012 (a), cut at the first Unity session with item 2.
  `SW-A0034` (`dial_paper.png`) is deliberate and staged by 0014 (c): item 7 of 0012 (a), requantised only if the render
  compare shows no change. The paragraph below is the idea as filed; the per-class table above is as measured at 8670dd1,
  before 0014, so its "Idea" column reads 2 where the ledger now reads 0 and its "staged" column 19 where the ledger reads 21. `apps/terrarium/Assets/Art/Textures/moss-repaint.png`
  (4110297 bytes on disk; census estimate to save 2053436) and `apps/sundial/Assets/Art/Textures/dial_paper.png`
  (estimate 44892). Both are `visible-change` at the census. 0011's measured arithmetic covers a lossless rewrite (moss-repaint: 4106871 to
  3872607 bytes, which adds 3872607 to history to save 234264; dial_paper: under the threshold), not a requantise to 8
  bits, and the `.meta` of both imports through `textureFormat -1`, `textureCompression 0`, so neither gives a reason to
  keep 16 bits. Neither is named in 0012 or 0013, so neither is staged. The owner decides per file.

No other idea: the rule for `no-reference-found` files with no loader found had zero cases.

### Check

`git diff --name-only 8670dd1..HEAD` lists `docs/health/sweep.json` and `docs/health/SWEEP.md` only. The 842 code records
are byte-identical as parsed JSON (`JSON.stringify` of the `half: "code"` findings before and after, equal). The three
`counts` blocks (`counts`, `counts.perHalf.code`, `counts.perHalf.assets`) show `unclassified` 0.

## Closure re-run (37541f0)

The closing run of the sweep. It re-ran the code-half tools at `37541f067357d462af87ed3376173e500e38befb` (I3 merged, after
I1 a576eb7, I2 c2e05b6 and I4 85973ea), closed I1 to I4 and I5 in the ledger, and classified what the re-run showed that the
ledger did not. It edits only `docs/health/SWEEP.md` and `docs/health/sweep.json`. No code, test, config or other doc changed, no
analyzer config, `.editorconfig`, `Directory.Build.props`, csproj, package.json or lock file was touched, and every tool wrote its
output to a temp directory outside the repo. Same SDK (9.0.308) and jscpd 4.3.0 as the base run. No Unity run was made, and no
Unity compile is claimed.

### Commands at 37541f0

| # | Command | Exit | Result |
| --- | --- | ---: | --- |
| 1a | `dotnet build shared/core-dotnet/GardenVR.sln --no-incremental` | 0 | 0 warnings, 0 errors |
| 1b | `dotnet build shared/core-dotnet/GardenVR.sln --no-incremental -p:AnalysisLevel=latest-recommended` | 0 | 512 warnings (512 unique sites), 0 errors |
| 2a | `dotnet format analyzers shared/core-dotnet/GardenVR.sln --verify-no-changes --severity info --report <temp>/fmt-analyzers` | 2 | made no change |
| 2b | `dotnet format style shared/core-dotnet/GardenVR.sln --verify-no-changes --severity info --report <temp>/fmt-style` | 2 | made no change |
| 2c | Name grep for the four members the Core approved surface gained since 00c2e4c: `LedgerJson.ReadHabits`, `ReadTends`, `WriteHabits`, `WriteTends`. Same method as the base run (whole word, every tracked `.cs` under `apps/` and `shared/`, comments stripped, string literals kept). The operators are left out, as 2c does. | - | All four are referenced from `SundialSave.cs` (app) and `TerrariumSave.cs` (core) as well as from `LedgerJsonTests.cs`: ReadHabits and ReadTends 5 occurrences each (2 in tests), WriteHabits and WriteTends 6 each (3 in tests). No finding, no record. |
| 3 | `npx --yes jscpd@4 --min-tokens 70 --format csharp --reporters json,console --output <temp>/jscpd-out --absolute shared/packages/com.gardenvr.core/Runtime` plus the six in-place files, run from a temp directory | 0 | 39 files, 6161 lines, 63540 tokens; 2 clones, 14 duplicated lines (0.23%), 257 tokens (0.4%) |
| test | `dotnet test shared/core-dotnet` | 0 | 335 of 335 passed (Core 285, SundialModel 36, TerrariumModel 14), 0 failed |

The 512 unique build sites are 190 production and 322 test. The default level still reports no warning, and no compiler (CS) warning.

### Build rules, before and after

Default level (1a): 0 warnings after 1a99071 (the base after the test fix), 0 at 37541f0.

`latest-recommended` (1b): 510 after 1a99071 (308 test + 202 production), 512 at 37541f0 (322 test + 190 production).

| Rule | After 1a99071 | At 37541f0 | Change |
| --- | ---: | ---: | ---: |
| CA1707 | 302 | 313 | +11 |
| CA1051 | 161 | 161 | 0 |
| CA1861 | 6 | 7 | +1 |
| CA1825 | 5 | 5 | 0 |
| CA1305 | 6 | 6 | 0 |
| CA1822 | 5 | 5 | 0 |
| CA1805 | 3 | 3 | 0 |
| CA1720 | 3 | 3 | 0 |
| CA2249 | 3 | 3 | 0 |
| CA1716 | 2 | 2 | 0 |
| CA2211 | 2 | 2 | 0 |
| CA2208 | 11 | 0 | -11 |
| CA1036 | 1 | 0 | -1 |
| CA1806 | 0 | 2 | +2 |
| **Total** | 510 | 512 | +2 |

The predicted drops hold: **CA2208 production sites 11 to 0** (8 by a576eb7, 3 by c2e05b6) and **CA1036 1 to 0** (37541f0). The test side
rose by 14: the guard tests that I1 and I2 added and the I4 codec tests add 11 CA1707 sites, 2 CA1806 and 1 CA1861 (all recorded below).
The production count fell from 202 to 190, exactly the 12 sites fixed, and each other production rule is unchanged.

Format (2a, 2b): per (rule, file), the re-run site count was compared with the not-fixed records for the pair. Four rules differ:

| Rule | Ledger (not fixed) | At 37541f0 | Why |
| --- | ---: | ---: | --- |
| IDE0028 | 7 | 9 | +2 in LedgerJsonTests.cs (the I4 codec tests) |
| IDE0090 | 54 | 56 | +2, one each in the I1 and I2 guard test files |
| IDE0300 | 35 | 30 | -5, the five `new T[0]` sites that 1a99071 rewrote to `Array.Empty<T>()` |
| IDE0301 | 0 | 5 | +5, the same five sites, now flagged as an empty collection |

### jscpd, before and after

| | Base run (00c2e4c) | At 37541f0 |
| --- | ---: | ---: |
| Clones | 4 | 2 |
| Duplicated lines | 51 (0.82%) | 14 (0.23%) |
| Duplicated tokens | 683 (1.07%) | 257 (0.4%) |
| Files, lines, tokens | 39, 6203, 63980 | 39, 6161, 63540 |

The delivery predicted 4 clones to 1. The re-run found 2, and neither is one of the four base clones. SW-C0838 to SW-C0840 (the
array loops) are gone, as I4 predicted. SW-C0841 (the four one-line wrappers) is gone too, because 85973ea deleted the wrappers and
the saves now call `LedgerJson.ReadHabits` and `ReadTends` directly. The two clones that remain are new records (SW-C0866 and
SW-C0867, below). The (rule, file) pair `clone`, `SundialSave.cs` has one not-fixed record (SW-C0841) and one site at 37541f0, so a
count comparison alone would call it line drift. The site is a different clone (8 lines, 144 tokens at `SundialSave.cs:69-76`
against `TerrariumSave.cs:83-90`, not 23 lines, 246 tokens at `:162-184`), so it got a record, and SW-C0841 is listed as vanished.

### New records (25)

Each was read at its file's blob at 37541f0, in the same fields as the existing records. None is `fixed`.

| Id | Rule | File:line | Class | Basis |
| --- | --- | --- | --- | --- |
| SW-C0843 | CA1707 | `GardenVR.Core.Tests/LedgerJsonTests.cs:128` | deliberate | test naming convention (CA1707 rule) |
| SW-C0844 | CA1707 | `GardenVR.Core.Tests/LedgerJsonTests.cs:140` | deliberate | test naming convention (CA1707 rule) |
| SW-C0845 | CA1707 | `GardenVR.Core.Tests/LedgerJsonTests.cs:149` | deliberate | test naming convention (CA1707 rule) |
| SW-C0846 | CA1707 | `GardenVR.Core.Tests/LedgerJsonTests.cs:158` | deliberate | test naming convention (CA1707 rule) |
| SW-C0847 | CA1707 | `GardenVR.Core.Tests/LedgerJsonTests.cs:173` | deliberate | test naming convention (CA1707 rule) |
| SW-C0848 | CA1707 | `GardenVR.Core.Tests/ArgumentGuardParamNameTests.cs:21` | deliberate | test naming convention (CA1707 rule) |
| SW-C0849 | CA1707 | `GardenVR.Core.Tests/ArgumentGuardParamNameTests.cs:37` | deliberate | test naming convention (CA1707 rule) |
| SW-C0850 | CA1707 | `GardenVR.Core.Tests/ArgumentGuardParamNameTests.cs:43` | deliberate | test naming convention (CA1707 rule) |
| SW-C0851 | CA1707 | `GardenVR.Core.Tests/ArgumentGuardParamNameTests.cs:53` | deliberate | test naming convention (CA1707 rule) |
| SW-C0852 | CA1707 | `GardenVR.SundialModel.Tests/SundialServiceGuardTests.cs:30` | deliberate | test naming convention (CA1707 rule) |
| SW-C0853 | CA1707 | `GardenVR.SundialModel.Tests/SundialServiceGuardTests.cs:38` | deliberate | test naming convention (CA1707 rule) |
| SW-C0854 | CA1806 | `GardenVR.Core.Tests/ArgumentGuardParamNameTests.cs:39` | idea | idea I6 |
| SW-C0855 | CA1806 | `GardenVR.SundialModel.Tests/SundialServiceGuardTests.cs:32` | idea | idea I6 |
| SW-C0856 | CA1861 | `GardenVR.Core.Tests/LedgerJsonTests.cs:164` | deliberate | expected value of one assertion (CA1861 rule) |
| SW-C0857 | IDE0028 | `GardenVR.Core.Tests/LedgerJsonTests.cs:133` | deliberate | IDE style rule, test |
| SW-C0858 | IDE0028 | `GardenVR.Core.Tests/LedgerJsonTests.cs:145` | deliberate | IDE style rule, test |
| SW-C0859 | IDE0090 | `GardenVR.Core.Tests/ArgumentGuardParamNameTests.cs:10` | deliberate | IDE style rule, test |
| SW-C0860 | IDE0090 | `GardenVR.SundialModel.Tests/SundialServiceGuardTests.cs:13` | deliberate | IDE style rule, test |
| SW-C0861 | IDE0301 | `GardenVR.Core.Tests/WeekDialTests.cs:115` | deliberate | IDE style rule, test; the Array.Empty<T>() of 1a99071 |
| SW-C0862 | IDE0301 | `GardenVR.Core.Tests/SundialTests.cs:32` | deliberate | IDE style rule, test; the Array.Empty<T>() of 1a99071 |
| SW-C0863 | IDE0301 | `GardenVR.Core.Tests/SaveCrashWindowTests.cs:73` | deliberate | IDE style rule, test; the Array.Empty<T>() of 1a99071 |
| SW-C0864 | IDE0301 | `GardenVR.Core.Tests/SaveRestoreTests.cs:241` | deliberate | IDE style rule, test; the Array.Empty<T>() of 1a99071 |
| SW-C0865 | IDE0301 | `GardenVR.Core.Tests/RimScrubTests.cs:146` | deliberate | IDE style rule, test; the Array.Empty<T>() of 1a99071 |
| SW-C0866 | clone | `core/Common/Ledger.cs:147` | idea | idea I7 |
| SW-C0867 | clone | `sundial/SundialSave.cs:69` | deliberate | successor of the SW-C0841 wrappers (clone rule) |

By class: 11 CA1707, 1 CA1861 and 9 IDE-style test sites (2 IDE0028, 2 IDE0090, 5 IDE0301) are deliberate by rule, and so is 1 clone
(SW-C0867). Three are ideas: SW-C0854 and SW-C0855 (CA1806, idea I6) and SW-C0866 (the Ledger.cs clone, idea I7). The five IDE0301
records are not caused by I1 to I4: 1a99071 wrote `Array.Empty<T>()` at the five CA1825 test sites, which IDE0300 then stopped flagging
and IDE0301 began to.

### Vanished sites

Explained by step 1 (the records are now `fixed`):

| Rule | Sites | Removed by |
| --- | --- | --- |
| CA2208 | 8 (Ledger.cs 5, SaveStore.cs 1, SundialState.cs 1, Companions.cs 1) | a576eb729a95b8e628a1d2c954d55be5d5f383b1 (I1) |
| CA2208 | 3 (SundialService.cs) | c2e05b649c8eeb6455b4d1345c20014d402968ca (I2) |
| CA1036 | 1 (GardenDay.cs) | 37541f067357d462af87ed3376173e500e38befb (I3) |
| clone | 3 (SW-C0838 to SW-C0840) | 85973eaa972e4996ca4b12958e41a75dc79d7de9 (I4) |

Not explained by step 1. These records are unchanged (they keep their base line and blob). Each pair is listed with the commit that
removed the site:

| Pair | Record | Removed by |
| --- | --- | --- |
| IDE0300, SundialTests.cs | SW-C0782 (line 30) | 1a990718f551c88d34db7f963f55f59fb883c8e7 |
| IDE0300, SaveCrashWindowTests.cs | SW-C0776 (line 71) | 1a990718f551c88d34db7f963f55f59fb883c8e7 |
| IDE0300, SaveRestoreTests.cs | SW-C0778 (line 239) | 1a990718f551c88d34db7f963f55f59fb883c8e7 |
| IDE0300, WeekDialTests.cs | SW-C0794 (line 113) | 1a990718f551c88d34db7f963f55f59fb883c8e7 |
| IDE0300, RimScrubTests.cs | SW-C0774 (line 144) | 1a990718f551c88d34db7f963f55f59fb883c8e7 |
| clone, SundialSave.cs | SW-C0841 (23 lines, 246 tokens, `:162-184`) | 85973eaa972e4996ca4b12958e41a75dc79d7de9 (the wrappers it described were deleted) |

The base ledger read the five IDE0300 pairs before 1a99071 rewrote `new T[0]` to `Array.Empty<T>()` at the lines named in "Fixed (1a99071)",
so those records describe sites the fix commit removed.

### Counts after the closure

| Block | Records | Fixed | Idea | Deliberate | Staged | Unclassified |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| code | 867 | 66 | 3 | 798 | 24 | 0 |
| assets | 57 | 0 | 0 | 57 | 21 | 0 |
| **all** | **924** | **66** | **3** | **855** | **45** | **0** |

Before the new records the code half read fixed 66, idea 0, deliberate 776 (24 staged) and the asset half deliberate 57 (21 staged),
idea 0, as expected. The 25 new records leave the staged count unchanged. The machine-readable record of this run is the `closure`
object in `sweep.json` (commit, the exit code of each tool, and the per-rule counts).

### Check

```
existing records 899: 881 identical (JSON.stringify per id), 18 of the 18 named changed only in their step 1 fields, 0 unexpected, 0 missing
added records 25: SW-C0843 to SW-C0867; order of existing ids kept: true
counts: records 924, fixed 66, idea 3, deliberate 855, staged 45, unclassified 0; counts block equals the recount: true
counts.perHalf.code: records 867, fixed 66, idea 3, deliberate 798, staged 24, unclassified 0; counts block equals the recount: true
counts.perHalf.assets: records 57, fixed 0, idea 0, deliberate 57, staged 21, unclassified 0; counts block equals the recount: true
perTool equals the recount: true
ideas I1 to I4 closed with a full SHA, I5 closed by 0014: true ; ideas listed: I1 I2 I3 I4 I5 I6 I7
```

The check is a node script run in a temp directory against `git show HEAD:docs/health/sweep.json` (the file as it was before this
commit) and the working file. `git diff --name-only 37541f0..HEAD` lists `docs/health/SWEEP.md` and `docs/health/sweep.json` only.

## Second closure re-run (8a875b6e868133030925e63b5b63ce072fbf06fb)

Delivers I6 (`e02ce5e534c4fda233e31a0d06fe305f1c986590`, test code only) and I7 (`8a875b6e868133030925e63b5b63ce072fbf06fb`, core `Ledger.cs`: `KeptDays(habitId)` now returns
`KeptDaysFrom(habitId, int.MinValue)`; no signature, no other member and no doc comment changed; it joins the compile debt of
0012 (a) item 1), then re-runs the code-half tools at the I7 commit and closes the three records. The run edits `Ledger.cs`, the two
guard test files, `docs/health/SWEEP.md` and `docs/health/sweep.json` only. No analyzer config, `.editorconfig`,
`Directory.Build.props`, csproj, package.json or lock file was touched; every tool wrote to a temp directory outside the repo. Same
SDK (9.0.308) and jscpd 4.3.0 as before. No Unity run was made, and no Unity compile is claimed.

### Commands at 8a875b6

| # | Command | Exit | Result |
| --- | --- | ---: | --- |
| 1a | `dotnet build shared/core-dotnet/GardenVR.sln --no-incremental` | 0 | 0 warnings, 0 errors |
| 1b | `dotnet build shared/core-dotnet/GardenVR.sln --no-incremental -p:AnalysisLevel=latest-recommended` | 0 | 510 warnings (510 unique sites: 190 production, 320 test), 0 errors |
| 2a | `dotnet format analyzers shared/core-dotnet/GardenVR.sln --verify-no-changes --severity info --report <temp>/fmt-analyzers` | 2 | made no change |
| 2b | `dotnet format style shared/core-dotnet/GardenVR.sln --verify-no-changes --severity info --report <temp>/fmt-style` | 2 | made no change |
| 3 | `npx --yes jscpd@4 --min-tokens 70 --format csharp --reporters json,console --output <temp>/jscpd-out --absolute shared/packages/com.gardenvr.core/Runtime` plus the six in-place files, run from a temp directory | 0 | 39 files, 6151 lines, 63421 tokens; 1 clone, 7 duplicated lines (0.11%), 144 tokens (0.23%) |
| test | `dotnet test shared/core-dotnet` | 0 | 335 of 335 passed (Core 285, SundialModel 36, TerrariumModel 14), 0 failed; no test added, removed or renamed |

### Per rule, before and after

Closure head (37541f0) against this head. Every other rule is unchanged from the closure table above, and the default level stays at 0.

| Rule | At 37541f0 | At 8a875b6 | Change |
| --- | ---: | ---: | ---: |
| CA1806 | 2 | 0 | -2 |
| clone (jscpd) | 2 | 1 | -1 |
| **Build total (1b)** | 512 | 510 | -2 |
| of which test | 322 | 320 | -2 |
| of which production | 190 | 190 | 0 |

### jscpd, before and after

| | At 37541f0 | At 8a875b6 |
| --- | ---: | ---: |
| Clones | 2 | 1 |
| Duplicated lines | 14 (0.23%) | 7 (0.11%) |
| Duplicated tokens | 257 (0.4%) | 144 (0.23%) |
| Files, lines, tokens | 39, 6161, 63540 | 39, 6151, 63421 |

The one clone left is `SundialSave.cs:69-76` against `TerrariumSave.cs:83-90` (SW-C0867, deliberate). The Ledger.cs clone (SW-C0866) is gone.

### New or vanished records

New records: none. Every site the re-run shows is covered by an existing record. Vanished sites, all explained by this run:
SW-C0854 and SW-C0855 (CA1806, removed by `e02ce5e534c4fda233e31a0d06fe305f1c986590`) and SW-C0866 (clone, removed by `8a875b6e868133030925e63b5b63ce072fbf06fb`). The three records are now `fixed`
with those commits. The other production sites in `Ledger.cs` moved up by the lines I7 removed; their records keep the line they were read at.

App Master, wake 8f68c68e: SW-C0867 is confirmed deliberate (each save keeps its own root fields; the shared lines are calls to the one owner, LedgerJson), and the five vanished IDE0300 records stay as written.

### Counts after this run

| Block | Records | Fixed | Idea | Deliberate | Staged | Unclassified |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| code | 867 | 69 | 0 | 798 | 24 | 0 |
| assets | 57 | 0 | 0 | 57 | 21 | 0 |
| **all** | **924** | **69** | **0** | **855** | **45** | **0** |

The machine-readable record is the `closure2` object in `sweep.json` (same shape as `closure`; `closure` is unchanged). Its
`perRule` compares the closure head (`closureHead`) with this head (`head`).

### Check

```
existing records 924: 921 identical (JSON.stringify per id), 3 of the 3 named changed only in class and commit, 0 unexpected, 0 missing; added 0; order kept: true
closure unchanged: true
counts all: {"records":924,"fixed":69,"idea":0,"deliberate":855,"staged":45,"unclassified":0}; block equals the recount: true
counts code: {"records":867,"fixed":69,"idea":0,"deliberate":798,"staged":24,"unclassified":0}; block equals the recount: true
counts assets: {"records":57,"fixed":0,"idea":0,"deliberate":57,"staged":21,"unclassified":0}; block equals the recount: true
ideas closed: I1=a576eb7 I2=c2e05b6 I3=37541f0 I4=85973ea I5=decisio I6=e02ce5e I7=8a875b6
```

The check is a node script run in a temp directory against `git show HEAD:docs/health/sweep.json` (the file before the ledger commit) and the working file.
